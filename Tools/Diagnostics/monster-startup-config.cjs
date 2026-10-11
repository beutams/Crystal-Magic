// Read-only audit / patch producer. Apply the emitted patch through the file editor.
const fs = require('fs');
const path = 'Assets/Res/Data/StateScriptDataTable.json';
const source = fs.readFileSync(path, 'utf8').replace(/^\uFEFF/, '');
const data = JSON.parse(source);
const trees = JSON.parse(fs.readFileSync('Assets/Res/Data/BehaviorTreeDataTable.json', 'utf8').replace(/^\uFEFF/, '')).Rows;
const profiles = JSON.parse(fs.readFileSync('Assets/Res/Data/UnitAnimationProfileDataTable.json', 'utf8').replace(/^\uFEFF/, '')).Rows;
const spans = new Map(), stack = [];
// Record authored object spans without reserializing the surrounding data table.
for (let i = 0; i < source.length; i++) {
  const c = source[i];
  if (c === '{' || c === '[') stack.push({ start: i });
  else if (c === '}' || c === ']') {
    const item = stack.pop();
    for (const key of item.keys || []) {
      if (!spans.has(key)) spans.set(key, []);
      spans.get(key).push({ start: item.start, end: i + 1 });
    }
  } else if (c === '"') {
    const start = i++;
    while (i < source.length && source[i] !== '"') { if (source[i] === '\\') i++; i++; }
    const key = source.slice(start + 1, i);
    if (key !== 'Guid' && key !== 'OutputNodeGuid') continue;
    const match = /^\s*:\s*"([^"]+)"/.exec(source.slice(i + 1));
    if (match) (stack[stack.length - 1].keys ||= []).push(key + ':' + match[1]);
  }
}
const edits = [], report = [];
const spanFor = (key) => {
  const matches = spans.get(key);
  if (matches?.length !== 1) throw new Error('Ambiguous object: ' + key);
  return matches[0];
};
const format = (value) => JSON.stringify(value, null, 2).replace(/\n/g, '\n            ');
const replace = (span, value) => edits.push({ ...span, value });
for (const tree of trees) {
  const row = data.Rows.find(r => r.Id === tree.UnitDataId);
  const locomotion = row?.Graphs.find(g => g.Name === 'Locomotion');
  if (!locomotion) continue;
  const prefix = locomotion.Guid.replace(/_locomotion$/, '');
  const existing = new Set(row.Graphs.flatMap(g => g.Nodes).filter(n => n.SetterKey === 'unit.variables.set' && n.Guid.includes('_init_')).map(n => n.Key));
  const keys = [...new Set([...JSON.stringify(row).matchAll(/"(ai\.(?:attack\d+|heal)\.coolingDown)"/g)].map(m => m[1]))].sort();
  const additions = keys.filter(k => !existing.has(k)).map(key => ({ key, literal: { Type: 1, Bool: false } }));
  if (row.Id === 16) {
    for (const [key, value] of Object.entries({ 'ai.bat.dashCoolingDown': false, 'ai.bat.dashing': false, 'ai.bat.comboReady': false, 'ai.bat.randomFlying': true }))
      if (!existing.has(key)) additions.push({ key, literal: { Type: 1, Bool: value } });
    if (!existing.has('ai.bat.wander.roll')) additions.push({ key: 'ai.bat.wander.roll', literal: { Type: 2, Int: 0 } });
    if (!existing.has('ai.bat.wander.direction')) additions.push({ key: 'ai.bat.wander.direction', expression: { Kind: 1, GetterKey: 'unit.random.direction2', Inputs: [{ Kind: 0, Literal: { Type: 2, Int: 0 }, Inputs: [] }] } });
  }
  if (additions.length) {
    const nodes = additions.map((item, index) => ({
      Type: 'SetValue', Guid: prefix + '_init_' + item.key.slice(3).replace(/\./g, '_'),
      EditorPosition: { x: 580 + index * 350, y: -220 },
      SetterKey: 'unit.variables.set', SourceTarget: 0, Key: item.key,
      Value: item.expression || { Kind: 0, Literal: item.literal, Inputs: [] }, Values: []
    }));
    for (const n of nodes) if (locomotion.Nodes.some(old => old.Guid === n.Guid)) throw new Error('Duplicate node ' + n.Guid);
    const entry = spanFor('Guid:' + locomotion.EntryNodeGuid);
    replace(entry, source.slice(entry.start, entry.end) + ',\n            ' + nodes.map(format).join(',\n            '));
    const edgeSpan = spanFor('OutputNodeGuid:' + locomotion.EntryNodeGuid);
    const originalEdge = JSON.parse(source.slice(edgeSpan.start, edgeSpan.end));
    const edges = nodes.map((node, index) => ({ OutputNodeGuid: node.Guid, OutputPortName: 'Out', InputNodeGuid: nodes[index + 1]?.Guid || originalEdge.InputNodeGuid, InputPortName: 'In' }));
    replace(edgeSpan, [{ ...originalEdge, InputNodeGuid: nodes[0].Guid }, ...edges].map(format).join(',\n            '));
  }
  const names = new Set(profiles.find(p => p.UnitDataId === row.Id)?.Animations.map(a => a.Name));
  const renamed = [];
  for (const node of row.Graphs.flatMap(g => g.Nodes)) {
    if (!['unit.animation.play', 'unit.animation.setName'].includes(node.SetterKey)) continue;
    const name = node.Value?.Literal?.String;
    if (!name || names.has(name)) continue;
    const replacement = row.Id === 16 && ['Idle', 'Walk'].includes(name) ? 'Flying'
      : name === 'Walk' && names.has('Move') ? 'Move'
      : row.Id === 20 && name === 'Walk' && names.has('Walk01') ? 'Walk01' : null;
    if (!replacement || !names.has(replacement)) throw new Error('Unmapped animation: ' + tree.Name + '/' + name);
    const span = spanFor('Guid:' + node.Guid), raw = source.slice(span.start, span.end);
    replace(span, raw.replace(new RegExp('("String"\\s*:\\s*)"' + name + '"'), '$1"' + replacement + '"'));
    renamed.push({ node: node.Guid, from: name, to: replacement });
  }
  if (additions.length || renamed.length) report.push({ id: row.Id, name: tree.Name, initialized: additions.map(a => a.key), renamed });
}
if (process.argv.includes('--patch')) {
  let patch = '*** Begin Patch\n*** Update File: ' + path + '\n';
  for (const edit of edits.sort((a, b) => a.start - b.start)) {
    const suffix = source[edit.end] === ',' ? ',' : '';
    const old = source.slice(edit.start, edit.end).replace(/\r\n/g, '\n') + suffix;
    const indent = source.slice(source.lastIndexOf('\n', edit.start) + 1, edit.start);
    if (!/^\s*$/.test(indent)) throw new Error('Unexpected compact target');
    patch += '@@\n' + (indent + old).split('\n').map(l => '-' + l).join('\n') + '\n';
    patch += (indent + edit.value.replace(/\r\n/g, '\n') + suffix).split('\n').map(l => '+' + l).join('\n') + '\n';
  }
  process.stdout.write(patch + '*** End Patch\n');
} else process.stdout.write(JSON.stringify(report, null, 2));

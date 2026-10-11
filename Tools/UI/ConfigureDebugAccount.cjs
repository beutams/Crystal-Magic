// Reuse the existing mechanical YAML authoring helper without running its settings mutations.
const fs = require('fs');
const vm = require('vm');
const helper = fs.readFileSync('Tools/UI/ConfigureDisplaySettings.cjs', 'utf8').split('const setting =')[0];
if (!helper.includes('class Prefab')) throw Error('UI prefab authoring helper changed.');
vm.runInNewContext(helper.replace('9181100000000000000n', '9181300000000000000n') + `
const p = new Prefab('Assets/Res/UI/DebugUI.prefab');
if ([...p.nodes.values()].some(n => n.path === 'Content/Account')) throw Error('Account page already exists.');
p.clone(p, 'Content/UnitSpawner', 'Content', 'Account', false);
p.clone(p, 'Content/UnitSpawner/Title', 'Content/Account', 'Title');
p.clone(p, 'Content/UnitSpawner/SelectedUnit', 'Content/Account', 'Info');
p.clone(p, 'Content/UnitSpawner/SelectedUnit', 'Content/Account', 'Status');
p.clone(p, 'Content/UnitSpawner/SelectedUnit', 'Content/Account', 'LocalIdLabel');
p.clone(p, 'Content/UnitInspector/Editor', 'Content/Account', 'LocalId');
for (const [name, title, x, width] of [
  ['UseLocal', '使用测试账号', 26, 246], ['UseSteam', '使用 Steam', 292, 246],
  ['ReturnMenu', '保存并返回主菜单', 558, 318]]) {
  p.clone(p, 'Content/UnitSpawner/Spawn', 'Content/Account', name);
  p.rect('Content/Account/' + name, x, -368, width, 60);
  p.label('Content/Account/' + name + '/Label', title);
}
p.rect('Content/Account/Title', 26, -24, 850, 48);
p.rect('Content/Account/Info', 26, -98, 850, 164);
p.rect('Content/Account/LocalIdLabel', 26, -284, 250, 56);
p.rect('Content/Account/LocalId', 292, -280, 584, 58);
p.rect('Content/Account/Status', 26, -462, 850, 154);
p.label('Content/Account/Title', '账号与联机  /  DEBUG');
p.label('Content/Account/Info', '当前账号');
p.label('Content/Account/LocalIdLabel', '本地测试编号');
p.label('Content/Account/Status', '主菜单可切换；Steam 与测试存档隔离。');
const input = p.node('Content/Account/LocalId').components.find(b => b.text.includes('m_CharacterLimit:'));
for (const [key,value] of Object.entries({m_LineType:0,m_CharacterLimit:19,m_RichText:0,m_ContentType:0,
  m_CharacterValidation:0,m_GlobalPointSize:28,m_Text:'1',m_OnFocusSelectAll:1})) p.set(input,key,value);
p.label('Content/Account/LocalId/Text', '1');
for (const node of p.nodes.values()) if (node.path.startsWith('Content/Account/')) {
  const label = node.components.find(b => /^  m_text:/m.test(b.text));
  if (!label) continue;
  p.set(label,'m_fontSize', node.path.endsWith('/Title') ? 34 : 26);
  p.set(label,'m_fontSizeBase', node.path.endsWith('/Title') ? 34 : 26);
  p.set(label,'m_isRichText',0);
  if (/\\/(Info|Status|LocalIdLabel)$/.test(node.path)) {
    p.set(label,'m_HorizontalAlignment',1); p.set(label,'m_VerticalAlignment',256);
  }
}
p.set(p.node('Content/Account').go,'m_IsActive',0);
for(const block of p.blocks) {
  if(BigInt(block.id)>=9181300000000000000n) block.text=block.text.replace(/[ \\t]+$/gm,'');
  for(const [,id] of block.text.matchAll(/fileID: (-?\\d+)\\}/g))
    if(id!=='0'&&!p.byId.has(id))throw Error('Dangling local reference: '+id);
}
p.save();
p.bindings('', 'Assets/Scripts/UI/DebugUI/DebugUIData.cs', 'DebugUIData');
console.log('Added the account page using existing DebugUI sprites, buttons and input field.');
`, {require,console});

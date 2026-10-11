// Mechanical YAML change; use the existing paper button/label assets and binding generator.
const fs = require('fs');
const vm = require('vm');
const helper = fs.readFileSync('Tools/UI/ConfigureDisplaySettings.cjs', 'utf8').split('const setting =')[0];
if (!helper.includes('class Prefab')) throw Error('UI prefab helper changed.');
vm.runInNewContext(helper.replace('9181100000000000000n', '9181400000000000000n') + `
const p = new Prefab('Assets/Res/UI/SettingUI.prefab');
if ([...p.nodes.values()].some(n => n.path === 'Panel/SkipTutorial')) throw Error('Button already exists.');
p.clone(p, 'Panel/Reset', 'Panel', 'SkipTutorial');
p.rect('Panel/SkipTutorial', 164, -760, 950, 60);
for (const state of ['Default','Click']) {
  p.set(p.node('Panel/SkipTutorial/' + state).rect, 'm_SizeDelta', '{x: 950, y: 60}');
  p.set(p.node('Panel/SkipTutorial/' + state + '/Text').rect, 'm_SizeDelta', '{x: 926, y: 60}');
  p.label('Panel/SkipTutorial/' + state + '/Text', '跳过新手教程：关');
}
p.clone(p, 'Panel/LanguageValue', 'Panel', 'SkipTutorialHint');
p.rect('Panel/SkipTutorialHint', 164, -828, 950, 44);
p.label('Panel/SkipTutorialHint', '仅影响之后的新游戏；点击按钮自动保存', 'ui.settings.skip_tutorial_hint');
const hint = p.node('Panel/SkipTutorialHint').components.find(b => /^  m_text:/m.test(b.text));
p.set(hint, 'm_fontSize', 24);
p.set(hint, 'm_fontSizeBase', 24);
p.set(p.node('').rect, 'm_SizeDelta', '{x: 1281, y: 1072}');
p.rect('Panel', 0, 0, 1281, 1072, '0.5, y: 0.5');
p.rect('Panel/Decorations', 0, 0, 1281, 1072);
p.rect('Panel/Decorations/FooterDivider', 0, -904, 1281, 26);
for (const [name,x,width] of [['Save',164,260],['Reset',508,264],['Back',920,196]])
  p.rect('Panel/' + name, x, -930, width, 60);
for (const block of p.blocks) {
  if (BigInt(block.id) >= 9181400000000000000n) block.text = block.text.replace(/[ \\t]+$/gm, '');
  for (const [,id] of block.text.matchAll(/fileID: (-?\\d+)\\}/g))
    if (id !== '0' && !p.byId.has(id)) throw Error('Dangling reference: ' + id);
}
if (p.blocks.length !== p.byId.size) throw Error('Duplicate YAML object IDs');
p.save();
p.bindings('', 'Assets/Scripts/UI/SettingUI/SettingUIData.cs', 'SettingUIData');
console.log('Added the tutorial button and hint using existing paper assets.');
`, {require,console});

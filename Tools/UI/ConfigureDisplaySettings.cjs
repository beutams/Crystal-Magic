// Mechanical Unity-YAML authoring: reuse existing labels and ShopBuyUI arrow sprites.
// Run without --apply to inspect; --apply updates only the two settings hierarchies/bindings.
const fs = require('fs');
let serial = 9181100000000000000n;
const field = (text, name) => text.match(new RegExp(`^  ${name}: (.*)$`, 'm'))?.[1];
class Prefab {
  constructor(path) {
    this.path = path;
    const text = fs.readFileSync(path, 'utf8').replace(/\r\n/g, '\n');
    this.header = text.slice(0, text.indexOf('--- !u!'));
    this.blocks = [...text.matchAll(/^--- !u!(\d+) &(\d+)[^\n]*\n[\s\S]*?(?=^--- !u!|$(?![\s\S]))/gm)]
      .map(match => ({ id: match[2], type: match[1], text: match[0] }));
    this.index();
  }
  index() {
    this.byId = new Map(this.blocks.map(b => [b.id, b]));
    this.nodes = new Map();
    for (const block of this.blocks.filter(b => b.type === '1')) {
      const ids = [...block.text.matchAll(/component: \{fileID: (\d+)\}/g)].map(m => m[1]);
      const rect = this.byId.get(ids[0]);
      const node = { go: block, rect, components: ids.map(id => this.byId.get(id)), name: field(block.text, 'm_Name'),
        parent: field(rect.text, 'm_Father').match(/\d+/)[0] };
      this.nodes.set(rect.id, node);
    }
    for (const n of this.nodes.values()) {
      let names = [n.name], parent = this.nodes.get(n.parent);
      while (parent) { names.unshift(parent.name); parent = this.nodes.get(parent.parent); }
      n.path = names.slice(1).join('/');
    }
  }
  node(path) {
    const n = [...this.nodes.values()].find(n => n.path === path);
    if (!n) throw Error(`${this.path}: missing ${path}`);
    return n;
  }
  set(block, key, value) {
    const pattern = new RegExp(`^  ${key}:.*`, 'm');
    if (!pattern.test(block.text)) throw Error(`Missing field ${key}`);
    block.text = block.text.replace(pattern, `  ${key}: ${value}`);
  }
  rect(path, x, y, w, h, anchor = '0, y: 1', pivot = anchor) {
    const b = this.node(path).rect;
    this.set(b, 'm_AnchorMin', `{x: ${anchor}}`);
    this.set(b, 'm_AnchorMax', `{x: ${anchor}}`);
    this.set(b, 'm_Pivot', `{x: ${pivot}}`);
    this.set(b, 'm_AnchoredPosition', `{x: ${x}, y: ${y}}`);
    this.set(b, 'm_SizeDelta', `{x: ${w}, y: ${h}}`);
  }
  clone(source, sourcePath, parentPath, name, deep = true) {
    const root = source.node(sourcePath), parent = this.node(parentPath);
    const nodes = [...source.nodes.values()].filter(n => n === root || (deep && n.path.startsWith(sourcePath + '/')));
    const originals = nodes.flatMap(n => [n.go, ...n.components]);
    const map = new Map(originals.map(b => [b.id, (++serial).toString()]));
    for (const b of originals) {
      const text = b.text.replace(/^--- !u!(\d+) &\d+/, `--- !u!$1 &${map.get(b.id)}`)
        .replace(/fileID: (\d+)(?=\})/g, (all, id) => map.has(id) ? `fileID: ${map.get(id)}` : all);
      this.blocks.push({ id: map.get(b.id), type: b.type, text });
    }
    const go = this.blocks.find(b => b.id === map.get(root.go.id));
    const rect = this.blocks.find(b => b.id === map.get(root.rect.id));
    this.set(go, 'm_Name', name);
    this.set(rect, 'm_Father', `{fileID: ${parent.rect.id}}`);
    if (!deep) rect.text = rect.text.replace(/  m_Children:[\s\S]*?(?=  m_Father:)/, '  m_Children: []\n');
    parent.rect.text = parent.rect.text.replace(/  m_Children: \[\]\n/, '  m_Children:\n')
      .replace(/(?=  m_Father:)/, `  - {fileID: ${rect.id}}\n`);
    this.index();
    return `${parentPath ? parentPath + '/' : ''}${name}`;
  }
  label(path, text, key = '') {
    const component = this.node(path).components.find(b => /^  m_text:/m.test(b.text));
    this.set(component, 'm_text', JSON.stringify(text));
    if (/^  _localizationKey:/m.test(component.text)) this.set(component, '_localizationKey', key);
  }
  save() {
    const text = this.header + this.blocks.map(b => b.text).join('');
    if (fs.readFileSync(this.path, 'utf8').replace(/\r\n/g, '\n') !== text)
      fs.writeFileSync(this.path, text, 'utf8');
  }
  bindings(rootPath, output, className) {
    const nodes = [...this.nodes.values()].filter(n => n.path.startsWith(rootPath ? rootPath + '/' : '') && n.path !== rootPath);
    const paths = nodes.map(n => rootPath ? n.path.slice(rootPath.length + 1) : n.path);
    const names = paths.map(p => p.replace(/[ /]/g, '_'));
    if (new Set(names).size !== names.length) throw Error('Duplicate binding names');
    fs.writeFileSync(output, '// AUTO-GENERATED - DO NOT EDIT MANUALLY\n// Right-click Prefab -> Assets/Tools/Generate UIData to regenerate\n\nusing UnityEngine;\nusing CrystalMagic.Core;\n\n' +
      `public class ${className} : UIData\n{\n` + names.map(n => `    public UINode ${n};\n`).join('') +
      '\n    public override void Bind(Transform root)\n    {\n' + paths.map((p,i) => `        ${names[i]} = UINode.From(Find(root, "${p}"));\n`).join('') + '    }\n}\n');
  }
}
const setting = new Prefab('Assets/Res/UI/SettingUI.prefab');
const book = new Prefab('Assets/Res/UI/CharacterUI.prefab');
const shop = new Prefab('Assets/Res/UI/ShopBuyUI.prefab');
function spaceRows() {
  // Preserve the original slider spacing: sliced handle borders have a larger visual footprint.
  setting.set(setting.node('').rect, 'm_SizeDelta', '{x: 1281, y: 928}');
  setting.rect('Panel', 0, 0, 1281, 928, '0.5, y: 0.5');
  setting.rect('Panel/Decorations', 0, 0, 1281, 928);
  for (const [i, name] of ['Master', 'Bgm', 'Sfx'].entries()) {
    const y = -204 - i * 96;
    for (const [part, x, width] of [['Label',160,216], ['Value',394,102], ['Slider',526,584]])
      setting.rect(`Panel/${name}Volume${part}`, x, y, width, 60);
  }
  setting.rect('Panel/Decorations/LanguageDivider', 0, -490, 1281, 26);
  for (const [name, y] of [['Language',-524], ['DisplayMode',-600], ['Resolution',-676]])
    for (const part of ['Label','Value','Previous','Next']) {
      const node = setting.node(`Panel/${name}${part}`);
      const x = field(node.rect.text, 'm_AnchoredPosition').match(/x: ([\d.-]+)/)[1];
      setting.set(node.rect, 'm_AnchoredPosition', `{x: ${x}, y: ${y}}`);
    }
  setting.rect('Panel/LanguageUnderline', 616, -583, 404, 2);
  setting.rect('Panel/Decorations/FooterDivider', 0, -760, 1281, 26);
  for (const [name, x, width] of [['Save',164,260], ['Reset',508,264], ['Back',920,196]])
    setting.rect('Panel/' + name, x, -786, width, 60);
  setting.save();
  console.log('Restored slider spacing and extended the settings paper to 928 units.');
}
if (process.argv.includes('--space-rows')) {
  spaceRows();
  process.exit(0);
}
if (process.argv.includes('--validate')) {
  for (const prefab of [setting, book]) {
    if (prefab.byId.size !== prefab.blocks.length) throw Error('Duplicate YAML object IDs');
    for (const block of prefab.blocks) {
      for (const [, id] of block.text.matchAll(/fileID: (-?\d+)\}/g))
        if (id !== '0' && !prefab.byId.has(id)) throw Error(`${prefab.path}: dangling fileID ${id}`);
      if (BigInt(block.id) >= 9181100000000000000n)
        block.text = block.text.replace(/[ \t]+$/gm, '');
    }
    prefab.save();
    console.log(`${prefab.path}: ${prefab.blocks.length} objects, all local references valid.`);
  }
  const rows = JSON.parse(fs.readFileSync('Assets/Res/Data/LocalizationDataTable.json', 'utf8').replace(/^\uFEFF/, '')).Rows;
  if (new Set(rows.map(r => r.Id)).size !== rows.length || new Set(rows.map(r => r.Key)).size !== rows.length)
    throw Error('Duplicate localization IDs or keys');
  console.log('Localization IDs and keys are unique.');
  process.exit(0);
}
if (!process.argv.includes('--apply')) {
  for (const prefab of [setting, book])
    for (const n of prefab.nodes.values())
      if ((prefab === setting && n.path.split('/').length === 2) ||
          (prefab === book && /^Setting(\/Navigation)?\/[^/]+$/.test(n.path)) ||
          (prefab === book && /^Setting\/(Audio\/MasterVolume|Language)\/[^/]+$/.test(n.path)))
        console.log(n.path, field(n.rect.text, 'm_AnchoredPosition'), field(n.rect.text, 'm_SizeDelta'),
          n.components.map(c => field(c.text, '_localizationKey')).filter(Boolean).join(' '));
  process.exit(0);
}
if ([...setting.nodes.values()].some(n => n.path === 'Panel/DisplayModeLabel'))
  throw Error('Display settings already configured; refusing to duplicate nodes.');

// Keep the existing paper size; tighten the vertical rhythm to make room for two rows.
for (const [i, name] of ['Master', 'Bgm', 'Sfx'].entries()) {
  const y = -184 - i * 70;
  setting.rect(`Panel/${name}VolumeLabel`, 160, y, 216, 60);
  setting.rect(`Panel/${name}VolumeValue`, 394, y, 102, 60);
  setting.rect(`Panel/${name}VolumeSlider`, 526, y, 584, 60);
}
setting.rect('Panel/Decorations/LanguageDivider', 0, -404, 1281, 26);
for (const [name, x, w] of [['Label',160,280], ['Previous',526,66], ['Value',616,404], ['Next',1044,66]])
  setting.rect('Panel/Language' + name, x, -430, w, 60);
setting.rect('Panel/LanguageUnderline', 616, -489, 404, 2);
setting.rect('Panel/Decorations/FooterDivider', 0, -644, 1281, 26);
for (const [name, x, w] of [['Save',164,260], ['Reset',508,264], ['Back',920,196]])
  setting.rect('Panel/' + name, x, -672, w, 60);

function arrow(target, parent, name, left, x, y, w = 36, h = 60) {
  const path = target.clone(shop, left ? 'Reduce' : 'Add', parent, name);
  target.rect(path, x, y, w, h);
  // Preserve the real shop sprite and its ButtonPlus; tint to the existing parchment ink.
  const image = target.node(path).components.find(c => /^  m_Sprite:/m.test(c.text));
  target.set(image, 'm_Color', '{r: 0.6862745, g: 0.4745098, b: 0.3411765, a: 1}');
}
for (const [name, title, key, value, y] of [
  ['DisplayMode', '显示模式', 'ui.settings.display_mode', '全屏', -500],
  ['Resolution', '分辨率', 'ui.settings.resolution', '1920 × 1080', -570],
]) {
  setting.clone(setting, 'Panel/LanguageLabel', 'Panel', name + 'Label');
  setting.label('Panel/' + name + 'Label', title, key);
  setting.rect('Panel/' + name + 'Label', 160, y, 300, 60);
  setting.clone(setting, 'Panel/LanguageValue', 'Panel', name + 'Value');
  setting.label('Panel/' + name + 'Value', value);
  setting.rect('Panel/' + name + 'Value', 616, y, 404, 60);
  arrow(setting, 'Panel', name + 'Previous', true, 541, y);
  arrow(setting, 'Panel', name + 'Next', false, 1059, y);
}

book.clone(book, 'Setting/Navigation/Audio', 'Setting/Navigation', 'Display');
book.label('Setting/Navigation/Display/Label', '显示', 'ui.settings.display');
for (const side of ['Left', 'Right'])
  book.clone(book, `Setting/Decorations/Audio${side}Diamond`, 'Setting/Decorations', `Display${side}Diamond`);
for (const [i, name] of ['Audio', 'Display', 'Language', 'Save', 'ReturnMainMenu'].entries()) {
  const y = -530 - i * 120;
  book.rect('Setting/Navigation/' + name, 532, y, 560, 100);
  for (const side of ['Left', 'Right']) {
    const rect = book.node(`Setting/Decorations/${name}${side}Diamond`).rect;
    const oldY = Number(field(rect.text, 'm_AnchoredPosition').match(/y: ([\d.-]+)/)[1]);
    const oldNavY = { Audio:-581, Display:-581, Language:-721, Save:-861, ReturnMainMenu:-1001 }[name];
    const x = field(rect.text, 'm_AnchoredPosition').match(/x: ([\d.-]+)/)[1];
    book.set(rect, 'm_AnchoredPosition', `{x: ${x}, y: ${oldY + y - oldNavY + 6}}`);
  }
}
book.clone(book, 'Setting/Language', 'Setting', 'Display', false);
book.set(book.node('Setting/Display').go, 'm_IsActive', '0');
for (const [name, title, key, value, y] of [
  ['DisplayMode', '显示模式', 'ui.settings.display_mode', '全屏', -570],
  ['Resolution', '分辨率', 'ui.settings.resolution', '1920 × 1080', -830],
]) {
  book.clone(book, 'Setting/Audio/MasterVolume/Label', 'Setting/Display', name + 'Label');
  book.label('Setting/Display/' + name + 'Label', title, key);
  book.rect('Setting/Display/' + name + 'Label', 1344, y, 707, 70);
  book.clone(book, 'Setting/Audio/MasterVolume/Value', 'Setting/Display', name + 'Value');
  book.label('Setting/Display/' + name + 'Value', value);
  book.rect('Setting/Display/' + name + 'Value', 1440, y - 100, 500, 84);
  const text = book.node('Setting/Display/' + name + 'Value').components.find(c => /^  m_text:/m.test(c.text));
  book.set(text, 'm_HorizontalAlignment', '2');
  arrow(book, 'Setting/Display', name + 'Previous', true, 1344, y - 103, 48, 80);
  arrow(book, 'Setting/Display', name + 'Next', false, 2003, y - 103, 48, 80);
}
spaceRows();
book.save();
setting.bindings('', 'Assets/Scripts/UI/SettingUI/SettingUIData.cs', 'SettingUIData');
book.bindings('Setting', 'Assets/Scripts/UI/CharacterUI/Sub/CharacterUI_SettingData.cs', 'CharacterUI_SettingData');
console.log('Configured both settings prefabs and regenerated their UIData bindings.');

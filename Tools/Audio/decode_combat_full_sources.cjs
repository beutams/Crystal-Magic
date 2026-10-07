// Decode owned, explicitly selected OGG files in a separate temporary browser.
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.argv[2]);
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'output/audio/combat-full-v1/sources');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage();
    const records = JSON.parse(fs.readFileSync(path.join(out, 'sources.json'), 'utf8'));
    for (const record of records) {
      const target = path.join(out, path.basename(record.file, '.ogg') + '.f32');
      if (fs.existsSync(target)) continue;
      const encoded = fs.readFileSync(path.join(root, record.file)).toString('base64');
      const result = await page.evaluate(async data => {
        const bytes = Uint8Array.from(atob(data), c => c.charCodeAt(0));
        const context = new OfflineAudioContext(1, 48000, 48000);
        const buffer = await context.decodeAudioData(bytes.buffer);
        if (buffer.sampleRate !== 48000) throw new Error('Unexpected sample rate');
        const mono = new Float32Array(buffer.length);
        for (let c = 0; c < buffer.numberOfChannels; c++) {
          const channel = buffer.getChannelData(c);
          for (let i = 0; i < mono.length; i++) mono[i] += channel[i] / buffer.numberOfChannels;
        }
        const raw = new Uint8Array(mono.buffer);
        let binary = '';
        for (let i = 0; i < raw.length; i += 8192)
          binary += String.fromCharCode(...raw.subarray(i, i + 8192));
        return btoa(binary);
      }, encoded);
      fs.writeFileSync(target, Buffer.from(result, 'base64'));
      console.log(path.basename(target));
    }
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

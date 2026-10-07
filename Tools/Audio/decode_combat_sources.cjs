// Decode local source audio in a separate headless Edge instance, no user profile.
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.argv[2]);
const root = path.resolve(__dirname, '../..');
const sourceRoot = path.join(root, 'output/audio/combat-v1/sources');
const records = JSON.parse(fs.readFileSync(path.join(sourceRoot, 'selected-sources.json'), 'utf8'));
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage();
    for (const record of records) {
      const name = path.parse(record.local_file).name;
      const target = path.join(sourceRoot, 'decoded', record.pack, name + '.f32');
      if (fs.existsSync(target)) continue;
      const input = fs.readFileSync(path.join(root, record.local_file)).toString('base64');
      const result = await page.evaluate(async input => {
        const context = new OfflineAudioContext(1, 48000, 48000);
        const buffer = await context.decodeAudioData(Uint8Array.from(atob(input), c => c.charCodeAt(0)).buffer);
        const mono = new Float32Array(buffer.length);
        for (let c = 0; c < buffer.numberOfChannels; c++) {
          const samples = buffer.getChannelData(c);
          for (let i = 0; i < mono.length; i++) mono[i] += samples[i] / buffer.numberOfChannels;
        }
        const bytes = new Uint8Array(mono.buffer);
        let binary = '';
        for (let i = 0; i < bytes.length; i += 8192) binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
        return { data: btoa(binary), samples: mono.length, rate: buffer.sampleRate };
      }, input);
      if (result.rate !== 48000) throw new Error('Unexpected sample rate');
      fs.mkdirSync(path.dirname(target), { recursive: true });
      fs.writeFileSync(target, Buffer.from(result.data, 'base64'));
      console.log(`${record.pack}/${name}: ${(result.samples / 48000).toFixed(3)} seconds`);
    }
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

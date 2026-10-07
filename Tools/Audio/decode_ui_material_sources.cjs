// Decode selected Ogg source assets using Chromium's codec and resampler.
// This is a separate headless process, not the user's browser/profile.
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.argv[2]);
const root = path.resolve(__dirname, '../..');
const sourceRoot = path.join(root, 'output/audio/ui-v2/sources');
const records = JSON.parse(fs.readFileSync(path.join(sourceRoot, 'selected-sources.json'), 'utf8'));
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage();
    for (const record of records) {
      const name = path.basename(record.local_file, '.ogg');
      const target = path.join(sourceRoot, 'decoded', record.pack, name + '.f32');
      if (fs.existsSync(target)) {
        console.log(`Preserved decoded source: ${record.pack}/${name}`);
        continue;
      }
      const input = fs.readFileSync(path.join(root, record.local_file)).toString('base64');
      const decoded = await page.evaluate(async input => {
        const encoded = Uint8Array.from(atob(input), c => c.charCodeAt(0));
        const context = new OfflineAudioContext(1, 48000, 48000);
        const buffer = await context.decodeAudioData(encoded.buffer);
        const mono = new Float32Array(buffer.length);
        for (let c = 0; c < buffer.numberOfChannels; c++) {
          const channel = buffer.getChannelData(c);
          for (let i = 0; i < mono.length; i++) mono[i] += channel[i] / buffer.numberOfChannels;
        }
        const bytes = new Uint8Array(mono.buffer);
        let binary = '';
        for (let i = 0; i < bytes.length; i += 8192) binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
        return { data: btoa(binary), samples: mono.length, rate: buffer.sampleRate, channels: buffer.numberOfChannels };
      }, input);
      if (decoded.rate !== 48000) throw new Error('Unexpected decode sample rate');
      fs.mkdirSync(path.dirname(target), { recursive: true });
      fs.writeFileSync(target, Buffer.from(decoded.data, 'base64'));
      console.log(`${record.pack}/${name}: ${(decoded.samples / 48000).toFixed(3)}s, ${decoded.channels}ch -> mono`);
    }
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

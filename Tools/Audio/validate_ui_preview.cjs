// Headless check of the authored local preview; does not control a user's browser.
const { chromium } = require(process.argv[2]);
const { pathToFileURL } = require('node:url');
const assert = require('node:assert/strict');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 736, height: 1000 } });
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.goto(pathToFileURL(process.argv[3]).href);
    const frame = page.frameLocator('iframe');
    const status = frame.locator('[data-status]');
    const buttons = frame.locator('[data-sound]');
    await buttons.first().waitFor();
    assert.equal(await buttons.count(), 24);
    for (let i = 0; i < 24; i++) {
      await buttons.nth(i).click();
      await frame.getByText(/播放完成$/).waitFor({ timeout: 5000 });
    }
    await frame.locator('[data-action="all"]').click();
    await status.filter({ hasText: /正在准备|01 选中/ }).waitFor();
    await frame.locator('[data-action="stop"]').click();
    await page.waitForTimeout(700);
    assert.equal(await status.textContent(), '已停止');
    assert.equal(await frame.locator('[aria-pressed="true"]').count(), 0);
    await frame.locator('[data-action="all"]').click();
    await frame.getByText('24 个音效试听完成', { exact: true }).waitFor({ timeout: 25000 });
    for (const width of [736, 320]) {
      await page.setViewportSize({ width, height: 1000 });
      const overflow = await frame.locator('#cm-ui-sfx-v1').evaluate(el => el.scrollWidth > el.clientWidth + 1);
      assert.equal(overflow, false, `overflow at ${width}`);
    }
    await page.screenshot({ path: process.argv[4], fullPage: true });
    assert.deepEqual(errors, []);
    console.log(JSON.stringify({ individualPlayback: 24, playAll: true, stop: true, responsiveWidths: [736, 320], errors }));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

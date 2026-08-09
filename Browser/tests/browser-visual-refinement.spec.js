const { test, expect } = require('@playwright/test');
const path = require('path');
const { pathToFileURL } = require('url');

const repo = path.resolve(__dirname, '../..');
const sample = pathToFileURL(path.join(repo, 'Browser/refs/one-ui-sample.html')).href;
const images = path.join(repo, 'Browser/docs/images');

async function state(page) {
  return page.evaluate(() => window.__browserPreview.getState());
}

async function expectCanvasScale(page, scale) {
  await expect.poll(() => page.locator('#app').evaluate(element => element.getBoundingClientRect().width / 1920)).toBeCloseTo(scale, 2);
}

test('Browser Samsung visual refinement interaction and evidence', async ({ browser }) => {
  const errors = [];
  const requests = [];
  const context = await browser.newContext({ viewport: { width: 1920, height: 1080 }, hasTouch: true });
  const page = await context.newPage();
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
  page.on('pageerror', error => errors.push(error.message));
  page.on('request', request => requests.push(request.url()));
  await page.goto(sample);

  await expect(page.locator('#address')).toBeFocused();
  await expect(page.locator('#back')).toBeDisabled();
  await expect(page.locator('#forward')).toBeDisabled();
  await page.screenshot({ path: path.join(images, 'html-browser-home-1920x1080.png') });

  await page.keyboard.press('ArrowRight');
  await expect(page.locator('#reload')).toBeFocused();
  await page.keyboard.press('ArrowDown');
  await expect(page.getByRole('button', { name: 'Open Tizen guide' })).toBeFocused();
  await page.keyboard.press('ArrowUp');
  await expect(page.locator('#address')).toBeFocused();

  await page.locator('#address').fill('https://docs.tizen.org/');
  await page.keyboard.press('Enter');
  await expect.poll(async () => (await state(page)).mode).toBe('loading');
  await expect(page.locator('#reload')).toBeDisabled();
  await page.setViewportSize({ width: 1280, height: 720 });
  await expectCanvasScale(page, 2 / 3);
  await page.screenshot({ path: path.join(images, 'html-browser-loading-1280x720.png') });
  await page.setViewportSize({ width: 1920, height: 1080 });
  await expectCanvasScale(page, 1);
  await expect.poll(async () => (await state(page)).mode).toBe('page');
  await page.screenshot({ path: path.join(images, 'html-browser-page-1920x1080.png') });

  await page.keyboard.press('ArrowDown');
  await expect(page.locator('.fixture-page')).toBeFocused();
  await page.keyboard.press('ArrowDown');
  await expect(page.locator('#tabs')).toBeFocused();
  await page.keyboard.press('Enter');
  await expect.poll(async () => (await state(page)).mode).toBe('tabs');
  await expect(page.locator('.tabs-open')).toHaveCount(1);
  await expect(page.locator('.tab-open').first()).toBeFocused();

  await page.evaluate(() => window.__browserPreview.seedTabs(3));
  await page.screenshot({ path: path.join(images, 'html-browser-tabs-1920x1080.png') });
  await page.keyboard.press('ArrowRight');
  await expect(page.locator('.tab-close').first()).toBeFocused();
  await page.keyboard.press('Enter');
  await expect.poll(async () => (await state(page)).modalOpen).toBe(true);
  await expect(page.locator('#modal-cancel')).toBeFocused();
  await page.screenshot({ path: path.join(images, 'html-browser-close-confirmation-1920x1080.png') });
  await page.setViewportSize({ width: 1440, height: 1080 });
  await expectCanvasScale(page, .75);
  await page.screenshot({ path: path.join(images, 'html-browser-close-confirmation-1440x1080.png') });
  await page.setViewportSize({ width: 1920, height: 1080 });
  await expectCanvasScale(page, 1);
  await page.keyboard.press('ArrowDown');
  await expect(page.locator('#modal-cancel')).toBeFocused();
  await page.keyboard.press('ArrowRight');
  await expect(page.locator('#modal-confirm')).toBeFocused();
  await page.keyboard.press('ArrowLeft');
  await expect(page.locator('#modal-cancel')).toBeFocused();
  await page.keyboard.press('Escape');
  await expect.poll(async () => (await state(page)).modalOpen).toBe(false);
  await expect(page.locator('.tab-close').first()).toBeFocused();

  await page.locator('.tabs-back').click();
  await expect.poll(async () => (await state(page)).mode).toBe('page');
  await page.locator('#tabs').click();
  await expect.poll(async () => (await state(page)).mode).toBe('tabs');
  const newTab = page.getByRole('button', { name: 'New tab' });
  await newTab.tap();
  await expect.poll(async () => (await state(page)).tabCount).toBe(4);

  await page.evaluate(() => window.__browserPreview.showState('offline'));
  await page.setViewportSize({ width: 1280, height: 720 });
  await expectCanvasScale(page, 2 / 3);
  await expect(page.getByRole('button', { name: 'Retry' })).toBeFocused();
  await page.screenshot({ path: path.join(images, 'html-browser-offline-1280x720.png') });

  for (const [width, height, expectedScale, expectedX, expectedY] of [
    [1920, 1080, 1, 0, 0],
    [1280, 720, 2 / 3, 0, 0],
    [1440, 1080, .75, 0, 135],
    [2560, 1080, 1, 320, 0],
  ]) {
    await page.setViewportSize({ width, height });
    await expectCanvasScale(page, expectedScale);
    const geometry = await page.locator('#app').evaluate(element => {
      const rect = element.getBoundingClientRect();
      return { left: rect.left, top: rect.top, scale: rect.width / 1920 };
    });
    expect(geometry.left).toBeCloseTo(expectedX, 1);
    expect(geometry.top).toBeCloseTo(expectedY, 1);
    expect(geometry.scale).toBeCloseTo(expectedScale, 2);
  }

  expect(errors).toEqual([]);
  expect(requests.every(url => url.startsWith('file:'))).toBe(true);
  await context.close();
});

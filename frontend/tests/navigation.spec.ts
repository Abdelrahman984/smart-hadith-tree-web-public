import { test, expect } from '@playwright/test';

// These pages render without the API, so the checks hold whether or not the backend is running.
const CONTENT_PAGES = ['/', '/search', '/books', '/verify', '/glossary', '/sources'];

const NAV_TARGETS = ['/search', '/books', '/verify', '/glossary', '/sources'];

for (const path of CONTENT_PAGES) {
  test(`${path} has the shared header with all sections`, async ({ page }) => {
    await page.goto(path);

    const nav = page.getByRole('navigation', { name: 'التنقل الرئيسي' });
    await expect(nav).toBeVisible();
    for (const href of NAV_TARGETS) {
      await expect(nav.locator(`a[href="${href}"]`)).toHaveCount(1);
    }
    await expect(page.locator('main#main-content')).toHaveCount(1);
  });

  test(`${path} fits a phone screen without sideways scrolling`, async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto(path);

    const nav = page.getByRole('navigation', { name: 'التنقل الرئيسي' });
    await expect(nav).toBeVisible();
    // The header stays on one row.
    const headerBox = await page.locator('header').first().boundingBox();
    expect(headerBox?.height ?? 0).toBeLessThanOrEqual(64);

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    expect(overflow).toBeLessThanOrEqual(0);
  });
}

test('the current section is marked in the header', async ({ page }) => {
  await page.goto('/glossary');

  await expect(page.locator('nav[aria-label="التنقل الرئيسي"] a[href="/glossary"]')).toHaveAttribute('aria-current', 'page');
  await expect(page.locator('nav[aria-label="التنقل الرئيسي"] a[href="/search"]')).not.toHaveAttribute('aria-current', 'page');
});

test('an unknown address shows the not-found page with the header', async ({ page }) => {
  const response = await page.goto('/no-such-page');

  expect(response?.status()).toBe(404);
  await expect(page.getByRole('heading', { name: 'الصفحة غير موجودة' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'التنقل الرئيسي' })).toBeVisible();
});

test('takhreej with fewer than two ids explains what to do', async ({ page }) => {
  await page.goto('/takhreej?ids=only-one');

  await expect(page.getByRole('heading', { name: 'يرجى تحديد حديثين على الأقل للتخريج' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'التنقل الرئيسي' })).toBeVisible();
});

// The header holds five sections: they must fit the screen from small phones to wide desktops.
for (const width of [320, 375, 640, 768, 1024, 1366]) {
  test(`the header navigation fits a ${width}px screen`, async ({ page }) => {
    await page.setViewportSize({ width, height: 800 });
    await page.goto('/sources');

    const edges = await page.locator('nav[aria-label="التنقل الرئيسي"] a').evaluateAll((links) =>
      links.map((a) => {
        const r = a.getBoundingClientRect();
        return { left: r.left, right: r.right };
      })
    );
    expect(edges).toHaveLength(5);
    for (const { left, right } of edges) {
      expect(left).toBeGreaterThanOrEqual(0);
      expect(right).toBeLessThanOrEqual(width);
    }
  });
}

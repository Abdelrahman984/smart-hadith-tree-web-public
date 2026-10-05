import { test, expect } from '@playwright/test';

test('sources page explains where jarh wa ta\'dil and ilal come from', async ({ page }) => {
  await page.goto('/sources');

  await expect(page.locator('h1')).toHaveText('المصادر والمنهج');
  await expect(page.locator('h2#jarh-tadil')).toBeVisible();
  await expect(page.locator('h2#ilal')).toBeVisible();
  // The two sections name their actual books.
  await expect(page.locator('section#jarh-tadil, section[aria-labelledby="jarh-tadil"]')).toContainText('تهذيب الكمال');
  await expect(page.locator('section[aria-labelledby="ilal"]')).toContainText('الكواكب النيرات');
  // Each of the six rules is listed.
  await expect(page.locator('section[aria-labelledby="ilal"] article')).toHaveCount(6);
});

test('the page contents links jump to their sections', async ({ page }) => {
  await page.goto('/sources');

  await page.getByRole('navigation', { name: 'أقسام الصفحة' }).getByRole('link', { name: 'العلل' }).click();
  await expect(page).toHaveURL(/#ilal$/);
  await expect(page.locator('h2#ilal')).toBeInViewport();
});

test('the footer links to the sources page and its sections', async ({ page }) => {
  await page.goto('/');

  const footer = page.getByRole('navigation', { name: 'روابط التذييل' });
  await expect(footer.locator('a[href="/sources"]')).toHaveCount(1);
  await expect(footer.locator('a[href="/sources#jarh-tadil"]')).toHaveCount(1);
  await expect(footer.locator('a[href="/sources#ilal"]')).toHaveCount(1);
});

test('the logo is the shared mark and the tab icon is the new one', async ({ page }) => {
  await page.goto('/');

  await expect(page.locator('header a[href="/"] svg')).toHaveCount(1);
  const icons = await page.locator('link[rel~="icon"]').evaluateAll((els) => els.map((e) => e.getAttribute('href')));
  expect(icons.some((h) => h?.includes('icon.svg'))).toBe(true);
  await expect(page.locator('link[rel="apple-touch-icon"]')).toHaveCount(1);
  await expect(page.locator('meta[name="theme-color"]')).toHaveAttribute('content', '#1A3A5C');
});

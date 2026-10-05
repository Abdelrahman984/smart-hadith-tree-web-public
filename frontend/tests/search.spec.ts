import { test, expect } from '@playwright/test';

test('search page renders and accepts input', async ({ page }) => {
  await page.goto('/search');

  // Verify the header
  const heading = page.locator('h1');
  await expect(heading).toHaveText('ابحث وخرّج الأحاديث النبوية');

  // Find the search input
  const searchInput = page.getByPlaceholder('ابحث بمتن الحديث، اسم الراوي، أو المصدر...');
  await expect(searchInput).toBeVisible();

  // Type a query
  await searchInput.fill('البخاري');
  await expect(searchInput).toHaveValue('البخاري');
});

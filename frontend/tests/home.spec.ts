import { test, expect } from '@playwright/test';

test('homepage has the main heading and the search form', async ({ page }) => {
  await page.goto('/');

  await expect(page.locator('h1')).toContainText('شجرة الأسانيد الذكية');
  await expect(page.getByRole('textbox', { name: 'ابحث في الأحاديث والأسانيد' })).toBeVisible();
});

test('the home search form opens the search page with the query', async ({ page }) => {
  await page.goto('/');

  await page.getByRole('textbox', { name: 'ابحث في الأحاديث والأسانيد' }).fill('إنما الأعمال بالنيات');
  await page.getByRole('button', { name: 'بحث' }).click();

  await expect(page).toHaveURL(/\/search\?q=/);
});

test('the header link opens the search page', async ({ page }) => {
  await page.goto('/');

  await page.getByRole('navigation', { name: 'التنقل الرئيسي' }).getByRole('link', { name: /البحث/ }).click();

  await expect(page).toHaveURL(/\/search$/);
});

import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  
  console.log("Navigating to /search...");
  await page.goto('http://localhost:3000/search');
  
  console.log("Searching for إنما الأعمال بالنيات...");
  // Using React Query's rapid input onChange, we just type it
  await page.fill('input[type="search"]', 'بالنيات');
  
  console.log("Waiting for results...");
  await page.waitForSelector('text=Sahih al Bukhari', { timeout: 10000 });
  await page.screenshot({ path: 'search-results.png' });
  console.log("Saved search-results.png");
  
  console.log("Clicking the first result...");
  const firstResult = await page.locator('div.bg-white.p-5').first();
  await firstResult.click();
  
  console.log("Waiting for tree page...");
  await page.waitForLoadState('networkidle');
  await page.screenshot({ path: 'tree-page.png' });
  console.log("Saved tree-page.png");
  
  await browser.close();
})();

import { test, expect, type Page } from '@playwright/test';
import { mockApiAvailable, MOCK_API_SKIP_REASON } from './fixtures/mockApi';

test.beforeAll(async () => {
  test.skip(!(await mockApiAvailable()), MOCK_API_SKIP_REASON);
});

/** Waits for both layout passes to finish: the canvas fades in once the graph is laid out and fitted. */
async function graphReady(page: Page) {
  await expect(page.locator('div.absolute.inset-0.bg-surface-muted')).toHaveClass(/opacity-100/);
}

/** A narrator card, found by the full name in its title attribute. */
const card = (page: Page, fullName: string) =>
  page.locator('.react-flow__node').filter({ has: page.locator(`[title="${fullName}"]`) });

const edge = (page: Page, id: string) => page.getByTestId(`rf__edge-${id}`);

test.describe('single tree (/tree/[id])', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/tree/x');
    await graphReady(page);
  });

  test('draws every narrator and link of the chain', async ({ page }) => {
    await expect(page.locator('.react-flow__node')).toHaveCount(5);
    await expect(page.locator('.react-flow__edge')).toHaveCount(4);
    await expect(card(page, 'محمد بن إسماعيل البخاري')).toContainText('المصدر والمُخَرِّج');
  });

  test('shows the grade of each narrator as text, not only as a colour', async ({ page }) => {
    await expect(card(page, 'راو متروك للاختبار')).toContainText('كذاب');
    await expect(card(page, 'عمر بن الخطاب')).toContainText('صحابي');
  });

  test('marks the broken link', async ({ page }) => {
    await expect(page.locator('.react-flow__edge-text', { hasText: 'انقطاع' })).toBeVisible();
    await expect(edge(page, 'e-N2-N1').locator('path.react-flow__edge-path')).toHaveCSS('stroke', 'rgb(239, 68, 68)');
  });

  test('a click opens the narrator details; Escape closes them', async ({ page }) => {
    await card(page, 'راو متروك للاختبار').click();

    const dialog = page.getByRole('tabpanel', { name: 'الراوي' });
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText('لا تشترك بلدانهما في الإقامة'); // the node's travel note reaches touch users
    await expect(dialog).toContainText('كذاب');

    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
  });

  test('«إبراز الضعفاء» dims the reliable narrators and brings them back', async ({ page }) => {
    const reliable = card(page, 'عبد الله بن الزبير الحميدي').locator('> div');
    await expect(reliable).toHaveCSS('opacity', '1');

    await page.getByRole('button', { name: 'إبراز الضعفاء' }).click();
    await expect(reliable).toHaveCSS('opacity', '0.3');
    await expect(card(page, 'راو متروك للاختبار').locator('> div')).toHaveCSS('opacity', '1');

    await page.getByRole('button', { name: 'إظهار الجميع' }).click();
    await expect(reliable).toHaveCSS('opacity', '1');
  });

  test('«فحص العلل» opens the report and marks the link on the graph', async ({ page }) => {
    await expect(page.locator('#workspace-panel')).toHaveCSS('width', '0px'); // graph first: the panel starts closed
    await page.getByRole('button', { name: /فحص العلل/ }).click();

    const panel = page.locator('#workspace-panel');
    await expect(panel.getByRole('tab', { name: /العلل/ })).toHaveAttribute('aria-selected', 'true');
    await expect(panel).toContainText('عنعنة مدلس');
    await expect(edge(page, 'e-N2-N1').locator('.react-flow__edge-text')).toContainText('عنعنة مدلس');
  });
});

test.describe('comparative tree (/takhreej)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);
  });

  test('merges three books into one graph', async ({ page }) => {
    // Compilers C, D, E and narrators N1, N2, N4, N5.
    await expect(page.locator('.react-flow__node')).toHaveCount(7);
    // N1→C, N1→D, N5→E, N2→N1, N2→N5, N4→N2.
    await expect(page.locator('.react-flow__edge')).toHaveCount(6);
    await expect(card(page, 'سفيان بن عيينة')).toContainText('مدلس');
  });

  test('a link used by one book takes that book\'s colour', async ({ page }) => {
    await expect(edge(page, 'e-N1-C').locator('path.react-flow__edge-path')).toHaveCSS('stroke', 'rgb(37, 99, 235)');
  });

  test('Ilal findings are laid over the edges and the madar is ringed', async ({ page }) => {
    await expect(edge(page, 'e-N2-N1').locator('.react-flow__edge-text')).toContainText('عنعنة مدلس');
    await expect(edge(page, 'e-N2-N1').locator('path.react-flow__edge-path')).toHaveCSS('stroke', 'rgb(234, 88, 12)');
    await expect(card(page, 'سفيان بن عيينة').locator('> div')).toHaveClass(/ring-amber-400/);
  });

  test('the legend focuses one book\'s routes; Escape clears it', async ({ page }) => {
    const legendButton = page.locator('button[title="إبراز مسارات هذا الكتاب"]', { hasText: 'صحيح مسلم' });
    await legendButton.click();
    await expect(legendButton).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByRole('button', { name: /إبراز: صحيح مسلم/ })).toBeVisible();

    await page.keyboard.press('Escape');
    await expect(page.getByRole('button', { name: /إبراز: صحيح مسلم/ })).toHaveCount(0);
    await expect(legendButton).toHaveAttribute('aria-pressed', 'false');
  });

  test('the sidebar shows the automatic grade, the matns and the Ilal tab', async ({ page }) => {
    const sidebar = page.locator('#workspace-panel');
    await expect(sidebar).toContainText('التقدير الآلي للطرق');
    await expect(sidebar).toContainText('متون الروايات');

    await sidebar.getByRole('tab', { name: /العلل/ }).click();
    await expect(sidebar).toContainText('عنعنة مدلس');
  });
});

test.describe('selecting a narrator', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);
  });

  test('keeps its chain highlighted after the details close; Escape clears it', async ({ page }) => {
    // N5 (Abu Dawud's branch) lights N5, N2, N4 and the compiler; the Bukhari branch is dimmed.
    await card(page, 'قتيبة بن سعيد').click();
    await page.keyboard.press('Escape'); // closes the details
    await expect(page.getByRole('tabpanel', { name: 'الراوي' })).toBeHidden();

    await expect(card(page, 'عبد الله بن الزبير الحميدي')).toHaveCSS('opacity', '0.25');
    await expect(card(page, 'قتيبة بن سعيد')).toHaveCSS('opacity', '1');

    // Escape clears the selection. (While the pointer rests on the card, hovering still previews its chain.)
    await page.keyboard.press('Escape');
    await page.mouse.move(5, 300);
    await expect(card(page, 'عبد الله بن الزبير الحميدي')).toHaveCSS('opacity', '1');
  });

  test('a click on empty canvas clears it', async ({ page }) => {
    await card(page, 'قتيبة بن سعيد').click();
    await page.keyboard.press('Escape');
    await expect(card(page, 'عبد الله بن الزبير الحميدي')).toHaveCSS('opacity', '0.25');

    await page.locator('.react-flow__pane').click({ position: { x: 5, y: 5 } });
    await expect(card(page, 'عبد الله بن الزبير الحميدي')).toHaveCSS('opacity', '1');
  });

  test('Enter on a focused card opens its details', async ({ page }) => {
    await card(page, 'عمر بن الخطاب').focus();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('tabpanel', { name: 'الراوي' })).toBeVisible();
  });
});

test.describe('a narrator without a grade', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);
  });

  test('is «غير مُقيَّم», dashed, and not mistaken for weak', async ({ page }) => {
    const unrated = card(page, 'قتيبة بن سعيد');
    await expect(unrated).toContainText('غير مُقيَّم');
    await expect(unrated.locator('> div')).toHaveCSS('border-style', 'dashed');

    // «إبراز الضعفاء» dims the reliable and the unrated alike.
    await page.getByRole('button', { name: 'إبراز الضعفاء' }).click();
    await expect(unrated.locator('> div')).toHaveCSS('opacity', '0.3');
  });

  test('the legend lists it', async ({ page }) => {
    await expect(page.getByText('غير مُقيَّم (لا حكم)')).toBeVisible();
  });
});

test.describe('edge labels on a big graph', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/takhreej?ids=big,h1'); // the fixture returns its 14-link graph for `big`
    await graphReady(page);
  });

  test('are markers at rest and printed on the focused chain', async ({ page }) => {
    const marker = page.locator('[data-edge-marker="e-N2-N1"]');
    await expect(page.locator('.react-flow__edge')).toHaveCount(14);
    await expect(marker).toHaveAttribute('aria-label', /عنعنة مدلس/);
    await expect(page.locator('.react-flow__edge-text')).toHaveCount(0);

    await card(page, 'عبد الله بن الزبير الحميدي').click();
    await page.keyboard.press('Escape'); // close the details, keep the selection
    await expect(page.locator('.react-flow__edge-text', { hasText: 'عنعنة مدلس' })).toBeVisible();
    await expect(marker).toHaveCount(0);

    await page.keyboard.press('Escape'); // clear the selection
    await page.mouse.move(5, 300); // and let go of the hover preview
    await expect(marker).toHaveCount(1);
  });
});

test.describe('the toolbar and the graph', () => {
  test('no narrator card sits under the toolbar', async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h4');
    await graphReady(page);

    const covered = await page.evaluate(() => {
      const bar = document.querySelector('[role=toolbar]')!.getBoundingClientRect();
      return [...document.querySelectorAll('.react-flow__node')].filter((n) => {
        const r = n.getBoundingClientRect();
        return r.left < bar.right && r.right > bar.left && r.top < bar.bottom && r.bottom > bar.top;
      }).length;
    });
    expect(covered).toBe(0);
  });
});

test.describe('the legend and a big graph', () => {
  test('no narrator card sits under the open legend', async ({ page }) => {
    await page.goto('/takhreej?ids=big,h1');
    await graphReady(page);

    const overlaps = () =>
      page.evaluate(() => {
        const legend = document.querySelector('.react-flow__panel.top.right')?.getBoundingClientRect();
        if (!legend) return -1;
        return [...document.querySelectorAll('.react-flow__node')].filter((n) => {
          const r = n.getBoundingClientRect();
          return r.left < legend.right && r.right > legend.left && r.top < legend.bottom && r.bottom > legend.top;
        }).length;
      });

    expect(await overlaps()).toBe(0);

    // Closing the legend gives the room back; opening it again reserves it again.
    await page.getByRole('button', { name: 'تصغير المفتاح' }).click();
    await page.getByRole('button', { name: /مفتاح الرموز/ }).click();
    await expect.poll(overlaps).toBe(0);
  });
});

test.describe('the toolbar', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);
  });

  const toolbar = (page: Page) => page.getByRole('toolbar', { name: 'أدوات الشجرة' });

  test('has every tool in one place', async ({ page }) => {
    for (const name of ['بحث عن راوٍ', 'إبراز الضعفاء', 'عرض مضغوط للرواة', 'خريطة الشجرة', 'مفتاح الرموز', 'تكبير', 'تصغير', 'ملاءمة الشجرة للشاشة', 'تصدير صورة']) {
      await expect(toolbar(page).getByRole('button', { name })).toBeVisible();
    }
  });

  test('the legend button opens and closes the legend', async ({ page }) => {
    const legendButton = toolbar(page).getByRole('button', { name: 'مفتاح الرموز' });
    await expect(legendButton).toHaveAttribute('aria-pressed', 'true'); // open beside the graph on a wide screen
    await legendButton.click();
    await expect(page.getByText('مفتاح الرموز والمصطلحات')).toBeHidden();
    await legendButton.click();
    await expect(page.getByText('مفتاح الرموز والمصطلحات')).toBeVisible();
  });

  test('zoom buttons change the view and fit brings it back', async ({ page }) => {
    const viewport = page.locator('.react-flow__viewport');
    const fit = toolbar(page).getByRole('button', { name: 'ملاءمة الشجرة للشاشة' });

    await fit.click();
    await page.waitForTimeout(300);
    const fitted = await viewport.getAttribute('style');

    await toolbar(page).getByRole('button', { name: 'تكبير' }).click();
    await expect.poll(() => viewport.getAttribute('style')).not.toBe(fitted);
    await fit.click();
    await expect.poll(() => viewport.getAttribute('style')).toBe(fitted);
  });

  test('find a narrator: type part of the name, press Enter, and the card is selected', async ({ page }) => {
    await toolbar(page).getByRole('button', { name: 'بحث عن راوٍ' }).click();
    const box = page.getByRole('combobox', { name: 'ابحث عن راوٍ في الشجرة' });
    await expect(box).toBeFocused();

    await box.fill('قتيبه'); // taa marbuta typed as haa still finds «قتيبة»
    const option = page.getByRole('option', { name: /قتيبة بن سعيد/ });
    await expect(option).toBeVisible();
    await box.press('Enter');

    await expect(card(page, 'قتيبة بن سعيد').locator('> div')).toHaveClass(/ring-brand-blue/);
    await expect(box).toBeHidden();
  });

  test('find says so when nobody matches, and Escape closes it', async ({ page }) => {
    await toolbar(page).getByRole('button', { name: 'بحث عن راوٍ' }).click();
    const box = page.getByRole('combobox', { name: 'ابحث عن راوٍ في الشجرة' });
    await box.fill('زيد');
    await expect(page.getByText('لا راوٍ بهذا الاسم في هذه الشجرة.')).toBeVisible();
    await box.press('Escape');
    await expect(box).toBeHidden();
  });

  test('card detail: compact shows the name and grade only, detailed brings the rest back', async ({ page }) => {
    const sufyan = card(page, 'سفيان بن عيينة');
    await expect(sufyan).toContainText('الثامنة'); // detailed by default on a small graph
    await expect(sufyan).toContainText('مدلس');

    await toolbar(page).getByRole('button', { name: 'عرض مضغوط للرواة' }).click();
    await expect(sufyan).not.toContainText('الثامنة');
    await expect(sufyan).toContainText('صدوق'); // the grade stays
    await expect(page.locator('div.absolute.inset-0.bg-surface-muted')).toHaveClass(/opacity-100/); // no blink-out

    await toolbar(page).getByRole('button', { name: 'عرض مفصّل للرواة' }).click();
    await expect(sufyan).toContainText('الثامنة');
  });
});

test.describe('a very large graph', () => {
  test('starts compact with the minimap on, and both can be switched', async ({ page }) => {
    await page.goto('/takhreej?ids=huge,h1');
    await graphReady(page);

    const toolbar = page.getByRole('toolbar', { name: 'أدوات الشجرة' });
    await expect(page.locator('.react-flow__node')).toHaveCount(35);
    await expect(card(page, 'سفيان بن عيينة')).not.toContainText('الثامنة'); // compact by default
    await expect(page.locator('.react-flow__minimap')).toBeVisible();

    // The minimap does not sit on the legend.
    const [map, legend] = await Promise.all([
      page.locator('.react-flow__minimap').boundingBox(),
      page.locator('.react-flow__panel.top.right').boundingBox(),
    ]);
    const overlap = map!.x < legend!.x + legend!.width && map!.x + map!.width > legend!.x && map!.y < legend!.y + legend!.height && map!.y + map!.height > legend!.y;
    expect(overlap).toBe(false);

    await toolbar.getByRole('button', { name: 'خريطة الشجرة' }).click();
    await expect(page.locator('.react-flow__minimap')).toHaveCount(0);

    await toolbar.getByRole('button', { name: 'عرض مفصّل للرواة' }).click();
    await expect(card(page, 'سفيان بن عيينة')).toContainText('الثامنة');
  });

  test.describe('on a phone', () => {
    test.use({ viewport: { width: 375, height: 812 } });

    test('has no minimap by default', async ({ page }) => {
      await page.goto('/takhreej?ids=huge,h1');
      await graphReady(page);
      await expect(page.locator('.react-flow__minimap')).toHaveCount(0);
      expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(0);
    });
  });
});

test.describe('the takhreej summary', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);
  });

  test('gives the routes, madar, grade and findings at a glance', async ({ page }) => {
    const summary = page.getByRole('region', { name: 'ملخص التخريج' });
    await expect(summary).toContainText('3 روايات من 3 كتب');
    await expect(summary).toContainText('سفيان بن عيينة');
    await expect(summary).toContainText('صحيح');
    // One real finding; the low-confidence note is not counted.
    await expect(summary.getByRole('button', { name: '1 علة قادحة' })).toBeVisible();
  });

  test('a madar chip selects that narrator on the graph', async ({ page }) => {
    await page.getByRole('region', { name: 'ملخص التخريج' }).getByRole('button', { name: 'سفيان بن عيينة' }).click();
    await expect(card(page, 'سفيان بن عيينة').locator('> div')).toHaveClass(/ring-brand-blue/);
  });

  test('a severity pill opens the findings', async ({ page }) => {
    await page.getByRole('region', { name: 'ملخص التخريج' }).getByRole('button', { name: '1 علة قادحة' }).click();
    await expect(page.locator('#workspace-panel')).toContainText('عنعنة مدلس');
  });
});

test.describe('the source chips', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);
  });

  test('a chip highlights that book\'s routes and shows its matn', async ({ page }) => {
    const header = page.locator('header', { has: page.getByRole('heading', { name: 'شجرة التخريج المقارنة' }) });
    const chip = header.getByRole('button', { name: /صحيح مسلم/ }).filter({ hasText: '1907' });
    await chip.click();
    await expect(chip).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByRole('button', { name: /إبراز: صحيح مسلم/ })).toBeVisible();
    await expect(page.locator('#matn-card-h2')).toBeInViewport();
  });

  test('removing a narration updates the address and the graph; two must remain', async ({ page }) => {
    await page.getByRole('button', { name: /إزالة صحيح مسلم 1907/ }).click();
    await expect(page).toHaveURL(/ids=h1,h3$/);
    await expect(page.getByRole('region', { name: 'ملخص التخريج' })).toContainText('2 روايات من 2 كتب');
    await expect(page.getByRole('button', { name: /إزالة/ }).first()).toBeDisabled();
  });
});

test.describe('the matn cards', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);
  });

  test('a card\'s book button highlights that book\'s routes', async ({ page }) => {
    const button = page.locator('#matn-card-h2').getByRole('button', { name: /صحيح مسلم/ });
    await button.click();
    await expect(button).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByRole('button', { name: /إبراز: صحيح مسلم/ })).toBeVisible();
    await expect(page.locator('#matn-card-h2')).toHaveClass(/border-brand-blue/);
  });

  test('«قارن بالأصل» shows the words that differ from the first narration, without the isnads', async ({ page }) => {
    await expect(page.locator('#matn-card-h1')).toContainText('الأصل');
    await expect(page.locator('#matn-card-h1').getByRole('button', { name: 'قارن بالأصل' })).toHaveCount(0);

    const card = page.locator('#matn-card-h2');
    await card.getByRole('button', { name: 'قارن بالأصل' }).click();
    await expect(card).toContainText('المرجع:');
    await expect(card).toContainText('التشابه:');
    // The ending Muslim's narration leaves out is struck through; neither isnad is compared.
    await expect(card.locator('del', { hasText: 'فمن كانت هجرته' })).toBeVisible();
    await expect(card.locator('p').last()).not.toContainText('الحميدي');

    await card.getByRole('button', { name: 'إخفاء المقارنة' }).click();
    await expect(card.locator('del')).toHaveCount(0);
  });

  test('with one Companion there is no witnesses section', async ({ page }) => {
    await expect(page.getByRole('region', { name: 'الشواهد' })).toHaveCount(0);
  });
});

test.describe('shawahid', () => {
  test('a narration through another Companion is shown apart, and marked on its chip and in the summary', async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h4');
    await graphReady(page);

    const panel = page.locator('#workspace-panel');
    const witnesses = panel.getByRole('region', { name: 'الشواهد' });
    await expect(witnesses).toContainText('سنن ابن ماجه');
    await expect(witnesses).toContainText('شاهد: علي');
    await expect(witnesses).toContainText('ما تزال داخل الشجرة');
    // Only the two routes of the first hadith are counted as routes.
    await expect(panel.getByText('2 روايات', { exact: true })).toBeVisible();
    await expect(panel.locator('#matn-card-h4')).toBeVisible();

    await expect(page.getByRole('region', { name: 'ملخص التخريج' })).toContainText('(منها شاهد)');
    const header = page.locator('header', { has: page.getByRole('heading', { name: 'شجرة التخريج المقارنة' }) });
    await expect(header.getByRole('button', { name: /ابن ماجه \| 4227/ })).toContainText('شاهد');
  });
});

test.describe('Ilal findings and the graph', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);
  });

  test('picking a finding brings its narrators into view and rings them', async ({ page }) => {
    const viewport = page.locator('.react-flow__viewport');
    const before = await viewport.getAttribute('style');

    await page.locator('#workspace-panel').getByRole('tab', { name: /العلل/ }).click();
    await page.locator('#workspace-panel').getByRole('button', { name: /عنعنة مدلس/ }).first().click();

    await expect(card(page, 'سفيان بن عيينة').locator('> div')).toHaveClass(/ring-rose-400/);
    await expect(async () => expect(await viewport.getAttribute('style')).not.toBe(before)).toPass();
  });

  test('a low-confidence finding is not drawn on an edge until its group is opened', async ({ page }) => {
    const path = edge(page, 'e-N4-N2').locator('path.react-flow__edge-path');
    await expect(path).toHaveCSS('stroke', 'rgb(51, 65, 85)'); // shared by three books, no finding on it

    await page.locator('#workspace-panel').getByRole('tab', { name: /العلل/ }).click();
    await page.getByRole('button', { name: /ملاحظات منخفضة الثقة/ }).click();
    await expect(path).toHaveCSS('stroke', 'rgb(217, 119, 6)');
    await expect(edge(page, 'e-N4-N2').locator('.react-flow__edge-text')).toContainText('لم يثبت اللقاء');
  });
});

test.describe('the side panel', () => {
  test('on a wide screen the narrator opens as a tab, and the graph stays in view and selected', async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);

    await card(page, 'عمر بن الخطاب').click();
    const details = page.getByRole('tabpanel', { name: 'الراوي' });
    await expect(details).toBeVisible();
    await expect(page.getByRole('dialog')).toHaveCount(0); // not a modal: nothing covers the graph
    await expect(page.locator('#workspace-panel').getByRole('tab', { name: 'الراوي' })).toHaveAttribute('aria-selected', 'true');
    await expect(card(page, 'عمر بن الخطاب').locator('> div')).toHaveClass(/ring-brand-blue/);

    // Closing it returns to the tab that was showing.
    await details.getByRole('button', { name: 'إغلاق تفاصيل الراوي' }).click();
    await expect(details).toBeHidden();
    await expect(page.locator('#workspace-panel').getByRole('tab', { name: 'المتون' })).toHaveAttribute('aria-selected', 'true');
  });

  test.describe('on a tablet', () => {
    test.use({ viewport: { width: 800, height: 900 } });

    test('the narrator opens as a drawer over the graph', async ({ page }) => {
      await page.goto('/tree/x');
      await graphReady(page);
      await card(page, 'راو متروك للاختبار').click();
      await expect(page.getByRole('dialog', { name: 'تفاصيل الراوي' })).toBeVisible();
    });
  });
});

test.describe('the address keeps the view', () => {
  test('the open tab and the selected narrator survive a reload', async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);

    await page.locator('#workspace-panel').getByRole('tab', { name: /العلل/ }).click();
    await expect(page).toHaveURL(/tab=ilal/);
    await card(page, 'عمر بن الخطاب').click();
    await expect(page).toHaveURL(/narrator=N4/);
    await expect(page).toHaveURL(/ids=h1,h2,h3/); // the comparison's own parameter is kept

    await page.reload();
    await graphReady(page);
    await expect(page.getByRole('tabpanel', { name: 'الراوي' })).toBeVisible();
    await expect(card(page, 'عمر بن الخطاب').locator('> div')).toHaveClass(/ring-brand-blue/);

    await page.getByRole('button', { name: 'إغلاق تفاصيل الراوي' }).click();
    await expect(page).not.toHaveURL(/narrator=/);
    await expect(page.locator('#workspace-panel').getByRole('tab', { name: /العلل/ })).toHaveAttribute('aria-selected', 'true');
  });

  test('a focused book is restored from the link', async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3&book=' + encodeURIComponent('صحيح مسلم'));
    await graphReady(page);
    await expect(page.getByRole('button', { name: /إبراز: صحيح مسلم/ })).toBeVisible();

    await page.keyboard.press('Escape');
    await expect(page).not.toHaveURL(/book=/);
  });
});

test.describe('the takhreej title', () => {
  test('says which hadith is compared', async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);
    await expect(page).toHaveTitle('تخريج صحيح البخاري 1 و2 روايات أخرى | شجرة الأسانيد الذكية');
  });
});

test.describe('resizing the side panel', () => {
  const width = async (page: Page) => Math.round((await page.locator('#workspace-panel').boundingBox())!.width);

  test.beforeEach(async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);
    await page.evaluate(() => localStorage.clear());
  });

  test('dragging the edge changes the width, within limits, and it is remembered', async ({ page }) => {
    expect(await width(page)).toBe(384);

    const edge = page.getByRole('separator', { name: 'تغيير عرض اللوحة' });
    const box = (await edge.boundingBox())!;
    await page.mouse.move(box.x + box.width / 2, box.y + 200);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width / 2 + 100, box.y + 200, { steps: 5 });
    await page.mouse.up();
    await expect.poll(() => width(page)).toBe(484);

    // Never narrower than 320 or wider than 640.
    await edge.focus();
    await page.keyboard.press('Home');
    await expect.poll(() => width(page)).toBe(320);
    await page.keyboard.press('ArrowLeft');
    await expect.poll(() => width(page)).toBe(320);
    await page.keyboard.press('End');
    await expect.poll(() => width(page)).toBeLessThanOrEqual(640);
    await page.keyboard.press('Home');
    await page.keyboard.press('ArrowRight');
    await expect.poll(() => width(page)).toBe(344);

    await page.reload();
    await graphReady(page);
    await expect.poll(() => width(page)).toBe(344);
  });

  test('the last tab is remembered on a fresh visit', async ({ page }) => {
    await page.locator('#workspace-panel').getByRole('tab', { name: /العلل/ }).click();
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);
    await expect(page.locator('#workspace-panel').getByRole('tab', { name: /العلل/ })).toHaveAttribute('aria-selected', 'true');
  });
});

test.describe('the single tree workspace', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/tree/x');
    await graphReady(page);
  });

  test('the matn tab has the full text and the book', async ({ page }) => {
    await page.getByRole('button', { name: 'إظهار المتن والعلل' }).click();
    const panel = page.locator('#workspace-panel');
    await expect(panel.getByRole('tab', { name: 'المتن' })).toHaveAttribute('aria-selected', 'true');
    await expect(panel).toContainText('حديث رقم 1');
    await expect(panel.getByRole('link', { name: /أبواب صحيح البخاري/ })).toBeVisible();
  });

  test('«التخريج المقارن» gathers the related routes and opens the comparison', async ({ page }) => {
    await page.getByRole('button', { name: 'التخريج المقارن' }).click();
    await expect(page).toHaveURL(/\/takhreej\?ids=x,h2,h3/);
  });

  test('the hadith ruling note stays in the header', async ({ page }) => {
    await expect(page.getByText('حكم الحديث:')).toBeVisible();
    await expect(page.locator('#ruling-note')).toBeVisible();
  });

  test('the book name links to its chapters', async ({ page }) => {
    await expect(page.getByRole('link', { name: 'صحيح البخاري', exact: true })).toHaveAttribute('href', /\/books\//);
  });
});

test.describe('on a phone', () => {
  test.use({ viewport: { width: 375, height: 812 } });

  test('takhreej starts with the graph, and the matns open over it', async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);

    expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(0);
    await expect(page.locator('#workspace-panel')).toHaveCSS('width', '0px');

    await page.getByRole('button', { name: 'إظهار المتون' }).click();
    await expect(page.locator('#workspace-panel')).toContainText('متون الروايات');
    await page.getByRole('button', { name: /إغلاق والعودة للشجرة/ }).click();
    await expect(page.locator('#workspace-panel')).toHaveCSS('width', '0px');
  });

  test('the tree page fits the screen and the legend starts collapsed', async ({ page }) => {
    await page.goto('/tree/x');
    await graphReady(page);

    expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(0);
    await expect(page.getByRole('button', { name: /مفتاح الرموز/ })).toBeVisible();
    await card(page, 'راو متروك للاختبار').click(); // not covered by an open legend
    await expect(page.getByRole('dialog', { name: 'تفاصيل الراوي' })).toBeVisible();
  });

  test('picking a finding closes the sidebar so the graph can be seen', async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);

    await page.getByRole('button', { name: 'إظهار المتون' }).click();
    await page.locator('#workspace-panel').getByRole('tab', { name: /العلل/ }).click();
    await page.locator('#workspace-panel').getByRole('button', { name: /عنعنة مدلس/ }).first().click();

    await expect(page.locator('#workspace-panel')).toHaveCSS('width', '0px');
    await expect(card(page, 'سفيان بن عيينة').locator('> div')).toHaveClass(/ring-rose-400/);
  });

  test('the tree page header keeps its three buttons on one row', async ({ page }) => {
    await page.goto('/tree/x');
    await graphReady(page);

    const tops = await Promise.all(
      ['التخريج المقارن', /فحص العلل/, 'عودة للبحث'].map(async (name) => {
        const box = await page.getByRole('button', { name }).first().boundingBox();
        return Math.round(box!.y);
      })
    );
    expect(Math.max(...tops) - Math.min(...tops)).toBeLessThanOrEqual(4);
  });

  test('the ruling note opens from its badge', async ({ page }) => {
    await page.goto('/tree/x');
    await graphReady(page);

    const note = page.locator('#ruling-note');
    await expect(note).toBeHidden();
    await page.getByRole('button', { name: 'تفاصيل', exact: true }).click();
    await expect(note).toBeVisible();
    await expect(note).toContainText('الدرر السنية');
  });
});

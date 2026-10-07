import { test, expect } from '@playwright/test';
import { alignTokens, compareMatns, extractBody, normalizeForComparison, tokenize } from '../src/features/isnad-tree/utils/matnDiff';

// The expectations of extractBody come from the server's MatnAlignerTests.cs: the port must cut the same way.
test.describe('extractBody (port of MatnText.ExtractBody)', () => {
  test('strips the isnad and the trailing commentary', () => {
    expect(
      extractBody('حدثنا قتيبة حدثنا الليث عن نافع عن ابن عمر أن رسول الله صلى الله عليه وسلم قال لا يبع بعضكم على بيع بعض قال أبو عيسى هذا حديث حسن صحيح')
    ).toBe('ان رسول الله قال لا يبع بعضكم علي بيع بعض');
  });

  test('a mawquf text skips past the isnad', () => {
    expect(extractBody('حدثنا وكيع عن سفيان عن منصور عن إبراهيم قال كانوا يكرهون ذلك')).toBe('قال كانوا يكرهون ذلك');
  });

  test('cuts a compiler\'s note and a modern editor\'s grading', () => {
    const bayhaqi = extractBody('أخبرنا أبو عبد الله الحافظ عن المغيرة بن شعبة قال كان النبي صلى الله عليه وسلم إذا ذهب المذهب أبعد قال الشيخ إسماعيل هو ابن جعفر ومحمد هو ابن عمرو');
    const khuzaymah = extractBody('ثنا علي بن حجر عن المغيرة بن شعبة قال كان النبي صلى الله عليه وسلم إذا ذهب المذهب أبعد قال الأعظمي إسناده حسن');
    expect(bayhaqi).toBe('كان النبي اذا ذهب المذهب ابعد');
    expect(khuzaymah).toBe('كان النبي اذا ذهب المذهب ابعد');
  });

  test('cuts the notes, pointers, bracketed editor text and a pasted isnad that the server cuts', () => {
    const isnad = 'حدثنا عفان حدثنا أبان عن يحيى عن أبي مالك الأشعري أن رسول الله ﷺ قال: ';
    const matn = 'الطهور شطر الإيمان والحمد لله تملأ الميزان';
    const expected = 'ان رسول الله قال الطهور شطر الايمان والحمد لله تملا الميزان';
    for (const tail of [
      '. أخرجه مسلم في الصحيح عن إسحاق',
      '. وكذلك رواه معاذ بن معاذ',
      ' [حكم حسين سليم أسد]: إسناده صحيح',
      ' [٦٦١]',
      ' ب د ع ف م تحفه اتحاف',
      ' تفرد به عثمان عن الدراوردي',
      ' فذكر مثله إلا أنه قال: الصلاة برهان',
      ' وهذا الحديث صحيح',
    ]) {
      expect(extractBody(isnad + matn + tail), tail).toBe(expected);
    }
    expect(extractBody('سمعت النبي ﷺ يقول: الحلال بين والحرام بين. حدثنا علي بن عبد الله حدثنا ابن عيينة')).toBe('سمعت النبي يقول الحلال بين والحرام بين');
  });

  test('a Companion saying "haddathana rasul Allah" is not a pasted isnad', () => {
    expect(extractBody('قال عبد الله: حدثنا رسول الله ﷺ وهو الصادق المصدوق: إن أحدكم يجمع خلقه')).toContain('يجمع خلقه');
  });

  test('empty in, empty out', () => {
    expect(extractBody('')).toBe('');
    expect(extractBody(null)).toBe('');
  });
});

test.describe('normalizeForComparison', () => {
  test('drops diacritics, honorifics and punctuation and unifies letter forms', () => {
    expect(normalizeForComparison('إِنَّمَا الأَعْمَالُ بِالنِّيَّةِ، ﷺ')).toBe('انما الاعمال بالنيه');
    expect(tokenize('رضي الله عنه  قال')).toEqual(['قال']);
  });
});

test.describe('alignTokens (port of MatnAligner)', () => {
  test('equal runs, an addition and an omission', () => {
    const { segments, similarity } = alignTokens(['a', 'b', 'c', 'd'], ['a', 'x', 'c', 'd', 'e']);
    expect(segments).toEqual([
      { kind: 'equal', text: 'a' },
      { kind: 'removed', text: 'b' },
      { kind: 'added', text: 'x' },
      { kind: 'equal', text: 'c d' },
      { kind: 'added', text: 'e' },
    ]);
    expect(similarity).toBeCloseTo((2 * 3) / 9);
  });

  test('identical texts are one equal run; two empty texts are fully similar', () => {
    expect(alignTokens(['a', 'b'], ['a', 'b'])).toEqual({ segments: [{ kind: 'equal', text: 'a b' }], similarity: 1 });
    expect(alignTokens([], [])).toEqual({ segments: [], similarity: 1 });
  });
});

test.describe('compareMatns', () => {
  const bukhari = 'حدثنا الحميدي حدثنا سفيان عن يحيى بن سعيد أن رسول الله صلى الله عليه وسلم قال إنما الأعمال بالنيات وإنما لكل امرئ ما نوى فمن كانت هجرته إلى الله ورسوله';
  const muslim = 'عن عمر بن الخطاب رضي الله عنه قال سمعت رسول الله صلى الله عليه وسلم يقول إنما الأعمال بالنيات وإنما لكل امرئ ما نوى';

  test('the isnads do not count: only the matn is compared', () => {
    const c = compareMatns({ hadithId: 'a', text: bukhari }, { hadithId: 'b', text: muslim });
    expect(c.referenceHadithId).toBe('a');
    expect(c.comparedHadithId).toBe('b');
    const kinds = c.segments.map((s) => s.kind);
    expect(kinds).toContain('equal');
    // Nothing of either isnad (the narrators' names) shows up in the comparison.
    expect(c.segments.map((s) => s.text).join(' ')).not.toMatch(/الحميدي|سفيان|عمر بن الخطاب/);
    // The ending Muslim's narration leaves out is the last omitted run; the opening verbs differ too.
    const omitted = c.segments.filter((s) => s.kind === 'removed');
    expect(omitted.at(-1)?.text).toBe('فمن كانت هجرته الي الله ورسوله');
    expect(c.similarity).toBeGreaterThan(0.6);
  });
});

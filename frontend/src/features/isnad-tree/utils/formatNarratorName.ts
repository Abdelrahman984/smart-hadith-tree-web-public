const GENERIC_HONORIFICS = new Set([
  'الحافظ',
  'الإمام',
  'الشيخ',
  'شيخ الإسلام',
  'العلامة',
  'الفقيه',
  'المحدث',
  'القاضي',
  'الأمير',
  'الزاهد',
  'العابد',
  'الشهيد',
  'المقرئ',
  'النحوي',
  'اللغوي',
  'المفسر',
  'المؤرخ',
  'الأصولي',
  'الفرضي',
  'الأديب',
  'الشاعر',
  'الخطيب',
  'الواعظ',
  'الطبيب',
  'الفأفأ',
  'الفأفأ.',
  'الأحول',
  'الأحدب',
  'الصغير',
  'الكبير',
  'الطويل',
  'القصير',
  'الأعرج',
  'الأعمى',
  'البصير',
  'الضرير',
  'الأصم',
  'الأشج',
  'الوراق',
  'القطان',
  'البزاز',
  'الطحان',
  'الخياط',
  'الحذاء',
  'الصفار',
  'العطار',
  'البقال',
  'القصاب',
  'النجار',
  'الحداد',
  'الصائغ',
  'الصيرفي',
  'الجوهري',
  'الحريري',
  'الكتاني',
  'الأنماطي',
  'القراطيسي',
  'ذو الأذنين',
  'ذو العينين',
  'حبر الأمة',
  'حبر العرب',
  'البحر',
  'الحميراء',
]);

/**
 * Formats a narrator's full name to display only the first two names (الاسم الثنائي: اسم الراوي واسم أبيه),
 * e.g. "عمر بن الخطاب", "محمد بن إسماعيل", "عبد الله بن الزبير", "هند بنت أبي أمية".
 */
export function formatTwoPartNarratorName(rawName: string | null | undefined): string {
  if (!rawName) return '';

  let name = rawName.trim();

  // 1. Remove leading numbering, parentheses, braces, slashes, e.g. "( 1564 ) ", "123 - ", "/ "
  name = name.replace(/^[\s\(\[\{/\\0-9\-\.\)]+/, '').trim();

  // 1b. Handle "محمد بن ماجه : يزيد" where ": <realFatherName>" clarifies the grandfather/mother nickname
  name = name.replace(/^(\S+\s+(?:بن|ابن)\s+)\S+\s*:\s*(\S+)$/, '$1$2');

  // 2. Truncate trailing annotations, variants or glosses after punctuation:
  // e.g. " ، وقيل : ...", " [وهو ...]", " : القرد"
  name = name.split(/\s*[\(\[\{،]|\s+:\s+/)[0].trim();

  // 3. Remove biographical narrative suffixes like " عن ...", " روى عنه ...", " ذكره ...", " صاحب ...", " حليف ..."
  const bioCutoff = name.search(/\s+(عن|روى|وثقه|ذكره|صاحب|حليف|مولى|مولاهم)\b/);
  if (bioCutoff > 0) {
    name = name.substring(0, bioCutoff).trim();
  }

  // 4. Handle title / shuhrah prefixes:
  // e.g. "ابن ماجه محمد بن يزيد" -> "محمد بن يزيد"
  // e.g. "أبو هريرة عبد الرحمن بن صخر" -> "عبد الرحمن بن صخر"
  // e.g. "الإمام البخاري محمد بن إسماعيل" -> "محمد بن إسماعيل"
  // Use negative lookahead so compound Kunyas like "أبو بكر بن أبي شيبة" are NOT stripped!
  const prefixTitleMatch = name.match(
    /^(?:ابن|أبو|ابو|الإمام|الشيخ)\s+\S+\s+(?!(?:بن|ابن|بنت)\b)([^\s].*?\s+(?:بن|ابن|بنت)\s+.*)$/
  );
  if (prefixTitleMatch) {
    name = prefixTitleMatch[1].trim();
  }

  // 5. Match patronymic connectors: "بن", "ابن", "بنت"
  const connectorRegex = /\s+(?:بن|ابن|بنت)\s+/g;
  const matches = [...name.matchAll(connectorRegex)];

  if (matches.length === 0) {
    // No patronymic connector found. Handle compound names (عبد الله, أبو بكر) or return first 2 words.
    const words = name.split(/\s+/);
    if (words.length <= 2) return name;
    if (['عبد', 'أبو', 'أبي', 'ابو', 'ابي', 'أم', 'ام'].includes(words[0])) {
      return words.slice(0, 3).join(' ');
    }
    return words.slice(0, 2).join(' ');
  }

  // First connector connects the narrator to their father
  const firstMatch = matches[0];
  const firstConnector = firstMatch[0].trim(); // "بن", "ابن", or "بنت"
  const firstName = name.substring(0, firstMatch.index).trim();
  const afterFirstConnector = name.substring(firstMatch.index + firstMatch[0].length).trim();

  // Extract father's name: stops before the second connector if present
  let fatherName = '';
  if (matches.length >= 2) {
    const secondConnectorRelIndex = afterFirstConnector.search(/\s+(?:بن|ابن|بنت)\s+/);
    if (secondConnectorRelIndex !== -1) {
      fatherName = afterFirstConnector.substring(0, secondConnectorRelIndex).trim();
    } else {
      fatherName = afterFirstConnector.trim();
    }
  } else {
    // Only one connector in the whole string
    // Father's name might be compound (e.g. "عبد الله", "أبي شيبة", "أبي طالب") or single word
    const words = afterFirstConnector.split(/\s+/);
    if (['عبد', 'أبي', 'أبو', 'ابي', 'ابو', 'أم', 'ام'].includes(words[0]) && words.length >= 2) {
      fatherName = words.slice(0, 2).join(' ');
    } else {
      fatherName = words[0];
    }
  }

  // Clean fatherName: if there are more than 2 words and no second connector was found,
  // trim trailing nisbah/epithets unless it's a compound name
  const fatherWords = fatherName.split(/\s+/);
  if (fatherWords.length > 2) {
    if (['عبد', 'أبي', 'أبو', 'ابي', 'ابو', 'أم', 'ام'].includes(fatherWords[0])) {
      fatherName = fatherWords.slice(0, 2).join(' ');
    } else {
      fatherName = fatherWords[0];
    }
  }

  return `${firstName} ${firstConnector} ${fatherName}`.trim();
}

/**
 * Formats a narrator's display name for comparative tree cards:
 * prefers distinctive scholarly titles in `knownAs` (e.g. "أبو بكر بن أبي شيبة", "أبو العباس الأصم")
 * or well-known nisbahs/shuhrahs (e.g. "إسماعيل بن علية") while filtering out generic honorifics ("الحافظ").
 */
export function formatScholarlyNarratorName(
  rawName: string | null | undefined,
  knownAs?: string | null
): string {
  const full = (rawName || '').trim();

  if (full.startsWith('إسماعيل بن إبراهيم بن مقسم')) {
    return 'إسماعيل بن علية';
  }

  if (knownAs && knownAs.trim()) {
    const parts = knownAs
      .split(/[،,]/)
      .map((p) => p.trim())
      .filter((p) => p.length > 1 && !GENERIC_HONORIFICS.has(p));

    if (parts.length > 0) {
      const candidate = parts[0];
      // Use multi-word scholarly shuhrah directly (e.g. "أبو بكر بن أبي شيبة", "أبو العباس الأصم", "أبو سلمة بن عبد الرحمن الزهري")
      if (candidate.includes(' ')) {
        return candidate;
      }
    }
  }

  return formatTwoPartNarratorName(rawName);
}


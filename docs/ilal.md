# Ilal Engine — علل الحديث

The Ilal engine looks across the turuq of a hadith for hidden defects. Its findings are
**heuristic aids for the researcher (المحقق), not a final verdict**. Each finding carries the
evidence behind it and a confidence score.

## Pipeline

1. **Gather turuq**
   - `GET /api/ilal?ids=...` analyzes the given hadiths, with a cap of 30.
   - `GET /api/ilal/{hadithId}` collects related hadiths automatically, using the same matn match as `/api/takhreej/related`.
   - `GET /api/takhreej` also embeds `ilalReport` in its response.
2. **Load the context** (`IlalAnalysisService`)
   - Build one isnad path per hadith from `Transmissions`, starting at step 1 and following student → sheikh.
   - Narrator grades, mudallis tiers and ikhtilat data come from `Narrators`.
   - Known teacher/student pairs come from `NarratorRelations`.
   - Before/after-ikhtilat hearings come from `MukhtalitHearings`.
3. **Run the rules** in memory. They live in `src/SmartHadithTree.Application/Services/Ilal/Rules/`.
4. **Grade adjustment** (`TaqwiyahService`)
   - If every tariq carries a decisive defect (علة قادحة), the grade becomes **ضعيف (معلول)**.
   - Otherwise the grade is kept and a warning is added to the details.
5. **Optional AI explanation.** `POST /api/ilal/explain` sends only the report's findings to Gemini through Semantic Kernel, with instructions not to add claims.

Severity levels: `Qadihah` (قادحة) · `GhayrQadihah` (غير قادحة) · `Tanbih` (تنبيه).

## Rules

| Rule | Type(s) | Trigger | Severity |
|---|---|---|---|
| `TadlisRule` | تدليس | A mudallis of Ibn Hajr tier ≥ 3 narrates with عن / أن / قال / ذكر. | Qadihah. Tanbih if he states hearing from the same sheikh in another tariq. GhayrQadihah if that link appears in al-Bukhari or Muslim. |
| `IkhtilatRule` | اختلاط | The sheikh is a mukhtalit. | Heard after → Qadihah (GhayrQadihah in the Sahihayn). Unknown → Tanbih. Heard before → no finding. |
| `HiddenInqitaRule` | انقطاع خفي | The sheikh–student pair is in neither narrator's teacher or student list, **and both narrators have relation data**. | Tanbih |
| `MatnAtMadarRule` | زيادة / شذوذ / نكارة / اضطراب | At each madar, branch matns are aligned word by word. Branches with matching texts are clustered, and the strongest cluster is treated as the preserved text (المحفوظ). | See below. |
| `RafWaqfRule` | رفع ووقف | Branches from the same madar disagree on whether the text is attributed to the Prophet ﷺ. | Weaker side raises it → Qadihah. Weaker side stops it at the Companion → GhayrQadihah. Comparable → Tanbih. |
| `WaslIrsalRule` | وصل وإرسال | A Successor at the top of a marfu' chain. | If another tariq through him names a Companion, the two sides are weighed the same way as raf'/waqf. Otherwise → Tanbih "ظاهره الإرسال". |

### Weighing branches (الترجيح)

`IsnadBranching.Compare` weighs two sides of a disagreement:

1. A difference of two or more tiers in the reliability of the madar's students decides it.
2. Otherwise, the side with more distinct students wins.
3. Otherwise, the slightly more reliable side wins.
4. If none of these decides it, the sides are comparable.

Tiers come from `NarratorGradeScale` (T1 companion … T12 fabricator), which `TaqwiyahService` also uses.

### Matn comparison

- `MatnText.ExtractBody` removes the isnad. The matn starts at «قال/أن/عن… رسول الله / النبي»; for mawquf texts it starts after the last transmission formula. Compiler commentary is also cut, e.g. «قال أبو عيسى» or «وفي الباب».
- `MatnText.NormalizeForComparison` removes diacritics, honorifics and punctuation, and unifies letter forms.
- `MatnAligner` aligns the two word sequences with an LCS.
- Differences are classified as follows:
  - Similarity below 0.4: treated as a different hadith and ignored.
  - At least 2 substituted words (and similarity below 0.9): contradiction.
  - At least 3 added words, including a run of 2 or more: addition.
  - Anything smaller: narration by meaning (الرواية بالمعنى), ignored.
- An addition is classified by who adds it:
  - A weak narrator → نكارة.
  - A narrator weaker than those who omit it → يُخشى شذوذها (Tanbih).
  - A thiqa or saduq narrator → زيادة ثقة (GhayrQadihah).
- A contradiction is classified by the strength of the two sides:
  - The contradicting side is weaker → شذوذ, or نكارة if its narrator is weak (Qadihah).
  - The sides are comparable → اضطراب (Qadihah).

## Data

- **Teacher/student relations** come from the teacher and student lists of Tahdhib al-Kamal (and the compilers' shaykh books), linked by `link_tahdhib.py` and loaded from `registry.json` (115,816 relations). Late narrators (the shaykhs of al-Tabarani, al-Bayhaqi and al-Hakim) have no lists, so the rule cannot check them.
- **Mudallisin** (158): Ibn Hajar's طبقات المدلسين with its five tiers. **Mukhtalitun** (131): الكواكب النيرات and المختلطين للعلائي, with the students who heard before and after the change. Both are read in `scripts/shamela4-extractor/rijal_pilot/` (`link_ilal.py`, `ilal_data/`) and written to `ilal.json`.
- **Name matching.** The decisions made by hand are kept in `ilal_overrides.json` (git-tracked). To add a narrator, add the entry there and re-run the pipeline (`docs/shamela_migration.md`).

Load it with:

```bash
dotnet ef database update -p src/SmartHadithTree.Infrastructure -s src/SmartHadithTree.Api
dotnet run --project src/SmartHadithTree.Etl -- seed-ilal-shamela data/shamela_rijal
```

`seed-ilal-shamela` loads `ilal.json`. Like the rest of the ingestion it only inserts, so run it on an empty database (`docs/data_ingestion.md`).

## Known limitations

- Chains come from the Python resolver. If it drops or misidentifies a narrator, the result can be a false انقطاع خفي or a false ظاهره الإرسال. Those findings are always Tanbih with low confidence for this reason.
- Multi-chain isnads (tahwil) are read as separate chains, up to 8 per record; some written forms are still missed or joined wrongly (`docs/backlog.md`, section 1).
- Matn comparison uses the stored text, which sometimes still carries a compiler's comment or an editor's note; these produce false matn findings (`docs/backlog.md`, section 4). A shorter, abridged text is also taken as an omission, not as an abridgement.
- `/api/ilal/{id}` gathers turuq through a substring match on the matn, so it can miss turuq that use different wording.
- Detecting raf' depends on the text naming the Prophet ﷺ. Marfu' hukman (e.g. «من السنة») counts as mawquf.
- The seed lists cover high-frequency narrators only. Extend them as needed.

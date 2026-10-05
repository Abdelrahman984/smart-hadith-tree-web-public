using Microsoft.Extensions.Logging;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Etl.Parsers;

/// <summary>
/// Generates realistic sample Hadith data for development and testing.
/// Produces narrators with proper Isnad chains, scholar evaluations, and Arabic text.
/// Implements <see cref="IDataSourceParser"/> so it plugs into the same pipeline.
/// </summary>
public class SeedDataGenerator(ILogger<SeedDataGenerator> logger) : IDataSourceParser
{
    public string Name => "Seed Data Generator";

    /// <summary>Matches the special keyword "seed" as the source path.</summary>
    public bool CanParse(string sourcePath) =>
        sourcePath.Equals("seed", StringComparison.OrdinalIgnoreCase);

    public Task<ParsedDataset> ParseAsync(string sourcePath, CancellationToken ct = default)
    {
        logger.LogInformation("Generating seed data for development...");

        var dataset = new ParsedDataset();

        // ── 1. Create Narrators ────────────────────────────────────
        var narrators = CreateNarrators();
        dataset.Narrators.AddRange(narrators);

        // Build a quick lookup by KnownAs
        var byAlias = narrators.ToDictionary(n => n.KnownAs ?? n.FullName);

        // ── 2. Create Hadiths & Transmission Chains ────────────────
        var (hadiths, transmissions) = CreateHadithsWithChains(byAlias);
        dataset.Hadiths.AddRange(hadiths);
        dataset.Transmissions.AddRange(transmissions);

        // ── 3. Create Scholar Evaluations ──────────────────────────
        var evaluations = CreateEvaluations(byAlias);
        dataset.ScholarEvaluations.AddRange(evaluations);

        logger.LogInformation(
            "Seed data generated: {Narrators} narrators, {Hadiths} hadiths, " +
            "{Transmissions} transmissions, {Evaluations} evaluations.",
            dataset.Narrators.Count, dataset.Hadiths.Count,
            dataset.Transmissions.Count, dataset.ScholarEvaluations.Count);

        return Task.FromResult(dataset);
    }

    private static List<Narrator> CreateNarrators()
    {
        return
        [
            new()
            {
                Id = Guid.CreateVersion7(),
                FullName = "عمر بن الخطاب بن نفيل القرشي العدوي",
                KnownAs = "عمر بن الخطاب",
                Kunyah = "أبو حفص",
                GenerationTier = "الطبقة الأولى - الصحابة",
                DeathYearHijri = 23,
                DeathPlace = "المدينة المنورة",
                Biography = "أمير المؤمنين، ثاني الخلفاء الراشدين"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                FullName = "عبد الرحمن بن صخر الدوسي",
                KnownAs = "أبو هريرة",
                Kunyah = "أبو هريرة",
                GenerationTier = "الطبقة الأولى - الصحابة",
                DeathYearHijri = 57,
                DeathPlace = "المدينة المنورة",
                Biography = "أكثر الصحابة رواية للحديث عن النبي ﷺ"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                FullName = "عبد الله بن عمر بن الخطاب القرشي العدوي",
                KnownAs = "عبد الله بن عمر",
                Kunyah = "أبو عبد الرحمن",
                GenerationTier = "الطبقة الأولى - الصحابة",
                DeathYearHijri = 73,
                DeathPlace = "مكة المكرمة",
                Biography = "من أكثر الصحابة فتوى وعلمًا"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                FullName = "محمد بن مسلم بن عبيد الله بن شهاب الزهري",
                KnownAs = "ابن شهاب الزهري",
                Kunyah = "أبو بكر",
                GenerationTier = "الطبقة الرابعة - كبار تابعي التابعين",
                BirthYearHijri = 51,
                DeathYearHijri = 124,
                DeathPlace = "الشام",
                Biography = "إمام الحديث في زمانه، أول من دوّن الحديث بأمر عمر بن عبد العزيز"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                FullName = "سعيد بن المسيب بن حزن القرشي المخزومي",
                KnownAs = "سعيد بن المسيب",
                Kunyah = "أبو محمد",
                GenerationTier = "الطبقة الثانية - كبار التابعين",
                BirthYearHijri = 15,
                DeathYearHijri = 94,
                DeathPlace = "المدينة المنورة",
                Biography = "سيد التابعين، من أعلم الناس بأقضية عمر بن الخطاب"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                FullName = "نافع مولى عبد الله بن عمر",
                KnownAs = "نافع مولى ابن عمر",
                GenerationTier = "الطبقة الثالثة - من الوسطى من التابعين",
                DeathYearHijri = 117,
                DeathPlace = "المدينة المنورة",
                Biography = "ثقة ثبت، كان من أعلم الناس بحديث ابن عمر"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                FullName = "مالك بن أنس بن مالك بن أبي عامر الأصبحي",
                KnownAs = "مالك بن أنس",
                Kunyah = "أبو عبد الله",
                GenerationTier = "الطبقة السابعة - كبار أتباع التابعين",
                BirthYearHijri = 93,
                DeathYearHijri = 179,
                DeathPlace = "المدينة المنورة",
                Biography = "إمام دار الهجرة، صاحب الموطأ، أحد الأئمة الأربعة"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                FullName = "سفيان بن عيينة بن أبي عمران الهلالي",
                KnownAs = "سفيان بن عيينة",
                Kunyah = "أبو محمد",
                GenerationTier = "الطبقة الثامنة - الوسطى من أتباع التابعين",
                BirthYearHijri = 107,
                DeathYearHijri = 198,
                DeathPlace = "مكة المكرمة",
                Biography = "إمام حافظ حجة، من أعلم أهل الحجاز بالحديث"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                FullName = "عبد الله بن يوسف التنيسي",
                KnownAs = "عبد الله بن يوسف",
                Kunyah = "أبو محمد",
                GenerationTier = "الطبقة العاشرة - كبار الآخذين عن تبع الأتباع",
                DeathYearHijri = 218,
                Biography = "ثقة متقن، من أثبت الناس في الموطأ"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                FullName = "الحميدي عبد الله بن الزبير القرشي المكي",
                KnownAs = "الحميدي",
                Kunyah = "أبو بكر",
                GenerationTier = "الطبقة العاشرة - كبار الآخذين عن تبع الأتباع",
                DeathYearHijri = 219,
                DeathPlace = "مكة المكرمة",
                Biography = "ثقة حافظ فقيه، شيخ البخاري الأول في صحيحه"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                FullName = "محمد بن إسماعيل بن إبراهيم البخاري الجعفي",
                KnownAs = "الإمام البخاري",
                Kunyah = "أبو عبد الله",
                GenerationTier = "الطبقة الحادية عشرة - الوسطى من الآخذين عن تبع الأتباع",
                BirthYearHijri = 194,
                DeathYearHijri = 256,
                BirthPlace = "بخارى",
                DeathPlace = "خرتنك (قرب سمرقند)",
                Biography = "أمير المؤمنين في الحديث، صاحب أصح كتاب بعد كتاب الله"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                FullName = "يحيى بن سعيد بن قيس الأنصاري",
                KnownAs = "يحيى بن سعيد الأنصاري",
                Kunyah = "أبو سعيد",
                GenerationTier = "الطبقة الخامسة - صغار التابعين",
                DeathYearHijri = 143,
                DeathPlace = "المدينة المنورة",
                Biography = "ثقة ثبت، قاضي المدينة"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                FullName = "علقمة بن وقاص الليثي",
                KnownAs = "علقمة بن وقاص",
                GenerationTier = "الطبقة الثانية - كبار التابعين",
                DeathYearHijri = 83,
                DeathPlace = "المدينة المنورة",
                Biography = "تابعي ثقة قليل الحديث"
            },
            new()
            {
                Id = Guid.CreateVersion7(),
                FullName = "محمد بن إبراهيم بن الحارث التيمي",
                KnownAs = "محمد بن إبراهيم التيمي",
                GenerationTier = "الطبقة الرابعة - كبار تابعي التابعين",
                DeathYearHijri = 120,
                DeathPlace = "المدينة المنورة",
                Biography = "ثقة، له أفراد"
            }
        ];
    }

    private static (List<HadithText>, List<Transmission>) CreateHadithsWithChains(
        Dictionary<string, Narrator> n)
    {
        var hadiths = new List<HadithText>();
        var transmissions = new List<Transmission>();

        // ── Hadith 1: إنما الأعمال بالنيات (البخاري #1) ────────────
        // Chain: البخاري ← الحميدي ← سفيان ← يحيى الأنصاري ← محمد بن إبراهيم ← علقمة ← عمر بن الخطاب
        var h1 = new HadithText
        {
            Id = Guid.CreateVersion7(),
            MatnArabic = "إِنَّمَا الأَعْمَالُ بِالنِّيَّاتِ، وَإِنَّمَا لِكُلِّ امْرِئٍ مَا نَوَى، فَمَنْ كَانَتْ هِجْرَتُهُ إِلَى دُنْيَا يُصِيبُهَا، أَوْ إِلَى امْرَأَةٍ يَنْكِحُهَا، فَهِجْرَتُهُ إِلَى مَا هَاجَرَ إِلَيْهِ",
            BookName = "صحيح البخاري",
            HadithNumber = 1,
            Volume = "1",
            Chapter = "بدء الوحي",
            FullIsnadText = "حَدَّثَنَا الحُمَيْدِيُّ عَبْدُ اللَّهِ بْنُ الزُّبَيْرِ، قَالَ: حَدَّثَنَا سُفْيَانُ، قَالَ: حَدَّثَنَا يَحْيَى بْنُ سَعِيدٍ الأَنْصَارِيُّ، قَالَ: أَخْبَرَنِي مُحَمَّدُ بْنُ إِبْرَاهِيمَ التَّيْمِيُّ، أَنَّهُ سَمِعَ عَلْقَمَةَ بْنَ وَقَّاصٍ اللَّيْثِيَّ، يَقُولُ: سَمِعْتُ عُمَرَ بْنَ الخَطَّابِ رَضِيَ اللَّهُ عَنْهُ عَلَى المِنْبَرِ"
        };
        hadiths.Add(h1);

        // StepOrder 1 = compiler (البخاري), going UP to the Companion
        Narrator[] chain1 =
        [
            n["الإمام البخاري"], n["الحميدي"], n["سفيان بن عيينة"],
            n["يحيى بن سعيد الأنصاري"], n["محمد بن إبراهيم التيمي"],
            n["علقمة بن وقاص"], n["عمر بن الخطاب"]
        ];
        string[] terms1 = ["حدثنا", "حدثنا", "حدثنا", "أخبرنا", "سمعت", "سمعت"];

        for (int i = 0; i < chain1.Length - 1; i++)
        {
            transmissions.Add(new Transmission
            {
                Id = Guid.CreateVersion7(),
                HadithId = h1.Id,
                StudentId = chain1[i].Id,     // The one listening
                SheikhId = chain1[i + 1].Id,  // The one narrating
                StepOrder = i + 1,
                TransmissionTerm = terms1[i]
            });
        }

        // ── Hadith 2: الطهور شطر الإيمان (مسلم-style) ──────────────
        // Chain: البخاري ← عبد الله بن يوسف ← مالك ← نافع ← ابن عمر
        // (Simplified Bukhari-via-Malik chain for a different Hadith)
        var h2 = new HadithText
        {
            Id = Guid.CreateVersion7(),
            MatnArabic = "بُنِيَ الإِسْلامُ عَلَى خَمْسٍ: شَهَادَةِ أَنْ لا إِلَهَ إِلا اللَّهُ وَأَنَّ مُحَمَّدًا رَسُولُ اللَّهِ، وَإِقَامِ الصَّلاةِ، وَإِيتَاءِ الزَّكَاةِ، وَالحَجِّ، وَصَوْمِ رَمَضَانَ",
            BookName = "صحيح البخاري",
            HadithNumber = 8,
            Volume = "1",
            Chapter = "الإيمان",
            FullIsnadText = "حَدَّثَنَا عَبْدُ اللَّهِ بْنُ يُوسُفَ، قَالَ: أَخْبَرَنَا مَالِكٌ، عَنْ نَافِعٍ، عَنْ عَبْدِ اللَّهِ بْنِ عُمَرَ رَضِيَ اللَّهُ عَنْهُمَا"
        };
        hadiths.Add(h2);

        Narrator[] chain2 =
        [
            n["الإمام البخاري"], n["عبد الله بن يوسف"],
            n["مالك بن أنس"], n["نافع مولى ابن عمر"], n["عبد الله بن عمر"]
        ];
        string[] terms2 = ["حدثنا", "أخبرنا", "عن", "عن"];

        for (int i = 0; i < chain2.Length - 1; i++)
        {
            transmissions.Add(new Transmission
            {
                Id = Guid.CreateVersion7(),
                HadithId = h2.Id,
                StudentId = chain2[i].Id,
                SheikhId = chain2[i + 1].Id,
                StepOrder = i + 1,
                TransmissionTerm = terms2[i]
            });
        }

        // ── Hadith 3: إنما الأعمال بالنيات — alternate chain via الزهري
        // Shows chain convergence: same Hadith, different path
        // البخاري ← عبد الله بن يوسف ← مالك ← الزهري ← سعيد بن المسيب ← أبو هريرة
        var h3 = new HadithText
        {
            Id = Guid.CreateVersion7(),
            MatnArabic = "إِنَّمَا الأَعْمَالُ بِالنِّيَّاتِ، وَإِنَّمَا لِكُلِّ امْرِئٍ مَا نَوَى...",
            NormalizedMatn = SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize("إِنَّمَا الأَعْمَالُ بِالنِّيَّاتِ، وَإِنَّمَا لِكُلِّ امْرِئٍ مَا نَوَى..."),
            BookName = "صحيح البخاري",
            NormalizedBookName = SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize("صحيح البخاري"),
            HadithNumber = 1,
            Chapter = "كيف كان بدء الوحي",
            FullIsnadText = "حَدَّثَنَا الْحُمَيْدِيُّ عَبْدُ اللَّهِ بْنُ الزُّبَيْرِ، قَالَ حَدَّثَنَا سُفْيَانُ...مَالِكٌ، عَنِ ابْنِ شِهَابٍ، عَنْ سَعِيدِ بْنِ المُسَيَّبِ، عَنْ أَبِي هُرَيْرَةَ"
        };
        hadiths.Add(h3);

        Narrator[] chain3 =
        [
            n["الإمام البخاري"], n["عبد الله بن يوسف"],
            n["مالك بن أنس"], n["ابن شهاب الزهري"],
            n["سعيد بن المسيب"], n["أبو هريرة"]
        ];
        string[] terms3 = ["حدثنا", "أخبرنا", "عن", "عن", "عن"];

        for (int i = 0; i < chain3.Length - 1; i++)
        {
            transmissions.Add(new Transmission
            {
                Id = Guid.CreateVersion7(),
                HadithId = h3.Id,
                StudentId = chain3[i].Id,
                SheikhId = chain3[i + 1].Id,
                StepOrder = i + 1,
                TransmissionTerm = terms3[i]
            });
        }

        return (hadiths, transmissions);
    }

    private static List<ScholarEvaluation> CreateEvaluations(Dictionary<string, Narrator> n)
    {
        return
        [
            // ── مالك بن أنس ────────────────────────────────────────
            new()
            {
                Id = Guid.CreateVersion7(), NarratorId = n["مالك بن أنس"].Id,
                ScholarName = "يحيى بن معين", VerdictRating = "ثقة",
                EvaluationText = "ثقة حجة",
                SourceBook = "تهذيب الكمال"
            },
            new()
            {
                Id = Guid.CreateVersion7(), NarratorId = n["مالك بن أنس"].Id,
                ScholarName = "أحمد بن حنبل", VerdictRating = "ثقة",
                EvaluationText = "مالك إمام في الحديث والفقه",
                SourceBook = "العلل ومعرفة الرجال"
            },
            new()
            {
                Id = Guid.CreateVersion7(), NarratorId = n["مالك بن أنس"].Id,
                ScholarName = "الشافعي", VerdictRating = "ثقة",
                EvaluationText = "إذا ذُكر العلماء فمالك النجم",
                SourceBook = "تهذيب التهذيب"
            },

            // ── سفيان بن عيينة ─────────────────────────────────────
            new()
            {
                Id = Guid.CreateVersion7(), NarratorId = n["سفيان بن عيينة"].Id,
                ScholarName = "يحيى بن معين", VerdictRating = "ثقة ثبت",
                EvaluationText = "ثقة ثبت، كان من أثبت الناس في حديث عمرو بن دينار والزهري",
                SourceBook = "تهذيب الكمال"
            },
            new()
            {
                Id = Guid.CreateVersion7(), NarratorId = n["سفيان بن عيينة"].Id,
                ScholarName = "أحمد بن حنبل", VerdictRating = "ثقة",
                EvaluationText = "ما رأيت أحدًا أعلم بالسنن منه، هو إمام في الحديث",
                SourceBook = "العلل ومعرفة الرجال"
            },
            new()
            {
                Id = Guid.CreateVersion7(), NarratorId = n["سفيان بن عيينة"].Id,
                ScholarName = "ابن حجر العسقلاني", VerdictRating = "ثقة حافظ",
                EvaluationText = "ثقة حافظ فقيه إمام حجة، إلا أنه تغير حفظه بأخرة وكان ربما دلّس لكن عن الثقات",
                SourceBook = "تقريب التهذيب"
            },

            // ── ابن شهاب الزهري ────────────────────────────────────
            new()
            {
                Id = Guid.CreateVersion7(), NarratorId = n["ابن شهاب الزهري"].Id,
                ScholarName = "ابن حجر العسقلاني", VerdictRating = "ثقة حافظ",
                EvaluationText = "الفقيه الحافظ متفق على جلالته وإتقانه",
                SourceBook = "تقريب التهذيب"
            },
            new()
            {
                Id = Guid.CreateVersion7(), NarratorId = n["ابن شهاب الزهري"].Id,
                ScholarName = "الليث بن سعد", VerdictRating = "ثقة",
                EvaluationText = "ما رأيت عالمًا قط أجمع من ابن شهاب",
                SourceBook = "سير أعلام النبلاء"
            },

            // ── نافع مولى ابن عمر ──────────────────────────────────
            new()
            {
                Id = Guid.CreateVersion7(), NarratorId = n["نافع مولى ابن عمر"].Id,
                ScholarName = "ابن حجر العسقلاني", VerdictRating = "ثقة ثبت",
                EvaluationText = "ثقة ثبت فقيه مشهور",
                SourceBook = "تقريب التهذيب"
            },
            new()
            {
                Id = Guid.CreateVersion7(), NarratorId = n["نافع مولى ابن عمر"].Id,
                ScholarName = "الإمام البخاري", VerdictRating = "ثقة",
                EvaluationText = "أصح الأسانيد: مالك عن نافع عن ابن عمر",
                SourceBook = "التاريخ الكبير"
            },

            // ── أبو هريرة ──────────────────────────────────────────
            new()
            {
                Id = Guid.CreateVersion7(), NarratorId = n["أبو هريرة"].Id,
                ScholarName = "ابن حجر العسقلاني", VerdictRating = "صحابي",
                EvaluationText = "صحابي جليل، أكثر الصحابة حديثًا، حفظ عن النبي ﷺ ما لم يحفظه غيره",
                SourceBook = "الإصابة في تمييز الصحابة"
            },
            new()
            {
                Id = Guid.CreateVersion7(), NarratorId = n["أبو هريرة"].Id,
                ScholarName = "الذهبي", VerdictRating = "صحابي",
                EvaluationText = "الإمام الفقيه المجتهد الحافظ صاحب رسول الله ﷺ، أكثر الصحابة حفظًا للحديث وأداءً له",
                SourceBook = "سير أعلام النبلاء"
            },

            // ── سعيد بن المسيب ──────────────────────────────────────
            new()
            {
                Id = Guid.CreateVersion7(), NarratorId = n["سعيد بن المسيب"].Id,
                ScholarName = "ابن حجر العسقلاني", VerdictRating = "ثقة",
                EvaluationText = "من كبار التابعين وسيدهم، ثقة حجة فقيه",
                SourceBook = "تقريب التهذيب"
            },
            new()
            {
                Id = Guid.CreateVersion7(), NarratorId = n["سعيد بن المسيب"].Id,
                ScholarName = "أحمد بن حنبل", VerdictRating = "ثقة",
                EvaluationText = "أفضل التابعين سعيد بن المسيب",
                SourceBook = "العلل ومعرفة الرجال"
            }
        ];
    }
}

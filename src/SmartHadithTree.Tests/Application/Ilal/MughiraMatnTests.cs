using FluentAssertions;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Application.Services.Ilal.Rules;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Tests.Application.Ilal;

/// <summary>
/// The narrations of al-Mughira b. Shu'ba «كان إذا ذهب المذهب أبعد» as they appear in the books:
/// the same hadith worded differently, with one real addition (Tabarani 1063).
/// </summary>
public class MughiraMatnTests
{
    private const string IbnMajah =
        "حَدَّثَنَا أَبُو بَكْرِ بْنُ أَبِي شَيْبَةَ، حَدَّثَنَا إِسْمَاعِيلُ ابْنُ عُلَيَّةَ، عَنْ مُحَمَّدِ بْنِ عَمْرٍو، عَنْ أَبِي سَلَمَةَ\nعَنْ الْمُغِيرَةِ بْنِ شُعْبَةَ، قَالَ: كَانَ النَّبِيُّ ﷺ إِذَا ذَهَبَ الْمَذْهَبَ أَبْعَدَ.";

    private const string AbuDawud =
        "حَدَّثَنَا عَبْدُ اللهِ بْنُ مَسْلَمَةَ بْنِ قَعْنَبٍ الْقَعْنَبِيُّ ، ثَنَا عَبْدُ الْعَزِيزِ يَعْنِي ابْنَ مُحَمَّدٍ ، عَنْ مُحَمَّدٍ يَعْنِي ابْنَ عَمْرٍو ، عَنْ أَبِي سَلَمَةَ ، عَنِ الْمُغِيرَةِ بْنِ شُعْبَةَ: أَنَّ النَّبِيَّ ﷺ «كَانَ إِذَا ذَهَبَ الْمَذْهَبَ أَبْعَدَ».";

    private const string Bayhaqi =
        "أخبرَنا أبو عبدِ اللَّهِ الحافظُ، حدثنا أبو العباسِ محمدُ بنُ يَعقوبَ، حدثنا العَبّاسُ بنُ محمدٍ، حدثنا يَزيدُ بنُ هارونَ، أخبرَنِي محمدُ بنُ عمرٍو، عن أبي سلَمةَ، عن المُغيرَةِ بنِ شعبَةَ قال: كنت مَعَ رسولِ اللَّهِ ﷺ في بَعضِ أسفارِه، وكانَ إذا ذَهَبَ أبعَدَ في المَذهَبِ.";

    private const string Tabarani1065 =
        "حَدَّثَنَا عُبَيْدُ بْنُ غَنَّامٍ، ثَنَا أَبُو بَكْرِ بْنُ أَبِي شَيْبَةَ، ثَنَا إِسْمَاعِيلُ بْنُ عُلَيَّةَ، عَنْ مُحَمَّدِ بْنِ عَمْرٍو، عَنْ أَبِي سَلَمَةَ، عَنِ الْمُغِيرَةِ بْنِ شُعْبَةَ، قَالَ: «كُنْتُ مَعَ رَسُولِ اللهِ ﷺ فِي بَعْضِ أَسْفَارِهِ فَكَانَ رَسُولُ اللهِ ﷺ إِذَا ذَهَبَ أَبْعَدَ الْمَذْهَبَ»";

    private const string Tabarani1063 =
        "حَدَّثَنَا أَبُو يَزِيدَ الْقَرَاطِيسِيُّ، ثَنَا حَجَّاجُ بْنُ إِبْرَاهِيمَ الْأَزْرَقُ، ثَنَا إِسْمَاعِيلُ بْنُ جَعْفَرٍ، عَنْ مُحَمَّدِ بْنِ عَمْرٍو، عَنْ أَبِي سَلَمَةَ، عَنِ الْمُغِيرَةِ بْنِ شُعْبَةَ، قَالَ: كَانَ رَسُولُ اللهِ ﷺ إِذَا ذَهَبَ الْمَذْهَبَ أَبْعَدَ , فَذَهَبَ لِحَاجَةٍ وَهُوَ فِي بَعْضِ أَسْفَارِهِ فَقَالَ: «ائْتِنِي بِوَضُوءٍ» , فَجِئْتُهُ بِوَضُوءٍ , فَأَخْرَجَ يَدَيْهِ مِنْ تَحْتِ الْجُبَّةِ فَتَوَضَّأَ وَمَسَحَ عَلَى الْخُفَّيْنِ ";

    [Fact]
    public void ExtractBody_KeepsTheNarrativePrefaceTogetherWithTheRestOfTheMatn()
    {
        MatnText.ExtractBody(Bayhaqi).Should().StartWith("كنت مع رسول الله");
        MatnText.ExtractBody(Tabarani1065).Should().StartWith("كنت مع رسول الله");
        MatnText.ExtractBody(IbnMajah).Should().StartWith("كان النبي");
    }

    [Fact]
    public void Honorifics_WithAlAndTheLongFormAreStripped()
    {
        MatnText.NormalizeForComparison("قال رسول الله صلى الله عليه وآله وسلم كذا").Should().Be("قال رسول الله كذا");
        MatnText.NormalizeForComparison("قال النبي عليه الصلاة والسلام كذا").Should().Be("قال النبي كذا");
    }

    [Theory]
    [InlineData(IbnMajah, Bayhaqi)]
    [InlineData(IbnMajah, AbuDawud)]
    [InlineData(IbnMajah, Tabarani1065)]
    [InlineData(Bayhaqi, Tabarani1065)]
    public void PrefaceAndWordingDifferences_AreNotAdditions(string reference, string compared)
    {
        var diff = MatnAligner.Align(MatnText.ExtractBody(reference), MatnText.ExtractBody(compared));

        MatnAtMadarRule.Classify(diff).Should().Be(MatnAtMadarRule.DiffKind.None);
    }

    [Fact]
    public void TheWuduClause_IsARealAddition()
    {
        var diff = MatnAligner.Align(MatnText.ExtractBody(IbnMajah), MatnText.ExtractBody(Tabarani1063));

        MatnAtMadarRule.Classify(diff).Should().Be(MatnAtMadarRule.DiffKind.Addition);
    }

    [Fact]
    public void FindVariants_FlagsOnlyTheNarrationWithTheAddition_RegardlessOfOrder()
    {
        string[] texts = [Tabarani1063, IbnMajah, AbuDawud, Bayhaqi, Tabarani1065];
        var bodies = texts.Select(t => MatnText.Tokenize(MatnText.ExtractBody(t))).ToList();

        MatnVariation.FindVariants(bodies).Should().BeEquivalentTo(new[] { 0 });

        bodies.Reverse();
        MatnVariation.FindVariants(bodies).Should().BeEquivalentTo(new[] { 4 });
    }

    [Fact]
    public void FindMedoid_PicksTheMostCentralText()
    {
        string[] texts = [Tabarani1063, IbnMajah, AbuDawud];
        var bodies = texts.Select(t => MatnText.Tokenize(MatnText.ExtractBody(t))).ToList();

        MatnVariation.FindMedoid(bodies).Should().NotBe(0);
    }
}

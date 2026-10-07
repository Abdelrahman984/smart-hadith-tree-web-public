using FluentAssertions;
using SmartHadithTree.Application.Services;

namespace SmartHadithTree.Tests.Application;

public class NarratorMentionedNameTests
{
    private const string Sufyan = "سفيان بن عيينة بن أبي عمران الهلالي";

    [Fact]
    public void Choose_PrefersTheFormTheIsnadsUseMost()
    {
        var texts = new[]
        {
            "حدثنا الحميدي قال حدثنا سفيان قال حدثنا الزهري",
            "حدثنا قتيبة حدثنا سفيان عن الزهري",
            "حدثنا مسدد حدثنا ابن عيينة عن الزهري",
        };

        NarratorMentionedName.Choose(Sufyan, null, texts).Should().Be("سفيان");
    }

    [Fact]
    public void Choose_BareFirstNameFollowedByPatronymic_CountsForTheLongerForm()
    {
        var texts = new[] { "حدثنا محمد بن يحيى قال حدثنا محمد بن يحيى", "حدثنا محمد بن بشار" };

        NarratorMentionedName.Choose("محمد بن يحيى بن عبد الله الذهلي", null, texts).Should().Be("محمد بن يحيى");
    }

    [Fact]
    public void Choose_UsesKnownAsAndIgnoresDiacriticsAndCaseEndings()
    {
        var texts = new[] { "عَنِ الزُّهْرِيِّ، عَنْ أَبِي هُرَيْرَةَ" };

        NarratorMentionedName.Choose("محمد بن مسلم بن عبيد الله القرشي", "الزهري", texts).Should().Be("الزهري");
        NarratorMentionedName.Choose("عبد الرحمن بن صخر الدوسي", "أبو هريرة", texts).Should().Be("أبو هريرة");
    }

    [Fact]
    public void Choose_NobodyNamesHim_ReturnsNull()
    {
        NarratorMentionedName.Choose(Sufyan, null, ["حدثنا مالك عن نافع"]).Should().BeNull();
        NarratorMentionedName.Choose(Sufyan, null, [null, " "]).Should().BeNull();
    }

    [Fact]
    public void Choose_UsesKunyah_WhenFullNameDoesNotCarryIt()
    {
        NarratorMentionedName.Choose("عبد الرحمن بن صخر الدوسي", null, ["عن أبي هريرة قال"], "أبو هريرة")
            .Should().Be("أبو هريرة");
    }

    [Fact]
    public void Choose_TreatsSpacedAndJoinedAbdAsTheSameName()
    {
        NarratorMentionedName.Choose("عبد الله بن عمر بن الخطاب", null, ["عن عبدالله بن عمر عن النبي"])
            .Should().Be("عبد الله بن عمر");
    }

    [Fact]
    public void Choose_KunyahFollowedByPatronymic_IsNotAMentionOfTheBareKunyah()
    {
        // "أبو بكر بن أبي شيبة" is someone else's name; it must not make al-Bayhaqi "أبو بكر".
        NarratorMentionedName.Choose("أحمد بن الحسين بن علي البيهقي", null, ["حدثنا أبو بكر بن أبي شيبة"], "أبو بكر")
            .Should().BeNull();
    }

    [Fact]
    public void Choose_FormsSharedWithAnotherNarratorOfTheTree_AreLeftOut()
    {
        var texts = new[] { "أخبرنا أبو عبد الله الحافظ قال أبو عبد الله الثاني حدثنا محمد بن يعقوب" };
        var shared = new HashSet<string> { "ابو عبدالله" };

        NarratorMentionedName.Choose("محمد بن يعقوب بن يوسف", null, texts, "أبو عبد الله", shared)
            .Should().Be("محمد بن يعقوب");
        NarratorMentionedName.Choose("محمد بن يعقوب بن يوسف", null, texts, "أبو عبد الله")
            .Should().Be("أبو عبد الله");
    }

    [Fact]
    public void FormKeys_SameFirstNameOfTwoNarrators_Collides()
    {
        var a = NarratorMentionedName.FormKeys("سفيان بن عيينة بن أبي عمران", null);
        var b = NarratorMentionedName.FormKeys("سفيان بن سعيد بن مسروق الثوري", null);

        a.Intersect(b).Should().Contain("سفيان");
    }

    [Fact]
    public void Choose_IgnoresTheArticleAndAJoinedWaw()
    {
        NarratorMentionedName.Choose("ليث بن سعد بن عبد الرحمن الفهمي", null, ["أخبرنا الليث بن سعد"])
            .Should().Be("ليث بن سعد");
        NarratorMentionedName.Choose("سفيان بن عيينة", null, ["حدثنا الحميدي وسفيان قال"], null)
            .Should().Be("سفيان");
    }

    [Fact]
    public void Counts_LetsTheSharedFormGoToTheNarratorWhoseIsnadsUseItMost()
    {
        var texts = new[] { "حدثنا يحيى بن سعيد عن سفيان", "حدثنا يحيى بن سعيد" };
        var ansari = NarratorMentionedName.Counts("يحيى بن سعيد بن قيس الأنصاري", null, texts);
        var qattan = NarratorMentionedName.Counts("يحيى بن سعيد بن فروخ القطان", null, texts.Take(0));

        ansari["يحيي بن سعيد"].Should().Be(2);
        qattan["يحيي بن سعيد"].Should().Be(0);
    }

    [Fact]
    public void Choose_NameInsideAKunyahOrANasab_IsNotAMention()
    {
        // "أبو عبد الله" and "محمد بن عبد الله" must not make al-Humaydi "عبد الله".
        var texts = new[] { "أخبرنا أبو عبد الله الحافظ حدثنا محمد بن عبد الله", "حدثنا الحميدي" };

        NarratorMentionedName.Choose("عبد الله بن الزبير بن عيسى", null, texts).Should().BeNull();
    }

    [Fact]
    public void Choose_AddsTheNisbahWhenAnIsnadWritesItAfterTheName()
    {
        var texts = new[] { "عَنْ عَلْقَمَةَ بْنِ وَقَّاصٍ", "عَنْ عَلْقَمَةَ بْنِ وَقَّاصٍ اللَّيْثِيِّ" };

        NarratorMentionedName.Choose("علقمة بن وقاص بن محصن الليثي العتواري المدني", null, texts)
            .Should().Be("علقمة بن وقاص الليثي");
    }

    [Fact]
    public void Choose_TakesTheNisbahFromAfterTheYaqulaAnnotation()
    {
        var name = "يحيى بن سعيد بن قيس بن عمرو ويقال: يحيى بن سعيد بن قيس بن قهد الأنصاري";

        NarratorMentionedName.Choose(name, null, ["حدثنا يحيى بن سعيد الأنصاري", "عن يحيى بن سعيد"])
            .Should().Be("يحيى بن سعيد الأنصاري");
        NarratorMentionedName.Choose(name, null, ["عن يحيى بن سعيد"]).Should().Be("يحيى بن سعيد");
    }

    [Fact]
    public void Choose_NisbahOnlyForm_IsLeftAlone()
    {
        NarratorMentionedName.Choose(
                "سفيان بن سعيد بن مسروق الثوري", null, ["أخبرنا سفيان هو الثوري"], null, new HashSet<string> { "سفيان" })
            .Should().Be("الثوري");
    }
}

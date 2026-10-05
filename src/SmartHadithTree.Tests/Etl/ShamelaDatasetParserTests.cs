using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SmartHadithTree.Etl.Parsers.Shamela;
using Xunit;

namespace SmartHadithTree.Tests.Etl;

/// <summary>The parser end to end on a tiny dataset laid out like data/shamela_rijal and data/shamela.</summary>
public sealed class ShamelaDatasetParserTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "shamela-" + Guid.NewGuid());
    private string Rijal => Path.Combine(_root, "shamela_rijal");
    private string Texts => Path.Combine(_root, "shamela");

    private const string IsnadText = "حدثنا أبو أحمد عن مجهول عن بكر. قال النبي: إنما الأعمال بالنيات. هذا حديث صحيح";

    public ShamelaDatasetParserTests()
    {
        Write("shamela_rijal/registry.json", """
            [
              {"id":"tk1","name":"البخاري","header":"محمد بن إسماعيل، أبو عبد الله البخاري.","source":"tahdhib","rank":3,"verdict":"ثقة","tabaqa":"الحادية عشرة",
               "shuyukh":["tk2"],"talamidh":[],"quotes":[{"critic":"ابن حجر","via":null,"text":"ثقة حافظ"}]},
              {"id":"tk2","name":"أبو أحمد","header":"أبو أحمد.","source":"tahdhib","rank":4,"verdict":"صدوق","tabaqa":null,"shuyukh":[],"talamidh":["tk1"],"quotes":[]},
              {"id":"tk3","name":"بكر","header":"بكر.","source":"tahdhib","rank":null,"verdict":null,"tabaqa":null,"shuyukh":[],"talamidh":[],"quotes":[]}
            ]
            """);
        Write("shamela_rijal/chains/bukhari.json", """
            {"book":"bukhari","compiler":"tk1","records":2,"chains":[
              {"id":1,"number":1,"names":[{"n":"أبو أحمد","id":"tk2","how":"unique","verbs":["عن"]},
                                          {"n":"مجهول","id":null,"how":"missing","verbs":["عن"]},
                                          {"n":"بكر","id":"tk3","how":"unique","verbs":[]}]},
              {"id":3,"number":3,"names":[{"n":"بكر","id":"tk3","how":"unique","verbs":[]}]}]}
            """);
        Write("shamela_rijal/chains/_empty.json", "[]");
        Write("shamela_rijal/review/links/s1_registry_map.json", """
            {"7":{"registry":"tk2","confidence":"exact"},"8":{"registry":"tk3","confidence":"probable"}}
            """);
        Write("shamela/narrators.json", """
            [{"id":7,"death":150,"fields":{"الكنية":"أبو أحمد الزبيري","بلد الإقامة":"الكوفة"},
              "quotes":[{"section":"garh","critic":"ابن حبان","text":"ذكره في الثقات","book":"الثقات","vol":3,"page":120},
                        {"section":"garh","critic":"ابن معين","text":"ثقة","book":"تهذيب الكمال","vol":1,"page":2}]},
             {"id":8,"death":99,"fields":{},"quotes":[{"section":"garh","critic":"x","text":"y","book":"b","vol":1,"page":1}]}]
            """);
        Write("shamela/bukhari/book.json", """{"slug":"bukhari","title":"الجامع الصحيح","shamela_id":1681}""");
        Write("shamela/bukhari/index.json", """[{"chapter":1,"file":"1.json","name_ar":"كتاب بدء الوحي","count":3}]""");
        var text = IsnadText;
        var isnadEnd = text.IndexOf('.');
        var matnStart = text.IndexOf("قال النبي", StringComparison.Ordinal);
        Write("shamela/bukhari/1.json", $$"""
            [
              {"id":1,"number":1,"kind":"hadith","bab":"باب","vol":"1","arabic":"{{text}}",
               "parts":[{"type":"isnad","start":0,"end":{{isnadEnd}}},{"type":"matn","start":{{matnStart}},"end":{{text.IndexOf("هذا حديث", StringComparison.Ordinal)}}}]},
              {"id":2,"number":null,"kind":"text","bab":null,"vol":"1","arabic":"مقدمة المؤلف","parts":[{"type":"text","start":0,"end":12}]},
              {"id":3,"number":3,"kind":"hadith","bab":"باب","vol":"1","arabic":"حدثنا بكر","parts":[]},
              {"id":4,"number":4,"kind":"hadith","bab":"باب آخر","vol":"1","arabic":"حدثنا آخر","parts":[]}
            ]
            """);
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void CanParse_OnlyAFolderWithARegistry()
    {
        var parser = new ShamelaDatasetParser();
        parser.CanParse(Rijal).Should().BeTrue();
        parser.CanParse(Texts).Should().BeFalse();
        parser.CanParse(Path.Combine(_root, "missing")).Should().BeFalse();
    }

    [Fact]
    public async Task ParseAsync_BuildsNarratorsQuotesRelationsHadithsAndLinks()
    {
        var data = await new ShamelaDatasetParser().ParseAsync(Rijal);

        // Narrators: registry fields, and Shamela's encyclopedia only for the exact mapping (tk2, not the probable tk3).
        var byKey = data.Narrators.ToDictionary(n => n.SourceKey!);
        byKey.Keys.Should().BeEquivalentTo("tk1", "tk2", "tk3");
        byKey["tk2"].Should().Match<SmartHadithTree.Domain.Entities.Narrator>(
            n => n.ShamelaManId == 7 && n.DeathYearHijri == 150 && n.Kunyah == "أبو أحمد الزبيري" && n.ResidencePlaces == "الكوفة" && n.IbnHajarRank == 4);
        byKey["tk3"].Should().Match<SmartHadithTree.Domain.Entities.Narrator>(n => n.ShamelaManId == null && n.DeathYearHijri == null);
        byKey["tk1"].Kunyah.Should().Be("أبو عبد الله");

        // Quotes: the registry's, plus the encyclopedia's without its Tahdhib al-Kamal one.
        data.ScholarEvaluations.Select(e => e.EvaluationText).Should().BeEquivalentTo("ثقة حافظ", "ذكره في الثقات");
        data.ScholarEvaluations.Single(e => e.SourceBook == "الثقات").Should().Match<SmartHadithTree.Domain.Entities.ScholarEvaluation>(
            e => e.SourceVolume == "3" && e.SourcePage == 120 && e.NarratorId == byKey["tk2"].Id);

        // Relations: tk1 lists tk2 as a teacher, tk2 lists tk1 as a student: one relation.
        data.NarratorRelations.Should().ContainSingle().Which.Should().Match<SmartHadithTree.Domain.Entities.NarratorRelation>(
            r => r.TeacherId == byKey["tk2"].Id && r.StudentId == byKey["tk1"].Id && r.Source == "shamela");

        // Hadiths: the app's book name, the kitab from index.json (the bab where a file has no kitab), prose skipped.
        data.Hadiths.Should().HaveCount(3).And.OnlyContain(h => h.BookName == "صحيح البخاري");
        data.Hadiths.Select(h => h.HadithNumber).Should().Equal(1, 3, 4);
        data.Hadiths.Should().OnlyContain(h => h.Chapter == "كتاب بدء الوحي");
        var first = data.Hadiths[0];
        first.MatnArabic.Should().Be(IsnadText);
        first.FullIsnadText.Should().Be("حدثنا أبو أحمد عن مجهول عن بكر");
        data.Hadiths[1].FullIsnadText.Should().BeNull();

        // Transmissions: compiler ← first narrator; nothing across the undecided name (tk2 ← ? ← tk3 gives no tk2 → tk3);
        // record 4 has no chain at all.
        data.Transmissions.Select(t => (t.HadithId, t.StudentId, t.SheikhId, t.StepOrder, t.TransmissionTerm)).Should().BeEquivalentTo(
        [
            (first.Id, byKey["tk1"].Id, byKey["tk2"].Id, 1, (string?)null),
            (data.Hadiths[1].Id, byKey["tk1"].Id, byKey["tk3"].Id, 1, (string?)null)
        ]);
    }

    [Fact]
    public async Task ParseAsync_WithoutTheEncyclopedia_StillLoadsEverythingElse()
    {
        File.Delete(Path.Combine(Rijal, "review", "links", "s1_registry_map.json"));

        var data = await new ShamelaDatasetParser().ParseAsync(Rijal);

        data.Narrators.Should().HaveCount(3).And.OnlyContain(n => n.ShamelaManId == null && n.DeathYearHijri == null);
        data.Hadiths.Should().HaveCount(3);
        data.ScholarEvaluations.Should().ContainSingle();     // only the registry's quote
    }
}

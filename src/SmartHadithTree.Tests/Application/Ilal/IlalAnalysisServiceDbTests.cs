using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.Services.Ilal;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Domain.Enums;
using SmartHadithTree.Infrastructure.Data;

namespace SmartHadithTree.Tests.Application.Ilal;

public class IlalAnalysisServiceDbTests
{
    [Fact]
    public async Task AnalyzeAsync_LoadsChainsAndNarratorFlagsFromDatabase()
    {
        var options = new DbContextOptionsBuilder<HadithTreeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new HadithTreeDbContext(options);

        Narrator N(string name, int? mudallisTier = null) => new()
        {
            Id = Guid.NewGuid(), FullName = name, ItqanGrade = "reliable",
            IsMudallis = mudallisTier.HasValue, MudallisTier = mudallisTier
        };
        var compiler = N("أبو داود");
        var shuba = N("شعبة");
        var qatada = N("قتادة", mudallisTier: 3);
        var anas = N("أنس");
        db.Narrators.AddRange(compiler, shuba, qatada, anas);

        var hadith = new HadithText
        {
            Id = Guid.NewGuid(), BookName = "سنن أبي داود", HadithNumber = 1,
            MatnArabic = "حدثنا شعبة عن قتادة عن أنس قال قال رسول الله ﷺ ...",
            NormalizedMatn = "", NormalizedBookName = ""
        };
        db.Hadiths.Add(hadith);
        db.Transmissions.AddRange(
            new Transmission { Id = Guid.NewGuid(), HadithId = hadith.Id, StepOrder = 1, StudentId = compiler.Id, SheikhId = shuba.Id, TransmissionTerm = "حدثنا" },
            new Transmission { Id = Guid.NewGuid(), HadithId = hadith.Id, StepOrder = 2, StudentId = shuba.Id, SheikhId = qatada.Id, TransmissionTerm = "عن" },
            new Transmission { Id = Guid.NewGuid(), HadithId = hadith.Id, StepOrder = 3, StudentId = qatada.Id, SheikhId = anas.Id, TransmissionTerm = "عن" });
        await db.SaveChangesAsync();

        var report = await new IlalAnalysisService(db).AnalyzeAsync([hadith.Id]);

        report.AnalyzedHadithIds.Should().Equal(hadith.Id);
        report.Findings.Should().ContainSingle(f => f.Type == IllahType.Tadlis && f.Severity == IllahSeverity.Qadihah);
        report.HasQadihah.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_ReadsEveryChainOfATahwilIsnad()
    {
        // Mustadrak 493: two heads that meet at إسماعيل, who is at step 3 on one chain and step 4 on the other.
        var options = new DbContextOptionsBuilder<HadithTreeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new HadithTreeDbContext(options);

        Narrator N(string name) => new() { Id = Guid.NewGuid(), FullName = name, ItqanGrade = "reliable" };
        var (hakim, qays, qutayba, sayduni, ayyub, musa, ismail, amr) =
            (N("الحاكم"), N("قيس"), N("قتيبة"), N("الصيدلاني"), N("ابن أيوب"), N("ابن موسى"), N("إسماعيل"), N("محمد بن عمرو"));
        db.Narrators.AddRange(hakim, qays, qutayba, sayduni, ayyub, musa, ismail, amr);

        var hadith = new HadithText
        {
            Id = Guid.NewGuid(), BookName = "المستدرك", HadithNumber = 493,
            MatnArabic = "حدثنا قيس حدثنا قتيبة وأخبرني الصيدلاني قالا حدثنا إسماعيل عن محمد بن عمرو قال قال رسول الله ﷺ",
            NormalizedMatn = "", NormalizedBookName = ""
        };
        db.Hadiths.Add(hadith);
        Transmission T(int step, Narrator student, Narrator sheikh) => new()
        {
            Id = Guid.NewGuid(), HadithId = hadith.Id, StepOrder = step, StudentId = student.Id, SheikhId = sheikh.Id, TransmissionTerm = "حدثنا"
        };
        db.Transmissions.AddRange(
            T(1, hakim, qays), T(2, qays, qutayba), T(3, qutayba, ismail), T(4, ismail, amr),
            T(1, hakim, sayduni), T(2, sayduni, ayyub), T(3, ayyub, musa), T(4, musa, ismail), T(5, ismail, amr));
        await db.SaveChangesAsync();

        var report = await new IlalAnalysisService(db).AnalyzeAsync([hadith.Id]);

        report.AnalyzedHadithIds.Should().Equal(hadith.Id);          // one hadith…
        report.Turuq.Should().HaveCount(2);                          // …two chains
        report.Madars.Should().ContainSingle(m => m.NarratorId == ismail.Id && m.BranchCount == 2);
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Domain.Enums;
using SmartHadithTree.Etl.Services;
using SmartHadithTree.Infrastructure.Data;
using Xunit;
using Xunit.Abstractions;

namespace SmartHadithTree.Tests.Etl;

public class ShamelaIlalServiceTests(ITestOutputHelper output)
{
    private const string SmallIlal = """
        {
          "mudallisin": [
            {"num": 1, "tier": 2, "id": "tk1"},
            {"num": 9, "tier": 4, "id": "tk2"},
            {"num": 150, "tier": null, "id": "tk3"},
            {"num": 5, "tier": 1, "id": "tk-missing"},
            {"num": 6, "tier": 1, "id": null}
          ],
          "mukhtalitun": [
            {"id": "tk4",
             "ikhtilat": [{"book": 309, "text": "اختلط بآخره"}, {"book": 25846, "text": "اختلط بآخره"}, {"book": 25846, "text": "ساء حفظه"}],
             "severity": [{"book": 309, "value": "light"}, {"book": 25846, "value": "harmful"}],
             "no_one_after": [],
             "group_rules": [{"group": "من سمع منه قديما", "timing": "before", "quote": "فمن سمع منه قديما فصحيح", "book": 309},
                             {"group": "", "timing": "after", "quote": "x", "book": 309}],
             "hearings": [
               {"id": "tk1", "timing": "before", "claims": [{"timing": "before", "quote": "كتبنا عنه قبل التخليط", "book": 309}]},
               {"id": "tk2", "timing": "after", "claims": [{"timing": "before", "quote": "غيره", "book": 309}, {"timing": "after", "quote": "سمع بعد ما اختلط", "book": 25846}]},
               {"id": "tk2", "timing": "both", "claims": []},
               {"id": "tk4", "timing": "before", "claims": []},
               {"id": null, "timing": "before", "claims": []},
               {"id": "tk-missing", "timing": "conflict", "claims": []}]},
            {"id": "tk3", "ikhtilat": [], "severity": [], "no_one_after": [{"book": 309, "entry": "main:3"}]}
          ]
        }
        """;

    private static HadithTreeDbContext NewDb() =>
        new(new DbContextOptionsBuilder<HadithTreeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Narrator N(string key) => new() { Id = Guid.NewGuid(), FullName = key, SourceKey = key };

    private static async Task<string> WriteAsync(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ilal-{Guid.NewGuid()}.json");
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    [Fact]
    public async Task RunAsync_AppliesTiersSeverityHearingsAndGroupRules()
    {
        await using var db = NewDb();
        var (n1, n2, n3, n4) = (N("tk1"), N("tk2"), N("tk3"), N("tk4"));
        db.Narrators.AddRange(n1, n2, n3, n4);
        // Left over from an earlier run: must be reset.
        n1.HasMukhtalit = true;
        n1.IkhtilatSeverity = IkhtilatSeverity.Light;
        await db.SaveChangesAsync();

        await new ShamelaIlalService(db, NullLogger<ShamelaIlalService>.Instance).RunAsync(await WriteAsync(SmallIlal));

        (await db.Narrators.FindAsync(n1.Id))!.Should().Match<Narrator>(n => n.IsMudallis && n.MudallisTier == 2 && !n.HasMukhtalit && n.IkhtilatSeverity == null);
        (await db.Narrators.FindAsync(n2.Id))!.Should().Match<Narrator>(n => n.IsMudallis && n.MudallisTier == 4);
        (await db.Narrators.FindAsync(n3.Id))!.Should().Match<Narrator>(
            n => n.IsMudallis && n.MudallisTier == null && n.HasMukhtalit && n.NoHearingAfterIkhtilat && n.IkhtilatSeverity == IkhtilatSeverity.Disputed);
        var mukhtalit = (await db.Narrators.FindAsync(n4.Id))!;
        mukhtalit.HasMukhtalit.Should().BeTrue();
        mukhtalit.IkhtilatSeverity.Should().Be(IkhtilatSeverity.Harmful);       // the most severe of the books' values
        mukhtalit.NoHearingAfterIkhtilat.Should().BeFalse();
        mukhtalit.IkhtilatNote.Should().Be("اختلط بآخره — ساء حفظه");            // distinct texts joined

        var hearings = await db.MukhtalitHearings.Where(h => h.MukhtalitId == n4.Id).ToListAsync();
        hearings.Should().HaveCount(2);                                          // tk2 once, no self, no null, no unknown narrator
        hearings.Single(h => h.StudentId == n1.Id).Should().Match<MukhtalitHearing>(
            h => h.Timing == HearingTiming.Before && h.Evidence == "كتبنا عنه قبل التخليط" && h.SourceBook == "الكواكب النيرات");
        hearings.Single(h => h.StudentId == n2.Id).Should().Match<MukhtalitHearing>(
            h => h.Timing == HearingTiming.After && h.Evidence == "سمع بعد ما اختلط" && h.SourceBook == "المختلطين للعلائي");

        var rule = (await db.MukhtalitGroupRules.ToListAsync()).Should().ContainSingle().Subject;   // the rule without a group is dropped
        (rule.MukhtalitId, rule.GroupAr, rule.Timing, rule.SourceBook).Should().Be((n4.Id, "من سمع منه قديما", HearingTiming.Before, "الكواكب النيرات"));
    }

    [Fact]
    public async Task RunAsync_IsRepeatable()
    {
        await using var db = NewDb();
        db.Narrators.AddRange(N("tk1"), N("tk2"), N("tk3"), N("tk4"));
        await db.SaveChangesAsync();
        var service = new ShamelaIlalService(db, NullLogger<ShamelaIlalService>.Instance);
        var path = await WriteAsync(SmallIlal);

        await service.RunAsync(path);
        await service.RunAsync(path);

        (await db.MukhtalitHearings.CountAsync()).Should().Be(2);
        (await db.MukhtalitGroupRules.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("before", HearingTiming.Before)]
    [InlineData("after", HearingTiming.After)]
    [InlineData("both", HearingTiming.Both)]
    [InlineData("conflict", HearingTiming.Conflict)]
    [InlineData(null, HearingTiming.Unknown)]
    [InlineData("whatever", HearingTiming.Unknown)]
    public void ParseTiming_ReadsTheFile(string? text, HearingTiming expected) =>
        ShamelaIlalService.ParseTiming(text).Should().Be(expected);

    [Fact]
    public async Task OnTheRealData_EveryLinkedEntryIsApplied()
    {
        // Needs data/shamela_rijal/ilal.json; passes silently where it is absent.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "data", "shamela_rijal", "ilal.json"))) dir = dir.Parent;
        if (dir is null) return;

        var file = System.Text.Json.JsonSerializer.Deserialize<ShamelaIlalService.IlalFile>(
            await File.ReadAllTextAsync(Path.Combine(dir.FullName, "data", "shamela_rijal", "ilal.json")))!;
        var keys = file.Mudallisin.Select(m => m.Id).Concat(file.Mukhtalitun.Select(m => m.Id))
            .Concat(file.Mukhtalitun.SelectMany(m => m.Hearings.Select(h => h.Id))).Where(k => k != null).Distinct().ToList();
        await using var db = NewDb();
        db.Narrators.AddRange(keys.Select(k => N(k!)));
        await db.SaveChangesAsync();

        await new ShamelaIlalService(db, NullLogger<ShamelaIlalService>.Instance)
            .RunAsync(Path.Combine(dir.FullName, "data", "shamela_rijal", "ilal.json"));

        output.WriteLine($"mudallisin {await db.Narrators.CountAsync(n => n.IsMudallis)}, with tier {await db.Narrators.CountAsync(n => n.MudallisTier != null)}, " +
                         $"mukhtalitun {await db.Narrators.CountAsync(n => n.HasMukhtalit)}, hearings {await db.MukhtalitHearings.CountAsync()}, " +
                         $"group rules {await db.MukhtalitGroupRules.CountAsync()}, no-one-after {await db.Narrators.CountAsync(n => n.NoHearingAfterIkhtilat)}");
        (await db.Narrators.CountAsync(n => n.IsMudallis)).Should().Be(158);
        (await db.Narrators.CountAsync(n => n.HasMukhtalit)).Should().Be(131);
        (await db.Narrators.Where(n => n.HasMukhtalit).CountAsync(n => n.IkhtilatSeverity == null)).Should().Be(0);
    }
}

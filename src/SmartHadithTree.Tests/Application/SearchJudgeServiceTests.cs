using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Infrastructure.Data;
using Xunit;

namespace SmartHadithTree.Tests.Application;

public class SearchJudgeServiceTests
{
    private const string Matn = "الطهور شطر الايمان والحمد لله تملا الميزان";
    private const string GoodQuote = "الطهور شطر الايمان";

    private sealed class FakeChat(Func<string, string> respond) : IChatCompletionService
    {
        public int Calls;
        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            var reply = respond(chatHistory[^1].Content ?? string.Empty);
            return Task.FromResult<IReadOnlyList<ChatMessageContent>>([new ChatMessageContent(AuthorRole.Assistant, reply)]);
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static (SearchJudgeService Service, List<Guid> Ids, FakeChat? Chat) Create(int count, Func<string, string>? respond)
    {
        var options = new DbContextOptionsBuilder<HadithTreeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new HadithTreeDbContext(options);
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);
            context.Hadiths.Add(new HadithText
            {
                Id = id, BookName = "كتاب", HadithNumber = i + 1,
                MatnArabic = Matn, NormalizedMatn = Matn, NormalizedBookName = "كتاب"
            });
        }
        context.SaveChanges();

        FakeChat? chat = null;
        Kernel kernel;
        if (respond == null) kernel = new Kernel();
        else
        {
            chat = new FakeChat(respond);
            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton<IChatCompletionService>(chat);
            kernel = builder.Build();
        }
        return (new SearchJudgeService(context, kernel), ids, chat);
    }

    /// <summary>Answers every numbered passage in the prompt with the given level and quote.</summary>
    private static Func<string, string> AnswerAll(string level, string quote = GoodQuote) => prompt =>
    {
        var n = Regex.Matches(prompt, @"^\[(\d+)\]", RegexOptions.Multiline).Count;
        var items = Enumerable.Range(1, n).Select(i =>
            $"{{\"Index\":{i},\"Level\":\"{level}\",\"Quote\":\"{quote}\",\"Reason\":\"سبب\"}}");
        return "[" + string.Join(",", items) + "]";
    };

    [Fact]
    public async Task Judge_ShouldLabelResults_WhenAnswerIsValid()
    {
        var (service, ids, _) = Create(3, AnswerAll("match"));

        var result = await service.JudgeAsync("الطهور شطر الايمان ق1", ids);

        result.Status.Should().Be(SearchJudgeStatus.Reviewed);
        result.Items.Select(i => i.Id).Should().Equal(ids);
        result.Items.Should().OnlyContain(i => i.Level == SearchJudgeLevel.Match && i.Quote == GoodQuote);
    }

    [Fact]
    public async Task Judge_ShouldNotLabel_WhenQuoteIsNotInTheMatn()
    {
        var (service, ids, _) = Create(2, AnswerAll("match", "عبارة ليست في المتن اطلاقا"));

        var result = await service.JudgeAsync("الطهور شطر الايمان ق2", ids);

        result.Items.Should().OnlyContain(i => i.Level == SearchJudgeLevel.NotJudged);
    }

    [Fact]
    public async Task Judge_ShouldNotLabel_WhenQuoteHasFewerThanThreeWords()
    {
        var (service, ids, _) = Create(1, AnswerAll("match", "الطهور شطر"));

        var result = await service.JudgeAsync("الطهور شطر الايمان ق3", ids);

        result.Items[0].Level.Should().Be(SearchJudgeLevel.NotJudged);
    }

    [Fact]
    public async Task Judge_ShouldMarkRepeatedMissingAndOutOfRangeIndexes_AsNotJudged()
    {
        // Index 1 twice (ambiguous), index 2 absent, index 9 out of range.
        string Reply(string _) =>
            $"[{{\"Index\":1,\"Level\":\"match\",\"Quote\":\"{GoodQuote}\"}}," +
            $"{{\"Index\":1,\"Level\":\"scattered\",\"Quote\":\"{GoodQuote}\"}}," +
            $"{{\"Index\":9,\"Level\":\"match\",\"Quote\":\"{GoodQuote}\"}}]";
        var (service, ids, _) = Create(2, Reply);

        var result = await service.JudgeAsync("الطهور شطر الايمان ق4", ids);

        result.Items.Should().OnlyContain(i => i.Level == SearchJudgeLevel.NotJudged);
    }

    [Fact]
    public async Task Judge_ShouldReportUnavailable_WhenNoModelIsConfigured()
    {
        var (service, ids, _) = Create(2, null);

        var result = await service.JudgeAsync("الطهور شطر الايمان ق5", ids);

        result.Status.Should().Be(SearchJudgeStatus.Unavailable);
        result.Items.Should().OnlyContain(i => i.Level == SearchJudgeLevel.NotJudged);
    }

    [Fact]
    public async Task Judge_ShouldReportTimeout_WhenTheCallIsCancelledByTheTimeLimit()
    {
        var (service, ids, _) = Create(2, _ => throw new OperationCanceledException());

        var result = await service.JudgeAsync("الطهور شطر الايمان ق6", ids);

        result.Status.Should().Be(SearchJudgeStatus.Timeout);
    }

    [Fact]
    public async Task Judge_ShouldSplitLargePagesIntoChunks()
    {
        var (service, ids, chat) = Create(45, AnswerAll("partial"));

        var result = await service.JudgeAsync("الطهور شطر الايمان ق7", ids);

        chat!.Calls.Should().Be(3, "45 results are sent as 20 + 20 + 5");
        result.Status.Should().Be(SearchJudgeStatus.Reviewed);
        result.Items.Should().HaveCount(45).And.OnlyContain(i => i.Level == SearchJudgeLevel.Partial);
    }

    [Fact]
    public async Task Judge_ShouldAnswerFromCache_OnTheSecondCall()
    {
        var (service, ids, chat) = Create(3, AnswerAll("match"));

        await service.JudgeAsync("الطهور شطر الايمان ق8", ids);
        var second = await service.JudgeAsync("الطهور شطر الايمان ق8", ids);

        chat!.Calls.Should().Be(1);
        second.Items.Should().OnlyContain(i => i.Level == SearchJudgeLevel.Match);
    }

    [Fact]
    public async Task Judge_ShouldBeInvalid_WithoutTermsOrIds()
    {
        var (service, ids, _) = Create(1, AnswerAll("match"));

        (await service.JudgeAsync("   ", ids)).Status.Should().Be(SearchJudgeStatus.Invalid);
        (await service.JudgeAsync("الطهور شطر", new List<Guid>())).Status.Should().Be(SearchJudgeStatus.Invalid);
    }

    [Fact]
    public async Task Judge_ShouldIgnoreFencesAndExtraText_AroundTheJson()
    {
        var (service, ids, _) = Create(1, p => "```json\n" + AnswerAll("scattered")(p) + "\n```");

        var result = await service.JudgeAsync("الطهور شطر الايمان ق10", ids);

        result.Items[0].Level.Should().Be(SearchJudgeLevel.Scattered);
    }

    [Fact]
    public async Task Judge_ShouldAcceptQuote_WithOtherPunctuationHamzaAndDiacritics()
    {
        var (service, ids, _) = Create(1, AnswerAll("match", "الطُّهُورُ شَطْرُ الْإِيمَانِ، والحمد"));

        var result = await service.JudgeAsync("الطهور شطر الايمان ق11", ids);

        result.Items[0].Level.Should().Be(SearchJudgeLevel.Match);
    }

    [Fact]
    public async Task Judge_ShouldReportUnavailable_AndSayWhy_WhenTheAnswerIsNotJson()
    {
        var (service, ids, _) = Create(2, _ => "I cannot help with that.");

        var result = await service.JudgeAsync("الطهور شطر الايمان ق12", ids);

        result.Status.Should().Be(SearchJudgeStatus.Unavailable);
        result.Diagnostics.Should().Contain("unreadable-json=1").And.Contain("chunk-unreadable-json=1");
        result.Items.Should().OnlyContain(i => i.Level == SearchJudgeLevel.NotJudged);
    }

    [Fact]
    public async Task Judge_ShouldExplainRejections_InDiagnostics()
    {
        var (service, ids, _) = Create(2, AnswerAll("match", "عبارة ليست في المتن اطلاقا"));

        var result = await service.JudgeAsync("الطهور شطر الايمان ق13", ids);

        result.Diagnostics.Should().Contain("quote-not-in-matn=2").And.Contain("asked=2").And.Contain("chunks=1");
    }

    [Fact]
    public async Task Judge_ShouldFindTheArray_WhenTextSurroundsIt()
    {
        var (service, ids, _) = Create(1, p => "Here is the result: " + AnswerAll("partial")(p) + " Hope it helps.");

        var result = await service.JudgeAsync("الطهور شطر الايمان ق14", ids);

        result.Items[0].Level.Should().Be(SearchJudgeLevel.Partial);
    }
}

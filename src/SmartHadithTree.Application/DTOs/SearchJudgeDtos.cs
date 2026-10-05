namespace SmartHadithTree.Application.DTOs;

/// <summary>The search text and the ids of the results on the current page (at most 50).</summary>
public class SearchJudgeRequestDto
{
    public string Query { get; set; } = string.Empty;
    public List<Guid> Ids { get; set; } = [];
}

/// <summary>The AI's reading of one result. Only a verified quote from the matn backs a label.</summary>
public class SearchJudgeItemDto
{
    public Guid Id { get; set; }

    /// <summary>See <see cref="SearchJudgeLevel"/>.</summary>
    public string Level { get; set; } = SearchJudgeLevel.NotJudged;

    /// <summary>Short Arabic reason from the model. Empty when not judged.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>A phrase copied from the matn that supports the label (verified to occur in it).</summary>
    public string Quote { get; set; } = string.Empty;
}

public class SearchJudgeResponseDto
{
    /// <summary>See <see cref="SearchJudgeStatus"/>.</summary>
    public string Status { get; set; } = SearchJudgeStatus.Unavailable;

    /// <summary>What happened (results asked, cached, chunks, failures, why answers were rejected). For debugging and evaluation.</summary>
    public string? Diagnostics { get; set; }

    /// <summary>One item per requested id, in the requested order.</summary>
    public List<SearchJudgeItemDto> Items { get; set; } = [];
}

public static class SearchJudgeLevel
{
    /// <summary>يطابق: the matn is about the meaning of the search.</summary>
    public const string Match = "match";

    /// <summary>يطابق جزئياً.</summary>
    public const string Partial = "partial";

    /// <summary>كلمات متفرقة: the words occur but the matn is not about the search.</summary>
    public const string Scattered = "scattered";

    /// <summary>The model did not answer for this result, or its answer failed verification. Never hidden by the UI.</summary>
    public const string NotJudged = "not-judged";

    public static bool IsJudged(string? level) => level is Match or Partial or Scattered;
}

public static class SearchJudgeStatus
{
    public const string Reviewed = "reviewed";

    /// <summary>Some parts of the request were answered and some were not.</summary>
    public const string Partial = "partial";
    public const string Timeout = "timeout";
    public const string Unavailable = "unavailable";
    public const string Invalid = "invalid";
}

namespace SmartHadithTree.Application.DTOs;

public enum SearchScope
{
    All = 0,
    Matn = 1,
    Isnad = 2
}

public enum SearchMatchType
{
    AllWords = 0,
    AnyWord = 1,
    Exact = 2
}

public enum SearchLogicalOperator
{
    And = 0,
    Or = 1
}

public class SearchRequestDto
{
    public string Query { get; set; } = string.Empty;
    public SearchScope Scope { get; set; } = SearchScope.All;
    public SearchMatchType Match { get; set; } = SearchMatchType.AllWords;

    // ── Shamela Advanced Search Properties ──────────────────────────
    /// <summary>
    /// Legacy phrases list (retained for backward compatibility).
    /// </summary>
    public List<string> Phrases { get; set; } = [];
    public SearchLogicalOperator Operator { get; set; } = SearchLogicalOperator.And;

    /// <summary>
    /// Phrases that MUST all be present (AND).
    /// </summary>
    public List<string> AndPhrases { get; set; } = [];

    /// <summary>
    /// Phrases where AT LEAST ONE must be present (OR).
    /// </summary>
    public List<string> OrPhrases { get; set; } = [];

    /// <summary>
    /// Phrases that MUST NOT be present (NOT / ليس).
    /// </summary>
    public List<string> ExcludePhrases { get; set; } = [];

    public bool IsOrdered { get; set; } = false;
    public bool IsProximity { get; set; } = false;
    public int ProximityWords { get; set; } = 15;

    // ── Pagination Properties ───────────────────────────────────────
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

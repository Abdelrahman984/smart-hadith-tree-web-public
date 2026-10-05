namespace SmartHadithTree.Application.DTOs;

/// <summary>Request to check whether a pasted text is a hadith that exists in the corpus.</summary>
public class VerifyHadithRequestDto
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>A corpus hadith that matches the checked text, with how closely it matches.</summary>
public class VerifiedHadithMatchDto
{
    public Guid Id { get; set; }
    public string BookName { get; set; } = string.Empty;
    public int HadithNumber { get; set; }
    public string? Chapter { get; set; }
    public string MatnArabic { get; set; } = string.Empty;

    /// <summary>Share (0..1) of the checked text's word pairs found, in order, in this hadith.</summary>
    public double Similarity { get; set; }
}

/// <summary>Outcome of checking a text against the corpus. Only a non-empty <see cref="Matches"/> names a source.</summary>
public class HadithVerificationResultDto
{
    public string Status { get; set; } = HadithVerificationStatus.NotFound;

    /// <summary>"lexical" (text matching only) or "ai+lexical" (the model also reviewed the candidates).</summary>
    public string Method { get; set; } = "lexical";

    public string Explanation { get; set; } = string.Empty;

    /// <summary>What happened to the model review; see <see cref="VerificationModelStatus"/>.</summary>
    public string ModelStatus { get; set; } = VerificationModelStatus.NotNeeded;

    /// <summary>Why the answer came out this way (candidate count, best score, which rule decided). For debugging and evaluation.</summary>
    public string? Diagnostics { get; set; }

    /// <summary>Words of the checked text that are not in the closest matn (shows which word was changed).</summary>
    public List<string> UnmatchedWords { get; set; } = [];

    public List<VerifiedHadithMatchDto> Matches { get; set; } = [];
}

/// <summary>
/// <c>exact</c>: the text is quoted from the matn of the listed hadith(s).
/// <c>variant</c>: close wording; compare with the listed matn.
/// <c>not-found</c>: nothing in the corpus supports it; the system does not claim it is fabricated.
/// <c>invalid</c>: the input is too short or too long to check.
/// </summary>
public static class HadithVerificationStatus
{
    public const string Exact = "exact";
    public const string Variant = "variant";
    public const string NotFound = "not-found";
    public const string Invalid = "invalid";
}

/// <summary>
/// Whether the model took part. <c>unavailable</c> and <c>timeout</c> mean the answer came from text matching only,
/// so a run with many of them says nothing about the model's value.
/// </summary>
public static class VerificationModelStatus
{
    /// <summary>No review was needed (exact quotation, invalid input, or nothing close enough to review).</summary>
    public const string NotNeeded = "not-needed";
    public const string Reviewed = "reviewed";
    public const string Timeout = "timeout";
    public const string Unavailable = "unavailable";
}

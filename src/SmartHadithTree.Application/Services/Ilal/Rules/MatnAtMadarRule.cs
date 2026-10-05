using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Domain.Enums;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services.Ilal.Rules;

/// <summary>
/// Compares the matn carried by each branch at every madar to detect:
/// زيادة الثقة (accepted addition), الشذوذ (a reliable narrator contradicting stronger/more numerous peers),
/// النكارة (a weak narrator contradicting reliable peers), and الاضطراب (conflict between
/// branches of comparable strength that cannot be resolved by preference).
/// </summary>
public sealed class MatnAtMadarRule : IIlalRule
{
    /// <summary>Below this similarity the texts are treated as different hadiths and not compared.</summary>
    public const double MinRelatedSimilarity = 0.4;

    /// <summary>Minimum number of words for an addition or omission to count.</summary>
    public const int MinSignificantWords = 3;

    /// <summary>
    /// Minimum number of substituted words for a contradiction. Single-word substitutions are
    /// usually narration by meaning (الرواية بالمعنى) and are not flagged.
    /// </summary>
    public const int MinSubstitutedWords = 2;

    /// <summary>How a compared text differs from a reference text.</summary>
    public enum DiffKind { None, Unrelated, Addition, Omission, Contradiction }

    private sealed class TextCluster
    {
        public required IlalChain Representative { get; init; }
        public required string[] Tokens { get; init; }
        public List<IsnadBranch> Branches { get; } = [];
        public int BestTier => Branches.Min(b => b.StudentTier);
    }

    public IEnumerable<IlalFindingDto> Evaluate(IlalContext context)
    {
        foreach (var split in IsnadBranching.FindSplitPoints(context))
        {
            foreach (var finding in EvaluateSplit(context, split))
                yield return finding;
        }
    }

    private static IEnumerable<IlalFindingDto> EvaluateSplit(IlalContext context, SplitPoint split)
    {
        // 1. Group branches whose texts agree (only minor wording differences) into clusters.
        var clusters = new List<TextCluster>();
        foreach (var branch in split.Branches)
        {
            var rep = branch.Chains[0];
            var tokens = MatnText.Tokenize(MatnText.ExtractBody(rep.MatnArabic));
            if (tokens.Length == 0) continue;

            var home = clusters.FirstOrDefault(c => Classify(MatnAligner.Align(c.Tokens, tokens)) == DiffKind.None);
            if (home == null)
            {
                home = new TextCluster { Representative = rep, Tokens = tokens };
                clusters.Add(home);
            }
            home.Branches.Add(branch);
        }

        if (clusters.Count < 2) yield break;

        // 2. The preferred text (المحفوظ) is carried by the strongest cluster.
        var main = clusters.Aggregate((best, c) => IsnadBranching.Compare(c.Branches, best.Branches) > 0 ? c : best);
        var madarName = context.NameOf(split.MadarId);
        var conflicting = new List<TextCluster>();

        foreach (var other in clusters.Where(c => c != main))
        {
            var diff = MatnAligner.Align(main.Tokens, other.Tokens);
            var kind = Classify(diff);
            if (kind is DiffKind.None or DiffKind.Unrelated) continue;

            var comparison = ToComparison(main.Representative, other.Representative, diff);

            if (kind == DiffKind.Contradiction)
            {
                if (IsnadBranching.Compare(main.Branches, other.Branches) == 0)
                {
                    conflicting.Add(other);
                    continue;
                }

                var weak = other.BestTier >= 6;
                yield return new IlalFindingDto
                {
                    Type = weak ? IllahType.Nakarah : IllahType.Shudhudh,
                    Severity = IllahSeverity.Qadihah,
                    TitleAr = weak ? "مخالفة منكرة في المتن" : "مخالفة شاذة في المتن",
                    EvidenceAr =
                        $"اختلف الرواة عن {madarName} في لفظ المتن: رواه {Describe(context, other)} بلفظ يخالف رواية {Describe(context, main)}، "
                        + $"وهم أرجح {(main.Branches.Count > other.Branches.Count ? "عدداً" : "حفظاً")}، فالمحفوظ روايتهم"
                        + (weak ? " والمخالفة منكرة لضعف راويها." : " ورواية المخالف شاذة."),
                    NarratorIds = [split.MadarId, .. other.Branches.Select(b => b.StudentId)],
                    HadithIds = other.Branches.SelectMany(b => b.Chains).Select(c => c.HadithId).Distinct().ToList(),
                    Confidence = 0.5,
                    MatnComparison = comparison
                };
                continue;
            }

            // Addition by one side (زيادة)
            var (adder, omitter) = kind == DiffKind.Addition ? (other, main) : (main, other);
            var addedText = string.Join(" / ", diff.Segments
                .Where(s => s.Kind == (kind == DiffKind.Addition ? AlignmentKind.Added : AlignmentKind.Removed) && s.Tokens.Count >= 2)
                .Select(s => s.Text));
            var baseEvidence =
                $"زاد {Describe(context, adder)} عن {madarName} في المتن: «{addedText}»، ولم يذكرها {Describe(context, omitter)}.";

            if (adder.BestTier >= 6)
            {
                yield return Addition(IllahType.Nakarah, IllahSeverity.Qadihah, "زيادة منكرة",
                    $"{baseEvidence} وراوي الزيادة ضعيف فلا تقبل زيادته.", 0.6);
            }
            else if (IsnadBranching.Compare(adder.Branches, omitter.Branches) < 0)
            {
                yield return Addition(IllahType.Shudhudh, IllahSeverity.Tanbih, "زيادة يُخشى شذوذها",
                    $"{baseEvidence} ومن لم يذكرها أرجح، فيُنظر في قرائن قبول الزيادة.", 0.45);
            }
            else
            {
                var trusted = adder.BestTier <= 4;
                yield return Addition(IllahType.Ziyadah, trusted ? IllahSeverity.GhayrQadihah : IllahSeverity.Tanbih,
                    trusted ? "زيادة ثقة" : "زيادة تحتاج إلى نظر",
                    trusted
                        ? $"{baseEvidence} وراويها ثقة لم يخالف من هو أرجح منه، فهي زيادة ثقة مقبولة."
                        : $"{baseEvidence} وراويها دون الثقة، فيُنظر في قبولها.",
                    trusted ? 0.6 : 0.45);
            }

            IlalFindingDto Addition(IllahType type, IllahSeverity severity, string title, string evidence, double confidence) => new()
            {
                Type = type,
                Severity = severity,
                TitleAr = title,
                EvidenceAr = evidence,
                NarratorIds = [split.MadarId, .. adder.Branches.Select(b => b.StudentId)],
                HadithIds = adder.Branches.SelectMany(b => b.Chains).Select(c => c.HadithId).Distinct().ToList(),
                Confidence = confidence,
                MatnComparison = comparison
            };
        }

        // 3. Conflicts that cannot be resolved by preference: اضطراب
        if (conflicting.Count > 0)
        {
            var involved = conflicting.Prepend(main).ToList();
            var first = conflicting[0];
            yield return new IlalFindingDto
            {
                Type = IllahType.Idtirab,
                Severity = IllahSeverity.Qadihah,
                TitleAr = "اضطراب في المتن",
                EvidenceAr =
                    $"اختلف الرواة عن {madarName} في المتن اختلافاً لا يمكن الترجيح فيه لتقارب منزلتهم: "
                    + string.Join("، ", involved.Select(c => $"رواية {Describe(context, c)}"))
                    + ". فإن تعذّر الجمع بين الروايات فالحديث مضطرب.",
                NarratorIds = [split.MadarId, .. involved.SelectMany(c => c.Branches).Select(b => b.StudentId)],
                HadithIds = involved.SelectMany(c => c.Branches).SelectMany(b => b.Chains).Select(c => c.HadithId).Distinct().ToList(),
                Confidence = 0.45,
                MatnComparison = ToComparison(main.Representative, first.Representative,
                    MatnAligner.Align(main.Tokens, first.Tokens))
            };
        }
    }

    private static readonly HashSet<string> FramingTokens = new(StringComparer.Ordinal)
    {
        "ان", "انه", "انها", "قال", "فقال", "النبي", "رسول", "الله", "نبي",
        // Narrative preface that only sets the scene («كنت مع رسول الله في بعض أسفاره وكان …»); a text
        // that opens with it and one that does not are the same hadith, not an addition.
        "كنت", "مع", "في", "بعض", "اسفاره", "كان", "وكان"
    };

    /// <summary>Classifies the difference between a reference text and a compared text.</summary>
    public static DiffKind Classify(AlignmentResult diff)
    {
        if (!AreRelated(diff)) return DiffKind.Unrelated;

        var addedTokens = diff.Segments
            .Where(s => s.Kind == AlignmentKind.Added)
            .SelectMany(s => s.Tokens)
            .Where(t => !FramingTokens.Contains(t))
            .ToList();
        var removedTokens = diff.Segments
            .Where(s => s.Kind == AlignmentKind.Removed)
            .SelectMany(s => s.Tokens)
            .Where(t => !FramingTokens.Contains(t))
            .ToList();

        // Discount word-order transpositions (تقديم وتأخير) where the exact same word appears in both Added and Removed
        var removedSet = new HashSet<string>(removedTokens, StringComparer.Ordinal);
        int transposedCount = addedTokens.Count(removedSet.Contains);

        var sites = SubstitutionSites(diff);
        var substituted = sites.Sum();
        var maxSingleSubstitution = sites.DefaultIfEmpty(0).Max();
        var addedOnly = Math.Max(0, addedTokens.Count - substituted - transposedCount);
        var removedOnly = Math.Max(0, removedTokens.Count - substituted - transposedCount);

        // Multi-word contiguous substitution at a single site is a direct contradiction
        if (maxSingleSubstitution >= MinSubstitutedWords && diff.Similarity < 0.9) return DiffKind.Contradiction;
        if (addedOnly >= MinSignificantWords && removedOnly >= MinSignificantWords) return DiffKind.Contradiction;
        if ((addedOnly >= MinSignificantWords || (addedTokens.Count - transposedCount >= MinSignificantWords && maxSingleSubstitution < MinSubstitutedWords))
            && diff.LongestAddedRun >= 2 && removedOnly < MinSignificantWords)
        {
            return DiffKind.Addition;
        }
        if ((removedOnly >= MinSignificantWords || (removedTokens.Count - transposedCount >= MinSignificantWords && maxSingleSubstitution < MinSubstitutedWords))
            && LongestRemovedRun(diff) >= 2 && addedOnly < MinSignificantWords)
        {
            return DiffKind.Omission;
        }
        if (substituted >= MinSubstitutedWords && diff.Similarity < 0.9) return DiffKind.Contradiction;
        return DiffKind.None;
    }

    /// <summary>
    /// Whether the two texts are versions of the same matn. The overall similarity is not enough on its
    /// own: a long addition lowers it although the shorter text is wholly contained in the longer one,
    /// and framing words («كان النبي» / «كنت مع رسول الله في بعض أسفاره») inflate the difference in short texts.
    /// So the content words are compared too, by overlap with the shorter text.
    /// </summary>
    private static bool AreRelated(AlignmentResult diff)
    {
        if (diff.Similarity >= MinRelatedSimilarity) return true;

        var shared = diff.Segments.Where(s => s.Kind == AlignmentKind.Equal).Sum(s => s.Tokens.Count(IsContentWord));
        var reference = shared + diff.Segments.Where(s => s.Kind == AlignmentKind.Removed).Sum(s => s.Tokens.Count(IsContentWord));
        var compared = shared + diff.Segments.Where(s => s.Kind == AlignmentKind.Added).Sum(s => s.Tokens.Count(IsContentWord));
        var shorter = Math.Min(reference, compared);

        return shorter >= MinSignificantWords && (double)shared / shorter >= MinRelatedSimilarity;
    }

    /// <summary>
    /// Size of each substitution site (a removed run directly next to an added run), counting only
    /// content words: swapping «كان النبي» for «كنت مع رسول الله» replaces framing, not wording.
    /// </summary>
    private static List<int> SubstitutionSites(AlignmentResult diff)
    {
        var sites = new List<int>();
        for (int i = 0; i < diff.Segments.Count - 1; i++)
        {
            var a = diff.Segments[i];
            var b = diff.Segments[i + 1];
            if (a.Kind != AlignmentKind.Equal && b.Kind != AlignmentKind.Equal && a.Kind != b.Kind)
            {
                var size = Math.Min(a.Tokens.Count(IsContentWord), b.Tokens.Count(IsContentWord));
                if (size > 0) sites.Add(size);
            }
        }
        return sites;
    }

    private static bool IsContentWord(string token) => !FramingTokens.Contains(token);

    private static int LongestRemovedRun(AlignmentResult diff) =>
        diff.Segments.Where(s => s.Kind == AlignmentKind.Removed).Select(s => s.Tokens.Count).DefaultIfEmpty(0).Max();

    private static string Describe(IlalContext context, TextCluster cluster) =>
        string.Join(" و", cluster.Branches.Select(b =>
            $"{context.NameOf(b.StudentId)} ({context.Narrator(b.StudentId)?.GradeLabel ?? "غير محرر"})"));

    private static MatnComparisonDto ToComparison(IlalChain reference, IlalChain compared, AlignmentResult diff) => new()
    {
        ReferenceHadithId = reference.HadithId,
        ComparedHadithId = compared.HadithId,
        Similarity = Math.Round(diff.Similarity, 3),
        Segments = diff.Segments.Select(s => new MatnSegmentDto
        {
            Kind = s.Kind switch
            {
                AlignmentKind.Added => "added",
                AlignmentKind.Removed => "removed",
                _ => "equal"
            },
            Text = s.Text
        }).ToList()
    };
}

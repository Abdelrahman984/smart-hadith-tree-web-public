using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Application.Services.Ilal.Rules;

/// <summary>
/// الاختلاط: a narration from a narrator whose memory deteriorated, by a student who heard from him after the
/// deterioration (or whose timing is unknown).
/// <para>
/// The books rate the ikhtilat itself (<see cref="IkhtilatSeverity"/>): most mukhtalitun are only «خفيف» or
/// «مختلف فيه», and flagging every link to them buries the real findings (3,082 of 4,325 on the 31 books).
/// So the severity decides what a link is worth: a harmful ikhtilat is flagged even when the timing is unknown;
/// a disputed one only as a low-confidence note, and only for a narrator graded صدوق يهم (rank 5) or weaker; a light one only
/// when the books say the student heard after it.
/// Narrators without a severity (data from before the Shamela books) are treated as harmful, as they were.
/// </para>
/// </summary>
public sealed class IkhtilatRule : IIlalRule
{
    /// <summary>Ibn Hajar's rank (صدوق يهم) from which a disputed ikhtilat with unknown timing is still worth a note.</summary>
    public const int MinTierForDisputedNote = 5;

    public IEnumerable<IlalFindingDto> Evaluate(IlalContext context)
    {
        var occurrences = context.Chains
            .SelectMany(c => c.Links.Select(l => (Chain: c, Link: l)))
            .Where(x => context.Narrator(x.Link.SheikhId)?.IsMukhtalit == true)
            .GroupBy(x => (x.Link.SheikhId, x.Link.StudentId));

        foreach (var group in occurrences)
        {
            var (mukhtalitId, studentId) = group.Key;
            var mukhtalit = context.Narrator(mukhtalitId)!;
            var chains = group.Select(x => x.Chain).Distinct().ToList();
            var timing = context.Hearings.GetValueOrDefault((mukhtalitId, studentId), HearingTiming.Unknown);
            var allSahihayn = chains.All(c => TransmissionTerms.IsSahihayn(c.BookName));
            var severity = mukhtalit.IkhtilatSeverity ?? IkhtilatSeverity.Harmful;

            if (timing == HearingTiming.Before) continue;                 // heard before the ikhtilat: sound
            if (timing == HearingTiming.Unknown && mukhtalit.NoHearingAfterIkhtilat) continue;   // nobody heard after it

            var note = string.IsNullOrWhiteSpace(mukhtalit.IkhtilatNote) ? "" : $" ({mukhtalit.IkhtilatNote})";
            var baseEvidence = $"{mukhtalit.Name} ممن اختلط{SeverityPhrase(mukhtalit.IkhtilatSeverity)}{note}، والراوي عنه هنا {context.NameOf(studentId)}";
            var said = context.HearingEvidence.TryGetValue((mukhtalitId, studentId), out var quote) ? $" قال: «{quote}»." : "";

            IllahSeverity illah;
            string evidence;
            double confidence;

            switch (timing)
            {
                case HearingTiming.After:
                    (illah, confidence) = (severity, allSahihayn) switch
                    {
                        (IkhtilatSeverity.Harmful, false) => (IllahSeverity.Qadihah, 0.8),
                        (IkhtilatSeverity.Harmful, true) or (IkhtilatSeverity.Disputed, _) => (IllahSeverity.GhayrQadihah, 0.5),
                        _ => (IllahSeverity.Tanbih, 0.35)
                    };
                    evidence = $"{baseEvidence}، وقد نص العلماء على أنه سمع منه بعد الاختلاط.{said}"
                               + (allSahihayn ? " وأصحاب الصحيح ينتقون من حديثه ما ثبت أنه حدّث به قبل الاختلاط." : "");
                    break;

                case HearingTiming.Both or HearingTiming.Conflict:
                    if (allSahihayn || severity == IkhtilatSeverity.Light) continue;
                    illah = IllahSeverity.Tanbih;
                    confidence = 0.4;
                    evidence = $"{baseEvidence}، " + (timing == HearingTiming.Both
                        ? "ونص العلماء على أنه سمع منه قبل الاختلاط وبعده، فلا يُعرف أيهما هنا."
                        : "واختلف العلماء في وقت سماعه منه: أقبل الاختلاط أم بعده.") + said;
                    break;

                default:    // timing unknown
                    if (allSahihayn || severity == IkhtilatSeverity.Light) continue;   // the Sahihs only include what was heard before
                    // A disputed ikhtilat of a narrator the books grade ثقة or صدوق (Ibn Uyayna, Hisham b. Urwa, Jarir) is not
                    // worth a note on every link: 48,689 of the 60,487 notes on the 31 books came from such narrators.
                    if (severity == IkhtilatSeverity.Disputed && mukhtalit.Tier < MinTierForDisputedNote) continue;
                    illah = IllahSeverity.Tanbih;
                    confidence = severity == IkhtilatSeverity.Harmful ? 0.45 : 0.3;
                    evidence = $"{baseEvidence}، ولم يتبين أسمع منه قبل الاختلاط أم بعده." + GroupRuleHint(context, mukhtalitId);
                    break;
            }

            yield return new IlalFindingDto
            {
                Type = IllahType.Ikhtilat,
                Severity = illah,
                TitleAr = timing == HearingTiming.After ? "رواية بعد الاختلاط" : "رواية عن مختلط",
                EvidenceAr = evidence,
                NarratorIds = [mukhtalitId, studentId],
                HadithIds = chains.Select(c => c.HadithId).Distinct().ToList(),
                Confidence = confidence
            };
        }
    }

    private static string SeverityPhrase(IkhtilatSeverity? severity) => severity switch
    {
        IkhtilatSeverity.Harmful => " اختلاطًا فاحشًا",
        IkhtilatSeverity.Disputed => " (واختُلف في اختلاطه)",
        IkhtilatSeverity.Light => " اختلاطًا خفيفًا",
        _ => ""
    };

    /// <summary>The books' rule for a group of his students, as a hint where the student's own timing is not recorded.</summary>
    private static string GroupRuleHint(IlalContext context, Guid mukhtalitId)
    {
        if (!context.GroupRules.TryGetValue(mukhtalitId, out var rules) || rules.Count == 0) return "";
        var rule = rules[0];
        var verdict = rule.Timing == HearingTiming.After ? "بعد الاختلاط" : "قبل الاختلاط";
        return $" وقد نُص في جماعة من تلاميذه ({rule.Group}) أن سماعهم {verdict}: «{rule.Quote}».";
    }
}

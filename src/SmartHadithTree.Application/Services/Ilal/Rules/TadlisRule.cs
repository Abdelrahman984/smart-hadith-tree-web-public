using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Application.Services.Ilal.Rules;

/// <summary>
/// تدليس الإسناد: a mudallis (Ibn Hajr tier 3+) narrates with an ambiguous formula (عن / أن / قال)
/// without stating that he heard (تصريح بالسماع) in any of the collected turuq.
/// </summary>
public sealed class TadlisRule : IIlalRule
{
    /// <summary>Mudallis tiers whose 'an'ana is not accepted without an explicit statement of hearing.</summary>
    public const int MinRejectedTier = 3;

    public IEnumerable<IlalFindingDto> Evaluate(IlalContext context)
    {
        var occurrences = context.Chains
            .SelectMany(c => c.Links.Select(l => (Chain: c, Link: l)))
            .Where(x => context.Narrator(x.Link.StudentId)?.MudallisTier >= MinRejectedTier
                        && TransmissionTerms.IsAmbiguous(x.Link.Term))
            .GroupBy(x => (x.Link.StudentId, x.Link.SheikhId));

        foreach (var group in occurrences)
        {
            var (mudallisId, sheikhId) = group.Key;
            var mudallis = context.Narrator(mudallisId)!;
            var tier = mudallis.MudallisTier!.Value;
            var chains = group.Select(x => x.Chain).Distinct().ToList();
            var term = group.First().Link.Term;

            // Did he state hearing from the same sheikh in another collected tariq?
            var explicitChain = context.Chains.FirstOrDefault(c => c.Links.Any(l =>
                l.StudentId == mudallisId && l.SheikhId == sheikhId && TransmissionTerms.IsExplicitHearing(l.Term)));

            var baseEvidence =
                $"{mudallis.Name} مذكور في المرتبة {tier} من مراتب المدلسين، وقد روى هنا عن شيخه ({context.NameOf(sheikhId)}) بصيغة «{term}».";

            IllahSeverity severity;
            string evidence;
            double confidence;

            if (explicitChain != null)
            {
                severity = IllahSeverity.Tanbih;
                evidence = $"{baseEvidence} لكنه صرّح بالسماع منه في رواية {explicitChain.Label}، فتزول شبهة التدليس.";
                confidence = 0.3;
            }
            else if (chains.Any(c => TransmissionTerms.IsSahihayn(c.BookName)))
            {
                // The Sahihs only include a mudallis's 'an'ana where his hearing from that sheikh is
                // established, so the same link is treated as connected in the other books too.
                severity = IllahSeverity.GhayrQadihah;
                evidence = $"{baseEvidence} وقد أخرج صاحبا الصحيح هذا الإسناد بالعنعنة، وعنعنة المدلس في الصحيحين محمولة على الاتصال عند جمهور العلماء.";
                confidence = 0.4;
            }
            else
            {
                severity = IllahSeverity.Qadihah;
                evidence = $"{baseEvidence} ولم يصرّح بالسماع في الطرق المجموعة، فيُخشى أن يكون أسقط واسطة بينهما.";
                confidence = tier >= 4 ? 0.75 : 0.6;
            }

            yield return new IlalFindingDto
            {
                Type = IllahType.Tadlis,
                Severity = severity,
                TitleAr = "عنعنة مدلس",
                EvidenceAr = evidence,
                NarratorIds = [mudallisId, sheikhId],
                HadithIds = chains.Select(c => c.HadithId).Distinct().ToList(),
                Confidence = confidence
            };
        }
    }
}

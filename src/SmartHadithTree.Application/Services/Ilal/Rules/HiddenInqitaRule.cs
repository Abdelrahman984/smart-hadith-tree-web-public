using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Application.Services.Ilal.Rules;

/// <summary>
/// الانقطاع الخفي: the sheikh is not listed among the student's teachers, nor the student among
/// the sheikh's students, although both have teacher/student data. Meeting (اللقاء) is unproven.
/// The compiler's own link (his report from his shaykh) is not judged: the biographical books rarely
/// list the compilers' shaykhs (Tahdhib al-Kamal gives none for al-Nasa'i or Ibn Majah), so a missing
/// relation there reflects the data, not the isnad.
/// </summary>
public sealed class HiddenInqitaRule : IIlalRule
{
    public IEnumerable<IlalFindingDto> Evaluate(IlalContext context)
    {
        var occurrences = context.Chains
            .SelectMany(c => c.Links.Select(l => (Chain: c, Link: l)))
            .Where(x => x.Link.StudentId != x.Chain.Links[0].StudentId)
            .Where(x => context.NarratorsWithRelations.Contains(x.Link.SheikhId)
                        && context.NarratorsWithRelations.Contains(x.Link.StudentId)
                        && !context.Relations.Contains((x.Link.SheikhId, x.Link.StudentId)))
            .GroupBy(x => (x.Link.SheikhId, x.Link.StudentId));

        foreach (var group in occurrences)
        {
            var (sheikhId, studentId) = group.Key;
            var chains = group.Select(x => x.Chain).Distinct().ToList();
            var statesHearing = group.Any(x => TransmissionTerms.IsExplicitHearing(x.Link.Term));

            var evidence =
                $"لم يُذكر ({context.NameOf(sheikhId)}) في شيوخ ({context.NameOf(studentId)})، ولا الثاني في تلاميذ الأول، فلم يثبت اللقاء بينهما."
                + (statesHearing
                    ? " لكن ورد التصريح بالسماع في الإسناد، فقد يكون نقصاً في كتب التراجم أو خطأً في تمييز الراوي."
                    : " وقد يكون ذلك نقصاً في كتب التراجم أو خطأً في تمييز الراوي آلياً، فيُراجع.");

            yield return new IlalFindingDto
            {
                Type = IllahType.HiddenInqita,
                Severity = IllahSeverity.Tanbih,
                TitleAr = "لم يثبت اللقاء",
                EvidenceAr = evidence,
                NarratorIds = [sheikhId, studentId],
                HadithIds = chains.Select(c => c.HadithId).Distinct().ToList(),
                Confidence = statesHearing ? 0.25 : 0.45
            };
        }
    }
}

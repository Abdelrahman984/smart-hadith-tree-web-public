using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Domain.Enums;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services.Ilal.Rules;

/// <summary>
/// الإرسال والاختلاف في الوصل والإرسال: a Successor attributes the text directly to the Prophet ﷺ
/// (مرسل). When other turuq through the same Successor name the Companion (موصول), the two sides
/// are weighed and the weaker one is flagged.
/// </summary>
public sealed class WaslIrsalRule : IIlalRule
{
    public IEnumerable<IlalFindingDto> Evaluate(IlalContext context)
    {
        var mursalByTabii = context.Chains
            .Where(c => c.TopNarratorId.HasValue
                        && context.Narrator(c.TopNarratorId.Value)?.IsTabii == true
                        && MatnText.IsMarfu(c.MatnArabic))
            .GroupBy(c => c.TopNarratorId!.Value);

        foreach (var group in mursalByTabii)
        {
            var tabiiId = group.Key;
            var tabiiName = context.NameOf(tabiiId);
            var mursalChains = group.ToList();

            // Turuq where the same Successor narrates it from a Companion.
            var mawsulChains = context.Chains
                .Where(c => c.LinkFrom(tabiiId) is { } link && context.Narrator(link.SheikhId)?.IsCompanion == true)
                .ToList();

            if (mawsulChains.Count == 0)
            {
                yield return new IlalFindingDto
                {
                    Type = IllahType.WaslIrsal,
                    Severity = IllahSeverity.Tanbih,
                    TitleAr = "ظاهره الإرسال",
                    EvidenceAr = $"ينتهي الإسناد إلى {tabiiName} وهو تابعي يرفعه إلى النبي ﷺ دون ذكر الصحابي، ولم يوجد في الطرق المجموعة من وصله. وقد يكون سقوط الصحابي من استخراج الإسناد آلياً، فيُراجع.",
                    NarratorIds = [tabiiId],
                    HadithIds = mursalChains.Select(c => c.HadithId).Distinct().ToList(),
                    Confidence = 0.35
                };
                continue;
            }

            var mursalSide = Branches(context, tabiiId, mursalChains);
            var mawsulSide = Branches(context, tabiiId, mawsulChains);
            if (mursalSide.Count == 0 || mawsulSide.Count == 0) continue;

            var companion = context.NameOf(mawsulChains[0].LinkFrom(tabiiId)!.SheikhId);
            var cmp = IsnadBranching.Compare(mawsulSide, mursalSide);

            var (severity, title, conclusion, flagged, confidence) = cmp switch
            {
                < 0 => (IllahSeverity.Qadihah, "وصل مرسل",
                    "ومن أرسله أرجح، فالصواب الإرسال ووصله خطأ.", mawsulSide, 0.6),
                > 0 => (IllahSeverity.GhayrQadihah, "إرسال لا يضر الوصل",
                    "ومن وصله أرجح، فالوصل محفوظ ولا يضره إرسال من أرسله.", mursalSide, 0.5),
                _ => (IllahSeverity.Tanbih, "اختلاف في الوصل والإرسال",
                    "والطرفان متقاربان، فيحتاج إلى نظر في القرائن.", mawsulSide.Concat(mursalSide).ToList(), 0.4)
            };

            yield return new IlalFindingDto
            {
                Type = IllahType.WaslIrsal,
                Severity = severity,
                TitleAr = title,
                EvidenceAr = $"اختُلف على {tabiiName}: فوصله {Names(context, mawsulSide)} عنه عن {companion}، وأرسله {Names(context, mursalSide)} عنه عن النبي ﷺ. {conclusion}",
                NarratorIds = [tabiiId, .. flagged.Select(b => b.StudentId)],
                HadithIds = flagged.SelectMany(b => b.Chains).Select(c => c.HadithId).Distinct().ToList(),
                Confidence = confidence
            };
        }
    }

    private static List<IsnadBranch> Branches(IlalContext context, Guid tabiiId, IEnumerable<IlalChain> chains) =>
        chains
            .Select(c => (Chain: c, Student: c.StudentOf(tabiiId)))
            .Where(x => x.Student.HasValue)
            .GroupBy(x => x.Student!.Value)
            .Select(g => new IsnadBranch
            {
                StudentId = g.Key,
                Chains = g.Select(x => x.Chain).ToList(),
                StudentTier = context.TierOf(g.Key)
            })
            .ToList();

    private static string Names(IlalContext context, IEnumerable<IsnadBranch> branches) =>
        string.Join(" و", branches.Select(b => context.NameOf(b.StudentId)));
}

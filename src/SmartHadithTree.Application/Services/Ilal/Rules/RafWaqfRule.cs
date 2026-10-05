using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Domain.Enums;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services.Ilal.Rules;

/// <summary>
/// الاختلاف في الرفع والوقف: at a madar, some branches attribute the text to the Prophet ﷺ (مرفوع)
/// while others stop at the Companion or later (موقوف). The weaker side is flagged.
/// </summary>
public sealed class RafWaqfRule : IIlalRule
{
    public IEnumerable<IlalFindingDto> Evaluate(IlalContext context)
    {
        foreach (var split in IsnadBranching.FindSplitPoints(context))
        {
            var marfu = new List<IsnadBranch>();
            var mawquf = new List<IsnadBranch>();

            foreach (var branch in split.Branches)
            {
                var flags = branch.Chains.Select(c => MatnText.IsMarfu(c.MatnArabic)).Distinct().ToList();
                if (flags.Count != 1) continue; // the branch itself is inconsistent; resolved at a lower split
                (flags[0] ? marfu : mawquf).Add(branch);
            }

            if (marfu.Count == 0 || mawquf.Count == 0) continue;

            var madarName = context.NameOf(split.MadarId);
            var rafiun = Names(context, marfu);
            var waqifun = Names(context, mawquf);
            var cmp = IsnadBranching.Compare(marfu, mawquf);

            var (severity, title, conclusion, flagged, confidence) = cmp switch
            {
                < 0 => (IllahSeverity.Qadihah, "رفع موقوف",
                    "ومن وقفه أرجح، فالصواب الوقف ورفعه خطأ.", marfu, 0.6),
                > 0 => (IllahSeverity.GhayrQadihah, "وقف لا يضر الرفع",
                    "ومن رفعه أرجح، فالرفع محفوظ ولا يضره وقف من وقفه.", mawquf, 0.5),
                _ => (IllahSeverity.Tanbih, "اختلاف في الرفع والوقف",
                    "والطرفان متقاربان، فيحتاج إلى نظر في القرائن.", marfu.Concat(mawquf).ToList(), 0.4)
            };

            yield return new IlalFindingDto
            {
                Type = IllahType.RafWaqf,
                Severity = severity,
                TitleAr = title,
                EvidenceAr = $"اختُلف على {madarName}: فرفعه {rafiun}، ووقفه {waqifun}. {conclusion}",
                NarratorIds = [split.MadarId, .. flagged.Select(b => b.StudentId)],
                HadithIds = flagged.SelectMany(b => b.Chains).Select(c => c.HadithId).Distinct().ToList(),
                Confidence = confidence
            };
        }
    }

    private static string Names(IlalContext context, IEnumerable<IsnadBranch> branches) =>
        string.Join(" و", branches.Select(b => context.NameOf(b.StudentId)));
}

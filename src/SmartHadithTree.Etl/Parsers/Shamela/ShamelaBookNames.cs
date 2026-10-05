namespace SmartHadithTree.Etl.Parsers.Shamela;

/// <summary>
/// The name each of the 31 books carries in <c>HadithText.BookName</c>, by folder slug. They are the names the app
/// already uses (the Ilal rules test for «صحيح البخاري» / «صحيح مسلم», the book list groups by name), and unlike
/// Shamela's own titles they are unique: «المصنف» is both Abd al-Razzaq's and Ibn Abi Shayba's, «السنن الكبرى»
/// both al-Bayhaqi's and al-Nasa'i's. Copied from the Itqan parser, which is removed in Phase 7.
/// </summary>
public static class ShamelaBookNames
{
    public static readonly IReadOnlyDictionary<string, string> BySlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["bukhari"] = "صحيح البخاري",
        ["muslim"] = "صحيح مسلم",
        ["abudawud"] = "سنن أبي داود",
        ["tirmidhi"] = "جامع الترمذي",
        ["nasai"] = "سنن النسائي",
        ["ibnmajah"] = "سنن ابن ماجه",
        ["ahmed"] = "مسند أحمد",
        ["malik"] = "موطأ مالك",
        ["darimi"] = "سنن الدارمي",
        ["aladab_almufrad"] = "الأدب المفرد",
        ["shamail_muhammadiyah"] = "الشمائل المحمدية",
        ["musannaf_ibnabi_shaybah"] = "مصنف ابن أبي شيبة",
        ["musannaf_abdurrazzaq"] = "مصنف عبد الرزاق",
        ["musnad_tayalisi"] = "مسند أبي داود الطيالسي",
        ["musnad_shafii"] = "مسند الشافعي",
        ["musnad_humaydi"] = "مسند الحميدي",
        ["sunan_said_ibn_mansur"] = "سنن سعيد بن منصور",
        ["musnad_ishaq"] = "مسند إسحاق بن راهويه",
        ["musnad_bazzar"] = "مسند البزار",
        ["sunan_kubra_nasai"] = "السنن الكبرى للنسائي",
        ["musnad_abi_yala"] = "مسند أبي يعلى الموصلي",
        ["sahih_ibn_khuzaymah"] = "صحيح ابن خزيمة",
        ["mustakhraj_abi_awanah"] = "مستخرج أبي عوانة",
        ["sahih_ibn_hibban"] = "صحيح ابن حبان",
        ["mujam_kabir_tabarani"] = "المعجم الكبير للطبراني",
        ["mujam_awsat_tabarani"] = "المعجم الأوسط للطبراني",
        ["mujam_saghir_tabarani"] = "المعجم الصغير للطبراني",
        ["sunan_daraqutni"] = "سنن الدارقطني",
        ["mustadrak_hakim"] = "المستدرك على الصحيحين",
        ["sunan_kubra_bayhaqi"] = "السنن الكبرى للبيهقي",
        ["shuab_iman_bayhaqi"] = "شعب الإيمان للبيهقي"
    };

    /// <summary>The app's name for the book, or Shamela's own title for a book outside the 31.</summary>
    public static string For(string slug, string shamelaTitle) =>
        BySlug.TryGetValue(slug, out var name) ? name : shamelaTitle;
}

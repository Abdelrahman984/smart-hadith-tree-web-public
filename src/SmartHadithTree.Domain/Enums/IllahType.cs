namespace SmartHadithTree.Domain.Enums;

/// <summary>أنواع العلل — categories of defects detected by the Ilal engine.</summary>
public enum IllahType
{
    /// <summary>تدليس — a mudallis narrating with an ambiguous formula (عن/أن/قال).</summary>
    Tadlis,
    /// <summary>اختلاط — narration from a narrator after their memory deteriorated.</summary>
    Ikhtilat,
    /// <summary>انقطاع خفي — no known meeting between sheikh and student.</summary>
    HiddenInqita,
    /// <summary>زيادة الثقة — an acceptable addition by a reliable narrator.</summary>
    Ziyadah,
    /// <summary>شذوذ — a reliable narrator contradicting more reliable or more numerous peers.</summary>
    Shudhudh,
    /// <summary>نكارة — a weak narrator contradicting reliable peers.</summary>
    Nakarah,
    /// <summary>اضطراب — irreconcilable conflict between branches of comparable strength.</summary>
    Idtirab,
    /// <summary>اختلاف في الرفع والوقف.</summary>
    RafWaqf,
    /// <summary>اختلاف في الوصل والإرسال.</summary>
    WaslIrsal
}

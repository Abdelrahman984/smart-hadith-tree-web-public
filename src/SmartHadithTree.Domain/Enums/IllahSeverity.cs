namespace SmartHadithTree.Domain.Enums;

/// <summary>Severity of a detected illah.</summary>
public enum IllahSeverity
{
    /// <summary>تنبيه — worth reviewing; not decisive on its own.</summary>
    Tanbih = 0,
    /// <summary>علة غير قادحة — a defect that does not invalidate the narration.</summary>
    GhayrQadihah = 1,
    /// <summary>علة قادحة — a defect that invalidates this route.</summary>
    Qadihah = 2
}

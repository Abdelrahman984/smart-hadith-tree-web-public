namespace SmartHadithTree.Domain.Enums;

/// <summary>How seriously the books rate a narrator's ikhtilat (الاختلاط).</summary>
public enum IkhtilatSeverity
{
    /// <summary>خفيف: slight; the books note it but it rarely harms his narrations.</summary>
    Light = 1,
    /// <summary>مختلف فيه: the critics disagree on whether he suffered ikhtilat or how much.</summary>
    Disputed = 2,
    /// <summary>فاحش: clear and harmful; narrations after it are rejected.</summary>
    Harmful = 3
}

namespace SmartHadithTree.Domain.Enums;

/// <summary>When a student heard from a mukhtalit narrator, relative to the ikhtilat.</summary>
public enum HearingTiming
{
    Unknown = 0,
    /// <summary>سمع منه قبل الاختلاط — accepted.</summary>
    Before = 1,
    /// <summary>سمع منه بعد الاختلاط — rejected.</summary>
    After = 2,
    /// <summary>The books say he heard both before and after the ikhtilat.</summary>
    Both = 3,
    /// <summary>The books contradict each other on the timing.</summary>
    Conflict = 4
}

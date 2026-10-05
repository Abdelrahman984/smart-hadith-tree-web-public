namespace SmartHadithTree.Application.DTOs;

public class NarratorSearchResultDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? KnownAs { get; set; }
    public string? GenerationTier { get; set; }
    public int? DeathYearHijri { get; set; }
}

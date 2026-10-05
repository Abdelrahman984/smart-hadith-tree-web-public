namespace SmartHadithTree.Domain.Entities;

public class HadithCluster
{
    public Guid Id { get; set; }
    public string GawamiClusterId { get; set; } = string.Empty;
    public string Taraf { get; set; } = string.Empty;
    public ICollection<HadithText> Hadiths { get; set; } = new List<HadithText>();
}

namespace SmartHadithTree.Application.Interfaces;

public interface IGawamiImporterService
{
    Task ImportNarratorsAsync(string dataDir, CancellationToken cancellationToken);
    Task ImportScholarEvaluationsAsync(string dataDir, CancellationToken cancellationToken);
    Task ImportIsnadJudgmentsAsync(string dataDir, CancellationToken cancellationToken);
}

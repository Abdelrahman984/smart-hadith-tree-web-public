using SmartHadithTree.Application.DTOs;

namespace SmartHadithTree.Application.Interfaces;

public interface ITaqwiyahService
{
    void CalculateTreeStrength(ComparativeTreeResponseDto tree);
}

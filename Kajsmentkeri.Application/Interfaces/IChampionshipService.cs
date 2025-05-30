using Kajsmentkeri.Domain;

namespace Kajsmentkeri.Application.Interfaces;

public interface IChampionshipService
{
    Task<List<Championship>> GetAllAsync();
    Task<Championship> GetByIdAsync(Guid id);
    Task<Championship> CreateChampionshipAsync(string name, int year, string? description);
    Task<Championship?> GetLatestAsync();
}

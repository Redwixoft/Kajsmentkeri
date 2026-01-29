using Kajsmentkeri.Domain;

namespace Kajsmentkeri.Application.Interfaces;

public interface IChampionshipService
{
    Task<List<Championship>> GetAllAsync();
    Task<Championship> GetByIdAsync(Guid id);
    Task<Championship> CreateChampionshipAsync(Championship championship);
    Task<Championship> CreateChampionshipAsync(string name, int year, string? description, bool isTest = false);
    Task<Championship?> GetLatestAsync();
    Task UpdateChampionshipAsync(Championship championship);
    Task DeleteChampionshipAsync(Guid id);
    Task EndChampionshipAsync(Guid championshipId);
    Task UpdateWinnerPaymentInfoAsync(Guid championshipId, string iban, string note);
}

using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Application.Services;

public class PercentagePredictionService : IPercentagePredictionService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public PercentagePredictionService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<bool> HasUserSubmittedAsync(Guid userId)
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.PercentagePredictions.AnyAsync(p => p.UserId == userId);
    }

    public async Task SubmitPredictionsAsync(Guid userId, PercentagePredictionDto dto)
    {
        using var context = _dbContextFactory.CreateDbContext();

        if (await context.PercentagePredictions.AnyAsync(p => p.UserId == userId))
        {
            throw new InvalidOperationException("Predictions already submitted.");
        }

        var prediction = new PercentagePrediction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            Question1 = dto.Question1,
            Question2 = dto.Question2,
            Question3 = dto.Question3,
            Question4 = dto.Question4,
            Question5 = dto.Question5,
            Question6 = dto.Question6,
            Question7 = dto.Question7,
            Question8 = dto.Question8,
            Question9 = dto.Question9,
            Question10 = dto.Question10
        };

        context.PercentagePredictions.Add(prediction);
        await context.SaveChangesAsync();
    }
}

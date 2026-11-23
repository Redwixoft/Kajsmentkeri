using Kajsmentkeri.Application.DTOs;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Application.Services;

public class PercentagePredictionService : IPercentagePredictionService
{
    private const int QuestionCount = IPercentagePredictionService.QuestionCount;

    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly UserManager<AppUser> _userManager;

    public PercentagePredictionService(IDbContextFactory<AppDbContext> dbContextFactory, UserManager<AppUser> userManager)
    {
        _dbContextFactory = dbContextFactory;
        _userManager = userManager;
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

    public async Task<PercentageRecallStatusDto> GetRecallStatusAsync(Guid userId)
    {
        using var context = _dbContextFactory.CreateDbContext();

        var hasOriginal = await context.PercentagePredictions.AnyAsync(p => p.UserId == userId);
        var hasRecall = await context.PercentagePredictionRecalls.AnyAsync(r => r.UserId == userId);
        var revealed = await IsRevealedAsync(context);

        return new PercentageRecallStatusDto(hasOriginal, hasRecall, revealed);
    }

    public async Task SubmitRecallAsync(Guid userId, IReadOnlyList<int> answers)
    {
        if (answers.Count != QuestionCount)
            throw new InvalidOperationException($"Expected {QuestionCount} answers.");
        if (answers.Any(a => a < 0 || a > 100))
            throw new InvalidOperationException("Each answer must be between 0 and 100.");

        using var context = _dbContextFactory.CreateDbContext();

        if (!await context.PercentagePredictions.AnyAsync(p => p.UserId == userId))
            throw new InvalidOperationException("You did not submit predictions last year, so there is nothing to recall.");

        if (await context.PercentagePredictionRecalls.AnyAsync(r => r.UserId == userId))
            throw new InvalidOperationException("You have already submitted your answers.");

        context.PercentagePredictionRecalls.Add(new PercentagePredictionRecall
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            Question1 = answers[0],
            Question2 = answers[1],
            Question3 = answers[2],
            Question4 = answers[3],
            Question5 = answers[4],
            Question6 = answers[5],
            Question7 = answers[6],
            Question8 = answers[7],
            Question9 = answers[8],
            Question10 = answers[9]
        });
        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyDictionary<int, bool?>> GetOutcomesAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await LoadOutcomesAsync(context);
    }

    public async Task SetOutcomesAsync(IReadOnlyDictionary<int, bool?> outcomes)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var existing = await context.PercentagePredictionOutcomes.ToDictionaryAsync(o => o.QuestionNumber);
        var now = DateTime.UtcNow;

        foreach (var (questionNumber, happened) in outcomes)
        {
            if (questionNumber < 1 || questionNumber > QuestionCount)
                continue;

            if (existing.TryGetValue(questionNumber, out var row))
            {
                if (row.Happened != happened)
                {
                    row.Happened = happened;
                    row.UpdatedAtUtc = now;
                }
            }
            else
            {
                context.PercentagePredictionOutcomes.Add(new PercentagePredictionOutcome
                {
                    QuestionNumber = questionNumber,
                    Happened = happened,
                    UpdatedAtUtc = now
                });
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task<PercentagePredictionConclusionDto> GetConclusionAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();

        var originals = await context.PercentagePredictions.AsNoTracking().ToListAsync();
        var recalls = await context.PercentagePredictionRecalls.AsNoTracking().ToDictionaryAsync(r => r.UserId);

        var userIds = originals.Select(o => o.UserId).ToList();
        var userNames = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName ?? "?");
        string NameOf(Guid id) => userNames.GetValueOrDefault(id, "Unknown");

        var pending = originals
            .Where(o => !recalls.ContainsKey(o.UserId))
            .Select(o => NameOf(o.UserId))
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var isRevealed = originals.Count > 0 && pending.Count == 0;

        if (!isRevealed)
        {
            return new PercentagePredictionConclusionDto
            {
                IsRevealed = false,
                TotalParticipants = originals.Count,
                SubmittedRecalls = originals.Count - pending.Count,
                PendingUserNames = pending
            };
        }

        var outcomes = await LoadOutcomesAsync(context);

        // (userName, originalAnswers, recalledAnswers) for every participant
        var participants = originals
            .Select(o => (Name: NameOf(o.UserId), Original: AnswersOf(o), Recalled: AnswersOf(recalls[o.UserId])))
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var questions = new List<PercentageQuestionResultDto>();
        for (var q = 1; q <= QuestionCount; q++)
        {
            var happened = outcomes[q];
            var answers = participants
                .Select(p =>
                {
                    var original = p.Original[q - 1];
                    var recalled = p.Recalled[q - 1];
                    return new PercentageUserAnswerDto(
                        p.Name,
                        original,
                        recalled,
                        recalled - original,
                        happened is bool h ? Score(original, h) : null);
                })
                .ToList();
            questions.Add(new PercentageQuestionResultDto(q, happened, answers));
        }

        var totals = participants
            .Select(p => new
            {
                p.Name,
                Points = Enumerable.Range(1, QuestionCount)
                    .Sum(q => outcomes[q] is bool h ? Score(p.Original[q - 1], h) : 0),
                AbsDiff = Enumerable.Range(0, QuestionCount)
                    .Sum(i => Math.Abs(p.Recalled[i] - p.Original[i]))
            })
            .OrderByDescending(t => t.Points)
            .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // Standard competition ranking: ties share a rank, next rank skips (1, 2, 2, 4)
        var ranking = new List<PercentagePredictorRankDto>();
        for (var i = 0; i < totals.Count; i++)
        {
            var rank = i > 0 && totals[i].Points == totals[i - 1].Points ? ranking[i - 1].Rank : i + 1;
            ranking.Add(new PercentagePredictorRankDto(rank, totals[i].Name, totals[i].Points, totals[i].AbsDiff));
        }

        return new PercentagePredictionConclusionDto
        {
            IsRevealed = true,
            TotalParticipants = originals.Count,
            SubmittedRecalls = originals.Count,
            KnownOutcomeCount = outcomes.Values.Count(v => v.HasValue),
            Ranking = ranking,
            Questions = questions
        };
    }

    /// <summary>
    /// Points = how close the guess was to reality: the guessed % if the event happened, 100 − guess if it didn't.
    /// </summary>
    private static int Score(int guess, bool happened) => happened ? guess : 100 - guess;

    private static async Task<bool> IsRevealedAsync(AppDbContext context)
    {
        // Revealed once every user with an original prediction has also submitted a recall.
        var anyOriginal = await context.PercentagePredictions.AnyAsync();
        if (!anyOriginal)
            return false;

        return !await context.PercentagePredictions
            .AnyAsync(p => !context.PercentagePredictionRecalls.Any(r => r.UserId == p.UserId));
    }

    private static async Task<Dictionary<int, bool?>> LoadOutcomesAsync(AppDbContext context)
    {
        var stored = await context.PercentagePredictionOutcomes.AsNoTracking()
            .ToDictionaryAsync(o => o.QuestionNumber, o => o.Happened);

        return Enumerable.Range(1, QuestionCount)
            .ToDictionary(q => q, q => stored.GetValueOrDefault(q));
    }

    private static int[] AnswersOf(PercentagePrediction p) =>
        [p.Question1, p.Question2, p.Question3, p.Question4, p.Question5,
         p.Question6, p.Question7, p.Question8, p.Question9, p.Question10];

    private static int[] AnswersOf(PercentagePredictionRecall r) =>
        [r.Question1, r.Question2, r.Question3, r.Question4, r.Question5,
         r.Question6, r.Question7, r.Question8, r.Question9, r.Question10];
}

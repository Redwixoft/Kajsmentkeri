using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Kajsmentkeri.Application.Services;

public class SecretSantaService : ISecretSantaService
{
    // Arbitrary constant key for the Postgres advisory lock that serializes rule assignment.
    private const long AssignmentLockKey = 0x5A47A5A;

    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IConfiguration _configuration;

    public SecretSantaService(IDbContextFactory<AppDbContext> dbContextFactory, IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
    }

    private int RulesPerUser => _configuration.GetValue("SecretSanta:RulesPerUser", 4);

    private List<string> RulePool =>
        (_configuration.GetSection("SecretSanta:Rules").Get<string[]>() ?? Array.Empty<string>())
            .Select(r => r.Trim())
            .Where(r => r.Length > 0)
            .Distinct()
            .ToList();

    public async Task<IReadOnlyList<string>> GetUserRulesAsync(Guid userId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.SecretSantaRuleAssignments
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.AssignedAtUtc).ThenBy(a => a.Id)
            .Select(a => a.RuleText)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<string>> AssignRulesAsync(Guid userId, string nickname)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var tx = await context.Database.BeginTransactionAsync();

            // Serialize all assignments so two users clicking at once can't draw the same rule,
            // and one user double-clicking can't get two sets.
            await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", AssignmentLockKey);

            var existing = await context.SecretSantaRuleAssignments
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.AssignedAtUtc).ThenBy(a => a.Id)
                .Select(a => a.RuleText)
                .ToListAsync();
            if (existing.Count > 0)
            {
                await tx.CommitAsync();
                return (IReadOnlyList<string>)existing;
            }

            var taken = (await context.SecretSantaRuleAssignments
                .Select(a => a.RuleText)
                .ToListAsync()).ToHashSet();

            var available = RulePool.Where(r => !taken.Contains(r)).ToList();
            var count = RulesPerUser;
            if (available.Count < count)
                throw new InvalidOperationException("There are not enough unassigned rules left. Please contact the administrator.");

            var picked = available.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
            var now = DateTime.UtcNow;
            foreach (var rule in picked)
            {
                context.SecretSantaRuleAssignments.Add(new SecretSantaRuleAssignment
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    UserNickname = nickname,
                    RuleText = rule,
                    AssignedAtUtc = now
                });
            }

            await context.SaveChangesAsync();
            await tx.CommitAsync();
            return (IReadOnlyList<string>)picked;
        });
    }
}

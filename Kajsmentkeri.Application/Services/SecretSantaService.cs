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

    // Each inner list is one group; a user gets exactly one rule from every group.
    private List<List<string>> RuleGroups =>
        _configuration.GetSection("SecretSanta:RuleGroups").GetChildren()
            .Select(g => (g.Get<string[]>() ?? Array.Empty<string>())
                .Select(r => r.Trim())
                .Where(r => r.Length > 0)
                .Distinct()
                .ToList())
            .Where(g => g.Count > 0)
            .ToList();

    // Show rules in group order (colour, property, letter, extra) regardless of storage order.
    private List<string> OrderByGroup(IEnumerable<string> rules)
    {
        var groups = RuleGroups;
        int GroupIndex(string rule)
        {
            var i = groups.FindIndex(g => g.Contains(rule));
            return i < 0 ? int.MaxValue : i;
        }
        return rules.OrderBy(GroupIndex).ToList();
    }

    public async Task<IReadOnlyList<string>> GetUserRulesAsync(Guid userId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var rules = await context.SecretSantaRuleAssignments
            .Where(a => a.UserId == userId)
            .Select(a => a.RuleText)
            .ToListAsync();
        return OrderByGroup(rules);
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
                .Select(a => a.RuleText)
                .ToListAsync();
            if (existing.Count > 0)
            {
                await tx.CommitAsync();
                return (IReadOnlyList<string>)OrderByGroup(existing);
            }

            var taken = (await context.SecretSantaRuleAssignments
                .Select(a => a.RuleText)
                .ToListAsync()).ToHashSet();

            var groups = RuleGroups;
            if (groups.Count == 0)
                throw new InvalidOperationException("No Secret Santa rules are configured. Please contact the administrator.");

            var picked = new List<string>();
            foreach (var group in groups)
            {
                var available = group.Where(r => !taken.Contains(r)).ToList();
                if (available.Count == 0)
                    throw new InvalidOperationException("There are no unassigned rules left. Please contact the administrator.");
                picked.Add(available[Random.Shared.Next(available.Count)]);
            }
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

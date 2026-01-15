using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Application.Services;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        return;
        var services = new ServiceCollection();
        // Constant connection string from appsettings.json
        string connectionString = "";

        services.AddLogging(configure => configure.AddConsole());
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IPredictionScoringService, PredictionScoringService>();
        services.AddScoped<IPredictionService, PredictionService>();
        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddScoped<IChampionshipService, ChampionshipService>();
        services.AddScoped<ICurrentUserService, SystemUserService>();

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var champService = scope.ServiceProvider.GetRequiredService<IChampionshipService>();
        var matchService = scope.ServiceProvider.GetRequiredService<IMatchService>();
        var identityDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // 1. Find an admin user
        var adminUser = await identityDb.Users.FirstOrDefaultAsync(u => u.IsAdmin) 
                     ?? await identityDb.Users.FirstOrDefaultAsync();

        if (adminUser == null)
        {
            Console.WriteLine("No users found in database. Please register a user first.");
            return;
        }

        Console.WriteLine($"Using user: {adminUser.UserName} ({adminUser.Id}) as creator.");

        // 2. Create Championship
        var champName = "Winter Olympic Games";
        var champYear = 2026;
        
        var currentUserService = (SystemUserService)scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
        currentUserService.SetUser(adminUser);

        Championship championship;
        try 
        {
            championship = await champService.CreateChampionshipAsync(champName, champYear, "Ice Hockey - Men's Tournament");
            Console.WriteLine($"Created championship: {championship.Name} ({championship.Id})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating championship: {ex.Message}");
            // Try to find if it exists
            championship = (await champService.GetAllAsync()).FirstOrDefault(c => c.Name == champName && c.Year == champYear);
            if (championship == null) 
            {
                Console.WriteLine("Failed to find or create championship.");
                return;
            }
        }

        // 3. Add Matches
        var matches = new List<(string Home, string Away, DateTime Time)>
        {
            // Day 1: Feb 11
            ("Slovakia", "Finland", new DateTime(2026, 2, 11, 16, 40, 0)),
            ("Italy", "Sweden", new DateTime(2026, 2, 11, 21, 10, 0)),
            // Day 2: Feb 12
            ("Switzerland", "France", new DateTime(2026, 2, 12, 12, 10, 0)),
            ("Canada", "Czechia", new DateTime(2026, 2, 12, 16, 40, 0)),
            ("Germany", "Denmark", new DateTime(2026, 2, 12, 21, 10, 0)),
            ("Latvia", "United States", new DateTime(2026, 2, 12, 21, 10, 0)),
            // Day 3: Feb 13
            ("Finland", "Sweden", new DateTime(2026, 2, 13, 12, 10, 0)),
            ("Italy", "Slovakia", new DateTime(2026, 2, 13, 12, 10, 0)),
            ("France", "Czechia", new DateTime(2026, 2, 13, 16, 40, 0)),
            ("Canada", "Switzerland", new DateTime(2026, 2, 13, 21, 10, 0)),
            // Day 4: Feb 14
            ("Sweden", "Slovakia", new DateTime(2026, 2, 14, 12, 10, 0)),
            ("Germany", "Latvia", new DateTime(2026, 2, 14, 12, 10, 0)),
            ("Finland", "Italy", new DateTime(2026, 2, 14, 16, 40, 0)),
            ("United States", "Denmark", new DateTime(2026, 2, 14, 21, 10, 0)),
            // Day 5: Feb 15
            ("Switzerland", "Czechia", new DateTime(2026, 2, 15, 12, 10, 0)),
            ("Canada", "France", new DateTime(2026, 2, 15, 16, 40, 0)),
            ("United States", "Germany", new DateTime(2026, 2, 15, 21, 10, 0)),
            ("Denmark", "Latvia", new DateTime(2026, 2, 15, 21, 10, 0))
        };

        foreach (var m in matches)
        {
            try 
            {
                // CET is UTC+1. Schedule matches in UTC.
                var utcTime = m.Time.AddHours(1); 
                await matchService.CreateMatchAsync(championship.Id, m.Home, m.Away, utcTime);
                Console.WriteLine($"Added match: {m.Home} vs {m.Away} at {m.Time} (CET)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding match {m.Home} vs {m.Away}: {ex.Message}");
            }
        }

        Console.WriteLine("Import completed successfully!");
    }
}

public class SystemUserService : ICurrentUserService
{
    private AppUser? _user;
    public Guid? UserId => _user?.Id;
    public string? UserName => _user?.UserName;
    public bool IsAuthenticated => _user != null;
    public bool IsAdmin => _user?.IsAdmin ?? false;
    public void SetUser(AppUser user) => _user = user;
}

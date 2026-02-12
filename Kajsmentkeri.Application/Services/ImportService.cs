using ExcelDataReader;
using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Text.RegularExpressions;

namespace Kajsmentkeri.Application.Services;

public class ImportService : IImportService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IChampionshipService _championshipService;
    private readonly IMatchService _matchService;
    private readonly IPredictionService _predictionService;
    private readonly IPredictionScoringService _scoringService;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public ImportService(
        UserManager<AppUser> userManager,
        IChampionshipService championshipService,
        IMatchService matchService,
        IPredictionService predictionService,
        IPredictionScoringService scoringService,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _userManager = userManager;
        _championshipService = championshipService;
        _matchService = matchService;
        _predictionService = predictionService;
        _scoringService = scoringService;
        _configuration = configuration;
    }

    public async Task<ImportAnalysisResult> AnalyzeImportAsync(Stream fileStream)
    {
        var result = new ImportAnalysisResult();
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        result.FileContent = memoryStream.ToArray();
        memoryStream.Position = 0;

        using var reader = ExcelReaderFactory.CreateReader(memoryStream);
        var conf = new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = false 
            }
        };

        var dataSet = reader.AsDataSet(conf);
        var table = dataSet.Tables[0];

        // 1. Analyze Users (Headers in Row 0)
        // Expected: Match | Date | Result | User1 | User2 ...
        var headerRow = table.Rows[0];
        // Start from column 3 (index 3, 4th column)
        for (int i = 3; i < table.Columns.Count; i++)
        {
            var headerValue = headerRow[i]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(headerValue))
            {
                // Try to find user
                var user = await _userManager.FindByNameAsync(headerValue); 
                result.UserMappings.Add(new UserMapping
                {
                    ExcelName = headerValue,
                    MappedUserId = user?.Id
                });
            }
        }

        // 2. Analyze Matches and Teams
        var teams = new HashSet<string>();
        string lastDate = string.Empty;

        for (int i = 1; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            var matchStr = row[0]?.ToString()?.Trim();
            var dateStr = row[1]?.ToString()?.Trim();

            if (string.IsNullOrEmpty(matchStr)) continue;

            if (!string.IsNullOrEmpty(dateStr))
            {
                lastDate = dateStr;
            }

            // Parse match "Home - Away"
            var parts = matchStr.Split(new[] { " - ", "-" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var home = parts[0].Trim();
                var away = parts[1].Trim();
                teams.Add(home);
                teams.Add(away);

                result.PreviewMatches.Add($"{lastDate}: {home} vs {away}");
            }
        }

        var staticMappings = _configuration.GetSection("CountryMappings").Get<Dictionary<string, string>>() ?? new();

        foreach (var team in teams)
        {
            if (staticMappings.TryGetValue(team, out var mappedName))
            {
                // Skip if already mapped statically
                continue;
            }
            result.TeamMappings.Add(new TeamMapping { Code = team, MappedName = team });
        }

        return result;
    }

    public async Task ImportChampionshipAsync(ImportData data)
    {
        using var memoryStream = new MemoryStream(data.FileContent);
        using var reader = ExcelReaderFactory.CreateReader(memoryStream);
        var conf = new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = false
            }
        };

        var dataSet = reader.AsDataSet(conf);
        var table = dataSet.Tables[0];

        // 1. Create Championship
        var champ = await _championshipService.CreateChampionshipAsync(data.ChampionshipName, data.Year, data.Description ?? "Imported Championship");
        
        // Add static mappings to the data.TeamMap for the import process
        var staticMappings = _configuration.GetSection("CountryMappings").Get<Dictionary<string, string>>() ?? new();
        foreach (var mapping in staticMappings)
        {
            if (!data.TeamMap.ContainsKey(mapping.Key))
            {
                data.TeamMap[mapping.Key] = mapping.Value;
            }
        }
        
        // 2. Process Matches and Predictions
        string lastDate = string.Empty;
        var headerRow = table.Rows[0];
        
        // Map column index to UserId
        var columnUserMap = new Dictionary<int, Guid>();
        for (int i = 3; i < table.Columns.Count; i++)
        {
            var headerName = headerRow[i]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(headerName) && data.UserMap.ContainsKey(headerName))
            {
                columnUserMap[i] = data.UserMap[headerName];
            }
        }

        string lastDateStr = string.Empty;
        int matchCountToday = 0;

        for (int i = 1; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            var matchStr = row[0]?.ToString()?.Trim();

            if (string.IsNullOrEmpty(matchStr)) continue;

            var dateObj = row[1];
            if (dateObj != null && dateObj != DBNull.Value)
            {
                string currentDateStr = string.Empty;
                if (dateObj is DateTime dt)
                {
                    currentDateStr = dt.ToString("yyyy-MM-dd");
                }
                else if (dateObj is double d)
                {
                    currentDateStr = DateTime.FromOADate(d).ToString("yyyy-MM-dd");
                }
                else
                {
                    var s = dateObj.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(s))
                    {
                        currentDateStr = s;
                    }
                }

                if (!string.IsNullOrEmpty(currentDateStr))
                {
                    if (currentDateStr != lastDateStr)
                    {
                        lastDateStr = currentDateStr;
                        matchCountToday = 0;
                    }
                    else
                    {
                        // Same day as before, increment count is handled below
                    }
                }
            }

            var parts = matchStr.Split(new[] { " - ", "-" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) continue;

            var homeCode = parts[0].Trim();
            var awayCode = parts[1].Trim();

            var homeName = data.TeamMap.ContainsKey(homeCode) ? data.TeamMap[homeCode] : homeCode;
            var awayName = data.TeamMap.ContainsKey(awayCode) ? data.TeamMap[awayCode] : awayCode;

            // Parse Date
            DateTime parsedDate;
            if (!DateTime.TryParse(lastDateStr, out parsedDate))
            {
                parsedDate = DateTime.UtcNow; // Fallback
            }

            // Ensure year from championship is used
            var matchDate = new DateTime(data.Year, parsedDate.Month, parsedDate.Day);

            // Starting at 16:00, increment by 1 hour for each match on the same day
            var matchTime = matchDate.AddHours(16 + matchCountToday);
            matchCountToday++;

            // 1. Create Match (without result)
            var match = await _matchService.CreateMatchAsync(champ.Id, homeName, awayName, matchTime.ToUniversalTime());

            // 2. Process Predictions (Must be before result for leaderboard logic)
            foreach (var kvp in columnUserMap)
            {
                var colIndex = kvp.Key;
                var userId = kvp.Value;
                var predStr = row[colIndex]?.ToString()?.Trim();

                if (!string.IsNullOrEmpty(predStr))
                {
                     var scores = predStr.Split(new[] { ":", "-" }, StringSplitOptions.RemoveEmptyEntries);
                     if (scores.Length == 2 && int.TryParse(scores[0], out int hScore) && int.TryParse(scores[1], out int aScore))
                     {
                         await _predictionService.CreateOrUpdatePredictionAsync(match.Id, userId, hScore, aScore);
                     }
                }
            }

            // 3. Process Result if present (Results added LAST)
            var resultStr = row[2]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(resultStr))
            {
                var scores = resultStr.Split(new[] { ":", "-" }, StringSplitOptions.RemoveEmptyEntries);
                if (scores.Length == 2 && int.TryParse(scores[0], out int hScore) && int.TryParse(scores[1], out int aScore))
                {
                    await _matchService.UpdateMatchResultAsync(match.Id, hScore, aScore);
                    await _scoringService.RecalculateForMatchAsync(match.Id);
                }
            }
        }
    }
}

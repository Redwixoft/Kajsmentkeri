using System.IO;
using Kajsmentkeri.Domain;

namespace Kajsmentkeri.Application.Interfaces;

public class ImportAnalysisResult
{
    public List<UserMapping> UserMappings { get; set; } = new();
    public List<TeamMapping> TeamMappings { get; set; } = new();
    public List<string> PreviewMatches { get; set; } = new();
    public byte[] FileContent { get; set; } = Array.Empty<byte>();
}

public class UserMapping
{
    public string ExcelName { get; set; } = string.Empty;
    public Guid? MappedUserId { get; set; }
}

public class TeamMapping
{
    public string Code { get; set; } = string.Empty;
    public string MappedName { get; set; } = string.Empty;
}

public class ImportData
{
    public byte[] FileContent { get; set; } = Array.Empty<byte>();
    public Dictionary<string, Guid> UserMap { get; set; } = new();
    public Dictionary<string, string> TeamMap { get; set; } = new();
    public string ChampionshipName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? Description { get; set; }
    public ChampionshipType Type { get; set; } = ChampionshipType.IceHockey;
    public int PointsForCorrectWinner { get; set; } = 3;
    public int PointsForExactScore { get; set; } = 2;
    public int PointsForOnlyCorrectWinner { get; set; } = 2;
    public decimal RarityPointsBonus { get; set; } = 0;
    public int PointsForChampionshipWinner { get; set; } = 3;
    public int PointsForChampionshipRunnerUp { get; set; } = 2;
    public int PointsForChampionshipThirdPlace { get; set; } = 1;
}

public interface IImportService
{
    Task<ImportAnalysisResult> AnalyzeImportAsync(Stream fileStream);
    Task ImportChampionshipAsync(ImportData data);
}

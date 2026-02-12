using System.IO;

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
}

public interface IImportService
{
    Task<ImportAnalysisResult> AnalyzeImportAsync(Stream fileStream);
    Task ImportChampionshipAsync(ImportData data);
}

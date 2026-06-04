namespace Harem.Contracts.Dtos;

/// <summary>
/// 关卡列表项DTO — 前端只关心这些通用字段
/// </summary>
public class LevelListDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string GameType { get; set; } = "narrative";
    public string Status { get; set; } = "not_started";
}

/// <summary>
/// 游戏报告DTO
/// </summary>
public class GameReportDto
{
    public string LevelId { get; set; } = "";
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string Result { get; set; } = "";
    public List<AuditLogDto> AuditLog { get; set; } = new();
    public List<string> SelectedOptions { get; set; } = new();
    public ReportMetricsDto Metrics { get; set; } = new();
}

/// <summary>
/// 审计日志DTO
/// </summary>
public class AuditLogDto
{
    public string Timestamp { get; set; } = "";
    public string NodeId { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
}

/// <summary>
/// 报告统计DTO
/// </summary>
public class ReportMetricsDto
{
    public int TotalDurationS { get; set; }
    public List<string> BranchPath { get; set; } = new();
}

/// <summary>
/// 进度更新请求DTO
/// </summary>
public class ProgressUpdateDto
{
    public string LevelId { get; set; } = "";
    public string NodeId { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
}

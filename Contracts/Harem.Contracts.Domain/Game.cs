namespace Harem.Contracts.Domain;

/// <summary>
/// 游戏状态枚举
/// </summary>
public enum GameStatus
{
    None,
    Pending,
    InProgress,
    Completed,
    Failed
}

/// <summary>
/// 游戏会话状态（按 SessionKey 区分）
/// </summary>
public class GameState
{
    public string SessionKey { get; set; } = "";
    public string? CurrentLevelId { get; set; }
    public string? CurrentNodeId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? LastActiveTime { get; set; }
    public GameStatus Status { get; set; } = GameStatus.None;
    public List<string> CompletedLevels { get; set; } = new();
    public Dictionary<string, LevelProgress> LevelProgresses { get; set; } = new();
}

/// <summary>
/// 关卡进度
/// </summary>
public class LevelProgress
{
    public int ProgressPercent { get; set; }
    public List<string> VisitedNodes { get; set; } = new();
    public List<string> SelectedOptions { get; set; } = new();
    public string? Result { get; set; }
    public DateTime? CompletedTime { get; set; }
    public List<GameAuditLog> AuditLog { get; set; } = new();
}

/// <summary>
/// 审计日志
/// </summary>
public class GameAuditLog
{
    public DateTime Timestamp { get; set; }
    public string NodeId { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
}

namespace Harem.Contracts.Domain.Entities;

public class HistoryMessage
{
    public HistoryRole Role { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public enum HistoryRole
{
    User,
    Assistant,
}
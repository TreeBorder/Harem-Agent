namespace Harem.Contracts.Domain.Entities;

public class ChatRawMessage<T>
{
    public long RoundId { get; set; }
    public List<T> Request { get; set; } = [];
    public List<T> Response { get; set; } = [];
}
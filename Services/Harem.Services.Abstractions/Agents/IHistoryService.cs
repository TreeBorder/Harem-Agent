
using Harem.Contracts.Domain.Entities;
using Microsoft.Extensions.AI;

namespace Harem.Services.Abstractions.Agents;

public interface IHistoryService
{
    Task<ChatHistory> GetHistoryAsync(string sessionId);
    Task SaveHistoryAsync(string sessionId, ChatHistory history);

    /// <summary>
    /// 保存本轮交互到历史记录（不含摘要，摘要由 SummaryService 独立管理）
    /// </summary>
    Task SaveHistoryAsync(string sessionId, ChatHistory history, ChatMessage inputMessage,
        string content);

    Task SetHistoryBag<T>(string sessionId, string key, T content);
    Task<T?> GetHistoryBag<T>(string sessionId, string key);
}

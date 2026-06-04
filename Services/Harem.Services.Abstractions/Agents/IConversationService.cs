using Harem.Contracts.Domain.Messages;

namespace Harem.Services.Abstractions.Agents;

public interface IConversationService
{
    Task ProcessMessageAsync(ReceivedMessage message, CancellationToken ct = default);
}
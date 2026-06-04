namespace Harem.Contracts.Domain.Messages;

public record ReceivedMessage(string SessionKey, string ChannelId, string Message);
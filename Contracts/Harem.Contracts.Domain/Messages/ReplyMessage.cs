namespace Harem.Contracts.Domain.Messages;

public record ReplyMessage(string SessionKey, string Message, List<string>? Files = null, bool IsTyping = false);
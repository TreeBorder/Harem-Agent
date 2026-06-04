using System.Threading.Channels;
using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Channels;

namespace Harem.Services.Channels;

public class ChatReplyChannel : IMessageChannel<ReplyMessage>
{
    private readonly Channel<ReplyMessage> _channel = Channel.CreateUnbounded<ReplyMessage>();

    public ValueTask WriteAsync(ReplyMessage message)
        => _channel.Writer.WriteAsync(message);

    public ValueTask WriteAsync(ReplyMessage messages, CancellationToken ct)
        => _channel.Writer.WriteAsync(messages, ct);

    public IAsyncEnumerable<ReplyMessage> ReadAllAsync()
        => _channel.Reader.ReadAllAsync();

    public IAsyncEnumerable<ReplyMessage> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
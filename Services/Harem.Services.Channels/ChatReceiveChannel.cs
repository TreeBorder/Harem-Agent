using System.Threading.Channels;
using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Channels;

namespace Harem.Services.Channels;

public class ChatReceiveChannel : IMessageChannel<ReceivedMessage>
{
    private readonly Channel<ReceivedMessage> _channel = Channel.CreateUnbounded<ReceivedMessage>();

    public ValueTask WriteAsync(ReceivedMessage message)
        => _channel.Writer.WriteAsync(message);


    public ValueTask WriteAsync(ReceivedMessage messages, CancellationToken ct)
        => _channel.Writer.WriteAsync(messages, ct);

    public IAsyncEnumerable<ReceivedMessage> ReadAllAsync()
        => _channel.Reader.ReadAllAsync();

    public IAsyncEnumerable<ReceivedMessage> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
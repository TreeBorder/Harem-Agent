using System.Threading.Channels;
using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Channels;

namespace Harem.Services.Channels;

public class SystemMessageChannel : IMessageChannel<SystemMessage>
{
    private readonly Channel<SystemMessage> _channel = Channel.CreateUnbounded<SystemMessage>();

    public ValueTask WriteAsync(SystemMessage message)
        => _channel.Writer.WriteAsync(message);

    public ValueTask WriteAsync(SystemMessage messages, CancellationToken ct)
        => _channel.Writer.WriteAsync(messages, ct);

    public IAsyncEnumerable<SystemMessage> ReadAllAsync()
        => _channel.Reader.ReadAllAsync();

    public IAsyncEnumerable<SystemMessage> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
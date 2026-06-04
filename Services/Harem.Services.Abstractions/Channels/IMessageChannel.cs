namespace Harem.Services.Abstractions.Channels;

public interface IMessageChannel<T>
{
    ValueTask WriteAsync(T message);
    ValueTask WriteAsync(T messages, CancellationToken ct);
    IAsyncEnumerable<T> ReadAllAsync();
    IAsyncEnumerable<T> ReadAllAsync(CancellationToken ct);
}
using Harem.Contracts.Domain.Entities;

namespace Harem.Services.Abstractions.Handlers;

public interface ICommandHandler
{
    string Command { get; }
    Task<CommandResult> HandleAsync(string args, string sessionKey);
}
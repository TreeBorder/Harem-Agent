using Harem.Services.Abstractions.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Harem.Services.Handlers;

public static class ServiceExtensions
{
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.AddKeyedSingleton<ICommandHandler, HelpHandler>(HelpHandler.CommandName);
        services.AddKeyedSingleton<ICommandHandler, StateHandler>(StateHandler.CommandName);
        services.AddKeyedSingleton<ICommandHandler, SessionHandler>(SessionHandler.CommandName);
        services.AddKeyedSingleton<ICommandHandler, PhotoHandler>(PhotoHandler.CommandName);
        services.AddKeyedSingleton<ICommandHandler, NpcHandler>(NpcHandler.CommandName);
        services.AddKeyedSingleton<ICommandHandler, MemoryHandler>(MemoryHandler.CommandName);
        services.AddKeyedSingleton<ICommandHandler, HeartbeatHandler>(HeartbeatHandler.CommandName);
        services.AddKeyedSingleton<ICommandHandler, ModelHandler>(ModelHandler.CommandName);
        services.AddKeyedSingleton<ICommandHandler, FixHandler>(FixHandler.CommandName);
        services.AddKeyedSingleton<ICommandHandler, CronHandler>(CronHandler.CommandName);
        services.AddKeyedSingleton<ICommandHandler, SummaryHandler>(SummaryHandler.CommandName);
        services.AddKeyedSingleton<ICommandHandler, KeywordHandler>(KeywordHandler.CommandName);
        return services;
    }
}
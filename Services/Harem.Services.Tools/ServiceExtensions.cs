using Harem.Services.Abstractions.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Harem.Services.Tools;

public static class ServiceExtensions
{
    public static IServiceCollection AddRpgTools(this IServiceCollection services)
    {
        services.AddSingleton<IHelpTool, HelpTool>();
        services.AddSingleton<IStateTool, StateTool>();
        services.AddSingleton<IFileTool, FileTool>();
        services.AddSingleton<ICharacterImageTool, CharacterImageTool>();
        services.AddSingleton<IMemoryTool, MemoryTool>();
        services.AddSingleton<INpcTool, NpcTool>();
        services.AddSingleton<IWebTool, WebTool>();
        services.AddSingleton<ICronTool, CronTool>();
        services.AddSingleton<IHermesTool, HermesTool>();
        services.AddSingleton<IMimoVoiceTool, MimoVoiceTool>();
        return services;
    }
}
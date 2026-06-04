using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Harem.Services.Agents;

public static class ServiceExtensions
{
    public static IServiceCollection AddAgentServices(this IServiceCollection services)
    {
        services.AddSingleton<ICliService, CliService>();
        services.AddSingleton<ICommandDispatchService, CommandDispatchService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IStateService, StateService>();
        services.AddSingleton<ICharacterImageService, CharacterImageService>();
        services.AddSingleton<IMemoryService, MemoryService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IWorkflowBuilderService, WorkflowBuilderService>();
        services.AddSingleton<INpcService, NpcService>();
        services.AddSingleton<IHeartbeatService, HeartbeatService>();
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<IContextService, ContextService>();
        services.AddSingleton<IToolService, ToolService>();
        services.AddSingleton<IAgentService, AgentService>();
        services.AddSingleton<IResponseService, ResponseService>();
        services.AddSingleton<ILlmService, LlmService>();
        services.AddSingleton<IConversationService, ConversationService>();
        services.AddSingleton<ICronService, CronService>();
        services.AddSingleton<IClioClient, ClioClient>();
        services.AddSingleton<IKeywordService, KeywordService>();
        services.AddSingleton<ISummaryService, SummaryService>();
        services.AddSingleton<IMimoService, MimoService>();
        return services;
    }
}
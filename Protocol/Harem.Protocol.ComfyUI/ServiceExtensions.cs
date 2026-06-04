using Microsoft.Extensions.DependencyInjection;

namespace Harem.Protocol.ComfyUI;

public static class ServiceExtensions
{
    public static IServiceCollection AddComfyUIProtocol(this IServiceCollection services, string baseUrl)
    {
        services.AddHttpClient("ComfyUI", client => { client.Timeout = TimeSpan.FromMinutes(5); });

        services.AddSingleton<IComfyUiClientFactory, ComfyUiClientFactory>(provider =>
            new ComfyUiClientFactory(provider.GetRequiredService<IHttpClientFactory>(), baseUrl));

        return services;
    }
}
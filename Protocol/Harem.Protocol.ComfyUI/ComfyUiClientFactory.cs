namespace Harem.Protocol.ComfyUI;

public interface IComfyUiClientFactory
{
    ComfyUiClient CreateClient();
}

public class ComfyUiClientFactory(IHttpClientFactory httpClientFactory, string baseUrl) : IComfyUiClientFactory
{
    public ComfyUiClient CreateClient()
    {
        var httpClient = httpClientFactory.CreateClient("ComfyUI");
        return new ComfyUiClient(httpClient, baseUrl);
    }
}
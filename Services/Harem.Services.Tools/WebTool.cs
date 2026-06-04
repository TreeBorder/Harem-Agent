using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Services.Abstractions.Tools;
using Microsoft.Extensions.Options;

namespace Harem.Services.Tools;

public class WebTool : IWebTool
{
    public WebTool(IOptions<AgentOptions> options)
    {
    }

    public async Task<string> WebSearch(string words)
    {
        return "暂不支持网络检索";
    }
}
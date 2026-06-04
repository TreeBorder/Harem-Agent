using System.ComponentModel;
using System.Text.Json;

namespace Harem.Services.Abstractions.Tools;

[Description("帮助工具")]
public interface IHelpTool
{
    [Description("获取支持的场景列表")]
    public Task<JsonElement> GetSupportScene();

    [Description("获取支持的服装列表")]
    public Task<JsonElement> GetSupportOutfit();

    [Description("获取支持的动作列表")]
    public Task<JsonElement> GetSupportAction();
}

using System.Text.Json;
using Spectre.Console;

namespace Harem.Cli;

/// <summary>
/// CLI 全局设置
/// </summary>
public static class CliSettings
{
    public static string ServerUrl { get; set; } = "https://harem-border.taila384f8.ts.net";
    public static string? WorkspacePath { get; set; }

    public static string HubUrl => $"{ServerUrl.TrimEnd('/')}/chat/hub";
    public static string ApiBase => $"{ServerUrl.TrimEnd('/')}/api";

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static void Initialize(string? serverArg, string? workspaceArg)
    {
        ServerUrl = serverArg
                    ?? Environment.GetEnvironmentVariable("HAREM_SERVER")
                    ?? "https://harem-border.taila384f8.ts.net";
        WorkspacePath = workspaceArg
                        ?? Environment.GetEnvironmentVariable("HAREM_WORKSPACE");
    }

    /// <summary>
    /// 调用 REST API 获取 JSON
    /// </summary>
    public static async Task<T?> GetAsync<T>(string path)
    {
        try
        {
            var url = $"{ApiBase}{path}";
            var json = await HttpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ API 请求失败: {ex.Message.EscapeMarkup()}[/]");
            return default;
        }
    }
}

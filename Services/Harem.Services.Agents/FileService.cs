using System.Text.Encodings.Web;
using System.Text.Json;
using Harem.Contracts.Configurations;
using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.Options;

namespace Harem.Services.Agents;

public class FileService(IOptions<RuntimeOptions> runtimeOptions) : IFileService
{
    private readonly string _workspaceDirectory = runtimeOptions.Value.Workspace;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public async Task<JsonElement?> GetJsonFileAsync(string fileName)
    {
        var filePath = Path.Combine(_workspaceDirectory, fileName);
        if (!File.Exists(filePath))
            return null;

        var content = await File.ReadAllTextAsync(filePath);
        if (string.IsNullOrWhiteSpace(content))
            return null;

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.Clone();
    }

    public async Task<string?> GetFileAsync(string fileName, int? startLine, int? endLine)
    {
        var filePath = Path.Combine(_workspaceDirectory, fileName);
        if (!File.Exists(filePath))
            return null;

        if (startLine.HasValue && endLine.HasValue)
            return await File.ReadLinesAsync(filePath).Skip(startLine.Value)
                .Take(endLine.Value - startLine.Value + 1).AggregateAsync((current, line) => current + line);
        return await File.ReadAllTextAsync(filePath);
    }

    public async Task<bool> WriteJsonFileAsync(string fileName, JsonElement content)
    {
        var filePath = Path.Combine(_workspaceDirectory, fileName);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var json = content.GetRawText();
        await File.WriteAllTextAsync(filePath, json);
        return true;
    }

    async Task<bool> IFileService.WriteFileAsync(string fileName, string content)
    {
        var filePath = Path.Combine(_workspaceDirectory, fileName);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(filePath, content);
        return true;
    }

    public async Task<T?> ReadJsonAsync<T>(string fileName)
    {
        var filePath = Path.Combine(_workspaceDirectory, fileName);
        if (!File.Exists(filePath))
            return default;

        var content = await File.ReadAllTextAsync(filePath);
        if (string.IsNullOrWhiteSpace(content))
            return default;

        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    public async Task<bool> WriteJsonAsync<T>(string fileName, T value)
    {
        var filePath = Path.Combine(_workspaceDirectory, fileName);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(value, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
        return true;
    }
}
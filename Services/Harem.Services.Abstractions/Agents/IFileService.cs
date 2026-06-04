using System.Text.Json;

namespace Harem.Services.Abstractions.Agents;

public interface IFileService
{
    Task<string?> GetFileAsync(string fileName, int? startLine = null, int? endLine = null);

    Task<bool> WriteFileAsync(string fileName, string content);

    Task<T?> ReadJsonAsync<T>(string fileName);
    Task<bool> WriteJsonAsync<T>(string fileName, T value);

    Task<JsonElement?> GetJsonFileAsync(string fileName);
}
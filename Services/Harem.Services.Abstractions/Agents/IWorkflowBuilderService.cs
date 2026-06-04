namespace Harem.Services.Abstractions.Agents;

public interface IWorkflowBuilderService
{
    Task<string?> BuildAsync(string scene, string outfit, string action, string? aspectRatio = null);
    
    Task<bool> CheckBlock(string type,string key);
}
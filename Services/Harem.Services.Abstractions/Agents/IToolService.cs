using Microsoft.Extensions.AI;

namespace Harem.Services.Abstractions.Agents;

public interface IToolService
{
    List<AITool> GetTools();
}
using System.Text.Json;

namespace Harem.Contracts.Domain.Entities;

public class Session
{
    public required SessionInfo Info { get; set; }
    public JsonElement Content { get; set; }
}
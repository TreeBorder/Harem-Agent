namespace Harem.Services.Abstractions.Scheduler;

public interface ICharacterImageTracker
{
    void StartTracking(string sessionKey, string promptId);
}
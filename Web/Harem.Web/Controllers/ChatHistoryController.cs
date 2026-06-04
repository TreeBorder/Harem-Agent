using Harem.Services.Abstractions.Agents;
using Microsoft.AspNetCore.Mvc;

namespace Harem.Web.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatHistoryController(
    ISessionService sessionService,
    IHistoryService historyService) : ControllerBase
{
    /// <summary>获取指定会话的最近 N 条消息</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string sessionKey,
        [FromQuery] int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            return BadRequest(new { error = "sessionKey 是必填参数" });

        try
        {
            var session = await sessionService.GetSessionAsync(sessionKey);
            var history = await historyService.GetHistoryAsync(session.Info.SessionId);

            var messages = history.Messages
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    role = m.Role.ToString(),
                    text = m.Text,
                    time = m.CreatedAt.ToUnixTimeMilliseconds()
                })
                .ToList();

            return Ok(new { sessionKey, sessionId = session.Info.SessionId, messages });
        }
        catch (Exception ex)
        {
            return NotFound(new { error = $"获取历史失败: {ex.Message}" });
        }
    }
}

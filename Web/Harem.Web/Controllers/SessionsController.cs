using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Microsoft.AspNetCore.Mvc;

namespace Harem.Web.Controllers;

[ApiController]
[Route("api/sessions")]
public class SessionsController(ISessionService sessionService) : ControllerBase
{
    /// <summary>列出所有会话（key + sessionId + 更新时间）</summary>
    [HttpGet]
    public async Task<IReadOnlyList<SessionInfo>> GetAll()
    {
        return await sessionService.GetAllSessionsAsync();
    }

    /// <summary>获取单个会话信息</summary>
    [HttpGet("{sessionKey}")]
    public async Task<IActionResult> GetByKey(string sessionKey)
    {
        var info = await sessionService.GetSessionInfoAsync(sessionKey);
        return info != null ? Ok(info) : NotFound();
    }
}

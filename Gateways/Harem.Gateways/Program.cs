using Harem.Chats.Mattermost;
using Harem.Chats.SignalR;
using Harem.Contracts.Configurations;
using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Contracts.Domain.Messages;
using Harem.Data.Abstractions.Vector;
using Harem.Data.Vector;
using Harem.Protocol.Agents.Abstractions;
using Harem.Protocol.ComfyUI;
using Harem.Protocol.Ollama;
using Harem.Services.Agents;
using Harem.Services.Handlers;
using Harem.Services.Jobs;
using Harem.Services.Tools;
using Harem.Services.Abstractions.Channels;
using Harem.Services.Channels;
using Harem.Works;
using Microsoft.Extensions.FileProviders;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

// ===== 0. Systemd =====
if (OperatingSystem.IsLinux()) builder.Services.AddSystemd();

// ===== 1. 基础运行时配置 =====
var agentKey = builder.Configuration["Key"]
               ?? Environment.GetEnvironmentVariable("HAREM_KEY")
               ?? throw new("Agent key not found. Use --Key <value> or set HAREM_KEY.");
var workspace = builder.Configuration["Workspace"]
                ?? Environment.GetEnvironmentVariable("HAREM_WORKSPACE")
                ?? throw new("Workspace not found. Use --Workspace <path> or set HAREM_WORKSPACE.");

builder.Services.Configure<RuntimeOptions>(o =>
{
    o.Key = agentKey;
    o.Workspace = workspace;
});

var configPath = Path.Combine(workspace, "config.json");
builder.Configuration.AddJsonFile(configPath, optional: false, reloadOnChange: true);
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.Configure<ChatOptions>(builder.Configuration.GetSection("Agent:Chat"));
builder.Configuration.AddJsonFile(Path.Combine(workspace, ".heartbeat/config.json"), optional: false,
    reloadOnChange: true);
builder.Services.Configure<HeartbeatOptions>(builder.Configuration.GetSection("Heartbeat"));

var agentOptions = new AgentOptions();
builder.Configuration.GetSection("Agent").Bind(agentOptions);
var heartbeatOptions = new HeartbeatOptions();
builder.Configuration.GetSection("Heartbeat").Bind(heartbeatOptions);

var cronConfigPath = Path.Combine(workspace, ".cron/config.json");
if (File.Exists(cronConfigPath))
    builder.Configuration.AddJsonFile(cronConfigPath, optional: true, reloadOnChange: true);
builder.Services.Configure<CronOptions>(builder.Configuration.GetSection("Cron"));

// ===== 2. Protocol =====
builder.Services.AddComfyUIProtocol(agentOptions.ComfyUi?.BaseUrl ?? throw new("ComfyUI baseUrl not configured."));
builder.Services.AddSingleton<IEmbeddingClient>(_ =>
    new OllamaEmbeddingClient(
        agentOptions.Ollama?.BaseUrl ?? "http://localhost:11434",
        agentOptions.Ollama?.EmbeddingModel ?? "qwen3-embedding:0.6b",
        agentOptions.Ollama?.EmbeddingDimension ?? 1024));

// ===== 3. Data / Services / Tools / Handlers =====
builder.Services.AddSingleton<IVectorIndex, VectorDataIndex>();
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddAgentServices();
builder.Services.AddRpgTools();
builder.Services.AddCommandHandlers();

// ===== 3b. Channel 消息管线 =====
builder.Services.AddSingleton<IMessageChannel<ReceivedMessage>, ChatReceiveChannel>();
builder.Services.AddSingleton<IMessageChannel<ReplyMessage>, ChatReplyChannel>();
builder.Services.AddSingleton<IReplyChannelManager, ReplyChannelManager>();

// ===== 4. Quartz =====
builder.Services.AddQuartz(q =>
{
    q.AddJob<HeartbeatJob>(j => j.WithIdentity("HeartbeatJob"));
    q.AddTrigger(t => t.ForJob("HeartbeatJob")
        .StartAt(DateTimeOffset.UtcNow.AddMinutes(heartbeatOptions.MinMinutes))
        .WithIdentity("HeartbeatTrigger"));
    q.AddJob<BuildIndexJob>(j => j.WithIdentity("BuildIndexJob"));
    q.AddTrigger(t => t.ForJob("BuildIndexJob").WithCronSchedule("0 0 3 * * ?").WithIdentity("BuildIndexTrigger"));
    q.AddJob<DailyResetJob>(j => j.WithIdentity("DailyResetJob"));
    q.AddTrigger(t => t.ForJob("DailyResetJob").WithCronSchedule("0 0 4 * * ?").WithIdentity("DailyResetTrigger"));
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

// ===== 5. Hosted Services =====
builder.Services.AddHostedService<WorkspaceMigrationWork>();

// Mattermost 出站（WebSocket + 回复消费）
builder.Services.AddSingleton<IHostedService, MattermostService>();

// SignalR 出站
builder.Services.AddSingleton<IHostedService, SignalRService>();

// RpgWork 启动
builder.Services.AddHostedService<RpgWork>();

// ChatRouterWork 启动
builder.Services.AddHostedService<ChatRouterWork>();

// ===== 6. Web 配置 & 目录 =====
builder.Services.Configure<WebOptions>(builder.Configuration.GetSection("Agent:Web"));
var publicUrl = (agentOptions.Web?.PublicUrl ?? "http://localhost:5000").TrimEnd('/');

var gameDir = Path.Combine(workspace, ".game");
var gameAudioDir = Path.Combine(gameDir, "audio");
Directory.CreateDirectory(gameDir);
Directory.CreateDirectory(gameAudioDir);

var app = builder.Build();

// ===== 7. 中间件 =====
app.UseForwardedHeaders();

// SPA rewrite: /game/xxx（无后缀）→ /game/index.html
// 有后缀的（.json, .mp3 等）正常走静态文件
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/game"))
    {
        var ext = Path.GetExtension(ctx.Request.Path.Value);
        if (string.IsNullOrEmpty(ext) && ctx.Request.Path.Value != "/game")
        {
            ctx.Request.Path = "/game/index.html";
        }
    }

    await next();
});

// 托管 .game/ 目录：/game/* → {workspace}/.game/
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(gameDir),
    RequestPath = "/game"
});

// 托管音频文件：/api/game/audio/* → {workspace}/.game/audio/
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(gameAudioDir),
    RequestPath = "/api/game/audio",
    ServeUnknownFileTypes = true
});

// ===== 8. 遗留端点 =====
// 健康检查
app.MapGet("/ping", () => "pong");

// 页面重定向
app.MapGet("/", () => Results.Redirect("/game/index.html"));
app.MapGet("/game", () => Results.Redirect("/game/index.html"));

app.MapHub<ChatHub>("/chat/hub");
app.MapControllers();

// 文件代理：将工作区内的本地文件路径转为可下载 URL
// 只允许通过特定目录前缀访问（防止遍历整个工作区）
app.MapGet("/files/{**path}", async (string path) =>
{
    var allowedDirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/"] = ".audio/",
    };

    var matched = allowedDirs.FirstOrDefault(d =>
        path.StartsWith(d.Key, StringComparison.OrdinalIgnoreCase)).Key;

    if (matched == null) return Results.Forbid();

    // 去掉前缀目录名，拼上磁盘上带点的真实目录
    var relative = path[matched.Length..];
    var diskDir = allowedDirs[matched];
    var fullPath = Path.GetFullPath(Path.Combine(workspace, diskDir, relative));

    // 安全检查
    if (!fullPath.StartsWith(Path.GetFullPath(Path.Combine(workspace, diskDir))))
        return Results.Forbid();
    if (!File.Exists(fullPath))
        return Results.NotFound();

    return Results.File(File.OpenRead(fullPath), "application/octet-stream");
});

// ===== 9. 启动 =====
Console.WriteLine($"🎮 Harem: {agentKey} | {workspace}");
Console.WriteLine($"   Game: {publicUrl}/game/index.html");
await app.RunAsync();
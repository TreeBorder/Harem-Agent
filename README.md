# Harem

多角色 AI 智能体系统。每个智能体拥有独立工作区，通过本地文件管理人设、记忆和状态，接入 Mattermost / SignalR 即时通信和 CLI 客户端，集成 ComfyUI 图片生成与 MiMo TTS 语音合成，内置心跳思念系统、NPC 知识库、关键词世界书和定时任务系统。

## 架构

```
Harem.sln
├── Chats/
│   ├── Harem.Chats.Abstractions/      # 聊天渠道抽象接口
│   ├── Harem.Chats.Mattermost/        # Mattermost WebSocket 接入
│   └── Harem.Chats.SignalR/           # SignalR Hub 接入
├── Cli/
│   └── Harem.Cli/                     # CLI 客户端（Spectre.Console + SignalR）
├── Contracts/
│   ├── Harem.Contracts.Configurations/# 配置契约 (AgentOptions, HeartbeatOptions...)
│   ├── Harem.Contracts.Domain/        # 领域实体 (Session, NpcInfo, StateItem...)
│   └── Harem.Contracts.Dtos/          # 数据传输对象
├── Data/
│   ├── Harem.Data.Abstractions/       # 数据抽象 (IVectorIndex, IMetadataRepository)
│   └── Harem.Data.Vector/             # 向量索引实现
├── Gateways/
│   └── Harem.Gateways/                # 宿主入口 (Program.cs)
├── Protocol/
│   ├── Harem.Protocol.Agents.Abstractions/ # Embedding 抽象
│   ├── Harem.Protocol.ComfyUI/        # ComfyUI 图片生成客户端
│   └── Harem.Protocol.Ollama/         # Ollama Embedding 客户端
├── Services/
│   ├── Harem.Services.Abstractions/   # 服务接口层
│   ├── Harem.Services.Agents/         # 核心服务 (Conversation, Memory, Session, Heartbeat, Cron, Summary, Keyword, Clio, Mimo, Cli...)
│   ├── Harem.Services.Channels/       # 消息管道 (ChatReceive, ChatReply, ReplyChannelManager)
│   ├── Harem.Services.Handlers/       # 指令处理器 (/help, /new, /npc, /memory, /cron, /keyword, /session, /fix, /heartbeat...)
│   ├── Harem.Services.Tools/          # AI 工具实现 (Memory, State, File, MimoVoice, Cron, Npc...)
│   └── Harem.Services.Jobs/           # Quartz 定时任务
├── Web/
│   └── Harem.Web/                     # Web 控制器 (Sessions, ChatHistory)
├── Works/
│   └── Harem.Works/                   # 后台工作流 (RpgWork, ChatRouterWork, WorkspaceMigrationWork)
└── Templates/                         # 智能体工作区模板
```

## 智能体工作区

每个智能体拥有独立的工作区目录，全部数据通过本地文件管理：

```
/workspace/
├── AGENTS.md              # 行为守则（最外层系统提示）
├── SOUL.md                # 核心人设与灵魂
├── USER.md                # 对用户的认知
├── MEMORY.md              # 长期记忆摘要
├── config.json            # 模型参数、服务配置
├── state.json             # 动态状态数据
├── memories/              # 每日对话记忆
│   ├── 2026-05-01.md
│   └── keywords.json      # 关键词索引（类似世界书，自动提取+匹配注入）
├── .history/              # 会话历史与上下文存储
│   └── {sessionId}/
│       ├── history.json   # 历史摘要 + 消息摘要
│       ├── rounds.json    # 最近N轮完整交互（含工具调用）
│       └── recentNpcs.json # 最近注入的NPC上下文
├── .npc/                  # NPC 知识库
│   ├── info.json          # NPC 基本信息
│   └── record.json        # NPC 互动记录
├── .heartbeat/            # 心跳系统配置
│   └── config.json
├── .cron/                 # 定时任务系统
│   ├── jobs.json          # 任务配置（持久化）
│   ├── config.json        # 定时任务配置（可选）
│   └── execution.log      # 执行日志
├── .vector/               # 向量索引
└── .comfyui/              # ComfyUI 工作流配置
```

## 核心功能

### 自主上下文构建

Agent 在每轮对话中自动组装分层上下文，确保模型获得完整且不冗余的信息：

```
[系统提示]           — 角色设定和规则（AGENTS.md/SOUL.md/WORLD.md/MEMORY.md/USER.md）
[历史摘要]           — 由后台独立管理的对话摘要，用于保持对话连续性
[关键词]             — 根据本轮输入从关键词索引中匹配到的历史内容摘要（类似世界书，仅在命中时出现）
[动态上下文]         — 状态信息、NPC 角色、记忆片段、定时任务结果等实时注入的增强信息
[历史消息]           — 更早的对话记录，省略了 InnerVoice
[最近交互]           — 最近几轮的完整交互过程（包含工具调用、工具返回）
[当前输入]           — 用户本轮的输入和系统指令
```

- **最近N轮完整过程**：通过 `RpgChatHistoryProvider` 在 Agent 执行期间捕获响应消息（含工具调用），存入 `.history/{sessionId}/rounds.json`，下轮构建时完整注入
- **迁移摘要**：新会话无历史摘要时，自动读取 `memories/` 目录最近2天记忆文件，引导模型生成摘要
- **关键词区**：凌晨 4:00 `DailyResetJob` 导出天记忆后自动提取关键词，构建分层索引（一层 OR 匹配 → 二层 AND 筛选），用户输入命中时注入到摘要区之后
- **响应捕获**：非流式模式下分段输出（`<br/>` 分段 + 1s 延迟），流式模式下逐段实时推送

### 心跳系统

独立后台服务，按可配置的随机间隔运行：
- 思念值随时间增长，互动后重置
- 达到阈值时触发惊喜问候（主动撩用户）
- 思念值影响问候等级（Vivid / Familiar / Vague）
- 会话活跃时自动跳过心跳

### NPC 系统

- `.npc/` 目录管理所有 NPC 的信息和互动记录
- **自动注入**：消息中提及 NPC 名称/标签时，按亲密度和冷却规则注入背景知识
- **强制注入**：`{NPC名/标签}` 语法，无条件注入，不受冷却和数量限制
- 亲密度时间衰减，记忆深度等级（Vivid > Familiar > Vague > Forgotten）

### 记忆系统

- `MEMORY.md` 长期记忆 + `memories/YYYY-MM-DD.md` 每日记忆
- 每日记忆分为线上记忆（自动导出对话）和线下记忆（手动编辑）
- Ollama Embedding 向量化 + 语义检索
- **强制注入**：`#关键词#` 语法，语义检索 top 3 后无条件注入
- 定时构建索引（每天 3:00）+ 手动构建（`/memory index`）

### 关键词区（世界书）

- `memories/keywords.json` 单文件存储，无关 session
- **一层关键词（primary）**：OR 匹配，命中即注入全部关联条目
- **二层关键词（secondary）**：AND 筛选，仅有二层标签完全匹配才精确注入
- 每日凌晨 4:00 自动提取（调用 Clio 子智能体处理天记忆）
- 手动提取：`/keyword extract YYYY-MM-DD`
- 查看索引：`/keyword`
- 淘汰机制：条目超过 10 条且最近 30 天未触发时自动裁旧

### 指令系统

指令由 `ICommandDispatchService` 统一分发，不经过 LLM 的快速指令，直接由 KeyedService 注册的 Handler 处理：

| 指令 | 功能 |
|------|------|
| `/help` | 显示帮助 |
| `/new [内容]` | 重置上下文，开始新对话 |
| `/state [get/set]` | 查看/修改角色状态 |
| `/session [reset\|info]` | 会话管理（重置/查看信息） |
| `/npc [list/get/search]` | NPC 信息检索 |
| `/memory [search/index/get/longterm]` | 记忆检索与管理 |
| `/keyword [\|extract YYYY-MM-DD]` | 查看关键词索引 / 手动提取某天关键词 |
| `/photo [camera/gallery]` | 角色图片生成 |
| `/heartbeat` | 手动触发心跳 |
| `/cron [add\|once\|list\|remove\|enable\|disable\|help]` | 定时任务管理 |
| `/fix` | 修复对话状态（恢复中断的 session） |

**指令分发机制**：`CommandDispatchService` 解析消息首字符 `/`，通过 `IServiceProvider.GetKeyedService<ICommandHandler>(cmd)` 查找注册的处理器。处理器返回 `CommandDispatchResult`，支持：
- `CommandReply` — 直接回复用户（不经过 LLM）
- `SystemMessage` / `UserMessage` — 注入消息到上下文后继续走 LLM
- `SwitchModelKey` — 切换当前模型
- `ShouldStop` — 指令处理完毕后是否跳过 LLM 调用

行内注入语法：

| 语法 | 作用 |
|------|------|
| `{杨文丽}` | 强制注入 NPC 信息 |
| `#技术栈#` | 强制注入记忆检索结果 |

### 定时任务系统（Cron）

用户和 Agent 均可创建定时任务，支持单次和循环调度：

**三种消息目标：**

| Target | 说明 |
|--------|------|
| `SystemToAgent` | 系统消息→Agent，触发 Agent 响应 |
| `MessageToUser` | 直接发消息给用户 |
| `MessageToAgent` | 用户消息→Agent，触发 Agent 处理 |

**用户命令：**

```
/cron add <名称> <cron表达式> <内容>        # 创建循环任务（默认 MessageToAgent）
/cron once <名称> <时间> <内容>             # 创建单次任务
/cron list                                  # 列出所有任务
/cron remove <id>                           # 删除任务
/cron enable <id> / /cron disable <id>      # 启用/禁用
```

**AI 工具：** Agent 通过 `create_cron_job`、`create_one_shot_job`、`list_cron_jobs`、`delete_cron_job`、`toggle_cron_job` 工具管理定时任务。

**核心机制：**
- **文件持久化**：`.cron/jobs.json` 存储任务配置，`.cron/execution.log` 记录执行日志
- **活跃会话检测**：执行时检查会话 `UpdateAt`，10 分钟内有更新则延后 10 分钟重试，最多延后 6 次（1 小时）后放弃执行
- **执行结果注入**：任务执行后结果存入内存，下次用户发消息时通过 `[定时任务执行记录]` 标签注入上下文，让 Agent 知晓执行情况
- **Cron 表达式校验**：创建前通过 `CronExpression.IsValidExpression()` 校验合法性

### 定时任务

| 任务 | 时间 | 功能 |
|------|------|------|
| BuildIndexJob | 3:00 | 增量构建记忆 + NPC 向量索引 |
| DailyResetJob | 4:00 | 导出对话到天记忆 → 提取关键词 → 重置会话上下文 |
| HeartbeatJob | 随机间隔 | 思念值计算 + 惊喜问候触发 |
| CronJob | 动态调度 | 用户/Agent 创建的定时任务（由 CronService 管理） |

### Clio 子智能体

`IClioClient` 统一封装对 Clio（Hermes 子智能体）的进程调用，用于需要独立 LLM 处理的场景：

- **关键词提取**：处理天记忆、提取关键词节点
- **HermesTool**：Agent 通过 Clio 调用代码工具
- 通过 `ClioClient` 管理进程生命周期、PATH 注入、JSON 结构化输出

### MiMo 语音合成

`MimoService` + `MimoVoiceTool` 提供 TTS 语音合成能力，通过 MiMo MCP 协议调用：

- Agent 可通过工具 `mimo_voice` 将文本转为语音
- 支持音色配置（在 MCP 服务端配置，运行时通过工具参数传递）
- TTS 缓存复用：定时任务创建时预生成音频，后续执行直接复用

## 技术栈

| 组件 | 技术 |
|------|------|
| 运行时 | .NET 10 |
| AI 框架 | Microsoft.Agents.AI |
| 聊天渠道 | Mattermost (WebSocket) / SignalR Hub |
| CLI 客户端 | Spectre.Console + SignalR |
| 图片生成 | ComfyUI |
| 语音合成 | MiMo TTS (via MCP) |
| 向量嵌入 | Ollama Embedding |
| 任务调度 | Quartz.NET |
| 服务管理 | systemd (Linux) / Supervisor |

## 模型配置

支持多模型提供商，按 `Provider/Model` 格式指定模型。每个模型可独立配置行为参数：

```json
{
  "ModelProviders": [
    {
      "Name": "deepseek",
      "Protocol": "OpenAI",
      "BaseUrl": "https://api.deepseek.com",
      "ApiKey": "...",
      "Models": [
        {
          "Name": "deepseek-chat",
          "ContextTokens": 65536,
          "ChatBehavior": {
            "Temperature": 0.8,
            "EnableStreaming": true,
            "EnableReasoning": false
          }
        },
        {
          "Name": "deepseek-reasoner",
          "ContextTokens": 65536,
          "ChatBehavior": {
            "EnableStreaming": true,
            "EnableReasoning": true,
            "ReasoningEffort": "high"
          }
        }
      ]
    }
  ]
}
```

### ChatBehavior 配置项

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Temperature` | float? | null | 生成温度 (0.0 ~ 2.0) |
| `MaxOutputTokens` | int? | null | 最大输出 token 数 |
| `TopP` | float? | null | 核采样概率阈值 |
| `EnableStreaming` | bool | true | 是否开启流式输出，关闭后走非流式调用 |
| `EnableReasoning` | bool | false | 是否为推理模型，开启后传入 ReasoningEffort |
| `ReasoningEffort` | string? | null | 推理努力程度: "low" / "medium" / "high" |
| `AllowMultipleToolCalls` | bool? | null | 是否允许单次响应调用多个工具 |
| `StopSequences` | string[]? | null | 停止序列 |

> `EnableStreaming` 和 `EnableReasoning` 是独立开关。关闭原生推理后，通过自定义 `<Thinking>` 标签实现角色内心独白，更适合角色扮演场景。

## 快速开始

### 前置条件

- .NET 10 SDK
- Ollama（运行 Embedding 模型）
- Mattermost 实例 + Bot Token（可选，选其一即可）
- SignalR 客户端 / Cli 客户端（可选）
- ComfyUI（可选，图片生成）
- MiMo MCP Server（可选，语音合成）
- Clio（可选，关键词提取 + HermesTool）

### 配置

三种方式注入配置，优先级递减：

1. **CLI 参数**: `harem --Key <key> --Workspace <path>`
2. **环境变量**: `HAREM_KEY` + `HAREM_WORKSPACE`
3. **工作区配置**: `{Workspace}/config.json`

### 运行

```bash
# 开发（指定 Key 和工作区）
dotnet run --project Gateways/Harem.Gateways/Harem.Gateways.csproj -- --Key my-agent --Workspace /path/to/workspace

# 发布
dotnet publish Gateways/Harem.Gateways/Harem.Gateways.csproj -c Release -o ./publish
./publish/harem --Key my-agent --Workspace /path/to/workspace
```

### 部署（Supervisor）

生产环境推荐使用 Supervisor 管理智能体进程。

**安装 Supervisor：**

```bash
# macOS（Homebrew）
brew install supervisor

# Ubuntu / Debian
apt install supervisor

# CentOS / RHEL
yum install supervisor
```

**智能体配置（`/etc/supervisor/conf.d/agents.ini`）：**

```ini
[program:agent-name]
command=/path/to/harem
directory=/path/to/workspace
environment=HAREM_KEY="agent-name",HAREM_WORKSPACE="/path/to/workspace",DOTNET_ENVIRONMENT="Production"
autostart=true
autorestart=true
stdout_logfile=/path/to/workspace/.logs/agent-name.log
stderr_logfile=/path/to/workspace/.logs/agent-name-error.log
stdout_logfile_maxbytes=5MB
stderr_logfile_maxbytes=5MB
stdout_logfile_backups=2
stderr_logfile_backups=2
```

**管理智能体：**

```bash
supervisorctl reread              # 重新加载配置
supervisorctl update              # 应用变更
supervisorctl start agent-name    # 启动
supervisorctl stop agent-name     # 停止
supervisorctl restart agent-name  # 重启
supervisorctl status              # 查看所有状态
supervisorctl tail agent-name     # 查看日志
```

> **工作区目录建议：** 每个智能体独立工作区，通过环境变量 `HAREM_KEY` 和 `HAREM_WORKSPACE` 注入。运行时会自动生成 `.history/`、`.sessions/`、`.audio/`、`.logs/`、`.cron/` 等运行时目录。`.npc/` 目录需手动创建并添加 NPC 知识库文件。

## License

MIT

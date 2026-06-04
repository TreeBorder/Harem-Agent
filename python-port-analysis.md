# Harem Python Port — 架构分析

## 项目规模

C# 版约 **15000+ 行**，涵盖：

| 模块 | 说明 | C# 行数估算 |
|------|------|------------|
| Agent 核心 | BorderRpgAgent (主流程+上下文+流式+模型管理) | ~4000 |
| 服务层 | Session, Memory, NPC, State, Heartbeat, History, WorkflowBuilder | ~6000 |
| Handler | 10+ 指令处理器 | ~3000 |
| 工具层 | File/State/Memory/NPC/TTS/Image/Web | ~1500 |
| 数据契约 | 领域实体、DTO、配置 | ~1500 |
| 聊天渠道 | Mattermost WebSocket 接入 | ~1000 |
| Protocol | ComfyUI, MiniMax, Ollama 客户端 | ~1000 |

## 核心架构

```
消息输入 ──→ [指令检测] ──yes──→ Handler 处理 ──→ 回复
                    │ no
                    ▼
          [消息循环]
              │
              ▼
      [上下文构建]
    ┌─────────────────┐
    │ AGENTS.md       │ 系统提示
    │ SOUL.md         │ 人设
    │ USER.md         │ 用户认知
    │ MEMORY.md       │ 长期记忆
    │ 状态注入         │ 思念/场景/情绪
    │ NPC注入           │ 关键词/语义匹配
    │ 强制记忆注入     │ #关键词# 语法
    │ 历史摘要         │ 迁移摘要
    │ 历史消息(100条)  │ 已摘要历史
    │ 最近3轮完整交互  │ 含工具调用
    │ 新输入           │ 时间戳+消息
    └─────────────────┘
              │
              ▼
      [LLM 调用] ─── 流式/非流式 ────→ 分段发送
              │
              ▼
      [格式解析] <Thinking>/<Content>/<Summary>
              │
              ▼
      [保存历史] history.json + rounds.json
              │
              ▼
      [保存会话] AgentSession 序列化
```

## Python 版要保留的关键特性

### 1️⃣ 工作区文件结构（不动）

```
workspace/
├── AGENTS.md / SOUL.md / USER.md / MEMORY.md / WORLD.md
├── config.json          # 模型、服务配置
├── state.json           # 角色状态
├── memories/            # 每日记忆 YYYY-MM-DD.md
├── .history/{sid}/      # history.json + rounds.json + bags
├── .npc/{key}/          # info.json + memories
├── .heartbeat/          # config.json + prompts.json
├── .vector/             # 内存/FAISS 向量索引
├── .sessions/           # sessions config.json + session content
├── .comfyui/            # ComfyUI 工作流配置
```

### 2️⃣ 核心逻辑全部保留

- **上下文构建**: 5段分层 (基础prompt → 摘要 → 历史 → 完整交互 → 新输入)
- **记忆系统**: MEMORY.md + 每日记忆 + 向量检索 + 强制注入 `#关键词#`
- **NPC系统**: 人物卡 + 亲密度衰减 + 记忆深度等级 + 强制/自动注入 `{NPC名}`
- **心跳系统**: 思念值 + 寂寞值 + 惊喜问候 + 状态更新(8/12/18点)
- **指令系统**: /help /new /npc /memory /state /photo /scene /tts /session /heartbeat
- **状态系统**: longing/loneliness/dating/emotion/scene/outfit
- **对话格式**: <Thinking> <Content> <Summary> 三段式 + <br/> 分段发送

### 3️⃣ 简化的地方

| 模块 | C# 方式 | Python 方式 |
|------|---------|------------|
| AI Agent | Microsoft.Agents.AI + OpenAI SDK | openai SDK + asyncio |
| 向量索引 | IVectorIndex 接口 + 实现 | FAISS 或 numpy 余弦相似度 |
| Embedding | Ollama Embedding | 直接调用 Ollama API / 用 sentence-transformers |
| 聊天渠道 | Mattermost WebSocket | 先做 CLI / 后续 Mattermost |
| ComfyUI | 模板替换 | 保留模板逻辑, 直接 HTTP Post |
| 任务调度 | Quartz.NET | APScheduler |
| DI | 构造函数注入 | 简单工厂 / 依赖注入可选 |
| 流式 | Microsoft.Agents 流式 | OpenAI stream + async generator |

## 建议的实现策略

分阶段搞：

**Phase 1 — 核心引擎** (CLI 交互)
- Agent 核心流程: 上下文构建 → LLM 调用 → 格式解析 → 历史保存
- 工作区文件管理: 读写 AGENTS.md, 状态, 记忆, NPC
- 会话管理: session 创建/恢复/重置
- 指令系统: 文本界面输入, 同上输出
- 不做: 向量索引、流式、心跳、ComfyUI、TTS

**Phase 2 — 完整功能**
- 向量索引 (FAISS + sentence-transformers)
- 记忆强制注入 #关键词#
- NPC 强制/自动注入 {NPC名}
- 心跳系统 + 状态更新
- 流式输出
- 全部指令 Handler

**Phase 3 — 渠道 & 媒体**
- Mattermost WebSocket 接入
- ComfyUI 图片生成
- MiniMax TTS
- 定时任务 (APScheduler)

## 文件结构建议

```
harem-py/
├── main.py                    # 入口
├── config.py                  # 配置加载
├── agent/
│   ├── __init__.py
│   ├── core.py                # Agent 主循环
│   ├── context.py             # 上下文构建
│   ├── model.py               # 模型管理/回退
│   ├── streaming.py           # 流式处理
│   └── parser.py              # <Thinking>/<Content>/<Summary> 解析
├── services/
│   ├── __init__.py
│   ├── session.py             # SessionService
│   ├── history.py             # HistoryService
│   ├── memory.py              # MemoryService
│   ├── npc.py                 # NpcService
│   ├── state.py               # StateService
│   ├── heartbeat.py           # HeartbeatService
│   └── file_service.py        # FileService (文件读写)
├── handlers/
│   ├── __init__.py
│   ├── help.py
│   ├── session_handler.py
│   ├── npc_handler.py
│   ├── memory_handler.py
│   ├── state_handler.py
│   ├── photo_handler.py
│   └── tts_handler.py
├── tools/
│   ├── __init__.py
│   ├── file_tool.py
│   ├── state_tool.py
│   ├── memory_tool.py
│   ├── npc_tool.py
│   ├── tts_tool.py
│   └── image_tool.py
├── protocol/
│   ├── __init__.py
│   ├── ollama.py              # Ollama Embedding
│   ├── comfyui.py             # ComfyUI API
│   └── minimax.py             # MiniMax API
├── channels/
│   ├── __init__.py
│   ├── cli.py                 # CLI 交互
│   └── mattermost.py          # Mattermost WebSocket
├── vector/
│   ├── __init__.py
│   ├── index.py               # 向量索引接口
│   └── faiss_index.py         # FAISS 实现
├── models/
│   ├── __init__.py
│   ├── session.py             # 数据模型
│   ├── npc.py
│   ├── state.py
│   └── history.py
├── jobs/
│   ├── __init__.py
│   └── scheduler.py           # APScheduler 定时任务
└── requirements.txt
```

## 是否值得做

主人，说句实话——这项目 C# 版已经写得很完整了，架构清晰、模块划分合理、功能齐全。Python 版不会省什么工作量，只是换个语言。

**值得做的理由**：
- Python 生态更适合 AI 项目（openai SDK 直接接入、FAISS、sentence-transformers）
- 你自己更好改、更好扩展
- 可以搞成 pip 包直接装

**不值得做的理由**：
- C# 版已经有心跳、NPC 记忆注入、ComfyUI 全套
- .NET 10 性能比 Python 好太多
- 改 C# 代码更快

你觉得呢主人？要搞的话我直接开整 Phase 1，先把核心引擎跑起来，后面再慢慢加完整功能。

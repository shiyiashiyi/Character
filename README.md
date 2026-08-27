# Character

前端登录/注册（Vue 3）+ 后端认证 API（.NET 8）+ SQL Server 数据库 **CharacterSkills**。

## 目录

| 目录 | 说明 |
|------|------|
| `login/` | Vue 3 前端：登录/注册、主页菜单、Persona 工坊 |
| `api/FrontStudy.Api/` | ASP.NET Core Web API |
| `database/` | SQL Server 建表与授权脚本 |

## 前置条件

1. SQL Server 实例已运行（示例：`localhost\SQLEXPRESS` 或你的「服务器\实例名」）
2. 已创建数据库 **CharacterSkills**，并执行 `database/CharacterSkills/001_create_users.sql`
3. 推荐 SQL 登录：执行 `database/CharacterSkills/007_create_sql_login.sql`

连接字符串：复制 `api/FrontStudy.Api/appsettings.Development.json.example` 为 `appsettings.Development.json` 并填写密码；前端复制 `login/.env.example` 为 `.env.development`。

如需使用 Persona 工坊的 **AI 精修** 模式，还需要在 `appsettings.Development.json` 中配置 `AiProvider`：

```json
"AiProvider": {
  "Provider": "DeepSeek",
  "ApiKind": "ChatCompletions",
  "ApiKey": "DEEPSEEK_API_KEY_HERE",
  "Model": "deepseek-v4-flash",
  "BaseUrl": "https://api.deepseek.com",
  "MaxOutputTokens": 8000,
  "Temperature": 0.3
}
```

也可以参考 `api/FrontStudy.Api/appsettings.ai-providers.example.json` 切换为 OpenAI 或其他 OpenAI-compatible 服务。若不配置 API Key，Persona 工坊仍可使用默认的规则模式。

## 启动后端 API

```bash
cd Character/api/FrontStudy.Api
dotnet run
```

默认：**http://localhost:5050**

## 启动前端

```bash
cd Character/login
npm install
npm run dev
```

默认：**http://localhost:5173**

## 前端路由

| 路径 | 说明 |
|------|------|
| `/login` | 登录 / 注册 |
| `/home` | 主页（需登录，localStorage 会话） |
| `/forge` | Persona 工坊：上传小说文本生成 Skill，生成中显示 loading 与步骤进度 |

登录/注册成功约 0.9 秒后自动进入 `/home`。

## API 接口

- `POST /api/auth/register` — 注册
- `POST /api/auth/login` — 登录
- `POST /api/persona/forge` — 上传 `.txt`/`.md` + 角色名，生成 `character-card.json`、`SKILL.md` 与 `source-evidence.md`（multipart：`file`, `characterName`, `workTitle`, `chapterRange`, `mode`）
- `GET /api/health/db` — 数据库连接自检

`mode` 支持：

- `rule`：默认规则模式，基于规则抽取 + 模板生成（无需 AI，作为证据索引 / 回退方案）
- `ai`：多阶段 LLM 流水线——通读原文 → 抽取证据 → 综合人格 → 生成示例对话 → 输出结构化角色卡（`character-card.json`）+ `SKILL.md` + `source-evidence.md`

Persona 生成仅供个人/私有使用。上传小说文本时建议使用 `.txt` 或 `.md`，后端会尝试兼容 UTF-8 与常见中文文本编码。

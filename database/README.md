# CharacterSkills 数据库脚本

与 `login/` 前端登录页配套的 SQL Server 表结构。

## 数据库

- 名称：**CharacterSkills**
- 实例示例：`localhost\SQLEXPRESS`（按本机 SSMS 连接窗口填写）

## 在 DBeaver 中执行顺序

1. 确认左侧已展开 **CharacterSkills** 数据库（若无，先 `CREATE DATABASE CharacterSkills`）。
2. 打开 **SQL 编辑器** → **新建 SQL 脚本**。
3. 依次执行（选中脚本 → `Ctrl+Enter` 或工具栏执行）：
   - `CharacterSkills/001_create_users.sql` — 创建 **Users** 表
   - `CharacterSkills/004_grant_api_windows_user.sql` — **API 连库失败时必做**（给运行 `dotnet run` 的 Windows 用户授权）
   - `CharacterSkills/006_fix_login_user_mapping.sql` — 若 005 报 **15063**（登录已用另一用户名开户）则执行此脚本
   - `CharacterSkills/002_seed_demo_user.sql` — 可选，插入演示邮箱
   - `CharacterSkills/003_verify_users.sql` — 查看表结构与数据

## API 报错「无法打开数据库 CharacterSkills / 用户登录失败」

`dotnet run` 使用**当前 Windows 用户**，与 SSMS 里用的账户可能不同。

1. 在 SSMS 用管理员身份连接你的 SQL Server 实例
2. 执行 `004_grant_api_windows_user.sql`（若报错用户名不同，把脚本里的登录名改成报错中的名称）
3. 重启 API：`dotnet run`

## Users 表字段

| 列名 | 类型 | 说明 |
|------|------|------|
| UserId | BIGINT | 主键，自增 |
| Email | NVARCHAR(256) | 登录邮箱，唯一 |
| PasswordHash | NVARCHAR(512) | 密码哈希（禁止存明文） |
| DisplayName | NVARCHAR(100) | 显示名 |
| IsActive | BIT | 是否启用 |
| CreatedAtUtc | DATETIME2 | 创建时间（UTC） |
| UpdatedAtUtc | DATETIME2 | 更新时间 |
| LastLoginAtUtc | DATETIME2 | 最后登录时间 |

## 演示账号

请在前端 **注册** 页面自行创建测试账号，不要使用真实邮箱或常用密码。

密码在库中仅存哈希，由 **.NET Core API** 的 `PasswordHasher` 写入，禁止明文入库。

## .NET 连接字符串示例

复制 `Character/api/FrontStudy.Api/appsettings.Development.json.example` 为 `appsettings.Development.json`，按本机环境填写。

**SQL Server 身份验证（推荐）：**

1. 执行 `007_create_sql_login.sql`（脚本内密码请自行修改）
2. 示例格式：

```json
"CharacterSkills": "Server=你的服务器\\实例名;Database=CharacterSkills;User Id=你的SQL登录名;Password=你的密码;TrustServerCertificate=True;Encrypt=False;"
```

**Windows 身份验证（需为运行 dotnet 的 Windows 用户授权，见 006）：**

```json
"CharacterSkills": "Server=你的服务器\\实例名;Database=CharacterSkills;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
```

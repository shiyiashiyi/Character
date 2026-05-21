# CharacterSkills 数据库脚本

与 `login/` 前端登录页配套的 SQL Server 表结构。

## 数据库

- 名称：**CharacterSkills**
- 实例示例：`YEBBYHUANG-PC\YEBBYHUANG`（Windows 身份验证）

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

`dotnet run` 使用**当前 Windows 用户**（如 `MicrosoftAccount\xxx@qq.com`），与 SSMS 里用的账户可能不同。

1. 在 SSMS 用管理员身份连接 `YEBBYHUANG-PC\YEBBYHUANG`
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

## 与前端演示账号

| 邮箱 | 密码（明文仅用于开发） |
|------|------------------------|
| demo@front.study | demo12345 |

密码哈希需在 **.NET Core API** 中用 `PasswordHasher` 或 Identity 生成后写入，不要手写明文进库。

## .NET 连接字符串示例

**SQL Server 身份验证（推荐，避免 Windows 权限问题）：**

1. 执行 `007_create_sql_login.sql`
2. 在 `Character/api/FrontStudy.Api/appsettings.Development.json` 使用：

```json
"CharacterSkills": "Server=YEBBYHUANG-PC\\YEBBYHUANG;Database=CharacterSkills;User Id=frontstudy_app;Password=FrontStudy@2026;TrustServerCertificate=True;Encrypt=False;"
```

**Windows 身份验证（需为运行 dotnet 的 Windows 用户授权，见 006）：**

```json
"CharacterSkills": "Server=YEBBYHUANG-PC\\YEBBYHUANG;Database=CharacterSkills;Trusted_Connection=True;TrustServerCertificate=True;"
```

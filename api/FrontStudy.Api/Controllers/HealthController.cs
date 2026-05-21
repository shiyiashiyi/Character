/**
 * HealthController.cs — 数据库连接诊断（开发联调用）
 */
using FrontStudy.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FrontStudy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(AppDbContext db, IConfiguration config) : ControllerBase
{
    [HttpGet("db")]
    public async Task<IActionResult> CheckDatabase(CancellationToken ct)
    {
        var connStr = config.GetConnectionString("CharacterSkills");
        var usesSqlAuth = connStr?.Contains("User Id=", StringComparison.OrdinalIgnoreCase) == true
            || connStr?.Contains("User ID=", StringComparison.OrdinalIgnoreCase) == true;
        var authHint = usesSqlAuth
            ? "SQL 登录（连接字符串中的 User Id）"
            : $"{Environment.UserDomainName}\\{Environment.UserName}";

        try
        {
            var userCount = await db.Users.CountAsync(ct);

            return Ok(new
            {
                ok = true,
                canConnect = true,
                authMode = usesSqlAuth ? "SqlAuth" : "Windows",
                authHint,
                database = "CharacterSkills",
                usersTableRows = userCount,
                message = "数据库连接正常，Users 表可访问",
            });
        }
        catch (Exception ex)
        {
            var sql = FindSqlException(ex);
            return Ok(new
            {
                ok = false,
                canConnect = false,
                authMode = usesSqlAuth ? "SqlAuth" : "Windows",
                authHint,
                database = "CharacterSkills",
                sqlErrorNumber = sql?.Number,
                message = MapSqlMessage(sql, ex),
                detail = ex.Message,
            });
        }
    }

    private static SqlException? FindSqlException(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
            if (e is SqlException sql) return sql;
        return null;
    }

    private static string MapSqlMessage(SqlException? sql, Exception ex)
    {
        if (sql?.Number == 4060)
            return $"无法打开数据库 CharacterSkills。请在 SSMS 执行 Character/database/CharacterSkills/007_create_sql_login.sql（SQL 登录）或 006（Windows 登录）。";

        if (sql?.Number == 18456)
            return "SQL Server 登录失败，请检查连接字符串与账户权限。";

        return $"数据库异常：{ex.Message}";
    }
}

/**
 * AuthController.cs — 认证 API：注册与登录
 */
using FrontStudy.Api.DTOs;
using FrontStudy.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace FrontStudy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService authService, IHostEnvironment env) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new AuthResponse(false, "请求参数无效", null));

        try
        {
            var result = await authService.RegisterAsync(request, ct);
            if (!result.Success)
                return Conflict(result);
            return Ok(result);
        }
        catch (Exception ex) when (IsDatabaseError(ex))
        {
            return DatabaseError(ex);
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new AuthResponse(false, "请求参数无效", null));

        try
        {
            var result = await authService.LoginAsync(request, ct);
            if (!result.Success)
                return Unauthorized(result);
            return Ok(result);
        }
        catch (Exception ex) when (IsDatabaseError(ex))
        {
            return DatabaseError(ex);
        }
    }

    private static bool IsDatabaseError(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
            if (e is SqlException or InvalidOperationException) return true;
        return false;
    }

    private ActionResult<AuthResponse> DatabaseError(Exception ex)
    {
        var sql = FindSqlException(ex);
        var user = $"{Environment.UserDomainName}\\{Environment.UserName}";
        var message = sql?.Number == 4060
            ? $"数据库连接失败：账户 [{user}] 无法访问 CharacterSkills。请在 SSMS 执行 Character/database/CharacterSkills/007 或 006。"
            : $"数据库连接失败：{ex.InnerException?.Message ?? ex.Message}";

        if (env.IsDevelopment())
            return StatusCode(503, new AuthResponse(false, message, null));

        return StatusCode(503, new AuthResponse(false, "数据库暂时不可用，请稍后重试", null));
    }

    private static SqlException? FindSqlException(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
            if (e is SqlException sql) return sql;
        return null;
    }
}

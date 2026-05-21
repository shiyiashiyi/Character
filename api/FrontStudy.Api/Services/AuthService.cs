/**
 * AuthService.cs — 注册、登录与密码哈希
 */
using FrontStudy.Api.Data;
using FrontStudy.Api.DTOs;
using FrontStudy.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FrontStudy.Api.Services;

public class AuthService(AppDbContext db)
{
    private readonly PasswordHasher<User> _hasher = new();

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return new AuthResponse(false, "该邮箱已被注册", null);

        var user = new User
        {
            Email = email,
            PasswordHash = _hasher.HashPassword(null!, request.Password),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? email.Split('@')[0]
                : request.DisplayName.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return new AuthResponse(true, "注册成功", ToDto(user));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);

        if (user is null)
            return new AuthResponse(false, "邮箱或密码错误", null);

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return new AuthResponse(false, "邮箱或密码错误", null);

        user.LastLoginAtUtc = DateTime.UtcNow;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new AuthResponse(true, "登录成功", ToDto(user));
    }

    private static UserDto ToDto(User user) =>
        new(user.UserId, user.Email, user.DisplayName);
}

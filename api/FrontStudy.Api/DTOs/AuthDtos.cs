/**
 * AuthDtos.cs — 注册/登录请求与响应
 */
using System.ComponentModel.DataAnnotations;

namespace FrontStudy.Api.DTOs;

public record RegisterRequest(
    [Required][EmailAddress][MaxLength(256)] string Email,
    [Required][MinLength(8)][MaxLength(128)] string Password,
    [MaxLength(100)] string? DisplayName
);

public record LoginRequest(
    [Required][EmailAddress][MaxLength(256)] string Email,
    [Required][MinLength(8)][MaxLength(128)] string Password
);

public record UserDto(long UserId, string Email, string? DisplayName);

public record AuthResponse(bool Success, string? Message, UserDto? User);

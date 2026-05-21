/**
 * Program.cs — API 启动与依赖注入
 */
using FrontStudy.Api.Data;
using FrontStudy.Api.Options;
using FrontStudy.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("CharacterSkills"),
        sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<EmailVerificationOptions>(
    builder.Configuration.GetSection(EmailVerificationOptions.SectionName));
builder.Services.AddScoped<EmailSenderService>();
builder.Services.AddScoped<EmailVerificationService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<PersonaForgeService>();

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 12 * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 12 * 1024 * 1024;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("Frontend");
app.MapControllers();

app.Run();

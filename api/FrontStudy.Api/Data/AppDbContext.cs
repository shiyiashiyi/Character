/**
 * AppDbContext.cs — EF Core 数据上下文
 */
using FrontStudy.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FrontStudy.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<SavedCard> SavedCards => Set<SavedCard>();
    public DbSet<GenerationJob> GenerationJobs => Set<GenerationJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasColumnName("UserId");
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.EmailConfirmed).HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<EmailVerificationCode>(entity =>
        {
            entity.ToTable("EmailVerificationCodes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.CodeHash).HasMaxLength(128).IsRequired();
            entity.Property(e => e.AttemptCount).HasDefaultValue(0);
            entity.Property(e => e.IsConsumed).HasDefaultValue(false);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => new { e.Email, e.CreatedAtUtc });
        });

        modelBuilder.Entity<SavedCard>(entity =>
        {
            entity.ToTable("CharacterCards");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Slug).HasMaxLength(80).IsRequired();
            entity.Property(e => e.CharacterName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.WorkTitle).HasMaxLength(200);
            entity.Property(e => e.CardJson).IsRequired();
            entity.Property(e => e.SkillMarkdown).IsRequired();
            entity.Property(e => e.EvidenceMarkdown).IsRequired();
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.Slug);
        });

        modelBuilder.Entity<GenerationJob>(entity =>
        {
            entity.ToTable("GenerationJobs");
            entity.HasKey(e => e.JobId);
            entity.Property(e => e.JobId).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(16).IsRequired();
            entity.Property(e => e.CurrentStageKey).HasMaxLength(32);
            entity.Property(e => e.Message);
            entity.Property(e => e.ResultJson);
        });
    }
}

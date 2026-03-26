using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Data;

public class FojiDbContext(DbContextOptions<FojiDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<UserCompany> UserCompanies => Set<UserCompany>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentFile> AgentFiles => Set<AgentFile>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<AIModel> AIModels => Set<AIModel>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemAdminInvitation> SystemAdminInvitations => Set<SystemAdminInvitation>();
    public DbSet<DailyStat> DailyStats => Set<DailyStat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(255).IsRequired();
            e.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            e.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            e.Property(u => u.HashedPassword).HasMaxLength(500).IsRequired();
        });

        // Company
        modelBuilder.Entity<Company>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.Slug).IsUnique();
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.Property(c => c.TradeName).HasMaxLength(200);
            e.Property(c => c.Slug).HasMaxLength(100).IsRequired();
            e.Property(c => c.Description).HasMaxLength(1000);
            e.Property(c => c.AccountType).HasConversion<string>().HasMaxLength(20).HasDefaultValue(AccountType.Business);
            e.Property(c => c.CpfCnpj).HasMaxLength(14); // CPF=11, CNPJ=14 digits
            e.Property(c => c.AdminNotes).HasMaxLength(2000);
        });

        // UserCompany (composite key)
        modelBuilder.Entity<UserCompany>(e =>
        {
            e.HasKey(uc => new { uc.UserId, uc.CompanyId });
            e.Property(uc => uc.Role).HasConversion<string>().HasMaxLength(20);
            e.HasOne(uc => uc.User).WithMany(u => u.UserCompanies).HasForeignKey(uc => uc.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(uc => uc.Company).WithMany(c => c.UserCompanies).HasForeignKey(uc => uc.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });

        // Agent
        modelBuilder.Entity<Agent>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.AgentToken).IsUnique();
            e.Property(a => a.Name).HasMaxLength(200).IsRequired();
            e.Property(a => a.Description).HasMaxLength(1000);
            e.Property(a => a.SystemPrompt).IsRequired();
            e.Property(a => a.AgentToken).HasMaxLength(64).IsRequired();
            e.Property(a => a.IndustryType).HasConversion<string>().HasMaxLength(30);
            e.Property(a => a.AgentLanguage).HasConversion<string>().HasMaxLength(10);
            e.HasOne(a => a.Company).WithMany(c => c.Agents).HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });

        // Plan (updated: IsPublic, CustomForCompanyId)
        // (existing Plan config is below — we'll amend it)

        // AgentFile
        modelBuilder.Entity<AgentFile>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.FileName).HasMaxLength(500).IsRequired();
            e.Property(f => f.S3Key).HasMaxLength(1000).IsRequired();
            e.Property(f => f.ContentType).HasMaxLength(100).IsRequired();
            e.Property(f => f.ProcessingStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(f => f.S3RawTextKey).HasMaxLength(1000);
            e.Property(f => f.S3NormalizedTextKey).HasMaxLength(1000);
            e.Property(f => f.S3ChunksKey).HasMaxLength(1000);
            e.HasOne(f => f.Agent).WithMany(a => a.Files).HasForeignKey(f => f.AgentId).OnDelete(DeleteBehavior.Cascade);
        });

        // SystemAdminInvitation
        modelBuilder.Entity<SystemAdminInvitation>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => i.Token).IsUnique();
            e.Property(i => i.Email).HasMaxLength(255).IsRequired();
            e.Property(i => i.Token).HasMaxLength(36).IsRequired();
            e.HasOne(i => i.InvitedByUser)
                .WithMany(u => u.SentSystemAdminInvitations)
                .HasForeignKey(i => i.InvitedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Plan
        modelBuilder.Entity<Plan>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.Slug).IsUnique();
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.Property(p => p.Slug).HasMaxLength(50).IsRequired();
            e.Property(p => p.MonthlyPriceUsd).HasPrecision(10, 2);
            e.Property(p => p.IsPublic).HasDefaultValue(true);
            e.HasOne(p => p.CustomForCompany)
                .WithMany()
                .HasForeignKey(p => p.CustomForCompanyId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        // Subscription
        modelBuilder.Entity<Subscription>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.AdminNotes).HasMaxLength(1000);
            e.HasOne(s => s.Company).WithMany(c => c.Subscriptions).HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Plan).WithMany(p => p.Subscriptions).HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.AssignedByAdmin).WithMany().HasForeignKey(s => s.AssignedByAdminId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        });

        // Invitation
        modelBuilder.Entity<Invitation>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => i.Token).IsUnique();
            e.Property(i => i.Email).HasMaxLength(255).IsRequired();
            e.Property(i => i.Token).HasMaxLength(36).IsRequired();
            e.Property(i => i.Role).HasConversion<string>().HasMaxLength(20);
            e.HasOne(i => i.Company).WithMany(c => c.Invitations).HasForeignKey(i => i.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.InviterUser).WithMany(u => u.SentInvitations).HasForeignKey(i => i.InviterUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // AIModel
        modelBuilder.Entity<AIModel>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Name).HasMaxLength(100).IsRequired();
            e.Property(m => m.DisplayName).HasMaxLength(150).IsRequired();
            e.Property(m => m.ModelId).HasMaxLength(100).IsRequired();
            e.Property(m => m.Provider).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.InputCostPer1M).HasPrecision(10, 4);
            e.Property(m => m.OutputCostPer1M).HasPrecision(10, 4);
        });

        // DailyStat
        modelBuilder.Entity<DailyStat>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.CompanyId, d.StatDate }).IsUnique();
            e.Property(d => d.StatDate).HasColumnType("date").IsRequired();
            e.HasOne(d => d.Company).WithMany(c => c.DailyStats).HasForeignKey(d => d.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Action).HasMaxLength(100).IsRequired();
            e.Property(a => a.Resource).HasMaxLength(100).IsRequired();
            e.Property(a => a.ResourceId).HasMaxLength(50);
            e.Property(a => a.IpAddress).HasMaxLength(45);
            e.HasOne(a => a.Company).WithMany(c => c.AuditLogs).HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(a => a.User).WithMany(u => u.AuditLogs).HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.SetNull);
        });

    }

    public override int SaveChanges()
    {
        SetTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    // No seed data — Plans and AIModels are seeded manually via SQL scripts or the admin UI.
    // See /docs/seed.md for the reference INSERT statements.
}

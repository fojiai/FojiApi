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
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<ContactSubmission> ContactSubmissions => Set<ContactSubmission>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<HandoffEvent> HandoffEvents => Set<HandoffEvent>();
    public DbSet<AgentCalendarConnection> AgentCalendarConnections => Set<AgentCalendarConnection>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<ContactTag> ContactTags => Set<ContactTag>();
    public DbSet<Pipeline> Pipelines => Set<Pipeline>();
    public DbSet<PipelineStage> PipelineStages => Set<PipelineStage>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<DealStageHistory> DealStageHistory => Set<DealStageHistory>();

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
            e.Property(a => a.WelcomeMessage).HasMaxLength(500);
            e.Property(a => a.ConversationStarters).HasMaxLength(2000);
            e.Property(a => a.WidgetPrimaryColor).HasMaxLength(9);
            e.Property(a => a.WidgetTitle).HasMaxLength(100);
            e.Property(a => a.WidgetPlaceholder).HasMaxLength(200);
            e.Property(a => a.WidgetPosition).HasMaxLength(10);
            e.Property(a => a.ResponseStyle).HasMaxLength(20);
            e.Property(a => a.LeadCapturePrompt).HasMaxLength(500);
            e.Property(a => a.HandoffNotifyEmail).HasMaxLength(254);
            e.Property(a => a.HandoffNotifyWhatsApp).HasMaxLength(30);
            e.Property(a => a.HandoffMessage).HasMaxLength(500);
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
            e.Property(p => p.MonthlyPrice).HasPrecision(10, 2);
            e.Property(p => p.Currency).HasMaxLength(3).HasDefaultValue("USD");
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

        // PlatformSetting
        modelBuilder.Entity<PlatformSetting>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Key).HasMaxLength(100).IsRequired();
            e.HasIndex(s => s.Key).IsUnique();
            e.Property(s => s.Value).HasMaxLength(2000).IsRequired();
            e.Property(s => s.Label).HasMaxLength(200);
            e.Property(s => s.Category).HasMaxLength(50);
        });

        // Lead
        modelBuilder.Entity<Lead>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => new { l.CompanyId, l.CreatedAt });
            e.HasIndex(l => l.SessionId);
            e.Property(l => l.Name).HasMaxLength(200);
            e.Property(l => l.Email).HasMaxLength(254);
            e.Property(l => l.Phone).HasMaxLength(30);
            e.Property(l => l.SessionId).HasMaxLength(64).IsRequired();
            e.Property(l => l.Source).HasMaxLength(20).HasDefaultValue("widget");
            e.HasIndex(l => l.ContactId);
            e.HasOne(l => l.Agent).WithMany(a => a.Leads).HasForeignKey(l => l.AgentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.Company).WithMany(c => c.Leads).HasForeignKey(l => l.CompanyId).OnDelete(DeleteBehavior.Restrict);
            // Lead → Contact: preserve raw capture events when a contact is deleted/merged.
            e.HasOne(l => l.Contact).WithMany(c => c.Leads).HasForeignKey(l => l.ContactId).OnDelete(DeleteBehavior.SetNull);
        });

        // HandoffEvent
        modelBuilder.Entity<HandoffEvent>(e =>
        {
            e.HasKey(h => h.Id);
            e.HasIndex(h => new { h.CompanyId, h.CreatedAt });
            e.HasIndex(h => h.SessionId);
            e.Property(h => h.SessionId).HasMaxLength(64).IsRequired();
            e.Property(h => h.UserMessage).HasMaxLength(2000);
            e.Property(h => h.Source).HasMaxLength(20).HasDefaultValue("widget");
            e.HasOne(h => h.Agent).WithMany().HasForeignKey(h => h.AgentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.Company).WithMany(c => c.HandoffEvents).HasForeignKey(h => h.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        // ContactSubmission
        modelBuilder.Entity<ContactSubmission>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(100);
            e.Property(c => c.Email).HasMaxLength(200);
            e.Property(c => c.Category).HasMaxLength(50);
            e.Property(c => c.Subject).HasMaxLength(200);
            e.Property(c => c.Message).HasMaxLength(5000);
            e.Property(c => c.AdminNotes).HasMaxLength(2000);
            e.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // AgentCalendarConnection
        modelBuilder.Entity<AgentCalendarConnection>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.AgentId).IsUnique();
            e.Property(c => c.GoogleAccountEmail).HasMaxLength(254).IsRequired();
            e.Property(c => c.EncryptedRefreshToken).IsRequired();
            e.Property(c => c.IsActive).HasDefaultValue(true);
            e.HasOne(c => c.Agent).WithOne(a => a.CalendarConnection)
                .HasForeignKey<AgentCalendarConnection>(c => c.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
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

        // ── CRM ───────────────────────────────────────────────────────────────

        // Contact
        modelBuilder.Entity<Contact>(e =>
        {
            e.HasKey(c => c.Id);
            // Partial-unique dedup keys (race guard) — one contact per normalized email/phone per company.
            e.HasIndex(c => new { c.CompanyId, c.EmailNormalized })
                .IsUnique().HasFilter("\"EmailNormalized\" IS NOT NULL");
            e.HasIndex(c => new { c.CompanyId, c.PhoneNormalized })
                .IsUnique().HasFilter("\"PhoneNormalized\" IS NOT NULL");
            e.HasIndex(c => new { c.CompanyId, c.LastActivityAt });
            e.HasIndex(c => new { c.CompanyId, c.OwnerUserId });
            e.Property(c => c.Name).HasMaxLength(200);
            e.Property(c => c.Email).HasMaxLength(254);
            e.Property(c => c.Phone).HasMaxLength(30);
            e.Property(c => c.EmailNormalized).HasMaxLength(254);
            e.Property(c => c.PhoneNormalized).HasMaxLength(30);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Source).HasMaxLength(20);
            e.Property(c => c.EstimatedValue).HasPrecision(12, 2);
            e.Property(c => c.Notes).HasMaxLength(4000);
            e.HasOne(c => c.Company).WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.OwnerUser).WithMany().HasForeignKey(c => c.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
        });

        // ContactTag
        modelBuilder.Entity<ContactTag>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.ContactId, t.Tag }).IsUnique();
            e.HasIndex(t => new { t.CompanyId, t.Tag });
            e.Property(t => t.Tag).HasMaxLength(50).IsRequired();
            e.HasOne(t => t.Contact).WithMany(c => c.Tags).HasForeignKey(t => t.ContactId).OnDelete(DeleteBehavior.Cascade);
        });

        // Pipeline
        modelBuilder.Entity<Pipeline>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => new { p.CompanyId, p.IsDefault })
                .IsUnique().HasFilter("\"IsDefault\" = true");
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.HasOne(p => p.Company).WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });

        // PipelineStage
        modelBuilder.Entity<PipelineStage>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.PipelineId, s.SortOrder });
            e.Property(s => s.Name).HasMaxLength(100).IsRequired();
            e.HasOne(s => s.Pipeline).WithMany(p => p.Stages).HasForeignKey(s => s.PipelineId).OnDelete(DeleteBehavior.Cascade);
        });

        // Deal
        modelBuilder.Entity<Deal>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.CompanyId, d.PipelineId, d.StageId });
            e.HasIndex(d => new { d.CompanyId, d.Status });
            e.HasIndex(d => d.ContactId);
            e.Property(d => d.Title).HasMaxLength(200).IsRequired();
            e.Property(d => d.Value).HasPrecision(12, 2);
            e.Property(d => d.Currency).HasMaxLength(3).HasDefaultValue("BRL");
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(d => d.Company).WithMany().HasForeignKey(d => d.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Contact).WithMany(c => c.Deals).HasForeignKey(d => d.ContactId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Pipeline).WithMany().HasForeignKey(d => d.PipelineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Stage).WithMany().HasForeignKey(d => d.StageId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.OwnerUser).WithMany().HasForeignKey(d => d.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
        });

        // DealStageHistory
        modelBuilder.Entity<DealStageHistory>(e =>
        {
            e.HasKey(h => h.Id);
            e.HasIndex(h => new { h.DealId, h.CreatedAt });
            e.HasOne(h => h.Deal).WithMany(d => d.StageHistory).HasForeignKey(h => h.DealId).OnDelete(DeleteBehavior.Cascade);
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

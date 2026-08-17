using Amazon.S3;
using Amazon.SQS;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using FojiApi.Infrastructure.HostedServices;
using FojiApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Resend;

namespace FojiApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<FojiDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                   .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        // AWS
        services.AddDefaultAWSOptions(configuration.GetAWSOptions());
        services.AddAWSService<IAmazonS3>();
        services.AddAWSService<IAmazonSQS>();

        // Resend (transactional email)
        services.AddOptions();
        services.AddHttpClient<ResendClient>();
        services.Configure<ResendClientOptions>(options =>
        {
            options.ApiToken = configuration["Resend:ApiKey"]
                ?? throw new InvalidOperationException("Resend:ApiKey not configured");
        });
        services.AddTransient<IResend, ResendClient>();

        // HTTP client for Google OAuth calls
        services.AddHttpClient("GoogleOAuth");

        // Infrastructure services
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IIndustryPromptService, IndustryPromptService>();
        services.AddScoped<IPlanEnforcementService, PlanEnforcementService>();
        services.AddScoped<IStorageService, S3StorageService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddScoped<ICalendarConnectionService, CalendarConnectionService>();

        // Application services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IAIModelService, AIModelService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<ISystemAdminInvitationService, SystemAdminInvitationService>();
        services.AddScoped<IAdminCompanyService, AdminCompanyService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<ICrmAnalyticsService, CrmAnalyticsService>();
        services.AddScoped<IPlatformSettingService, PlatformSettingService>();
        services.AddScoped<IWhatsAppWebhookService, WhatsAppWebhookService>();
        services.AddScoped<IWhatsAppInboxService, WhatsAppInboxService>();
        services.AddScoped<IWhatsAppOnboardingService, WhatsAppOnboardingService>();
        services.AddScoped<IWhatsAppUsageService, WhatsAppUsageService>();
        // Keeps 60-day Embedded Signup tokens alive without the customer ever
        // knowing they expire.
        services.AddHostedService<BackgroundJobs.WhatsAppTokenRefreshJob>();
        services.AddScoped<ITrialExpiryService, TrialExpiryService>();
        services.AddScoped<ILeadService, LeadService>();
        services.AddScoped<IHandoffService, HandoffService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IPipelineService, PipelineService>();
        services.AddScoped<IDealService, DealService>();
        services.AddScoped<ICrmTaskService, CrmTaskService>();
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<ICrmEmailService, CrmEmailService>();

        // Daily background job for trial expiry checks and reminder emails
        services.AddHostedService<TrialExpiryHostedService>();

        return services;
    }
}

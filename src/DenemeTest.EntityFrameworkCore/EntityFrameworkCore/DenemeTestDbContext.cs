using DenemeTest.Exams;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace DenemeTest.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class DenemeTestDbContext :
    AbpDbContext<DenemeTestDbContext>,
    ITenantManagementDbContext,
    IIdentityDbContext
{
    /* Aggregate Roots / Entities */
    public DbSet<Test> Tests { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<QuestionOption> QuestionOptions { get; set; }
    public DbSet<CodeTestCase> CodeTestCases { get; set; }

    public DbSet<Candidate> Candidates { get; set; }
    public DbSet<ExamInvitation> ExamInvitations { get; set; }
    public DbSet<ExamSession> ExamSessions { get; set; }
    public DbSet<Answer> Answers { get; set; }
    public DbSet<ProctoringEvent> ProctoringEvents { get; set; }
    public DbSet<Score> Scores { get; set; }

    #region Entities from the modules

    // Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }

    // Tenant Management
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

    #endregion

    public DenemeTestDbContext(DbContextOptions<DenemeTestDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */
        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureFeatureManagement();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureTenantManagement();
        builder.ConfigureBlobStoring();

        /* Exams domain mappings */
        builder.Entity<Test>(b =>
        {
            b.ToTable("AppTests");
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.ShuffleQuestions);
            b.Property(x => x.ShuffleOptions);

            // Yeni alanlar
            b.Property(x => x.DurationMinutes)
                .IsRequired()
                .HasDefaultValue(60);

            b.Property(x => x.PassScore)
                .IsRequired()
                .HasDefaultValue(50);

            b.HasMany(x => x.Questions)
             .WithOne()
             .HasForeignKey(q => q.TestId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Question>(b =>
        {
            b.ToTable("AppQuestions");
            b.ConfigureByConvention();
            b.Property(x => x.Text).IsRequired().HasMaxLength(4000);
            b.Property(x => x.Points).HasDefaultValue(1);
            b.Property(x => x.Type).IsRequired();
            b.HasMany(x => x.Options)
             .WithOne()
             .HasForeignKey(o => o.QuestionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<QuestionOption>(b =>
        {
            b.ToTable("AppQuestionOptions");
            b.ConfigureByConvention();
            b.Property(x => x.Text).IsRequired().HasMaxLength(2000);
            b.Property(x => x.IsCorrect).IsRequired();
        });

        builder.Entity<Candidate>(b =>
        {
            b.ToTable("AppCandidates");
            b.ConfigureByConvention();

            b.Property(x => x.FirstName).IsRequired().HasMaxLength(128);
            b.Property(x => x.LastName).IsRequired().HasMaxLength(128);
            b.Property(x => x.Email).IsRequired().HasMaxLength(256);

            // Yeni alan
            b.Property(x => x.Status)
             .IsRequired()
             .HasMaxLength(64)
             .HasDefaultValue("Pending");

            b.HasIndex(x => x.Email).IsUnique();
        });

        builder.Entity<ExamInvitation>(b =>
        {
            b.ToTable("AppExamInvitations");
            b.ConfigureByConvention();

            b.Property(x => x.Token)
             .IsRequired()
             .HasMaxLength(64);

            b.Property(x => x.ExpireAt)
             .IsRequired();

            b.Property(x => x.SentAt);
            b.Property(x => x.UsedAt);

            b.Property(x => x.IsUsed)
             .IsRequired()
             .HasDefaultValue(false);

            b.HasIndex(x => x.Token).IsUnique();
            b.HasIndex(x => new { x.CandidateId, x.TestId });

            b.HasOne<Candidate>()
             .WithMany()
             .HasForeignKey(x => x.CandidateId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<Test>()
             .WithMany()
             .HasForeignKey(x => x.TestId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ExamSession>(b =>
        {
            b.ToTable("AppExamSessions");
            b.ConfigureByConvention();
            b.Property(x => x.IsCancelled).HasDefaultValue(false);
        });

        builder.Entity<Answer>(b =>
        {
            b.ToTable("AppAnswers");
            b.ConfigureByConvention();
            b.Property(x => x.TextAnswer);
            // PostgreSQL jsonb
            b.Property(x => x.SelectedOptionIds)
             .HasColumnType("jsonb");
        });

        builder.Entity<ProctoringEvent>(b =>
        {
            b.ToTable("AppProctoringEvents");
            b.ConfigureByConvention();
            b.Property(x => x.Type).IsRequired();
            b.Property(x => x.Detail).HasMaxLength(2000);
        });

        builder.Entity<Score>(b =>
        {
            b.ToTable("AppScores");
            b.ConfigureByConvention();
            b.Property(x => x.Value).IsRequired();
            b.Property(x => x.Explanation).HasMaxLength(4000);
        });

        /* your custom tables... */
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SteamIRL.Api.Features.Accounts;
using SteamIRL.Api.Features.Activities;
using SteamIRL.Api.Features.Locations;
using SteamIRL.Api.Features.Sessions;
using SteamIRL.Api.Features.Moderation;

namespace SteamIRL.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<PublicProfile> PublicProfiles => Set<PublicProfile>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<LocationActivity> LocationActivities => Set<LocationActivity>();
    public DbSet<GameSession> Sessions => Set<GameSession>();
    public DbSet<Participation> Participations => Set<Participation>();
    public DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();
    public DbSet<LocationSubmission> LocationSubmissions => Set<LocationSubmission>();
    public DbSet<SubmissionActivity> SubmissionActivities => Set<SubmissionActivity>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<ModerationDecision> ModerationDecisions => Set<ModerationDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new LocationConfiguration());
        modelBuilder.ApplyConfiguration(new LocationActivityConfiguration());
        modelBuilder.ApplyConfiguration(new SessionConfiguration());
        modelBuilder.ApplyConfiguration(new ParticipationConfiguration());
        modelBuilder.ApplyConfiguration(new IdempotencyConfiguration());
        modelBuilder.ApplyConfiguration(new SubmissionConfiguration());
        modelBuilder.ApplyConfiguration(new SubmissionActivityConfiguration());
        modelBuilder.ApplyConfiguration(new ReportConfiguration());
        modelBuilder.ApplyConfiguration(new DecisionConfiguration());
        modelBuilder.Entity<IdentityRole<Guid>>().HasData(new IdentityRole<Guid> { Id = ModeratorAccess.RoleId, Name = "Moderator", NormalizedName = "MODERATOR", ConcurrencyStamp = "moderator-role-v1" });
        modelBuilder.Entity<ApplicationUser>().HasIndex(user => user.NormalizedEmail).IsUnique();
        modelBuilder.Entity<ApplicationUser>().HasOne(user => user.Profile).WithOne()
            .HasForeignKey<PublicProfile>(profile => profile.UserId).OnDelete(DeleteBehavior.Cascade);
        var profile = modelBuilder.Entity<PublicProfile>();
        profile.ToTable("public_profiles");
        profile.HasKey(item => item.UserId);
        profile.Property(item => item.DisplayName).HasMaxLength(60).IsRequired();
        var activity = modelBuilder.Entity<Activity>();
        activity.ToTable("activities", table => table.HasCheckConstraint(
            "ck_activities_slug", "slug ~ '^[a-z]+(-[a-z]+)*$'"));
        activity.HasKey(item => item.Id);
        activity.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        activity.Property(item => item.Slug).HasColumnName("slug").HasMaxLength(80).IsRequired();
        activity.Property(item => item.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        activity.Property(item => item.Description).HasColumnName("description").HasMaxLength(2000).IsRequired();
        activity.HasIndex(item => item.Slug).IsUnique();
        activity.HasData(
            new Activity { Id = Guid.Parse("11111111-1111-4111-8111-111111111111"), Slug = "volleyball", Name = "Волейбол", Description = "Найди площадку и компанию для игры в волейбол." },
            new Activity { Id = Guid.Parse("22222222-2222-4222-8222-222222222222"), Slug = "basketball", Name = "Баскетбол", Description = "Собери команду для игры на открытой площадке." },
            new Activity { Id = Guid.Parse("33333333-3333-4333-8333-333333333333"), Slug = "football", Name = "Футбол", Description = "Планируй игру и находи недостающих игроков." },
            new Activity { Id = Guid.Parse("44444444-4444-4444-8444-444444444444"), Slug = "table-tennis", Name = "Настольный теннис", Description = "Найди место для партии и партнёра по игре." });
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SteamIRL.Api.Features.Accounts;

namespace SteamIRL.Api.Features.Sessions;

public sealed class SessionConfiguration : IEntityTypeConfiguration<GameSession>
{
    public void Configure(EntityTypeBuilder<GameSession> session)
    {
        session.ToTable("sessions", table =>
        {
            table.HasCheckConstraint("ck_session_capacity", "\"Capacity\" BETWEEN 1 AND 30");
            table.HasCheckConstraint("ck_session_time", "\"EndsAt\" > \"StartsAt\"");
            table.HasCheckConstraint("ck_session_kind", "\"Kind\" IN ('Planned', 'Immediate')");
            table.HasCheckConstraint("ck_session_level", "\"SkillLevel\" IN ('Beginner', 'Intermediate', 'Advanced')");
            table.HasCheckConstraint("ck_session_equipment", "\"EquipmentStatus\" IN ('NotSpecified', 'BallAvailable', 'BallNeeded')");
        });
        session.HasKey(item => item.Id);
        session.Property(item => item.Id).ValueGeneratedNever();
        session.Property(item => item.Kind).HasConversion<string>().HasMaxLength(16);
        session.Property(item => item.SkillLevel).HasConversion<string>().HasMaxLength(16);
        session.Property(item => item.EquipmentStatus).HasConversion<string>().HasMaxLength(20);
        session.Property(item => item.CancellationReason).HasMaxLength(1000);
        session.HasOne(item => item.PlaceActivity).WithMany().HasForeignKey(item => new { item.LocationId, item.ActivityId }).OnDelete(DeleteBehavior.Restrict);
        session.HasOne(item => item.Organiser).WithMany().HasForeignKey(item => item.OrganiserId).OnDelete(DeleteBehavior.Restrict);
        session.HasIndex(item => new { item.ActivityId, item.EndsAt });
        session.HasIndex(item => new { item.LocationId, item.EndsAt });
        session.HasIndex(item => new { item.OrganiserId, item.CreatedAt });
    }
}

public sealed class ParticipationConfiguration : IEntityTypeConfiguration<Participation>
{
    public void Configure(EntityTypeBuilder<Participation> participation)
    {
        participation.ToTable("participations", table => table.HasCheckConstraint("ck_participation_time", "\"LeftAt\" IS NULL OR \"LeftAt\" >= \"JoinedAt\""));
        participation.HasKey(item => new { item.SessionId, item.UserId });
        participation.HasOne(item => item.Session).WithMany(item => item.Participations).HasForeignKey(item => item.SessionId).OnDelete(DeleteBehavior.Restrict);
        participation.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        participation.HasIndex(item => new { item.UserId, item.LeftAt });
    }
}

public sealed class IdempotencyConfiguration : IEntityTypeConfiguration<IdempotencyRequest>
{
    public void Configure(EntityTypeBuilder<IdempotencyRequest> request)
    {
        request.ToTable("idempotency_requests");
        request.HasKey(item => item.Id);
        request.Property(item => item.Key).HasMaxLength(128);
        request.Property(item => item.Operation).HasMaxLength(100);
        request.Property(item => item.RequestHash).HasMaxLength(64);
        request.Property(item => item.ResponseJson).HasColumnType("jsonb");
        request.Property(item => item.ContentType).HasMaxLength(80);
        request.Property(item => item.ResourceLocation).HasMaxLength(200);
        request.HasIndex(item => new { item.UserId, item.Key }).IsUnique();
        request.HasIndex(item => item.ExpiresAt);
        request.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

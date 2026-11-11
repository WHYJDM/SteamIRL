using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SteamIRL.Api.Features.Moderation;

public sealed class SubmissionConfiguration : IEntityTypeConfiguration<LocationSubmission>
{
    public void Configure(EntityTypeBuilder<LocationSubmission> item)
    {
        item.ToTable("location_submissions", table =>
        {
            table.HasCheckConstraint("ck_submission_state", "\"State\" IN ('Pending','Approved','Rejected')");
            table.HasCheckConstraint("ck_submission_kind", "\"Kind\" IN ('New','Correction')");
            table.HasCheckConstraint("ck_submission_coordinates", "\"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180");
            table.HasCheckConstraint("ck_submission_revision", "\"BaseRevision\" >= 1");
        });
        item.HasKey(value => value.Id);
        item.Property(value => value.Kind).HasConversion<string>().HasMaxLength(16);
        item.Property(value => value.State).HasConversion<string>().HasMaxLength(16);
        item.Property(value => value.Name).HasMaxLength(200);
        item.Property(value => value.Description).HasMaxLength(4000);
        item.Property(value => value.Address).HasMaxLength(300);
        item.Property(value => value.Latitude).HasPrecision(9, 6);
        item.Property(value => value.Longitude).HasPrecision(9, 6);
        item.Property(value => value.SourceUrl).HasMaxLength(1000);
        item.Property(value => value.CoordinateNote).HasMaxLength(500);
        item.Property(value => value.AccessNotes).HasMaxLength(2000);
        item.Property(value => value.Evidence).HasMaxLength(2000);
        item.Property(value => value.Fingerprint).HasMaxLength(64);
        item.Property(value => value.OriginalDataJson).HasColumnType("jsonb");
        item.HasOne(value => value.Author).WithMany().HasForeignKey(value => value.AuthorId).OnDelete(DeleteBehavior.Restrict);
        item.HasOne(value => value.Location).WithMany().HasForeignKey(value => value.LocationId).OnDelete(DeleteBehavior.Restrict);
        item.HasIndex(value => new { value.State, value.SubmittedAt });
        item.HasIndex(value => new { value.AuthorId, value.SubmittedAt });
        item.HasIndex(value => new { value.AuthorId, value.Fingerprint }).IsUnique().HasFilter("\"State\" = 'Pending'");
        item.HasIndex(value => value.Fingerprint).IsUnique().HasFilter("\"Kind\" = 'New' AND \"State\" = 'Approved'");
    }
}

public sealed class SubmissionActivityConfiguration : IEntityTypeConfiguration<SubmissionActivity>
{
    public void Configure(EntityTypeBuilder<SubmissionActivity> item)
    {
        item.ToTable("submission_activities"); item.HasKey(value => new { value.SubmissionId, value.ActivityId });
        item.HasOne(value => value.Submission).WithMany(value => value.Activities).HasForeignKey(value => value.SubmissionId).OnDelete(DeleteBehavior.Restrict);
        item.HasOne(value => value.Activity).WithMany().HasForeignKey(value => value.ActivityId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> item)
    {
        item.ToTable("content_reports", table =>
        {
            table.HasCheckConstraint("ck_report_target", "(\"LocationId\" IS NOT NULL) <> (\"SessionId\" IS NOT NULL)");
            table.HasCheckConstraint("ck_report_state", "\"State\" IN ('Open','Resolved','Dismissed')");
            table.HasCheckConstraint("ck_report_reason", "\"Reason\" IN ('Inaccurate','Spam','Abuse','AccessIssue','Safety','Other')");
        });
        item.HasKey(value => value.Id);
        item.Property(value => value.Reason).HasConversion<string>().HasMaxLength(20);
        item.Property(value => value.State).HasConversion<string>().HasMaxLength(16);
        item.Property(value => value.Details).HasMaxLength(2000);
        item.HasOne(value => value.Author).WithMany().HasForeignKey(value => value.AuthorId).OnDelete(DeleteBehavior.Restrict);
        item.HasOne(value => value.Location).WithMany().HasForeignKey(value => value.LocationId).OnDelete(DeleteBehavior.Restrict);
        item.HasOne(value => value.Session).WithMany().HasForeignKey(value => value.SessionId).OnDelete(DeleteBehavior.Restrict);
        item.HasIndex(value => new { value.AuthorId, value.LocationId, value.Reason }).IsUnique().HasFilter("\"State\" = 'Open' AND \"LocationId\" IS NOT NULL");
        item.HasIndex(value => new { value.AuthorId, value.SessionId, value.Reason }).IsUnique().HasFilter("\"State\" = 'Open' AND \"SessionId\" IS NOT NULL");
        item.HasIndex(value => new { value.State, value.SubmittedAt });
        item.HasIndex(value => new { value.AuthorId, value.SubmittedAt });
    }
}

public sealed class DecisionConfiguration : IEntityTypeConfiguration<ModerationDecision>
{
    public void Configure(EntityTypeBuilder<ModerationDecision> item)
    {
        item.ToTable("moderation_decisions", table =>
        {
            table.HasCheckConstraint("ck_decision_target", "(\"SubmissionId\" IS NOT NULL) <> (\"ReportId\" IS NOT NULL)");
            table.HasCheckConstraint("ck_decision_reason", "length(trim(\"Reason\")) >= 10");
            table.HasCheckConstraint("ck_decision_action", "(\"SubmissionId\" IS NOT NULL AND \"Action\" IN ('Approve','Reject')) OR (\"ReportId\" IS NOT NULL AND \"Action\" IN ('Resolve','Dismiss','RemoveContent'))");
        });
        item.HasKey(value => value.Id);
        item.Property(value => value.Action).HasConversion<string>().HasMaxLength(20);
        item.Property(value => value.Reason).HasMaxLength(1000);
        item.Property(value => value.BeforeJson).HasColumnType("jsonb");
        item.Property(value => value.AfterJson).HasColumnType("jsonb");
        item.HasOne(value => value.Moderator).WithMany().HasForeignKey(value => value.ModeratorId).OnDelete(DeleteBehavior.Restrict);
        item.HasOne(value => value.Submission).WithMany(value => value.Decisions).HasForeignKey(value => value.SubmissionId).OnDelete(DeleteBehavior.Restrict);
        item.HasOne(value => value.Report).WithMany(value => value.Decisions).HasForeignKey(value => value.ReportId).OnDelete(DeleteBehavior.Restrict);
        item.HasIndex(value => value.SubmissionId).IsUnique().HasFilter("\"SubmissionId\" IS NOT NULL");
        item.HasIndex(value => value.ReportId).IsUnique().HasFilter("\"ReportId\" IS NOT NULL");
    }
}

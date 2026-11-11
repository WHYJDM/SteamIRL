using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SteamIRL.Api.Features.Locations;

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> location)
    {
        location.ToTable("locations", table =>
        {
            table.HasCheckConstraint("ck_location_latitude", "\"Latitude\" BETWEEN -90 AND 90");
            table.HasCheckConstraint("ck_location_longitude", "\"Longitude\" BETWEEN -180 AND 180");
            table.HasCheckConstraint("ck_location_status", "\"ApprovalStatus\" IN ('Pending', 'Approved', 'Rejected', 'Removed')");
            table.HasCheckConstraint("ck_location_name", "length(trim(\"Name\")) > 0");
            table.HasCheckConstraint("ck_location_revision", "\"Revision\" >= 1");
        });
        location.HasKey(item => item.Id);
        location.Property(item => item.Id).ValueGeneratedNever();
        location.Property(item => item.Revision).HasDefaultValue(1L);
        location.Property(item => item.Name).HasMaxLength(200).IsRequired();
        location.Property(item => item.Description).HasMaxLength(4000).IsRequired();
        location.Property(item => item.Address).HasMaxLength(300).IsRequired();
        location.Property(item => item.Latitude).HasPrecision(9, 6);
        location.Property(item => item.Longitude).HasPrecision(9, 6);
        location.Property(item => item.ApprovalStatus).HasConversion<string>().HasMaxLength(16);
        location.Property(item => item.SourceUrl).HasMaxLength(1000).IsRequired();
        location.Property(item => item.CoordinateNote).HasMaxLength(500).IsRequired();
        location.Property(item => item.AccessNotes).HasMaxLength(2000).IsRequired();
        location.HasIndex(item => new { item.ApprovalStatus, item.Name, item.Id });
        LocationSeed.Configure(location);
    }
}

public sealed class LocationActivityConfiguration : IEntityTypeConfiguration<LocationActivity>
{
    public void Configure(EntityTypeBuilder<LocationActivity> link)
    {
        link.ToTable("location_activities");
        link.Property(item => item.IsEnabled).HasDefaultValue(true);
        link.HasKey(item => new { item.LocationId, item.ActivityId });
        link.HasOne(item => item.Location).WithMany(item => item.Activities).HasForeignKey(item => item.LocationId).OnDelete(DeleteBehavior.Restrict);
        link.HasOne(item => item.Activity).WithMany().HasForeignKey(item => item.ActivityId).OnDelete(DeleteBehavior.Restrict);
        link.HasIndex(item => new { item.ActivityId, item.LocationId });
        link.HasData(LocationSeed.Links);
    }
}

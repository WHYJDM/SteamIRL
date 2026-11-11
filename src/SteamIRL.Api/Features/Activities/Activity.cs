namespace SteamIRL.Api.Features.Activities;

public sealed class Activity
{
    public Guid Id { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
}

using Microsoft.AspNetCore.Identity;

namespace SteamIRL.Api.Features.Accounts;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public PublicProfile Profile { get; set; } = null!;
}

public sealed class PublicProfile
{
    public Guid UserId { get; set; }
    public required string DisplayName { get; set; }
}

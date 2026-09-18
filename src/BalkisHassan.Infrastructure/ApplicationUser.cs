using Microsoft.AspNetCore.Identity;

namespace BalkisHassan.Infrastructure;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}

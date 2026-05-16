using Microsoft.AspNetCore.Identity;

namespace UsersAPI.Domain.Entity;

public class AppUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; } = string.Empty;
}
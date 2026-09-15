using Microsoft.AspNetCore.Identity;
namespace UCredit.Infrastructure.Identity.Models;
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<UserTenantMembership> Memberships { get; } = new List<UserTenantMembership>();
}

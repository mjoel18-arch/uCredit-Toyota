namespace UCredit.Infrastructure.Identity.Models;
public sealed class UserTenantMembership
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public string? LegacyUserCode { get; set; }
    public ICollection<MembershipPermission> Permissions { get; } = new List<MembershipPermission>();
}

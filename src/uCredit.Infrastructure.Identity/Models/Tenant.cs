namespace UCredit.Infrastructure.Identity.Models;
public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<UserTenantMembership> Memberships { get; } = new List<UserTenantMembership>();
}

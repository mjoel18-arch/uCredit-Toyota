#pragma warning disable CA1711
namespace UCredit.Infrastructure.Identity.Models;
public sealed class MembershipPermission
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public UserTenantMembership Membership { get; set; } = null!;
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

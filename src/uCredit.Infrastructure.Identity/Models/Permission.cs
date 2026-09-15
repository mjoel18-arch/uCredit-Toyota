#pragma warning disable CA1711
namespace UCredit.Infrastructure.Identity.Models;
public sealed class Permission
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ICollection<MembershipPermission> Memberships { get; } = new List<MembershipPermission>();
}

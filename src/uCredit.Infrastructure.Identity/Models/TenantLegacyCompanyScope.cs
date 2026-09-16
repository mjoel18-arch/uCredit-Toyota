namespace UCredit.Infrastructure.Identity.Models;

public sealed class TenantLegacyCompanyScope
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int CompanyId { get; set; }
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
}
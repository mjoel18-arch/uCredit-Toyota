using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UCredit.Infrastructure.Identity.Models;
namespace UCredit.Infrastructure.Identity;
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserTenantMembership> UserTenantMemberships => Set<UserTenantMembership>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<MembershipPermission> MembershipPermissions => Set<MembershipPermission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Tenant>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
        });
        builder.Entity<UserTenantMembership>(e =>
        {
            e.HasKey(x => new { x.UserId, x.TenantId });
            e.HasOne(x => x.User).WithMany(x => x.Memberships).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Tenant).WithMany(x => x.Memberships).HasForeignKey(x => x.TenantId);
        });
        builder.Entity<Permission>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(128).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
        });
        builder.Entity<MembershipPermission>(e =>
        {
            e.HasKey(x => new { x.UserId, x.TenantId, x.PermissionId });
            e.HasOne(x => x.Membership).WithMany(x => x.Permissions)
                .HasForeignKey(x => new { x.UserId, x.TenantId });
            e.HasOne(x => x.Permission).WithMany(x => x.Memberships).HasForeignKey(x => x.PermissionId);
        });
    }
}

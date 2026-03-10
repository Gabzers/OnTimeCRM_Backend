using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configuration;

public class MenuItemPermissionConfiguration : IEntityTypeConfiguration<MenuItemPermission>
{
    public void Configure(EntityTypeBuilder<MenuItemPermission> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.RouteKey).HasMaxLength(100).IsRequired();

        // Each role can have at most one permission row per route
        builder.HasIndex(p => new { p.Role, p.RouteKey }).IsUnique();
    }
}

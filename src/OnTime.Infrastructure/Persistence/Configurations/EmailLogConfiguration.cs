using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTime.Domain.Entities;

namespace OnTime.Infrastructure.Persistence.Configurations;

public class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ToEmail).IsRequired().HasMaxLength(320);
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(300);
        builder.Property(x => x.EmailType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);

        // Listing is always "most recent first" — no FK to User, an email log must survive the
        // recipient being deleted, and the recipient's address is already on the row itself.
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.EmailType);
    }
}

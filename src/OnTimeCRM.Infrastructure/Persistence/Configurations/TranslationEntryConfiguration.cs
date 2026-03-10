using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class TranslationEntryConfiguration : IEntityTypeConfiguration<TranslationEntry>
{
    public void Configure(EntityTypeBuilder<TranslationEntry> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Locale).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => new { x.Key, x.Locale }).IsUnique();
    }
}

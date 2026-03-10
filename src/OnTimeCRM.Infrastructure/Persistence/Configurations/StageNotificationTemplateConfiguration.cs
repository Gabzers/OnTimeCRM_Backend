using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class StageNotificationTemplateConfiguration : IEntityTypeConfiguration<StageNotificationTemplate>
{
    public void Configure(EntityTypeBuilder<StageNotificationTemplate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.Stage)
            .WithMany(x => x.Templates)
            .HasForeignKey(x => x.StageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

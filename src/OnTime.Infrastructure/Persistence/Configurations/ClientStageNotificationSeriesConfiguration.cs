using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTime.Domain.Entities;

namespace OnTime.Infrastructure.Persistence.Configurations;

public class ClientStageNotificationSeriesConfiguration : IEntityTypeConfiguration<ClientStageNotificationSeries>
{
    public void Configure(EntityTypeBuilder<ClientStageNotificationSeries> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.ClientStageHistory)
            .WithMany()
            .HasForeignKey(x => x.ClientStageHistoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Template)
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        // One series per (history, template) pair — a template can't double-fire on the same stay.
        builder.HasIndex(x => new { x.ClientStageHistoryId, x.TemplateId }).IsUnique();

        // The cron job's due-series scan filters on IsActive — keep it indexed.
        builder.HasIndex(x => x.IsActive);
    }
}

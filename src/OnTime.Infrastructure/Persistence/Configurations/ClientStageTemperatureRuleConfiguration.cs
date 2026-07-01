using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTime.Domain.Entities;

namespace OnTime.Infrastructure.Persistence.Configurations;

public class ClientStageTemperatureRuleConfiguration : IEntityTypeConfiguration<ClientStageTemperatureRule>
{
    public void Configure(EntityTypeBuilder<ClientStageTemperatureRule> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Stage)
            .WithMany(x => x.TemperatureRules)
            .HasForeignKey(x => x.StageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.StageId, x.DaysAfterEntry });
    }
}

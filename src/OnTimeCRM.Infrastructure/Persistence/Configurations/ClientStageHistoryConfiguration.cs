using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class ClientStageHistoryConfiguration : IEntityTypeConfiguration<ClientStageHistory>
{
    public void Configure(EntityTypeBuilder<ClientStageHistory> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Obs).HasMaxLength(1000);
        builder.Property(x => x.ProposalSnapshot).HasMaxLength(4000);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.Client)
            .WithMany(x => x.StageHistory)
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FromStage)
            .WithMany()
            .HasForeignKey(x => x.FromStageId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(x => x.ToStage)
            .WithMany()
            .HasForeignKey(x => x.ToStageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

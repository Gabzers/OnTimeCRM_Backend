using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class ProposalConfiguration : IEntityTypeConfiguration<Proposal>
{
    public void Configure(EntityTypeBuilder<Proposal> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProposalValue).HasPrecision(18, 2);
        builder.Property(x => x.Discount).HasPrecision(18, 2);
        builder.Property(x => x.TradeInEstimatedValue).HasPrecision(18, 2);
        builder.Property(x => x.TradeInPlate).HasMaxLength(20);
        builder.Property(x => x.TradeInBrand).HasMaxLength(100);
        builder.Property(x => x.TradeInModel).HasMaxLength(100);
        builder.Property(x => x.LossNotes).HasMaxLength(1000);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        // ProposalDate is nullable — user-controlled business date, no DB default
        builder.Property(x => x.ProposalDate).IsRequired(false);

        builder.HasOne(x => x.Client)
            .WithMany(x => x.Proposals)
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

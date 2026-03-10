using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FinalValue).IsRequired().HasPrecision(18, 2);
        builder.Property(x => x.Commission).HasPrecision(18, 2);
        builder.Property(x => x.Plate).HasMaxLength(20);
        builder.Property(x => x.Chassis).HasMaxLength(50);
        builder.Property(x => x.FreeTextModel).HasMaxLength(200);
        builder.Property(x => x.Obs).HasMaxLength(1000);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        // SoldAt is required and must NEVER have a DB default — always comes from the request
        builder.Property(x => x.SoldAt).IsRequired();

        builder.HasOne(x => x.Proposal)
            .WithMany(x => x.Sales)
            .HasForeignKey(x => x.ProposalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Client)
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Model)
            .WithMany()
            .HasForeignKey(x => x.ModelId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class ProposalVehicleConfiguration : IEntityTypeConfiguration<ProposalVehicle>
{
    public void Configure(EntityTypeBuilder<ProposalVehicle> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FreeTextModel).HasMaxLength(200);
        builder.Property(x => x.Obs).HasMaxLength(1000);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.Discount).HasPrecision(18, 2);

        builder.HasOne(x => x.Proposal)
            .WithMany(x => x.Vehicles)
            .HasForeignKey(x => x.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Model)
            .WithMany(x => x.ProposalVehicles)
            .HasForeignKey(x => x.ModelId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(x => x.Version)
            .WithMany()
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}

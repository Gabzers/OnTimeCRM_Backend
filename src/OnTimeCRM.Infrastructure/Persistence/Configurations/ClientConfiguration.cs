using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.Property(x => x.TaxId).HasMaxLength(20);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Clients)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CurrentStage)
            .WithMany(x => x.Clients)
            .HasForeignKey(x => x.CurrentStageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

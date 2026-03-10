using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class UserSubscriptionPaymentConfiguration : IEntityTypeConfiguration<UserSubscriptionPayment>
{
    public void Configure(EntityTypeBuilder<UserSubscriptionPayment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("EUR");
        builder.Property(x => x.StripePaymentIntentId).HasMaxLength(200);
        builder.Property(x => x.StripeInvoiceId).HasMaxLength(200);
        builder.Property(x => x.IfthenpayReference).HasMaxLength(50);
        builder.Property(x => x.IfthenpayMBWayAlias).HasMaxLength(50);
        builder.Property(x => x.IfthenpayTransactionId).HasMaxLength(100);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.User)
            .WithMany(x => x.SubscriptionPayments)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configuration;

public class UserGoalConfiguration : IEntityTypeConfiguration<UserGoal>
{
    public void Configure(EntityTypeBuilder<UserGoal> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.TargetValue).HasPrecision(18, 4);

        builder.HasOne(g => g.User)
               .WithMany()
               .HasForeignKey(g => g.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => new { g.UserId, g.MetricType, g.StartDate });
    }
}

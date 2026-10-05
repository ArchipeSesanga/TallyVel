using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Data;

public sealed class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.HasIndex(p => p.ContributionCycleId).IsUnique();

        builder.HasOne<ContributionCycle>()
            .WithMany()
            .HasForeignKey(p => new { p.StokvelId, p.ContributionCycleId })
            .HasPrincipalKey(c => new { c.StokvelId, c.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // The link to the recipient's StokvelMember is configured from that
        // side, in StokvelMemberConfiguration.
    }
}

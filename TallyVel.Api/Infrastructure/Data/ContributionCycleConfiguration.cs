using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Data;

public sealed class ContributionCycleConfiguration : IEntityTypeConfiguration<ContributionCycle>
{
    public void Configure(EntityTypeBuilder<ContributionCycle> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Label).HasMaxLength(ContributionCycle.MaxLabelLength);
        builder.HasAlternateKey(c => new { c.StokvelId, c.Label });
        // Lets Payout reference (StokvelId, Id) so a payout's stokvel
        // must match its cycle's stokvel.
        builder.HasAlternateKey(c => new { c.StokvelId, c.Id });
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Stokvel>()
            .WithMany()
            .HasForeignKey(c => c.StokvelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

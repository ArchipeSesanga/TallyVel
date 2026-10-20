using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain;

namespace Infrastructure.Data;

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
        // Status is worked out from IsPaidOut and ClosedAt (ContributionCycle.StatusOf), never stored.
        builder.Ignore(c => c.Status);

        // One cycle per start date per stokvel; the cycle-close job scans by
        // (EndsOn, ClosedAt).
        builder.HasIndex(c => new { c.StokvelId, c.StartsOn }).IsUnique();
        builder.HasIndex(c => new { c.EndsOn, c.ClosedAt });
        builder.ToTable(t => t.HasCheckConstraint("CK_ContributionCycles_Dates", "\"EndsOn\" >= \"StartsOn\""));

        builder.HasOne<Stokvel>()
            .WithMany()
            .HasForeignKey(c => c.StokvelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

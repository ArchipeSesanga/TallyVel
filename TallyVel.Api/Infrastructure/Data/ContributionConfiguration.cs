using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Data;

public sealed class ContributionConfiguration : IEntityTypeConfiguration<Contribution>
{
    public void Configure(EntityTypeBuilder<Contribution> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Amount).HasPrecision(18, 2);
        builder.Property(c => c.Cycle).HasMaxLength(ContributionCycle.MaxLabelLength);
        builder.HasIndex(c => new { c.StokvelId, c.MemberUserId, c.Cycle }).IsUnique();

        // Serves the keyset-paged listing (GetPageAsync): equality on StokvelId
        // and Cycle, then rows come out already ordered by RecordedAt, Id.
        builder.HasIndex(c => new { c.StokvelId, c.Cycle, c.RecordedAt, c.Id })
            .HasDatabaseName("IX_Contributions_StokvelId_Cycle_RecordedAt_Id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.MemberUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ContributionCycle>()
            .WithMany()
            .HasForeignKey(c => new { c.StokvelId, c.Cycle })
            .HasPrincipalKey(cc => new { cc.StokvelId, cc.Label })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Data;

public sealed class StokvelMemberConfiguration : IEntityTypeConfiguration<StokvelMember>
{
    public void Configure(EntityTypeBuilder<StokvelMember> builder)
    {
        // Composite natural key: one membership per user per stokvel.
        // StokvelId leads, so the PK index also serves "all members of a
        // stokvel"; EF adds a separate index on UserId for the User FK.
        builder.HasKey(m => new { m.StokvelId, m.UserId });

        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);

        // Restrict, not Cascade: deleting a user must not silently erase
        // their memberships (and with them, the payout history that points
        // at those memberships).
        builder.HasOne(m => m.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // The Stokvel → Members side (Cascade) is configured in
        // StokvelConfiguration, alongside its backing-field mapping.

        // A payout's recipient must be a member of the payout's stokvel:
        // the composite FK (StokvelId, RecipientUserId) makes the database
        // enforce that. Contributions deliberately have no such link — see
        // StokvelMember.Payouts.
        builder.HasMany(m => m.Payouts)
            .WithOne()
            .HasForeignKey(p => new { p.StokvelId, p.RecipientUserId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

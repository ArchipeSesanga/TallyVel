using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain;

namespace Infrastructure.Data;

public sealed class StokvelInviteConfiguration : IEntityTypeConfiguration<StokvelInvite>
{
    public void Configure(EntityTypeBuilder<StokvelInvite> builder)
    {
        // The code IS the key, so it is unique by definition and a lookup by code is a primary-key lookup.
        builder.HasKey(i => i.Code);
        builder.Property(i => i.Code).HasMaxLength(64);
        builder.HasIndex(i => i.StokvelId);

        builder.HasOne<Stokvel>()
            .WithMany()
            .HasForeignKey(i => i.StokvelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

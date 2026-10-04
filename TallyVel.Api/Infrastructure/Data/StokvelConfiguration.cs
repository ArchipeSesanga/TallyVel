using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Data;

public sealed class StokvelConfiguration : IEntityTypeConfiguration<Stokvel>
{
    public void Configure(EntityTypeBuilder<Stokvel> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(Stokvel.MaxNameLength);
        builder.Property(s => s.ContributionAmount).HasPrecision(18, 2);
        builder.Property(s => s.Cycle).HasConversion<string>().HasMaxLength(20);

        builder.HasMany(s => s.Members)
            .WithOne(m => m.Stokvel)
            .HasForeignKey(m => m.StokvelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Members)
            .HasField("_members")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain;

namespace Infrastructure.Data;

public sealed class ArrearConfiguration : IEntityTypeConfiguration<Arrear>
{
    public void Configure(EntityTypeBuilder<Arrear> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => new { a.CycleId, a.MemberUserId }).IsUnique();
        builder.HasIndex(a => a.StokvelId);

        builder.ToTable(t => t.HasCheckConstraint("CK_Arrears_Shortfall", "\"ShortfallCents\" > 0"));

        builder.HasOne<ContributionCycle>()
            .WithMany()
            .HasForeignKey(a => a.CycleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Stokvel>()
            .WithMany()
            .HasForeignKey(a => a.StokvelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.MemberUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

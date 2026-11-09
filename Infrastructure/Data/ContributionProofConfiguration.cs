using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain;

namespace Infrastructure.Data;

public sealed class ContributionProofConfiguration : IEntityTypeConfiguration<ContributionProof>
{
    public void Configure(EntityTypeBuilder<ContributionProof> builder)
    {
        // One proof per contribution: the contribution's id is the key.
        builder.HasKey(p => p.ContributionId);
        builder.Property(p => p.ContentType).HasMaxLength(100);
        builder.Property(p => p.FileName).HasMaxLength(ContributionProof.MaxFileNameLength);
        builder.Property(p => p.Sha256).HasMaxLength(64);
        builder.Property(p => p.Content).HasColumnType("bytea");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ContributionProofs_Size", $"\"SizeBytes\" > 0 AND \"SizeBytes\" <= {ContributionProof.MaxBytes}"));

        // Deleting a contribution takes its proof with it.
        builder.HasOne<Contribution>()
            .WithOne()
            .HasForeignKey<ContributionProof>(p => p.ContributionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

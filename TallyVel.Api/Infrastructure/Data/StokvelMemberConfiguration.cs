using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallyVel.Api.Domain;

public class StokvelMemberConfiguration : IEntityTypeConfiguration<StokvelMember>
{

    public void Configure(EntityTypeBuilder<StokvelMember> b)
    {
        b.ToTable("SokevelMembers");

        //composite natural key

        b.HasKey(m => new {m.UserId, m.StokvelId});

        b.Property(m => m.Role)
         .HasConversion<string>()
         .HasMaxLength(20)
         .IsRequired();
        
        b.Property(m => m.JoinedAt).IsRequired();

        b.HasOne(m => m.User)
         .WithMany(u => u.Memberships)
         .HasForeignKey(m => m.UserId)
         .OnDelete(DeleteBehavior.Cascade);
        
        b.HasOne(m => m.Stokvel)
         .WithMany(s => s.Members)
         .HasForeignKey(m => m.StokvelId)
         .OnDelete(DeleteBehavior.Cascade);

        // PK index leads with UserId, so "all members of a stokvel"
        // needs its own index on StokvelId
        b.HasIndex(m => m.StokvelId);

    }

}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbHouseRatingConfiguration : IEntityTypeConfiguration<DbHouseRating>
{
    public void Configure(EntityTypeBuilder<DbHouseRating> builder)
    {
        builder.HasKey(r => r.HouseId);
        builder.Property(r => r.HouseId).IsRequired().ValueGeneratedNever();

        builder.Property(r => r.Published).IsRequired();
        builder.Property(r => r.Description).IsRequired(false).HasMaxLength(400);
        builder.Property(r => r.Keywords).IsRequired(false).HasMaxLength(200);

        builder.HasOne(r => r.House)
            .WithOne()
            .HasForeignKey<DbHouseRating>(r => r.HouseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Votes)
            .WithOne(v => v.Rating)
            .HasForeignKey(v => v.HouseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

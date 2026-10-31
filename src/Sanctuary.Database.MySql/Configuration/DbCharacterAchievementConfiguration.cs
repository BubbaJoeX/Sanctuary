using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbCharacterAchievementConfiguration : IEntityTypeConfiguration<DbCharacterAchievement>
{
    public void Configure(EntityTypeBuilder<DbCharacterAchievement> builder)
    {
        builder.HasKey(a => new { a.CharacterId, a.AchievementId });
        builder.Property(a => a.AchievementId).IsRequired().ValueGeneratedNever();

        builder.Property(a => a.Started).IsRequired();
        builder.Property(a => a.Finished).IsRequired(false);

        builder.HasMany(a => a.Objectives)
            .WithOne(o => o.Achievement)
            .HasForeignKey(o => new { o.CharacterId, o.AchievementId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

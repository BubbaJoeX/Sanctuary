using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbCharacterAchievementObjectiveConfiguration : IEntityTypeConfiguration<DbCharacterAchievementObjective>
{
    public void Configure(EntityTypeBuilder<DbCharacterAchievementObjective> builder)
    {
        builder.HasKey(o => new { o.CharacterId, o.AchievementId, o.ObjectiveId });
        builder.Property(o => o.ObjectiveId).IsRequired().ValueGeneratedNever();

        builder.Property(o => o.Count).IsRequired();
        builder.Property(o => o.Completed).IsRequired();
    }
}

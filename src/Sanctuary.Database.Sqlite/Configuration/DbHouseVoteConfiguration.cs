using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbHouseVoteConfiguration : IEntityTypeConfiguration<DbHouseVote>
{
    public void Configure(EntityTypeBuilder<DbHouseVote> builder)
    {
        builder.HasKey(v => new { v.HouseId, v.VoterCharacterId });
        builder.Property(v => v.VoterCharacterId).IsRequired().ValueGeneratedNever();

        builder.Property(v => v.Stars).IsRequired();
        builder.Property(v => v.Voted).IsRequired();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbVehicleLoadoutConfiguration : IEntityTypeConfiguration<DbVehicleLoadout>
{
    public void Configure(EntityTypeBuilder<DbVehicleLoadout> builder)
    {
        builder.HasKey(l => new { l.CharacterId, l.Id });
        builder.Property(l => l.Id).IsRequired().ValueGeneratedNever();

        builder.Property(l => l.Type).IsRequired();
        builder.PrimitiveCollection(l => l.Slots).IsRequired();
    }
}

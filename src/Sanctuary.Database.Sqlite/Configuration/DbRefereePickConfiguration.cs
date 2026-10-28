using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbRefereePickConfiguration : IEntityTypeConfiguration<DbRefereePick>
{
    public void Configure(EntityTypeBuilder<DbRefereePick> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).IsRequired().ValueGeneratedOnAdd();

        builder.Property(p => p.Position).IsRequired();

        builder.Property(p => p.Title).IsRequired().HasMaxLength(40);
        builder.Property(p => p.Text).IsRequired().HasMaxLength(120);

        builder.Property(p => p.Action).IsRequired().HasMaxLength(32);
        builder.Property(p => p.Param1).IsRequired();
        builder.Property(p => p.Icon).IsRequired().HasMaxLength(32);
        builder.Property(p => p.Button).IsRequired().HasMaxLength(24);

        builder.Property(p => p.UpdatedBy).IsRequired(false);
        builder.Property(p => p.Updated).IsRequired().HasDefaultValueSql("DATE()");
    }
}

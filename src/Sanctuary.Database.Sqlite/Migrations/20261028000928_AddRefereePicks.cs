using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRefereePicks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefereePicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Param1 = table.Column<int>(type: "INTEGER", nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Button = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    UpdatedBy = table.Column<ulong>(type: "INTEGER", nullable: true),
                    Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefereePicks", x => x.Id);
                });

            // The stock picks from Resources/RefereePicks.json, which !pick reset and the admin site restore.
            migrationBuilder.InsertData(
                table: "RefereePicks",
                columns: ["Position", "Title", "Text", "Action", "Param1", "Icon", "Button"],
                values: new object[,]
                {
                    { 0, "Meet up in Sanctuary", "The heart of Sacred Grove. Show off your look and find people to play with.", "Teleport", 5, "tree", "Teleport" },
                    { 1, "Make a house a home", "Pick out a house, then fill it with furniture from the Coin Shop.", "HouseBrowser", 0, "house", "" },
                    { 2, "Fresh in the Coin Shop", "Spend the coins you earn on outfits, mounts and pets.", "Marketplace", 0, "coin", "Shop" },
                    { 3, "Explore Briarwood", "Adventure is waiting in the woods. Bring a friend!", "Teleport", 4, "trophy", "Teleport" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefereePicks");
        }
    }
}

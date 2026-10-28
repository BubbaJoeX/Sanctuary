using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Text = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Action = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Param1 = table.Column<int>(type: "int", nullable: false),
                    Icon = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Button = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedBy = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    Updated = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefereePicks", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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

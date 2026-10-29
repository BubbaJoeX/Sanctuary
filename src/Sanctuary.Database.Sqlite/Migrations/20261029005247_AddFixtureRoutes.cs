using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddFixtureRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "RouteEndX",
                table: "HouseFixtures",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "RouteEndY",
                table: "HouseFixtures",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "RouteEndZ",
                table: "HouseFixtures",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RouteEndX",
                table: "HouseFixtures");

            migrationBuilder.DropColumn(
                name: "RouteEndY",
                table: "HouseFixtures");

            migrationBuilder.DropColumn(
                name: "RouteEndZ",
                table: "HouseFixtures");
        }
    }
}

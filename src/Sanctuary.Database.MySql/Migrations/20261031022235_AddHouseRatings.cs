using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HouseRatings",
                columns: table => new
                {
                    HouseId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Published = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    Description = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Keywords = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseRatings", x => x.HouseId);
                    table.ForeignKey(
                        name: "FK_HouseRatings_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HouseVotes",
                columns: table => new
                {
                    HouseId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    VoterCharacterId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Stars = table.Column<int>(type: "int", nullable: false),
                    Voted = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseVotes", x => new { x.HouseId, x.VoterCharacterId });
                    table.ForeignKey(
                        name: "FK_HouseVotes_HouseRatings_HouseId",
                        column: x => x.HouseId,
                        principalTable: "HouseRatings",
                        principalColumn: "HouseId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseVotes");

            migrationBuilder.DropTable(
                name: "HouseRatings");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddAchievementsAndRecipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Recipes",
                table: "Characters",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CharacterAchievements",
                columns: table => new
                {
                    AchievementId = table.Column<int>(type: "int", nullable: false),
                    CharacterId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Started = table.Column<long>(type: "bigint", nullable: false),
                    Finished = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterAchievements", x => new { x.CharacterId, x.AchievementId });
                    table.ForeignKey(
                        name: "FK_CharacterAchievements_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CharacterAchievementObjectives",
                columns: table => new
                {
                    AchievementId = table.Column<int>(type: "int", nullable: false),
                    CharacterId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ObjectiveId = table.Column<int>(type: "int", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    Completed = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterAchievementObjectives", x => new { x.CharacterId, x.AchievementId, x.ObjectiveId });
                    table.ForeignKey(
                        name: "FK_CharacterAchievementObjectives_CharacterAchievements_Charact~",
                        columns: x => new { x.CharacterId, x.AchievementId },
                        principalTable: "CharacterAchievements",
                        principalColumns: new[] { "CharacterId", "AchievementId" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterAchievementObjectives");

            migrationBuilder.DropTable(
                name: "CharacterAchievements");

            migrationBuilder.DropColumn(
                name: "Recipes",
                table: "Characters");
        }
    }
}

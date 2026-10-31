using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
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
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CharacterAchievements",
                columns: table => new
                {
                    AchievementId = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Started = table.Column<long>(type: "INTEGER", nullable: false),
                    Finished = table.Column<long>(type: "INTEGER", nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "CharacterAchievementObjectives",
                columns: table => new
                {
                    AchievementId = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ObjectiveId = table.Column<int>(type: "INTEGER", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    Completed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterAchievementObjectives", x => new { x.CharacterId, x.AchievementId, x.ObjectiveId });
                    table.ForeignKey(
                        name: "FK_CharacterAchievementObjectives_CharacterAchievements_CharacterId_AchievementId",
                        columns: x => new { x.CharacterId, x.AchievementId },
                        principalTable: "CharacterAchievements",
                        principalColumns: new[] { "CharacterId", "AchievementId" },
                        onDelete: ReferentialAction.Cascade);
                });
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

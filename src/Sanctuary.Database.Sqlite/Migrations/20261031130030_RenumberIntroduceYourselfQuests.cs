using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
{
    /// <summary>
    /// Sanctuary's two Introduce Yourself quests took the ids of two live Hero's Journal stickers (2563, 2564); they are
    /// 3082 and 3083 now. Progress rows point at their quest row, so each quest row is copied to the new id first.
    /// </summary>
    public partial class RenumberIntroduceYourselfQuests : Migration
    {
        private static readonly (int Old, int New)[] Renumbered = [(2563, 3082), (2564, 3083)];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (oldId, newId) in Renumbered)
                Move(migrationBuilder, oldId, newId);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (oldId, newId) in Renumbered)
                Move(migrationBuilder, newId, oldId);
        }

        private static void Move(MigrationBuilder migrationBuilder, int from, int to)
        {
            migrationBuilder.Sql($"INSERT INTO CharacterQuests (QuestId, CharacterId, Completed, CurrentGoalId) SELECT {to}, CharacterId, Completed, CurrentGoalId FROM CharacterQuests WHERE QuestId = {from};");
            migrationBuilder.Sql($"UPDATE CharacterQuestProgress SET QuestId = {to} WHERE QuestId = {from};");
            migrationBuilder.Sql($"DELETE FROM CharacterQuests WHERE QuestId = {from};");
            migrationBuilder.Sql($"UPDATE Characters SET ActiveQuestId = {to} WHERE ActiveQuestId = {from};");
        }
    }
}

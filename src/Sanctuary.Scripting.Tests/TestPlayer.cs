using System.Collections.Generic;

namespace Sanctuary.Scripting.Tests;

/// <summary>
/// A player for the zone script tests: who they are, where and with whom is set by the test, and what scripts give
/// or tell them is recorded.
/// </summary>
internal sealed class TestPlayer(ulong guid, string name) : IScriptablePlayer
{
    public ulong Guid => guid;
    public string Name => name;
    public (float x, float y, float z) Position { get; set; }

    public List<string> Messages { get; } = [];
    public void Message(string message) => Messages.Add(message);

    public Dictionary<int, int> Inventory { get; } = [];
    public void GiveItem(int itemDefinitionId, int quantity) => Inventory[itemDefinitionId] = CountItem(itemDefinitionId) + quantity;
    public int CountItem(int itemDefinitionId) => Inventory.GetValueOrDefault(itemDefinitionId);

    public bool TakeItem(int itemDefinitionId, int quantity)
    {
        if (CountItem(itemDefinitionId) < quantity)
            return false;

        Inventory[itemDefinitionId] -= quantity;
        return true;
    }

    public int Coins { get; private set; }
    public void GiveCoins(int amount) => Coins += amount;

    public bool TakeCoins(int amount)
    {
        if (Coins < amount)
            return false;

        Coins -= amount;
        return true;
    }

    public int Stars { get; private set; }

    public bool GiveStars(int amount)
    {
        Stars += amount;
        return true;
    }

    public List<int> Effects { get; } = [];
    public void PlayEffect(int compositeEffectId) => Effects.Add(compositeEffectId);

    public IReadOnlyList<ulong> GroupMemberGuids { get; set; } = [];
    public int GroupId => GroupMemberGuids.Count > 0 ? 1 : 0;
    public bool IsGroupLeader => GroupMemberGuids.Count > 0 && GroupMemberGuids[0] == guid;

    public bool IsKnockedOut { get; set; }
    public bool Revive(int healthPercent) => false;

    public int Job { get; set; } = 1;
    public int JobRank { get; set; } = 1;

    public void AcceptQuest(int questId) { }
    public bool HasCompletedQuest(int questId) => false;
    public bool IsGm => false;
    public bool IsMounted => false;
    public void Dismount() { }
    public void Teleport(float x, float y, float z, float? heading) => Position = (x, y, z);
    public void Heal(int amount) { }
    public void Emote(int animationId) { }
    public void Transform(int modelId, int seconds) { }
    public void ClearTransform() { }
    public void Resize(float scale, int seconds) { }
    public int ZoneId => 1;
    public string ZoneName => "FabledRealms";
    public int Health => 100;
    public int MaxHealth => 100;
    public bool IsInCombat => false;
    public bool Warp(int zoneId, (float x, float y, float z)? position, float? heading) => false;
    public bool StartMinigame(int minigameId) => false;
    public int BattleId => 0;
    public bool IsFishing => false;
    public bool IsInMinigame => false;
    public bool StartBattle(int battleId) => false;
    public bool HasAchievement(int achievementId) => false;
    public bool KnowsRecipe(int recipeId) => false;
    public bool LearnRecipe(int recipeId) => false;
    public bool PlayEmote(string name) => false;
    public void StopEmote() { }
}

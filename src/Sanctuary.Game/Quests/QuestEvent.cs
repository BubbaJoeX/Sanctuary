using System.Numerics;

using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Quests;

public abstract record QuestEvent
{
    public virtual bool CreditsOneQuest => false;
}

public sealed record NpcTalkedEvent(Npc Npc) : QuestEvent;

public sealed record NodeGatheredEvent(CollectionNode Node) : QuestEvent
{
    public override bool CreditsOneQuest => true;
}

public sealed record PlayerMovedEvent(Vector4 Position, int ZoneDefinitionId) : QuestEvent;

// A mob the player (or their group, within reward range) knocked out, in the world or in a battle.
public sealed record MobDefeatedEvent(Npc Mob) : QuestEvent;

// A Battles.json battle won.
public sealed record BattleWonEvent(int BattleId) : QuestEvent;

// A minigame finished, won or not.
public sealed record MinigameFinishedEvent(int MinigameId, bool Won) : QuestEvent;

// A recipe made, and the item it makes.
public sealed record ItemCraftedEvent(int RecipeId, int ItemDefinitionId) : QuestEvent;

// An item equipped on any job.
public sealed record ItemEquippedEvent(int ItemDefinitionId) : QuestEvent;

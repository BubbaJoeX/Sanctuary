using System;
using System.Collections.Generic;

using Sanctuary.Game.Authoring;
using Sanctuary.Game.Conversations;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Gateway.Admin;

// Requests and responses of the admin API. Guids travel as strings: they're 64-bit, which JavaScript numbers can't hold.
// Headings are radians, 0 facing +Z and increasing towards +X.

public sealed record LoginRequest(string Username, string Password);

public sealed record RefereeModel(string Id, string Name, string Role, string? Character);

public sealed record StatusModel(
    DateTimeOffset StartedAt,
    int Players,
    int Zones,
    bool Locked,
    string? MotdTitle,
    string? MotdMessage,
    float TimeScale,
    IReadOnlyList<EventModel> Events);

public sealed record EventModel(string Key, bool Active);

public sealed record EventRequest(bool Active);

public sealed record SettingsRequest(bool? Locked, bool? KickPlayers, string? MotdTitle, string? MotdMessage, float? TimeScale);

public sealed record BroadcastRequest(string Message, bool Banner, string? Zone);

public sealed record ResourceModel(string Name);

public sealed record CommandModel(string Keyword, string Usage, string Description, string Role);

public sealed record CommandRequest(string Command);

public sealed record CommandResult(string Character, bool Handled, IReadOnlyList<string> Lines);

public sealed record PlayerModel(
    string Id,
    string Name,
    string Zone,
    string ZoneName,
    float X,
    float Y,
    float Z,
    float H,
    string Role,
    bool GodMode,
    bool Frozen,
    bool Muted,
    string CharacterId);

public sealed record TeleportRequest(string? Zone, float? X, float? Y, float? Z, string? ToPlayer);

public sealed record MessageRequest(string Message);

/// <summary>
/// A game account and its characters. Bans and mutes are given only while they last; a ban for good lasts until
/// <see cref="DateTimeOffset.MaxValue"/>.
/// </summary>
public sealed record AccountModel(
    string Id,
    string Username,
    string Role,
    DateTimeOffset Created,
    DateTimeOffset? LastLogin,
    DateTimeOffset? BannedUntil,
    DateTimeOffset? MutedUntil,
    int MaxCharacters,
    IReadOnlyList<CharacterSummaryModel> Characters);

public sealed record CharacterSummaryModel(
    string Id,
    string Name,
    int Coins,
    int PlayTime,
    DateTimeOffset Created,
    DateTimeOffset? LastLogin,
    OnlineModel? Online);

/// <summary>
/// Where a character is in the world: <see cref="PlayerId"/> is the guid the player routes take.
/// </summary>
public sealed record OnlineModel(string PlayerId, string Zone, string ZoneName, float X, float Y, float Z);

/// <summary>
/// No <see cref="Minutes"/> bans for good. The reason goes to the audit log.
/// </summary>
public sealed record BanRequest(int? Minutes, string? Reason);

public sealed record MuteRequest(int Minutes, string? Reason);

public sealed record RoleRequest(string Role);

public sealed record CharacterModel(
    string Id,
    string Name,
    AccountRefModel Account,
    int Coins,
    int PlayTime,
    DateTimeOffset Created,
    DateTimeOffset? LastLogin,
    OnlineModel? Online,
    IReadOnlyList<InventoryItemModel> Items,
    IReadOnlyList<OwnedMountModel> Mounts);

public sealed record AccountRefModel(string Id, string Username, string Role, DateTimeOffset? BannedUntil, DateTimeOffset? MutedUntil);

/// <summary>
/// An item a character holds: <see cref="Id"/> is theirs, <see cref="Definition"/> the kind of item.
/// </summary>
public sealed record InventoryItemModel(int Id, int Definition, string Name, int Count, int Tint, bool Equipped);

public sealed record OwnedMountModel(int Id, int Definition, string Name, bool Upgraded);

public sealed record ItemGrantRequest(int Item, int Quantity = 1, int Tint = 0);

public sealed record ItemTakeRequest(int Item, int Quantity = 1);

/// <summary>
/// Coins to give (positive) or take (negative).
/// </summary>
public sealed record CoinsRequest(int Amount);

public sealed record ItemSearchModel(int Id, string Name, int Slot, int Rarity, int MaxStackSize);

/// <summary>
/// A gateway log there's something to read in: its days, newest first.
/// </summary>
public sealed record LogKindModel(string Key, string Name, IReadOnlyList<string> Dates);

/// <summary>
/// One log entry: its time as written, level, logger and message (an exception's stack trace included).
/// </summary>
public sealed record LogEntryModel(string Time, string Level, string Logger, string Message);

/// <summary>
/// A day's log entries matching a search, newest first; <see cref="More"/> when older ones matched too.
/// </summary>
public sealed record LogPageModel(string Date, IReadOnlyList<LogEntryModel> Entries, bool More);

public sealed record QuestListModel(IReadOnlyList<CustomQuestSummaryModel> Custom, IReadOnlyList<StockQuestModel> Stock);

/// <summary>
/// A GM-authored quest in a list: <see cref="QuestId"/> is what the game knows it as (900000 + <see cref="Id"/>).
/// </summary>
public sealed record CustomQuestSummaryModel(int Id, int QuestId, string Title, bool Published, int GiverNpcId, string? GiverName, int Goals,
    DateTimeOffset Updated);

/// <summary>
/// One of the game's own quests (Quests.json), titled from its locale string.
/// </summary>
public sealed record StockQuestModel(int QuestId, string Title, int GiverNpcId, string? GiverName, int Goals, int? NextQuestId);

public sealed record QuestOptionsModel(IReadOnlyList<NodeTypeModel> NodeTypes, IReadOnlyList<ZoneOptionModel> Zones, int MaxGoals, int MaxRewards);

public sealed record NodeTypeModel(string Key, string Name);

public sealed record ZoneOptionModel(int Id, string Name);

public sealed record QuestCreateRequest(string Title);

/// <summary>
/// Every field of a GM-authored quest, as saved. Goals complete in order; 0 for an NPC or locale string id means none
/// (the giver, or the generic string).
/// </summary>
public sealed record CustomQuestRequest(
    string Title,
    string? Description,
    int TitleId,
    int DescriptionId,
    int GiverNpcId,
    int TurnInNpcId,
    int RewardCoins,
    int? PrerequisiteQuestId,
    int? NextQuestId,
    List<CustomQuestGoal>? Goals,
    List<CustomQuestReward>? Rewards);

/// <summary>
/// A GM-authored quest; <see cref="Blocked"/> says what stops it being published, if anything.
/// </summary>
public sealed record CustomQuestModel(
    int Id,
    int QuestId,
    string Title,
    string? Description,
    int TitleId,
    int DescriptionId,
    int GiverNpcId,
    int TurnInNpcId,
    int RewardCoins,
    int? PrerequisiteQuestId,
    int? NextQuestId,
    IReadOnlyList<CustomQuestGoal> Goals,
    IReadOnlyList<CustomQuestReward> Rewards,
    bool Published,
    string? Blocked,
    DateTimeOffset Updated);

/// <summary>
/// An NPC placed in a running world zone: <see cref="Id"/> is its guid for the object routes, <see cref="Nodes"/>
/// its conversation's size (0 for none) and <see cref="Greeting"/> what it says first.
/// </summary>
public sealed record WorldNpcModel(string Zone, string ZoneName, string Id, int Definition, string Name, string? DefinitionName, int Nodes,
    string? Greeting, float X, float Z);

public sealed record ZoneModel(
    string Key,
    int DefinitionId,
    string? OwnerId,
    string Name,
    string Type,
    int Players,
    int Npcs,
    bool Editable,
    bool HasTerrain,
    BoundsModel? Bounds,
    PointModel Spawn);

public sealed record BoundsModel(float MinX, float MinZ, float MaxX, float MaxZ);

public sealed record PointModel(float X, float Y, float Z, float H);

/// <summary>
/// A player in the live feed. <see cref="Look"/> keys their look in the feed's <c>looks</c> (see <see cref="LiveLooks"/>).
/// </summary>
public sealed record LivePlayer(string Id, string Name, float X, float Y, float Z, float H, string? Look);

/// <summary>
/// A placed object. <see cref="Kind"/> is prop (a furniture item), npc (a world NPC) or model (a world NPC that's a
/// placed model). <see cref="Model"/> is its actor file, for the zone editor to draw; pitch and roll are radians.
/// </summary>
public sealed record LiveObject(
    string Id,
    string Kind,
    int Def,
    string Label,
    string? Model,
    float X,
    float Y,
    float Z,
    float H,
    float Pitch,
    float Roll,
    float Scale,
    string? Look);

/// <summary>
/// One of a zone's other NPCs. <see cref="Kind"/> is npc, mob, node (a collection node) or coin.
/// </summary>
public sealed record LiveNpc(string Id, string Label, string Kind, float X, float Y, float Z, float H, float Scale, string? Look);

/// <summary>
/// What changed among a zone's other NPCs since the last update; <see cref="Reset"/> on the first, which has them all.
/// </summary>
public sealed record LiveNpcChanges(bool Reset, IReadOnlyList<LiveNpc> Upsert, IReadOnlyList<string> Remove);

public sealed record ObjectDetail(
    string Id,
    string Kind,
    int Def,
    string Label,
    float X,
    float Y,
    float Z,
    float H,
    float Scale,
    int? Tint,
    bool Collidable,
    string? Name,
    int? ModelId,
    string? Event,
    bool HasScript);

/// <summary>
/// A new prop (<see cref="Def"/> a furniture item id) or NPC (an NPC definition id). Without <see cref="Y"/> it
/// stands on the ground.
/// </summary>
public sealed record PlaceRequest(int Def, float X, float? Y, float Z, float H, float? Scale, int? Tint);

/// <summary>
/// A placed model: any actor with a Models.txt row, spawned as a still, solid world NPC.
/// </summary>
public sealed record ModelPlaceRequest(string Model, float X, float? Y, float Z, float H, float? Pitch, float? Roll, float? Scale);

/// <summary>
/// Changes to a placed object. With <see cref="Snap"/> and no <see cref="Y"/>, a move keeps its height above the ground.
/// </summary>
public sealed record ObjectPatch(
    float? X,
    float? Y,
    float? Z,
    float? H,
    bool Snap,
    float? Pitch,
    float? Roll,
    float? Scale,
    int? Tint,
    bool? Collidable,
    string? Name);

/// <summary>
/// What a placed NPC, model (<see cref="Kind"/> npc) or prop is beyond where it stands, as !npc and !prop set it in
/// game. Props have no model, disposition, mob, behaviour, speed, event or conversation of their own.
/// <see cref="Disposition"/> is hostile, neutral or ally; <see cref="Behavior"/> stay, follow or wander.
/// </summary>
public sealed record ObjectSettings(
    string Kind,
    int Def,
    string? DefName,
    string? Name,
    int NameId,
    bool Nameplate,
    int ModelId,
    int Animation,
    string Disposition,
    string? Mob,
    string Behavior,
    float WanderRadius,
    float Speed,
    bool Collidable,
    string? Event,
    string? TextureAlias,
    string? TintAlias,
    int TintId,
    int Effect,
    int Subtitle,
    float Offset,
    IReadOnlyList<NpcProperties.NpcIcon> Icons,
    NpcConditions? Conditions,
    bool HasConversation);

/// <summary>
/// Changes to an object's settings, as one undo step; null leaves a field alone. An empty <see cref="Name"/> goes back
/// to the definition's name, an empty alias, mob or event clears it, and empty <see cref="Conditions"/> clear them.
/// Follow follows the referee's character.
/// </summary>
public sealed record ObjectSettingsPatch(
    string? Name,
    int? NameId,
    bool? Nameplate,
    int? ModelId,
    int? Animation,
    string? Disposition,
    string? Mob,
    string? Behavior,
    float? WanderRadius,
    float? Speed,
    string? Event,
    string? TextureAlias,
    string? TintAlias,
    int? TintId,
    int? Effect,
    int? Subtitle,
    float? Offset,
    IReadOnlyList<NpcProperties.NpcIcon>? Icons,
    NpcConditions? Conditions);

/// <summary>
/// What the settings can be set to: the GM panel's animations, Mobs.json's mobs, the world events and seasons, quest
/// states for conditions, the overhead icons the game's NPCs use, and the game clock's hour now.
/// </summary>
public sealed record NpcOptions(
    IReadOnlyList<AnimationOption> Animations,
    IReadOnlyList<string> Mobs,
    IReadOnlyList<EventModel> Events,
    IReadOnlyList<SeasonModel> Seasons,
    IReadOnlyList<string> QuestStates,
    IReadOnlyList<NpcIconCatalog.Entry> Icons,
    float Hour);

public sealed record AnimationOption(int Id, string Label, bool Loop);

/// <summary>
/// A seasonal holiday (Resources/Seasons.json): on between its days of the year, as an event key.
/// </summary>
public sealed record SeasonModel(string Key, string Name, int StartMonth, int StartDay, int EndMonth, int EndDay, bool Active);

/// <summary>
/// A world NPC's conversation as saved; null when it has none.
/// </summary>
public sealed record ConversationModel(Conversation? Conversation);

public sealed record RespawnResult(int Respawned);

public sealed record CopyRequest(float X, float Z, float? H);

public sealed record EditResult(string? Id, int Undo);

public sealed record UndoResult(string? Undid, int Undo);

public sealed record CatalogEntry(int Id, string Name, string? Model);

/// <summary>
/// A world NPC's or prop's GM script as saved, and whether that's what is running (a saved script that failed to
/// compile when the NPC spawned isn't).
/// </summary>
public sealed record ScriptModel(string Kind, string Source, bool Running);

public sealed record ScriptRequest(string Source);

/// <summary>
/// What a save, removal, template use or test came to: <see cref="Ok"/> false with the compiler's or hook's message.
/// </summary>
public sealed record ScriptResult(bool Ok, string Message, bool Running);

/// <summary>
/// A library script put on an NPC or prop, its parameters from <see cref="Values"/> (the rest their defaults).
/// </summary>
public sealed record UseScriptRequest(string Name, Dictionary<string, string>? Values);

public sealed record ScriptParamModel(string Key, string Type, string Default);

/// <summary>
/// A script library entry: a shipped template or a referee's saved script. <see cref="Source"/> only when one is asked for.
/// </summary>
public sealed record LibraryEntryModel(string Name, string Description, bool IsTemplate, IReadOnlyList<ScriptParamModel> Params, string? Source);

public sealed record LibrarySaveRequest(string Source, string? Description);

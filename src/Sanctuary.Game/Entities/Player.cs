using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Core.Extensions;
using Sanctuary.Core.IO;
using Sanctuary.Core.Helpers;
using Sanctuary.Game;
using Sanctuary.Game.Achievements;
using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Fishing;
using Sanctuary.Game.Groups;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Minigames;
using Sanctuary.Game.Quests;
using Sanctuary.Game.Resources.Definitions.Combat;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Chat;
using Sanctuary.UdpLibrary;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.Game.Entities;

public sealed class Player : ClientPcData, IEntity
{
    private enum PacketSendType
    {
        All,
        AllAndSelf,
        SelfOnly
    }

    public const int WeaponSlot = 7;
    private const int FlairShardSlot = 13;
    private const int IdleCharacterState = 1;
    private const int KnockedOutCharacterState = 0x80;
    private const int ActionBarSlotUnknown5 = 1;
    private const int ActionBarSlotUnknown6 = 4;
    private const int ActionBarSlotUnknown7 = 15;
    private const float OnFootMovementSpeed = 8f;
    private const long AreaSettleMilliseconds = 1500;

    private readonly UdpConnection _connection;
    private readonly IResourceManager _resourceManager;
    private readonly IZoneManager _zoneManager;

    public bool Visible { get; set; }

    public IZone Zone { get; set; }
    public ZoneTile ZoneTile { get; private set; } = ZoneTile.Empty;
    public ConcurrentDictionary<ulong, Npc> VisibleNpcs { get; } = [];
    public ConcurrentDictionary<ulong, Player> VisiblePlayers { get; } = [];

    private uint? _portraitCrc;
    private uint? _pendingPortraitCrc;
    private DateTimeOffset _portraitUpdateExpiresAt;

    public int PointOfInterestId { get; private set; }
    public int ChatAreaNameId { get; private set; }

    private ZoneAreaDefinition? _pendingZoneArea;
    private long _pendingZoneAreaSince;

    public int ChatBubbleForegroundColor { get; set; }
    public int ChatBubbleBackgroundColor { get; set; }
    public int ChatBubbleSize { get; set; }

    public bool IsAdmin { get; set; }
    public bool IsMod { get; set; }
    public ChatCommandRole ChatCommandRole => ChatHelper.GetRoleFromFlags(IsAdmin, IsMod);

    /// <summary>
    /// Toggled with !godmode. Off by default, so referees see the world as players do until they turn it on.
    /// </summary>
    public bool GodMode { get; set; }

    /// <summary>
    /// Referee editing is live: cursors, glow and GM menus on everything editable.
    /// </summary>
    public bool IsInGodMode => GodMode && ChatCommandRole >= ChatCommandRole.Mod;
    public DateTimeOffset? MutedUntil { get; set; }

    /// <summary>
    /// While set, every packet to and from this player is traced (see !packetlog).
    /// </summary>
    public ILogger? PacketLogger { get; set; }

    /// <summary>
    /// Movement speed to restore on unfreeze; null while not frozen (see !freeze).
    /// </summary>
    public float? FrozenMovementSpeed { get; set; }

    /// <summary>
    /// Referees reading a copy of everything this player says (see !snoop).
    /// </summary>
    public ConcurrentSet<ulong> Snoopers { get; } = [];

    /// <summary>
    /// While set, also collects the system messages sent to this player, so the admin panel can show what a
    /// command it ran as this referee answered.
    /// </summary>
    public ConcurrentQueue<string>? SystemMessageCapture { get; set; }

    /// <summary>
    /// While set with <see cref="SystemMessageCapture"/>, collected system messages aren't sent to the player: a
    /// command the admin panel runs on them answers the referee, not them.
    /// </summary>
    public bool SystemMessageCaptureOnly { get; set; }

    /// <summary>
    /// Where a referee stood before their last !gm goto/go/gather jump; !gm back returns there.
    /// </summary>
    public (int ZoneDefinitionId, ulong? OwnerId, Vector4 Position, Quaternion Rotation)? ReturnPoint { get; set; }

    /// <summary>
    /// The referee client tools (/lua, /dofile, ...) were loaded into this client (see ClientLuaHelper).
    /// </summary>
    public bool RefereeToolsLoaded { get; set; }

    /// <summary>
    /// The client extensions (Sanctuary's region badges, ...) were loaded into this client (see ClientLuaHelper).
    /// </summary>
    public bool ClientExtensionsLoaded { get; set; }

    /// <summary>
    /// Glide and jump stats to restore when !fly is turned off; null while not flying.
    /// </summary>
    public CharacterStat[]? FlyRestoreStats { get; set; }

    /// <summary>
    /// NPC a referee last clicked; the target of !npc edits.
    /// </summary>
    public ulong? SelectedNpcGuid { get; set; }

    /// <summary>
    /// The NPC conversation (and node) this player has open, if any.
    /// </summary>
    public (ulong NpcGuid, int NodeId)? ActiveConversation { get; set; }

    /// <summary>
    /// Script source a referee is uploading in chunks (!npc script append), until they commit it.
    /// </summary>
    public System.Text.StringBuilder? PendingScript { get; set; }

    public ClientPcProfile ActiveProfile =>
        Profiles.FirstOrDefault(x => x.Id == ActiveProfileId) ?? Profiles.First();

    public Mount? Mount { get; set; }
    public PlayerPet PlayerPet { get; }
    public Npc? Pet => PlayerPet.Pet;
    public Dictionary<int, int> PetDefinitions { get; } = [];
    public Dictionary<int, PetTrait> PetTraits { get; } = [];
    public Dictionary<int, DateTimeOffset> PetTreasureFoundAt { get; } = [];

    public PetClientInfo? GetPetClientInfo(int petId) => Pets.FirstOrDefault(petClientInfo => petClientInfo.Id == petId);

    public PetClientInfo? GetActivePetClientInfo() => GetPetClientInfo(ActivePetId);

    public List<FriendData> Friends { get; set; } = [];
    public List<IgnoreData> Ignores { get; set; } = [];

    public ConcurrentSet<ulong> IncomingFriendRequests { get; } = [];
    public ConcurrentSet<ulong> IncomingGuildInvites { get; } = [];


    public ulong CharacterId { get; set; }

    public PlayerAchievements Achievements { get; } = new();

    /// <summary>
    /// The Recipes.json ids this player knows. Lock it to read or change it.
    /// </summary>
    public HashSet<int> KnownRecipes { get; } = [];

    public QuestLog Quests { get; } = new();

    public QuestConversation QuestConversation { get; } = new();

    public ulong ObjectiveTaskId { get; } = (ulong)Random.Shared.NextInt64();

    public ConcurrentDictionary<ChatChannel, bool> ChatChannelStatus { get; set; } = [];

    private static readonly ChatChannel[] DefaultEnabledChatChannels = [ChatChannel.WorldArea, ChatChannel.WorldMembersOnly];

    public DateTimeOffset LoginStartedAt { get; set; }

    public long NextClientPathRequestAt { get; set; }
    public int SecondsSinceLastLogin { get; set; } = -1;
    public ConcurrentSet<int> CollectedCollectionEntryIds { get; } = [];

    public List<CoinStoreTransactionRecord> CoinStoreTransactions { get; set; } = [];

    public GuildData? GuildData { get; set; }

    /// <summary>
    /// The party this player is in, kept by GroupManager.
    /// </summary>
    public PlayerGroup? Group { get; set; }

    public int TimezoneOffset { get; set; }

    public Dictionary<int, Dictionary<int, int>> ActionBarItemGuids { get; set; } = new();

    public int TemporaryAppearance { get; private set; }

    public int BoomboxDancePriority { get; set; }
    public ulong BoomboxDanceOwner { get; set; }
    public byte PositionState { get; set; }

    private const int CrowdedVisiblePlayerCount = 50;
    private const int CrowdedRelayIntervalMs = 500;

    public RelayedPosition? LatestPosition { get; private set; }
    private long _positionVersion;
    private readonly ConcurrentDictionary<ulong, (long Version, byte State, long NextDueAt)> _sentPositions = new();
    public int BoomboxDanceAnimation { get; set; }
    public bool BoomboxDanceIsStanding { get; set; }
    public int BoomboxDanceTransform { get; set; }

    // A looping !emote (the player's base animation while it lasts) and where it started; walking away ends it.
    public int LoopingEmoteAnimation { get; set; }
    public Vector4 LoopingEmoteFrom { get; set; }
    private const float LoopingEmoteWalkAwaySquared = 0.5f * 0.5f;

    public const float DefaultScale = 1f;

    public float Scale { get; private set; } = DefaultScale;

    public ulong LastSillyStringTarget { get; set; }

    public int ActiveFoodEffectId { get; set; }

    private readonly ConcurrentDictionary<int, (DateTimeOffset ExpiresAt, int TotalMs)> _itemCooldowns = new();

    public bool IsItemOnCooldown(int itemDefinitionId) =>
        _itemCooldowns.TryGetValue(itemDefinitionId, out var cooldown) && DateTimeOffset.UtcNow < cooldown.ExpiresAt;

    public void StartItemCooldown(int itemDefinitionId, int cooldownMs) =>
        _itemCooldowns[itemDefinitionId] = (DateTimeOffset.UtcNow.AddMilliseconds(cooldownMs), cooldownMs);

    public bool TryGetItemCooldown(int itemDefinitionId, out int totalMs, out int elapsedMs)
    {
        if (_itemCooldowns.TryGetValue(itemDefinitionId, out var cooldown) && DateTimeOffset.UtcNow < cooldown.ExpiresAt)
        {
            totalMs = cooldown.TotalMs;
            elapsedMs = totalMs - (int)(cooldown.ExpiresAt - DateTimeOffset.UtcNow).TotalMilliseconds;
            return true;
        }

        totalMs = 0;
        elapsedMs = 0;
        return false;
    }

    private readonly ConcurrentDictionary<int, int> _lastRandomTransformIndex = new();

    public int LastRandomTransformIndex(int itemDefinitionId) =>
        _lastRandomTransformIndex.TryGetValue(itemDefinitionId, out var index) ? index : -1;

    public void SetLastRandomTransformIndex(int itemDefinitionId, int index) =>
        _lastRandomTransformIndex[itemDefinitionId] = index;

    // For one-off delayed packets unrelated to tracked effects (e.g. a silly string reset
    // animation, a boombox poof) - see PlayerEffect/AddEffect for anything with its own state.
    // Min-heap ordered by send time.
    private readonly PriorityQueue<(ISerializablePacket Packet, PacketSendType SendType), DateTimeOffset> _delayedPackets = new();

    // One scheduled personal-UI packet per action bar slot (the cooldown re-enable) - keyed, not queued,
    // so a slot that gets emptied before its cooldown naturally expires (last item consumed) can cancel
    // its own pending re-enable instead of it firing later and silently un-deleting the slot.
    private readonly ConcurrentDictionary<(int, int), (DateTimeOffset SendAt, ISerializablePacket Packet)> _delayedSlotPackets = new();

    public void ScheduleSlotPacket(int actionBarId, int slotIndex, ISerializablePacket serializablePacket, int delayMs)
    {
        _delayedSlotPackets[(actionBarId, slotIndex)] = (DateTimeOffset.UtcNow.AddMilliseconds(delayMs), serializablePacket);
    }

    public void CancelScheduledSlotPacket(int actionBarId, int slotIndex) => _delayedSlotPackets.TryRemove((actionBarId, slotIndex), out _);

    /// <summary>
    /// Empties every action bar slot holding <paramref name="itemId"/>, after the item left the inventory.
    /// </summary>
    public void RemoveItemFromActionBars(int itemId)
    {
        foreach (var (actionBarId, slotItemGuids) in ActionBarItemGuids)
        {
            foreach (var slot in slotItemGuids.Where(entry => entry.Value == itemId).Select(entry => entry.Key).ToList())
            {
                slotItemGuids.Remove(slot);
                CancelScheduledSlotPacket(actionBarId, slot);

                var slotPacket = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = actionBarId, Slot = slot } };
                slotPacket.Slot.IsEmpty = true;

                SendTunneled(slotPacket);
            }
        }
    }

    /// <summary>
    /// Whether any job has <paramref name="itemId"/> equipped.
    /// </summary>
    public bool IsItemEquipped(int itemId) => Profiles.Any(profile => profile.Items.Values.Any(item => item.Id == itemId));

    public Vector4 StartingZonePosition { get; set; }
    public Quaternion StartingZoneRotation { get; set; }

    /// <summary>
    /// Tick count before which zone portals ignore the player (set when one moves them).
    /// </summary>
    public long PortalReadyAt { get; set; }

    public ConcurrentSet<OneTimeNotification> SeenOneTimeNotifications { get; } = [];

    public bool ShopDataSent { get; set; }
    public ConcurrentSet<int> SentCoinStoreItemDefinitionIds { get; } = [];

    public Player(BaseZone zone, UdpConnection connection, IResourceManager resourceManager, IZoneManager zoneManager)
    {
        Zone = zone;

        _connection = connection;
        _resourceManager = resourceManager;
        _zoneManager = zoneManager;
        PlayerPet = new PlayerPet(this, resourceManager);
    }

    #region Connection

    public void Send(ISerializablePacket serializablePacket)
    {
        var data = serializablePacket.Serialize();

        SendSerialized(data);
    }

    internal void SendSerialized(byte[] data)
    {
        if (PacketLogger is not null)
            PacketLogHelper.Log(PacketLogger, this, true, data);

        _connection.Send(UdpChannel.Reliable1, data);
    }

    internal static byte[] SerializeTunneled(ISerializablePacket serializablePacket)
    {
        var packetTunneledClientPacket = new PacketTunneledClientPacket
        {
            Payload = serializablePacket.Serialize()
        };

        return packetTunneledClientPacket.Serialize();
    }

    public void SendToVisible(ISerializablePacket serializablePacket, bool sendToSelf = false)
    {
        if (VisiblePlayers.IsEmpty && !sendToSelf)
            return;

        var data = serializablePacket.Serialize();

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendSerialized(data);

        if (sendToSelf)
            SendSerialized(data);
    }

    public void SendTunneled(ISerializablePacket serializablePacket)
    {
        var packetTunneledClientPacket = new PacketTunneledClientPacket
        {
            Payload = serializablePacket.Serialize()
        };

        Send(packetTunneledClientPacket);
    }

    /// <summary>
    /// A tunneled packet that may be lost or overtaken, for state sent many times a second (a car's physics), as
    /// live sent it.
    /// </summary>
    public void SendTunneledUnreliable(ISerializablePacket serializablePacket)
    {
        var data = new PacketTunneledClientPacket
        {
            Reliable = false,
            Payload = serializablePacket.Serialize()
        }.Serialize();

        if (PacketLogger is not null)
            PacketLogHelper.Log(PacketLogger, this, true, data);

        _connection.Send(UdpChannel.Unreliable, data);
    }

    [Obsolete]
    public void SendTunneled(byte[] buffer)
    {
        var packetTunneledClientPacket = new PacketTunneledClientPacket
        {
            Payload = buffer
        };

        Send(packetTunneledClientPacket);
    }

    public void SendTunneledToVisible(ISerializablePacket serializablePacket, bool sendToSelf = false)
    {
        if (VisiblePlayers.IsEmpty && !sendToSelf)
            return;

        var data = SerializeTunneled(serializablePacket);

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendSerialized(data);

        if (sendToSelf)
            SendSerialized(data);
    }

    public void SendTunneledToVisibleDelayed(ISerializablePacket serializablePacket, int delayMs, bool sendToSelf = false)
    {
        lock (_delayedPackets)
        {
            var sendType = sendToSelf ? PacketSendType.AllAndSelf : PacketSendType.All;
            _delayedPackets.Enqueue((serializablePacket, sendType), DateTimeOffset.UtcNow.AddMilliseconds(delayMs));
        }
    }

    public void SendTunneledDelayed(ISerializablePacket packet, int delayMs)
    {
        lock (_delayedPackets)
        {
            _delayedPackets.Enqueue((packet, PacketSendType.SelfOnly), DateTimeOffset.UtcNow.AddMilliseconds(delayMs));
        }
    }

    public bool IsMuted()
    {
        DateTimeOffset currentTime = DateTimeOffset.UtcNow;
        DateTimeOffset? mutedUntil = MutedUntil;
        return mutedUntil.HasValue && mutedUntil > currentTime;
    }

    public void Disconnect()
    {
        _connection.Disconnect();
    }

    public void Dismount()
    {
        if (Mount is null)
            return;

        SendTunneledToVisible(new PacketDismountResponse
        {
            RiderGuid = Guid,
            CompositeEffectId = 46
        }, sendToSelf: true);

        UpdateCharacterStats(
            CharacterStats.GlideEnabled.Set(0),
            CharacterStats.JumpHeight.Set(0f));

        SetMountMovementSpeed(null);

        EntityHelper.RemovePlayerGracefully(Mount, recipients: VisiblePlayers.Values.Append(this));

        foreach (var mountInfo in Mounts)
        {
            if (mountInfo.Guid == Mount.Guid)
                mountInfo.Guid = 0;
        }

        Mount = null;
        if (BoomboxDanceTransform != 0 && TemporaryAppearance == BoomboxDanceTransform)
            SendTunneledToVisible(new PlayerUpdatePacketUpdateTemporaryAppearance
            {
                Guid = Guid,
                TemporaryAppearance = TemporaryAppearance
            }, true);
    }

    #endregion

    #region Update

    public void UpdateEveryTick()
    {
        var now = DateTimeOffset.UtcNow;

        TickEffects(now);

        if (QuestConversation.TalkingNpcGuid != 0 && now >= QuestConversation.TalkAnimationEndsAt)
            QuestDialogue.StopTalkAnimation(this);

        PlayerPet.UpdateEveryTick();
        MinigameManager.Tick(this);
        SendVisiblePositions();

        while (true)
        {
            (ISerializablePacket Packet, PacketSendType SendType) due;

            lock (_delayedPackets)
            {
                if (!_delayedPackets.TryPeek(out _, out var sendAt) || sendAt > now)
                    break; // none or none ready

                due = _delayedPackets.Dequeue();
            }

            switch (due.SendType)
            {
                case PacketSendType.All:
                    SendTunneledToVisible(due.Packet);
                    break;
                case PacketSendType.AllAndSelf:
                    SendTunneledToVisible(due.Packet, sendToSelf: true);
                    break;
                case PacketSendType.SelfOnly:
                    SendTunneled(due.Packet);
                    break;
            }
        }

        foreach (var (key, scheduled) in _delayedSlotPackets)
        {
            if (scheduled.SendAt > now)
                continue;

            if (_delayedSlotPackets.TryRemove(key, out var removed))
                SendTunneled(removed.Packet);
        }
    }

    public ProjectileHelper Projectiles { get; } = new();
    public int NpcAbilityEffectId { get; set; }

    public NpcAbilityDefinition? NpcAbility { get; set; }
    public DateTimeOffset NpcAbilityExpiresAt { get; set; }
    public DateTimeOffset NpcAbilityNextCastAt { get; set; }
    public int NpcAbilityProfileId { get; set; }

    public void UpdateEverySecond()
    {
        RegenerateVitals();
        UpdateRespawn();
        SnowballInteraction.Update(this);
        UpdatePortrait();
        UpdatePendingPointOfInterest();
        UpdateAggro();
    }

    public void RelayPosition(PlayerUpdatePacketUpdatePosition playerUpdatePacketUpdatePosition)
    {
        var data = SerializeTunneled(playerUpdatePacketUpdatePosition);

        LatestPosition = new RelayedPosition(data, playerUpdatePacketUpdatePosition.State, Interlocked.Increment(ref _positionVersion));
    }

    private void SendVisiblePositions()
    {
        if (VisiblePlayers.IsEmpty)
            return;

        var now = Environment.TickCount64;
        var crowded = VisiblePlayers.Count >= CrowdedVisiblePlayerCount;
        List<byte[]>? positionData = null;

        foreach (var (guid, visiblePlayer) in VisiblePlayers)
            QueuePosition(guid, visiblePlayer.LatestPosition, now, crowded, ref positionData);

        if (positionData is not null)
            _connection.Send(UdpChannel.Reliable1, positionData);
    }

    private void QueuePosition(ulong guid, RelayedPosition? latestPosition, long now, bool crowded, ref List<byte[]>? positionData)
    {
        if (latestPosition is null)
            return;

        _sentPositions.TryGetValue(guid, out var sentPosition);

        if (latestPosition.Version == sentPosition.Version)
            return;

        if (latestPosition.State == sentPosition.State && now < sentPosition.NextDueAt)
            return;

        _sentPositions[guid] = (latestPosition.Version, latestPosition.State, crowded ? now + CrowdedRelayIntervalMs : now);

        (positionData ??= []).Add(latestPosition.Data);
    }

    private void UpdatePortrait()
    {
        if (_pendingPortraitCrc is null)
            return;

        if (DateTimeOffset.UtcNow >= _portraitUpdateExpiresAt)
        {
            _pendingPortraitCrc = null;
            return;
        }

        try
        {
            var path = Path.Combine(PortraitStorage.GetCharacterDirectory(Guid), "portrait.crc");

            if (!File.Exists(path))
                return;

            var data = File.ReadAllBytes(path);

            if (data.Length != sizeof(uint) || BitConverter.ToUInt32(data) != _pendingPortraitCrc)
                return;

            var packetPlayerImageDataHeadshot = GetPortraitPacket(PortraitProvider.Headshot);
            var packetPlayerImageDataPortrait = GetPortraitPacket(PortraitProvider.Portrait);

            if (packetPlayerImageDataHeadshot is null || packetPlayerImageDataPortrait is null)
                return;

            SendTunneledToVisible(packetPlayerImageDataHeadshot, true);
            SendTunneledToVisible(packetPlayerImageDataPortrait, true);
            _portraitCrc = _pendingPortraitCrc;
            _pendingPortraitCrc = null;
        }
        catch (IOException)
        {
            // Retry while the upload is being written.
        }
    }

    public void UpdatePortrait(uint portraitCrc)
    {
        if (_portraitCrc == portraitCrc)
            return;

        _pendingPortraitCrc = portraitCrc;
        _portraitUpdateExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30);
    }

    public PacketPlayerImageData? GetPortraitPacket(PortraitProvider provider)
    {
        var fileName = provider switch
        {
            PortraitProvider.Headshot => "headshot.png",
            PortraitProvider.Portrait => "portrait.png",
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

        var path = Path.Combine(PortraitStorage.GetCharacterDirectory(Guid), fileName);

        if (!File.Exists(path))
            return null;

        return new PacketPlayerImageData
        {
            Guid = Guid,
            Provider = provider.ToString(),
            Portrait =
            {
                Unknown2 = 1,
                Guid = Guid,
                ModelId = Model,
                Attachments = GetAttachments(),
                Head = Head,
                Hair = Hair,
                SkinTone = SkinTone,
                FacePaint = FacePaint,
                ModelCustomization = ModelCustomization,
                HairColor = HairColor,
                EyeColor = EyeColor,
                HeadId = HeadId,
                HairId = HairId,
                SkinToneId = SkinToneId,
                FacePaintId = FacePaintId,
                Provider = provider.ToString()
            },
            PngPayload = File.ReadAllBytes(path)
        };
    }

    // The client animates the cooldown sweep itself from TotalRefreshTime -
    // no per-second resend needed for that. But it does NOT re-enable the slot for input on its own once
    // the sweep finishes ("sweep animates but after the sweep I cannot use the ability again") - that
    // needs one explicit packet once the cooldown is actually over. So: one packet now, one packet
    // scheduled for later - not the old repeating-every-second loop, and not silence either.
    public void StartActionBarCooldown(int actionBarId, int slotIndex, int iconId, int nameId, int count, int cooldownMs, int iconTintId = 0, int elapsedMs = 0)
    {
        var remainingMs = Math.Max(0, cooldownMs - elapsedMs);

        SendTunneled(BuildActionBarSlotPacket(actionBarId, slotIndex, iconId, iconTintId, nameId, count, cooldownMs, enabled: false, elapsed: elapsedMs));
        ScheduleSlotPacket(actionBarId, slotIndex, BuildActionBarSlotPacket(actionBarId, slotIndex, iconId, iconTintId, nameId, count, cooldownMs, enabled: true, elapsed: cooldownMs), remainingMs);
    }

    private static ClientUpdatePacketUpdateActionBarSlot BuildActionBarSlotPacket(int actionBarId, int slotIndex, int iconId, int iconTintId, int nameId, int count, int cooldownMs, bool enabled, int elapsed)
    {
        var clientUpdatePacketUpdateActionBarSlot = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = actionBarId, Slot = slotIndex } };
        clientUpdatePacketUpdateActionBarSlot.Slot.IsEmpty = false;
        clientUpdatePacketUpdateActionBarSlot.Slot.IconId = iconId;
        clientUpdatePacketUpdateActionBarSlot.Slot.IconTintId = iconTintId;
        clientUpdatePacketUpdateActionBarSlot.Slot.NameId = nameId;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown5 = ActionBarSlotUnknown5;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown6 = ActionBarSlotUnknown6;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown7 = ActionBarSlotUnknown7;
        clientUpdatePacketUpdateActionBarSlot.Slot.Enabled = enabled;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown10 = elapsed;
        clientUpdatePacketUpdateActionBarSlot.Slot.TotalRefreshTime = cooldownMs;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown12 = elapsed;
        clientUpdatePacketUpdateActionBarSlot.Slot.Quantity = count;
        clientUpdatePacketUpdateActionBarSlot.Slot.ForceDismount = true;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown15 = elapsed;
        return clientUpdatePacketUpdateActionBarSlot;
    }

    public void UpdatePosition(Vector4 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;

        Mount?.UpdatePosition(position, rotation);

        if (LoopingEmoteAnimation != 0)
        {
            var (dx, dz) = (position.X - LoopingEmoteFrom.X, position.Z - LoopingEmoteFrom.Z);
            if (dx * dx + dz * dz > LoopingEmoteWalkAwaySquared)
                StopLoopingEmote();
        }

        if (Visible)
            UpdateZoneTile();
    }

    public void StopLoopingEmote()
    {
        if (LoopingEmoteAnimation == 0)
            return;

        LoopingEmoteAnimation = 0;
        SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
        {
            Guid = Guid,
            AnimationId = 1, // the stand
            Flags = 1
        }, true);
    }

    public bool IsChatChannelOn(ChatChannel chatChannel)
    {
        return ChatChannelStatus.TryGetValue(chatChannel, out var channelStatus)
            ? channelStatus
            : DefaultEnabledChatChannels.Contains(chatChannel);
    }

    // Nearest other player in the zone within range, excluding excludeGuid if given.
    public Player? FindNearestPlayer(float range, ulong excludeGuid = 0)
    {
        Player? nearest = null;
        var nearestDistance = range * range;

        foreach (var candidate in Zone.Players)
        {
            if (candidate.Guid == Guid || candidate.Guid == excludeGuid)
                continue;

            var distance = candidate.Position.ToVector3().DistanceSquaredXZ(Position.ToVector3());

            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = candidate;
        }

        return nearest;
    }

    public Vector3 GetFacingDirection()
    {
        var direction = Rotation.GetFacing();
        var lengthSquared = direction.LengthSquared();

        return float.IsFinite(lengthSquared) && lengthSquared > 0
            ? Vector3.Normalize(direction) : Vector3.UnitZ;
    }

    public IEntity? FindTarget(ulong guid, float range, ulong excludeGuid = 0)
    {
        if (!float.IsFinite(range) || range < 0)
            return null;

        if (guid == 0 || guid == Guid)
            return FindNearestPlayer(range, excludeGuid) ?? FindNearestPlayer(range);

        IEntity? target = VisiblePlayers.TryGetValue(guid, out var player) ? player :
            VisibleNpcs.TryGetValue(guid, out var npc) ? npc : null;
        if (target is null || !ReferenceEquals(target.Zone, Zone))
            return null;

        var distance = Vector3.DistanceSquared(Position.ToVector3(),
            target.Position.ToVector3());

        return float.IsFinite(distance) && distance <= range * range ? target : null;
    }

    private void UpdateZoneTile()
    {
        var newZoneTile = Zone.GetTileFromPosition(Position);

        if (newZoneTile == ZoneTile)
            return;

        if (!Zone.IsOutOfBounds(ZoneTile) && Zone.IsOutOfBounds(newZoneTile))
            Zone.Logger.LogWarning("{name} entered out of bounds tile {tile} at {position}.", Name.FullName, newZoneTile, Position);

        Zone.UpdateEntityZoneTile(this, ZoneTile, newZoneTile);

        ZoneTile = newZoneTile;

        QueuePointOfInterest();
    }

    public bool TeleportToZone(IZone destinationZone, Vector4 position, Quaternion rotation)
    {
        if (Zone == destinationZone)
            return true;

        return TeleportToZone(destinationZone.DefinitionId, destinationZone.OwnerId, position, rotation);
    }

    public bool TeleportToZone(int zoneDefinitionId, ulong? ownerId, Vector4 position, Quaternion rotation)
    {
        if (Zone.DefinitionId == zoneDefinitionId && Zone.OwnerId == ownerId)
            return true;

        var previousPosition = Position;
        var previousRotation = Rotation;
        // The starting zone is the one the saved position is for; other world zones (a fishing pond, a battle) go back there.
        var wasInStartingZone = Zone == _zoneManager.StartingZone;

        if (!_zoneManager.TryMovePlayerToZone(zoneDefinitionId, ownerId, this, position, rotation, out _))
            return false;

        if (wasInStartingZone)
        {
            StartingZonePosition = previousPosition;
            StartingZoneRotation = previousRotation;
        }

        if (_appearanceEffectId != 0 && _effects.TryGetValue(_appearanceEffectId, out var appearanceEffect) && appearanceEffect.ExpiresAt is null)
            RemoveTemporaryAppearance();

        BeginZoning(position, rotation);

        return true;
    }

    /// <summary>
    /// Loads the zone the player is in again where they stand, as entering it does: the client reads the zone's
    /// files again (so a zone editor's saves show), and everything in it comes back once it has loaded.
    /// </summary>
    public void ReloadZone()
    {
        var position = Position;
        var rotation = Rotation;

        OnZoneChanged(Zone, position, rotation);

        BeginZoning(position, rotation);
    }

    private void BeginZoning(Vector4 position, Quaternion rotation)
    {
        PointOfInterestId = 0;
        ChatAreaNameId = 0;

        if (Mount is not null && !Mount.TeleportToZone(Zone, position, rotation))
            Dismount();

        var packetClientBeginZoning = new PacketClientBeginZoning
        {
            Name = Zone.Name,
            Position = position,
            Rotation = rotation,
            Sky = Zone.Sky,
            Id = Zone.Id,
            GeometryId = 214,
            OverrideUpdateRadius = true
        };

        SendTunneled(packetClientBeginZoning);

        ClearObjectives();
    }

    private void QueuePointOfInterest()
    {
        var zoneAreaDefinition = Zone is WorldZone worldZone ? worldZone.GetZoneArea(Position, ZoneTile) : null;

        if (zoneAreaDefinition is null || zoneAreaDefinition.Id == PointOfInterestId)
        {
            _pendingZoneArea = null;
            return;
        }

        if (_pendingZoneArea?.Id == zoneAreaDefinition.Id)
            return;

        _pendingZoneArea = zoneAreaDefinition;
        _pendingZoneAreaSince = Environment.TickCount64;
    }

    private void UpdatePendingPointOfInterest()
    {
        if (_pendingZoneArea is null || Environment.TickCount64 - _pendingZoneAreaSince < AreaSettleMilliseconds)
            return;

        UpdatePointOfInterest(_pendingZoneArea);
    }

    public void UpdatePointOfInterest()
    {
        if (Zone is WorldZone worldZone && worldZone.GetZoneArea(Position, ZoneTile) is { } zoneAreaDefinition && zoneAreaDefinition.Id != PointOfInterestId)
            UpdatePointOfInterest(zoneAreaDefinition);
    }

    public void UpdatePointOfInterest(ZoneAreaDefinition? zoneAreaDefinition)
    {
        _pendingZoneArea = null;

        if (zoneAreaDefinition is null || zoneAreaDefinition.Id == PointOfInterestId)
            return;

        PointOfInterestId = zoneAreaDefinition.Id;
        ChatAreaNameId = zoneAreaDefinition.ChatAreaNameId;

        var packetPOIChangeMessage = new PacketPOIChangeMessage
        {
            NameId = zoneAreaDefinition.NameId,
            ZoneId = zoneAreaDefinition.Id,
            AreaId = zoneAreaDefinition.AreaId
        };

        SendTunneled(packetPOIChangeMessage);

        var chatPacketEnterArea = new ChatPacketEnterArea
        {
            AreaNameId = ChatAreaNameId
        };

        SendTunneled(chatPacketEnterArea);
    }

    public void SetMountMovementSpeed(float? mountMovementSpeed)
    {
        _mountMovementSpeed = mountMovementSpeed;

        RefreshMovementSpeed();
    }

    private void RefreshMovementSpeed()
    {
        var maxMovementSpeed = _mountMovementSpeed ?? OnFootMovementSpeed + _effects.Values.Sum(effect => effect.MovementSpeedBonus);

        UpdateCharacterStats(CharacterStats.MaxMovementSpeed.Set(maxMovementSpeed));
    }

    public void UpdateCharacterStats(params CharacterStat[] characterStats)
    {
        var clientUpdatePacketUpdateStat = new ClientUpdatePacketUpdateStat
        {
            Guid = Guid
        };

        clientUpdatePacketUpdateStat.Stats.AddRange(characterStats);

        SendTunneled(clientUpdatePacketUpdateStat);

        foreach (var characterStat in characterStats)
        {
            Stats[characterStat.Id] = characterStat;

            if (characterStat.Id == CharacterStatId.MaxMovementSpeed)
            {
                var playerUpdatePacketExpectedSpeed = new PlayerUpdatePacketExpectedSpeed
                {
                    Guid = Guid,
                    ExpectedSpeed = characterStat.Float
                };

                SendTunneledToVisible(playerUpdatePacketExpectedSpeed);
            }
        }
    }

    #endregion

    #region Events

    public void OnAddVisibleNpcs(params IEnumerable<Npc> npcs)
    {
        // A slain mob stays out of view until it respawns.
        var visibleNpcs = npcs.Where(npc => npc.IsVisibleTo(this) && npc.Combat?.IsDespawned != true).ToArray();

        foreach (var npc in visibleNpcs)
        {
            if (npc is Mount)
                continue;

            var playerUpdatePacketAddNpc = npc.GetAddNpcPacket();

            // Gives referees in god mode the mouseover glow on everything they can edit, props included.
            if (IsInGodMode && RefereeNpcInteractions.CanEdit(npc))
                playerUpdatePacketAddNpc.IsInteractable = true;

            if (npc is PetTreasureChest petTreasureChest && !petTreasureChest.IsOwner(this))
                playerUpdatePacketAddNpc.IsInteractable = false;

            // Placed objects (house furniture, world props) must be interactable for everyone: otherwise the
            // client neither collides with them nor keeps their placed height (it drops them to the ground).
            // Furniture's cursor only shows while its owner decorates (see HousingZone.SetEditMode).
            if (npc.IsHouseFixture || npc.IsWorldProp)
                playerUpdatePacketAddNpc.IsInteractable = true;

            SendTunneled(playerUpdatePacketAddNpc);

            // Placed objects get no idle state, which would loop their animation (and any sound in it).
            if (!npc.IsHouseFixture && !npc.IsWorldProp)
                SendTunneled(new PlayerUpdatePacketUpdateCharacterState { Guid = npc.Guid, State = npc.CharacterState });

            // A hurt mob's health bar, which AddNpc shows as full.
            if (npc.Combat is { } combat)
                SendTunneled(combat.GetHitpointsPacket());
        }

        var playerUpdatePacketNpcRelevance = InteractionMenuHelper.GetNpcRelevancePacket(
            visibleNpcs.Where(npc => npc is not PetTreasureChest petTreasureChest || petTreasureChest.IsOwner(this)), IsInGodMode);
        if (playerUpdatePacketNpcRelevance.Entries.Count > 0)
            SendTunneled(playerUpdatePacketNpcRelevance);

        var playerUpdatePacketAddNotifications = new PlayerUpdatePacketAddNotifications();

        foreach (var npc in visibleNpcs)
            playerUpdatePacketAddNotifications.Notifications.AddRange(GetNotificationsFor(npc));

        if (playerUpdatePacketAddNotifications.Notifications.Count > 0)
            SendTunneled(playerUpdatePacketAddNotifications);

        foreach (var npc in visibleNpcs)
            VisibleNpcs.TryAdd(npc.Guid, npc);
    }

    public IEnumerable<NotificationInfo> GetNotificationsFor(Npc npc)
    {
        foreach (var notification in npc.Notifications)
            yield return notification;

        if ((Zone.GetQuestBadge(this, npc) ?? npc.Notification) is { } badge)
            yield return badge;
    }

    public void OnAddVisiblePlayers(params IEnumerable<Player> players)
    {
        foreach (var player in players)
        {
            if (player.Mount is not null)
            {
                var addPc = player.GetAddPcPacket();
                addPc.MountGuid = 0;
                addPc.MountSeat = -1;
                addPc.MountQueuePosition = -1;
                addPc.NameVerticalOffset = 0;

                SendTunneled(addPc);
                SendTunneled(player.Mount.GetAddNpcPacket());
                SendTunneled(player.Mount.GetMountResponsePacket());
            }
            else
                SendTunneled(player.GetAddPcPacket());

            if (player.Scale != DefaultScale)
                SendTunneled(new PlayerUpdatePacketUpdateScale { Guid = player.Guid, Scale = player.Scale });

            SendTunneled(new PlayerUpdatePacketUpdateCharacterState { Guid = player.Guid, State = player.WithModes(player.IsDead ? KnockedOutCharacterState : IdleCharacterState) });

            SendTunneled(player.GetWeaponFlairOverridePacket());

            foreach (var effect in player._effects.Values.Where(effect => effect.WorldEffectStarted))
                SendTunneled(player.GetAddEffectTagCompositeEffectPacket(effect));

            if (player.Mount is null && player.BoomboxDanceIsStanding && player.BoomboxDanceAnimation != 0)
                SendTunneled(new PlayerUpdatePacketSetAnimation
                {
                    Guid = player.Guid,
                    AnimationId = player.BoomboxDanceAnimation,
                    Flags = 1
                });

            if (player.Mount is null && player.LoopingEmoteAnimation != 0)
                SendTunneled(new PlayerUpdatePacketSetAnimation
                {
                    Guid = player.Guid,
                    AnimationId = player.LoopingEmoteAnimation,
                    Flags = 1
                });
        }

        foreach (var player in players)
            VisiblePlayers.TryAdd(player.Guid, player);
    }

    public void OnRemoveVisibleNpcs(params IEnumerable<Npc> npcs)
    {
        PlayerUpdatePacketRemoveNotifications? playerUpdatePacketRemoveNotifications = null;

        foreach (var npc in npcs)
        {
            foreach (var notification in npc.Notifications)
                (playerUpdatePacketRemoveNotifications ??= new()).Notifications.Add(GetRemoveNotificationInfo(notification));

            if (npc.Notification is not null)
                (playerUpdatePacketRemoveNotifications ??= new()).Notifications.Add(GetRemoveNotificationInfo(npc.Notification));
        }

        if (playerUpdatePacketRemoveNotifications is not null)
            SendTunneled(playerUpdatePacketRemoveNotifications);

        foreach (var npc in npcs)
        {
            if (npc is Mount)
                continue;

            SendTunneled(new PlayerUpdatePacketRemovePlayer { Guid = npc.Guid });
        }

        foreach (var npc in npcs)
            VisibleNpcs.TryRemove(npc.Guid, out _);
    }

    private static RemoveNotificationInfo GetRemoveNotificationInfo(NotificationInfo notification) => new()
    {
        Guid = notification.Guid,
        ThoughtBubbleIconId = notification.Unknown,
        CompositeEffectId = notification.Unknown2
    };

    public void OnRemoveVisibleNpcGracefully(Npc npc, bool animate)
    {
        if (npc is Mount)
            return;

        SendTunneled(EntityHelper.GetRemovePlayerGracefullyPacket(npc.Guid, animate));

        VisibleNpcs.TryRemove(npc.Guid, out _);
    }

    public void OnRemoveVisiblePlayers(params IEnumerable<Player> players)
    {
        foreach (var player in players)
        {
            SendTunneled(new PlayerUpdatePacketRemovePlayer { Guid = player.Guid });

            if (player.Mount is not null)
                SendTunneled(new PlayerUpdatePacketRemovePlayer { Guid = player.Mount.Guid });
        }

        foreach (var player in players)
        {
            VisiblePlayers.TryRemove(player.Guid, out _);
            _sentPositions.TryRemove(player.Guid, out _);
        }
    }

    public void OnInteract(Player player)
    {
        var commandPacketInteractionList = new CommandPacketInteractionList();

        commandPacketInteractionList.List.Guid = Guid;

        commandPacketInteractionList.List.Interactions.Add(InspectInteraction.Data);

        if (!player.Ignores.Any(x => x.Guid == Guid))
            commandPacketInteractionList.List.Interactions.Add(TradeInteraction.Data);

        if (GroupInviteInteraction.CanInvite(player, this))
            commandPacketInteractionList.List.Interactions.Add(GroupInviteInteraction.Data);

        if (Friends.Any(x => x.Guid == player.Guid))
        {
            commandPacketInteractionList.List.Interactions.Add(RemoveFriendInteraction.Data);
        }
        else
        {
            commandPacketInteractionList.List.Interactions.Add(AddFriendInteraction.Data);
        }

        if (player.Ignores.Any(x => x.Guid == Guid))
        {
            commandPacketInteractionList.List.Interactions.Add(StopIgnoringInteraction.Data);
        }
        else if (!Friends.Any(x => x.Guid == player.Guid))
        {
            commandPacketInteractionList.List.Interactions.Add(IgnoreInteraction.Data);
        }

        if (GuildData is null && GuildInviteInteraction.CanInvite(player))
            commandPacketInteractionList.List.Interactions.Add(GuildInviteInteraction.Data);

        player.SendTunneled(commandPacketInteractionList);
    }

    #endregion

    public ClientItem? GetEquippedItem(int slot) =>
        ActiveProfile.Items.TryGetValue(slot, out var profileItem)
            ? Items.FirstOrDefault(item => item.Id == profileItem.Id)
            : null;

    public int GetWieldType()
    {
        foreach (var equipmentSlot in _resourceManager.EquipmentSlots.ByAnimationPriority)
        {
            if (GetEquippedItem(equipmentSlot.Id) is not { } item
                || !_resourceManager.ClientItemDefinitions.TryGetValue(item.Definition, out var definition)
                || definition.Class == 0)
                continue;

            if (!_resourceManager.ItemClasses.TryGetValue(definition.Class, out var itemClass))
                return 0;

            if (itemClass.WieldType != 0)
                return itemClass.WieldType;
        }

        return 0;
    }

    public int GetEquippedCompositeEffectId(int slot)
    {
        var item = GetEquippedItem(slot);
        return item is not null && _resourceManager.ClientItemDefinitions.TryGetValue(item.Definition, out var definition)
            ? Math.Max(0, definition.CompositeEffectId)
            : 0;
    }

    public PlayerUpdatePacketSlotCompositeEffectOverride GetSlotCompositeEffectOverridePacket(int slot, int compositeEffect) => new()
    {
        Guid = Guid,
        Slot = slot,
        CompositeEffect = Math.Max(0, compositeEffect)
    };

    public PlayerUpdatePacketSlotCompositeEffectOverride GetWeaponFlairOverridePacket() =>
        GetSlotCompositeEffectOverridePacket(WeaponSlot, GetEquippedCompositeEffectId(FlairShardSlot));

    public void ClearWeaponFlairOverride() =>
        SendTunneledToVisible(GetSlotCompositeEffectOverridePacket(WeaponSlot, 0), true);

    public void RefreshWeaponFlair(int profileId, int changedSlot)
    {
        if (profileId != ActiveProfileId || (changedSlot != WeaponSlot && changedSlot != FlairShardSlot))
            return;

        // Job switches reuse the cached attachment effect until the new weapon appears.
        if (GetEquippedItem(WeaponSlot) is { } item
            && _resourceManager.ClientItemDefinitions.TryGetValue(item.Definition, out var definition)
            && GetAttachment(WeaponSlot) is { } attachment)
        {
            SendTunneledToVisible(new PlayerUpdatePacketEquipItemChange
            {
                Guid = Guid,
                Id = item.Id,
                Attachment = attachment,
                ProfileId = ActiveProfileId,
                ItemClassId = definition.Class,
                WieldType = GetWieldType()
            }, true);
        }

        SendTunneledToVisible(GetWeaponFlairOverridePacket(), true);
    }

    public List<CharacterAttachmentData> GetAttachments()
    {
        var list = new List<CharacterAttachmentData>();

        foreach (var profileItem in ActiveProfile.Items)
        {
            var attachment = GetAttachment(profileItem.Key);

            if (attachment is null)
                continue;

            list.Add(attachment);
        }

        return list;
    }

    public CharacterAttachmentData? GetAttachment(int slot)
    {
        var clientItem = GetEquippedItem(slot);

        if (clientItem is null)
            return null;

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
            return null;

        var compositeEffectId = clientItemDefinition.CompositeEffectId;

        if (slot == WeaponSlot)
        {
            var flairEffectId = GetEquippedCompositeEffectId(FlairShardSlot);

            if (flairEffectId > 0)
                compositeEffectId = flairEffectId;
        }

        return new CharacterAttachmentData
        {
            ModelName = clientItemDefinition.ModelName,
            TextureAlias = ItemDefinitionHelper.GetAttachmentTextureAlias(clientItemDefinition.TextureAlias, SkinTone),
            TintAlias = clientItemDefinition.TintAlias,
            TintId = clientItem.Tint == 0 ? clientItemDefinition.Icon.TintId : clientItem.Tint,
            CompositeEffectId = compositeEffectId,
            Slot = clientItemDefinition.Slot
        };
    }

    public PlayerUpdatePacketAddPc GetAddPcPacket()
    {
        bool isReferee = IsMod || IsAdmin;
        var playerUpdatePacketAddPc = new PlayerUpdatePacketAddPc
        {
            Guid = Guid,

            Name = Name,

            Model = Model,

            ChatBubbleForegroundColor = ChatBubbleForegroundColor,
            ChatBubbleBackgroundColor = ChatBubbleBackgroundColor,
            ChatBubbleSize = ChatBubbleSize,

            Position = Position,
            Rotation = Rotation,

            Attachments = GetAttachments(),

            Head = Head,
            Hair = Hair,

            HairColor = HairColor,
            EyeColor = EyeColor,

            SkinTone = SkinTone,

            FacePaint = FacePaint,
            ModelCustomization = ModelCustomization,

            MaxMovementSpeed = Stats[CharacterStatId.MaxMovementSpeed],

            IsUnderage = Age < 18,
            IsReferee = isReferee,

            TemporaryAppearance = TemporaryAppearance,

            ActiveProfileId = ActiveProfileId,

            WieldType = GetWieldType(),

            MountQueuePosition = -1,
            MountSeat = -1,
        };

        var activeTitle = Titles.FirstOrDefault(x => x.Id == ActiveTitle);

        if (activeTitle is not null)
            playerUpdatePacketAddPc.Title = activeTitle;

        if (Mount is not null)
        {
            playerUpdatePacketAddPc.MountGuid = Mount.Guid;
            playerUpdatePacketAddPc.MountSeat = Mount.Seat;
            playerUpdatePacketAddPc.MountQueuePosition = Mount.QueuePosition;

            playerUpdatePacketAddPc.NameVerticalOffset = Mount.Definition.NameVerticalOffset;
        }

        if (GuildData is not null)
            playerUpdatePacketAddPc.Guilds.Add(0, GuildData.Guid);

        return playerUpdatePacketAddPc;
    }

    public const int ChangeFormBuffIconId = 3843;

    public void ApplyTemporaryAppearance(int modelId, int durationMs, int effectId = 0, int buffNameId = 0)
    {
        if (_appearanceEffectId != 0)
            RemoveEffect(_appearanceEffectId);

        _appearanceEffectId = AddEffect(new PlayerEffect
        {
            ExpiresAt = durationMs > 0 ? DateTimeOffset.UtcNow.AddMilliseconds(durationMs) : null,
            AppearanceModelId = modelId,
            AppearancePoofEffectId = effectId,
            BuffIconId = buffNameId != 0 ? ChangeFormBuffIconId : 0,
            BuffNameId = buffNameId,
            OnRemoved = () => _appearanceEffectId = 0
        });
    }

    public void RemoveTemporaryAppearance()
    {
        if (_appearanceEffectId != 0)
            RemoveEffect(_appearanceEffectId);
    }

    public void SetScale(float scale)
    {
        if (!float.IsFinite(scale) || scale <= 0)
            return;

        Scale = scale;

        SendTunneledToVisible(new PlayerUpdatePacketUpdateScale { Guid = Guid, Scale = scale }, true);
    }

    public void ApplyTemporaryScale(float scale, int durationMs, int buffNameId = 0)
    {
        if (_scaleEffectId != 0)
            RemoveEffect(_scaleEffectId);

        _scaleEffectId = AddEffect(new PlayerEffect
        {
            ExpiresAt = durationMs > 0 ? DateTimeOffset.UtcNow.AddMilliseconds(durationMs) : null,
            Scale = scale,
            BuffIconId = buffNameId != 0 ? ChangeFormBuffIconId : 0,
            BuffNameId = buffNameId,
            OnRemoved = () => _scaleEffectId = 0
        });
    }

    public void RemoveTemporaryScale()
    {
        if (_scaleEffectId != 0)
            RemoveEffect(_scaleEffectId);
    }

    #region Effects

    // A tracked, timed effect on the player - a transformation, a food effect aura, or both a
    // world-visible composite effect and a buff bar icon at once. One tick loop expires them,
    // one pair of methods applies/unapplies whatever packets each of those parts needs, so adding
    // a new kind of effect doesn't mean adding another set of ad hoc fields and conditions.
    private readonly ConcurrentDictionary<int, PlayerEffect> _effects = new();
    private int _appearanceEffectId;
    private int _scaleEffectId;
    private float? _mountMovementSpeed;

    public int AddEffect(PlayerEffect effect)
    {
        effect.Id = EffectTagIdGenerator.Next();
        _effects[effect.Id] = effect;

        ApplyEffect(effect);

        return effect.Id;
    }

    public void RemoveEffect(int id)
    {
        if (!_effects.TryRemove(id, out var effect))
            return;

        UnapplyEffect(effect);
        effect.OnRemoved?.Invoke();
    }

    public IEnumerable<PlayerEffect> Effects => _effects.Values;

    public void RemoveEffectGroup(string group)
    {
        foreach (var effect in _effects.Values)
        {
            if (string.Equals(effect.Group, group, StringComparison.OrdinalIgnoreCase))
                RemoveEffect(effect.Id);
        }
    }

    private void TickEffects(DateTimeOffset now)
    {
        List<PlayerEffect> starting = [];
        List<PlayerEffect> ticking = [];
        List<int> expired = [];

        foreach (var effect in _effects.Values)
        {
            if (!effect.WorldEffectStarted && effect.WorldEffectId != 0 &&
                (effect.WorldEffectStartsAt is null || effect.WorldEffectStartsAt <= now))
                starting.Add(effect);

            if (effect.TicksRemaining > 0 && effect.NextTickAt <= now)
                ticking.Add(effect);

            if (effect.ExpiresAt is { } expiresAt && expiresAt <= now)
                expired.Add(effect.Id);
        }

        foreach (var effect in starting)
            StartWorldEffect(effect);

        // Before expiry, so the last tick of a restore over time still lands on the tick it expires.
        foreach (var effect in ticking)
            TickRestore(effect, now);

        foreach (var id in expired)
            RemoveEffect(id);
    }

    private void TickRestore(PlayerEffect effect, DateTimeOffset now)
    {
        var ticks = 0;

        while (effect.TicksRemaining > 0 && effect.NextTickAt <= now)
        {
            effect.TicksRemaining--;
            effect.NextTickAt = effect.NextTickAt.AddMilliseconds(effect.TickIntervalMs);
            ticks++;
        }

        if (effect.HealthPerTick != 0)
            Heal(effect.HealthPerTick * ticks);

        if (effect.EnergyPerTick != 0)
            RestoreEnergy(effect.EnergyPerTick * ticks);
    }

    // Stat values from before any effect touched them, so bonuses come off cleanly whatever order they expire in.
    private readonly ConcurrentDictionary<CharacterStatId, float> _statBases = new();

    public float GetStatBonus(CharacterStatId statId) =>
        _effects.Values.Sum(effect => effect.StatBonuses?.GetValueOrDefault(statId) ?? 0f);

    private float GetStatValue(CharacterStatId statId) =>
        Stats.TryGetValue(statId, out var stat) ? stat.IsFloat ? stat.Float : stat.Int : 0f;

    private void RefreshStatBonuses(IEnumerable<CharacterStatId> statIds)
    {
        List<CharacterStat> changed = [];

        foreach (var statId in statIds)
        {
            Stats.TryGetValue(statId, out var current);

            var isFloat = current?.IsFloat ?? false;
            var bonus = GetStatBonus(statId);

            float value;

            if (bonus != 0)
                value = _statBases.GetOrAdd(statId, _ => current is null ? 0f : isFloat ? current.Float : current.Int) + bonus;
            else if (_statBases.TryRemove(statId, out var baseValue))
                value = baseValue;
            else
                continue;

            changed.Add(isFloat ? new CharacterStat(statId, value) : new CharacterStat(statId, (int)MathF.Round(value)));
        }

        if (changed.Count == 0)
            return;

        UpdateCharacterStats([.. changed]);

        // Re-clamp against the new maximums.
        if (changed.Any(stat => stat.Id == CharacterStatId.MaxHealth))
            Hitpoints = _hitpoints;

        if (changed.Any(stat => stat.Id == CharacterStatId.MaxMana))
            Energy = _energy;
    }

    private void ApplyEffect(PlayerEffect effect)
    {
        if (effect.AppearanceModelId != 0)
        {
            TemporaryAppearance = effect.AppearanceModelId;

            if (effect.AppearancePoofEffectId != 0)
                SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect { Guid = Guid, CompositeEffectId = effect.AppearancePoofEffectId, Position = Position, Clear = false }, true);

            SendTunneledToVisible(new PlayerUpdatePacketUpdateTemporaryAppearance { Guid = Guid, TemporaryAppearance = effect.AppearanceModelId }, true);

            ResyncWorldEffects();
        }

        if (effect.Scale != 0)
            SetScale(effect.Scale);

        if (effect.WorldEffectId != 0 && (effect.WorldEffectStartsAt is null || effect.WorldEffectStartsAt <= DateTimeOffset.UtcNow))
            StartWorldEffect(effect);

        if (effect.BuffIconId != 0)
            SendEffectTag(effect);

        if (effect.MovementSpeedBonus != 0)
            RefreshMovementSpeed();

        if (effect.StatBonuses is { Count: > 0 } statBonuses)
            RefreshStatBonuses(statBonuses.Keys);

        if (effect.TickCount > 0 && effect.TickIntervalMs > 0)
        {
            effect.TicksRemaining = effect.TickCount;
            effect.NextTickAt = DateTimeOffset.UtcNow.AddMilliseconds(effect.TickIntervalMs);
        }
    }

    public bool RefreshEffect(int id, DateTimeOffset expiresAt)
    {
        if (!_effects.TryGetValue(id, out var effect))
            return false;

        effect.ExpiresAt = expiresAt;

        if (effect.BuffIconId != 0)
            SendEffectTag(effect);

        return true;
    }

    private void SendEffectTag(PlayerEffect effect)
    {
        var durationSeconds = effect.ExpiresAt is { } expiresAt
            ? Math.Max(1, (int)(expiresAt - DateTimeOffset.UtcNow).TotalSeconds)
            : 0;

        SendTunneled(new ClientUpdatePacketAddEffectTag
        {
            TagId = effect.Id,
            EffectTag = new EffectTag
            {
                InstanceId = effect.Id,
                DefinitionId = effect.BuffDefinitionId,
                Duration = durationSeconds,
                StartTime = 0,
                StopTime = (uint)-durationSeconds
            },
            IconId = effect.BuffIconId,
            NameId = effect.BuffNameId
        });
    }

    private void UnapplyEffect(PlayerEffect effect)
    {
        if (effect.AppearanceModelId != 0)
        {
            TemporaryAppearance = 0;
            SendTunneledToVisible(new PlayerUpdatePacketRemoveTemporaryAppearance { Guid = Guid }, true);
            ResyncWorldEffects();
        }

        if (effect.Scale != 0)
            SetScale(_effects.Values.FirstOrDefault(e => e.Id != effect.Id && e.Scale != 0)?.Scale ?? DefaultScale);

        if (effect.WorldEffectStarted)
        {
            SendTunneledToVisible(new PlayerUpdatePacketRemoveEffectTagCompositeEffect { Guid = Guid, TagId = effect.WorldTagId }, true);
            effect.WorldEffectStarted = false;
        }

        if (effect.BuffIconId != 0)
            SendTunneled(new ClientUpdatePacketRemoveEffectTag { TagId = effect.Id });

        if (effect.MovementSpeedBonus != 0)
            RefreshMovementSpeed();

        if (effect.StatBonuses is { Count: > 0 } statBonuses)
            RefreshStatBonuses(statBonuses.Keys);
    }

    private void StartWorldEffect(PlayerEffect effect)
    {
        effect.WorldEffectStarted = true;
        effect.WorldTagId = EffectTagIdGenerator.Next();

        SendTunneledToVisible(GetAddEffectTagCompositeEffectPacket(effect), true);
    }

    private PlayerUpdatePacketAddEffectTagCompositeEffect GetAddEffectTagCompositeEffectPacket(PlayerEffect effect) => new()
    {
        Guid = Guid,
        TagId = effect.WorldTagId,
        CompositeEffectId = effect.WorldEffectId,
        SourceGuid = effect.SourceGuid != 0 ? effect.SourceGuid : Guid
    };

    // A temporary appearance change is unreliable about which world-visible effect tags survive
    // it - sometimes a tag keeps playing across it, sometimes it doesn't, and the client also
    // ignores a repeat of a tag id it's already seen for this actor. Rather than guess which case
    // this is, explicitly drop and re-add every effect that's supposed to be showing one, with a
    // fresh id, on every appearance change in either direction.
    private void ResyncWorldEffects()
    {
        var active = _effects.Values.Where(e => e.WorldEffectStarted).ToList();

        foreach (var effect in active)
        {
            SendTunneledToVisible(new PlayerUpdatePacketRemoveEffectTagCompositeEffect { Guid = Guid, TagId = effect.WorldTagId }, true);
            StartWorldEffect(effect);
        }
    }

    #endregion

    #region Combat


    private const int DefaultMaxHitpoints = 2500;

    private int _hitpoints = DefaultMaxHitpoints;

    public int Hitpoints
    {
        get => _hitpoints;
        set
        {
            _hitpoints = Math.Clamp(value, 0, MaxHitpoints);

            SendTunneled(new ClientUpdatePacketHitpoints
            {
                CurrentHitpoints = _hitpoints,
                MaxHitpoints = MaxHitpoints
            });
        }
    }

    public int MaxHitpoints => GetStatValue(CharacterStatId.MaxHealth) is var maxHealth and > 0 ? (int)maxHealth : DefaultMaxHitpoints;

    private int _energy = 100;

    public int Energy
    {
        get => _energy;
        set
        {
            _energy = Math.Clamp(value, 0, EffectiveMaxEnergy);

            SendTunneled(new ClientUpdatePacketMana
            {
                CurrentMana = _energy,
                MaxMana = EffectiveMaxEnergy
            });
        }
    }

    private int MaxEnergy { get; set; } = 100;

    // MaxMana bonuses (energy scrolls and foods) on top of the job kit's energy.
    public int EffectiveMaxEnergy => MaxEnergy + (int)MathF.Round(GetStatBonus(CharacterStatId.MaxMana));

    public void Heal(int amount)
    {
        // A defeated player waits for their respawn; a heal over time mustn't stand them back up.
        if (IsDead)
            return;

        if (amount > 0 && _hitpoints < MaxHitpoints)
            Hitpoints = _hitpoints + amount;
    }

    public void RestoreEnergy(int amount)
    {
        if (IsDead)
            return;

        if (amount > 0 && _energy < EffectiveMaxEnergy)
            Energy = _energy + amount;
    }

    public bool IsDead => _hitpoints <= 0;

    /// <summary>
    /// Knocked out by a mob and still down: waiting to recover at the zone's start, or for an ally to revive them.
    /// </summary>
    public bool IsKnockedOut => IsDead && _knockOut.IsDown;

    /// <summary>
    /// Just revived, so mobs leave them be for a moment (see <see cref="CombatRules.ReviveProtectionSeconds"/>).
    /// </summary>
    public bool IsReviveProtected => _knockOut.IsProtected(Environment.TickCount64);

    private long _lastCombatAt = long.MinValue;
    private readonly KnockOutTimer _knockOut = new();

    // The mobs with this player on their threat list (see NpcCombat.HasAggroOn), by guid.
    private readonly Dictionary<ulong, Npc> _aggro = [];

    /// <summary>
    /// A mob has them on its threat list: it spotted them, they clicked or hit it, or they came close to its fight.
    /// </summary>
    public bool HasAggro
    {
        get
        {
            lock (_aggro)
                return _aggro.Count > 0;
        }
    }

    /// <summary>
    /// In a fight: while any mob has aggro on them (see <see cref="HasAggro"/>), and for
    /// <see cref="CombatRules.OutOfCombatSeconds"/> after a hit that came without it (a script's npc:attack).
    /// Once no mob has them they're out, and the map and job picker are free again.
    /// </summary>
    public bool IsInCombat => HasAggro || CombatRules.IsInCombat(_lastCombatAt, Environment.TickCount64);

    /// <summary>
    /// Out of any fight now (leaving a battle, knocked out, back on their feet at the zone's start), not in a few
    /// seconds. The mobs that had them forget them on their next think, as they can no longer be attacked.
    /// </summary>
    public void LeaveCombat()
    {
        DropAllAggro();

        _lastCombatAt = long.MinValue;
        UpdateCombatMode();
    }

    /// <summary>
    /// <paramref name="mob"/> put them on its threat list: they're in a fight, and get their combat job out. The
    /// mob says so on every think, so for a player who already knows it does nothing.
    /// </summary>
    public void AddAggro(Npc mob)
    {
        // Knocked out, protected or gone since the mob last looked: it lets them go on its next think.
        if (!NpcCombat.CanBeAttacked(this, mob))
            return;

        lock (_aggro)
        {
            if (!_aggro.TryAdd(mob.Guid, mob))
                return;
        }

        EnterCombat();
    }

    /// <summary>
    /// <paramref name="mob"/> let them go: it died, gave up at its leash, walked home or lost them. With no mob left
    /// on them they're out of the fight.
    /// </summary>
    public void DropAggro(Npc mob)
    {
        lock (_aggro)
        {
            if (!_aggro.Remove(mob.Guid))
                return;
        }

        UpdateCombatMode();
    }

    private void DropAllAggro()
    {
        lock (_aggro)
            _aggro.Clear();
    }

    /// <summary>
    /// Lets go of any mob that no longer has them, in case its word went astray, and says so once a fight is over.
    /// </summary>
    private void UpdateAggro()
    {
        KeyValuePair<ulong, Npc>[] aggro;

        lock (_aggro)
            aggro = [.. _aggro];

        foreach (var (guid, mob) in aggro)
        {
            if (ReferenceEquals(mob.Zone, Zone) && mob.Combat?.HasAggroOn(Guid) == true)
                continue;

            lock (_aggro)
                _aggro.Remove(guid);
        }

        UpdateCombatMode();
    }

    /// <summary>
    /// The mobs that have them on their threat list, for <c>!area here</c>.
    /// </summary>
    public IReadOnlyList<Npc> GetAggro()
    {
        lock (_aggro)
            return [.. _aggro.Values];
    }

    /// <summary>
    /// The combat job last used, which a fight switches back to.
    /// </summary>
    public int LastCombatProfileId { get; private set; }

    public bool IsInCombatJob => _resourceManager.CombatJobs.ContainsKey(ActiveProfileId);

    /// <summary>
    /// Owns a combat job, so it can fight back. Mobs leave players without one alone.
    /// </summary>
    public bool CanFight => IsInCombatJob || GetCombatProfileId() != 0;

    private int GetCombatProfileId() =>
        CombatRules.PickCombatProfileId(Profiles, [.. _resourceManager.CombatJobs.Keys], LastCombatProfileId);

    /// <summary>
    /// A fight is starting (a mob's aggro, or the player's own attack): a player in a non-combat job (a Miner, a
    /// Chef...) switches to their combat job, as clicking an enemy in Free Realms does, and stays in it afterwards.
    /// The fight itself lasts while a mob has aggro on them (see <see cref="IsInCombat"/>).
    /// </summary>
    public void EnterCombat()
    {
        // A revived player who picks a fight is fair game again.
        _knockOut.EndProtection();

        if (!IsInCombatJob)
        {
            var profileId = GetCombatProfileId();

            if (profileId != 0 && ActivateProfile(profileId))
                ChatHelper.SendSystemMessage(this, "You switched to your combat job to fight.");
        }

        UpdateCombatMode();
    }

    /// <summary>
    /// Puts a player in a non-combat job in their combat job, as entering a battle does. False if they're in one
    /// already or have none.
    /// </summary>
    public bool TrySwitchToCombatJob()
    {
        if (IsInCombatJob)
            return false;

        var profileId = GetCombatProfileId();

        return profileId != 0 && ActivateProfile(profileId);
    }

    /// <summary>
    /// The client's overworld combat mode, which needs both packets: in world combat (see
    /// <see cref="IsInWorldCombat"/>) and fighting (see <see cref="IsFighting"/>). While both are on the client counts
    /// the player in combat, as its CallHandler.SetInWorldCombat does it: the game dock's combat indicator lit, its
    /// map, welcome and mail buttons greyed out, no job change, popups held back. (A hostile NPC's red nameplate and
    /// clicks come with its own AddNpc, see <see cref="Npc.GetAddNpcPacket"/>.)
    /// </summary>
    public void SendCombatMode()
    {
        lock (_combatModeLock)
        {
            SendInWorldCombat(force: true);
            SendFighting(force: true);
        }
    }

    /// <summary>
    /// InWorldCombat (EncounterOverworldCombatPacket, see <see cref="CombatRules.IsInWorldCombat"/>). Live sent it for
    /// whole regions, which kept the job picker to combat jobs there ("You cannot select this job while in a combat
    /// area."); here it's on only while a mob has them. A battle's or a 3D game's own zone isn't the overworld: its
    /// rules come with EncounterDataCommon instead (live sent 0 in a battle, so the knocked out window offers only the
    /// respawn an instance has).
    /// </summary>
    public bool IsInWorldCombat => CombatRules.IsInWorldCombat(Zone is WorldZone { Arena: null, Battle: null }, IsInCombat, IsKnockedOut);

    /// <summary>
    /// In a fight, as the client's combat mode means it: in a combat job and in combat (see
    /// <see cref="IsInCombat"/>). Live sent it only while a fight lasted.
    /// </summary>
    public bool IsFighting => CombatRules.IsFighting(IsInCombatJob, IsInCombat);

    // What the client was last told of each, sent one at a time so the last word is the current one.
    private readonly object _combatModeLock = new();
    private bool _isInWorldCombat;
    private bool _isFighting;

    /// <summary>
    /// Sends InWorldCombat on its own, as live did at login (zoning in sends both).
    /// </summary>
    public void SendInWorldCombat()
    {
        lock (_combatModeLock)
            SendInWorldCombat(force: true);
    }

    /// <summary>
    /// Tells the client of a fight that started or ended.
    /// </summary>
    private void UpdateCombatMode()
    {
        lock (_combatModeLock)
        {
            SendInWorldCombat(force: false);
            SendFighting(force: false);
        }
    }

    private void SendInWorldCombat(bool force)
    {
        var isInWorldCombat = IsInWorldCombat;

        if (!force && isInWorldCombat == _isInWorldCombat)
            return;

        _isInWorldCombat = isInWorldCombat;
        SendTunneled(new EncounterOverworldCombatPacket { InWorldCombat = isInWorldCombat });
    }

    private void SendFighting(bool force)
    {
        var isFighting = IsFighting;

        if (!force && isFighting == _isFighting)
            return;

        _isFighting = isFighting;
        SendTunneled(new EncounterPacketIsFighting { IsFighting = isFighting });
    }

    public void OnKilled(Npc mob)
    {
        Achievements.RecordKill(mob);

        Quests.Record(new MobDefeatedEvent(mob));
    }

    /// <summary>
    /// Damage from a mob, after DamageReductionPercent. Returns what was taken. Referees in god mode take none.
    /// </summary>
    public int TakeDamage(int damage, Npc source)
    {
        if (IsDead || IsInGodMode || damage <= 0)
            return 0;

        damage = CombatRules.Mitigate(damage, GetStatValue(CharacterStatId.DamageReductionPercent));

        // A mob's hit comes with its aggro. One without (a script's npc:attack) is a fight for a few seconds.
        if (source.Combat?.HasAggroOn(Guid) != true)
            _lastCombatAt = Environment.TickCount64;

        EnterCombat();

        if (damage == 0)
            return 0;

        damage = Math.Min(damage, _hitpoints);
        Hitpoints = _hitpoints - damage;

        // Own floating number and flinch, and the bar others see over this player.
        SendTunneledToVisible(new PlayerUpdatePacketHitPointModification
        {
            SourceGuid = source.Guid,
            TargetGuid = Guid,
            ShowDelta = true,
            MaxHitpoints = MaxHitpoints,
            CurrentHitpoints = _hitpoints,
            Delta = -damage
        }, true);

        if (IsDead)
            Die(source);

        return damage;
    }

    private void Die(Npc killer)
    {
        // Running heal over time potions end with the fight.
        RemoveEffectGroup("Health");

        _knockOut.KnockOut(Environment.TickCount64);

        // Every mob lets them go (still "in world combat" while down, for the knocked out window's Revive here).
        LeaveCombat();

        SendTunneledToVisible(new PlayerUpdatePacketUpdateCharacterState { Guid = Guid, State = WithModes(KnockedOutCharacterState) }, true);

        var killerName = string.IsNullOrEmpty(killer.Name) ? "an enemy" : killer.Name;

        ChatHelper.SendSystemMessage(this, $"You were defeated by {killerName}! You'll recover at the zone's start in {CombatRules.PlayerRespawnSeconds} seconds.");

        // The knocked out window, and a battle's count of knock outs.
        (Zone as WorldZone)?.OnPlayerKnockedOut(this);

        Zone?.FirePlayerEvent("playerKnockedOut", this);
    }

    private void UpdateRespawn()
    {
        if (!_knockOut.TryRecover(Environment.TickCount64) || !IsDead)
            return;

        Recover();
    }

    /// <summary>
    /// A knocked out player respawns now, rather than when the countdown runs out (the knocked out window's
    /// Respawn). False if they aren't knocked out.
    /// </summary>
    public bool RecoverNow()
    {
        if (!IsDead || !_knockOut.TryRecoverNow())
            return false;

        Recover();

        return true;
    }

    /// <summary>
    /// Back on their feet at the zone's start (a battle's entry), on full health.
    /// </summary>
    private void Recover()
    {
        var battle = (Zone as WorldZone)?.Battle;
        var position = battle?.EntryPosition ?? Zone.SpawnPosition;
        var rotation = battle is not null ? QuaternionExtensions.FromHeading(battle.Definition.EntryHeading) : Zone.SpawnRotation;

        UpdatePosition(position, rotation);
        UpdatePointOfInterest();

        SendTunneled(new ClientUpdatePacketUpdateLocation
        {
            Position = position,
            Rotation = rotation,
            Teleport = true
        });

        Hitpoints = MaxHitpoints;
        Energy = EffectiveMaxEnergy;

        SendTunneledToVisible(new PlayerUpdatePacketUpdateCharacterState { Guid = Guid, State = WithModes(IdleCharacterState) }, true);

        LeaveCombat();
    }

    /// <summary>
    /// Stands a knocked-out player back up where they lie with <paramref name="hitpoints"/> (at least 1), instead of
    /// at the zone's start: everyone around sees them get up, and mobs leave them be for
    /// <see cref="CombatRules.ReviveProtectionSeconds"/>. False if they aren't knocked out (or already got up).
    /// </summary>
    public bool Revive(int hitpoints, ulong reviverGuid = 0)
    {
        if (!IsDead || !_knockOut.TryRevive(Environment.TickCount64))
            return false;

        Hitpoints = Math.Max(1, hitpoints);

        SendTunneledToVisible(new PlayerUpdatePacketUpdateCharacterState { Guid = Guid, State = WithModes(IdleCharacterState) }, true);

        // Own floating number, and the bar others see over this player.
        SendTunneledToVisible(new PlayerUpdatePacketHitPointModification
        {
            SourceGuid = reviverGuid != 0 ? reviverGuid : Guid,
            TargetGuid = Guid,
            ShowDelta = true,
            MaxHitpoints = MaxHitpoints,
            CurrentHitpoints = _hitpoints,
            Delta = _hitpoints
        }, true);

        // Up again, and out of the fight.
        UpdateCombatMode();

        return true;
    }

    /// <summary>
    /// Switches the active job, as the client's job picker does (CommandPacketSetProfile). False if it isn't owned.
    /// </summary>
    public bool ActivateProfile(int profileId)
    {
        var profile = Profiles.FirstOrDefault(x => x.Id == profileId);

        if (profile is null)
            return false;

        // Let each weapon use its own attachment effect during the job transition.
        ClearWeaponFlairOverride();
        ActiveProfileId = profileId;

        if (IsInCombatJob)
            LastCombatProfileId = profileId;

        using var packetWriter = new PacketWriter();

        profile.Serialize(packetWriter);

        var clientUpdatePacketActivateProfile = new ClientUpdatePacketActivateProfile
        {
            Payload = packetWriter.Buffer,
            Attachments = GetAttachments(),
            Animation = 3001, // emo_outfit_all
            CompositeEffect = 4005 // PFX_Job_Swirl
        };

        SendTunneled(clientUpdatePacketActivateProfile);

        SendTunneledToVisible(new PlayerUpdatePacketEquippedItemsChange
        {
            Guid = Guid,
            Attachments = clientUpdatePacketActivateProfile.Attachments
        });

        SendToolbar();
        SendCombatMode();
        SendFriendStatus();
        ApplyRankHealth();

        // A pond starts or stops fishing as the Fisherman job comes and goes.
        (Zone as WorldZone)?.Fishing?.OnProfileChanged(this);

        return true;
    }

    /// <summary>
    /// The client is fishing (see <see cref="FishingRules.FishingCharacterState"/>): set by a fishing pond while a
    /// Fisherman is in it.
    /// </summary>
    public bool IsFishing { get; private set; }

    /// <summary>
    /// Turns the client's fishing on or off. Without <paramref name="notify"/> only the server forgets it, for a
    /// player leaving the zone, whose client stops fishing as the zone unloads.
    /// </summary>
    public void SetFishing(bool fishing, bool notify = true)
    {
        if (IsFishing == fishing)
            return;

        IsFishing = fishing;

        if (notify)
            SendTunneled(new PlayerUpdatePacketUpdateCharacterState { Guid = Guid, State = WithModes(IsDead ? KnockedOutCharacterState : IdleCharacterState) });
    }

    /// <summary>
    /// A 3D game's bit (0x400 kart racing, 0x800 demolition derby, 0x1000 soccer) while the player is in its zone:
    /// set on the player's own client it starts the game there. 0 elsewhere.
    /// </summary>
    public int GameCharacterState { get; private set; }

    /// <summary>
    /// Sets (or with 0 clears) <see cref="GameCharacterState"/>, for the player and everyone around. Without
    /// <paramref name="notify"/> only the server forgets it, for a player leaving the game's zone, whose client
    /// stops the game as the zone unloads.
    /// </summary>
    public void SetGameCharacterState(int state, bool notify = true)
    {
        if (GameCharacterState == state)
            return;

        GameCharacterState = state;

        if (notify)
            SendTunneledToVisible(new PlayerUpdatePacketUpdateCharacterState { Guid = Guid, State = WithModes(IsDead ? KnockedOutCharacterState : IdleCharacterState) }, true);
    }

    /// <summary>
    /// Tells the player's own client their character state again (a game's zone left behind sends 1, as live).
    /// </summary>
    public void SendCharacterState() =>
        SendTunneled(new PlayerUpdatePacketUpdateCharacterState { Guid = Guid, State = WithModes(IsDead ? KnockedOutCharacterState : IdleCharacterState) });

    /// <summary>
    /// The character state sends the client's flags whole, so each one keeps the fishing and game bits while
    /// they're set.
    /// </summary>
    private int WithModes(int state) => (IsFishing ? state | FishingRules.FishingCharacterState : state) | GameCharacterState;

    /// <summary>
    /// Shows friends and the group the active job and its rank.
    /// </summary>
    private void SendFriendStatus()
    {
        Group?.NotifyMemberChanged();

        var friendStatusPacket = new FriendStatusPacket
        {
            Guid = Guid,
            Status =
            {
                ProfileId = ActiveProfile.Id,
                ProfileRank = ActiveProfile.Rank,
                ProfileIconId = ActiveProfile.Icon,
                ProfileNameId = ActiveProfile.NameId,
                ProfileBackgroundImageId = ActiveProfile.BadgeImageSet
            }
        };

        foreach (var friend in Friends)
        {
            if (_zoneManager.TryGetPlayer(friend.Guid, out var friendPlayer))
                friendPlayer.SendTunneled(friendStatusPacket);
        }
    }

    /// <summary>
    /// Moves a job's bar to <paramref name="progress"/> (already saved). A higher rank gets the client's
    /// level-up notice, the level-up sparkle for everyone around, more health and a full refill.
    /// </summary>
    public void UpdateProfileProgress(ClientPcProfile profile, ProfileProgress progress)
    {
        var rankedUp = progress.Rank > profile.Rank;

        profile.Rank = progress.Rank;
        profile.RankPercent = ProfileExperience.RankPercent(progress);

        // The rank packet goes first: it only shows the level-up notice while the client still has the old rank.
        SendTunneled(new ClientUpdatePacketUpdateProfileRank
        {
            ProfileId = profile.Id,
            Rank = profile.Rank
        });

        SendTunneled(new ClientUpdatePacketUpdateProfileExperience
        {
            ProfileId = profile.Id,
            Rank = profile.Rank,
            RankPercent = profile.RankPercent,
            StarsAvailable = profile.StarsAvailable,
            StarsEarned = profile.StarsEarned,
            Unknown20 = profile.Unknown20
        });

        if (profile.Id == ActiveProfile.Id)
            ApplyRankHealth();

        if (!rankedUp)
            return;

        Achievements.Record(AchievementEvent.JobRank, profile.Id, profile.Rank);
        Zone?.FirePlayerEvent("jobRankUp", this, profile.Id, profile.Rank);

        SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = Guid,
            CompositeEffectId = ProfileExperience.LevelUpEffectId,
            Position = Position
        }, true);

        if (!IsDead)
        {
            Hitpoints = MaxHitpoints;
            Energy = EffectiveMaxEnergy;
        }

        if (profile.Id == ActiveProfile.Id)
            SendFriendStatus();
    }

    /// <summary>
    /// Resends hitpoints, energy and any buffed stats, e.g. after a zone load overwrote them with defaults.
    /// </summary>
    public void SendVitals()
    {
        ApplyRankHealth();
        RefreshStatBonuses(_statBases.Keys.ToList());

        Hitpoints = _hitpoints;
        Energy = _energy;
    }

    /// <summary>
    /// Sets base MaxHealth from the active job's rank (CombatRules.MaxHitpointsAtRank), under any health buff.
    /// </summary>
    private void ApplyRankHealth()
    {
        var baseHealth = CombatRules.MaxHitpointsAtRank(ActiveProfile.Rank);
        var wasFull = !IsDead && _hitpoints >= MaxHitpoints;

        // A health buff holds the base aside and adds itself on top.
        if (_statBases.ContainsKey(CharacterStatId.MaxHealth))
        {
            _statBases[CharacterStatId.MaxHealth] = baseHealth;
            RefreshStatBonuses([CharacterStatId.MaxHealth]);
        }
        else if (GetStatValue(CharacterStatId.MaxHealth) != baseHealth)
            UpdateCharacterStats(new CharacterStat(CharacterStatId.MaxHealth, baseHealth));
        else
            return;

        // A full bar stays full when the cap goes up; anything else is clamped to the new cap.
        Hitpoints = wasFull ? MaxHitpoints : _hitpoints;
    }

    // Regen at the HitPointRegen/ManaRegen stat rates. A downed player (0 hp) stays down.
    private void RegenerateVitals()
    {
        // Energy keeps coming mid-fight for specials; health waits until it's over.
        if (_hitpoints > 0 && _hitpoints < MaxHitpoints && !IsInCombat)
            Heal((int)GetStatValue(CharacterStatId.HitPointRegen));

        if (_energy < EffectiveMaxEnergy)
            RestoreEnergy((int)GetStatValue(CharacterStatId.ManaRegen));
    }

    /// <summary>
    /// Coins earned from rewards, with coin scrolls (GoldBoostPercent) applied.
    /// </summary>
    public int ApplyCoinBoost(int coins) => ApplyPercentBoost(coins, GetStatValue(CharacterStatId.GoldBoostPercent));

    /// <summary>
    /// Stars (experience) earned from rewards, with star scrolls (ExperienceBoostPercent) applied.
    /// </summary>
    public int ApplyExperienceBoost(int experience) => ApplyPercentBoost(experience, GetStatValue(CharacterStatId.ExperienceBoostPercent));

    public static int ApplyPercentBoost(int amount, float percent) =>
        amount <= 0 || percent <= 0 ? amount : (int)Math.Min(int.MaxValue, Math.Round(amount * (1 + percent / 100d), MidpointRounding.AwayFromZero));

    /// <summary>
    /// Combat ability ids that replace the job kit's basic/special abilities on the toolbar (see !ability).
    /// </summary>
    public int? BasicAbilityOverride { get; set; }
    public int? SpecialAbilityOverride { get; set; }

    public int GetEquippedWeaponDefinitionId() => GetEquippedItem(WeaponSlot)?.Definition ?? 0;

    public bool SendToolbar()
    {
        if (!_resourceManager.CombatJobs.TryGetValue(ActiveProfileId, out var kit))
        {
            SendTunneled(new AbilityPacketSetDefinition { ProfileId = ActiveProfileId });
            return false;
        }

        var abilityPacketSetDefinition = new AbilityPacketSetDefinition { ProfileId = kit.ProfileId };

        var weaponDefinitionId = GetEquippedWeaponDefinitionId();

        if (_resourceManager.ClientItemDefinitions.TryGetValue(weaponDefinitionId, out var weaponDefinition))
        {
            var (basic, special) = ResolveWeaponAbilities(kit, weaponDefinitionId);

            if (basic is not null)
            {
                abilityPacketSetDefinition.AbilitySet.Abilities[0] = CreateToolbarSlot(kit.BasicSlotDefId, basic.IconId, weaponDefinition.NameId, manaCost: 0);
                SendAbilityDefinition(kit.BasicSlotDefId, basic);
            }

            if (special is not null)
            {
                abilityPacketSetDefinition.AbilitySet.Abilities[1] = CreateToolbarSlot(kit.SpecialSlotDefId, special.IconId, weaponDefinition.NameId, special.EnergyCost);
                SendAbilityDefinition(kit.SpecialSlotDefId, special);
            }
        }

        SendTunneled(abilityPacketSetDefinition);

        MaxEnergy = kit.Energy.Max;
        // Resync energy against the new max.
        Energy = _energy;

        return true;
    }

    private void SendAbilityDefinition(int abilityDefinitionId, AbilityDefinition ability)
    {
        SendTunneled(new AbilityPacketAbilityDefinition
        {
            AbilityId = abilityDefinitionId,
            NameId = ability.NameId,
            DescriptionId = ability.DescriptionId,
            IconId = ability.IconId,
            ManaCost = ability.EnergyCost
        });
    }

    private static Ability CreateToolbarSlot(int abilityDefinitionId, int iconId, int nameId, int manaCost) => new()
    {
        Type = 3,
        InstanceId = abilityDefinitionId,
        ManaCost = manaCost,
        IconId = iconId,
        NameId = nameId,
        Unknown7 = 4,
        Unknown9 = 1,
        AbilityDefinitionId = abilityDefinitionId,
        ForceDismount = true
    };

    public const int BasicToolbarSlot = 0;
    public const int SpecialToolbarSlot = 1;

    /// <summary>
    /// When the basic attack is next allowed (the job kit's BasicRecastMs after the last one).
    /// </summary>
    public long NextBasicAttackAt { get; set; }

    /// <summary>
    /// The job kit and the ability on a toolbar slot (<see cref="BasicToolbarSlot"/> or <see cref="SpecialToolbarSlot"/>),
    /// as <see cref="SendToolbar"/> put them there.
    /// </summary>
    public bool TryGetToolbarAbility(int slot, [MaybeNullWhen(false)] out JobKitDefinition kit, [MaybeNullWhen(false)] out AbilityDefinition ability)
    {
        ability = null;

        if (!_resourceManager.CombatJobs.TryGetValue(ActiveProfileId, out kit))
            return false;

        var (basic, special) = ResolveWeaponAbilities(kit, GetEquippedWeaponDefinitionId());

        ability = slot switch
        {
            BasicToolbarSlot => basic,
            SpecialToolbarSlot => special,
            _ => null
        };

        return ability is not null;
    }

    private (AbilityDefinition? Basic, AbilityDefinition? Special) ResolveWeaponAbilities(JobKitDefinition kit, int weaponDefinitionId)
    {
        var mapping = kit.Weapons.FirstOrDefault(w => w.WeaponDefIds.Contains(weaponDefinitionId));

        var basicId = BasicAbilityOverride ?? mapping?.BasicAbilityId ?? kit.FallbackBasicAbilityId;
        var specialId = SpecialAbilityOverride ?? mapping?.SpecialAbilityId ?? 0;

        return (
            _resourceManager.CombatAbilities.TryGetValue(basicId, out var basic) ? basic : null,
            _resourceManager.CombatAbilities.TryGetValue(specialId, out var special) ? special : null);
    }

    #endregion

    #region Equatable

    public bool Equals(IEntity? other)
    {
        return Guid == other?.Guid;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is Player other)
            return Equals(other);

        return false;
    }

    public override int GetHashCode()
    {
        return Guid.GetHashCode();
    }

    public static bool operator ==(Player left, Player right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Player left, Player right)
    {
        return !(left == right);
    }

    #endregion

    private void RemoveFromVisibleEntities(bool notifySelf)
    {
        foreach (var visibleNpc in VisibleNpcs)
            visibleNpc.Value.OnRemoveVisiblePlayers([this]);

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.OnRemoveVisiblePlayers([this]);

        if (notifySelf)
        {
            OnRemoveVisibleNpcs(VisibleNpcs.Values);
            OnRemoveVisiblePlayers(VisiblePlayers.Values);
        }
    }

    public void ClearObjectives()
    {
        SendTunneled(new ObjectiveTargetUpdatePacket { Active = false });
        SendTunneled(new ObjectiveClientClearPacket { Unknown = Random.Shared.Next() });
    }

    public void OnZoneChanged(IZone zone, Vector4 position, Quaternion rotation)
    {
        // Leaving a zone (into a battle too) leaves its mobs' fights; zoning in tells the client.
        DropAllAggro();

        PlayerPet.RecallPet(remember: zone is not CombatZone);

        RemoveFromVisibleEntities(true);

        ZoneTile.Entities.Remove(Guid, out _);

        Zone = zone;

        ZoneTile = ZoneTile.Empty;

        Visible = false;

        UpdatePosition(position, rotation);
    }

    public void Dispose()
    {
        DropAllAggro();

        PlayerPet.Dispose();

        RemoveFromVisibleEntities(false); // no need to notify self since we're DCing

        Mount?.Dispose();
        Mount = null;

        ZoneTile.Entities.Remove(Guid, out _);
        Zone.TryRemovePlayer(Guid);
        _zoneManager.EvictIfEmpty(Zone);
    }

    public sealed record InteractionMenu(ulong Guid, IReadOnlyDictionary<int, Action<Player>> Options);

    public InteractionMenu? OpenInteractionMenu { get; set; }

    private const int NpcInteractionIdBase = 1_000_000;

    public void SendInteractionMenu(Npc npc, IReadOnlyList<NpcInteractionOption> options, IEnumerable<InteractionData>? npcInteractions = null)
    {
        var npcInteractionList = npcInteractions?.ToList() ?? [];
        var usedIds = npcInteractionList.Select(interaction => interaction.Id).ToHashSet();

        var interactions = new List<InteractionData>(options.Count);
        var actions = new Dictionary<int, Action<Player>>(options.Count);
        var nextId = NpcInteractionIdBase;

        foreach (var option in options)
        {
            while (!usedIds.Add(nextId))
                nextId++;

            var id = nextId++;

            interactions.Add(new InteractionData
            {
                Id = id,
                IconId = option.IconId,
                ButtonText = option.ButtonTextId,
                TooltipId = option.TooltipId
            });

            actions[id] = option.Invoke;
        }

        interactions.AddRange(npcInteractionList);

        OpenInteractionMenu = new InteractionMenu(npc.Guid, actions);

        SendTunneled(InteractionMenuHelper.GetInteractionListPacket(npc.Guid, npc.Name, interactions));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Housing;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;

namespace Sanctuary.Game.Zones;

/// <summary>
/// Furniture that does something (HousingFixtures.json): trampolines, beds and music blocks carry a client
/// area that acts on whoever stands on them, elevators and moving platforms ride their routes, and
/// teleporters beam players to the pads they're linked to.
/// </summary>
public sealed partial class HousingZone
{
    /// <summary>
    /// env_loop_01: the moving block's and teleporter's plain loop, without the route dots.
    /// </summary>
    private const int FixtureRunAnimationId = 2901;

    /// <summary>
    /// env_oneshot_01: the teleporter's crystal flare.
    /// </summary>
    private const int TeleporterFlareAnimationId = 2801;

    /// <summary>
    /// How far from a teleporter's middle (its pad is about 1.5 m across each way) and how high above it a
    /// player counts as standing on it.
    /// </summary>
    private const float TeleporterPadRadius = 1.2f;
    private const float TeleporterPadMinHeight = -0.5f;
    private const float TeleporterPadMaxHeight = 2f;

    /// <summary>
    /// Arrivals are dropped just above the pad and land on it.
    /// </summary>
    private const float TeleporterArrivalHeight = 0.5f;

    private readonly Dictionary<ulong, HousePlatform> _platforms = [];
    private readonly Dictionary<ulong, HouseTeleporter> _teleporters = [];
    private readonly Random _teleporterRandom = new();

    /// <summary>
    /// Whether fixtures are showing the decorating view: platforms parked where they were placed with their
    /// routes drawn, and teleporter links beamed.
    /// </summary>
    private bool _showingDecorating;

    private sealed class HousePlatform(Npc npc, HousingPlatformDefinition definition)
    {
        public Npc Npc { get; } = npc;
        public HousingPlatformDefinition Definition { get; } = definition;
        public PlatformRoute Route { get; set; } = null!;
        public Quaternion Rotation { get; set; }
        public long StartedAt { get; set; }
        public bool WasMoving { get; set; }
    }

    private sealed class HouseTeleporter(Npc npc, HousingTeleporterDefinition definition)
    {
        public Npc Npc { get; } = npc;
        public HousingTeleporterDefinition Definition { get; } = definition;
        public TeleporterPad Pad { get; } = new();
        public List<int> LinkTags { get; } = [];
    }

    /// <summary>
    /// Someone in the house is decorating. Editors who left without turning edit mode off don't count.
    /// </summary>
    private bool IsDecorating => _editors.Keys.Any(guid => TryGetPlayer(guid, out _));

    /// <summary>
    /// Gives a fixture actor its behaviour, before it's shown to anyone.
    /// </summary>
    private void AttachFixtureBehavior(ulong fixtureGuid, DbHouseFixture fixture, Npc npc)
    {
        if (!_resourceManager.HousingFixtures.TryGetValue(fixture.Definition, out var behavior))
            return;

        npc.AreaDefinitionId = behavior.AreaDefinitionId;

        if (behavior.Platform is { } platformDefinition)
        {
            var platform = new HousePlatform(npc, platformDefinition);

            npc.IsPlatform = true;
            npc.Speed = platformDefinition.Speed;
            npc.Animation = _showingDecorating ? platformDefinition.PathAnimationId : FixtureRunAnimationId;
            npc.UpdateEveryTickAction = () => UpdatePlatform(fixtureGuid);

            ResetPlatformRoute(platform, fixture);

            _platforms[fixtureGuid] = platform;
        }
        else if (behavior.Teleporter is { } teleporterDefinition)
        {
            npc.UpdateEveryTickAction = () => UpdateTeleporter(fixtureGuid);

            _teleporters[fixtureGuid] = new HouseTeleporter(npc, teleporterDefinition);
        }
    }

    /// <summary>
    /// A fixture actor is now in view: a new teleporter joins up with its partner.
    /// </summary>
    private void OnFixtureShown(ulong fixtureGuid)
    {
        if (_teleporters.ContainsKey(fixtureGuid))
            RefreshTeleporterLinks();
    }

    private void DetachFixtureBehavior(ulong fixtureGuid)
    {
        _platforms.Remove(fixtureGuid);

        if (_teleporters.Remove(fixtureGuid))
            RefreshTeleporterLinks();
    }

    /// <summary>
    /// A fixture was moved or turned: platforms set off from their new spot.
    /// </summary>
    private void OnFixtureMoved(ulong fixtureGuid, DbHouseFixture fixture)
    {
        if (_platforms.TryGetValue(fixtureGuid, out var platform))
            ResetPlatformRoute(platform, fixture);
    }

    private void ResetPlatformRoute(HousePlatform platform, DbHouseFixture fixture)
    {
        var (yaw, _, _) = GetFixtureAngles(fixture);
        var origin = new Vector3(fixture.PositionX, fixture.PositionY, fixture.PositionZ);

        platform.Route = PlatformRoute.FromLocal(platform.Definition.Path, origin, yaw,
            platform.Definition.Speed, platform.Definition.PauseSeconds);
        platform.Rotation = QuaternionExtensions.FromHeading(yaw);
        platform.StartedAt = Environment.TickCount64;
        platform.WasMoving = false;
    }

    /// <summary>
    /// Starts or ends the decorating view when someone starts or stops decorating (or leaves mid-way):
    /// platforms go back to where they were placed and stop there while it's on.
    /// </summary>
    private void SyncDecoratingView()
    {
        var decorating = IsDecorating;

        if (decorating == _showingDecorating)
            return;

        _showingDecorating = decorating;

        foreach (var (fixtureGuid, platform) in _platforms)
        {
            if (!_fixtures.TryGetValue(fixtureGuid, out var fixture))
                continue;

            ResetPlatformRoute(platform, fixture);

            platform.Npc.UpdatePosition(new Vector4(platform.Route.Start, 1f), platform.Rotation, false);
            platform.Npc.PlayAnimation(decorating ? platform.Definition.PathAnimationId : FixtureRunAnimationId, true);
        }

        RefreshTeleporterLinks();
    }

    private void UpdatePlatform(ulong fixtureGuid)
    {
        lock (_houseLock)
        {
            SyncDecoratingView();

            if (_showingDecorating || !_platforms.TryGetValue(fixtureGuid, out var platform))
                return;

            var (position, moving) = platform.Route.Sample((Environment.TickCount64 - platform.StartedAt) / 1000f);

            // Corners only need the one update that stops it there.
            if (!moving && !platform.WasMoving)
                return;

            platform.WasMoving = moving;
            platform.Npc.UpdatePosition(new Vector4(position, 1f), platform.Rotation, moving);
        }
    }

    #region Teleporters

    private List<TeleporterLinks.Pad> GetTeleporterPads() => _teleporters
        .OrderBy(x => x.Key)
        .Select(x => new TeleporterLinks.Pad(x.Key, x.Value.Definition.Mode))
        .ToList();

    /// <summary>
    /// Linked pads swirl. While decorating, a beam joins each pair (and runs round the multi-way pads), so
    /// it's clear which pad goes where.
    /// </summary>
    private void RefreshTeleporterLinks()
    {
        var pads = GetTeleporterPads();

        foreach (var (fixtureGuid, teleporter) in _teleporters)
        {
            foreach (var tagId in teleporter.LinkTags)
                teleporter.Npc.RemoveEffectTag(tagId);

            teleporter.LinkTags.Clear();

            var animationId = TeleporterLinks.IsLinked(pads, fixtureGuid) ? FixtureRunAnimationId : 0;

            if (teleporter.Npc.Animation != animationId)
                teleporter.Npc.PlayAnimation(animationId, true);
        }

        if (!_showingDecorating)
            return;

        foreach (var (from, to) in TeleporterLinks.GetLinks(pads))
        {
            var source = _teleporters[from];
            var target = _teleporters[to];

            if (source.Definition.LinkEffectId != 0)
                source.LinkTags.Add(source.Npc.AddEffectTag(source.Definition.LinkEffectId, target.Npc.Guid));
        }
    }

    private void UpdateTeleporter(ulong fixtureGuid)
    {
        lock (_houseLock)
        {
            SyncDecoratingView();

            if (!_teleporters.TryGetValue(fixtureGuid, out var teleporter))
                return;

            var scale = teleporter.Npc.Scale > 0f ? teleporter.Npc.Scale : 1f;

            // Decorators can walk over pads without being beamed about.
            var onPad = teleporter.Npc
                .GetPlayerGuidsOnTop(TeleporterPadRadius * scale, TeleporterPadMinHeight, TeleporterPadMaxHeight * scale)
                .Where(guid => !_editors.ContainsKey(guid))
                .ToList();

            var (started, ready) = teleporter.Pad.Update(onPad, Environment.TickCount64);

            if (started.Count == 0 && ready.Count == 0)
                return;

            var pads = GetTeleporterPads();

            foreach (var guid in started)
            {
                if (!TryGetPlayer(guid, out var player))
                    continue;

                // A pad that sends nowhere (unpaired, or the receiving end of a one-way pair) just holds them.
                if (TeleporterLinks.GetDestination(pads, fixtureGuid, _teleporterRandom) is null)
                {
                    teleporter.Pad.MarkArrived(guid);
                    continue;
                }

                if (teleporter.Definition.TeleportEffectId != 0)
                {
                    player.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
                    {
                        Guid = player.Guid,
                        CompositeEffectId = teleporter.Definition.TeleportEffectId,
                        Position = player.Position
                    }, true);
                }
            }

            foreach (var guid in ready)
            {
                if (!TryGetPlayer(guid, out var player))
                    continue;

                if (TeleporterLinks.GetDestination(pads, fixtureGuid, _teleporterRandom) is not { } destinationGuid
                    || !_teleporters.TryGetValue(destinationGuid, out var destination))
                {
                    teleporter.Pad.MarkArrived(guid);
                    continue;
                }

                var position = destination.Npc.Position with { W = player.Position.W };
                position.Y += TeleporterArrivalHeight;

                destination.Pad.MarkArrived(guid);

                player.UpdatePosition(position, destination.Npc.Rotation);

                player.SendTunneled(new ClientUpdatePacketUpdateLocation
                {
                    Position = position,
                    Rotation = destination.Npc.Rotation,
                    Teleport = true
                });

                teleporter.Npc.PlayAnimation(TeleporterFlareAnimationId, false);
                destination.Npc.PlayAnimation(TeleporterFlareAnimationId, false);
            }
        }
    }

    #endregion
}

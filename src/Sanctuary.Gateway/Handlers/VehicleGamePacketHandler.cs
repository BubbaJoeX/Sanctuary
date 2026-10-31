using System;

using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Game.VehicleGames;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// The 3D games' packets (33 kart racing, 34 demolition derby, 76 soccer, 139 vehicle parts), whole, for
/// <see cref="IVehicleGameManager"/>. The client sends each car's state many times a second unreliably; those
/// come through here the same.
/// </summary>
[PacketHandler]
public static class VehicleGamePacketHandler
{
    private static IVehicleGameManager _vehicleGameManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        _vehicleGameManager = serviceProvider.GetRequiredService<IVehicleGameManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, short opCode, ReadOnlySpan<byte> data)
    {
        _vehicleGameManager.HandlePacket(connection.Player, opCode, data);

        return true;
    }
}

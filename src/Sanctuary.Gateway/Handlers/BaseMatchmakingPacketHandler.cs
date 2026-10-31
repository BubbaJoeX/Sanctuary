using System;

using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Game.VehicleGames;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// The lobby's matchmaking packets (141), whole, for <see cref="IVehicleGameManager"/>, whose games are the queues.
/// The client asks for the queue list every time its HUD comes back.
/// </summary>
[PacketHandler]
public static class BaseMatchmakingPacketHandler
{
    private static IVehicleGameManager _vehicleGameManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        _vehicleGameManager = serviceProvider.GetRequiredService<IVehicleGameManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        _vehicleGameManager.HandleMatchmakingPacket(connection.Player, data);

        return true;
    }
}

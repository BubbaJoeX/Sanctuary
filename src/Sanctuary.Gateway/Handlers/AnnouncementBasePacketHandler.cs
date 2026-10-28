using System;

using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Core.IO;
using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class AnnouncementBasePacketHandler
{
    private static IRefereePickManager _refereePickManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        _refereePickManager = serviceProvider.GetRequiredService<IRefereePickManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out byte subOpCode) || subOpCode != 1)
            return false;

        connection.SendTunneled(WelcomeScreenHelper.GetAnnouncementsPacket(_refereePickManager.Current));
        return true;
    }
}

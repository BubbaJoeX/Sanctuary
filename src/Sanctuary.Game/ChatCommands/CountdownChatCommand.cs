using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Event start countdown, shown as on-screen zone broadcasts (the banner the client draws for BroadcastPacketZone).
/// </summary>
public class CountdownChatCommand : IChatCommand
{
    private const int MaxSeconds = 600;

    private static readonly int[] AnnouncedSeconds = [600, 300, 120, 60, 30, 20, 10, 5, 4, 3, 2, 1];

    private readonly IChatCommandManager _chatCommandManager;

    // One countdown per zone instance; starting another replaces it.
    private readonly ConcurrentDictionary<IZone, CancellationTokenSource> _countdowns = new();

    public string KeyWord => "countdown";
    public string Usage => "<seconds> [message shown at zero] | cancel";
    public string Description => "Counts down on everyone's screen in your zone, e.g. to start a race. The message (default \"Go!\") shows at zero.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public CountdownChatCommand(IChatCommandManager chatCommandManager)
    {
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        var zone = invoker.Zone;

        if (args[0].Equals("cancel", StringComparison.OrdinalIgnoreCase) && args.Length == 1)
        {
            if (!_countdowns.TryRemove(zone, out var running))
            {
                ChatHelper.SendSystemMessage(invoker, "No countdown is running in this zone.");
                return true;
            }

            running.Cancel();
            Broadcast(zone, "Countdown cancelled.");

            _chatCommandManager.LogAction(this, invoker, "Cancel", null, null);
            return true;
        }

        if (!int.TryParse(args[0], out var seconds) || seconds is < 1 or > MaxSeconds)
        {
            ChatHelper.SendSystemMessage(invoker, $"Count from 1 to {MaxSeconds} seconds.");
            return true;
        }

        var message = args.Length > 1 ? string.Join(' ', args[1..]) : "Go!";

        var cancellation = new CancellationTokenSource();

        if (_countdowns.TryGetValue(zone, out var previous))
            previous.Cancel();

        _countdowns[zone] = cancellation;

        _ = RunAsync(zone, seconds, message, cancellation);

        _chatCommandManager.LogAction(this, invoker, "Start", null, $"Seconds: {seconds}, Message: \"{message}\"");
        return true;
    }

    private async Task RunAsync(IZone zone, int seconds, string message, CancellationTokenSource cancellation)
    {
        try
        {
            for (var remaining = seconds; remaining > 0; remaining--)
            {
                if (remaining == seconds || AnnouncedSeconds.Contains(remaining))
                    Broadcast(zone, remaining >= 60 && remaining % 60 == 0 ? $"{remaining / 60} minute(s)..." : $"{remaining}...");

                await Task.Delay(TimeSpan.FromSeconds(1), cancellation.Token);
            }

            Broadcast(zone, message);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _countdowns.TryRemove(new(zone, cancellation));
        }
    }

    private static void Broadcast(IZone zone, string message)
    {
        var packet = new BroadcastPacketZone { Message = message };

        foreach (var player in zone.Players)
            player.SendTunneled(packet);
    }
}

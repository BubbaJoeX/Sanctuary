using System;
using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

public class EmoteChatCommand : IChatCommand
{
    public string KeyWord => "emote";

    public string Usage => "<name> | stop";

    public string Description => "Plays an emote everyone near you sees (twerk, griddy, dab, floss, robot, moonwalk, ...). " +
                                 "Loops end when you walk away or type !emote stop.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Player;

    /// <summary>
    /// Animation ids are AnimationTypes.xml slots. 3590 and up are the FR Maya MCP dances, which the client gets from
    /// the AnimationTypes.xml client patch and the human and pixie actors in ClientMods. A looping emote becomes the
    /// player's base animation until it ends.
    /// </summary>
    private static readonly Dictionary<string, (int AnimationId, bool Loop)> Emotes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["twerk"] = (3590, false),      // emo_frmcp_twerk
        ["twerkloop"] = (3591, true),   // emo_frmcp_twerk_loop
        ["griddy"] = (3592, true),      // emo_frmcp_griddy
        ["dab"] = (3593, false),        // emo_frmcp_dab
        ["floss"] = (3594, true),       // emo_frmcp_floss
        ["robot"] = (3595, true),       // emo_frmcp_robot
        ["runningman"] = (3596, true),  // emo_frmcp_running_man
        ["moonwalk"] = (3597, true),    // emo_frmcp_moonwalk
        ["disco"] = (3598, false),      // emo_frmcp_disco_point
        ["sprinkler"] = (3599, true),   // emo_frmcp_sprinkler
        ["carlton"] = (3600, true),     // emo_frmcp_carlton
        ["macarena"] = (3601, true),    // emo_frmcp_macarena
        ["shuffle"] = (3602, true),     // emo_frmcp_shuffle
        ["headbang"] = (3603, true),    // emo_frmcp_headbang
        ["dance"] = (3501, true),       // emo_dance_freestyle_01
        ["wave"] = (3333, false),       // emo_wave
        ["cheer"] = (3306, false),      // emo_cheer
        ["laugh"] = (3316, false),      // emo_laugh
        ["bow"] = (3342, false),        // emo_bow
        ["point"] = (3319, false)       // emo_point
    };

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length != 1)
        {
            ChatHelper.SendSystemMessage(invoker, $"Emotes: {string.Join(", ", Emotes.Keys)}. !emote stop ends a loop.");
            return true;
        }

        if (args[0].Equals("stop", StringComparison.OrdinalIgnoreCase))
        {
            invoker.StopLoopingEmote();
            return true;
        }

        if (!Emotes.TryGetValue(args[0], out var emote))
        {
            ChatHelper.SendSystemMessage(invoker, $"No emote '{args[0]}'. Emotes: {string.Join(", ", Emotes.Keys)}.");
            return true;
        }

        if (invoker.Mount is not null)
        {
            ChatHelper.SendSystemMessage(invoker, "Dismount to emote.");
            return true;
        }

        invoker.StopLoopingEmote();

        if (emote.Loop)
        {
            invoker.LoopingEmoteAnimation = emote.AnimationId;
            invoker.LoopingEmoteFrom = invoker.Position;
        }

        invoker.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
        {
            Guid = invoker.Guid,
            AnimationId = emote.AnimationId,
            Flags = (byte)(emote.Loop ? 1 : 0)
        }, true);

        return true;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

public class EmoteChatCommand : IChatCommand
{
    public string KeyWord => "emote";

    public string Usage => "<name> | stop";

    public string Description => "Plays an emote everyone near you sees: dances (twerk, griddy, floss, waltz, zombie, chicken, ...), " +
                                 "poses and the client's own emotes (shrug, flex, salute, backflip, ...). Loops end when you walk away or type !emote stop.";

    private enum EmoteKind
    {
        Dance,
        Pose,
        Social
    }

    public ChatCommandRole RequiredRole => ChatCommandRole.Player;

    /// <summary>
    /// Animation ids are AnimationTypes.xml slots. 3590 and up are the FR Maya MCP dances, which the client gets from
    /// the AnimationTypes.xml client patch and the human and pixie actors in ClientMods. A looping emote becomes the
    /// player's base animation until it ends.
    /// </summary>
    private static readonly Dictionary<string, (int AnimationId, bool Loop, EmoteKind Kind)> Emotes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["twerk"] = (3590, false, EmoteKind.Dance),                   // emo_frmcp_twerk
        ["twerkloop"] = (3591, true, EmoteKind.Dance),                // emo_frmcp_twerk_loop
        ["griddy"] = (3592, true, EmoteKind.Dance),                   // emo_frmcp_griddy
        ["dab"] = (3593, false, EmoteKind.Dance),                     // emo_frmcp_dab
        ["floss"] = (3594, true, EmoteKind.Dance),                    // emo_frmcp_floss
        ["robot"] = (3595, true, EmoteKind.Dance),                    // emo_frmcp_robot
        ["runningman"] = (3596, true, EmoteKind.Dance),               // emo_frmcp_running_man
        ["moonwalk"] = (3597, true, EmoteKind.Dance),                 // emo_frmcp_moonwalk
        ["disco"] = (3598, false, EmoteKind.Dance),                   // emo_frmcp_disco_point
        ["sprinkler"] = (3599, true, EmoteKind.Dance),                // emo_frmcp_sprinkler
        ["carlton"] = (3600, true, EmoteKind.Dance),                  // emo_frmcp_carlton
        ["macarena"] = (3601, true, EmoteKind.Dance),                 // emo_frmcp_macarena
        ["shuffle"] = (3602, true, EmoteKind.Dance),                  // emo_frmcp_shuffle
        ["headbang"] = (3603, true, EmoteKind.Dance),                 // emo_frmcp_headbang
        ["dance"] = (3501, true, EmoteKind.Dance),                    // emo_dance_freestyle_01
        ["wave"] = (3333, false, EmoteKind.Social),                   // emo_wave
        ["cheer"] = (3306, false, EmoteKind.Social),                  // emo_cheer
        ["laugh"] = (3316, false, EmoteKind.Social),                  // emo_laugh
        ["bow"] = (3342, false, EmoteKind.Social),                    // emo_bow
        ["point"] = (3319, false, EmoteKind.Social),                  // emo_point

        // The client's own emotes (AnimationTypes.xml's emo_ slots): every dance family with its variants, the poses and
        // the social ones. Its outfit, talk and pet-cajole slots are the UI's and NPCs', not emotes.
        ["basicdance"] = (3201, true, EmoteKind.Dance),               // emo_dance_basic
        ["dance2"] = (3211, true, EmoteKind.Dance),                   // emo_dance_01
        ["dance3"] = (3212, true, EmoteKind.Dance),                   // emo_dance_02
        ["dance4"] = (3213, true, EmoteKind.Dance),                   // emo_dance_03
        ["dance5"] = (3214, true, EmoteKind.Dance),                   // emo_dance_04
        ["freestyle"] = (3501, true, EmoteKind.Dance),                // emo_dance_freestyle_01
        ["freestyle2"] = (3502, true, EmoteKind.Dance),               // emo_dance_freestyle_02
        ["freestyle3"] = (3503, true, EmoteKind.Dance),               // emo_dance_freestyle_03
        ["freestyle4"] = (3504, true, EmoteKind.Dance),               // emo_dance_freestyle_04
        ["freestyle5"] = (3505, true, EmoteKind.Dance),               // emo_dance_freestyle_05
        ["zombie"] = (3511, true, EmoteKind.Dance),                   // emo_dance_zombie_01
        ["zombie2"] = (3512, true, EmoteKind.Dance),                  // emo_dance_zombie_02
        ["zombie3"] = (3513, true, EmoteKind.Dance),                  // emo_dance_zombie_03
        ["zombie4"] = (3514, true, EmoteKind.Dance),                  // emo_dance_zombie_04
        ["zombie5"] = (3515, true, EmoteKind.Dance),                  // emo_dance_zombie_05
        ["slide"] = (3521, true, EmoteKind.Dance),                    // emo_dance_slide_01
        ["slide2"] = (3522, true, EmoteKind.Dance),                   // emo_dance_slide_02
        ["slide3"] = (3523, true, EmoteKind.Dance),                   // emo_dance_slide_03
        ["slide4"] = (3524, true, EmoteKind.Dance),                   // emo_dance_slide_04
        ["slide5"] = (3525, true, EmoteKind.Dance),                   // emo_dance_slide_05
        ["xmas"] = (3531, true, EmoteKind.Dance),                     // emo_dance_xmas_01
        ["xmas2"] = (3532, true, EmoteKind.Dance),                    // emo_dance_xmas_02
        ["xmas3"] = (3533, true, EmoteKind.Dance),                    // emo_dance_xmas_03
        ["xmas4"] = (3534, true, EmoteKind.Dance),                    // emo_dance_xmas_04
        ["rocknroll"] = (3541, true, EmoteKind.Dance),                // emo_dance_rroll_01
        ["rocknroll2"] = (3542, true, EmoteKind.Dance),               // emo_dance_rroll_02
        ["rocknroll3"] = (3543, true, EmoteKind.Dance),               // emo_dance_rroll_03
        ["rocknroll4"] = (3544, true, EmoteKind.Dance),               // emo_dance_rroll_04
        ["totem"] = (3551, true, EmoteKind.Dance),                    // emo_dance_totem_01
        ["totem2"] = (3552, true, EmoteKind.Dance),                   // emo_dance_totem_02
        ["totem3"] = (3553, true, EmoteKind.Dance),                   // emo_dance_totem_03
        ["totem4"] = (3554, true, EmoteKind.Dance),                   // emo_dance_totem_04
        ["waltz"] = (3560, true, EmoteKind.Dance),                    // emo_dance_waltz_01
        ["waltz2"] = (3561, true, EmoteKind.Dance),                   // emo_dance_waltz_02
        ["waltz3"] = (3562, true, EmoteKind.Dance),                   // emo_dance_waltz_03
        ["waltz4"] = (3563, true, EmoteKind.Dance),                   // emo_dance_waltz_04
        ["plants"] = (3564, true, EmoteKind.Dance),                   // emo_dance_plants_01
        ["plants2"] = (3565, true, EmoteKind.Dance),                  // emo_dance_plants_02
        ["plants3"] = (3566, true, EmoteKind.Dance),                  // emo_dance_plants_03
        ["plants4"] = (3567, true, EmoteKind.Dance),                  // emo_dance_plants_04
        ["chicken"] = (35010, true, EmoteKind.Dance),                 // emo_chicken_dance_01
        ["chicken2"] = (35020, true, EmoteKind.Dance),                // emo_chicken_dance_02
        ["chicken3"] = (35030, true, EmoteKind.Dance),                // emo_chicken_dance_03
        ["chicken4"] = (35040, true, EmoteKind.Dance),                // emo_chicken_dance_04
        ["chicken5"] = (35050, true, EmoteKind.Dance),                // emo_chicken_dance_05
        ["ballet"] = (3568000, true, EmoteKind.Dance),                // emo_dance_balletboombox_01
        ["bigband"] = (3568001, true, EmoteKind.Dance),               // emo_dance_bigbandboombox_01
        ["gramophone"] = (3568002, true, EmoteKind.Dance),            // emo_dance_gramophoneboombox_01
        ["headbanger"] = (3568003, true, EmoteKind.Dance),            // emo_dance_headbangerboombox_01
        ["heavymetal"] = (3568004, true, EmoteKind.Dance),            // emo_dance_heavymetalboombox_01
        ["robodance"] = (3568005, true, EmoteKind.Dance),             // emo_dance_roboboombox_01
        ["tiki"] = (3568006, true, EmoteKind.Dance),                  // emo_dance_tikiboombox_01
        ["scifi"] = (3568007, true, EmoteKind.Dance),                 // emo_dance_wackyscifiboombox_01
        ["western"] = (3568008, true, EmoteKind.Dance),               // emo_dance_westernboombox_01
        ["xylophone"] = (3568009, true, EmoteKind.Dance),             // emo_dance_xylophoneboombox_01
        ["kpop"] = (3568010, true, EmoteKind.Dance),                  // emo_dance_kpop_01
        ["8bit"] = (3568011, true, EmoteKind.Dance),                  // emo_dance_8bit_01
        ["boomboxshuffle"] = (3568012, true, EmoteKind.Dance),        // emo_dance_shuffle_01
        ["ballet2"] = (3569000, true, EmoteKind.Dance),               // emo_dance_balletboombox_02
        ["bigband2"] = (3569001, true, EmoteKind.Dance),              // emo_dance_bigbandboombox_02
        ["gramophone2"] = (3569002, true, EmoteKind.Dance),           // emo_dance_gramophoneboombox_02
        ["headbanger2"] = (3569003, true, EmoteKind.Dance),           // emo_dance_headbangerboombox_02
        ["heavymetal2"] = (3569004, true, EmoteKind.Dance),           // emo_dance_heavymetalboombox_02
        ["robodance2"] = (3569005, true, EmoteKind.Dance),            // emo_dance_roboboombox_02
        ["tiki2"] = (3569006, true, EmoteKind.Dance),                 // emo_dance_tikiboombox_02
        ["scifi2"] = (3569007, true, EmoteKind.Dance),                // emo_dance_wackyscifiboombox_02
        ["western2"] = (3569008, true, EmoteKind.Dance),              // emo_dance_westernboombox_02
        ["xylophone2"] = (3569009, true, EmoteKind.Dance),            // emo_dance_xylophoneboombox_02
        ["kpop2"] = (3569010, true, EmoteKind.Dance),                 // emo_dance_kpop_02
        ["8bit2"] = (3569011, true, EmoteKind.Dance),                 // emo_dance_8bit_02
        ["boomboxshuffle2"] = (3569012, true, EmoteKind.Dance),       // emo_dance_shuffle_02
        ["ballet3"] = (3570000, true, EmoteKind.Dance),               // emo_dance_balletboombox_03
        ["bigband3"] = (3570001, true, EmoteKind.Dance),              // emo_dance_bigbandboombox_03
        ["gramophone3"] = (3570002, true, EmoteKind.Dance),           // emo_dance_gramophoneboombox_03
        ["headbanger3"] = (3570003, true, EmoteKind.Dance),           // emo_dance_headbangerboombox_03
        ["heavymetal3"] = (3570004, true, EmoteKind.Dance),           // emo_dance_heavymetalboombox_03
        ["robodance3"] = (3570005, true, EmoteKind.Dance),            // emo_dance_roboboombox_03
        ["tiki3"] = (3570006, true, EmoteKind.Dance),                 // emo_dance_tikiboombox_03
        ["scifi3"] = (3570007, true, EmoteKind.Dance),                // emo_dance_wackyscifiboombox_03
        ["western3"] = (3570008, true, EmoteKind.Dance),              // emo_dance_westernboombox_03
        ["xylophone3"] = (3570009, true, EmoteKind.Dance),            // emo_dance_xylophoneboombox_03
        ["kpop3"] = (3570010, true, EmoteKind.Dance),                 // emo_dance_kpop_03
        ["8bit3"] = (3570011, true, EmoteKind.Dance),                 // emo_dance_8bit_03
        ["boomboxshuffle3"] = (3570012, true, EmoteKind.Dance),       // emo_dance_shuffle_03
        ["ballet4"] = (3571000, true, EmoteKind.Dance),               // emo_dance_balletboombox_04
        ["bigband4"] = (3571001, true, EmoteKind.Dance),              // emo_dance_bigbandboombox_04
        ["gramophone4"] = (3571002, true, EmoteKind.Dance),           // emo_dance_gramophoneboombox_04
        ["headbanger4"] = (3571003, true, EmoteKind.Dance),           // emo_dance_headbangerboombox_04
        ["heavymetal4"] = (3571004, true, EmoteKind.Dance),           // emo_dance_heavymetalboombox_04
        ["robodance4"] = (3571005, true, EmoteKind.Dance),            // emo_dance_roboboombox_04
        ["tiki4"] = (3571006, true, EmoteKind.Dance),                 // emo_dance_tikiboombox_04
        ["scifi4"] = (3571007, true, EmoteKind.Dance),                // emo_dance_wackyscifiboombox_04
        ["western4"] = (3571008, true, EmoteKind.Dance),              // emo_dance_westernboombox_04
        ["xylophone4"] = (3571009, true, EmoteKind.Dance),            // emo_dance_xylophoneboombox_04
        ["kpop4"] = (3571010, true, EmoteKind.Dance),                 // emo_dance_kpop_04
        ["8bit4"] = (3571011, true, EmoteKind.Dance),                 // emo_dance_8bit_04
        ["boomboxshuffle4"] = (3571012, true, EmoteKind.Dance),       // emo_dance_shuffle_04
        ["boomboxshuffle5"] = (3572012, true, EmoteKind.Dance),       // emo_dance_shuffle_05
        ["pose1"] = (3251, true, EmoteKind.Pose),                     // emo_pose_01
        ["pose2"] = (3252, true, EmoteKind.Pose),                     // emo_pose_02
        ["pose3"] = (3253, true, EmoteKind.Pose),                     // emo_pose_03
        ["pose4"] = (3254, true, EmoteKind.Pose),                     // emo_pose_04
        ["pose5"] = (3255, true, EmoteKind.Pose),                     // emo_pose_05
        ["pose6"] = (3256, true, EmoteKind.Pose),                     // emo_pose_06
        ["pose7"] = (3257, true, EmoteKind.Pose),                     // emo_pose_07
        ["pose8"] = (3258, true, EmoteKind.Pose),                     // emo_pose_08
        ["pose9"] = (3259, true, EmoteKind.Pose),                     // emo_pose_09
        ["pose10"] = (3260, true, EmoteKind.Pose),                    // emo_pose_10
        ["posing"] = (3321, true, EmoteKind.Pose),                    // emo_posing
        ["angry"] = (3301, false, EmoteKind.Social),                  // emo_angry
        ["applaud"] = (3302, false, EmoteKind.Social),                // emo_applaud
        ["beg"] = (3303, false, EmoteKind.Social),                    // emo_beg
        ["callperson"] = (3304, false, EmoteKind.Social),             // emo_call_person
        ["callpet"] = (3305, false, EmoteKind.Social),                // emo_call_pet
        ["confused"] = (3307, false, EmoteKind.Social),               // emo_confused
        ["congrats"] = (3308, false, EmoteKind.Social),               // emo_congrats
        ["cry"] = (3309, false, EmoteKind.Social),                    // emo_cry
        ["curious"] = (3310, false, EmoteKind.Social),                // emo_curious
        ["flex"] = (3311, false, EmoteKind.Social),                   // emo_flex
        ["flirt"] = (3312, false, EmoteKind.Social),                  // emo_flirt
        ["giggle"] = (3313, false, EmoteKind.Social),                 // emo_giggle
        ["give"] = (3314, false, EmoteKind.Social),                   // emo_give
        ["insult"] = (3315, false, EmoteKind.Social),                 // emo_insult
        ["no"] = (3317, false, EmoteKind.Social),                     // emo_no
        ["omg"] = (3318, false, EmoteKind.Social),                    // emo_omg
        ["ponder"] = (3320, false, EmoteKind.Social),                 // emo_ponder
        ["pray"] = (3322, false, EmoteKind.Social),                   // emo_pray
        ["receive"] = (3323, false, EmoteKind.Social),                // emo_receive
        ["rofl"] = (3324, false, EmoteKind.Social),                   // emo_rofl
        ["sad"] = (3325, false, EmoteKind.Social),                    // emo_sad
        ["shrug"] = (3326, false, EmoteKind.Social),                  // emo_shrug
        ["shy"] = (3327, false, EmoteKind.Social),                    // emo_shy
        ["sleepsitting"] = (3328, true, EmoteKind.Social),            // emo_sleep_sitting
        ["sleepstanding"] = (3329, true, EmoteKind.Social),           // emo_sleep_standing
        ["taunt"] = (3330, false, EmoteKind.Social),                  // emo_taunt
        ["thanks"] = (3331, false, EmoteKind.Social),                 // emo_thanks
        ["twirl"] = (3332, false, EmoteKind.Social),                  // emo_twirl
        ["yawn"] = (3334, false, EmoteKind.Social),                   // emo_yawn
        ["yes"] = (3335, false, EmoteKind.Social),                    // emo_yes
        ["frustrated"] = (3336, false, EmoteKind.Social),             // emo_frustrated
        ["afraid"] = (3339, false, EmoteKind.Social),                 // emo_afraid
        ["fart"] = (3340, false, EmoteKind.Social),                   // emo_fart
        ["burp"] = (3341, false, EmoteKind.Social),                   // emo_burp
        ["charge"] = (3343, false, EmoteKind.Social),                 // emo_charge
        ["salute"] = (3344, false, EmoteKind.Social),                 // emo_salute
        ["shoo"] = (3345, false, EmoteKind.Social),                   // emo_shoo
        ["tap"] = (3346, false, EmoteKind.Social),                    // emo_tap
        ["wait"] = (3347, false, EmoteKind.Social),                   // emo_wait
        ["wink"] = (3348, false, EmoteKind.Social),                   // emo_wink
        ["backflip"] = (3349, false, EmoteKind.Social),               // emo_backflip
        ["airguitar"] = (3350, false, EmoteKind.Social),              // emo_air_guitar
        ["spraycan"] = (3351, false, EmoteKind.Social),               // emo_spraycan
        ["letsbefriends"] = (3352, false, EmoteKind.Social),          // emo_lets_be_friends
        ["goingonquest"] = (3353, false, EmoteKind.Social),           // emo_going_on_quest
        ["hey"] = (3354, false, EmoteKind.Social),                    // emo_hey
        ["please"] = (3355, false, EmoteKind.Social),                 // emo_please
        ["ilikethat"] = (3356, false, EmoteKind.Social),              // emo_I_like_that
        ["sorry"] = (3357, false, EmoteKind.Social),                  // emo_sorry
        ["dontworry"] = (3358, false, EmoteKind.Social),              // emo_dont_worry
        ["thatstinks"] = (3359, false, EmoteKind.Social),             // emo_that_stinks
        ["byebye"] = (3360, false, EmoteKind.Social),                 // emo_bye_bye
        ["imouttahere"] = (3361, false, EmoteKind.Social),            // emo_Im_outta_here
        ["peaceout"] = (3362, false, EmoteKind.Social),               // emo_peace_out
        ["yo"] = (3363, false, EmoteKind.Social),                     // emo_yo
        ["whatsup"] = (3364, false, EmoteKind.Social),                // emo_whats_up
        ["hi"] = (3365, false, EmoteKind.Social),                     // emo_hi
        ["clickheels"] = (3366, false, EmoteKind.Social),             // emo_click_heels
        ["flip"] = (3367, false, EmoteKind.Social),                   // emo_flip
        ["startled"] = (3368, false, EmoteKind.Social),               // emo_startled
        ["scare"] = (3369, false, EmoteKind.Social),                  // emo_scare
        ["drink"] = (3371, false, EmoteKind.Social),                  // emo_drink
    };

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length != 1)
        {
            SendList(invoker);
            return true;
        }

        if (args[0].Equals("stop", StringComparison.OrdinalIgnoreCase))
        {
            invoker.StopLoopingEmote();
            return true;
        }

        if (!Emotes.TryGetValue(args[0], out var emote))
        {
            ChatHelper.SendSystemMessage(invoker, $"No emote '{args[0]}'. Type !emote for the list.");
            return true;
        }

        if (invoker.Mount is not null)
        {
            ChatHelper.SendSystemMessage(invoker, "Dismount to emote.");
            return true;
        }

        Play(invoker, emote);
        return true;
    }

    /// <summary>
    /// Plays a named emote for everyone near the player (Lua's player:emote(name)). False for a name it doesn't know,
    /// or while they ride.
    /// </summary>
    public static bool TryPlay(Player player, string name)
    {
        if (!Emotes.TryGetValue(name, out var emote) || player.Mount is not null)
            return false;

        Play(player, emote);
        return true;
    }

    private static void Play(Player player, (int AnimationId, bool Loop, EmoteKind Kind) emote)
    {
        player.StopLoopingEmote();

        if (emote.Loop)
        {
            player.LoopingEmoteAnimation = emote.AnimationId;
            player.LoopingEmoteFrom = player.Position;
        }

        player.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
        {
            Guid = player.Guid,
            AnimationId = emote.AnimationId,
            Flags = (byte)(emote.Loop ? 1 : 0)
        }, true);
    }

    private static void SendList(Player invoker)
    {
        foreach (var kind in Enum.GetValues<EmoteKind>())
        {
            var names = Emotes.Where(emote => emote.Value.Kind == kind).Select(emote => emote.Key);

            ChatHelper.SendSystemMessage(invoker, $"{kind} emotes: {string.Join(", ", names)}.");
        }

        ChatHelper.SendSystemMessage(invoker, "Dances and poses loop until you walk away or type !emote stop.");
    }
}

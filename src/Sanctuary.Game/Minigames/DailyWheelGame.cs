using System;
using System.Collections.Concurrent;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions.Rewards;

namespace Sanctuary.Game.Minigames;

/// <summary>
/// The daily coin wheel ("Spin For The Win!", game_wheel.swf). The movie talks to the server with tab-separated
/// Flash messages (its Wheel.WheelGameClient class); the flow is connect, wheel data, a spin request answered
/// with the slot to land on, and the reward once the wheel stops. The wheel pays its own coins, so its
/// Minigames.json entry pays nothing more.
/// </summary>
[FlashMinigame("Wheel")]
public sealed class DailyWheelGame : FlashMinigame
{
    /// <summary>
    /// MiniGameData.txt: "Spin For The Win!".
    /// </summary>
    public const int MinigameId = 8;

    private const int WheelId = 1;

    /// <summary>
    /// The movie's wheel artwork: type 1 is the ten-slot wheel.
    /// </summary>
    private const int WheelType = 1;

    /// <summary>
    /// The coin icon. (The reward window turns icon 22663 into this one plus a marketplace "Shop Now!" button.)
    /// </summary>
    private const int CoinRewardIconId = 4809;

    /// <summary>
    /// String ids for the reward window: "Coins" and "Come back tomorrow for another spin!".
    /// </summary>
    private const int CoinsNameId = 4923;
    private const int ComeBackTomorrowId = 408045;

    /// <summary>
    /// The wheel's slots, clockwise from the arrow: the movie's slot category (its picture) and the coins it pays.
    /// Categories 15-21 are the movie's 2, 5, 10, 20, 30, 40 and 50 coin pictures.
    /// </summary>
    private static readonly (int Category, int Coins, int Weight)[] Slots =
    [
        (15, 2, 10),
        (17, 10, 7),
        (16, 5, 9),
        (18, 20, 5),
        (15, 2, 10),
        (19, 30, 3),
        (16, 5, 9),
        (20, 40, 2),
        (17, 10, 7),
        (21, 50, 1)
    ];

    /// <summary>
    /// The day (UTC) each player last spun. Kept in memory, so a server restart gives everyone a spin back.
    /// </summary>
    private static readonly ConcurrentDictionary<ulong, DateOnly> LastSpinDays = new();

    /// <summary>
    /// The slot this game's spin landed on, paid when the client reports that the wheel stopped.
    /// </summary>
    private int? _pendingSlot;

    public override bool OnMessage(string name, FlashArgs args)
    {
        switch (name)
        {
            case "OnConnectMsg":
            {
                var slots = string.Join(' ', Slots.Select(x => x.Category));

                Send("OnWheelDataMsg", WheelId, WheelType, slots, 0, 0);
                SendWheelUpdate();
                Send("OnServerReadyMsg");
                return true;
            }

            case "OnChangeWheelRequestMsg":
                Send("OnWheelChangedMsg", WheelId);
                return true;

            case "OnWheelSpinRequestMsg":
            {
                if (SpinsLeft(Player) <= 0)
                    return true;

                LastSpinDays[Player.Guid] = Today;

                // The movie moves the wheel this many slots past where its own spin would stop, which lands the
                // arrow on that slot.
                var slot = RollSlot();
                _pendingSlot = slot;
                Send("OnSpinInfoMsg", slot);
                return true;
            }

            case "OnWheelStopMsg":
            {
                if (_pendingSlot is not { } slot)
                    return true;

                _pendingSlot = null;

                var coins = Slots[slot].Coins;

                RewardManager.TryGrantCurrency(Player, CurrencyType.Coins, coins);
                Send("OnRewardInfoMsg", CoinRewardIconId, 0, coins, CoinsNameId, ComeBackTomorrowId, 0);
                SendWheelUpdate();
                return true;
            }

            case "FRServer_GameClose":
            case "FRServer_GameQuit":
                // MinigameManager closes the movie.
                return true;
        }

        return false;
    }

    /// <summary>
    /// Spins left today: one a day, reset at midnight UTC.
    /// </summary>
    public static int SpinsLeft(Player player)
    {
        return LastSpinDays.TryGetValue(player.Guid, out var day) && day == Today ? 0 : 1;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Sends the spins left, the consecutive days played and the seconds until the next spin.
    /// </summary>
    private void SendWheelUpdate()
    {
        var secondsUntilTomorrow = (int)(DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow).TotalSeconds;

        Send("OnWheelUpdateMsg", WheelId, SpinsLeft(Player), 1, secondsUntilTomorrow);
    }

    private static int RollSlot()
    {
        var roll = Random.Shared.Next(Slots.Sum(x => x.Weight));

        for (var i = 0; i < Slots.Length; i++)
        {
            if (roll < Slots[i].Weight)
                return i;

            roll -= Slots[i].Weight;
        }

        return 0;
    }
}

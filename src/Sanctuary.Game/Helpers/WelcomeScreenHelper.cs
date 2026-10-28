using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.GameCommerce;

namespace Sanctuary.Game.Helpers;

public static class WelcomeScreenHelper
{
    private const int MarketplaceAnnouncementId = 1;
    private const int MarketplaceAnnouncementPriority = 1;
    private const int MarketplaceAnnouncementIconId = 38443;
    private const int MarketplaceAnnouncementTitleStringId = 414;
    private const int MarketplaceAnnouncementBodyStringId = 415;
    private const int MarketplaceAnnouncementButtonStringId = 436323;
    private const int MarketplaceAnnouncementParam2 = 31415;
    private const int WelcomeContent1NameId = 6185;
    private const int WelcomeContent1DescriptionId = 6186;
    private const int WelcomeContent2NameId = 6187;
    private const int WelcomeContent2DescriptionId = 6188;
    private const int WelcomeContent3NameId = 6189;
    private const int WelcomeContent3DescriptionId = 6190;
    private const int MmmDonutClaimCodeNameId = 401519;
    private const int MmmDonutClaimCodeDescriptionId = 401534;
    private const int MmmDonutClaimCodeIconId = 929;
    private const int BerryCupcakeClaimCodeNameId = 401517;
    private const int BerryCupcakeClaimCodeDescriptionId = 401532;
    private const int BerryCupcakeClaimCodeIconId = 939;
    private const int SkeletalClaimCodeNameId = 409157;
    private const int SkeletalClaimCodeDescriptionId = 109132;
    private const int SkeletalClaimCodeIconId = 3459;
    private const int StrawberriesClaimCodeNameId = 409158;
    private const int StrawberriesClaimCodeDescriptionId = 108948;
    private const int StrawberriesClaimCodeIconId = 3441;
    private const int FroggyClaimCodeNameId = 409159;
    private const int FroggyClaimCodeDescriptionId = 3141;
    private const int FroggyClaimCodeIconId = 1258;
    private const int SandwichClaimCodeNameId = 409160;
    private const int SandwichClaimCodeDescriptionId = 2430;
    private const int SandwichClaimCodeIconId = 949;
    private const int FirstLoginSecondsSinceLastLogin = -1;

    // What's New items, Referee Picks and patch notes are sent as announcements whose text is all in
    // StringParam2: tag|title|text|button|icon (a patch note's title is its date). The welcome screen mod
    // (ClientMods/welcome/home.as) shows them in its own What's New, Referee Picks and Patch Notes panels.
    private const string WhatsNewTag = "[[SANCTUARY_NEWS]]";
    private const int WhatsNewAnnouncementId = 100;
    private const string RefereePickTag = "[[SANCTUARY_PICK]]";
    private const int RefereePickAnnouncementId = 1000;
    private const string PatchNoteTag = "[[SANCTUARY_NOTE]]";
    private const int PatchNoteAnnouncementId = 2000;

    public static AnnouncementDataSendPacket GetAnnouncementsPacket(IEnumerable<WelcomeCard> refereePicks, IEnumerable<AnnouncementInfo>? announcements = null)
    {
        if (announcements is not null)
            return new AnnouncementDataSendPacket { Announcements = announcements.ToList() };

        var news = WhatsNew.Current.Select((card, i) => GetCardAnnouncement(card, WhatsNewTag, WhatsNewAnnouncementId + i)).ToList();

        // The stock Marketplace ad only when there's no news to show.
        if (news.Count == 0)
        {
            news.Add(new AnnouncementInfo
            {
                Id = MarketplaceAnnouncementId,
                Priority = MarketplaceAnnouncementPriority,
                IconId = MarketplaceAnnouncementIconId,
                TitleStringId = MarketplaceAnnouncementTitleStringId,
                BodyStringId = MarketplaceAnnouncementBodyStringId,
                ButtonStringId = MarketplaceAnnouncementButtonStringId,
                LuaCall = "Marketplace",
                Param2 = MarketplaceAnnouncementParam2
            });
        }

        return new AnnouncementDataSendPacket
        {
            Announcements =
            [
                .. news,
                .. refereePicks.Select((card, i) => GetCardAnnouncement(card, RefereePickTag, RefereePickAnnouncementId + i)),
                .. PatchNotes.Current.Select((note, i) => GetCardAnnouncement(new WelcomeCard(note.Date, note.Subject), PatchNoteTag, PatchNoteAnnouncementId + i))
            ]
        };
    }

    private static AnnouncementInfo GetCardAnnouncement(WelcomeCard card, string tag, int id) => new()
    {
        Id = id,
        Priority = id,
        LuaCall = card.Action,
        Param1 = card.Param1,
        StringParam2 = tag + string.Concat(new[] { card.Title, card.Text, card.ButtonLabel, card.Icon }
            .Select(field => "|" + field.Replace('|', '/')))
    };

    public static int SecondsSinceLastLogin(DateTimeOffset? previousLogin, DateTimeOffset loginTime) =>
        previousLogin.HasValue
            ? (int)Math.Clamp((loginTime - previousLogin.Value).TotalSeconds, 0, int.MaxValue)
            : FirstLoginSecondsSinceLastLogin;

    public static PacketLoadWelcomeScreen GetWelcomeScreenPacket(int secondsSinceLastLogin,
        IEnumerable<ContentInfo>? contents = null, IEnumerable<ClaimCodeInfo>? claimCodes = null)
    {
        var packetLoadWelcomeScreen = new PacketLoadWelcomeScreen
        {
            SecondsSinceLastLogin = secondsSinceLastLogin
        };

        packetLoadWelcomeScreen.Contents.AddRange(contents ??
        [
            new ContentInfo
            {
                NameId = WelcomeContent1NameId,
                DescriptionId = WelcomeContent1DescriptionId,
            },
            new ContentInfo
            {
                NameId = WelcomeContent2NameId,
                DescriptionId = WelcomeContent2DescriptionId,
            },
            new ContentInfo
            {
                NameId = WelcomeContent3NameId,
                DescriptionId = WelcomeContent3DescriptionId,
            }
        ]);

        packetLoadWelcomeScreen.ClaimCodes.AddRange(claimCodes ??
        [
            new ClaimCodeInfo
            {
                Code = "MMMDONUT",
                NameId = MmmDonutClaimCodeNameId,
                DescriptionId = MmmDonutClaimCodeDescriptionId,
                IconId = MmmDonutClaimCodeIconId
            },
            new ClaimCodeInfo
            {
                Code = "BERRYCUPCAKE",
                NameId = BerryCupcakeClaimCodeNameId,
                DescriptionId = BerryCupcakeClaimCodeDescriptionId,
                IconId = BerryCupcakeClaimCodeIconId
            },
            new ClaimCodeInfo
            {
                Code = "SKELETAL",
                NameId = SkeletalClaimCodeNameId,
                DescriptionId = SkeletalClaimCodeDescriptionId,
                IconId = SkeletalClaimCodeIconId,
                TintAlias = "navy"
            },
            new ClaimCodeInfo
            {
                Code = "STRAWBERRIES",
                NameId = StrawberriesClaimCodeNameId,
                DescriptionId = StrawberriesClaimCodeDescriptionId,
                IconId = StrawberriesClaimCodeIconId,
                TintAlias = "concrete"
            },
            new ClaimCodeInfo
            {
                Code = "FROGGY",
                NameId = FroggyClaimCodeNameId,
                DescriptionId = FroggyClaimCodeDescriptionId,
                IconId = FroggyClaimCodeIconId
            },
            new ClaimCodeInfo
            {
                Code = "SANDWICH",
                NameId = SandwichClaimCodeNameId,
                DescriptionId = SandwichClaimCodeDescriptionId,
                IconId = SandwichClaimCodeIconId
            }
        ]);

        return packetLoadWelcomeScreen;
    }
}

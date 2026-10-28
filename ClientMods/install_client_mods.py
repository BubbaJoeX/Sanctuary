#!/usr/bin/env python3
"""Installs Sanctuary's client mods into a Free Realms client (or removes them).

UI movies are loaded synchronously from the local asset packs, and only when their crc
matches the asset manifest the client downloaded from the asset server. So installing:
  - writes the built UI mods (the .gfx and .lst files in src/Resources/ClientMods) into a new Assets_NNN.pack, and
  - switches the UI window definitions (and LoadingScreen.xml) to the renamed mod movies.
Sanctuary's WebAPI publishes the same files in the manifest it serves, which keeps them "current".

It also copies the referee client tools (lua/referee_tools.lua) to Resources/Scripts/1 and the client
extensions (lua/client_extensions.lua, e.g. Sanctuary's region badges) to Resources/Scripts/2, which the
server runs (for referees, and for everyone) through the client's dofileInternalOnlyCommand (see ClientLuaHelper).

Originals are backed up next to each file as <name>.sanctuary-backup.

Examples:
  python install_client_mods.py --client "%LOCALAPPDATA%/OSFRLauncher/Servers/OSFR Public Server/Client"
  python install_client_mods.py --client "..." --uninstall
"""

import shutil
import struct
import zlib
from argparse import ArgumentParser
from pathlib import Path

script_dir = Path(__file__).resolve().parent
mods_dir = script_dir.parent / "src" / "Resources" / "ClientMods"

BACKUP_SUFFIX = ".sanctuary-backup"
MARKER_FILE = "sanctuary_client_mods.txt"

# dofileInternalOnlyCommand(n) runs Client.PathScripts .. n, and the server can only pass it a number,
# so the files are named after one (ClientLuaHelper.RefereeToolsScript, ClientLuaHelper.ClientExtensionsScript).
REFEREE_LUA = (script_dir / "lua" / "referee_tools.lua", Path("Resources/Scripts/1"))
CLIENT_EXTENSIONS_LUA = (script_dir / "lua" / "client_extensions.lua", Path("Resources/Scripts/2"))
CLIENT_LUA = [(REFEREE_LUA, "referee client tools"), (CLIENT_EXTENSIONS_LUA, "client extensions")]

COMPRESSED_MAGIC = 0xA1B2C3D4
# Each chunk's index sits in one block of this size, with the chunk's data after it: the client reads no more of an
# index than this, so a longer one loses every entry past it ("swf not found"), and the stock packs never exceed it.
PACK_INDEX_BLOCK = 8192

# (client file, [(original text, modded text), ...]), applied to the original file.
WINDOW_PATCHES = [
    (Path("UI/UiModules/Main/wndChatLog.xml"), [
        ("UI\\chatWindowCtrl.swf", "UI\\sanctuaryChatWindowCtrl.swf"),
        # The client clips a movie to its window, so the chat window is enlarged to about the size of
        # the screen to give the Referee Tools panel room. Its Lua bottom-aligns the window and the mod
        # draws chat 1:1 at the bottom left, so chat itself stays where it was.
        ('Width="550" Height="345"', 'Width="2000" Height="1045"'),
    ]),
    (Path("UI/UiModules/Main/wndGameDock.xml"), [
        ("UI\\gamedock.swf", "UI\\sanctuaryGameDock.swf"),
    ]),
    (Path("UI/UiModules/Main/wndDialogNew.xml"), [
        ("UI\\dialog.swf", "UI\\sanctuaryDialog.swf"),
    ]),
    (Path("UI/UiModules/Main/wndHousingEditPanel.xml"), [
        ("UI\\housingEditPanel.swf", "UI\\sanctuaryHousingEditPanel.swf"),
        # Room for 10 items a page instead of 6 (4 more slots, 74 wide); the window and its movie both.
        ('Width="800" Height="200"', 'Width="1096" Height="200"'),
    ]),
    # Station Cash and membership no longer exist: the Marketplace window is the coin store, and Station
    # Cash wallets and Add Station Cash buttons are gone.
    (Path("UI/UiModules/Main/wndMarketplace.xml"), [
        ("UI\\marketplace.swf", "UI\\sanctuaryMarketplace.swf"),
        # The coin store is laid out 1300 wide (marketplace/coin_store_only.as); the window and its movie both.
        ('Width="1024"', 'Width="1300"'),
    ]),
    (Path("UI/UiModules/Main/wndNavigationBar.xml"), [("UI\\navigation.swf", "UI\\sanctuaryNavigation.swf")]),
    # The pet's HUD: name bar, Stats card and remote.
    (Path("UI/UiModules/Main/wndPetNameBar.xml"), [("UI\\PetNameBar.swf", "UI\\sanctuaryPetNameBar.swf")]),
    (Path("UI/UiModules/Main/wndPetDetails.xml"), [("UI\\aboutpet.swf", "UI\\sanctuaryAboutPet.swf")]),
    (Path("UI/UiModules/Main/wndPetRemote.xml"), [("UI\\petController.swf", "UI\\sanctuaryPetController.swf")]),
    (Path("UI/UiModules/Main/wndMysteryRewardBrowser.xml"), [("UI\\MysteryRewardBrowser.swf", "UI\\sanctuaryMysteryRewardBrowser.swf")]),
    (Path("UI/UiModules/Main/wndMysteryRewardBrowserSmall.xml"),
     [("UI\\MysteryRewardBrowserSmall.swf", "UI\\sanctuaryMysteryRewardBrowserSmall.swf")]),
    (Path("UI/UiModules/Main/wndPaidNameChange.xml"), [("UI\\PaidNameChange.swf", "UI\\sanctuaryPaidNameChange.swf")]),
    (Path("UI/UiModules/Main/wndUnifiedMessageWindow.xml"), [("UI\\UnifiedMessageWindow.swf", "UI\\sanctuaryUnifiedMessageWindow.swf")]),
    (Path("UI/UiModules/Main/wndAboutMe.xml"), [("UI\\aboutme.swf", "UI\\sanctuaryAboutMe.swf")]),
    # The welcome screen is a wider home screen (welcome/home.as draws it 1:1, anchored top left); the
    # window and its movie both. Its Lua centers the window from its size.
    (Path("UI/UiModules/Main/wndWelcomeHandler.xml"), [
        ("UI\\welcome.swf", "UI\\sanctuaryWelcome.swf"),
        ('Width="1024" Height="768"', 'Width="1366" Height="768"'),
    ]),
    (Path("UI/UiModules/Main/wndActivityPortal.xml"), [("UI\\ActivityPortal.swf", "UI\\sanctuaryActivityPortal.swf")]),
    (Path("UI/UiModules/Main/wndHousingPreview.xml"), [("UI\\HousingPreview.swf", "UI\\sanctuaryHousingPreview.swf")]),
    (Path("UI/UiModules/Main/wndStorage.xml"), [("UI\\storage.swf", "UI\\sanctuaryStorage.swf")]),
    (Path("UI/UiModules/Main/wndMyStuffBrowser.xml"), [("UI\\MyStuffBrowser.swf", "UI\\sanctuaryMyStuffBrowser.swf")]),
    # The repainted HUD (RESKINS in build_client_mods.py).
    (Path("UI/UiModules/Main/wndFRUtilNavigation.xml"), [("UI\\FRUtilNavigation.swf", "UI\\sanctuaryFRUtilNavigation.swf")]),
    # The video recorder/screenshot HUD, hidden (hud/no_video_capture.as).
    (Path("UI/UiModules/Main/wndVideoCaptureHud.xml"), [("UI\\video_new.swf", "UI\\sanctuaryVideoNew.swf")]),
    (Path("UI/UiModules/Main/wndScreenCapture.xml"), [("UI\\video_new.swf", "UI\\sanctuaryVideoNew.swf")]),
    (Path("UI/UiModules/Main/wndOptions.xml"), [("UI\\options.swf", "UI\\sanctuaryOptions.swf")]),
    (Path("UI/UiModules/Main/wndOptionsPanel.xml"), [("UI\\optionsPanel.swf", "UI\\sanctuaryOptionsPanel.swf")]),
    (Path("UI/UiModules/Main/wndMiniMap.xml"), [("UI\\mini_map.swf", "UI\\sanctuaryMiniMap.swf")]),
    (Path("UI/UiModules/Main/wndSocial.xml"), [("UI\\social.swf", "UI\\sanctuarySocial.swf")]),
    (Path("UI/UiModules/Main/wndAtlas.xml"), [("UI\\atlas.swf", "UI\\sanctuaryAtlas.swf")]),
    (Path("UI/UiModules/Main/wndEquipmentPane.xml"),
     [("UI\\equipmentscreen_new.swf", "UI\\sanctuaryEquipmentScreen.swf")]),
    (Path("UI/UiModules/Main/wndInventory.xml"), [("UI\\inventory.swf", "UI\\sanctuaryInventory.swf")]),
    # The tile browser (Games, Pets, Rides, Houses) and the farming browser share one movie.
    (Path("UI/UiModules/Main/wndBrowserV2.xml"), [("UI\\Browser_V2.swf", "UI\\sanctuaryBrowserV2.swf")]),
    (Path("UI/UiModules/Main/wndFarmingBrowser.xml"), [("UI\\Browser_V2.swf", "UI\\sanctuaryBrowserV2.swf")]),
    (Path("UI/UiModules/Main/wndCollectionsBrowser.xml"),
     [("UI\\CollectionsBrowser.swf", "UI\\sanctuaryCollectionsBrowser.swf")]),
    (Path("UI/UiModules/Main/wndAchievements.xml"), [("UI\\achievements.swf", "UI\\sanctuaryAchievements.swf")]),
    (Path("UI/UiModules/Main/wndFRJobBrowser.xml"), [("UI\\FRJobBrowser.swf", "UI\\sanctuaryFRJobBrowser.swf")]),
    # The rest of the housing UI (housing/*.as).
    (Path("UI/UiModules/Main/wndHousingSidePanel.xml"), [("UI\\housingSidePanel.swf", "UI\\sanctuaryHousingSidePanel.swf")]),
    (Path("UI/UiModules/Main/wndHousingOptions.xml"), [("UI\\HousingOptions.swf", "UI\\sanctuaryHousingOptions.swf")]),
    (Path("UI/UiModules/Main/wndHousingEditButton.xml"), [("UI\\housingEditButton.swf", "UI\\sanctuaryHousingEditButton.swf")]),
    (Path("UI/UiModules/Main/wndHousingSubMenu.xml"), [("UI\\housingSubMenu.swf", "UI\\sanctuaryHousingSubMenu.swf")]),
    (Path("UI/UiModules/Main/wndHousingBrowser.xml"), [("UI\\HousingBrowser.swf", "UI\\sanctuaryHousingBrowser.swf")]),
    # Farming: the Farm-O-Matic, its sample picker, the farm's side panel and the action progress bar.
    (Path("UI/UiModules/Main/wndFarmOMatic.xml"), [("UI\\farmomatic.swf", "UI\\sanctuaryFarmOMatic.swf")]),
    (Path("UI/UiModules/Main/wndSampleList.xml"), [("UI\\sampleList.swf", "UI\\sanctuarySampleList.swf")]),
    (Path("UI/UiModules/Main/wndFarmingSidePanel.xml"), [("UI\\FarmingSidePanel.swf", "UI\\sanctuaryFarmingSidePanel.swf")]),
    (Path("UI/UiModules/Main/wndActionProgress.xml"), [("UI\\actionProgress.swf", "UI\\sanctuaryActionProgress.swf")]),
    (Path("UI/UiModules/Main/wndNpcMerchantV2.xml"), [("UI\\NPCMerchantV2.swf", "UI\\sanctuaryNPCMerchantV2.swf")]),
    # The older drop-down browser (recipes, collections, pets, vehicles, classes) and the Farmers Market share one movie.
    (Path("UI/UiModules/Main/wndBrowser.xml"), [("UI\\Browser.swf", "UI\\sanctuaryBrowser.swf")]),
    (Path("UI/UiModules/Main/wndFarmersMarketBrowser.xml"), [("UI\\Browser.swf", "UI\\sanctuaryBrowser.swf")]),
    # The in-world HUD (hud/*.as) and its repainted buttons.
    (Path("UI/UiModules/Main/wndToolTip.xml"), [("UI\\ToolTipSystem.swf", "UI\\sanctuaryToolTipSystem.swf")]),
    (Path("UI/UiModules/Main/wndTarget.xml"), [("UI\\target.swf", "UI\\sanctuaryTarget.swf")]),
    (Path("UI/UiModules/Main/wndInteractionPrompt.xml"), [("UI\\interactionPrompt.swf", "UI\\sanctuaryInteractionPrompt.swf")]),
    (Path("UI/UiModules/Main/wndRespawn.xml"), [("UI\\revive_window.swf", "UI\\sanctuaryReviveWindow.swf")]),
    (Path("UI/UiModules/Main/wndNotification.xml"), [("UI\\notifications.swf", "UI\\sanctuaryNotifications.swf")]),
    (Path("UI/UiModules/Main/wndNotificationSm.xml"), [("UI\\notificationSm.swf", "UI\\sanctuaryNotificationSm.swf")]),
    (Path("UI/UiModules/Main/wndNotificationQuests.xml"), [("UI\\notificationsQuests.swf", "UI\\sanctuaryNotificationsQuests.swf")]),
    (Path("UI/UiModules/Main/wndAchievementNotification.xml"), [("UI\\achievementNotification.swf", "UI\\sanctuaryAchievementNotification.swf")]),
    (Path("UI/UiModules/Main/wndXpButton.xml"), [("UI\\XpButton.swf", "UI\\sanctuaryXpButton.swf")]),
    (Path("UI/UiModules/Main/wndLevelProgressPane.xml"), [("UI\\FRLevelProgressBar.swf", "UI\\sanctuaryFRLevelProgressBar.swf")]),
    (Path("UI/UiModules/Main/wndFRProgressBar.xml"), [("UI\\FRProgressBar.swf", "UI\\sanctuaryFRProgressBar.swf")]),
    (Path("UI/UiModules/Main/wndPetProgressBar.xml"), [("UI\\FRProgressBar.swf", "UI\\sanctuaryFRProgressBar.swf")]),
    (Path("UI/UiModules/Main/wndHerosJournalButton.xml"), [("UI\\HerosJournalButton.swf", "UI\\sanctuaryHerosJournalButton.swf")]),
    (Path("UI/UiModules/Main/wndStarCounter.xml"), [("UI\\starCounter.swf", "UI\\sanctuaryStarCounter.swf")]),
    (Path("UI/UiModules/Main/wndQuickItem.xml"), [("UI\\quickitem.swf", "UI\\sanctuaryQuickItem.swf")]),
    (Path("UI/UiModules/Main/wndRadialMenu.xml"), [("UI\\radial_menu.swf", "UI\\sanctuaryRadialMenu.swf")]),
    (Path("UI/UiModules/Main/wndCannedChat.xml"), [("UI\\cannedchat.swf", "UI\\sanctuaryCannedChat.swf")]),
    (Path("UI/UiModules/Main/wndArrowIndicator.xml"), [("UI\\ArrowIndicator.swf", "UI\\sanctuaryArrowIndicator.swf")]),
    (Path("UI/UiModules/Main/wndPayAttention.xml"), [("UI\\payattention.swf", "UI\\sanctuaryPayAttention.swf")]),
    (Path("UI/UiModules/Main/wndSceneText.xml"), [("UI\\scenetext.swf", "UI\\sanctuarySceneText.swf")]),
    # Quests, the generic dialogs and the retired membership/Station Cash windows.
    (Path("UI/UiModules/Main/wndQuestScreen.xml"), [("UI\\questjournal.swf", "UI\\sanctuaryQuestJournal.swf")]),
    (Path("UI/UiModules/Main/wndQuestStart.xml"), [("UI\\queststart.swf", "UI\\sanctuaryQuestStart.swf")]),
    (Path("UI/UiModules/Main/wndQuestEnd.xml"), [("UI\\queststart.swf", "UI\\sanctuaryQuestStart.swf")]),
    (Path("UI/UiModules/Main/wndQuestHelper.xml"), [("UI\\quest_helper.swf", "UI\\sanctuaryQuestHelper.swf")]),
    (Path("UI/UiModules/Main/wndObjectives.xml"), [("UI\\objectiveHelper.swf", "UI\\sanctuaryObjectiveHelper.swf")]),
    (Path("UI/UiModules/Main/wndMiniGameTasks.xml"), [("UI\\objectiveHelper.swf", "UI\\sanctuaryObjectiveHelper.swf")]),
    (Path("UI/UiModules/Main/wndMessageWindow.xml"), [("UI\\messageWindow.swf", "UI\\sanctuaryMessageWindow.swf")]),
    (Path("UI/UiModules/Main/wndUtilityWindow.xml"), [("UI\\messageWindow.swf", "UI\\sanctuaryMessageWindow.swf")]),
    (Path("UI/UiModules/Main/wndMessageWindowV2.xml"), [("UI\\messageWindowV2.swf", "UI\\sanctuaryMessageWindowV2.swf")]),
    (Path("UI/UiModules/Main/wndConfirmationWindow.xml"), [("UI\\ConfirmationWindow.swf", "UI\\sanctuaryConfirmationWindow.swf")]),
    (Path("UI/UiModules/Main/wndError.xml"), [("UI\\error.swf", "UI\\sanctuaryError.swf")]),
    (Path("UI/UiModules/Main/wndDirectedPlayPrompt.xml"), [("UI\\DirectedPlayPrompt.swf", "UI\\sanctuaryDirectedPlayPrompt.swf")]),
    (Path("UI/UiModules/Main/wndFRMembership.xml"), [("UI\\FRMembership.swf", "UI\\sanctuaryFRMembership.swf")]),
    (Path("UI/UiModules/Main/wndSCAvailable.xml"), [("UI\\SCAvailable.swf", "UI\\sanctuarySCAvailable.swf")]),
    (Path("UI/UiModules/Main/wndUpgradeButton.xml"), [("UI\\hud_membership_button.swf", "UI\\sanctuaryHudMembershipButton.swf")]),
    (Path("UI/UiModules/Main/wndMarketplaceRentalNag.xml"), [("UI\\MarketplaceRentalNag.swf", "UI\\sanctuaryMarketplaceRentalNag.swf")]),
    (Path("UI/UiModules/Main/wndJobTrialExpired.xml"), [("UI\\JobTrialExpired.swf", "UI\\sanctuaryJobTrialExpired.swf")]),
    (Path("UI/UiModules/Main/wndNewItemRem.xml"), [("UI\\NewItemRem.swf", "UI\\sanctuaryNewItemRem.swf")]),
    (Path("UI/UiModules/Main/wndNpxPlayMore.xml"), [("UI\\NpxPlayMore.swf", "UI\\sanctuaryNpxPlayMore.swf")]),
    (Path("UI/UiModules/Main/wndUpgradeDialog.xml"), [("UI\\upgrade_dialog.swf", "UI\\sanctuaryUpgradeDialog.swf")]),
    # Character select and creation, the loading widgets and the tutorials (login/*.as).
    (Path("UI/UiModules/Main/wndCharacterSelectV2.xml"), [("UI\\CharacterSelectV2.swf", "UI\\sanctuaryCharacterSelectV2.swf")]),
    (Path("UI/UiModules/Main/wndCharacterCreateType.xml"), [("UI\\characterCreateType.swf", "UI\\sanctuaryCharacterCreateType.swf")]),
    (Path("UI/UiModules/Main/wndCharacterCreate.xml"), [("UI\\charCreate.swf", "UI\\sanctuaryCharCreate.swf")]),
    (Path("UI/UiModules/Main/wndCharacterNameScreen.xml"), [("UI\\characterNameScreen.swf", "UI\\sanctuaryCharacterNameScreen.swf")]),
    (Path("UI/UiModules/Main/wndCharacterCustomNameScreen.xml"), [("UI\\characterCustomNameScreen.swf", "UI\\sanctuaryCharacterCustomNameScreen.swf")]),
    (Path("UI/UiModules/Main/wndLoadingLarge.xml"), [("UI\\loading_large.swf", "UI\\sanctuaryLoadingLarge.swf")]),
    (Path("UI/UiModules/Main/wndLoadingProgress.xml"), [("UI\\loading_progress.swf", "UI\\sanctuaryLoadingProgress.swf")]),
    (Path("UI/UiModules/Main/wndAssetLoadingStatus.xml"), [("UI\\AssetLoadingStatus.swf", "UI\\sanctuaryAssetLoadingStatus.swf")]),
    (Path("UI/UiModules/Main/wndTutorialDialog.xml"), [("UI\\tutorial_dialog.swf", "UI\\sanctuaryTutorialDialog.swf")]),
    (Path("UI/UiModules/Main/wndNewPlayerFTEWindow.xml"), [("UI\\NewPlayerFTEWindow.swf", "UI\\sanctuaryNewPlayerFTEWindow.swf")]),
    (Path("UI/UiModules/Main/wndCombatFTEWindow.xml"), [("UI\\CombatFTEWindow.swf", "UI\\sanctuaryCombatFTEWindow.swf")]),
    (Path("UI/UiModules/Main/wndFTELogoutReward.xml"), [("UI\\FTELogoutReward.swf", "UI\\sanctuaryFTELogoutReward.swf")]),
    (Path("UI/UiModules/Main/wndTutorialArrow.xml"), [("UI\\tutorial_arrow.swf", "UI\\sanctuaryTutorialArrow.swf")]),
    (Path("UI/UiModules/Main/wndTutorialButton.xml"), [("UI\\tutorialButton.swf", "UI\\sanctuaryTutorialButton.swf")]),
    # The character, social, activity and details windows.
    (Path("UI/UiModules/Main/wndAbilitiesScreen.xml"), [("UI\\AbilitiesScreen.swf", "UI\\sanctuaryAbilitiesScreen.swf")]),
    (Path("UI/UiModules/Main/wndPlayerInspect.xml"), [("UI\\PlayerInspect.swf", "UI\\sanctuaryPlayerInspect.swf")]),
    (Path("UI/UiModules/Main/wndCharacterNameChange.xml"), [("UI\\CharacterNameChange.swf", "UI\\sanctuaryCharacterNameChange.swf")]),
    (Path("UI/UiModules/Main/wndPlayerTrade.xml"), [("UI\\player_trade.swf", "UI\\sanctuaryPlayerTrade.swf")]),
    (Path("UI/UiModules/Main/wndTradeConfirmation.xml"), [("UI\\tradeConfirmation.swf", "UI\\sanctuaryTradeConfirmation.swf")]),
    (Path("UI/UiModules/Main/wndFriendPicker.xml"), [("UI\\friendPicker.swf", "UI\\sanctuaryFriendPicker.swf")]),
    (Path("UI/UiModules/Main/wndGuildDialog.xml"), [("UI\\guildDialog.swf", "UI\\sanctuaryGuildDialog.swf")]),
    (Path("UI/UiModules/Main/wndCombatGroup.xml"), [("UI\\combatGroupWindow.swf", "UI\\sanctuaryCombatGroupWindow.swf")]),
    (Path("UI/UiModules/Main/wndActivityCalendar.xml"), [("UI\\activityCalendar.swf", "UI\\sanctuaryActivityCalendar.swf")]),
    (Path("UI/UiModules/Main/wndQuickMatch.xml"), [("UI\\quickMatch.swf", "UI\\sanctuaryQuickMatch.swf")]),
    (Path("UI/UiModules/Main/wndMysteryChest.xml"), [("UI\\MysteryChest.swf", "UI\\sanctuaryMysteryChest.swf")]),
    (Path("UI/UiModules/Main/wndKeyBinds.xml"), [("UI\\keybinds.swf", "UI\\sanctuaryKeyBinds.swf")]),
    (Path("UI/UiModules/Main/wndCollectionDetails.xml"), [("UI\\collection_details.swf", "UI\\sanctuaryCollectionDetails.swf")]),
    (Path("UI/UiModules/Main/wndRecipeDetails.xml"), [("UI\\recipe_details.swf", "UI\\sanctuaryRecipeDetails.swf")]),
    # The release notes, What's New, VIP, credits, comic, bug report and Communicator windows, and the retired
    # purchase gateway.
    (Path("UI/UiModules/Main/wndUpdateNotes.xml"), [("UI\\UpdateNotes.swf", "UI\\sanctuaryUpdateNotes.swf")]),
    (Path("UI/UiModules/Main/wndWhatsNewPanel.xml"), [("UI\\WhatsNewPanel.swf", "UI\\sanctuaryWhatsNewPanel.swf")]),
    (Path("UI/UiModules/Main/wndNudgeWindow.xml"), [("UI\\WhatsNewPanel.swf", "UI\\sanctuaryWhatsNewPanel.swf")]),
    (Path("UI/UiModules/Main/wndVipMessageWindow.xml"), [("UI\\VipMessageWindow.swf", "UI\\sanctuaryVipMessageWindow.swf")]),
    (Path("UI/UiModules/Main/wndCredits.xml"), [("UI\\credits.swf", "UI\\sanctuaryCredits.swf")]),
    (Path("UI/UiModules/Main/wndComicBook.xml"), [("UI\\ComicBook.swf", "UI\\sanctuaryComicBook.swf")]),
    (Path("UI/UiModules/Main/wndBugSubmission.xml"), [("UI\\bugsubmission.swf", "UI\\sanctuaryBugSubmission.swf")]),
    (Path("UI/UiModules/Main/wndCommunicator.xml"), [("UI\\Communicator.swf", "UI\\sanctuaryCommunicator.swf")]),
    (Path("UI/UiModules/Main/wndFRGateway.xml"), [("UI\\FRGateway.swf", "UI\\sanctuaryFRGateway.swf")]),
    (Path("UI/UiModules/Main/wndStoryBook.xml"), [("UI\\StoryBook.swf", "UI\\sanctuaryStoryBook.swf")]),
    # Minigames in the Main Menu's style (minigames/*.as).
    (Path("UI/UiModules/Main/wndMinigameStartScreen.xml"), [("UI\\MinigameStartScreen.swf", "UI\\sanctuaryMinigameStartScreen.swf")]),
    (Path("UI/UiModules/Main/wndMinigameTitleBar.xml"), [("UI\\MinigameTitleBar.swf", "UI\\sanctuaryMinigameTitleBar.swf")]),
    (Path("UI/UiModules/Main/wndFlashTimer.xml"), [("UI\\timer.swf", "UI\\sanctuaryTimer.swf")]),
    (Path("UI/UiModules/Main/wndRaceTimer.xml"), [("UI\\timer.swf", "UI\\sanctuaryTimer.swf")]),
    (Path("UI/UiModules/Main/wndPiratesTimer.xml"), [("UI\\timer.swf", "UI\\sanctuaryTimer.swf")]),
    (Path("UI/UiModules/Main/wndFlashScore.xml"), [("UI\\score.swf", "UI\\sanctuaryScore.swf")]),
    (Path("UI/UiModules/Main/wndMiniGameLevel.xml"), [("UI\\minigameLevel.swf", "UI\\sanctuaryMinigameLevel.swf")]),
    (Path("UI/UiModules/Main/wndCountdown.xml"), [("UI\\countdown3.swf", "UI\\sanctuaryCountdown.swf")]),
    (Path("UI/UiModules/Main/wndMiniGameEndStageScreen.xml"), [("UI\\MinigameEndStageScreen.swf", "UI\\sanctuaryMinigameEndStageScreen.swf")]),
    (Path("UI/UiModules/Main/wndMinigameDetail.xml"), [("UI\\MinigameDetail.swf", "UI\\sanctuaryMinigameDetail.swf")]),
    (Path("UI/UiModules/Main/wndScoreScreen.xml"), [("UI\\scoreScreen.swf", "UI\\sanctuaryScoreScreen.swf")]),
    (Path("UI/UiModules/Main/wndMinigameStatusDescPane.xml"), [("UI\\minigameStatusDescPane.swf", "UI\\sanctuaryMinigameStatusDescPane.swf")]),
    (Path("UI/UiModules/Main/wndMinigameStatusGroupPane.xml"), [("UI\\minigameStatusGroupPane.swf", "UI\\sanctuaryMinigameStatusGroupPane.swf")]),
    (Path("UI/UiModules/Main/wndMinigameStatusObjPane.xml"), [("UI\\minigameStatusObjPane.swf", "UI\\sanctuaryMinigameStatusObjPane.swf")]),
    (Path("UI/UiModules/Main/wndMiniGameTutorial.xml"), [("UI\\minigameTutorial.swf", "UI\\sanctuaryMinigameTutorial.swf")]),
    (Path("UI/UiModules/Main/wndLobbyTop.xml"), [("UI\\lobbyTop.swf", "UI\\sanctuaryLobbyTop.swf")]),
    (Path("UI/UiModules/Main/wndLobbyLeft.xml"), [("UI\\lobbyLeft.swf", "UI\\sanctuaryLobbyLeft.swf")]),
    (Path("UI/UiModules/Main/wndLobbyLeftSoccer.xml"), [("UI\\lobbyLeftSoccer.swf", "UI\\sanctuaryLobbyLeftSoccer.swf")]),
    (Path("UI/UiModules/Main/wndLobbyBottom.xml"), [("UI\\lobbyBottom.swf", "UI\\sanctuaryLobbyBottom.swf")]),
    (Path("UI/UiModules/Main/wndFishFinder.xml"), [("UI\\fishFinder.swf", "UI\\sanctuaryFishFinder.swf")]),
    (Path("UI/UiModules/Main/wndFishingTensionMeter.xml"), [("UI\\fishingMeter_REV2.swf", "UI\\sanctuaryFishingMeter.swf")]),
    (Path("UI/UiModules/Main/wndFishingDistanceMeter.xml"), [("UI\\fishingmeters_bbe.swf", "UI\\sanctuaryFishingMeters.swf")]),
    (Path("UI/UiModules/Main/wndFishingStatusText.xml"), [("UI\\fishingStatus.swf", "UI\\sanctuaryFishingStatus.swf")]),
    (Path("UI/UiModules/Main/wndFishingCaughtStatusText.xml"), [("UI\\caughtStatus.swf", "UI\\sanctuaryCaughtStatus.swf")]),
    (Path("UI/UiModules/Main/wndFishingReel.xml"), [("UI\\icon_reel.swf", "UI\\sanctuaryIconReel.swf")]),
    (Path("UI/UiModules/Main/wndFishingCombo.xml"), [("UI\\icon_fish_combo_bbe.swf", "UI\\sanctuaryIconFishCombo.swf")]),
    (Path("UI/UiModules/Main/wndFishingTutorialSplat.xml"), [("UI\\fish_moving_target.swf", "UI\\sanctuaryFishMovingTarget.swf")]),
    (Path("UI/UiModules/Main/wndRaceResults.xml"), [("UI\\raceResults.swf", "UI\\sanctuaryRaceResults.swf")]),
    (Path("UI/UiModules/Main/wndRaceOrder.xml"), [("UI\\raceOrder.swf", "UI\\sanctuaryRaceOrder.swf")]),
    (Path("UI/UiModules/Main/wndRacePlaceKeeper.xml"), [("UI\\racePlaceMin.swf", "UI\\sanctuaryRacePlaceMin.swf")]),
    (Path("UI/UiModules/Main/wndLapCount.xml"), [("UI\\raceLap.swf", "UI\\sanctuaryRaceLap.swf")]),
    (Path("UI/UiModules/Main/wndRaceBoostBar.xml"), [("UI\\raceBoostBar.swf", "UI\\sanctuaryRaceBoostBar.swf")]),
    (Path("UI/UiModules/Main/wndRaceItemSlots.xml"), [("UI\\itemSlots.swf", "UI\\sanctuaryItemSlots.swf")]),
    (Path("UI/UiModules/Main/wndRaceMiniMap.xml"), [("UI\\race_mini_map.swf", "UI\\sanctuaryRaceMiniMap.swf")]),
    (Path("UI/UiModules/Main/wndRaceBackwards.xml"), [("UI\\raceBackwards.swf", "UI\\sanctuaryRaceBackwards.swf")]),
    (Path("UI/UiModules/Main/wndRaceProxArrow1.xml"), [("UI\\raceProxArrow.swf", "UI\\sanctuaryRaceProxArrow.swf")]),
    (Path("UI/UiModules/Main/wndRaceProxArrow2.xml"), [("UI\\raceProxArrow.swf", "UI\\sanctuaryRaceProxArrow.swf")]),
    (Path("UI/UiModules/Main/wndRaceProxArrow3.xml"), [("UI\\raceProxArrow.swf", "UI\\sanctuaryRaceProxArrow.swf")]),
    (Path("UI/UiModules/Main/wndRaceProxArrow4.xml"), [("UI\\raceProxArrow.swf", "UI\\sanctuaryRaceProxArrow.swf")]),
    (Path("UI/UiModules/Main/wndRaceProxArrow5.xml"), [("UI\\raceProxArrow.swf", "UI\\sanctuaryRaceProxArrow.swf")]),
    (Path("UI/UiModules/Main/wndRaceProxArrow6.xml"), [("UI\\raceProxArrow.swf", "UI\\sanctuaryRaceProxArrow.swf")]),
    (Path("UI/UiModules/Main/wndRaceProxArrow7.xml"), [("UI\\raceProxArrow.swf", "UI\\sanctuaryRaceProxArrow.swf")]),
    (Path("UI/UiModules/Main/wndRaceProxArrow8.xml"), [("UI\\raceProxArrow.swf", "UI\\sanctuaryRaceProxArrow.swf")]),
    (Path("UI/UiModules/Main/wndRaceProxArrow9.xml"), [("UI\\raceProxArrow9.swf", "UI\\sanctuaryRaceProxArrow9.swf")]),
    (Path("UI/UiModules/Main/wndDerbyResults.xml"), [("UI\\derbyResults.swf", "UI\\sanctuaryDerbyResults.swf")]),
    (Path("UI/UiModules/Main/wndDerbyCrown.xml"), [("UI\\derbyCrown.swf", "UI\\sanctuaryDerbyCrown.swf")]),
    (Path("UI/UiModules/Main/wndDemoHealthBar.xml"), [("UI\\demoHealthBar.swf", "UI\\sanctuaryDemoHealthBar.swf")]),
    (Path("UI/UiModules/Main/wndSSSScores.xml"), [("UI\\SSS_score.swf", "UI\\sanctuarySSSScore.swf")]),
    (Path("UI/UiModules/Main/wndSSSTimer.xml"), [("UI\\sss_timer.swf", "UI\\sanctuarySSSTimer.swf")]),
    (Path("UI/UiModules/Main/wndSSSChargeMeter.xml"), [("UI\\scr_charge_meter_bbe.swf", "UI\\sanctuaryScrChargeMeter.swf")]),
    (Path("UI/UiModules/Main/wndSSSPopups.xml"), [("UI\\SSS_popups.swf", "UI\\sanctuarySSSPopups.swf")]),
    (Path("UI/UiModules/Main/wndPiratesPopups.xml"), [("UI\\SSS_popups.swf", "UI\\sanctuarySSSPopups.swf")]),
    (Path("UI/UiModules/Main/wndSSSHalftimeBoard.xml"), [("UI\\SSS_halftimeBoard.swf", "UI\\sanctuarySSSHalftimeBoard.swf")]),
    (Path("UI/UiModules/Main/wndPiratesResultScreen.xml"), [("UI\\pir_resultscreen_bbe.swf", "UI\\sanctuaryPirResultScreen.swf")]),
    (Path("UI/UiModules/Main/wndPiratesShipSelection.xml"), [("UI\\pir_shipselection_bbe.swf", "UI\\sanctuaryPirShipSelection.swf")]),
    (Path("UI/UiModules/Main/wndPiratesStandings.xml"), [("UI\\pir_player_ship_hold_bbe.swf", "UI\\sanctuaryPirStandings.swf")]),
    (Path("UI/UiModules/Main/wndPiratesInstructions.xml"), [("UI\\pir_instructions_bbe.swf", "UI\\sanctuaryPirInstructions.swf")]),
    # The client picks a loading screen at random from this block, so the OSFR one is the only live entry
    # and the stock ones are commented out.
    (Path("LoadingScreen.xml"), [
        ("<LoadingScreens>",
         '<LoadingScreens>\n\t<LoadingScreen Id=1000 Filename="sanctuaryLoadingScreen.swf" ShowToMembers=1 ShowToNonMembers=1 Weight=100/>\n<!-- Stock loading screens'),
        ("</LoadingScreens>", "-->\n</LoadingScreens>"),
    ]),
]


def load_mods():
    # Packs hold assets uncompressed under their plain names. Only UI movies go in: everything else in the
    # folder (published items, zones) streams from the asset server, and a copy in this read-only pack would
    # stop the client from ever taking a newer one (it hangs on "non-writeable pack file").
    mods = []
    for path in sorted(mods_dir.iterdir()):
        if not path.name.endswith((".gfx", ".lst", ".lst.z")):
            continue
        data = path.read_bytes()
        name = path.name
        if name.endswith(".z"):
            magic, _ = struct.unpack(">II", data[:8])
            if magic != COMPRESSED_MAGIC:
                raise ValueError(f"{name} is not a compressed asset")
            data = zlib.decompress(data[8:])
            name = name[:-2]
        mods.append((name, data))
    return mods


def pack_chunks(assets):
    """Splits the assets into chunks whose index fits one PACK_INDEX_BLOCK."""
    chunks = [[]]
    size = 8
    for name, data in assets:
        entry = 4 + len(name.encode()) + 12
        if chunks[-1] and size + entry > PACK_INDEX_BLOCK:
            chunks.append([])
            size = 8
        chunks[-1].append((name, data))
        size += entry
    return chunks


def write_pack(path, assets):
    # Chunks, like the stock packs: each starts with its index (next chunk offset, 0 = last; entry count; then
    # name length, name, data offset, data size and crc32 per entry, all big-endian) in a PACK_INDEX_BLOCK, followed
    # by its entries' data.
    chunks = pack_chunks(assets)
    with path.open("wb") as f:
        start = 0
        for i, chunk in enumerate(chunks):
            offset = start + PACK_INDEX_BLOCK
            end = offset + sum(len(data) for _, data in chunk)
            index = struct.pack(">II", end if i < len(chunks) - 1 else 0, len(chunk))
            for name, data in chunk:
                encoded = name.encode()
                index += struct.pack(">I", len(encoded)) + encoded + struct.pack(">III", offset, len(data), zlib.crc32(data))
                offset += len(data)
            f.write(index)
            f.write(b"\0" * (PACK_INDEX_BLOCK - len(index)))
            for _, data in chunk:
                f.write(data)
            start = end


def next_pack_path(client_dir):
    index = 0
    while (client_dir / f"Assets_{index:03d}.pack").exists():
        index += 1
    return client_dir / f"Assets_{index:03d}.pack"


def install(client_dir):
    mods = load_mods()
    if not mods:
        raise FileNotFoundError(f"No built mods in {mods_dir}; run build_client_mods.py first.")

    marker = client_dir / MARKER_FILE
    pack_path = client_dir / marker.read_text().strip() if marker.exists() else next_pack_path(client_dir)

    write_pack(pack_path, mods)
    marker.write_text(pack_path.name)
    print(f"{pack_path.name}: {', '.join(name for name, _ in mods)}")

    for relative_path, replacements in WINDOW_PATCHES:
        path = client_dir / relative_path
        backup = path.with_name(path.name + BACKUP_SUFFIX)

        if not backup.exists():
            shutil.copy2(path, backup)

        # Always patch the original, so reinstalling (or upgrading an older install) is idempotent.
        text = backup.read_text(encoding="utf-8-sig")

        for original, modded in replacements:
            if original not in text:
                raise ValueError(f"{relative_path}: {original} not found, refusing to patch")
            text = text.replace(original, modded)

        # Keep the BOM the client's files are saved with.
        path.write_text(text, encoding="utf-8-sig")
        print(f"{relative_path}: patched ({len(replacements)} change(s))")

    for (source, relative_path), description in CLIENT_LUA:
        target = client_dir / relative_path
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        print(f"{relative_path}: {description}")


def uninstall(client_dir):
    for relative_path, _ in WINDOW_PATCHES:
        path = client_dir / relative_path
        backup = path.with_name(path.name + BACKUP_SUFFIX)

        if not backup.exists():
            print(f"{relative_path}: no backup, nothing to restore")
            continue

        shutil.copy2(backup, path)
        backup.unlink()
        print(f"{relative_path}: restored")

    for (_, relative_path), _ in CLIENT_LUA:
        if (client_dir / relative_path).exists():
            (client_dir / relative_path).unlink()
            print(f"{relative_path}: removed")

    marker = client_dir / MARKER_FILE
    if marker.exists():
        pack_path = client_dir / marker.read_text().strip()
        pack_path.unlink(missing_ok=True)
        marker.unlink()
        print(f"{pack_path.name}: removed")


def main():
    parser = ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--client", type=Path, required=True, help="Free Realms client folder (contains FreeRealms.exe)")
    parser.add_argument("--uninstall", action="store_true", help="Remove the mods and restore the original UI")
    args = parser.parse_args()

    if not (args.client / "FreeRealms.exe").exists():
        raise FileNotFoundError(f"FreeRealms.exe not found in {args.client}")

    if args.uninstall:
        uninstall(args.client)
    else:
        install(args.client)


if __name__ == "__main__":
    main()

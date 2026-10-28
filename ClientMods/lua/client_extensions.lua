-- ===== Sanctuary client extensions (client Lua) =====
-- install_client_mods.py installs this as Resources/Scripts/2, and the server loads it for every player the first
-- time they enter the world, by calling the client's own dofileInternalOnlyCommand(2) through
-- AbilityPacketExecuteClientLua (see ClientLuaHelper). A client without it logs the failed dofile to
-- Logs/LuaErrors.log and carries on as before.
--
-- Region badges: walking into a region shows "Now entering" with the region's badge (notifications.swf's
-- playZoneAnimation). The stock scripts only know the stock regions, area ids 2 to 10
-- (BaseClient_CurrentPOI_OnDataUpdate and NotificationHandler:LoadingZone); these add Sanctuary's own, with the
-- badges build_client_mods.py makes (REGION_BADGES).
--
-- Game Options: tells the window's More Options page (options/game_options.as) the current values of the two
-- settings it adds.
--
-- Lua 5.1, run by the client. Safe to load more than once.

SanctuaryClient = SanctuaryClient or {}

-- Area id (a zone's AreaDefinitions Id, sent in PacketPOIChangeMessage) -> badge movie.
SanctuaryClient.RegionBadges = {
   [13] = "sanctuaryLavenderCoastNotification", -- Lavender Coast
}

-- The stock handlers, kept from the first load so reloading doesn't wrap them twice.
SanctuaryClient.StockCurrentPoiUpdate = SanctuaryClient.StockCurrentPoiUpdate or BaseClient_CurrentPOI_OnDataUpdate
SanctuaryClient.StockLoadingZone = SanctuaryClient.StockLoadingZone or NotificationHandler.LoadingZone

local function regionBadge(areaId)
   return SanctuaryClient.RegionBadges[tonumber(areaId)]
end

-- The client calls this by name when the current point of interest changes; the stock one ignores area ids
-- above 10.
function BaseClient_CurrentPOI_OnDataUpdate(...)
   local areaId = DsTable.Find(DataSources[DS_CURRENT_POI]):GetData(0, 1)
   if regionBadge(areaId) ~= nil then
      NotificationHandler:LoadingZone(tonumber(areaId))
      return
   end
   return SanctuaryClient.StockCurrentPoiUpdate(...)
end

-- The stock LoadingZone, with our badges: queued while notifications are held back (loading screens, cut
-- scenes), else the notification window plays the badge with the stock "Now entering" text and chime.
function NotificationHandler:LoadingZone(areaId)
   local badge = regionBadge(areaId)
   if badge == nil then
      return SanctuaryClient.StockLoadingZone(self, areaId)
   end
   if self.m_suppressNotifications then
      FunctionStack:Push("NotificationStack", "NotificationHandler:LoadingZone", function()
         NotificationHandler:LoadingZone(areaId)
      end)
      return
   end
   self:Open()
   self:CenterOnScreen()
   local movie = GfxCtrl.Find(self.m_movieName)
   movie:Reset()
   movie:Invoke("playZoneAnimation", Ui.GetString("note_nowEntering"), badge .. ".swf", "0xCCCCFF")
   Ui.PlaySoundFromDefinition(16125)
end

-- The More Options page shows two settings the stock Lua handles but OptionElements.txt never lists, so nothing
-- tells the window their values; this does, after the stock rows.
SanctuaryClient.StockOptionsUpdateSettings = SanctuaryClient.StockOptionsUpdateSettings or Options.UpdateSettings

function Options:UpdateSettings(...)
   local result = SanctuaryClient.StockOptionsUpdateSettings(self, ...)
   local movie = GfxCtrl.Find(self.optionsPanelSwf)
   if movie then
      movie:Invoke("goSetValue", "setShowAllNamePlates", Options:CheckStoredOption("NamePlateSettings", "ShowAllNamePlates", 1))
      movie:Invoke("goSetValue", "SetOptionShowTrainers", Ui.GetUserOptionsValue("MinimapOptions", "ShowTrainers", 1))
   end
   return result
end
-- ===== End Sanctuary client extensions =====

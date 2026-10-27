-- ===== Sanctuary referee tools (client Lua) =====
-- install_client_mods.py installs this as Resources/Scripts/1. The retail client ships without its
-- internal-only Lua (the Admin module, the /dofile binding, the debug console switch); the server
-- loads this file for referees when they enter the world, by calling the client's own
-- dofileInternalOnlyCommand(1) through AbilityPacketExecuteClientLua (see ClientLuaHelper).
--
--   /lua <code>      runs Lua in this client; "/lua =<expression>" prints its value
--   /dofile <file>   runs Resources/Scripts/<file>; "/dofile 1" reloads this file
--   /admin on|off    turns on the client's admin-only UI (the Admin global), e.g. the atlas
--                    printing world coordinates on click
--
-- Lua 5.1, run by the client. Safe to load more than once.

SanctuaryReferee = SanctuaryReferee or {}

-- A line in the chat log (and the Referee Tools status line), or print as a fallback.
function SanctuaryReferee.Say(text)
   text = tostring(text)
   if ChatLog ~= nil and ChatLog.AddMessage ~= nil then
      if pcall(ChatLog.AddMessage, ChatLog, nil, "Lua", text) then
         return
      end
   end
   print(text)
end

local function report(ok, ...)
   if not ok then
      SanctuaryReferee.Say("Lua error: " .. tostring((...)))
      return
   end
   local count = select("#", ...)
   if count == 0 then
      return
   end
   local parts = {}
   for i = 1, count do
      parts[i] = tostring((select(i, ...)))
   end
   SanctuaryReferee.Say(table.concat(parts, ", "))
end

-- /lua <code>
function SanctuaryReferee.Run(code)
   code = tostring(code or "")
   if code == "" then
      SanctuaryReferee.Say("Usage: /lua <code>, or /lua =<expression> to print a value.")
      return
   end
   if string.sub(code, 1, 1) == "=" then
      code = "return " .. string.sub(code, 2)
   end
   local chunk, err = loadstring(code, "=/lua")
   if chunk == nil then
      SanctuaryReferee.Say("Lua error: " .. tostring(err))
      return
   end
   report(pcall(chunk))
end

-- /admin on|off. The retail scripts test the Admin global (nil when off) for admin-only UI.
function SanctuaryReferee.Admin(state)
   state = string.lower(tostring(state or ""))
   if state == "on" then
      Admin = Admin or {}
      -- The atlas calls this with the clicked world position.
      Admin.OnClickAtlas = function(x, z)
         SanctuaryReferee.Say("Atlas: x " .. tostring(x) .. ", z " .. tostring(z))
      end
      -- Admin also turns on console logging, which opens this file; make sure it has a name.
      if Console ~= nil and Console.logFileName == nil then
         Console.logFileName = "FRConsoleLog.txt"
      end
   elseif state == "off" then
      Admin = nil
   else
      SanctuaryReferee.Say("Usage: /admin on|off (now " .. (Admin == nil and "off" or "on") .. ")")
      return
   end
   SanctuaryReferee.Say("Client admin mode " .. (Admin == nil and "off." or "on."))
end

BindCommandToLua("lua", "SanctuaryReferee.Run")
BindCommandToLua("dofile", "dofileInternalOnlyCommand")
BindCommandToLua("admin", "SanctuaryReferee.Admin")

-- Unlocks the chat log's debug console mode (minchat.lua StartDebugConsole).
Client.EnableDebugConsole = true

SanctuaryReferee.Say("Referee client tools loaded: /lua, /dofile, /admin on|off.")
-- ===== End Sanctuary referee tools =====

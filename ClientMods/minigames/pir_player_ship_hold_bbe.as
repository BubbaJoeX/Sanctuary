// ===== Sanctuary Pirates Standings =====
// Appended to pir_player_ship_hold_bbe's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The
// Pirates standings HUD (wndPiratesStandings): each player row (flag, name in the player's colour, gold hold bar)
// gets a navy glass plate with the flag in a frosted socket; the player's own row is rimmed in gold. The plates
// live inside the stock rows, so they slide with them when places swap and show and hide with them.

var STD_X = -262;
var STD_Y = -26;
var STD_W = 494;
var STD_H = 52;
var STD_FLAG_X = -235;

function stdBuild()
{
   var i;
   for(i = 1; i <= 6; i++)
   {
      var row = _root["pir_player" + i];
      row.sanctuaryPlate = mgUnder(row,"sanctuaryPlate");
      stdDraw(row,false);
      row.player_name.player_name_text.filters = mgOutline();
   }
}

function stdDraw(row, mine)
{
   var g = row.sanctuaryPlate;
   g.clear();
   mgGradientRect(g,STD_X,STD_Y,STD_W,STD_H,STD_H / 2,0x2E4FB0,0x141F55,80,mine ? MG_GOLD : MG_RIM,mine ? 100 : 55,mine ? 2.5 : 1.5);
   mgGloss(g,STD_X + 20,STD_Y + 2,STD_W - 40,STD_H * 0.4,10,22);
   mgSocketCircle(g,STD_FLAG_X,0,23,mine ? MG_GOLD : MG_RIM,mine ? 100 : 70,1.5);
   g.filters = mine ? [new flash.filters.GlowFilter(MG_GOLD,0.5,10,10,2,2)] : [new flash.filters.DropShadowFilter(2,90,MG_SHADOW,0.5,6,6,1,2)];
}

// Lua: enablePlayer(player, local) shows a row; the local player's (local 1) gets the gold rim.
var stdEnablePlayer = enablePlayer;
enablePlayer = function(player, local)
{
   var result = stdEnablePlayer.apply(_root,arguments);
   var row = Slots[player - 1];
   if(row.sanctuaryPlate != undefined)
   {
      stdDraw(row,local == 1);
   }
   return result;
};

stdBuild();
// ===== End Sanctuary Pirates Standings =====

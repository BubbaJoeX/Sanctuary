// ===== Sanctuary Arrow Indicator =====
// Appended to ArrowIndicator's main timeline by build_client_mods.py.
// The tutorial pointer (wndArrowIndicator: the screen dims around a hole over the thing to click, with a bouncing
// arrow and a glowing frame). The arrow and frame are repainted gold at build time (RESKINS); this script dims the
// screen in deep navy instead of black and gives the frame a soft gold glow. placeArrow_lua (which creates the
// dimming blockers on its first call) is wrapped; the blockers still swallow clicks as before.

var AI_DIM = 0x050A24;
var AI_GOLD = 0xFFC531;

function aiStyle()
{
   var i;
   for(i = 1; i <= 4; i++)
   {
      var b = _root["blocker" + i];
      if(b != undefined && !b.sanctuaryTinted)
      {
         new Color(b).setRGB(AI_DIM);
         b.sanctuaryTinted = true;
      }
   }
   arrowIndicator.glow_mc.filters = [new flash.filters.GlowFilter(AI_GOLD,0.8,14,14,2,2)];
   arrowIndicator.arrow_mc.filters = [new flash.filters.DropShadowFilter(3,90,AI_DIM,0.6,6,6,1,2)];
}

var aiPlaceArrow = placeArrow_lua;
placeArrow_lua = function()
{
   var result = aiPlaceArrow.apply(_root,arguments);
   aiStyle();
   return result;
};
// ===== End Sanctuary Arrow Indicator =====

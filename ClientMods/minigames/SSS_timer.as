// ===== Sanctuary Soccer Timer =====
// Appended to SSS_timer's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The soccer match
// clock (wndSSSTimer) is four bare digits and a colon (always m:ss); they get a navy glass pill with a gold rim
// behind them, like the minigame timer (minigames/timer.as). When the stock code switches to its red digits for
// the last seconds, the rim turns red and the pill breathes a red glow. The digits are untouched.

var STM_X = 2;
var STM_Y = 8;
var STM_W = 196;
var STM_H = 64;

var stmPlate;
var stmHurry;

function stmBuild()
{
   stmPlate = mgUnder(_root,"sanctuarySoccerTimer");
   stmPlate.onEnterFrame = stmPulse;
   stmDraw(false);
}

function stmDraw(hurry)
{
   stmHurry = hurry;
   var g = stmPlate;
   g.clear();
   mgGradientRect(g,STM_X,STM_Y,STM_W,STM_H,STM_H / 2,0x2E4FB0,0x141F55,92,hurry ? 0xFF6A5A : MG_GOLD,100,2.5);
   mgGloss(g,STM_X + 12,STM_Y + 3,STM_W - 24,STM_H * 0.42,14,30);
}

function stmPulse()
{
   if(!stmHurry)
   {
      this.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];
      return;
   }
   var k = (Math.sin(getTimer() / 160) + 1) / 2;
   this.filters = [new flash.filters.GlowFilter(0xFF4A3A,0.35 + 0.45 * k,10 + 10 * k,10 + 10 * k,2,2)];
}

// Every tick goes through setTimer, which picks the green or red digits (prefix "g" or "r").
var stmSetTimer = setTimer;
setTimer = function(s, r)
{
   var result = stmSetTimer.apply(_root,arguments);
   if((prefix == "r") != stmHurry)
   {
      stmDraw(prefix == "r");
   }
   return result;
};

stmBuild();
// ===== End Sanctuary Soccer Timer =====

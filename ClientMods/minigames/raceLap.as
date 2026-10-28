// ===== Sanctuary Race Lap =====
// Appended to raceLap's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The lap counter
// (wndLapCount: "LAP 2/3") on a navy glass pill with a gold rim and a chequered flag. A new lap pops the pill with
// a gold flash; on the final lap the rim stays lit and breathes. The numbers keep their size: the lap in light gold,
// "LAP" and the lap count in white and soft blue, all with the dark outline.

var RL_X = 4;
var RL_Y = 12;
var RL_W = 192;
var RL_H = 80;

var rlPlate;
var rlFinal = false;

function rlBuild()
{
   rlPlate = mgUnder(_root,"sanctuaryRaceLap");
   rlPlate._x = RL_X + RL_W / 2;
   rlPlate._y = RL_Y + RL_H / 2;
   var g = rlPlate.createEmptyMovieClip("bg",1);
   mgGradientRect(g,- RL_W / 2,- RL_H / 2,RL_W,RL_H,RL_H / 2,0x2E4FB0,0x141F55,92,MG_GOLD,100,2.5);
   mgGloss(g,- RL_W / 2 + 14,- RL_H / 2 + 3,RL_W - 28,RL_H * 0.4,16,30);
   var flag = rlPlate.createEmptyMovieClip("flag",2);
   flag._x = 14 - RL_W / 2;
   flag._y = -16;
   rlFlag(flag);
   rlPlate.flash = 0;
   rlPlate.onEnterFrame = rlTick;
   mgLight(lapCounter.CurLap,MG_GOLD_LIGHT);
   mgLight(lapCounter.lap,0xFFFFFF);
   mgLight(lapCounter.Laps,MG_SOFT);
}

// A chequered flag on a pole, about 30 x 34.
function rlFlag(g)
{
   g.lineStyle(3,MG_GOLD_LIGHT,100);
   g.moveTo(2,0);
   g.lineTo(2,34);
   g.lineStyle();
   var r;
   var c;
   for(r = 0; r < 3; r++)
   {
      for(c = 0; c < 4; c++)
      {
         g.beginFill((r + c) % 2 == 0 ? 0xFFFFFF : 0x101830,100);
         g.moveTo(4 + c * 6,1 + r * 6 + c);
         g.lineTo(10 + c * 6,1 + r * 6 + c + 1);
         g.lineTo(10 + c * 6,7 + r * 6 + c + 1);
         g.lineTo(4 + c * 6,7 + r * 6 + c);
         g.endFill();
      }
   }
   g.filters = mgOutline();
}

function rlTick()
{
   if(this.flash > 0)
   {
      this.flash -= 0.06;
   }
   var k = this.flash > 0 ? Math.sin(this.flash * Math.PI) : 0;
   this._xscale = this._yscale = 100 + 8 * k;
   var glow = 0;
   if(rlFinal)
   {
      glow = 0.4 + 0.3 * (Math.sin(getTimer() / 220) + 1) / 2;
   }
   glow = Math.max(glow,k);
   this.filters = [new flash.filters.GlowFilter(MG_GOLD,glow,8 + 14 * glow,8 + 14 * glow,2,2),new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];
}

function rlCheckFinal()
{
   var cur = parseInt(String(lapCounter.CurLap.text),10);
   var total = parseInt(String(lapCounter.Laps.text).substr(1),10);
   rlFinal = !isNaN(cur) && !isNaN(total) && total > 1 && cur >= total;
}

var rlSetCurrentLap = setCurrentLap;
setCurrentLap = function(lap)
{
   var result = rlSetCurrentLap.apply(_root,arguments);
   rlPlate.flash = 1;
   rlCheckFinal();
   return result;
};

var rlSetLapCount = setLapCount;
setLapCount = function(lap)
{
   var result = rlSetLapCount.apply(_root,arguments);
   rlCheckFinal();
   return result;
};

rlBuild();
// ===== End Sanctuary Race Lap =====

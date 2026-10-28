// ===== Sanctuary Race Backwards =====
// Appended to raceBackwards' main timeline (after minigames/mg_theme.as) by build_client_mods.py. The wrong-way
// warning (wndRaceBackwards): the stock U-turn arrow now sits on a navy glass disc with a gold rim that throbs with
// a red glow, so it catches the eye over any scenery. The arrow itself is unchanged and as big as before.

var rwDisc;

function rwBuild()
{
   rwDisc = mgUnder(_root,"sanctuaryBackwards");
   rwDisc._x = 32;
   rwDisc._y = 32;
   rwDisc.lineStyle(2.5,MG_GOLD,100);
   rwDisc.beginGradientFill("linear",[0x2E4FB0,0x141F55],[95,95],[0,255],{matrixType:"box",x:-30,y:-30,w:60,h:60,r:Math.PI / 2});
   mgCircle(rwDisc,0,0,30);
   rwDisc.endFill();
   mgGloss(rwDisc,-20,-27,40,18,9,30);
   rwDisc.onEnterFrame = function()
   {
      var k = (Math.sin(getTimer() / 140) + 1) / 2;
      this._xscale = this._yscale = 96 + 6 * k;
      this.filters = [new flash.filters.GlowFilter(0xFF3A2A,0.45 + 0.45 * k,6 + 8 * k,6 + 8 * k,2,2)];
   };
}

rwBuild();
// ===== End Sanctuary Race Backwards =====

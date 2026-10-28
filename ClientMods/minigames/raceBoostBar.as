// ===== Sanctuary Race Boost Bar =====
// Appended to raceBoostBar's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The boost meter
// (wndRaceBoostBar): its purple wedge frame is repainted gold (RESKINS), and here it gets a navy glass track under
// the fill, so the empty part of the meter reads as an empty trough instead of the world behind it, and the boost
// icon a navy socket with a gold rim that glows while the boost is full. The blue to red fill is the stock one.

// The frame texture's top left on stage; the wedge below is in its coordinates (184 x 55).
var RB_FRAME_X = 72.2;
var RB_FRAME_Y = 25.75;
var RB_ICON_X = 80.3;
var RB_ICON_Y = 65.75;

var rbLayer;

function rbBuild()
{
   rbLayer = mgUnder(_root,"sanctuaryBoostBar");
   var t = rbLayer.createEmptyMovieClip("track",1);
   t._x = RB_FRAME_X;
   t._y = RB_FRAME_Y;
   rbWedge(t,7,180,50,31,4,MG_NAVY,85);
   // A darker inner shadow along the top edge.
   t.lineStyle(2,0x050A24,50);
   t.moveTo(12,30.5);
   t.lineTo(176,5);
   var s = rbLayer.createEmptyMovieClip("socket",2);
   s._x = RB_ICON_X;
   s._y = RB_ICON_Y;
   s.lineStyle(2.5,MG_GOLD,100);
   s.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:-22,y:-22,w:44,h:44,r:Math.PI / 2});
   mgCircle(s,0,0,22);
   s.endFill();
   mgGloss(s,-15,-19,30,14,7,30);
   s.filters = mgShadow();
   s.full = false;
   s.onEnterFrame = rbTick;
}

// A wedge: from x0 to x1, flat along the bottom, its top rising from yl (left) to yr (right), corners rounded.
function rbWedge(g, x0, x1, bottom, yl, yr, color, alpha)
{
   var r = 5;
   var slope = (yr - yl) / (x1 - x0);
   g.lineStyle();
   g.beginFill(color,alpha);
   g.moveTo(x0 + r,bottom);
   g.lineTo(x1 - r,bottom);
   g.curveTo(x1,bottom,x1,bottom - r);
   g.lineTo(x1,yr + r);
   g.curveTo(x1,yr,x1 - r,yr - slope * r);
   g.lineTo(x0 + r,yl + slope * r);
   g.curveTo(x0,yl,x0,yl + r);
   g.lineTo(x0,bottom - r);
   g.curveTo(x0,bottom,x0 + r,bottom);
   g.endFill();
}

function rbTick()
{
   if(this.full)
   {
      var k = (Math.sin(getTimer() / 180) + 1) / 2;
      this.filters = [new flash.filters.GlowFilter(MG_GOLD,0.5 + 0.4 * k,12 + 10 * k,12 + 10 * k,2,2)];
      this.wasFull = true;
   }
   else if(this.wasFull)
   {
      this.filters = mgShadow();
      this.wasFull = false;
   }
}

var rbSetBoost = setBoost;
setBoost = function(numBoost)
{
   var result = rbSetBoost.apply(_root,arguments);
   rbLayer.socket.full = numBoost >= 100;
   return result;
};

rbBuild();
// ===== End Sanctuary Race Boost Bar =====

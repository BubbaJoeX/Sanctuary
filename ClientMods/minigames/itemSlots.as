// ===== Sanctuary Race Item Slots =====
// Appended to itemSlots' main timeline (after minigames/mg_theme.as) by build_client_mods.py. The racing power-up
// tray (wndRaceItemSlots): its purple tray is repainted navy glass and the boost wedge's frame gold (RESKINS). Here
// the boost meter gets a navy glass trough under its fill, and the tray's socket a gold ring that lights up and
// breathes while you hold a power-up ready to use, so the next item reads at a glance. The item icons, their slide
// and the blue to red boost fill are the stock ones. The movie has no font, so nothing here is text.

// The tray's round socket on stage, and the boost frame texture's top left inside the boost clip (184 x 55).
var IS_SOCKET_X = 119.65;
var IS_SOCKET_Y = 85.4;
var IS_SOCKET_R = 29;
var IS_FRAME_X = -85.75;
var IS_FRAME_Y = -29.65;

var isRing;

function isBuild()
{
   var t = mgUnder(boost,"sanctuaryTrack");
   t._x = IS_FRAME_X;
   t._y = IS_FRAME_Y;
   isWedge(t,7,180,50,31,4,MG_NAVY,85);
   t.lineStyle(2,0x050A24,50);
   t.moveTo(12,30.5);
   t.lineTo(176,5);

   // Over the tray, under the items (which the stock code attaches on top of everything).
   isRing = mgBelow(boost,"sanctuaryRing");
   isRing._x = IS_SOCKET_X;
   isRing._y = IS_SOCKET_Y;
   isRing.lineStyle(3,MG_GOLD,100);
   mgCircle(isRing,0,0,IS_SOCKET_R);
   isRing.lineStyle(1,MG_GOLD_RIM,80);
   mgCircle(isRing,0,0,IS_SOCKET_R - 3);
   isRing.active = false;
   isRing._alpha = 45;
   isRing.onEnterFrame = isTick;
}

// A wedge: from x0 to x1, flat along the bottom, its top rising from yl (left) to yr (right), corners rounded.
function isWedge(g, x0, x1, bottom, yl, yr, color, alpha)
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

// Lit while an item sits in the use slot (slot 1 of the stock holders).
function isTick()
{
   var active = false;
   var i;
   for(i = 0; i < holders.length; i++)
   {
      if(holders[i].slot == 1 && holders[i]._parent != undefined)
      {
         active = true;
      }
   }
   if(active)
   {
      var k = (Math.sin(getTimer() / 200) + 1) / 2;
      this._alpha = 100;
      this.filters = [new flash.filters.GlowFilter(MG_GOLD,0.55 + 0.35 * k,10 + 10 * k,10 + 10 * k,2,2)];
   }
   else if(this.active)
   {
      this._alpha = 45;
      this.filters = [];
   }
   this.active = active;
}

isBuild();
// ===== End Sanctuary Race Item Slots =====

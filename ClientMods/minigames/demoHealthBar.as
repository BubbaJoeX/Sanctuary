// ===== Sanctuary Derby Health Bar =====
// Appended to demoHealthBar's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The derby car's
// damage and boost meters (wndDemoHealthBar) on a navy glass card: the purple bar outline and boost wedge are
// repainted gold and the health bar's silver glass blue (RESKINS), the boost wedge gets a navy trough under its fill,
// and "Damage" and "Boost" are white with the dark outline. The red damage and blue to red boost fills are stock.

var DH_W = 190;
var DH_H = 111;
// The boost frame texture's top left inside the boost clip (184 x 55).
var DH_FRAME_X = -85.75;
var DH_FRAME_Y = -29.65;

function dhBuild()
{
   var card = mgUnder(_root,"sanctuaryHealthBar");
   mgGradientRect(card,0,0,DH_W,DH_H,18,0x2E4FB0,0x141F55,85,MG_RIM,70,1.5);
   mgGloss(card,8,2,DH_W - 16,30,12,22);
   card.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];

   var t = mgUnder(boost,"sanctuaryTrack");
   t._x = DH_FRAME_X;
   t._y = DH_FRAME_Y;
   dhWedge(t,7,180,50,31,4,MG_NAVY,85);
   t.lineStyle(2,0x050A24,50);
   t.moveTo(12,30.5);
   t.lineTo(176,5);

   var name;
   for(name in _root)
   {
      if(_root[name] instanceof TextField)
      {
         mgLight(_root[name],0xFFFFFF);
      }
   }
}

// A wedge: from x0 to x1, flat along the bottom, its top rising from yl (left) to yr (right), corners rounded.
function dhWedge(g, x0, x1, bottom, yl, yr, color, alpha)
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

dhBuild();
// ===== End Sanctuary Derby Health Bar =====

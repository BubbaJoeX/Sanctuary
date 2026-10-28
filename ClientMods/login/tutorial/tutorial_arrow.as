// ===== Sanctuary Tutorial Arrow =====
// Appended to tutorial_arrow's main timeline by build_client_mods.py.
// The bouncing arrow tutorials point at things with (TutorialArrow.lua), in the Main Menu's palette: the green
// arrow turns gold (its own shading kept) with a soft gold glow, and its hint box becomes a navy glass callout
// with a gold rim and outlined white text. The callout follows the stock box, which setAndPlaceText moves and
// shows or hides; the box itself stays, invisible.

var TA_RIM = 0x9DB8FF;
var TA_GOLD = 0xFFC531;

function taRoundRect(mc, x, y, w, h, r)
{
   mc.moveTo(x + r,y);
   mc.lineTo(x + w - r,y);
   mc.curveTo(x + w,y,x + w,y + r);
   mc.lineTo(x + w,y + h - r);
   mc.curveTo(x + w,y + h,x + w - r,y + h);
   mc.lineTo(x + r,y + h);
   mc.curveTo(x,y + h,x,y + h - r);
   mc.lineTo(x,y + r);
   mc.curveTo(x,y,x + r,y);
}

function taSparkle(mc, x, y, r, alpha)
{
   mc.lineStyle();
   mc.beginFill(0xFFFFFF,alpha);
   mc.moveTo(x,y - r * 2);
   mc.curveTo(x,y,x + r * 2,y);
   mc.curveTo(x,y,x,y + r * 2);
   mc.curveTo(x,y,x - r * 2,y);
   mc.curveTo(x,y,x,y - r * 2);
   mc.endFill();
}

var taCallout;

// The callout, over the stock box's bounds.
function taDrawCallout()
{
   var c = taCallout;
   c.clear();
   c._visible = textBackground._visible;
   if(!c._visible)
   {
      return;
   }
   var b = textBackground.getBounds(_root);
   var w = b.xMax - b.xMin;
   var h = b.yMax - b.yMin;
   c.lineStyle(2,TA_GOLD,90);
   c.beginGradientFill("linear",[0x2E4FB0,0x141F55],[96,96],[0,255],{matrixType:"box",x:b.xMin,y:b.yMin,w:w,h:h,r:Math.PI / 2});
   taRoundRect(c,b.xMin,b.yMin,w,h,18);
   c.endFill();
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[18,0],[0,255],{matrixType:"box",x:b.xMin,y:b.yMin + 2,w:w,h:h * 0.4,r:Math.PI / 2});
   taRoundRect(c,b.xMin + 6,b.yMin + 2,w - 12,h * 0.4,14);
   c.endFill();
   taSparkle(c,b.xMax - 14,b.yMin + 12,2,70);
   taSparkle(c,b.xMax - 26,b.yMin + 20,1,50);
}

function taBuild()
{
   taCallout = _root.createEmptyMovieClip("sanctuaryCallout",-16384);
   taCallout.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,10,10,1,2)];
   textBackground._alpha = 0;

   var f = tutorialText.getTextFormat();
   f.color = 0xFFFFFF;
   tutorialText.setTextFormat(f);
   tutorialText.setNewTextFormat(f);
   tutorialText.filters = [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];

   // Gold, keeping the arrow's shading (Color multiplies and offsets each channel).
   new Color(container.arrowClip).setTransform({ra:30,rb:200,ga:40,gb:120,ba:10,bb:20,aa:100,ab:0});
   container.arrowClip.filters = [new flash.filters.GlowFilter(TA_GOLD,0.6,16,16,2,2),new flash.filters.GlowFilter(0x0A1030,0.6,3,3,3,2)];
   taDrawCallout();
}

var taSetAndPlaceText = setAndPlaceText;
setAndPlaceText = function(str, dir)
{
   taSetAndPlaceText(str,dir);
   taDrawCallout();
};

taBuild();
// ===== End Sanctuary Tutorial Arrow =====

// ===== Sanctuary bug report =====
// Appended to bugsubmission's main timeline by build_client_mods.py.
// Redraws the bug report wizard (wndBugSubmission, the system menu's Report a Bug) as a card in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass frame with sparkles, the step's title in gold over a gold
// hairline, the step's list or text box in a recess, the back arrow on a glass bar, and the close button as the gold
// circle (drawn into a free depth inside the stock button, over its beige art and under its hidden X icons, so its
// click still hides the window). The wizard's steps are stock: they already draw white text on blue.

var BS_GOLD = 0xFFC531;
var BS_RIM = 0x9DB8FF;
var BS_INK = 0x3A2400;
// An FRButton holds HOVER, UP and DOWN at depths 1, 3 and 5 and its icons at 7 and 9.
var BS_DISC_DEPTH = 6 - 16384;

var bsFrame;

// --- drawing helpers -------------------------------------------------------------------

function bsRoundRect(mc, x, y, w, h, r)
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

function bsGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
{
   if(rim != undefined)
   {
      mc.lineStyle(rimWidth,rim,rimAlpha);
   }
   else
   {
      mc.lineStyle();
   }
   mc.beginGradientFill("linear",[top,bottom],[alpha,alpha],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   bsRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function bsGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   bsRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function bsCircle(mc, x, y, r)
{
   var k = Math.tan(Math.PI / 8);
   var s = Math.sin(Math.PI / 4);
   mc.moveTo(x + r,y);
   mc.curveTo(x + r,y + k * r,x + s * r,y + s * r);
   mc.curveTo(x + k * r,y + r,x,y + r);
   mc.curveTo(x - k * r,y + r,x - s * r,y + s * r);
   mc.curveTo(x - r,y + k * r,x - r,y);
   mc.curveTo(x - r,y - k * r,x - s * r,y - s * r);
   mc.curveTo(x - k * r,y - r,x,y - r);
   mc.curveTo(x + k * r,y - r,x + s * r,y - s * r);
   mc.curveTo(x + r,y - k * r,x + r,y);
}

function bsSparkle(mc, x, y, r, alpha)
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

function bsOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// --- frame -----------------------------------------------------------------------------

function bsBuildFrame()
{
   var b = bgClip.getBounds(_root);
   var x = b.xMax > b.xMin ? Math.round(b.xMin) + 2 : 106;
   var y = b.yMax > b.yMin ? Math.round(b.yMin) + 4 : 20;
   var w = b.xMax > b.xMin ? Math.round(b.xMax - b.xMin) - 4 : 336;
   var h = b.yMax > b.yMin ? Math.round(b.yMax - b.yMin) - 8 : 560;
   bgClip._alpha = 0;
   var f = bsFrame = _root.createEmptyMovieClip("sanctuaryBugReport",-16384);
   var bg = f.createEmptyMovieClip("bg",1);
   bsGradientRect(bg,x,y,w,h,28,0x2E4FB0,0x141F55,100,BS_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:x - 140,y:y - 240,w:w + 160,h:480,r:0});
   bsRoundRect(bg,x + 2,y + 2,w - 4,h - 4,26);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   bsRoundRect(bg,x + 4,y + 4,w - 8,52,24);
   bg.endFill();
   var s = 6464;
   var i;
   for(i = 0; i < 16; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + 20 + s % (w - 40);
      s = (s * 1103515245 + 12345) % 2147483648;
      bsSparkle(bg,sx,y + 10 + s % 40,1 + s % 2,20 + s % 45);
   }
   bg.beginGradientFill("linear",[BS_GOLD,BS_GOLD,BS_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:x + 30,y:0,w:w - 60,h:2,r:0});
   bsRoundRect(bg,x + 30,y + 58,w - 60,2,1);
   bg.endFill();

   // The steps' recess (the area theMask shows) and the bar under it with the back arrow.
   var m = theMask.getBounds(_root);
   if(m.xMax > m.xMin)
   {
      bg.lineStyle(1,BS_RIM,25);
      bg.beginFill(0x0A1440,40);
      bsRoundRect(bg,m.xMin - 4,m.yMin - 4,m.xMax - m.xMin + 8,m.yMax - m.yMin + 8,18);
      bg.endFill();
      bsGradientRect(bg,m.xMin - 4,m.yMax + 8,m.xMax - m.xMin + 8,40,20,0x284A9E,0x162660,100,BS_RIM,45,1.5);
      bsGloss(bg,m.xMin + 6,m.yMax + 9,m.xMax - m.xMin - 12,16,8,18);
   }
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   mainTitle.textColor = 0xFFE08A;
   mainTitle.filters = bsOutline();
   bsBuildClose();
}

// --- close -----------------------------------------------------------------------------

// The gold circle with a dark X, in the button's own coordinates (its art is 77x72, shown at about 65%).
function bsDrawClose(over)
{
   var c = frBut.bsDisc;
   c.clear();
   c.lineStyle(2,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:12,y:10,w:54,h:54,r:Math.PI / 2});
   bsCircle(c,39,37,27);
   c.endFill();
   bsGloss(c,22,13,34,18,8,50);
   c.lineStyle(6,BS_INK,100);
   c.moveTo(30,28);
   c.lineTo(48,46);
   c.moveTo(48,28);
   c.lineTo(30,46);
   c.filters = over ? [new flash.filters.GlowFilter(BS_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.6,8,8,1,2)];
}

function bsBuildClose()
{
   frBut.bsDisc = frBut.createEmptyMovieClip("bsDisc",BS_DISC_DEPTH);
   frBut.ICON_UP._alpha = 0;
   frBut.ICON_HOVER._alpha = 0;
   bsDrawClose(false);
   // FRButton broadcasts its states to listeners (the stock one hides the window on release).
   var lo = new Object();
   lo.state = function(action)
   {
      if(action == "rollover" || action == "dragover")
      {
         bsDrawClose(true);
      }
      else if(action == "rollout" || action == "dragout" || action == "releaseoutside")
      {
         bsDrawClose(false);
      }
   };
   frBut.addListener(lo);
}

bsBuildFrame();
// ===== End Sanctuary bug report =====

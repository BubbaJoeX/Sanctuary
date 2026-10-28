// ===== Sanctuary New Player FTE =====
// Appended to NewPlayerFTEWindow's main timeline by build_client_mods.py.
// The first "how to move and click" card a new player sees, in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass panel the size of the stock one with a header band,
// sparkles and the title in gold, outlined white hints, and the close button as the gold circle with a dark X.
// The keyboard, mouse and picture art stay as they are. The stock background and button art stay as invisible
// hit areas (the button still closes the card through the window's own close()).

var NP_RIM = 0x9DB8FF;
var NP_GOLD = 0xFFC531;
var NP_INK = 0x3A2400;

// --- drawing helpers -------------------------------------------------------------------

function npRoundRect(mc, x, y, w, h, r)
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

function npCircle(mc, x, y, r)
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

function npSparkle(mc, x, y, r, alpha)
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

function npOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function npRecolor(t, color)
{
   var f = new TextFormat();
   f.color = color;
   t.setTextFormat(f);
   t.setNewTextFormat(f);
   t.filters = npOutline();
}

// --- close button ----------------------------------------------------------------------

// Drawn inside the (scaled) stock button, so it moves and fades with it; r is on screen.
function npDrawClose(btn, over)
{
   var c = btn.sanctuaryClose;
   var r = 17 * 100 / btn._xscale;
   c.clear();
   c.lineStyle(2 * 100 / btn._xscale,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   npCircle(c,0,0,r);
   c.endFill();
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[50,0],[0,255],{matrixType:"box",x:- r * 0.65,y:- r * 0.85,w:r * 1.3,h:r * 0.65,r:Math.PI / 2});
   npRoundRect(c,- r * 0.65,- r * 0.85,r * 1.3,r * 0.65,r * 0.3);
   c.endFill();
   var k = r * 0.36;
   c.lineStyle(r * 0.24,NP_INK,100);
   c.moveTo(- k,- k);
   c.lineTo(k,k);
   c.moveTo(k,- k);
   c.lineTo(- k,k);
   c._xscale = c._yscale = over ? 112 : 100;
   c.filters = over ? [new flash.filters.GlowFilter(NP_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

function npStyleClose(btn)
{
   btn.UP._alpha = btn.HOVER._alpha = btn.DOWN._alpha = btn.ICON_UP._alpha = btn.ICON_HOVER._alpha = 0;
   var b = btn.UP.getBounds(btn);
   btn.sanctuaryClose = btn.createEmptyMovieClip("sanctuaryClose",-16384);
   btn.sanctuaryClose._x = b.xMax > b.xMin ? (b.xMin + b.xMax) / 2 : 40;
   btn.sanctuaryClose._y = b.yMax > b.yMin ? (b.yMin + b.yMax) / 2 : 40;
   npDrawClose(btn,false);
}

var npUpdateState = fr.buttons.FRButtonScalable.prototype.updateState;
fr.buttons.FRButtonScalable.prototype.updateState = function(mode)
{
   npUpdateState.apply(this,arguments);
   if(this.sanctuaryClose)
   {
      npDrawClose(this,mode == 2 || mode == 3);
   }
};

// --- panel -----------------------------------------------------------------------------

function npStyleTexts()
{
   var s = window_mc.screen1_mc;
   npRecolor(s.m_WindowTitle,0xFFE08A);
   var names = ["tfMouse","tfSpacebar","tfLeftClick","tfSpaceLabel","m_Text"];
   var i;
   for(i = 0; i < names.length; i++)
   {
      s[names[i]].filters = npOutline();
   }
   window_mc.screen2_mc.title1_txt.filters = npOutline();
}

function npBuild()
{
   var w = window_mc;
   var b = w.background.getBounds(w);
   w.background._alpha = 0;
   var p = w.createEmptyMovieClip("sanctuaryPanel",-16384);
   var x = b.xMin;
   var y = b.yMin;
   var pw = b.xMax - b.xMin;
   var ph = b.yMax - b.yMin;
   p.lineStyle(3,NP_RIM,80);
   p.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:pw,h:ph,r:Math.PI / 2});
   npRoundRect(p,x + 4,y + 4,pw - 8,ph - 8,30);
   p.endFill();
   p.lineStyle();
   p.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:x - 140,y:y - 220,w:520,h:460,r:0});
   npRoundRect(p,x + 6,y + 6,pw - 12,ph - 12,28);
   p.endFill();
   // The header band behind the title, a gold rule under it and sparkles.
   var t = w.screen1_mc.m_WindowTitle.getBounds(w);
   var hb = t.yMax + 8 - y;
   p.beginFill(0x0A1440,30);
   npRoundRect(p,x + 8,y + 8,pw - 16,hb - 8,26);
   p.endFill();
   p.beginGradientFill("linear",[NP_GOLD,NP_GOLD,NP_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:x + 50,y:0,w:pw - 100,h:2,r:0});
   npRoundRect(p,x + 50,y + hb + 2,pw - 100,2,1);
   p.endFill();
   var s = 3737;
   var i;
   for(i = 0; i < 16; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + 24 + s % (pw - 48);
      s = (s * 1103515245 + 12345) % 2147483648;
      npSparkle(p,sx,y + 14 + s % Math.max(10,hb - 20),1 + s % 3,20 + s % 45);
   }
   p.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   npStyleClose(w.m_CloseButton);
   npStyleTexts();
}

// The window sets its strings in onLoad; colour them again after.
var npSetStrings = fte.windows.NewPlayerFTEWindow.prototype.setStrings;
fte.windows.NewPlayerFTEWindow.prototype.setStrings = function()
{
   npSetStrings.apply(this,arguments);
   npStyleTexts();
};

npBuild();
// ===== End Sanctuary New Player FTE =====

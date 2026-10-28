// ===== Sanctuary Combat FTE =====
// Appended to CombatFTEWindow's main timeline by build_client_mods.py.
// The "How to rule in combat" card (number keys, arrow keys, the potion bar) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): the parchment becomes a navy glass panel the same size, with a header
// band, sparkles and the title in gold, glass recesses behind the three hints, the dark parchment text turned
// light and outlined, and the close button as the gold circle with a dark X. The key and potion art stays. The
// stock background and button art stay as invisible hit areas (the button still sends Hide(); the "show again"
// box still sends ToggleShowAgain()).

var CF_RIM = 0x9DB8FF;
var CF_GOLD = 0xFFC531;
var CF_INK = 0x3A2400;
var CF_SOFT = 0xC9D6FF;

// --- drawing helpers -------------------------------------------------------------------

function cfRoundRect(mc, x, y, w, h, r)
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

function cfCircle(mc, x, y, r)
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

function cfSparkle(mc, x, y, r, alpha)
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

function cfOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function cfRecolor(t, color)
{
   var f = new TextFormat();
   f.color = color;
   t.setTextFormat(f);
   t.setNewTextFormat(f);
   t.filters = cfOutline();
}

// --- close button ----------------------------------------------------------------------

// Drawn inside the (scaled) stock button, so it moves and fades with it; r is on screen.
function cfDrawClose(btn, over)
{
   var c = btn.sanctuaryClose;
   var r = 17 * 100 / btn._xscale;
   c.clear();
   c.lineStyle(2 * 100 / btn._xscale,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   cfCircle(c,0,0,r);
   c.endFill();
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[50,0],[0,255],{matrixType:"box",x:- r * 0.65,y:- r * 0.85,w:r * 1.3,h:r * 0.65,r:Math.PI / 2});
   cfRoundRect(c,- r * 0.65,- r * 0.85,r * 1.3,r * 0.65,r * 0.3);
   c.endFill();
   var k = r * 0.36;
   c.lineStyle(r * 0.24,CF_INK,100);
   c.moveTo(- k,- k);
   c.lineTo(k,k);
   c.moveTo(k,- k);
   c.lineTo(- k,k);
   c._xscale = c._yscale = over ? 112 : 100;
   c.filters = over ? [new flash.filters.GlowFilter(CF_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

function cfStyleClose(btn)
{
   btn.UP._alpha = btn.HOVER._alpha = btn.DOWN._alpha = btn.ICON_UP._alpha = btn.ICON_HOVER._alpha = 0;
   var b = btn.UP.getBounds(btn);
   btn.sanctuaryClose = btn.createEmptyMovieClip("sanctuaryClose",-16384);
   btn.sanctuaryClose._x = b.xMax > b.xMin ? (b.xMin + b.xMax) / 2 : 40;
   btn.sanctuaryClose._y = b.yMax > b.yMin ? (b.yMin + b.yMax) / 2 : 40;
   cfDrawClose(btn,false);
}

var cfUpdateState = fr.buttons.FRButtonScalable.prototype.updateState;
fr.buttons.FRButtonScalable.prototype.updateState = function(mode)
{
   cfUpdateState.apply(this,arguments);
   if(this.sanctuaryClose)
   {
      cfDrawClose(this,mode == 2 || mode == 3);
   }
};

// --- panel -----------------------------------------------------------------------------

function cfStyleTexts()
{
   var w = window;
   cfRecolor(w.m_WindowTitle,0xFFE08A);
   cfRecolor(w.m_NumberKeys.m_Attack,0xFFFFFF);
   cfRecolor(w.m_NumberKeys.m_SuperAttack,0xFFFFFF);
   cfRecolor(w.m_NumberKeys.m_PowerUp,0xFFFFFF);
   cfRecolor(w.m_ArrowKeys.m_Text,0xFFFFFF);
   cfRecolor(w.m_HealthPotions.m_Text,0xFFFFFF);
   cfRecolor(w.FrRadioBtn_ShowAgain.txt,CF_SOFT);
}

// A recess behind a hint (its art and text), in window coordinates.
function cfRecess(p, clip, pad)
{
   var b = clip.getBounds(window);
   if(b.xMax <= b.xMin)
   {
      return;
   }
   p.lineStyle(1,CF_RIM,25);
   p.beginFill(0x0A1440,38);
   cfRoundRect(p,b.xMin - pad,b.yMin - pad,b.xMax - b.xMin + pad * 2,b.yMax - b.yMin + pad * 2,18);
   p.endFill();
}

function cfBuild()
{
   var w = window;
   var b = w.background.getBounds(w);
   w.background._alpha = 0;
   var p = w.createEmptyMovieClip("sanctuaryPanel",-16384);
   var x = b.xMin;
   var y = b.yMin;
   var pw = b.xMax - b.xMin;
   var ph = b.yMax - b.yMin;
   p.lineStyle(3,CF_RIM,80);
   p.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:pw,h:ph,r:Math.PI / 2});
   cfRoundRect(p,x + 4,y + 4,pw - 8,ph - 8,30);
   p.endFill();
   p.lineStyle();
   p.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:x - 140,y:y - 220,w:500,h:440,r:0});
   cfRoundRect(p,x + 6,y + 6,pw - 12,ph - 12,28);
   p.endFill();
   var t = w.m_WindowTitle.getBounds(w);
   var hb = t.yMax + 8 - y;
   p.beginFill(0x0A1440,30);
   cfRoundRect(p,x + 8,y + 8,pw - 16,hb - 8,26);
   p.endFill();
   p.beginGradientFill("linear",[CF_GOLD,CF_GOLD,CF_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:x + 50,y:0,w:pw - 100,h:2,r:0});
   cfRoundRect(p,x + 50,y + hb + 2,pw - 100,2,1);
   p.endFill();
   var s = 3838;
   var i;
   for(i = 0; i < 14; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + 24 + s % (pw - 80);
      s = (s * 1103515245 + 12345) % 2147483648;
      cfSparkle(p,sx,y + 14 + s % Math.max(10,hb - 20),1 + s % 3,20 + s % 45);
   }
   cfRecess(p,w.m_NumberKeys,10);
   cfRecess(p,w.m_ArrowKeys,10);
   cfRecess(p,w.m_HealthPotions,10);
   p.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   cfStyleClose(w.m_CloseButton);
   cfStyleTexts();
}

// The window sets its strings in onLoad; colour them again after.
var cfSetStrings = fte.windows.CombatFTEWindow.prototype.setStrings;
fte.windows.CombatFTEWindow.prototype.setStrings = function()
{
   cfSetStrings.apply(this,arguments);
   cfStyleTexts();
};

cfBuild();
// ===== End Sanctuary Combat FTE =====

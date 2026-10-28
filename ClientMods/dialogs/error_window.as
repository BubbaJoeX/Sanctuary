// ===== Sanctuary error =====
// Appended to error's main timeline by build_client_mods.py.
// Redraws the error box (wndError: "Oops!" with Chatty peeking over it, or an icon) as a compact card in the Main
// Menu's style (ClientMods/MyStuffBrowser/main_menu.as): navy glass that grows with the message, the title in gold
// over a gold hairline, white outlined text, an icon in a frosted socket, and the close button as the gold circle
// with a dark X. The stock plate goes invisible; the circle is drawn into a free depth inside the stock button
// (between its state art and its icon), so its click (exitScreen) is untouched.

var ER_GOLD = 0xFFC531;
var ER_RIM = 0x9DB8FF;
var ER_INK = 0x3A2400;
// An FRButton holds DOWN, HOVER and UP at depths 1, 3 and 5 and its icons at 7 and 9.
var ER_DISC_DEPTH = 6 - 16384;

var erFrame;

// --- drawing helpers -------------------------------------------------------------------

function erRoundRect(mc, x, y, w, h, r)
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

function erGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   erRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function erGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   erRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function erCircle(mc, x, y, r)
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

function erOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Recolours a stock field (all of its text and what it's given next) and outlines it.
function erLightText(t, color)
{
   if(t == undefined)
   {
      return;
   }
   t.textColor = color;
   var f = t.getNewTextFormat();
   f.color = color;
   t.setNewTextFormat(f);
   t.filters = erOutline();
}

// --- card ------------------------------------------------------------------------------

function erDrawCard()
{
   var b = m_back.getBounds(_root);
   var g = erFrame.bg;
   var x = Math.round(b.xMin) + 2;
   var y = Math.round(b.yMin) + 2;
   var w = Math.round(b.xMax - b.xMin) - 4;
   var h = Math.round(b.yMax - b.yMin) - 4;
   g.clear();
   erGradientRect(g,x,y,w,h,26,0x2E4FB0,0x141F55,100,ER_RIM,80,3);
   g.lineStyle();
   g.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:x - 120,y:y - 200,w:w + 40,h:400,r:0});
   erRoundRect(g,x + 2,y + 2,w - 4,h - 4,24);
   g.endFill();
   // A gold hairline under the title.
   var ly = Math.round(label._y + 34);
   g.beginGradientFill("linear",[ER_GOLD,ER_GOLD,ER_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:x + 30,y:0,w:w - 60,h:2,r:0});
   erRoundRect(g,x + 30,ly,w - 60,2,1);
   g.endFill();
   g.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   // The icon's frosted socket (when there's an icon instead of Chatty).
   var s = erFrame.socket;
   s.clear();
   if(mcIcon._visible)
   {
      var ib = mcIcon.getBounds(_root);
      var cx = (ib.xMin + ib.xMax) / 2;
      var cy = (ib.yMin + ib.yMax) / 2;
      var r = Math.max(ib.xMax - ib.xMin,ib.yMax - ib.yMin) / 2 + 8;
      s.lineStyle(2,0xFFE08A,100);
      s.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:cx - r * 1.3,y:cy - r * 1.5,w:r * 2.6,h:r * 2.6,r:0});
      erCircle(s,cx,cy,r);
      s.endFill();
      s.filters = [new flash.filters.GlowFilter(ER_GOLD,0.45,12,12,2,2)];
   }
}

// --- close -----------------------------------------------------------------------------

// The gold circle with a dark X, in the button's own coordinates (its art is 77x72, shown at 70%).
function erDrawClose(over)
{
   var c = closeBtn.erDisc;
   c.clear();
   c.lineStyle(2,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:13,y:11,w:52,h:52,r:Math.PI / 2});
   erCircle(c,38.5,37,26);
   c.endFill();
   erGloss(c,22,14,33,17,8,50);
   c.lineStyle(6,ER_INK,100);
   c.moveTo(29.5,28);
   c.lineTo(47.5,46);
   c.moveTo(47.5,28);
   c.lineTo(29.5,46);
   c.filters = over ? [new flash.filters.GlowFilter(ER_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.6,8,8,1,2)];
}

function erBuildClose()
{
   closeBtn.erDisc = closeBtn.createEmptyMovieClip("erDisc",ER_DISC_DEPTH);
   closeBtn.ICON_UP._alpha = 0;
   closeBtn.ICON_HOVER._alpha = 0;
   erDrawClose(false);
   var rollOver = closeBtn.onRollOver;
   closeBtn.onRollOver = function()
   {
      rollOver.apply(this,arguments);
      erDrawClose(true);
   };
   var rollOut = closeBtn.onRollOut;
   closeBtn.onRollOut = function()
   {
      rollOut.apply(this,arguments);
      erDrawClose(false);
   };
   var dragOut = closeBtn.onDragOut;
   closeBtn.onDragOut = function()
   {
      dragOut.apply(this,arguments);
      erDrawClose(false);
   };
   var releaseOutside = closeBtn.onReleaseOutside;
   closeBtn.onReleaseOutside = function()
   {
      releaseOutside.apply(this,arguments);
      erDrawClose(false);
   };
}

// --- build -----------------------------------------------------------------------------

function erBuild()
{
   m_back._alpha = 0;
   erFrame = _root.createEmptyMovieClip("sanctuaryError",-16384);
   erFrame.bg = erFrame.createEmptyMovieClip("bg",1);
   erFrame.socket = erFrame.createEmptyMovieClip("socket",2);
   erLightText(label,0xFFE08A);
   erLightText(error,0xFFFFFF);
   erBuildClose();
   erDrawCard();
}

var erSetErrorText = setErrorText;
setErrorText = function(errorText, errorTitle, iconId)
{
   var r = erSetErrorText.apply(_root,arguments);
   erDrawCard();
   return r;
};

erBuild();
// ===== End Sanctuary error =====

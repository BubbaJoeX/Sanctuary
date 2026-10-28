// ===== Sanctuary What's New panel =====
// Appended to WhatsNewPanel's main timeline by build_client_mods.py.
// Redraws the What's New panel and the nudge window that share this movie (wndWhatsNewPanel, wndNudgeWindow: one
// big picture with a button, and thumbnails to flip between several) to match the home screen's What's New
// carousel (ClientMods/welcome/home.as): a navy glass panel with sparkles across its header and the title in gold,
// the picture in a rounded frame on a soft halo with a gold NEW! sticker, a recessed thumbnail strip, the action
// button turned gold (white label, like the home screen's gold buttons) and a gold close circle. The stock frame
// (an unnamed sprite at depth 1) and the picture's frame go invisible; the Chatty medallion over the frame is a
// plain shape, dropped at build time (REMOVED_DEPTHS). Every stock click and Lua callback is untouched.

var WN_W = 1024;
var WN_GOLD = 0xFFC531;
var WN_RIM = 0x9DB8FF;
var WN_SOFT = 0xC9D6FF;
var WN_INK = 0x3A2400;
// The panel (the stock frame's body) and its header band.
var WN_X = 110;
var WN_Y = 116;
var WN_PW = 804;
var WN_PH = 596;
var WN_HEADER_H = 70;

var wnFrame;

// --- drawing helpers -------------------------------------------------------------------

function wnRoundRect(mc, x, y, w, h, r)
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

function wnGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   wnRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function wnGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   wnRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function wnCircle(mc, x, y, r)
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

// Four-point sparkles (fixed seed, same every time), like the home screen's.
function wnStars(mc, count, seed, left, top, width, height)
{
   var s = seed;
   var i;
   for(i = 0; i < count; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = left + s % width;
      s = (s * 1103515245 + 12345) % 2147483648;
      var y = top + s % height;
      s = (s * 1103515245 + 12345) % 2147483648;
      var r = 1 + s % 3;
      mc.lineStyle();
      mc.beginFill(0xFFFFFF,25 + s % 50);
      mc.moveTo(x,y - r * 2);
      mc.curveTo(x,y,x + r * 2,y);
      mc.curveTo(x,y,x,y + r * 2);
      mc.curveTo(x,y,x - r * 2,y);
      mc.curveTo(x,y,x,y - r * 2);
      mc.endFill();
   }
}

function wnText(mc, name, txt, x, y, w, h, size, color)
{
   mc.createTextField(name,mc.getNextHighestDepth(),x,y,w,h);
   var t = mc[name];
   t.embedFonts = true;
   t.selectable = false;
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = "center";
   t.setNewTextFormat(f);
   t.text = txt;
   return t;
}

function wnOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function wnStrongOut(t, b, c, d)
{
   t = t / d - 1;
   return c * (t * t * t * t * t + 1) + b;
}

// --- frame -----------------------------------------------------------------------------

function wnBuildFrame()
{
   _root.getInstanceAtDepth(1 - 16384)._alpha = 0;
   var f = wnFrame = _root.createEmptyMovieClip("sanctuaryWhatsNew",-16384);
   var bg = f.createEmptyMovieClip("bg",1);
   wnGradientRect(bg,WN_X,WN_Y,WN_PW,WN_PH,28,0x2E4FB0,0x141F55,100,WN_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:WN_X - 200,y:WN_Y - 320,w:1000,h:760,r:0});
   wnRoundRect(bg,WN_X + 2,WN_Y + 2,WN_PW - 4,WN_PH - 4,26);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   wnRoundRect(bg,WN_X + 4,WN_Y + 4,WN_PW - 8,WN_HEADER_H,24);
   bg.endFill();
   bg.lineStyle(1,0xBFD3FF,25);
   bg.moveTo(WN_X + 24,WN_Y + WN_HEADER_H + 8);
   bg.lineTo(WN_X + WN_PW - 24,WN_Y + WN_HEADER_H + 8);
   wnStars(bg,40,2468,WN_X + 24,WN_Y + 10,WN_PW - 48,WN_HEADER_H - 14);
   // The bottom bar under the button and thumbnails.
   bg.lineStyle();
   bg.beginFill(0x0A1440,35);
   wnRoundRect(bg,WN_X + 14,WN_Y + WN_PH - 92,WN_PW - 28,78,20);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   title_txt.textColor = 0xFFE08A;
   title_txt.filters = wnOutline();

   wnBuildPicture();
   wnBuildButtons();
}

// The picture: its stock frame hidden, rounded corners (a mask), a soft halo behind and a gold NEW! sticker.
function wnBuildPicture()
{
   var holder = mainAd.holder;
   var b = holder.getBounds(_root);
   var x = b.xMax > b.xMin ? Math.round(b.xMin) : 156;
   var y = b.yMax > b.yMin ? Math.round(b.yMin) : 190;
   var w = b.xMax > b.xMin ? Math.round(b.xMax - b.xMin) : 700;
   var h = b.yMax > b.yMin ? Math.round(b.yMax - b.yMin) : 420;
   mainAd.getInstanceAtDepth(3 - 16384)._alpha = 0;

   var halo = wnFrame.createEmptyMovieClip("halo",2);
   halo.beginGradientFill("radial",[0xBFE0FF,0x5E8CF0,0x5E8CF0],[45,18,0],[0,150,255],{matrixType:"box",x:x - 60,y:y - 50,w:w + 120,h:h + 100,r:0});
   wnRoundRect(halo,x - 40,y - 30,w + 80,h + 60,40);
   halo.endFill();
   halo.lineStyle(3,0xFFF4C2,90);
   halo.beginFill(0x0A1440,60);
   wnRoundRect(halo,x - 6,y - 6,w + 12,h + 12,22);
   halo.endFill();
   halo.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,12,12,1,2)];

   var mask = mainAd.createEmptyMovieClip("wnMask",mainAd.getNextHighestDepth());
   var lb = holder.getBounds(mainAd);
   mask.beginFill(0,100);
   wnRoundRect(mask,lb.xMin,lb.yMin,lb.xMax - lb.xMin,lb.yMax - lb.yMin,18);
   mask.endFill();
   holder.setMask(mask);

   var sticker = _root.createEmptyMovieClip("sanctuaryNewSticker",_root.getNextHighestDepth());
   wnGradientRect(sticker,-38,-17,76,34,17,0xFFE066,0xE79A13,100,0xFFFFFF,100,2);
   wnText(sticker,"txt","NEW!",-38,-12,76,26,17,0x6A2C00);
   sticker._x = x + w - 24;
   sticker._y = y + 4;
   sticker._rotation = 12;
   sticker.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];
   sticker._xscale = sticker._yscale = 10;
   gs.TweenLite.to(sticker,0.8,{_xscale:100,_yscale:100,delay:0.2,ease:wnStrongOut});
}

// --- buttons and thumbnails ------------------------------------------------------------

function wnDrawClose(over)
{
   var c = wnFrame.close;
   c.clear();
   c.lineStyle(1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-17,y:-17,w:34,h:34,r:Math.PI / 2});
   wnCircle(c,0,0,17);
   c.endFill();
   wnGloss(c,-11,-14,22,11,5,50);
   c.lineStyle(4,WN_INK,100);
   c.moveTo(-6,-6);
   c.lineTo(6,6);
   c.moveTo(6,-6);
   c.lineTo(-6,6);
   c.filters = over ? [new flash.filters.GlowFilter(WN_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
   gs.TweenLite.to(c,over ? 0.4 : 0.25,{_xscale:over ? 112 : 100,_yscale:over ? 112 : 100,_rotation:over ? 90 : 0,ease:wnStrongOut});
}

function wnBuildButtons()
{
   // The close button: its state art hidden (still the hit area) over a gold circle in the header's corner.
   var exit = exitButton_mc;
   exit.up_mc._alpha = exit.over_mc._alpha = exit.down_mc._alpha = 0;
   var eb = exit.getBounds(_root);
   wnFrame.close = wnFrame.createEmptyMovieClip("close",3);
   wnFrame.close._x = eb.xMax > eb.xMin ? Math.round((eb.xMin + eb.xMax) / 2) : WN_X + WN_PW - 38;
   wnFrame.close._y = eb.yMax > eb.yMin ? Math.round((eb.yMin + eb.yMax) / 2) : WN_Y + 36;
   wnDrawClose(false);
   var lo = new Object();
   lo.onHoverOver = function(target)
   {
      wnDrawClose(true);
   };
   lo.onHoverOut = function(target)
   {
      wnDrawClose(false);
   };
   exit.addListener(lo);

   // The action button: its green plates turned gold by a colour offset (the white label stays white).
   new Color(go_mc).setTransform({ra:100,rb:120,ga:100,gb:0,ba:100,bb:20,aa:100,ab:0});
   go_mc.up_mc.textField_txt.filters = go_mc.over_mc.textField_txt.filters = go_mc.down_mc.textField_txt.filters = [new flash.filters.DropShadowFilter(1,90,0x6A2C00,0.9,2,2,2,2)];
   go_mc.filters = [new flash.filters.GlowFilter(WN_GOLD,0.45,14,14,2,2)];

   // The thumbnails' strip: a navy recess that follows its stock plate (hidden) as thumbnails are added.
   thumbnails_mc.backgroundMC._alpha = 0;
   thumbnails_mc.wnStrip = thumbnails_mc.createEmptyMovieClip("wnStrip",-16384);
   wnDrawStrip();
}

function wnDrawStrip()
{
   var t = thumbnails_mc;
   var g = t.wnStrip;
   var w = t.backgroundMC._width;
   var h = t.backgroundMC._height;
   g.clear();
   if(!(w > 0))
   {
      return;
   }
   g.lineStyle(1.5,WN_RIM,45);
   g.beginGradientFill("linear",[0x284A9E,0x162660],[100,100],[0,255],{matrixType:"box",x:0,y:0,w:w,h:h,r:Math.PI / 2});
   wnRoundRect(g,0,0,w,h,Math.min(20,h / 2));
   g.endFill();
   wnGloss(g,8,1,w - 16,h * 0.4,10,18);
}

var wnAddItem = _global.Thumbnails.prototype.addItem;
_global.Thumbnails.prototype.addItem = function(iconId, toolTipString)
{
   var r = wnAddItem.apply(this,arguments);
   wnDrawStrip();
   return r;
};

// The nudge window has no title; the panel's title strip stays, empty.
var wnSetWindowType = setWindowType_lua;
setWindowType_lua = function(type)
{
   var r = wnSetWindowType.apply(_root,arguments);
   title_txt.textColor = 0xFFE08A;
   return r;
};

wnBuildFrame();
// ===== End Sanctuary What's New panel =====

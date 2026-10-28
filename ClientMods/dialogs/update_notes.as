// ===== Sanctuary update notes =====
// Appended to UpdateNotes's main timeline by build_client_mods.py (it has no main timeline script, so FRAME_SCRIPTS
// gives it an empty one).
// Redraws the release notes window (wndUpdateNotes: shown on a first login after an update) to match the home
// screen's Patch Notes overlay (ClientMods/welcome/home.as): a navy glass panel with a cream-gold rim, the scroll
// icon (sanctuaryNotesScroll, TEXTURE_SYMBOLS) beside the stock title (outlined) over a gold hairline, the notes
// in a recess with white outlined text, and a gold close circle. The stock window art is one big bitmap shape
// inside the UpdateNotes clip, so the new panel is drawn into a free depth just above it (3, between the close
// button and the text area): the text area and title stay on top, and the stock close button under the panel still
// takes the click (the panel has no mouse handlers).

var UN_W = 800;
var UN_H = 670;
var UN_GOLD = 0xFFC531;
var UN_INK = 0x3A2400;
var UN_PANEL_DEPTH = 3 - 16384;

var unWin = _root.UpdateNotes;
var unPanel;

// --- drawing helpers -------------------------------------------------------------------

function unRoundRect(mc, x, y, w, h, r)
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

function unGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   unRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function unGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   unRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function unCircle(mc, x, y, r)
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
function unStars(mc, count, seed, left, top, width, height)
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

function unText(mc, name, txt, x, y, w, h, size, color, align)
{
   mc.createTextField(name,mc.getNextHighestDepth(),x,y,w,h);
   var t = mc[name];
   t.embedFonts = true;
   t.selectable = false;
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = align == undefined ? "center" : align;
   t.setNewTextFormat(f);
   t.text = txt;
   return t;
}

function unOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function unStrongOut(t, b, c, d)
{
   t = t / d - 1;
   return c * (t * t * t * t * t + 1) + b;
}

// --- panel -----------------------------------------------------------------------------

function unDrawClose(over)
{
   var c = unPanel.close;
   c.clear();
   c.lineStyle(1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-17,y:-17,w:34,h:34,r:Math.PI / 2});
   unCircle(c,0,0,17);
   c.endFill();
   unGloss(c,-11,-14,22,11,5,50);
   c.lineStyle(4,UN_INK,100);
   c.moveTo(-6,-6);
   c.lineTo(6,6);
   c.moveTo(6,-6);
   c.lineTo(-6,6);
   c.filters = over ? [new flash.filters.GlowFilter(UN_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
   gs.TweenLite.to(c,over ? 0.4 : 0.25,{_xscale:over ? 112 : 100,_yscale:over ? 112 : 100,_rotation:over ? 90 : 0,ease:unStrongOut});
}

function unBuild()
{
   var p = unPanel = unWin.createEmptyMovieClip("sanctuaryNotes",UN_PANEL_DEPTH);
   var bg = p.createEmptyMovieClip("bg",1);
   unGradientRect(bg,4,4,UN_W - 8,UN_H - 8,26,0x2D57C2,0x16296E,100,0xFFF4C2,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[30,0],[0,255],{matrixType:"box",x:-160,y:-280,w:760,h:640,r:0});
   unRoundRect(bg,6,6,UN_W - 12,UN_H - 12,24);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   unRoundRect(bg,8,8,UN_W - 16,68,22);
   bg.endFill();
   unStars(bg,30,4545,30,14,UN_W - 60,62);
   // The notes' recess, around the stock text area (and its scrollbar).
   var t = unWin.updateNotesText;
   var tb = t.getBounds(unWin);
   var rx = tb.xMax > tb.xMin ? Math.round(tb.xMin) - 14 : 76;
   var ry = tb.yMax > tb.yMin ? Math.round(tb.yMin) - 12 : 82;
   var rw = tb.xMax > tb.xMin ? Math.round(tb.xMax - tb.xMin) + 40 : 640;
   var rh = tb.yMax > tb.yMin ? Math.round(tb.yMax - tb.yMin) + 24 : 524;
   bg.lineStyle(1,0x9DB8FF,25);
   bg.beginFill(0x0A1440,40);
   unRoundRect(bg,rx,ry,rw,rh,20);
   bg.endFill();
   bg.lineStyle();
   bg.beginGradientFill("linear",[UN_GOLD,UN_GOLD,UN_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:40,y:0,w:UN_W - 80,h:2,r:0});
   unRoundRect(bg,40,78,UN_W - 80,2,1);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(8,90,0x050A24,0.7,24,24,1,2)];

   // The home screen's scroll beside the stock title.
   var icon = p.attachMovie("sanctuaryNotesScroll","icon",p.getNextHighestDepth());
   icon._width = icon._height = 58;
   icon._x = 112;
   icon._y = 12;
   unWin.windowTitle.filters = unOutline();

   // The gold close circle where the stock button is; it stays under, invisible, and keeps the click.
   var exit = unWin.exit_mc;
   var eb = exit.getBounds(unWin);
   p.close = p.createEmptyMovieClip("close",p.getNextHighestDepth());
   p.close._x = eb.xMax > eb.xMin ? Math.round((eb.xMin + eb.xMax) / 2) : UN_W - 40;
   p.close._y = eb.yMax > eb.yMin ? Math.round((eb.yMin + eb.yMax) / 2) : 40;
   unDrawClose(false);
   var lo = new Object();
   lo.over = function()
   {
      unDrawClose(true);
   };
   lo.out = function()
   {
      unDrawClose(false);
   };
   exit.addEventListener("rollOver",lo,"over");
   exit.addEventListener("rollOut",lo,"out");
   exit.addEventListener("releaseOutside",lo,"out");
}

// Lua sends the notes as html; the text goes white (and outlined) whatever colour the notes set.
var unSetNotes = _global.views.UpdateNotes.prototype._handleSetUpdateNotes;
_global.views.UpdateNotes.prototype._handleSetUpdateNotes = function(title, updateNotes, updateNumber)
{
   var r = unSetNotes.apply(this,arguments);
   var field = this.updateNotesText.textField;
   field.textColor = 0xFFFFFF;
   field.filters = unOutline();
   return r;
};

unBuild();
// ===== End Sanctuary update notes =====

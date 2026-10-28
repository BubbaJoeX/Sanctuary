// ===== Sanctuary comic book =====
// Appended to ComicBook's main timeline by build_client_mods.py.
// The comic reader (wndComicBook: story comics from items and the world) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as). Its night-sky backdrop already suits the navy theme and stays; the page
// gets a soft gold glow and shadow, the Back, Next and Teleport buttons are gold (their green plates are repainted at
// build time, RESKINS), their labels and the page text are outlined, and the close button is the gold circle (its
// stock art invisible, still the hit area, so Exit() is untouched).

var CB_GOLD = 0xFFC531;
var CB_INK = 0x3A2400;

// --- drawing helpers -------------------------------------------------------------------

function cbRoundRect(mc, x, y, w, h, r)
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

function cbGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   cbRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function cbCircle(mc, x, y, r)
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

function cbOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// --- close -----------------------------------------------------------------------------

// The gold circle, in the close button's own coordinates under its invisible state art.
function cbDrawClose(over)
{
   var c = exit_mc.cbDisc;
   var r = exit_mc.cbR;
   c.clear();
   c.lineStyle(2,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:exit_mc.cbX - r,y:exit_mc.cbY - r,w:2 * r,h:2 * r,r:Math.PI / 2});
   cbCircle(c,exit_mc.cbX,exit_mc.cbY,r);
   c.endFill();
   cbGloss(c,exit_mc.cbX - r * 0.65,exit_mc.cbY - r * 0.85,r * 1.3,r * 0.65,r * 0.3,50);
   var k = r * 0.36;
   c.lineStyle(Math.max(4,r * 0.24),CB_INK,100);
   c.moveTo(exit_mc.cbX - k,exit_mc.cbY - k);
   c.lineTo(exit_mc.cbX + k,exit_mc.cbY + k);
   c.moveTo(exit_mc.cbX + k,exit_mc.cbY - k);
   c.lineTo(exit_mc.cbX - k,exit_mc.cbY + k);
   c.filters = over ? [new flash.filters.GlowFilter(CB_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

function cbBuild()
{
   var b = exit_mc.up_mc.getBounds(exit_mc);
   exit_mc.cbX = b.xMax > b.xMin ? (b.xMin + b.xMax) / 2 : 38;
   exit_mc.cbY = b.yMax > b.yMin ? (b.yMin + b.yMax) / 2 : 36;
   exit_mc.cbR = b.xMax > b.xMin ? Math.round(Math.min(b.xMax - b.xMin,b.yMax - b.yMin) / 2) - 10 : 26;
   exit_mc.up_mc._alpha = exit_mc.over_mc._alpha = exit_mc.down_mc._alpha = 0;
   exit_mc.cbDisc = exit_mc.createEmptyMovieClip("cbDisc",-16384);
   cbDrawClose(false);
   var lo = new Object();
   lo.onHoverOver = function(target)
   {
      cbDrawClose(true);
   };
   lo.onHoverOut = function(target)
   {
      cbDrawClose(false);
   };
   exit_mc.addListener(lo);
   cbStyleComic();
}

// The comic's page and controls (the clip can be created later, when the first page arrives).
function cbStyleComic()
{
   var m = movie_mc;
   if(m == undefined || m.cbStyled)
   {
      return;
   }
   m.cbStyled = true;
   m.comicPageTransitioner_mc.filters = [new flash.filters.GlowFilter(CB_GOLD,0.35,16,16,2,2),new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   m.pageCount_txt.filters = cbOutline();
   m.callToAction_txt.filters = cbOutline();
   var buttons = [m.prevButton_mc,m.nextButton_mc,m.goButton_mc];
   var i;
   for(i = 0; i < buttons.length; i++)
   {
      buttons[i].filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
      buttons[i].up_mc.textField_txt.filters = buttons[i].over_mc.textField_txt.filters = buttons[i].down_mc.textField_txt.filters = [new flash.filters.DropShadowFilter(1,90,0x6A2C00,0.9,2,2,2,2)];
   }
}

var cbPopulate = populateComicPage_lua;
populateComicPage_lua = function(image_id, str, trans)
{
   var r = cbPopulate.apply(_root,arguments);
   cbStyleComic();
   return r;
};

cbBuild();
// ===== End Sanctuary comic book =====

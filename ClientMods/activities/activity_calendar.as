// ===== Sanctuary Activity Calendar =====
// Appended to activityCalendar's main timeline by build_client_mods.py.
// Redraws the Activity Calendar (wndActivityCalendar: a month of days, each with up to three activity icons, that
// opens onto a day's list of activities with a Go! button each) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as, collections/collections_browser.as): a navy glass frame with the month in
// gold, a gold close circle, gold chevron discs for the months and days, and a recessed bay where each day is a
// glass tile (its activities' icons in small frosted sockets; today rimmed in gold with a gold number; days outside
// the month recessed and empty) that lights up with a gold rim on hover. A day's list is a navy glass panel with its
// title in gold, each activity a glass card with its icon in a round frosted socket, its name, time and countdown,
// and Go! as a gold pill (dimmed until the activity starts), and a slim scrollbar with a gold thumb.
// The stock frame is one plain shape inside the window, so the calendar is moved up a depth and this frame takes its
// place just above it. Every stock clip keeps its role (clicks, tooltips, Lua's setDay/addMainItem/setTitle, the
// month and day navigation and the pop-open tween); the stock buttons stay where they are as invisible hit areas.

var ACAL_W = 825;
var ACAL_H = 668;
// The bay the days sit in, in the window's coordinates (the days span 70..758 x 168..634).
var ACAL_BAY = [60,124,708,526];
// The day list panel, in the day view's own coordinates (it sits at (-14, 128) and pops open from a day).
var ACAL_LIST = [66,0,724,530];
var ACAL_CLOSE_X = 783;
var ACAL_CLOSE_Y = 46;
// Solid, so the day tiles' bases (which cover the stock tiles' outlines) blend into it.
var ACAL_BAY_COLOR = 0x101D55;
var ACAL_GOLD = 0xFFC531;
var ACAL_RIM = 0x9DB8FF;
var ACAL_SOFT = 0xC9D6FF;
var ACAL_INK = 0x3A2400;

var acalRoot;
var acalMain;

// --- drawing helpers -------------------------------------------------------------------

function acalRoundRect(mc, x, y, w, h, r)
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

function acalGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   acalRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function acalCircle(mc, x, y, r)
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

// The frosted socket behind stock art (the icons are made for light plates).
function acalSocket(mc, x, y, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.25,y:y - r * 1.4,w:r * 2.5,h:r * 2.5,r:0});
   acalCircle(mc,x,y,r);
   mc.endFill();
}

function acalGoldFill(mc, x, y, w, h, over)
{
   mc.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
}

function acalSparkle(mc, x, y, r, alpha)
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

function acalOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function acalShadow()
{
   return [new flash.filters.DropShadowFilter(1,90,0x0A1030,0.9,2,2,1,1)];
}

// Penner's elastic ease out; this movie only carries mx.transitions.easing.Strong.
function acalElasticOut(t, b, c, d)
{
   if(t == 0)
   {
      return b;
   }
   t /= d;
   if(t == 1)
   {
      return b + c;
   }
   var p = d * 0.3;
   return c * Math.pow(2,-10 * t) * Math.sin((t * d - p / 4) * (2 * Math.PI) / p) + c + b;
}

// Colours a stock field: its text now and whatever text it's given later (the stock fitters keep the format).
function acalColor(t, color)
{
   if(t == undefined || t.textColor == color)
   {
      return;
   }
   t.textColor = color;
   var f = t.getNewTextFormat();
   f.color = color;
   t.setNewTextFormat(f);
}

// Wraps a clip's mouse handlers so they also call over(clip, true/false); the stock handler runs first.
function acalWrapHover(mc, over)
{
   if(mc.sanctuaryHover)
   {
      return;
   }
   mc.sanctuaryHover = true;
   var rollOver = mc.onRollOver;
   var rollOut = mc.onRollOut;
   mc.onRollOver = mc.onDragOver = function()
   {
      var r = rollOver.apply(this,arguments);
      over(this,true);
      return r;
   };
   mc.onRollOut = mc.onDragOut = function()
   {
      var r = rollOut.apply(this,arguments);
      over(this,false);
      return r;
   };
   var release = mc.onRelease;
   if(release != undefined)
   {
      mc.onRelease = function()
      {
         var r = release.apply(this,arguments);
         // The stock release puts the clip back as it was; a click that leaves it disabled ends the hover.
         if(this.enabled == false)
         {
            over(this,false);
         }
         return r;
      };
   }
}

// --- frame -----------------------------------------------------------------------------

function acalDrawFrame(bg)
{
   acalGradientRect(bg,2,2,ACAL_W - 4,ACAL_H - 4,30,0x2E4FB0,0x141F55,100,ACAL_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:-160,y:-320,w:900,h:700,r:0});
   acalRoundRect(bg,4,4,ACAL_W - 8,ACAL_H - 8,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   acalRoundRect(bg,14,14,ACAL_W - 28,64,26);
   bg.endFill();

   // Sparkles in the header (fixed seed, same every time).
   var s = 3131;
   var i;
   for(i = 0; i < 28; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 34 + s % (ACAL_W - 110);
      s = (s * 1103515245 + 12345) % 2147483648;
      acalSparkle(bg,x,20 + s % 52,1 + s % 3,25 + s % 50);
   }

   // A gold rule under the header, and the bay with the weekdays' strip.
   bg.beginGradientFill("linear",[ACAL_GOLD,ACAL_GOLD,ACAL_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:60,y:0,w:ACAL_W - 120,h:2,r:0});
   acalRoundRect(bg,60,86,ACAL_W - 120,2,1);
   bg.endFill();
   acalGradientRect(bg,ACAL_BAY[0],ACAL_BAY[1],ACAL_BAY[2],ACAL_BAY[3],22,ACAL_BAY_COLOR,ACAL_BAY_COLOR,100,ACAL_RIM,25,1);
   acalGradientRect(bg,ACAL_BAY[0] + 8,ACAL_BAY[1] + 8,ACAL_BAY[2] - 16,26,13,0x2B4FA8,0x17286A,100,ACAL_RIM,30,1);
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
}

// --- round gold buttons ----------------------------------------------------------------

// The close circle: an X, or a chevron pointing left (dir -1) or right (dir 1) for the month and day arrows.
function acalMakeDisc(parent, name, dir, r)
{
   var d = parent.createEmptyMovieClip(name,parent.getNextHighestDepth());
   d.dir = dir;
   d.r = r;
   d.draw = function(over)
   {
      var r = this.r;
      this.clear();
      this.lineStyle(2,0xFFF3C4,100);
      acalGoldFill(this,- r,- r,r * 2,r * 2,over);
      acalCircle(this,0,0,r);
      this.endFill();
      this.lineStyle(4,ACAL_INK,100);
      if(this.dir == 0)
      {
         this.moveTo(-6,-6);
         this.lineTo(6,6);
         this.moveTo(6,-6);
         this.lineTo(-6,6);
      }
      else
      {
         var k = this.dir * r * 0.24;
         this.moveTo(- k,- r * 0.42);
         this.lineTo(k,0);
         this.lineTo(- k,r * 0.42);
      }
      this.filters = over ? [new flash.filters.GlowFilter(ACAL_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,6,6,1,2)];
   };
   d.draw(false);
   return d;
}

function acalDiscHover(d, over)
{
   if(d.over == over)
   {
      return;
   }
   d.over = over;
   d.draw(over);
   if(d.dir == 0)
   {
      gs.TweenLite.to(d,0.6,{_rotation:over ? 90 : 0,_xscale:over ? 112 : 100,_yscale:over ? 112 : 100,ease:acalElasticOut});
   }
   else
   {
      gs.TweenLite.to(d,0.5,{_x:d.homeX + (over ? d.dir * 4 : 0),_xscale:over ? 112 : 100,_yscale:over ? 112 : 100,ease:acalElasticOut});
   }
}

// A disc under a stock arrow (invisible, still the hit area); dimmed while the stock disables the arrow.
function acalSyncDisc(d, stock, show)
{
   stock._alpha = 0;
   d._visible = show && stock._visible;
   d._alpha = stock.enabled == false ? 35 : 100;
   if(stock.enabled == false && d.over)
   {
      acalDiscHover(d,false);
   }
}

// Moves a stock clip so its bounds center on (x, y) in its parent.
function acalCenterOn(mc, x, y)
{
   var b = mc.getBounds(mc._parent);
   mc._x += x - (b.xMin + b.xMax) / 2;
   mc._y += y - (b.yMin + b.yMax) / 2;
}

// --- day tiles -------------------------------------------------------------------------

// A tile over a stock day (its plates and outline are plain shapes): a base in the bay's colour that covers them,
// the glass card, and frosted sockets for the activity icons. The number and icons are raised above it.
function acalSkinDay(cell)
{
   var skin = cell.createEmptyMovieClip("sanctuaryTile",100);
   skin.card = skin.createEmptyMovieClip("card",1);
   cell.mcTxtDay.swapDepths(101);
   cell.icon2.swapDepths(102);
   cell.icon3.swapDepths(103);
   cell.icon1.swapDepths(104);
   cell.mcHighlight._alpha = cell.mcHighlightSparkle._alpha = 0;
   cell.mcTxtDay.txt.filters = acalOutline();
   cell.sanctuaryTile = skin;
   acalWrapHover(cell,acalDayHover);
}

function acalDayHover(cell, over)
{
   cell.sanctuaryOver = over && cell.enabled != false;
   acalDrawDay(cell);
}

function acalDrawDay(cell)
{
   var inactive = cell.mcBlank._alpha >= 50;
   var today = !inactive && cell.mcHighlight._visible == true;
   var over = !inactive && cell.sanctuaryOver == true;
   var icons = [cell.icon1,cell.icon2,cell.icon3];
   // frSetIconByIndex shows or hides the art inside each icon (none for an empty slot).
   var shown = "";
   var i;
   for(i = 0; i < 3; i++)
   {
      shown += icons[i]._visible && icons[i].icon._visible && !inactive ? "1" : "0";
   }
   var key = inactive + "," + today + "," + over + "," + shown;
   if(key == cell.sanctuaryKey)
   {
      return;
   }
   cell.sanctuaryKey = key;
   var skin = cell.sanctuaryTile;
   skin.clear();
   skin.beginFill(ACAL_BAY_COLOR,100);
   acalRoundRect(skin,0,-1,100,83,0);
   skin.endFill();
   var g = skin.card;
   g.clear();
   if(inactive)
   {
      g.lineStyle(1,ACAL_RIM,12);
      g.beginFill(0x0A1440,55);
      acalRoundRect(g,4,2,92,74,12);
      g.endFill();
      g.filters = [];
      return;
   }
   if(over)
   {
      acalGradientRect(g,3,1,94,76,13,0x3F66D6,0x1D307A,100,0xFFE08A,100,2);
   }
   else if(today)
   {
      acalGradientRect(g,3,1,94,76,13,0x2B4FA8,0x17286A,100,ACAL_GOLD,100,2.5);
   }
   else
   {
      acalGradientRect(g,3,1,94,76,13,0x284A9E,0x162660,100,ACAL_RIM,40,1.5);
   }
   g.lineStyle();
   if(today)
   {
      // A gold wash from the top left.
      g.beginGradientFill("linear",[ACAL_GOLD,ACAL_GOLD],[45,0],[0,200],{matrixType:"box",x:3,y:1,w:94,h:76,r:Math.PI / 4});
      acalRoundRect(g,4,2,92,74,12);
      g.endFill();
   }
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[over ? 22 : 14,0],[0,255],{matrixType:"box",x:0,y:2,w:10,h:26,r:Math.PI / 2});
   acalRoundRect(g,6,3,88,26,11);
   g.endFill();
   for(i = 0; i < 3; i++)
   {
      if(shown.charAt(i) == "1")
      {
         acalSocket(g,icons[i]._x,icons[i]._y,14,0xFFFFFF,85,1);
      }
   }
   if(over)
   {
      g.filters = [new flash.filters.GlowFilter(ACAL_GOLD,0.55,10,10,2,2)];
   }
   else if(today)
   {
      g.filters = [new flash.filters.GlowFilter(ACAL_GOLD,0.4,8,8,2,2)];
   }
   else
   {
      g.filters = [];
   }
   acalColor(cell.mcTxtDay.txt,today ? 0xFFE08A : 0xFFFFFF);
}

function acalSyncDays(holder)
{
   var z;
   for(z = 1; z <= 42; z++)
   {
      var cell = holder["day_" + z];
      if(cell == undefined)
      {
         continue;
      }
      if(cell.sanctuaryTile == undefined)
      {
         acalSkinDay(cell);
      }
      acalDrawDay(cell);
   }
}

// --- the day's activities --------------------------------------------------------------

function acalDrawItem(item, over)
{
   var g = item.sanctuaryCard;
   g.clear();
   if(over)
   {
      acalGradientRect(g,42,0,632,108,20,0x3F66D6,0x1D307A,100,0xFFE08A,100,2);
   }
   else
   {
      acalGradientRect(g,42,0,632,108,20,0x284A9E,0x162660,100,ACAL_RIM,40,1.5);
   }
   g.lineStyle();
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[16,0],[0,255],{matrixType:"box",x:0,y:3,w:10,h:40,r:Math.PI / 2});
   acalRoundRect(g,45,3,626,40,17);
   g.endFill();
   // The icon's round frosted socket, with a soft halo.
   g.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[over ? 55 : 35,0],[0,255],{matrixType:"box",x:50,y:3,w:104,h:104,r:0});
   acalCircle(g,102,55,52);
   g.endFill();
   g.beginFill(0x050A24,30);
   acalCircle(g,102,58,45);
   g.endFill();
   acalSocket(g,102,55,44,over ? 0xFFE08A : 0xFFFFFF,90,2);
   g.filters = over ? [new flash.filters.GlowFilter(ACAL_GOLD,0.55,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
}

function acalItemHover(fg, over)
{
   acalDrawItem(fg._parent,over);
}

// The Go! button: a gold pill under the stock text (its green plate is hidden). An activity that hasn't started
// is dimmed by the stock's colour transform, which dims the pill with it.
function acalStyleGo(button)
{
   button.UP._alpha = 0;
   var p = button.createEmptyMovieClip("sanctuaryPill",-16384);
   p.lineStyle(2,0xFFF3C4,100);
   acalGoldFill(p,2,2,106,46,false);
   acalRoundRect(p,2,2,106,46,23);
   p.endFill();
   p.lineStyle();
   p.beginFill(0xFFFFFF,30);
   acalRoundRect(p,12,6,86,12,6);
   p.endFill();
   p.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,8,8,1,2)];
   acalColor(button.txt,ACAL_INK);
}

function acalStyleItem(item)
{
   if(item == undefined || item.sanctuaryCard != undefined)
   {
      return;
   }
   // The stock plate is the item's hit area (tooltip on hover), so it stays, invisible.
   item.fg._alpha = 0;
   item.sanctuaryCard = item.createEmptyMovieClip("sanctuaryCard",-16384);
   acalDrawItem(item,false);
   acalColor(item.txt1,0xFFFFFF);
   item.txt1.filters = acalOutline();
   acalColor(item.txt2,ACAL_SOFT);
   item.txt2.filters = acalShadow();
   acalColor(item.txt3,ACAL_SOFT);
   item.txt3.filters = acalShadow();
   acalColor(item.txt4,0xFFE08A);
   item.txt4.filters = acalOutline();
   acalStyleGo(item.mcButton);
   acalWrapHover(item.fg,acalItemHover);
   // The icon sits inside its socket: a little smaller than stock. The stock pop-in and pulse scale from these.
   item.icon.oxs = item.icon.oys = item.icon.oxs * 0.86;
   item.icon._xscale = item.icon._yscale = item.icon.oxs;
}

// The list's scrollbar (stock, invisible): a slim navy track with a gold thumb, following the stock thumb.
function acalSyncScroll()
{
   var sb = acalMain.scrollbar;
   var s = acalMain.sanctuaryScroll;
   if(sb == undefined || !sb._visible || sb.thumb_mc.enabled == false)
   {
      s.clear();
      s.key = undefined;
      return;
   }
   sb._alpha = 0;
   var b = sb.getBounds(acalMain);
   var t = sb.thumb_mc.getBounds(acalMain);
   var key = Math.round(b.yMin) + "," + Math.round(b.yMax) + "," + Math.round(t.yMin) + "," + Math.round(t.yMax);
   if(key == s.key)
   {
      return;
   }
   s.key = key;
   var cx = Math.round((b.xMin + b.xMax) / 2);
   s.clear();
   s.lineStyle(1,ACAL_RIM,30);
   s.beginFill(0x0A1440,60);
   acalRoundRect(s,cx - 6,b.yMin + 20,12,b.yMax - b.yMin - 40,6);
   s.endFill();
   s.lineStyle(3,ACAL_SOFT,80);
   s.moveTo(cx - 6,b.yMin + 12);
   s.lineTo(cx,b.yMin + 6);
   s.lineTo(cx + 6,b.yMin + 12);
   s.moveTo(cx - 6,b.yMax - 12);
   s.lineTo(cx,b.yMax - 6);
   s.lineTo(cx + 6,b.yMax - 12);
   if(t.yMax > t.yMin)
   {
      s.lineStyle(1,0xFFF3C4,80);
      acalGoldFill(s,cx - 5,t.yMin,10,t.yMax - t.yMin,false);
      acalRoundRect(s,cx - 5,t.yMin + 1,10,Math.max(10,t.yMax - t.yMin - 2),5);
      s.endFill();
   }
}

function acalBuildMain(main)
{
   acalMain = main;
   // The stock panel is a named sprite; it has no mouse handlers, so hiding it changes nothing underneath.
   main.bg._alpha = 0;
   var p = main.createEmptyMovieClip("sanctuaryPanel",-16384);
   var x = ACAL_LIST[0];
   var y = ACAL_LIST[1];
   var w = ACAL_LIST[2];
   var h = ACAL_LIST[3];
   acalGradientRect(p,x,y,w,h,24,0x223F92,0x111B4C,100,ACAL_RIM,60,2);
   p.lineStyle();
   p.beginFill(0x0A1440,35);
   acalRoundRect(p,x + 10,y + 10,w - 20,52,20);
   p.endFill();
   p.beginGradientFill("linear",[ACAL_GOLD,ACAL_GOLD,ACAL_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:x + 40,y:0,w:w - 80,h:2,r:0});
   acalRoundRect(p,x + 40,y + 66,w - 80,2,1);
   p.endFill();
   p.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   var title = main.mcTitleBar.mcText.txt;
   acalColor(title,0xFFE08A);
   title.filters = acalOutline();
   main.sanctuaryScroll = main.createEmptyMovieClip("sanctuaryScroll",main.getNextHighestDepth());

   // Back to the month (an X at the panel's top right) and the previous / next day arrows.
   var back = main.mcExitButton;
   acalCenterOn(back,x + w - 30,y + 36);
   back.ox = back._x;
   back.oy = back._y;
   main.sanctuaryBack = acalMakeDisc(main,"sanctuaryBack",0,15);
   main.sanctuaryBack._x = x + w - 30;
   main.sanctuaryBack._y = y + 36;
   main.sanctuaryPrev = acalMakeDisc(main,"sanctuaryPrev",-1,20);
   main.sanctuaryNext = acalMakeDisc(main,"sanctuaryNext",1,20);
   var arrows = [[main.mcArrowLeft,main.sanctuaryPrev],[main.mcArrowRight,main.sanctuaryNext]];
   var i;
   for(i = 0; i < arrows.length; i++)
   {
      var d = arrows[i][1];
      d._x = d.homeX = arrows[i][0]._x;
      d._y = arrows[i][0]._y;
      arrows[i][0].sanctuaryDisc = d;
      acalWrapHover(arrows[i][0],acalStockHover);
   }
   back.sanctuaryDisc = main.sanctuaryBack;
   acalWrapHover(back,acalStockHover);
}

function acalStockHover(stock, over)
{
   acalDiscHover(stock.sanctuaryDisc,over && stock.enabled != false);
}

// --- sync ------------------------------------------------------------------------------

function acalSync()
{
   var m = _root.mcMainManager;
   var cal = m.mcMonthlyCalendar;
   var main = m.mcMain;
   var open = main._visible == true;
   acalSyncDisc(acalRoot.prev,m.mcArrowLeft,!open);
   acalSyncDisc(acalRoot.next,m.mcArrowRight,!open);
   acalSyncDisc(main.sanctuaryPrev,main.mcArrowLeft,true);
   acalSyncDisc(main.sanctuaryNext,main.mcArrowRight,true);
   acalSyncDisc(main.sanctuaryBack,main.mcExitButton,true);
   // The close button goes while a day is open (the stock disables it), and comes back with the month.
   var exit = m.mcExitButton;
   exit._alpha = 0;
   var closeOn = exit.enabled != false;
   if(closeOn != acalRoot.close.shown)
   {
      acalRoot.close.shown = closeOn;
      gs.TweenLite.to(acalRoot.close,0.3,{_alpha:closeOn ? 100 : 0});
      if(!closeOn)
      {
         acalDiscHover(acalRoot.close,false);
      }
   }
   acalRoot.close._visible = acalRoot.close._alpha > 0 || closeOn;
   var over = closeOn && exit.hitTest(_root._xmouse,_root._ymouse,false);
   acalDiscHover(acalRoot.close,over);

   acalColor(cal.mcTitle.txt,0xFFE08A);
   var i;
   for(i = 1; i <= 7; i++)
   {
      acalColor(cal["weekday" + i].txt,ACAL_SOFT);
   }
   acalSyncDays(cal.mcDayHolder);
   acalColor(main.mcTitleBar.mcText.txt,0xFFE08A);
   // Items are styled as Lua adds them; this catches any added another way.
   for(i = 0; i < main.items.length; i++)
   {
      acalStyleItem(main.items[i]);
   }
   acalSyncScroll();
}

function acalPatch()
{
   // Lua adds each activity through _root.addMainItem (the day list's addChild); style it as it comes.
   var addMainItem = _root.addMainItem;
   _root.addMainItem = function()
   {
      var item = addMainItem.apply(this,arguments);
      acalStyleItem(item);
      return item;
   };
}

function acalBuild()
{
   var m = _root.mcMainManager;
   var cal = m.mcMonthlyCalendar;
   // The stock frame is a plain shape at the window's first depth: the calendar moves up out of the second, and
   // this frame takes it, over the stock frame and under everything else.
   cal.swapDepths(-16370);
   acalRoot = m.createEmptyMovieClip("sanctuaryCalendar",-16382);
   acalDrawFrame(acalRoot.createEmptyMovieClip("bg",1));

   // The month, in gold in the header.
   cal.mcTitle._y = -124.8;
   cal.mcTitle.txt.filters = acalOutline();
   acalColor(cal.mcTitle.txt,0xFFE08A);
   for(var i = 1; i <= 7; i++)
   {
      cal["weekday" + i].txt.filters = acalShadow();
   }

   acalRoot.prev = acalMakeDisc(acalRoot,"prev",-1,20);
   acalRoot.next = acalMakeDisc(acalRoot,"next",1,20);
   var arrows = [[m.mcArrowLeft,acalRoot.prev],[m.mcArrowRight,acalRoot.next]];
   var k;
   for(k = 0; k < arrows.length; k++)
   {
      var d = arrows[k][1];
      d._x = d.homeX = arrows[k][0]._x;
      d._y = arrows[k][0]._y;
      arrows[k][0].sanctuaryDisc = d;
      acalWrapHover(arrows[k][0],acalStockHover);
   }
   acalCenterOn(m.mcExitButton,ACAL_CLOSE_X,ACAL_CLOSE_Y);
   acalRoot.close = acalMakeDisc(acalRoot,"close",0,17);
   acalRoot.close._x = ACAL_CLOSE_X;
   acalRoot.close._y = ACAL_CLOSE_Y;
   acalRoot.close.shown = true;
   // Loading closed the day view, which faded the stock close button in; it stays invisible.
   gs.TweenLite.killTweensOf(m.mcExitButton);

   acalBuildMain(m.mcMain);

   // Showing a day fades the stock close button out (and closing it fades it back in); it stays invisible here.
   var proto = MainManager.prototype;
   var showMain = proto.showMain;
   proto.showMain = function()
   {
      var r = showMain.apply(this,arguments);
      gs.TweenLite.killTweensOf(m.mcExitButton);
      m.mcExitButton._alpha = 0;
      return r;
   };
   var hideMain = m.mcMain.onCloseFunc != undefined ? m.mcMain.onCloseFunc : proto.hideMain;
   m.mcMain.onCloseFunc = function()
   {
      var r = hideMain.apply(this,arguments);
      gs.TweenLite.killTweensOf(m.mcExitButton);
      m.mcExitButton._alpha = 0;
      return r;
   };

   acalPatch();
   acalRoot.onEnterFrame = acalSync;
   acalSync();
}

acalBuild();
// ===== End Sanctuary Activity Calendar =====

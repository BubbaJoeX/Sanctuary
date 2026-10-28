// ===== Sanctuary Stuff =====
// Appended to inventory's main timeline by build_client_mods.py.
// Redraws the Stuff window (My Stuff, wndInventory) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as):
// a navy glass frame with the title in gold, the coin wallet as a gold pill (Station Cash no longer exists, so its
// wallet is gone), a gold close button and Main Menu pill, and three glass panels. The category sidebar's rows are
// navy pills (the selected one rimmed in gold), the item cards and list rows are navy glass with bigger thumbnails
// in a frosted socket, and the detail panel shows the item's art in a glowing frosted socket over its stats on
// labeled chips. A footer counts what's shown. Everything is drawn under the stock clips, which keep their roles
// (hit areas, Lua callbacks, tooltips); the scrollbars and search button, whose art stock code keeps re-showing,
// get a skin drawn over them instead (a clip with no mouse handlers doesn't take clicks). The frame art and the
// grid's divider lines are dropped at build time (REMOVED_DEPTHS); the bag is a client texture (TEXTURE_SYMBOLS).

var ST_PANEL_Y = 112;
var ST_PANEL_H = 556;
// The three columns: [x, width]. The detail panel is drawn in descriptionPane, so it flips with the item.
var ST_LEFT = [30,226];
var ST_MID = [262,494];
var ST_RIGHT = [762,232];
var ST_PANE_X = 878;
var ST_PANE_Y = 390;
// The grid card thumbnail: its center and scale (the stock icon is 64px), resting and hovered.
var ST_ICON_X = 47;
var ST_ICON_Y = 48;
var ST_ICON_SCALE = 118;
var ST_ICON_HOVER = 124;
var ST_LABEL_X = 94;
var ST_LABEL_Y = 14;
// The list row thumbnail (24px stock).
var ST_ROW_ICON_SCALE = 118;
var ST_ROW_ICON_HOVER = 136;
var ST_ROW_W = 448;
// The detail panel's art (128px holder, 110px stock): top left, in descriptionPane's coordinates.
var ST_ART_X = -64;
var ST_ART_Y = -252;
var ST_GOLD = 0xFFC531;
var ST_RIM = 0x9DB8FF;
var ST_SOFT = 0xC9D6FF;
var ST_INK = 0x3A2400;

// --- drawing helpers -------------------------------------------------------------------

function stRoundRect(mc, x, y, w, h, r)
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

function stGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   stRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

// The gold of the close button, the Main Menu pill and the wallet.
function stGoldRect(mc, x, y, w, h, r, over)
{
   mc.lineStyle(over ? 2 : 1.5,0xFFF3C4,over ? 100 : 90);
   mc.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   stRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   // A glassy shine across the top half.
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h / 2,r:Math.PI / 2});
   stRoundRect(mc,x + 3,y + 2,w - 6,h / 2,Math.min(r,h / 4));
   mc.endFill();
}

// The frosted plate behind item art: the stock icons are made for light plates.
function stFrosted(mc, x, y, w, h, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - w * 0.2,y:y - h * 0.25,w:w * 1.4,h:h * 1.4,r:0});
   stRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function stCircle(mc, x, y, r)
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

function stText(mc, name, txt, x, y, w, h, size, color, align)
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

function stSparkle(mc, x, y, r, alpha)
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

// Sparkles in a box (fixed seed, same every time).
function stSparkles(mc, seed, count, x, y, w, h)
{
   var s = seed;
   var i;
   for(i = 0; i < count; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + s % w;
      s = (s * 1103515245 + 12345) % 2147483648;
      stSparkle(mc,sx,y + s % h,1 + s % 3,25 + s % 50);
   }
}

function stOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function stShadow()
{
   return [new flash.filters.DropShadowFilter(2,90,0x0A1030,0.9,3,3,1,1)];
}

// Text colors that would be hard to read on navy (black, dark, or missing: Lua's "Text Color" can be 0 or
// empty, and stock code applies it with setTextFormat/textColor) are swapped for a light one.
function stIsDark(color)
{
   color = Number(color);
   if(isNaN(color))
   {
      return true;
   }
   return (color >> 16 & 0xFF) * 0.299 + (color >> 8 & 0xFF) * 0.587 + (color & 0xFF) * 0.114 < 96;
}

function stLight(color, fallback)
{
   return stIsDark(color) ? fallback : Number(color);
}

// Sets a text field's color, unless it's already a readable one and keep is true.
function stLightText(t, fallback, keep)
{
   if(t == undefined)
   {
      return;
   }
   if(!keep || stIsDark(t.textColor))
   {
      t.textColor = fallback;
   }
}

// A thin gold rule that fades out at both ends.
function stGoldRule(mc, x, y, w)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[ST_GOLD,ST_GOLD,ST_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:x,y:y,w:w,h:2,r:0});
   stRoundRect(mc,x,y,w,2,1);
   mc.endFill();
}

// Hides a clip's unnamed children (stock art placed without an instance name), found by enumeration and,
// in case that misses them, by their timeline depths.
function stHideUnnamed(mc, depths)
{
   var name;
   for(name in mc)
   {
      if(typeof mc[name] == "movieclip" && mc[name]._parent == mc && name.substr(0,8) == "instance")
      {
         mc[name]._visible = false;
      }
   }
   var i;
   for(i = 0; i < depths.length; i++)
   {
      var o = mc.getInstanceAtDepth(depths[i] - 16384);
      if(o != undefined && o._name.substr(0,8) == "instance")
      {
         o._visible = false;
      }
   }
}

// 999966136 -> "999,966,136".
function stThousands(value)
{
   var s = String(value);
   if(s.length == 0 || isNaN(Number(s)) || s.indexOf(".") >= 0)
   {
      return s;
   }
   var sign = "";
   if(s.charAt(0) == "-")
   {
      sign = "-";
      s = s.substr(1);
   }
   var out = "";
   while(s.length > 3)
   {
      out = "," + s.substr(s.length - 3) + out;
      s = s.substr(0,s.length - 3);
   }
   return sign + s + out;
}

// --- frame -----------------------------------------------------------------------------

var stFrame;
var stPane;

function stPanel(mc, x, y, w, h)
{
   stGradientRect(mc,x,y,w,h,24,0x0A1440,0x0A1440,40,ST_RIM,25,1);
}

function stDrawFrame()
{
   var bg = stFrame.bg;
   bg.clear();
   stGradientRect(bg,10,14,1004,740,30,0x2E4FB0,0x141F55,100,ST_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-200,y:-380,w:1000,h:780,r:0});
   stRoundRect(bg,12,16,1000,736,28);
   bg.endFill();

   // The header band, with a few sparkles.
   bg.beginFill(0x0A1440,30);
   stRoundRect(bg,16,20,992,82,26);
   bg.endFill();
   stSparkles(bg,3131,28,250,26,520,70);
   stGoldRule(bg,412,90,200);

   stPanel(bg,ST_LEFT[0],ST_PANEL_Y,ST_LEFT[1],ST_PANEL_H);
   stPanel(bg,ST_MID[0],ST_PANEL_Y,ST_MID[1],ST_PANEL_H);
   // The footer's rule, under the grid.
   stGoldRule(bg,ST_MID[0] + 40,612,ST_MID[1] - 80);
}

// The list view's column header strip (its titles are the stock ListHeader's).
function stDrawHeader()
{
   var h = stFrame.header;
   h.clear();
   if(item_display.mode == 1)
   {
      stGradientRect(h,ST_MID[0] + 8,159,382,42,21,0x0A1440,0x0A1440,55,ST_RIM,30,1);
   }
   stDrawToggle();
}

// The grid/list toggle, under the stock view buttons (now invisible hit areas).
function stDrawToggle()
{
   var t = stFrame.toggle;
   t.clear();
   var gx = buttonsView_btn._x + 18;
   var lx = listView_btn._x + 18;
   var cy = buttonsView_btn._y + 18;
   var list = item_display.mode == 1;
   stGradientRect(t,gx - 24,cy - 21,lx - gx + 48,42,21,0x0A1440,0x0A1440,55,ST_RIM,30,1);
   var cx = list ? lx : gx;
   t.lineStyle(1,0xFFF3C4,80);
   t.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:cx - 16,y:cy - 16,w:32,h:32,r:Math.PI / 2});
   stCircle(t,cx,cy,16);
   t.endFill();

   // Grid glyph: four squares.
   t.lineStyle();
   t.beginFill(list ? ST_SOFT : ST_INK,100);
   stRoundRect(t,gx - 7,cy - 7,6,6,1);
   stRoundRect(t,gx + 1,cy - 7,6,6,1);
   stRoundRect(t,gx - 7,cy + 1,6,6,1);
   stRoundRect(t,gx + 1,cy + 1,6,6,1);
   t.endFill();
   // List glyph: three bars.
   t.beginFill(list ? ST_INK : ST_SOFT,100);
   stRoundRect(t,lx - 7,cy - 7,14,3,1);
   stRoundRect(t,lx - 7,cy - 1,14,3,1);
   stRoundRect(t,lx - 7,cy + 5,14,3,1);
   t.endFill();
}

// "Showing 1 - 8 of 42" under the grid.
function stUpdateFooter()
{
   var total = Number(item_display.totalItems);
   var txt = "";
   if(total > 0)
   {
      var from = item_display.offset + 1;
      var to = Math.min(item_display.offset + item_display.pageSize,total);
      txt = total == 1 ? "1 item" : "Showing " + from + " - " + to + " of " + stThousands(total);
   }
   stFrame.footer_txt.text = txt;
}

// The coin wallet: a gold pill with the coin in a navy socket (Station Cash no longer exists).
function stDrawWallet()
{
   var w = stFrame.wallet;
   // The coin (a button) is 26px at (1, 1.6) in CoinsAndCash.
   var cx = CoinsAndCash._x + 14;
   var cy = CoinsAndCash._y + 14.6;
   w.clear();
   stGoldRect(w,cx - 26,cy - 20,196,40,20,false);
   w.lineStyle(1.5,0xFFF3C4,90);
   w.beginGradientFill("radial",[0x2B4FA8,0x13215A],[100,100],[0,255],{matrixType:"box",x:cx - 16,y:cy - 22,w:32,h:32,r:0});
   stCircle(w,cx,cy,16);
   w.endFill();
   w.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
}

// The gold round close button, centered under the stock one (an invisible hit area).
function stDrawClose(over)
{
   var c = stFrame.close;
   c.clear();
   var r = 17;
   c.lineStyle(over ? 2 : 1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   stCircle(c,0,0,r);
   c.endFill();
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r,r:Math.PI / 2});
   stRoundRect(c,-11,-15,22,12,6);
   c.endFill();
   c.lineStyle(4,ST_INK,100);
   c.moveTo(-6,-6);
   c.lineTo(6,6);
   c.moveTo(6,-6);
   c.lineTo(-6,6);
   c.filters = over ? [new flash.filters.GlowFilter(ST_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
}

var stCloseListener = new Object();
// fr.core.FRButton broadcasts its states.
stCloseListener.state = function(kind)
{
   var over = kind == "rollover" || kind == "dragover" || kind == "press";
   stDrawClose(over);
   gs.TweenLite.to(stFrame.close,0.5,{_xscale:over ? 114 : 100,_yscale:over ? 114 : 100,ease:mx.transitions.easing.Elastic.easeOut});
};

// The gold Main Menu pill, under the stock button (an invisible hit area).
function stDrawMenuButton(over)
{
   var m = stFrame.menu.bg;
   m.clear();
   stGoldRect(m,-90,-20,180,40,20,over);
   stFrame.menu.filters = over ? [new flash.filters.GlowFilter(ST_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,10,10,1,2)];
}

function stBuildFrame()
{
   stFrame = _root.createEmptyMovieClip("sanctuaryStuff",-16384);
   stFrame.bg = stFrame.createEmptyMovieClip("bg",1);
   stFrame.bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   stFrame.header = stFrame.createEmptyMovieClip("header",2);
   stFrame.toggle = stFrame.createEmptyMovieClip("toggle",3);
   stFrame.wallet = stFrame.createEmptyMovieClip("wallet",4);
   stFrame.close = stFrame.createEmptyMovieClip("close",5);
   stFrame.menu = stFrame.createEmptyMovieClip("menu",6);
   stFrame.menu.bg = stFrame.menu.createEmptyMovieClip("bg",1);

   // The title: the stock field (Lua sets it), wider and in gold; the bag sits left of it.
   wndTitle._x = 312;
   wndTitle._y = 26;
   wndTitle._width = 400;
   wndTitle._height = 40;
   var f = wndTitle.getTextFormat();
   f.size = 28;
   f.color = 0xFFE08A;
   wndTitle.setTextFormat(f);
   wndTitle.setNewTextFormat(f);
   wndTitle.filters = stOutline();
   var bag = stFrame.attachMovie("sanctuaryStuffBag","bag",7);
   bag._width = bag._height = 42;
   bag._y = 25;
   stFrame.subtitle_txt = stText(stFrame,"subtitle_txt","Everything you own",312,66,400,22,14,ST_SOFT);
   stPlaceBag();

   stFrame.footer_txt = stText(stFrame,"footer_txt","",ST_MID[0],620,ST_MID[1],24,14,ST_SOFT);

   // Coins only: the Station Cash wallet goes.
   CoinsAndCash._x = 44;
   CoinsAndCash.m_SCLabel._visible = CoinsAndCash.m_SCIcon._visible = false;
   CoinsAndCash.m_CoinsLabel.textColor = ST_INK;
   stDrawWallet();

   closeButton._x = 948;
   closeButton._y = 39;
   closeButton.UP._alpha = closeButton.HOVER._alpha = closeButton.DOWN._alpha = 0;
   closeButton.ICON_UP._alpha = closeButton.ICON_HOVER._alpha = 0;
   stFrame.close._x = closeButton._x + closeButton._width / 2;
   stFrame.close._y = closeButton._y + closeButton._height / 2;
   closeButton.addListener(stCloseListener);
   stDrawClose(false);

   mainMenuButton._alpha = 0;
   stFrame.menu._x = 513;
   stFrame.menu._y = 704;
   stText(stFrame.menu,"label_txt","Main Menu",-90,-12,180,26,18,ST_INK);
   stDrawMenuButton(false);

   // The stock frame art (depth 1, dropped at build time) shared its instance name with an empty, window-sized
   // clip at depth 13 that sits over the category tree. With the frame gone, the stock "background.onRelease = null"
   // lands on that clip, which then takes the mouse and swallows the tree's clicks (no expanding categories). It
   // draws nothing, so it goes.
   var blocker = _root.getInstanceAtDepth(13 - 16384);
   if(blocker == undefined)
   {
      blocker = background;
   }
   delete blocker.onRelease;
   blocker._visible = false;

   // The view toggle and its column header.
   buttonsView_btn._alpha = listView_btn._alpha = 0;
   buttonViewTitleBar._alpha = 0;
   stHideUnnamed(ListHeader,[1,5]);
   ListHeader._y = 163;
   ListHeader.NameTitle.filters = ListHeader.QuantityTitle.filters = stOutline();
   stColorHeader();

   item_display_container._x = 270;
   scrollbar._x = 722;
   category._x = ST_MID[0] + 14;
   category._y = 120;
   category.filters = stOutline();
   m_NoItemsText.filters = stOutline();
   stLightText(m_NoItemsText,ST_SOFT,false);
   stLightText(category,0xFFFFFF,false);

   stDrawFrame();
   stDrawHeader();
}

// The bag sits just left of the title's text.
function stPlaceBag()
{
   var w = wndTitle.textWidth > 0 ? wndTitle.textWidth : 70;
   stFrame.bag._x = Math.round(512 - w / 2 - 52);
}

// --- search ----------------------------------------------------------------------------

// The search field becomes a navy pill; its button gets a gold tile drawn over it, since SearchManager keeps
// setting the stock button's alpha. The tile shows go (a magnifier), cancel (an X) or waiting (navy).
function stDrawSearchButton(go)
{
   var cx = go.cx;
   var cy = go.cy;
   go.clear();
   if(go.state == 0)
   {
      stGradientRect(go,cx - 18,cy - 18,36,36,12,0x2B4FA8,0x17286A,100,ST_RIM,60,1.5);
   }
   else
   {
      stGoldRect(go,cx - 18,cy - 18,36,36,12,go.over);
   }
   var ink = go.state == 0 ? ST_SOFT : ST_INK;
   if(go.state == 2)
   {
      go.lineStyle(3.5,ink,100);
      go.moveTo(cx - 6,cy - 6);
      go.lineTo(cx + 6,cy + 6);
      go.moveTo(cx + 6,cy - 6);
      go.lineTo(cx - 6,cy + 6);
   }
   else
   {
      go.lineStyle(3,ink,100);
      stCircle(go,cx - 2,cy - 2,6);
      go.lineStyle(4,ink,100);
      go.moveTo(cx + 3,cy + 3);
      go.lineTo(cx + 8,cy + 8);
   }
}

function stBuildSearch()
{
   stHideUnnamed(mcSearch,[1]);
   var field = mcSearch.createEmptyMovieClip("sanctuaryField",-16384);
   stGradientRect(field,0,1,160,34,17,0x0A1440,0x0A1440,70,ST_RIM,45,1.5);
   field.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.4,4,4,1,2,true)];

   stLightText(mcSearch.txtSearch,0xFFFFFF,false);
   var typed = mcSearch.txtSearch.getNewTextFormat();
   typed.color = 0xFFFFFF;
   mcSearch.txtSearch.setNewTextFormat(typed);

   var btn = mcSearch.btnSearch;
   var go = mcSearch.createEmptyMovieClip("sanctuarySearchButton",mcSearch.getNextHighestDepth());
   go.cx = Math.round(btn._x + btn._width / 2);
   go.cy = Math.round(btn._y + btn._height / 2);
   go.state = -1;
   go.onEnterFrame = function()
   {
      var state = mcSearch.btnCancel._visible ? 2 : (mcSearch.btnSearch.enabled == false ? 0 : 1);
      if(state != this.state)
      {
         this.state = state;
         stDrawSearchButton(this);
      }
   };
   var over = btn.onRollOver;
   btn.onRollOver = mcSearch.btnCancel.onRollOver = function()
   {
      over.call(this);
      go.over = true;
      stDrawSearchButton(go);
   };
   btn.onRollOut = btn.onReleaseOutside = mcSearch.btnCancel.onRollOut = function()
   {
      go.over = false;
      stDrawSearchButton(go);
   };
}

// --- category sidebar ------------------------------------------------------------------

function stDrawNavRow(item)
{
   var r = item.sanctuaryRow;
   if(r == undefined)
   {
      return;
   }
   // Rows end at one column whatever their indent; the expand chevron sits there (NavigationElement._init).
   var w = 200 - item._x;
   var child = item._x > 5;
   r.clear();
   if(item.sanctuaryOver)
   {
      stGradientRect(r,0,0,w,33,12,0x3F66D6,0x1D307A,100,0xFFE08A,100,1.5);
   }
   else if(item.boolIsSelected)
   {
      stGradientRect(r,0,0,w,33,12,0x2B4FA8,0x17286A,100,ST_GOLD,100,2);
      r.lineStyle();
      r.beginGradientFill("linear",[ST_GOLD,ST_GOLD],[38,4],[0,255],{matrixType:"box",x:0,y:0,w:w,h:33,r:0});
      stRoundRect(r,1,1,w - 2,31,11);
      r.endFill();
   }
   else if(child)
   {
      r.lineStyle();
      r.beginFill(0x0A1440,35);
      stRoundRect(r,0,0,w,33,12);
      r.endFill();
   }
   else
   {
      stGradientRect(r,0,0,w,33,12,0x284A9E,0x162660,100,ST_RIM,35,1);
   }
   r.filters = item.boolIsSelected && !item.sanctuaryOver ? [new flash.filters.GlowFilter(ST_GOLD,0.45,10,10,2,2)] : [];
}

function stStyleNavRow(item)
{
   if(!item.sanctuaryRow)
   {
      // The hover plate (a client image) and the separator line under each row.
      item.highlight._alpha = 0;
      stHideUnnamed(item,[9]);
      item.sanctuaryRow = item.createEmptyMovieClip("sanctuaryRow",-16384);
      item.sanctuaryLabelX = item.txtLabel._x;
      item.txtLabel.filters = stShadow();
   }
   stLightText(item.txtLabel,item.boolIsSelected ? 0xFFE08A : 0xFFFFFF,false);
   stDrawNavRow(item);
}

// The hover-to-scroll bars above and below the categories become slim capsules with a chevron.
function stDrawNavArrows()
{
   var a = m_tree_mc.sanctuaryArrows;
   a.clear();
   stDrawNavArrow(a,m_tree_mc.mcNavTop,m_tree_mc.mcNavTop.mcArrowTop._visible != false,true);
   stDrawNavArrow(a,m_tree_mc.mcNavBottom,m_tree_mc.mcNavBottom.mcArrowBottom._visible != false,false);
}

function stDrawNavArrow(a, bar, shown, up)
{
   if(!shown)
   {
      return;
   }
   var x = bar._x + 4;
   var y = bar._y + 3;
   var w = 198;
   var h = 21;
   var over = bar.sanctuaryOver == true;
   stGradientRect(a,x,y,w,h,10,0x1B2F78,0x13215A,over ? 100 : 75,over ? 0xFFE08A : ST_RIM,over ? 90 : 35,1);
   var cx = x + w / 2;
   var cy = y + h / 2;
   var d = up ? -1 : 1;
   a.lineStyle(3,over ? 0xFFE08A : ST_SOFT,90);
   a.moveTo(cx - 8,cy - 3 * d);
   a.lineTo(cx,cy + 3 * d);
   a.lineTo(cx + 8,cy - 3 * d);
}

function stPatchNav()
{
   var proto = fr.display.NavigationElement.prototype;
   var rollOver = proto._handleRollOver;
   proto._handleRollOver = function()
   {
      rollOver.call(this);
      this.sanctuaryOver = true;
      stDrawNavRow(this);
      gs.TweenLite.to(this.txtLabel,0.3,{_x:this.sanctuaryLabelX + 4,ease:mx.transitions.easing.Strong.easeOut});
   };
   var rollOut = proto._handleRollOut;
   proto._handleRollOut = function()
   {
      rollOut.call(this);
      this.sanctuaryOver = false;
      stDrawNavRow(this);
      if(this.sanctuaryLabelX != undefined)
      {
         gs.TweenLite.to(this.txtLabel,0.3,{_x:this.sanctuaryLabelX,ease:mx.transitions.easing.Strong.easeOut});
      }
   };
   var select = proto.select;
   proto.select = function()
   {
      select.call(this);
      this.txtLabel.textColor = 0xFFE08A;
      stDrawNavRow(this);
   };
   var unselect = proto.unselect;
   proto.unselect = function()
   {
      unselect.call(this);
      this.txtLabel.textColor = 0xFFFFFF;
      stDrawNavRow(this);
   };

   var tree = fr.display.TreeNavigation.prototype;
   var setElementData = tree._setElementData;
   tree._setElementData = function(item, data)
   {
      setElementData.call(this,item,data);
      stStyleNavRow(item);
   };
   var onScrollbarUpdate = tree.onScrollbarUpdate;
   tree.onScrollbarUpdate = function(obj)
   {
      onScrollbarUpdate.call(this,obj);
      stDrawNavArrows();
   };
}

function stBuildNav()
{
   var top = m_tree_mc.mcNavTop;
   var bottom = m_tree_mc.mcNavBottom;
   top._alpha = bottom._alpha = 0;
   m_tree_mc.sanctuaryArrows = m_tree_mc.createEmptyMovieClip("sanctuaryArrows",-16384);
   var bars = [top,bottom];
   var i;
   for(i = 0; i < bars.length; i++)
   {
      var bar = bars[i];
      bar.sanctuaryOver = false;
      bar.stockRollOver = bar.onRollOver;
      bar.stockRollOut = bar.onRollOut;
      bar.onRollOver = function()
      {
         this.stockRollOver();
         this.sanctuaryOver = true;
         stDrawNavArrows();
      };
      bar.onRollOut = function()
      {
         this.stockRollOut();
         this.sanctuaryOver = false;
         stDrawNavArrows();
      };
   }
   stDrawNavArrows();
}

// --- cards (grid view) -----------------------------------------------------------------

function stDrawCard(b, over, down)
{
   var c = b.sanctuaryCard;
   var selected = b.sanctuarySelected == true;
   var y = down ? 5 : 3;
   c.clear();
   if(over)
   {
      stGradientRect(c,2,y,218,88,18,0x4069DA,0x1E3486,100,selected ? ST_GOLD : 0xFFE08A,100,selected ? 3 : 2);
   }
   else if(selected)
   {
      stGradientRect(c,2,y,218,88,18,0x3159BC,0x1A2D74,100,ST_GOLD,100,3);
   }
   else
   {
      stGradientRect(c,2,y,218,88,18,0x2B4FA8,0x17286A,100,ST_RIM,40,1.5);
   }
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[16,0],[0,255],{matrixType:"box",x:0,y:y + 3,w:10,h:40,r:Math.PI / 2});
   stRoundRect(c,5,y + 3,212,40,15);
   c.endFill();
   stFrosted(c,9,y + 7,76,76,14,selected ? ST_GOLD : 0xFFFFFF,selected ? 100 : 85,2);

   if(over)
   {
      c.filters = [new flash.filters.GlowFilter(ST_GOLD,0.55,14,14,2,2)];
   }
   else if(selected)
   {
      c.filters = [new flash.filters.GlowFilter(ST_GOLD,0.4,12,12,2,2)];
   }
   else
   {
      c.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
   }
}

function stStyleCard(b)
{
   if(b.sanctuaryCard)
   {
      return;
   }
   // InventoryItemButton's stock plates and its selection sparkle; the hit area stays (invisibly).
   b.background._alpha = b.states._alpha = b.iconBackground._alpha = b.selection._alpha = 0;
   b.hitArea._alpha = 0;
   b.sanctuaryCard = b.createEmptyMovieClip("sanctuaryCard",-16384);

   b.icon._x = ST_ICON_X;
   b.icon._y = ST_ICON_Y;
   b.icon._xscale = b.icon._yscale = ST_ICON_SCALE;
   // Rounds off the thumbnail's corners (and crops it to the socket when it grows on hover).
   var mask = b.createEmptyMovieClip("sanctuaryIconMask",b.getNextHighestDepth());
   mask.beginFill(0,100);
   stRoundRect(mask,11,12,72,72,12);
   mask.endFill();
   b.icon.setMask(mask);

   b.label._x = ST_LABEL_X;
   b.label._y = ST_LABEL_Y;
   b.label._width = 118;
   b.label._height = 52;
   b.label.filters = stShadow();
   b.quantityObject._x = 12;
   b.quantityObject._y = 64;
}

// The "x10" quantity badge becomes a small gold pill.
function stDrawQuantity(q)
{
   if(!q.sanctuaryPill)
   {
      q.background._alpha = 0;
      q.label.filters = stShadow();
      q.sanctuaryPill = q.createEmptyMovieClip("sanctuaryPill",-16384);
   }
   q.label.textColor = 0xFFFFFF;
   var p = q.sanctuaryPill;
   var h = q.label._height + 2;
   p.clear();
   stGradientRect(p,q.label._x - 6,q.label._y - 1,q.label._width + 12,h,h / 2,0x2B4FA8,0x13215A,100,ST_GOLD,100,1.5);
}

function stPatchCards()
{
   var proto = inventory.item.InventoryItemButton.prototype;

   // Before the stock onLoad, whose updateState(1) draws the card.
   var onLoad = proto.onLoad;
   proto.onLoad = function()
   {
      stStyleCard(this);
      onLoad.call(this);
   };

   var select = proto.select;
   proto.select = function(value)
   {
      select.call(this,value);
      this.sanctuarySelected = value;
      if(this.sanctuaryCard)
      {
         stDrawCard(this,false,false);
      }
   };

   proto.updateState = function(mode)
   {
      stStyleCard(this);
      var over = mode == 2 || mode == 3;
      var down = mode == 3 ? inventory.item.InventoryItemButton.pressDisplacement : 0;
      stDrawCard(this,over,mode == 3);
      if(over)
      {
         gs.TweenLite.to(this.icon,0.6,{_xscale:ST_ICON_HOVER,_yscale:ST_ICON_HOVER,ease:mx.transitions.easing.Elastic.easeOut});
      }
      else
      {
         gs.TweenLite.to(this.icon,0.15,{_xscale:ST_ICON_SCALE,_yscale:ST_ICON_SCALE});
      }
      this.icon._y = ST_ICON_Y + down;
      this.label._y = ST_LABEL_Y + down;
      this.properties._y = inventory.item.InventoryItemButton.propOrigY + down;
   };

   var setName = proto.setName;
   proto.setName = function(value)
   {
      setName.call(this,value);
      this.label.textColor = stLight(this.getItem()[inventory.item.InventoryItemAttributes.TEXT_COLOR],0xFFFFFF);
   };
   var setTextColor = proto.setTextColor;
   proto.setTextColor = function(value)
   {
      setTextColor.call(this,stLight(value,0xFFFFFF));
   };

   var quantity = inventory.item.InventoryItemButtonQuantityDisplay.prototype;
   var resize = quantity.resize;
   quantity.resize = function()
   {
      resize.call(this);
      stDrawQuantity(this);
   };

   // Membership no longer exists: no Members Only badge on cards.
   inventory.item.InventoryItemButtonProperties.prototype.createMOClip = function()
   {
   };
}

// --- rows (list view) ------------------------------------------------------------------

function stDrawRow(row, over)
{
   var r = row.sanctuaryRow;
   var w = ST_ROW_W;
   var selected = row.sanctuarySelected == true;
   r.clear();
   if(over)
   {
      stGradientRect(r,0,1,w,30,12,0x4069DA,0x1E3486,100,0xFFE08A,100,1.5);
   }
   else if(selected)
   {
      stGradientRect(r,0,1,w,30,12,0x2B4FA8,0x17286A,100,ST_GOLD,100,2);
   }
   else
   {
      r.lineStyle();
      r.beginFill(0x0A1440,row.index % 2 == 0 ? 18 : 34);
      stRoundRect(r,0,1,w,30,12);
      r.endFill();
   }
   stFrosted(r,79,2,30,28,9,selected ? ST_GOLD : 0xFFFFFF,selected ? 100 : 70,1);
   r.filters = selected && !over ? [new flash.filters.GlowFilter(ST_GOLD,0.4,10,10,2,2)] : [];
}

function stStyleRow(row)
{
   if(row.sanctuaryRow)
   {
      return;
   }
   row.shade._alpha = row.highlight._alpha = row.hitArea._alpha = 0;
   row.sanctuaryRow = row.createEmptyMovieClip("sanctuaryRow",-16384);
   row.label.filters = stShadow();
   stLightText(row.ItemQuantity,0xFFFFFF,false);
   stLightText(row.coinAmount,0xFFFFFF,false);
   stLightText(row.powerRatingLabel,0xFFFFFF,false);
}

function stPatchRows()
{
   var proto = inventory.item.InventoryItemList.prototype;
   var onLoad = proto.onLoad;
   proto.onLoad = function()
   {
      stStyleRow(this);
      onLoad.call(this);
   };
   // The stock select redraws through updateState, so the flag goes first.
   var select = proto.select;
   proto.select = function(value)
   {
      this.sanctuarySelected = value;
      select.call(this,value);
   };
   proto.updateState = function(mode)
   {
      stStyleRow(this);
      this.highlight._visible = false;
      var over = mode == 2 || mode == 3;
      stDrawRow(this,over);
      if(over)
      {
         gs.TweenLite.to(this.icon,0.6,{_xscale:ST_ROW_ICON_HOVER,_yscale:ST_ROW_ICON_HOVER,ease:mx.transitions.easing.Elastic.easeOut});
      }
      else
      {
         gs.TweenLite.to(this.icon,0.1,{_xscale:ST_ROW_ICON_SCALE,_yscale:ST_ROW_ICON_SCALE});
      }
   };
   // Lua's "Text Color" (parseInt'd by the stock setter) can be dark or missing.
   var setTextColor = proto.setTextColor;
   proto.setTextColor = function(value)
   {
      setTextColor.call(this,stLight(parseInt(value),0xFFFFFF));
   };
   var setName = proto.setName;
   proto.setName = function(value)
   {
      setName.call(this,value);
      stLightText(this.label,0xFFFFFF,true);
   };
   var setQuantity = proto.setQuantity;
   proto.setQuantity = function(value)
   {
      setQuantity.call(this,value);
      stLightText(this.ItemQuantity,0xFFFFFF,true);
   };
   // No Members Only icon on rows either.
   var setMembersOnly = proto.setMembersOnly;
   proto.setMembersOnly = function(value)
   {
      setMembersOnly.call(this,value);
      this.membersOnlyIcon._visible = false;
   };
}

// --- scrollbars ------------------------------------------------------------------------

// FRScrollBarV2 keeps resetting its parts' alpha (enableScrollBar), so the skin is drawn over the stock
// scrollbar, which still takes the clicks: a navy capsule with chevrons where the arrows are and a gold thumb.
function stDrawScrollbar(s)
{
   var sb = s.sb;
   var t = sb.thumb_mc;
   var h = sb.background_mc._height;
   var w = 28;
   s.clear();
   stGradientRect(s,0,0,w,h,14,0x1B2F78,0x13215A,100,ST_RIM,40,1);
   s.lineStyle();
   s.beginFill(0x050A24,60);
   stRoundRect(s,w / 2 - 3,24,6,h - 48,3);
   s.endFill();
   s.lineStyle(2.5,ST_SOFT,80);
   s.moveTo(w / 2 - 5,13);
   s.lineTo(w / 2,8);
   s.lineTo(w / 2 + 5,13);
   s.moveTo(w / 2 - 5,h - 13);
   s.lineTo(w / 2,h - 8);
   s.lineTo(w / 2 + 5,h - 13);
   var enabled = t._alpha > 60;
   s.lineStyle(1,0xFFF3C4,enabled ? 90 : 40);
   s.beginGradientFill("linear",[0xFFE08A,0xF5A623],[enabled ? 100 : 45,enabled ? 100 : 45],[0,255],{matrixType:"box",x:w / 2 - 6,y:t._y,w:12,h:t._height,r:0});
   stRoundRect(s,w / 2 - 6,t._y,12,Math.max(t._height,14),6);
   s.endFill();
}

function stSkinScrollbar(sb)
{
   var s = sb.createEmptyMovieClip("sanctuarySkin",sb.getNextHighestDepth());
   s.sb = sb;
   s.onEnterFrame = function()
   {
      var t = this.sb.thumb_mc;
      var key = t._y + "," + t._height + "," + t._alpha + "," + this.sb.background_mc._height;
      if(key != this.key)
      {
         this.key = key;
         stDrawScrollbar(this);
      }
   };
}

// --- detail panel ----------------------------------------------------------------------

// The art's socket follows the icon, which the stock pane moves aside for rentals (setRental).
function stPlaceSocket()
{
   var dx = descriptionPane.icon._x - ST_ART_X;
   stPane.socket._x = stPane.artMask._x = dx;
}

function stDrawEquip(over)
{
   var b = descriptionPane.equipButton;
   var e = stPane.equip;
   e._visible = b._visible;
   var enabled = b.enabled != false;
   var use = _root.equipButtonString == _root.useString;
   e.bg.clear();
   if(enabled)
   {
      stGoldRect(e.bg,-75,-20,150,40,20,over);
   }
   else
   {
      stGradientRect(e.bg,-75,-20,150,40,20,0x2B4FA8,0x17286A,100,ST_GOLD,100,2);
   }
   e.label_txt.text = use ? "Use" : (enabled ? "Equip" : "Equipped");
   e.label_txt.textColor = enabled ? ST_INK : 0xFFE08A;
   e.filters = over ? [new flash.filters.GlowFilter(ST_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,10,10,1,2)];
}

// The stat lines (Description, Rank Required, Quantity, ...) on chips: gold title, white value.
function stStyleInfoItem(item, w)
{
   if(!item.sanctuaryChip)
   {
      item.sanctuaryChip = item.createEmptyMovieClip("sanctuaryChip",-16384);
      item.label._x = 12;
      item.label._y = 6;
      item.label._width = w - 20;
      // Re-set so the narrower field wraps (and autosizes) again; anything outside the stock font tags is white.
      item.label.htmlText = "<font color='#FFFFFF'>" + item.title + item.content + "</font>";
   }
   var c = item.sanctuaryChip;
   var h = Math.round(item.label._height) + 12;
   c.clear();
   stGradientRect(c,0,0,w,h,12,0x1B2F78,0x13215A,100,ST_RIM,35,1);
   c.lineStyle();
   c.beginFill(ST_GOLD,90);
   stRoundRect(c,4,7,3,h - 14,1.5);
   c.endFill();
}

function stPatchPane()
{
   var info = inventory.display.ItemInformationListItem;
   info.COLOR_TITLE = "#FFE08A";
   info.SIZE_TITLE = 13;
   info.SIZE_CONTENT = 13;
   inventory.display.ItemInformationList.spacer = 6;
   var list = inventory.display.ItemInformationList.prototype;
   var redraw = list.redraw;
   list.redraw = function()
   {
      var i;
      for(i = 0; i < this.m_InfoList.length; i++)
      {
         if(this.m_InfoList[i])
         {
            stStyleInfoItem(this.m_InfoList[i],this.scrollbar._x - 4);
         }
      }
      redraw.call(this);
   };

   var proto = inventory.display.DescriptionPane.prototype;
   var updateData = proto.updateData;
   proto.updateData = function()
   {
      updateData.call(this);
      stPane.placeholder._visible = false;
      // Plain white names are shown in gold; colored ones (rarity) keep their color.
      var color = Number(this.getItem()[inventory.item.InventoryItemAttributes.TEXT_COLOR]);
      this.name.textColor = color == 0xFFFFFF ? 0xFFE08A : stLight(color,0xFFE08A);
   };
   var setRentalTimeRemainingString = proto.setRentalTimeRemainingString;
   proto.setRentalTimeRemainingString = function(message)
   {
      setRentalTimeRemainingString.call(this,message);
      this.m_TimeLeft.textColor = 0xFFFFFF;
   };
   var setEquippable = proto.setEquippable;
   proto.setEquippable = function(value)
   {
      setEquippable.call(this,value);
      this.equipButton._alpha = 0;
      stDrawEquip(false);
   };
   var setRental = proto.setRental;
   proto.setRental = function(value)
   {
      setRental.call(this,value);
      stPlaceSocket();
   };
   // Membership no longer exists: no Members Only badge or Upgrade button.
   var setMembersOnly = proto.setMembersOnly;
   proto.setMembersOnly = function(value)
   {
      setMembersOnly.call(this,value);
      this.membersOnlyBadge._visible = false;
   };
}

function stBuildPane()
{
   var p = descriptionPane;
   p._x = ST_PANE_X;
   p._y = ST_PANE_Y;
   p.background._alpha = p.inlay._alpha = p.iconBackground._alpha = 0;
   p.membersOnlyBadge._visible = false;

   stPane = p.createEmptyMovieClip("sanctuaryCard",-16384);
   var c = stPane.createEmptyMovieClip("bg",1);
   stPanel(c,- ST_RIGHT[1] / 2,ST_PANEL_Y - ST_PANE_Y,ST_RIGHT[1],ST_PANEL_H);
   // A soft gold glow behind the art, with a few sparkles.
   c.lineStyle();
   c.beginGradientFill("radial",[0xFFE08A,0x8FB4FF],[40,0],[0,255],{matrixType:"box",x:-120,y:ST_ART_Y - 56,w:240,h:240,r:0});
   stRoundRect(c,- ST_RIGHT[1] / 2 + 2,ST_PANEL_Y - ST_PANE_Y + 2,ST_RIGHT[1] - 4,236,22);
   c.endFill();
   stSparkles(c,5151,14,-100,ST_ART_Y - 4,200,150);
   stGoldRule(c,-80,-50,160);

   // The frosted socket; the stock iconBackground is resized so the stock centering (setRental) fits 128px art.
   var s = stPane.socket = stPane.createEmptyMovieClip("socket",2);
   stFrosted(s,ST_ART_X - 6,ST_ART_Y - 6,140,140,24,0xFFFFFF,90,2.5);
   s.filters = [new flash.filters.GlowFilter(0xFFE08A,0.55,18,18,2,2)];
   // Until an item is picked, the socket holds a faint bag.
   var bag = stPane.placeholder = s.attachMovie("sanctuaryStuffBag","placeholder",1);
   bag._width = bag._height = 84;
   bag._x = ST_ART_X + 22;
   bag._y = ST_ART_Y + 22;
   bag._alpha = 35;
   p.iconBackground._xscale = p.iconBackground._yscale = 128 / 110 * 100;
   p.iconBackground._x = p.icon._x = ST_ART_X;
   p.iconBackground._y = p.icon._y = ST_ART_Y;
   p.icon._xscale = p.icon._yscale = 100;
   var mask = stPane.artMask = p.createEmptyMovieClip("sanctuaryArtMask",p.getNextHighestDepth());
   mask.beginFill(0,100);
   stRoundRect(mask,ST_ART_X - 2,ST_ART_Y - 2,132,132,20);
   mask.endFill();
   p.icon.setMask(mask);
   stPlaceSocket();

   p.name._x = -89;
   p.name._y = -112;
   p.name.filters = stOutline();
   // The stat chips: a little wider than stock, with the scrollbar's column still inside the card. The list
   // masks itself to the scrollbar's x (ItemInformationList.setup), so the mask follows if it already exists.
   p.InfoList._x = -100;
   p.InfoList._y = -38;
   p.InfoList.scrollbar._x = 186;
   p.InfoList.ItemInformationList_MASK._width = 186;
   stHideUnnamed(p.InfoList,[16,18]);
   stSkinScrollbar(p.InfoList.scrollbar);

   // Rentals: the time left and Buy button beside the art, which moves left.
   p.m_TimeLeft._x = 40;
   p.m_TimeLeft._y = -258;
   p.m_BuyRental._x = 40;
   p.m_BuyRental._y = -196;

   // The Equip/Use pill. The stock button is stretched to the pill's size, invisibly, and keeps its click.
   var e = stPane.equip = stPane.createEmptyMovieClip("equip",4);
   e._x = 0;
   e._y = 222;
   e.bg = e.createEmptyMovieClip("bg",1);
   stText(e,"label_txt","Equip",-75,-12,150,26,18,ST_INK);
   e._visible = false;
   var b = p.equipButton;
   b._xscale = 150 / 80 * 100;
   b._yscale = 40 / 56 * 100;
   b._x = -75;
   b._y = 202;
   b._alpha = 0;
   var over = b.onRollOver;
   b.onRollOver = function()
   {
      over.call(this);
      stDrawEquip(true);
      gs.TweenLite.to(stPane.equip,0.4,{_xscale:106,_yscale:106,ease:mx.transitions.easing.Elastic.easeOut});
   };
   b.onRollOut = b.onReleaseOutside = function()
   {
      stDrawEquip(false);
      gs.TweenLite.to(stPane.equip,0.3,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
   };
}

// The list view's column titles in gold: the named ones and the Equip column's unnamed one.
function stColorHeader()
{
   var name;
   for(name in ListHeader)
   {
      if(ListHeader[name] instanceof TextField)
      {
         ListHeader[name].textColor = 0xFFE08A;
         ListHeader[name].filters = stOutline();
      }
   }
   var titles = [ListHeader.NameTitle,ListHeader.QuantityTitle,ListHeader.getInstanceAtDepth(4 - 16384)];
   var i;
   for(i = 0; i < titles.length; i++)
   {
      if(titles[i] != undefined)
      {
         titles[i].textColor = 0xFFE08A;
         titles[i].filters = stOutline();
      }
   }
}

// --- Lua and stock hooks ---------------------------------------------------------------

// The breadcrumb's " > " separators sit outside the stock font tags; wrapped, they're white rather than default.
var stSetCategory = SetCategory;
SetCategory = function(value)
{
   stSetCategory(value != undefined && String(value).length > 0 ? "<font color='#FFFFFF'>" + value + "</font>" : value);
};

var stSetWindowTitle = SetWindowTitle;
SetWindowTitle = function(value)
{
   stSetWindowTitle(value);
   wndTitle.textColor = 0xFFE08A;
   stPlaceBag();
};

var stSetMode = setMode;
setMode = function(mode, resetScrollPosition)
{
   stSetMode(mode,resetScrollPosition);
   stColorHeader();
   stDrawHeader();
   stUpdateFooter();
};

var stSetData = setData;
setData = function(message, rowCount)
{
   stSetData(message,rowCount);
   stUpdateFooter();
};

// Coins with thousands separators.
var stSetCoins = inventory.display.CoinAndCashDisplay.prototype.setCoins;
inventory.display.CoinAndCashDisplay.prototype.setCoins = function(value)
{
   stSetCoins.call(this,stThousands(value));
};

var stOnMainMenuOver = _onmainMenuButtonOver;
_onmainMenuButtonOver = function()
{
   stOnMainMenuOver();
   stDrawMenuButton(true);
   gs.TweenLite.to(stFrame.menu,0.4,{_xscale:106,_yscale:106,ease:mx.transitions.easing.Elastic.easeOut});
};

var stOnMainMenuOut = _onmainMenuButtonOut;
_onmainMenuButtonOut = function()
{
   stOnMainMenuOut();
   stDrawMenuButton(false);
   gs.TweenLite.to(stFrame.menu,0.3,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
};

stPatchNav();
stPatchCards();
stPatchRows();
stPatchPane();
stBuildFrame();
stBuildSearch();
stBuildNav();
stBuildPane();
stSkinScrollbar(scrollbar);
// ===== End Sanctuary Stuff =====

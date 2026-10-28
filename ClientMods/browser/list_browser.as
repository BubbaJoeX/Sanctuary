// ===== Sanctuary List Browser =====
// Appended to Browser's main timeline by build_client_mods.py.
// The older drop-down browser (wndBrowser: crafting and factory recipes, collections, pets, vehicles and classes;
// wndFarmersMarketBrowser: the Farmers Market) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as):
// a navy glass frame with the title in gold over the subtitle in soft blue, the mode's emblem in a gold-rimmed
// frosted medallion on the frame's top edge (the stock drop-down handle, so it still opens and closes the list),
// a gold close circle, a recessed grid and a gold pager. Every badge type becomes a glass card with its icon in a
// frosted socket, the name in white and a glossy bar for what the card tracks: ingredients for recipes (green,
// gold once you have them all), items collected for collections (gold), the pet's mood (green, amber or red, by
// its face) and a class's level and experience (a gold rank disc and bar, MAX at the top). Hovered cards light
// up with a gold rim and their socket glows; the selected one has a gold rim and a gold check. Station Cash and
// membership no longer exist, so nothing is greyed out or sent to the membership page as members only.
// The frame is drawn inside the browser clip, so it plays the stock expand and compress tweens with it; the
// stock clips keep their roles (hit areas, tooltips, the pager's buttons, Lua events, including the stock
// "SelectFarmewrsMarketRecipe"); their art is hidden. The Farmers Market's emblem is the farming browser's seed
// bag, a client texture (TEXTURE_SYMBOLS).

var LB_GOLD = 0xFFC531;
var LB_RIM = 0x9DB8FF;
var LB_SOFT = 0xC9D6FF;
var LB_INK = 0x3A2400;
// The frame's top edge (the stock panel's), in the browser clip's coordinates; the header under it holds the
// medallion's lower half, the title and the subtitle.
var LB_TOP = -43;
var LB_RULE_Y = 58;
// The grid moves down a little to make room for the header.
var LB_VIEW_SHIFT = 16;
// The stock badge: 213 wide, 156 tall; the card inside it, and where its name and tracker go.
var LB_CARD_X = 3;
var LB_CARD_Y = -2;
var LB_CARD_W = 207;
var LB_CARD_H = 156;
var LB_SOCKET = 92;
var LB_NAME_Y = 95;
var LB_STATUS_Y = 117;
var LB_BAR_Y = 138;
var LB_BAR_X = 16;
var LB_BAR_W = 181;
// The handle's medallion, in the root icon clip's coordinates (the center of its stock 128px icon holder).
var LB_MEDAL_X = 1;
var LB_MEDAL_Y = -6;
// Titles for the modes when Lua sends none (the Farmers Market doesn't).
var LB_MODE_TITLES = [undefined,"Jobs","Pets","Collections","Recipes","Vehicles","Farmers Market"];
// Bar colours: light, mid and dark.
var LB_BAR_GOLD = [0xFFF1B8,0xFFC531,0xD98A10];
var LB_BAR_GREEN = [0xC4F59A,0x5DBE35,0x2E7A16];
var LB_BAR_AMBER = [0xFFE3A3,0xF5A623,0xB86A0C];
var LB_BAR_RED = [0xFFA394,0xEC4A36,0xA8261A];

var lbFrame;
var lbClose;

// --- drawing helpers -------------------------------------------------------------------

function lbRoundRect(mc, x, y, w, h, r)
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

function lbGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   lbRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function lbCircle(mc, x, y, r)
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

// A gold disc (close button, pager arrows, rank badge, the selected card's check).
function lbGoldDisc(mc, x, y, r, over)
{
   mc.lineStyle(1.5,0xFFF3C4,100);
   mc.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x - r,y:y - r,w:r * 2,h:r * 2,r:Math.PI / 2});
   lbCircle(mc,x,y,r);
   mc.endFill();
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:x - r,y:y - r,w:r * 2,h:r,r:Math.PI / 2});
   lbRoundRect(mc,x - r * 0.65,y - r + 3,r * 1.3,r * 0.7,r * 0.35);
   mc.endFill();
}

function lbText(mc, name, txt, x, y, w, h, size, color, align)
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
   t.baseSize = size;
   return t;
}

// Sets a field's text on one line, shrinking it to fit and cutting it short past the smallest size.
function lbSetFitted(t, txt, color)
{
   var f = t.getTextFormat();
   f.size = t.baseSize;
   f.color = color;
   t.setNewTextFormat(f);
   t.text = txt;
   while(t.textWidth > t._width - 8 && f.size > 11)
   {
      f.size -= 1;
      t.setTextFormat(f);
   }
   while(t.textWidth > t._width - 8 && txt.length > 3)
   {
      txt = txt.substr(0,txt.length - 1);
      t.text = txt + "...";
      t.setTextFormat(f);
   }
}

// Restyles a stock text field in place.
function lbRestyle(t, size, color, align)
{
   var f = t.getTextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = align;
   t.setNewTextFormat(f);
   t.setTextFormat(f);
   t.filters = lbOutline();
}

function lbSparkle(mc, x, y, r, alpha)
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

function lbOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// A glossy rounded bar of percent (0-100) on its dark recessed track.
function lbDrawBar(mc, x, y, w, h, percent, colors)
{
   var r = h / 2;
   mc.lineStyle(1.5,LB_RIM,30);
   mc.beginGradientFill("linear",[0x050A24,0x0E1A52],[85,85],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   lbRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   var v = Math.max(0,Math.min(100,Number(percent)));
   if(isNaN(v) || v <= 0)
   {
      return;
   }
   var fw = Math.max(h,Math.round(w * v / 100));
   mc.lineStyle(1,colors[0],100);
   mc.beginGradientFill("linear",[colors[0],colors[1],colors[2]],[100,100,100],[0,110,255],{matrixType:"box",x:x,y:y,w:fw,h:h,r:Math.PI / 2});
   lbRoundRect(mc,x,y,fw,h,r);
   mc.endFill();
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[60,5],[0,255],{matrixType:"box",x:x,y:y + 1,w:fw,h:h * 0.42,r:Math.PI / 2});
   lbRoundRect(mc,x + r * 0.5,y + 1,Math.max(fw - r,1),h * 0.42,h * 0.2);
   mc.endFill();
}

// --- frame -----------------------------------------------------------------------------

// The frame's size follows the browser's (resize picks one of three); the grid's recess follows the list.
function lbLayout()
{
   var size = browser.sizeArray[browser.sizeID == undefined ? 0 : browser.sizeID];
   var w = size.width;
   var left = - w / 2;
   var pages = browser.pages;
   var bottom = pages._y + 66;
   var view = browser.view;
   var s = view._xscale / 100;
   var gap = view.columnWidth - 213;
   var gx = view._x - 14;
   var gw = (view.columns * view.columnWidth - gap) * s + 28;
   var gy = view._y - 8;

   var bg = lbFrame.bg;
   bg.clear();
   lbGradientRect(bg,left,LB_TOP,w,bottom - LB_TOP,30,0x2E4FB0,0x141F55,100,LB_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:left - 200,y:LB_TOP - 380,w:w,h:780,r:0});
   lbRoundRect(bg,left + 2,LB_TOP + 2,w - 4,bottom - LB_TOP - 4,28);
   bg.endFill();

   // The header band, with a few sparkles (fixed seed, same every time) and a gold rule under the titles.
   bg.beginFill(0x0A1440,30);
   lbRoundRect(bg,left + 8,LB_TOP + 8,w - 16,LB_RULE_Y - LB_TOP - 4,24);
   bg.endFill();
   var r = 6161;
   var i;
   for(i = 0; i < 26; i++)
   {
      r = (r * 1103515245 + 12345) % 2147483648;
      var x = left + 40 + r % (w - 80);
      r = (r * 1103515245 + 12345) % 2147483648;
      lbSparkle(bg,x,LB_TOP + 14 + r % 80,1 + r % 3,25 + r % 50);
   }
   bg.beginGradientFill("linear",[LB_GOLD,LB_GOLD,LB_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:-170,y:0,w:340,h:2,r:0});
   lbRoundRect(bg,-170,LB_RULE_Y,340,2,1);
   bg.endFill();

   // The recess the cards sit in.
   lbGradientRect(bg,gx,gy,gw,pages._y - 6 - gy,26,0x0A1440,0x0A1440,35,LB_RIM,22,1);

   lbClose._x = w / 2 - 40;
   lbClose._y = LB_TOP + 40;
   lbFrame.fallback_txt._x = browser.title._x;
   lbFrame.fallback_txt._y = browser.title._y;
}

function lbDrawClose(over)
{
   var a = lbClose.art;
   a.clear();
   lbGoldDisc(a,0,0,17,over);
   a.lineStyle(3.5,LB_INK,100);
   a.moveTo(-6,-6);
   a.lineTo(6,6);
   a.moveTo(6,-6);
   a.lineTo(-6,6);
   lbClose.filters = over ? [new flash.filters.GlowFilter(LB_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.6,6,6,1,2)];
}

// The stock close button (a beige X at the bottom) is shrunk away; this one does what its release did
// (browser.onClose: CloseWindow, which hides the Farmers Market or compresses the browser).
function lbBuildClose()
{
   browser.buttonHolder._alpha = 0;
   var c = browser.closeBtn;
   c.UP._alpha = c.HOVER._alpha = c.DOWN._alpha = c.ICON_UP._alpha = c.ICON_HOVER._alpha = 0;
   c._xscale = c._yscale = 0;

   lbClose = browser.createEmptyMovieClip("sanctuaryClose",browser.getNextHighestDepth());
   lbClose.art = lbClose.createEmptyMovieClip("art",1);
   lbDrawClose(false);
   lbClose.onRollOver = function()
   {
      lbDrawClose(true);
      fscommand("FR_event","PlaySound(ButtonOver)");
      gs.TweenLite.to(this,0.6,{_xscale:115,_yscale:115,ease:mx.transitions.easing.Elastic.easeOut});
   };
   lbClose.onRollOut = lbClose.onReleaseOutside = function()
   {
      lbDrawClose(false);
      gs.TweenLite.to(this,0.3,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
   };
   lbClose.onPress = function()
   {
      fscommand("FR_event","PlaySound(ButtonPress)");
      gs.TweenLite.to(this,0.08,{_xscale:96,_yscale:96});
   };
   lbClose.onRelease = function()
   {
      lbDrawClose(false);
      gs.TweenLite.to(this,0.3,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
      browser.onClose();
   };
}

// --- header ----------------------------------------------------------------------------

function lbStyleTitles()
{
   var t = browser.title;
   t._x = -260;
   t._y = 3;
   t._width = 520;
   t._height = 38;
   lbRestyle(t,26,0xFFE08A,"center");
   var s = browser.subText;
   s._x = -260;
   s._y = 35;
   s._width = 520;
   s._height = 24;
   lbRestyle(s,15,LB_SOFT,"center");
   // frSetTextPreserveWidth keeps each field's first size and bounds; it should keep these.
   delete TFDim[String(t)];
   delete TFDim[String(s)];
   lbFrame.fallback_txt = lbText(lbFrame,"fallback_txt","",t._x,t._y,t._width,t._height,26,0xFFE08A);
   lbFrame.fallback_txt.filters = lbOutline();
}

// The drop-down handle (root icon): a navy disc with a gold rim around a frosted socket for the mode's emblem.
// The class browser keeps its stock job badge plate instead.
function lbDrawMedal()
{
   var m = icon.sanctuaryMedal;
   var mode = browser.getMode();
   m.clear();
   m.seeds.removeMovieClip();
   if(mode == 1)
   {
      return;
   }
   m.lineStyle(3,LB_GOLD,100);
   m.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:LB_MEDAL_X - 50,y:LB_MEDAL_Y - 50,w:100,h:100,r:Math.PI / 2});
   lbCircle(m,LB_MEDAL_X,LB_MEDAL_Y,50);
   m.endFill();
   m.lineStyle(1.5,0xFFFFFF,80);
   m.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:LB_MEDAL_X - 52,y:LB_MEDAL_Y - 58,w:104,h:104,r:0});
   lbCircle(m,LB_MEDAL_X,LB_MEDAL_Y,42);
   m.endFill();
   if(mode == 6)
   {
      var seeds = m.attachMovie("sanctuaryMarketSeeds","seeds",1);
      seeds._width = seeds._height = 60;
      seeds._x = LB_MEDAL_X - 30;
      seeds._y = LB_MEDAL_Y - 30;
   }
}

function lbBuildMedal()
{
   var m = icon.createEmptyMovieClip("sanctuaryMedal",-16384);
   m.filters = [new flash.filters.GlowFilter(LB_GOLD,0.45,12,12,2,2),new flash.filters.DropShadowFilter(4,90,0x050A24,0.6,10,10,1,2)];
   lbDrawMedal();
}

// --- pager -----------------------------------------------------------------------------

// The stock PageController stays as the hit areas (previous on its left half, next on its right) and keeps
// paging; it's invisible, and a navy capsule with gold arrow discs and the page label follows it.
function lbBuildPager()
{
   var p = browser.pages;
   p._alpha = 0;
   var g = lbFrame.pager = lbFrame.createEmptyMovieClip("pager",5);
   g.art = g.createEmptyMovieClip("art",1);
   g.art.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,6,6,1,2)];
   g.label_txt = lbText(g,"label_txt","",44,9,90,26,18,0xFFFFFF);
   g.label_txt.filters = lbOutline();

   var arrows = new Object();
   arrows.state = function(type, data)
   {
      if(type == "rollover" || type == "dragover" || type == "press")
      {
         lbFrame.pager.over = data.target._name;
      }
      else if(type == "rollout" || type == "dragout" || type == "releaseoutside")
      {
         lbFrame.pager.over = undefined;
      }
      lbFrame.pager.key = undefined;
   };
   p.previous.addListener(arrows);
   p.next.addListener(arrows);
}

function lbDrawPager()
{
   var p = browser.pages;
   var g = lbFrame.pager;
   var key = p._x + "," + p._y + "," + p.value + "," + p.min + "," + p.max + "," + g.over + "," + p._visible + "," + p.label.text;
   if(key == g.key)
   {
      return;
   }
   g.key = key;
   g._x = p._x;
   g._y = p._y;
   g._visible = p._visible;
   var a = g.art;
   a.clear();
   a.lineStyle(1.5,LB_RIM,45);
   a.beginGradientFill("linear",[0x1D307A,0x111C4F],[100,100],[0,255],{matrixType:"box",x:0,y:-1,w:178,h:46,r:Math.PI / 2});
   lbRoundRect(a,-2,-1,182,46,23);
   a.endFill();
   lbDrawArrow(a,22,p.value > p.min,g.over == "previous",-1);
   lbDrawArrow(a,156,p.value < p.max,g.over == "next",1);
   g.label_txt.text = p.label.text;
}

function lbDrawArrow(g, x, enabled, over, dir)
{
   var y = 22;
   if(enabled)
   {
      lbGoldDisc(g,x,y,over ? 18 : 17,over);
      g.lineStyle(3.5,LB_INK,100);
   }
   else
   {
      g.lineStyle(1.5,LB_RIM,40);
      g.beginFill(0x284A9E,60);
      lbCircle(g,x,y,17);
      g.endFill();
      g.lineStyle(3.5,LB_RIM,45);
   }
   g.moveTo(x - 3 * dir,y - 7);
   g.lineTo(x + 4 * dir,y);
   g.lineTo(x - 3 * dir,y + 7);
}

// --- cards -----------------------------------------------------------------------------

// How many of a recipe's ingredients you have, of how many. AddRecipe passes only the ones you have;
// AddFarmersMarketRecipe passes a has-it flag for each.
function lbIngredients(item)
{
   var count = Number(item.componentsCount);
   var list = item.components;
   var have = 0;
   var i;
   if(browser.getMode() == 6)
   {
      for(i = 0; i < list.length; i++)
      {
         if(Number(list[i]) > 0 || list[i] === true || String(list[i]).toLowerCase() == "true")
         {
            have++;
         }
      }
   }
   else
   {
      have = list.length;
   }
   return {have:have,count:isNaN(count) ? 0 : count};
}

function lbCollected(item)
{
   var list = item.collectables;
   var have = 0;
   var i;
   for(i = 0; i < list.length; i++)
   {
      if(list[i] == "1")
      {
         have++;
      }
   }
   return {have:have,count:list.length};
}

function lbStyleBadge(b, kind)
{
   if(b.lbCard != undefined)
   {
      return;
   }
   b.lbKind = kind;
   // The stock plates (the states clip is the hit area: it stays, invisible), the selection glint, the
   // members-only icon and the stock text and bars go; the icon, the pet's mood face and the class's
   // experience clip (its stock code needs it) stay where they are.
   b.states._alpha = 0;
   b.iconBackground._alpha = 0;
   b.SELECTION._alpha = 0;
   b.membersOnlyIcon._alpha = 0;
   b.membersOnlyIcon.enabled = false;
   b.membersOnly_mc._visible = false;
   b.componentsBar._alpha = b.collectionBar._alpha = 0;
   b.experience._alpha = 0;
   b.moodBar.petProg._alpha = 0;
   b.label._visible = false;
   b.status._visible = false;

   var card = b.lbCard = b.createEmptyMovieClip("sanctuaryCard",-16384);
   card.bg = card.createEmptyMovieClip("bg",1);
   card.socket = card.createEmptyMovieClip("socket",2);
   // The socket centers on the icon (64px stock art at the icon's scale).
   card.socket._x = b.icon._x + 32 * b.icon._xscale / 100;
   card.socket._y = b.icon._y + 32 * b.icon._yscale / 100;
   card.bar = card.createEmptyMovieClip("bar",3);
   card.name_txt = lbText(card,"name_txt","",LB_CARD_X + 6,LB_NAME_Y,LB_CARD_W - 12,26,17,0xFFFFFF);
   card.name_txt.filters = lbOutline();
   card.status_txt = lbText(card,"status_txt","",LB_CARD_X + 6,LB_STATUS_Y,LB_CARD_W - 12,20,13,LB_SOFT);
   card.status_txt.filters = lbOutline();
   card.rank_txt = lbText(card,"rank_txt","",LB_BAR_X - 4,LB_BAR_Y - 9,28,22,14,LB_INK);

   // The selected card's check, top left.
   var chip = b.lbChip = b.createEmptyMovieClip("sanctuaryChip",b.getNextHighestDepth());
   lbGoldDisc(chip,LB_CARD_X + 16,LB_CARD_Y + 16,13,false);
   chip.lineStyle(3,LB_INK,100);
   chip.moveTo(LB_CARD_X + 10,LB_CARD_Y + 16);
   chip.lineTo(LB_CARD_X + 14,LB_CARD_Y + 21);
   chip.lineTo(LB_CARD_X + 22,LB_CARD_Y + 11);
   chip.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];

   b.addListener(lbBadgeListener);
}

function lbDrawBadge(b)
{
   var card = b.lbCard;
   if(card == undefined)
   {
      return;
   }
   var item = b.item;
   var kind = b.lbKind;
   var over = b.lbOver == true;
   var sel = b.SELECTION._visible == true;
   var gold = over || sel;
   // A recipe the factory can't make yet (stock dims its icon), a job not learned yet.
   var dim = b.icon._alpha < 60 || (kind == "class" && Number(item.rank) == 0);
   var x = LB_CARD_X;
   var y = LB_CARD_Y;
   var w = LB_CARD_W;
   var h = LB_CARD_H;
   var bg = card.bg;
   bg.clear();
   lbGradientRect(bg,x,y,w,h,18,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : (sel ? LB_GOLD : LB_RIM),gold ? 100 : 45,sel ? 3 : 2);
   bg.lineStyle();
   bg.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[over ? 22 : 14,0],[0,255],{matrixType:"box",x:0,y:y + 3,w:10,h:60,r:Math.PI / 2});
   lbRoundRect(bg,x + 3,y + 3,w - 6,60,15);
   bg.endFill();
   // The strip under the art.
   bg.beginFill(0x0A1440,over ? 30 : 45);
   bg.moveTo(x + 2,LB_NAME_Y - 2);
   bg.lineTo(x + w - 2,LB_NAME_Y - 2);
   bg.lineTo(x + w - 2,y + h - 16);
   bg.curveTo(x + w - 2,y + h - 2,x + w - 16,y + h - 2);
   bg.lineTo(x + 16,y + h - 2);
   bg.curveTo(x + 2,y + h - 2,x + 2,y + h - 16);
   bg.lineTo(x + 2,LB_NAME_Y - 2);
   bg.endFill();
   bg.lineStyle(1,gold ? 0xFFE08A : LB_RIM,gold ? 70 : 25);
   bg.moveTo(x + 12,LB_NAME_Y - 2);
   bg.lineTo(x + w - 12,LB_NAME_Y - 2);
   if(over)
   {
      bg.filters = [new flash.filters.GlowFilter(LB_GOLD,0.6,16,16,2,2)];
   }
   else if(sel)
   {
      bg.filters = [new flash.filters.GlowFilter(LB_GOLD,0.4,10,10,2,2)];
   }
   else
   {
      bg.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,8,8,1,2)];
   }

   // The frosted socket behind the icon (stock icons are made for light plates).
   var s = card.socket;
   var r = LB_SOCKET / 2;
   s.clear();
   s.lineStyle(gold ? 2.5 : 2,gold ? 0xFFE08A : 0xFFFFFF,gold ? 100 : 85);
   s.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:- r - 14,y:- r - 20,w:LB_SOCKET + 28,h:LB_SOCKET + 28,r:0});
   lbRoundRect(s,- r,- r,LB_SOCKET,LB_SOCKET,16);
   s.endFill();
   if(dim)
   {
      s.lineStyle();
      s.beginFill(0x0A1440,40);
      lbRoundRect(s,- r,- r,LB_SOCKET,LB_SOCKET,16);
      s.endFill();
   }

   var name = item.name == undefined ? "" : String(item.name);
   lbSetFitted(card.name_txt,name,dim ? 0x8FA3D8 : (gold ? 0xFFE08A : 0xFFFFFF));
   lbDrawTracker(b,card,item,kind,dim);
   b.lbChip._visible = sel;
}

// The second line and bar: what the card tracks.
function lbDrawTracker(b, card, item, kind, dim)
{
   var bar = card.bar;
   var status = "";
   var rank = "";
   bar.clear();
   if(kind == "recipe")
   {
      var ing = lbIngredients(item);
      status = String(b.status.text);
      if(browser.getMode() == 6)
      {
         // The stock line counts every ingredient as had; this counts the ones you have.
         status = _root.STRING_INGREDIENTS + " " + ing.have + " " + _root.STRING_OF + " " + ing.count;
         b.status.text = status;
      }
      if(ing.count > 0)
      {
         lbDrawBar(bar,LB_BAR_X,LB_BAR_Y,LB_BAR_W,9,ing.have / ing.count * 100,ing.have >= ing.count ? LB_BAR_GOLD : LB_BAR_GREEN);
      }
   }
   else if(kind == "collection")
   {
      var col = lbCollected(item);
      status = String(b.status.text);
      if(col.count > 0)
      {
         lbDrawBar(bar,LB_BAR_X,LB_BAR_Y,LB_BAR_W,9,col.have / col.count * 100,LB_BAR_GOLD);
      }
   }
   else if(kind == "vehicle")
   {
      status = item.components == undefined ? "" : String(b.status.text);
   }
   else if(kind == "pet")
   {
      // The stock mood face stays at the left of the bar.
      if(b.moodBar._visible)
      {
         var mood = Number(item.mood);
         lbDrawBar(bar,b.moodBar._x + 30,b.moodBar._y + 6,LB_BAR_X + LB_BAR_W - b.moodBar._x - 30,10,mood,mood > 66 ? LB_BAR_GREEN : (mood > 33 ? LB_BAR_AMBER : LB_BAR_RED));
      }
   }
   else if(kind == "class")
   {
      var n = Number(item.rank);
      if(n > 0)
      {
         lbGoldDisc(bar,LB_BAR_X + 10,LB_BAR_Y + 2,13,false);
         rank = String(n);
         lbDrawBar(bar,LB_BAR_X + 28,LB_BAR_Y - 3,LB_BAR_W - 28,10,n >= 20 ? 100 : Number(item.exp),LB_BAR_GOLD);
         status = n >= 20 ? "MAX" : "";
      }
      else
      {
         status = "Not learned yet";
      }
   }
   lbSetFitted(card.status_txt,status,dim ? 0x8FA3D8 : LB_SOFT);
   card.rank_txt.text = rank;
   card.rank_txt.textColor = LB_INK;
}

var lbBadgeListener = new Object();
lbBadgeListener.state = function(type, data)
{
   var b = data.target;
   if(b.lbCard == undefined)
   {
      return;
   }
   switch(type)
   {
      case "rollover":
      case "dragover":
         b.lbOver = true;
         lbDrawBadge(b);
         b.lbCard.socket._xscale = b.lbCard.socket._yscale = 100;
         gs.TweenLite.to(b.lbCard.socket,0.6,{_xscale:108,_yscale:108,ease:mx.transitions.easing.Elastic.easeOut});
         break;
      case "rollout":
      case "dragout":
      case "releaseoutside":
         b.lbOver = false;
         lbDrawBadge(b);
         gs.TweenLite.to(b.lbCard.socket,0.3,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
         break;
      case "release":
         // The pick may select the card (SelectItem deactivates it).
         lbDrawBadge(b);
   }
};

function lbWrapSetItem(proto, kind)
{
   var setItem = proto.setItem;
   proto.setItem = function(obj)
   {
      var result = setItem.apply(this,arguments);
      lbStyleBadge(this,kind);
      // Membership no longer exists.
      this.membersOnlyIcon._alpha = 0;
      this.membersOnlyIcon.enabled = false;
      this.membersOnly_mc._visible = false;
      lbDrawBadge(this);
      return result;
   };
}

// The selected badge is deactivated (the summoned pet, the current job, the picked recipe).
function lbWrapSelection(proto)
{
   var activate = proto.activate;
   proto.activate = function()
   {
      var result = activate.apply(this,arguments);
      lbDrawBadge(this);
      return result;
   };
   var deactivate = proto.deactivate;
   proto.deactivate = function()
   {
      var result = deactivate.apply(this,arguments);
      lbDrawBadge(this);
      return result;
   };
}

function lbPatchBadges()
{
   lbWrapSetItem(fr.badges.RecipeBadge.prototype,"recipe");
   lbWrapSetItem(fr.badges.CollectionBadge.prototype,"collection");
   lbWrapSetItem(fr.badges.VehicleBadge.prototype,"vehicle");
   lbWrapSetItem(fr.badges.PetBadge.prototype,"pet");
   lbWrapSetItem(fr.badges.ClassBadge.prototype,"class");
   lbWrapSelection(fr.core.Badge.prototype);
   lbWrapSelection(fr.core.Badge2.prototype);
   lbWrapSelection(fr.badges.RecipeBadge.prototype);
   lbWrapSelection(fr.badges.CollectionBadge.prototype);
   lbWrapSelection(fr.badges.VehicleBadge.prototype);
}

// --- build -----------------------------------------------------------------------------

function lbBuild()
{
   // The stock panels (loaded images) and the title arch go; background stays as the panel's hit area.
   browser.background.holder._alpha = 0;
   browser.background.holder2._alpha = 0;
   browser.decoration._alpha = 0;

   lbFrame = browser.createEmptyMovieClip("sanctuaryFrame",-16384);
   lbFrame.bg = lbFrame.createEmptyMovieClip("bg",1);
   lbFrame.bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   browser.view._y += LB_VIEW_SHIFT;
   lbBuildClose();
   lbStyleTitles();
   lbBuildPager();
   lbBuildMedal();
   lbLayout();

   // What stock code shows and hides on its own: the close button, the pager, and the title Lua may not send.
   lbFrame.onEnterFrame = function()
   {
      lbClose._visible = browser.closeBtn._visible;
      lbDrawPager();
      var fallback = browser.title.text.length == 0 ? LB_MODE_TITLES[browser.getMode()] : undefined;
      fallback = fallback == undefined ? "" : fallback;
      if(this.fallback_txt.text != fallback)
      {
         this.fallback_txt.text = fallback;
      }
   };
}

// resize (one of three sizes) lays the list and pager out again.
var lbResize = Browser.prototype.resize;
Browser.prototype.resize = function(value)
{
   var result = lbResize.apply(this,arguments);
   this.view._y += LB_VIEW_SHIFT;
   lbLayout();
   return result;
};

// The class browser moves the list over for its wider panel.
// Every Add* call sets the mode again, so the frame is only redrawn when it changes.
var lbMode;
var lbSetMode = SetMode;
SetMode = function(value)
{
   var result = lbSetMode.apply(_root,arguments);
   var mode = browser.getMode();
   if(mode != lbMode)
   {
      lbMode = mode;
      lbLayout();
      lbDrawMedal();
   }
   return result;
};

// Everything members-only is everyone's: nothing is greyed out or sent to the membership page.
_global.isMember = true;
setMemberStatus = function(value)
{
   _global.isMember = true;
};

lbPatchBadges();
lbBuild();
// ===== End Sanctuary List Browser =====

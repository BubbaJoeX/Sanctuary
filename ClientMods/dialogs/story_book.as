// ===== Sanctuary Adventurer's Journal =====
// Appended to StoryBook's main timeline by build_client_mods.py.
// The Adventurer's Journal (wndStoryBook, the home screen's Journal link: OpenHerosJournal) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as) without losing its book. The parchment pages stay, so their dark ink stays
// readable, while the cover around them is repainted at build time as navy glass with a gold rim, gold corner guards
// and ivory page edges (RESKINS "book"), and the plum photo corners on the maps turn gold ("photo_corners"). Here the
// region tabs become navy glass tabs (the open one gold) with their icon in a frosted socket, the close button is the
// gold circle, a navy label on the spine carries the title, and the headings sit on navy ribbons in gold. On the
// pages the stickers are framed in gold plates, the quest markers sit in gold-ringed medallions (the active one
// glowing, finished ones with a gold check), objectives are cream cards ruled in gold, and the other quests are navy
// glass rows (RESKINS "navy") with a gold rim when chosen, a gold track radio and a red drop circle. Every stock clip
// keeps its role (hit areas, tooltips, Lua callbacks); the drawing goes under them or into clips without handlers.

var SB_GOLD = 0xFFC531;
var SB_RIM = 0x9DB8FF;
var SB_SOFT = 0xC9D6FF;
var SB_INK = 0x3A2400;
// Ink on the parchment: the stock black turned to a deep navy, and a softer one for descriptions.
var SB_PAGE_INK = 0x1A2456;
var SB_PAGE_SOFT = 0x3A3656;
// The book (StoryBook's root clip at 0,0): the gutter between the pages and the bottom of the cover.
var SB_GUTTER_X = 503;
var SB_BOTTOM = 766;
// A region tab (PageTab: 67x45, its icon 32px at 10.5,7.45).
var SB_TAB_W = 66;
var SB_TAB_H = 45;
var SB_TAB_ICON_X = 10.5;
var SB_TAB_ICON_Y = 7.45;

// The book, the open page's number (a tab Lua marks selected, then the book's current page after each page change)
// and the tabs styled so far.
var sbBook;
var sbPage;
var sbTabs = new Array();

// --- drawing helpers -------------------------------------------------------------------

function sbRoundRect(mc, x, y, w, h, r)
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

function sbGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   sbRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

// The soft white shine across the top of a card.
function sbGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   sbRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function sbCircle(mc, x, y, r)
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

function sbSparkle(mc, x, y, r, alpha)
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

// A frosted socket for icon art (stock icons are made for light plates).
function sbSocket(mc, x, y, r, rim, rimAlpha)
{
   mc.lineStyle(rim == undefined ? 1 : 2,rim == undefined ? 0xFFFFFF : rim,rim == undefined ? 60 : rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.3,y:y - r * 1.45,w:r * 2.6,h:r * 2.6,r:0});
   sbCircle(mc,x,y,r);
   mc.endFill();
}

// A gold chip with a dark check, for finished quests.
function sbCheck(mc, x, y, r)
{
   mc.lineStyle(Math.max(1,r * 0.18),0xFFF3C4,100);
   mc.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x - r,y:y - r,w:2 * r,h:2 * r,r:Math.PI / 2});
   sbCircle(mc,x,y,r);
   mc.endFill();
   mc.lineStyle(Math.max(2,r * 0.3),SB_INK,100);
   mc.moveTo(x - r * 0.45,y + r * 0.02);
   mc.lineTo(x - r * 0.1,y + r * 0.38);
   mc.lineTo(x + r * 0.5,y - r * 0.35);
}

// A navy ribbon centered on cx with swallowtail ends folded behind it, for a heading in gold.
function sbRibbon(mc, cx, y, w, h)
{
   var x = Math.round(cx - w / 2);
   var t = Math.round(h * 0.55);
   var i;
   mc.lineStyle(1,SB_RIM,40);
   for(i = 0; i < 2; i++)
   {
      // The folded tail (left, then mirrored right), a little lower and darker.
      var dir = i == 0 ? -1 : 1;
      var ex = i == 0 ? x : x + w;
      mc.beginFill(0x101A48,100);
      mc.moveTo(ex - dir * 10,y + 6);
      mc.lineTo(ex + dir * 22,y + 6);
      mc.lineTo(ex + dir * 12,y + 6 + h / 2);
      mc.lineTo(ex + dir * 22,y + 6 + h);
      mc.lineTo(ex - dir * 10,y + 6 + h);
      mc.endFill();
   }
   sbGradientRect(mc,x,y,w,h,Math.min(8,h / 3),0x2E4FB0,0x141F55,100,0xFFE08A,90,1.5);
   sbGloss(mc,x + 6,y + 2,w - 12,t,t / 2,28);
   mc.lineStyle(1,0xFFE08A,40);
   mc.moveTo(x + 6,y + h - 4);
   mc.lineTo(x + w - 6,y + h - 4);
}

// A gold pill (counts and badges), lit from the top.
function sbGoldPill(mc, x, y, w, h)
{
   mc.lineStyle(1.5,0xFFF3C4,100);
   mc.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   sbRoundRect(mc,x,y,w,h,h / 2);
   mc.endFill();
   sbGloss(mc,x + h / 3,y + 2,w - h / 1.5,h * 0.42,h * 0.2,45);
}

function sbText(mc, name, txt, x, y, w, h, size, color, align)
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

function sbOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Recolours a stock field (keeping its font and size). Light text on navy gets the dark outline; ink on the
// parchment loses the stock glows.
function sbColor(t, color, outlined)
{
   if(t == undefined)
   {
      return;
   }
   t.textColor = color;
   var f = t.getTextFormat();
   f.color = color;
   t.setNewTextFormat(f);
   t.filters = outlined ? sbOutline() : [];
}

// Penner's elastic and strong eases.
function sbElasticOut(t, b, c, d)
{
   if(t == 0)
   {
      return b;
   }
   t /= d;
   if(t >= 1)
   {
      return b + c;
   }
   var p = d * 0.3;
   return c * Math.pow(2,-10 * t) * Math.sin((t * d - p / 4) * (2 * Math.PI) / p) + c + b;
}

function sbStrongOut(t, b, c, d)
{
   t = t / d - 1;
   return c * (t * t * t * t * t + 1) + b;
}

// A gfx button's hover, as rollOver/rollOut events (its state frames skip them while it has focus). Called again on
// each restyle, it hooks a button once, as soon as it's constructed.
function sbOnHover(button, owner, callback)
{
   if(button.sbHooked || button.addEventListener == undefined)
   {
      return;
   }
   button.sbHooked = true;
   var lo = new Object();
   lo.owner = owner;
   lo.over = function()
   {
      callback(this.owner,true);
   };
   lo.out = function()
   {
      callback(this.owner,false);
   };
   button.addEventListener("rollOver",lo,"over");
   button.addEventListener("rollOut",lo,"out");
   button.addEventListener("dragOut",lo,"out");
   button.addEventListener("releaseOutside",lo,"out");
}

// --- close button and spine label ------------------------------------------------------

function sbDrawClose(c, over)
{
   c.clear();
   c.lineStyle(1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-17,y:-17,w:34,h:34,r:Math.PI / 2});
   sbCircle(c,0,0,17);
   c.endFill();
   sbGloss(c,-11,-14,22,11,5,50);
   c.lineStyle(4,SB_INK,100);
   c.moveTo(-6,-6);
   c.lineTo(6,6);
   c.moveTo(6,-6);
   c.lineTo(-6,6);
   c.filters = over ? [new flash.filters.GlowFilter(SB_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

function sbHoverClose(c, over)
{
   sbDrawClose(c,over);
   gs.TweenLite.to(c,over ? 0.7 : 0.25,{_xscale:over ? 115 : 100,_yscale:over ? 115 : 100,_rotation:over ? 90 : 0,ease:over ? sbElasticOut : sbStrongOut});
}

// The gold circle under the stock X (exitBookButton, a gfx Button at the right page's corner): the button stays on
// top, invisible, and keeps its click (Hide()).
function sbBuildClose(book)
{
   var exit = book.exitBookButton;
   var b = exit.getBounds(book);
   // Below exitBookButton (timeline depth 18), above the pages' art and the page turn (depths 1 to 7).
   var c = book.createEmptyMovieClip("sbClose",17 - 16384);
   c._x = b.xMax > b.xMin ? Math.round((b.xMin + b.xMax) / 2) : 958;
   c._y = b.yMax > b.yMin ? Math.round((b.yMin + b.yMax) / 2) : 32;
   sbDrawClose(c,false);
   exit._alpha = 0;
   sbOnHover(exit,c,sbHoverClose);
}

// The title on a navy label at the foot of the spine, where the cover shows under the pages.
function sbBuildLabel(book)
{
   var w = 216;
   var h = 26;
   var l = book.createEmptyMovieClip("sbLabel",book.getNextHighestDepth());
   l._x = SB_GUTTER_X - w / 2;
   l._y = SB_BOTTOM - h;
   sbGradientRect(l,0,0,w,h,h / 2,0x2E4FB0,0x141F55,100,0xFFE08A,100,1.5);
   sbGloss(l,8,2,w - 16,h * 0.45,h * 0.2,30);
   var s = 3131;
   var i;
   for(i = 0; i < 8; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 14 + s % (w - 28);
      s = (s * 1103515245 + 12345) % 2147483648;
      sbSparkle(l,x,5 + s % (h - 10),1 + s % 2,25 + s % 40);
   }
   var t = sbText(l,"title_txt","Adventurer's Journal",0,2,w,h,16,0xFFE08A);
   t.filters = sbOutline();
   l.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2),new flash.filters.GlowFilter(SB_GOLD,0.25,10,10,2,2)];
}

// --- region tabs -----------------------------------------------------------------------

function sbTabSelected(tab)
{
   return tab.pageNumber == (sbPage != undefined ? sbPage : sbBook.m_currentPage);
}

// A navy glass tab over the stock ribbon (or gold for the open page), with the region's icon in a frosted socket.
function sbDrawTab(tab)
{
   var g = tab.sbArt;
   var sel = sbTabSelected(tab);
   var over = tab.sbOver;
   g.clear();
   // The tab comes out of the book: square on the left, rounded on the right.
   var top = sel ? 0xFFE08A : (over ? 0x3F66D6 : 0x284A9E);
   var bottom = sel ? 0xF5A623 : (over ? 0x1D307A : 0x162660);
   g.lineStyle(sel || over ? 2 : 1.5,sel ? 0xFFF3C4 : (over ? 0xFFE08A : SB_RIM),sel || over ? 100 : 55);
   g.beginGradientFill("linear",[top,bottom],[100,100],[0,255],{matrixType:"box",x:0,y:2,w:SB_TAB_W,h:SB_TAB_H - 4,r:Math.PI / 2});
   g.moveTo(1,2);
   g.lineTo(SB_TAB_W - 14,2);
   g.curveTo(SB_TAB_W,2,SB_TAB_W,16);
   g.lineTo(SB_TAB_W,SB_TAB_H - 16);
   g.curveTo(SB_TAB_W,SB_TAB_H - 2,SB_TAB_W - 14,SB_TAB_H - 2);
   g.lineTo(1,SB_TAB_H - 2);
   g.lineTo(1,2);
   g.endFill();
   sbGloss(g,4,4,SB_TAB_W - 12,15,7,sel ? 45 : 22);
   // Where it tucks into the pages, a soft shadow.
   g.lineStyle();
   g.beginGradientFill("linear",[0x050A24,0x050A24],[45,0],[0,255],{matrixType:"box",x:1,y:0,w:10,h:10,r:0});
   g.moveTo(1,3);
   g.lineTo(11,3);
   g.lineTo(11,SB_TAB_H - 3);
   g.lineTo(1,SB_TAB_H - 3);
   g.lineTo(1,3);
   g.endFill();
   sbSocket(g,SB_TAB_ICON_X + 16,SB_TAB_ICON_Y + 16,17,sel ? 0xFFF3C4 : (over ? 0xFFE08A : undefined),100);
   // A small chevron for the open page.
   if(sel)
   {
      g.lineStyle(2.5,SB_INK,80);
      g.moveTo(50,17);
      g.lineTo(55,22.5);
      g.lineTo(50,28);
   }
   g.filters = sel || over ? [new flash.filters.GlowFilter(SB_GOLD,sel ? 0.6 : 0.45,12,12,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.55,6,6,1,2)];
}

function sbHoverTab(tab, over)
{
   tab.sbOver = over;
   sbDrawTab(tab);
   // The icon pops (scaled about its center).
   var s = over ? 46 : 40;
   var d = 80 * (s - 40) / 200;
   gs.TweenLite.to(tab.sbIcon,over ? 0.6 : 0.25,{_xscale:s,_yscale:s,_x:SB_TAB_ICON_X - d,_y:SB_TAB_ICON_Y - d,ease:over ? sbElasticOut : sbStrongOut});
}

function sbRedrawTabs()
{
   var kept = new Array();
   var i;
   for(i = 0; i < sbTabs.length; i++)
   {
      // removeMiscTab removes the misc tab's clip.
      if(sbTabs[i]._name != undefined)
      {
         sbDrawTab(sbTabs[i]);
         kept.push(sbTabs[i]);
      }
   }
   sbTabs = kept;
}

// After PageTabs.setData (the tab was just attached by populateTabs or addMiscTab). The art goes over the stock
// ribbon and its icon, inside the tab (it has no handlers, so the tab stays the hit area), and the icon is a copy of
// the stock bullet loaded with the same image.
function sbStyleTab(tab)
{
   if(tab.sbArt == undefined)
   {
      tab.sbArt = tab.createEmptyMovieClip("sbArt",1000);
      tab.sbIcon = tab.bullet.duplicateMovieClip("sbIcon",1001);
      sbTabs.push(tab);
   }
   sbOnHover(tab,tab,sbHoverTab);
   _root.frSetIconByIndex(tab.sbIcon.icon.holder,tab.iconId);
   var sel = tab.isSelected;
   if(sel == true || sel == 1 || sel == "1" || sel == "true")
   {
      sbPage = tab.pageNumber;
   }
   sbRedrawTabs();
}

// --- quest page ------------------------------------------------------------------------

// The chapter's title on a navy ribbon across the top of the map, sized to the title.
function sbDrawChapterRibbon(p)
{
   var t = p.chapter_title_txt;
   var g = p.sbChapterRibbon;
   g.clear();
   sbColor(t,0xFFE08A,true);
   if(t.text == "" || t.text == undefined)
   {
      return;
   }
   var w = Math.min(t._width + 24,Math.max(140,t.textWidth + 56));
   sbRibbon(g,t._x + t._width / 2,t._y - 1,w,t._height + 2);
   g.filters = [new flash.filters.DropShadowFilter(3,90,0x2A1A08,0.45,6,6,1,2)];
}

// The quest's name on a navy pill between the map and its description (hidden with the name, for a summary).
function sbDrawQuestPill(p)
{
   var t = p.quest_title_txt;
   var g = p.sbQuestPill;
   g.clear();
   sbColor(t,0xFFE08A,true);
   if(!t._visible || t.text == "" || t.text == undefined)
   {
      return;
   }
   var w = Math.min(t._width + 10,Math.max(120,t.textWidth + 48));
   var h = t._height + 4;
   sbGradientRect(g,Math.round(t._x + (t._width - w) / 2),t._y - 3,w,h,h / 2,0x2E4FB0,0x141F55,100,0xFFE08A,90,1.5);
   sbGloss(g,Math.round(t._x + (t._width - w) / 2) + 8,t._y - 1,w - 16,h * 0.42,h * 0.2,28);
   g.filters = [new flash.filters.DropShadowFilter(3,90,0x2A1A08,0.4,6,6,1,2)];
}

function sbInkQuestPage(p)
{
   sbColor(p.chapter_story_txt,SB_PAGE_INK,false);
   sbColor(p.quest_line_description,SB_PAGE_INK,false);
   sbColor(p.hubSummery_txt,SB_PAGE_INK,false);
}

function sbStyleQuestPage(p)
{
   if(p == undefined || p.sbStyled)
   {
      return;
   }
   p.sbStyled = true;
   // Under the chapter title (timeline depth 16) and above the map (10) and its markers (13); under the quest title
   // (8) and description (7), above the objectives (5).
   p.sbChapterRibbon = p.createEmptyMovieClip("sbChapterRibbon",14 - 16384);
   p.sbQuestPill = p.createEmptyMovieClip("sbQuestPill",6 - 16384);
   p.chapterMap_bg.filters = [new flash.filters.DropShadowFilter(4,90,0x3A2A10,0.35,10,10,1,2)];
   sbInkQuestPage(p);
   sbDrawChapterRibbon(p);
   sbDrawQuestPill(p);
}

// --- other quests page -----------------------------------------------------------------

function sbDrawCount(m)
{
   var t = m.questCount_txt;
   var g = m.sbCount;
   g.clear();
   sbColor(t,SB_INK,false);
   var w = Math.max(64,t.textWidth + 34);
   sbGoldPill(g,Math.round(t._x + (t._width - w) / 2),t._y - 1,w,t._height + 2);
   g.filters = [new flash.filters.GlowFilter(SB_GOLD,0.4,10,10,2,2)];
}

function sbStyleMiscPage(m)
{
   if(m == undefined || m.sbStyled)
   {
      return;
   }
   m.sbStyled = true;
   // The headings' ribbons under their text (depths 7 and 8), above the objectives list (5); the count's pill under
   // the count (3).
   var r = m.createEmptyMovieClip("sbRibbons",6 - 16384);
   var heads = [m.otherQuests_txt,m.otherObjectives_txt];
   var i;
   for(i = 0; i < heads.length; i++)
   {
      if(heads[i] != undefined)
      {
         sbColor(heads[i],0xFFE08A,true);
         sbRibbon(r,heads[i]._x + heads[i]._width / 2,heads[i]._y - 2,Math.min(heads[i]._width,200),heads[i]._height + 4);
      }
   }
   r.filters = [new flash.filters.DropShadowFilter(3,90,0x2A1A08,0.45,6,6,1,2)];
   m.sbCount = m.createEmptyMovieClip("sbCount",2 - 16384);
   sbDrawCount(m);
   sbColor(m.quest_description_txt,SB_PAGE_INK,false);
   // The map (an unnamed clip at depth 10) is a photo on the page.
   m.getInstanceAtDepth(10 - 16384).filters = [new flash.filters.DropShadowFilter(4,90,0x3A2A10,0.35,10,10,1,2)];
}

// --- stickers, map markers and list rows -----------------------------------------------

// A sticker (StickerItemRenderer, 120x120 with the art at 13,12) on a cream plate framed in gold.
function sbDrawSticker(s)
{
   var g = s.sbPlate;
   var over = s.sbOver;
   g.clear();
   if(!s.data)
   {
      return;
   }
   sbGradientRect(g,6,6,108,108,16,0xFFFBEF,0xF2E2BE,100,over ? 0xFFC531 : 0xE0A53A,100,over ? 3 : 2);
   g.lineStyle(1,0xFFFFFF,70);
   sbRoundRect(g,10,10,100,100,13);
   sbGloss(g,12,8,96,36,14,40);
   g.filters = over ? [new flash.filters.GlowFilter(SB_GOLD,0.75,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x3A2A10,0.4,7,7,1,2)];
}

function sbHoverSticker(s, over)
{
   s.sbOver = over;
   sbDrawSticker(s);
   var k = over ? 104 : 96;
   var d = 100 * (k - 96) / 200;
   gs.TweenLite.to(s.holder,over ? 0.6 : 0.25,{_xscale:k,_yscale:k,_x:13 - d,_y:12 - d,ease:over ? sbElasticOut : sbStrongOut});
}

function sbStyleSticker(s)
{
   if(s.sbPlate == undefined)
   {
      s.sbPlate = s.createEmptyMovieClip("sbPlate",-16384);
   }
   sbOnHover(s,s,sbHoverSticker);
   sbDrawSticker(s);
}

// A quest marker on a chapter map (MapIcon, 100x100 drawn at about half size): a cream medallion in a gold ring, glowing
// for the active quest, with a gold check once it's done; quests not reached yet are faint.
function sbDrawMapIcon(icon)
{
   var g = icon.sbMedal;
   g.clear();
   if(!icon.data)
   {
      return;
   }
   var open = icon.isActive || icon.isComplete;
   var over = icon.sbOver && open;
   g.lineStyle(7,icon.isActive || over ? 0xFFC531 : (icon.isComplete ? 0xE0A53A : 0xB9A98A),open ? 100 : 60);
   g.beginGradientFill("radial",[0xFFFFFF,0xFFF6DC,0xF0DDB0],[open ? 90 : 50,open ? 85 : 45,open ? 80 : 40],[0,150,255],{matrixType:"box",x:-10,y:-14,w:120,h:120,r:0});
   sbCircle(g,50,50,52);
   g.endFill();
   if(icon.isComplete)
   {
      sbCheck(g,92,12,18);
   }
   if(icon.isActive)
   {
      g.filters = [new flash.filters.GlowFilter(SB_GOLD,0.9,22,22,3,2)];
   }
   else if(over)
   {
      g.filters = [new flash.filters.GlowFilter(SB_GOLD,0.7,16,16,2,2)];
   }
   else
   {
      g.filters = [new flash.filters.DropShadowFilter(3,90,0x3A2A10,0.35,6,6,1,2)];
   }
}

function sbHoverMapIcon(icon, over)
{
   icon.sbOver = over;
   sbDrawMapIcon(icon);
}

function sbStyleMapIcon(icon)
{
   if(icon.sbMedal == undefined)
   {
      icon.sbMedal = icon.createEmptyMovieClip("sbMedal",-16384);
   }
   sbOnHover(icon,icon,sbHoverMapIcon);
   sbDrawMapIcon(icon);
}

// An objective (ObjectiveListItemRenderer, a 72px row): a cream card ruled in gold on the parchment, the status icon
// in a gold-ringed socket, and the text in the page's ink.
function sbStyleObjective(o)
{
   if(o.sbCard == undefined)
   {
      o.sbCard = o.createEmptyMovieClip("sbCard",-16384);
   }
   var g = o.sbCard;
   g.clear();
   sbColor(o.textField,SB_PAGE_INK,false);
   sbColor(o.description_txt,SB_PAGE_SOFT,false);
   if(!o.data)
   {
      return;
   }
   sbGradientRect(g,0,1,402,66,14,0xFFFBEF,0xF6EAD0,55,0xD9A441,70,1);
   sbGloss(g,6,2,390,20,9,30);
   g.lineStyle(2,0xE0A53A,100);
   g.beginGradientFill("radial",[0xFFFFFF,0xFFF6DC,0xF0DDB0],[100,100,100],[0,150,255],{matrixType:"box",x:2,y:-3,w:44,h:44,r:0});
   sbCircle(g,22.5,17,18);
   g.endFill();
}

// The other quests' rows (ExtraQuestListRenderer, 389x44): the stock bar is navy glass (RESKINS), with the quest's
// icon in a frosted socket, a gold rim on hover and for the chosen quest, and the drop button as a red glass circle
// (the stock button stays on top, invisible, as its hit area).
function sbDrawQuestRow(r)
{
   var g = r.sbRow;
   var sel = r._selected;
   var over = r.sbOver;
   g.clear();
   sbColor(r.textField,0xFFE08A,true);
   if(!r.data)
   {
      return;
   }
   if(sel || over)
   {
      g.lineStyle(2,0xFFE08A,sel ? 100 : 80);
      g.beginGradientFill("linear",[SB_GOLD,SB_GOLD],[sel ? 26 : 12,0],[0,255],{matrixType:"box",x:0,y:0,w:389,h:44,r:0});
      sbRoundRect(g,1,1,387,42,10);
      g.endFill();
   }
   sbSocket(g,22.5,20,17,sel ? 0xFFE08A : undefined,100);
   g.filters = sel || over ? [new flash.filters.GlowFilter(SB_GOLD,sel ? 0.5 : 0.35,10,10,2,2)] : [];
}

function sbDrawDrop(r)
{
   var g = r.sbDrop;
   var b = r.dropQuestButton.getBounds(r);
   g.clear();
   if(!(b.xMax > b.xMin))
   {
      return;
   }
   var over = r.sbDropOver;
   var x = (b.xMin + b.xMax) / 2;
   var y = (b.yMin + b.yMax) / 2;
   g.lineStyle(1.5,over ? 0xFFE08A : 0xFFC9C9,100);
   g.beginGradientFill("linear",over ? [0xFF8A8A,0xD0283C] : [0xF06A6A,0xA81E30],[100,100],[0,255],{matrixType:"box",x:x - 12,y:y - 12,w:24,h:24,r:Math.PI / 2});
   sbCircle(g,x,y,12);
   g.endFill();
   sbGloss(g,x - 8,y - 10,16,8,4,45);
   g.lineStyle(3,0xFFFFFF,100);
   g.moveTo(x - 4,y - 4);
   g.lineTo(x + 4,y + 4);
   g.moveTo(x + 4,y - 4);
   g.lineTo(x - 4,y + 4);
   g.filters = over ? [new flash.filters.GlowFilter(0xFF6A6A,0.7,10,10,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,5,5,1,2)];
}

function sbHoverQuestRow(r, over)
{
   r.sbOver = over;
   sbDrawQuestRow(r);
}

function sbHoverDrop(r, over)
{
   r.sbDropOver = over;
   sbDrawDrop(r);
}

function sbStyleQuestRow(r)
{
   if(r.sbRow == undefined)
   {
      // Above the bar (timeline depth 3) and under the radio (5) and icon (9); the red circle under the name (11)
      // and the drop button (12).
      r.sbRow = r.createEmptyMovieClip("sbRow",4 - 16384);
      r.sbDrop = r.createEmptyMovieClip("sbDrop",10 - 16384);
      r.dropQuestButton._alpha = 0;
   }
   sbOnHover(r,r,sbHoverQuestRow);
   sbOnHover(r.dropQuestButton,r,sbHoverDrop);
   sbDrawQuestRow(r);
   sbDrawDrop(r);
}

// The list items' methods, wrapped before any of them is created (the lists make them when they're drawn).
function sbAfter(proto, name, style)
{
   var original = proto[name];
   proto[name] = function()
   {
      var r = original.apply(this,arguments);
      style(this);
      return r;
   };
}

function sbPatchClasses()
{
   sbAfter(_global.PageTabs.prototype,"setData",sbStyleTab);
   sbAfter(_global.StickerItemRenderer.prototype,"setData",sbStyleSticker);
   sbAfter(_global.StickerItemRenderer.prototype,"updateAfterStateChange",sbStyleSticker);
   sbAfter(_global.MapIcon.prototype,"setData",sbStyleMapIcon);
   sbAfter(_global.MapIcon.prototype,"updateAfterStateChange",sbStyleMapIcon);
   sbAfter(_global.ObjectiveListItemRenderer.prototype,"setData",sbStyleObjective);
   sbAfter(_global.ObjectiveListItemRenderer.prototype,"updateAfterStateChange",sbStyleObjective);
   sbAfter(_global.ExtraQuestListRenderer.prototype,"setData",sbStyleQuestRow);
   sbAfter(_global.ExtraQuestListRenderer.prototype,"updateAfterStateChange",sbStyleQuestRow);
   // Membership no longer exists: the members-only badge (exported, not placed in the book) never shows.
   sbAfter(_global.fr.extras.FRMembersOnlyBadgeV2.prototype,"setIsMember",function(badge)
   {
      badge.m_member_mc._visible = false;
      badge.m_upgrade_mc._visible = false;
   });
}

// --- the book --------------------------------------------------------------------------

// Lua calls the pages' functions on _root (delegates the pages' constructors set); each is wrapped to restyle what it
// changed, calling the stock one with all its arguments first.
function sbWrapRoot(name, after)
{
   var original = _root[name];
   if(original == undefined)
   {
      return;
   }
   _root[name] = function()
   {
      var r = original.apply(_root,arguments);
      after(arguments);
      return r;
   };
}

function sbStyleBook(book)
{
   if(book == undefined || book.sbStyled)
   {
      return;
   }
   book.sbStyled = true;
   sbBook = book;
   sbBuildClose(book);
   sbBuildLabel(book);
   sbStyleQuestPage(book.questPage_mc);
   sbStyleMiscPage(book.miscQuestPage_mc);

   // A tab click (pageTabClicked), the misc tab going (removeMiscTab) or Lua turning the page: the open page's tab
   // turns gold.
   var turns = ["pageTabClicked","removeMiscTab","AdvanceToPage","ReturnToPage"];
   var i;
   for(i = 0; i < turns.length; i++)
   {
      sbWrapRoot(turns[i],function(args)
      {
         sbPage = sbBook.m_currentPage;
         sbRedrawTabs();
      });
   }
   sbWrapRoot("populateChapterInfo",function(args)
   {
      sbInkQuestPage(book.questPage_mc);
      sbDrawChapterRibbon(book.questPage_mc);
   });
   sbWrapRoot("showQuest",function(args)
   {
      sbInkQuestPage(book.questPage_mc);
      sbDrawQuestPill(book.questPage_mc);
   });
   sbWrapRoot("populateSummary",function(args)
   {
      sbInkQuestPage(book.questPage_mc);
      sbDrawQuestPill(book.questPage_mc);
   });
   sbWrapRoot("SetQuestLimit",function(args)
   {
      sbDrawCount(book.miscQuestPage_mc);
   });
   sbWrapRoot("setQuestDescription",function(args)
   {
      sbColor(book.miscQuestPage_mc.quest_description_txt,SB_PAGE_INK,false);
   });
}

function sbBuild()
{
   sbPatchClasses();
   // The book (StoryBook, the only clip on the main timeline, unnamed) builds its pages in configUI, when it loads.
   var proto = _global.views.StoryBook.prototype;
   var configUI = proto.configUI;
   proto.configUI = function()
   {
      var r = configUI.apply(this,arguments);
      sbStyleBook(this);
      return r;
   };
   var book = _root.getInstanceAtDepth(1 - 16384);
   if(book.questPage_mc != undefined)
   {
      sbStyleBook(book);
   }
}

sbBuild();
// ===== End Sanctuary Adventurer's Journal =====

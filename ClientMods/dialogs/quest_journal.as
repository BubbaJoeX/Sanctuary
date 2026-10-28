// ===== Sanctuary Hero's Journal =====
// Appended to questjournal's main timeline by build_client_mods.py.
// Redraws the Hero's Journal (wndQuestScreen) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as):
// a navy glass frame with the title in gold beside the Main Menu's journal (sanctuaryJournalBook, TEXTURE_SYMBOLS),
// a gold close button, a gold quest count pill, a column header strip and a recessed list. Each quest is a glass card
// with its job badge in a frosted well, the start date on a chip, a gold progress bar and a navy Drop Quest pill;
// hovering lifts it with a gold rim, and the quest the helper tracks keeps a gold rim. The stock art is all loaded
// icons inside browser, which is masked down to its list (the frame, the count plate, the column titles and the
// members-only button outside it are hidden that way); the rows keep their stock hit areas and Lua callbacks.

var QJ_W = 900;
var QJ_H = 750;
// The frame, and the list (root coordinates; browser's qMask is 92..780 x 200..630, the scrollbar sits at 784).
var QJ_FRAME_X = 28;
var QJ_FRAME_Y = 16;
var QJ_LIST_X = 84;
var QJ_LIST_Y = 158;
var QJ_LIST_W = 728;
var QJ_LIST_BOTTOM = 638;
// The toolbar row between the header and the list: the count pill and a hint.
var QJ_TOOLBAR_Y = 108;
var QJ_STRIP_H = 38;
// Quest cards, in a QuestEntry's own coordinates (its bg is 696x90 at 10,9; the mask cuts it at 698).
var QJ_CARD_X = 12;
var QJ_CARD_W = 674;
var QJ_BAR_X = 606;
var QJ_BAR_W = 70;
var QJ_GOLD = 0xFFC531;
var QJ_RIM = 0x9DB8FF;
var QJ_SOFT = 0xC9D6FF;
var QJ_INK = 0x3A2400;

var qjFrame;

// --- drawing helpers -------------------------------------------------------------------

function qjRoundRect(mc, x, y, w, h, r)
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

function qjGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   qjRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

// A recess: flat navy glass with a faint rim.
function qjPanel(mc, x, y, w, h, r, alpha, rimAlpha)
{
   mc.lineStyle(1,QJ_RIM,rimAlpha);
   mc.beginFill(0x0A1440,alpha);
   qjRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

// The soft white shine across the top of a card.
function qjGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   qjRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function qjCircle(mc, x, y, r)
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

function qjSparkle(mc, x, y, r, alpha)
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

// A gold progress bar in a navy track; f is 0..1.
function qjBar(mc, x, y, w, h, f)
{
   if(isNaN(f) || f < 0)
   {
      f = 0;
   }
   else if(f > 1)
   {
      f = 1;
   }
   mc.lineStyle(1,QJ_RIM,30);
   mc.beginFill(0x0A1440,70);
   qjRoundRect(mc,x,y,w,h,h / 2);
   mc.endFill();
   if(f <= 0)
   {
      return;
   }
   var fw = Math.max(h,w * f);
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:fw,h:h,r:Math.PI / 2});
   qjRoundRect(mc,x,y,fw,h,h / 2);
   mc.endFill();
   if(fw > h)
   {
      mc.beginFill(0xFFFFFF,45);
      qjRoundRect(mc,x + h / 2,y + 1,fw - h,Math.max(1,h * 0.3),h * 0.15);
      mc.endFill();
   }
}

function qjText(mc, name, txt, x, y, w, h, size, color, align)
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

function qjOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Recolours a stock field (keeping its font and size) and outlines it, so it reads on navy.
function qjLightText(t, color)
{
   if(t == undefined)
   {
      return;
   }
   t.textColor = color;
   var f = t.getTextFormat();
   f.color = color;
   t.setNewTextFormat(f);
   t.filters = qjOutline();
}

// Penner's elastic and strong eases (the movie only has None compiled in).
function qjElasticOut(t, b, c, d)
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

function qjStrongOut(t, b, c, d)
{
   t = t / d - 1;
   return c * (t * t * t * t * t + 1) + b;
}

// --- frame -----------------------------------------------------------------------------

function qjBuildFrame()
{
   var f = qjFrame = _root.createEmptyMovieClip("sanctuaryJournal",-16384);
   var w = QJ_W - 2 * QJ_FRAME_X;
   var h = QJ_H - 2 * QJ_FRAME_Y;
   var bg = f.createEmptyMovieClip("bg",1);
   qjGradientRect(bg,QJ_FRAME_X,QJ_FRAME_Y,w,h,30,0x2E4FB0,0x141F55,100,QJ_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-130,y:-290,w:820,h:660,r:0});
   qjRoundRect(bg,QJ_FRAME_X + 2,QJ_FRAME_Y + 2,w - 4,h - 4,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   qjRoundRect(bg,QJ_FRAME_X + 4,QJ_FRAME_Y + 4,w - 8,80,26);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   // Sparkles in the header (fixed seed, same every time).
   var s = 6262;
   var i;
   for(i = 0; i < 28; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = QJ_FRAME_X + 30 + s % (w - 60);
      s = (s * 1103515245 + 12345) % 2147483648;
      qjSparkle(bg,x,QJ_FRAME_Y + 12 + s % 64,1 + s % 3,25 + s % 50);
   }

   // The list: a header strip over a recess (the pager sits under it).
   var p = f.createEmptyMovieClip("panels",2);
   qjPanel(p,QJ_LIST_X,QJ_LIST_Y,QJ_LIST_W,QJ_LIST_BOTTOM - QJ_LIST_Y,22,30,22);
   p.lineStyle();
   p.beginGradientFill("linear",[0x284A9E,0x162660],[100,100],[0,255],{matrixType:"box",x:0,y:QJ_LIST_Y,w:10,h:QJ_STRIP_H,r:Math.PI / 2});
   p.moveTo(QJ_LIST_X + 22,QJ_LIST_Y);
   p.lineTo(QJ_LIST_X + QJ_LIST_W - 22,QJ_LIST_Y);
   p.curveTo(QJ_LIST_X + QJ_LIST_W,QJ_LIST_Y,QJ_LIST_X + QJ_LIST_W,QJ_LIST_Y + 22);
   p.lineTo(QJ_LIST_X + QJ_LIST_W,QJ_LIST_Y + QJ_STRIP_H);
   p.lineTo(QJ_LIST_X,QJ_LIST_Y + QJ_STRIP_H);
   p.lineTo(QJ_LIST_X,QJ_LIST_Y + 22);
   p.curveTo(QJ_LIST_X,QJ_LIST_Y,QJ_LIST_X + 22,QJ_LIST_Y);
   p.endFill();
   p.beginGradientFill("linear",[QJ_GOLD,QJ_GOLD,QJ_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:QJ_LIST_X + 20,y:0,w:QJ_LIST_W - 40,h:2,r:0});
   qjRoundRect(p,QJ_LIST_X + 20,QJ_LIST_Y + QJ_STRIP_H - 1,QJ_LIST_W - 40,2,1);
   p.endFill();
   // Column titles, over the cards' columns (a QuestEntry sits at root x 82).
   var y = QJ_LIST_Y + 10;
   qjText(p,"job_txt","Job",100,y,134,22,15,QJ_SOFT);
   qjText(p,"quest_txt","Quest",236,y,240,22,15,QJ_SOFT,"left");
   qjText(p,"date_txt","Started",558,y,110,22,15,QJ_SOFT);
   qjText(p,"progress_txt","Progress",665,y,116,22,15,QJ_SOFT);

   var title = qjText(f,"title_txt","Hero's Journal",0,QJ_FRAME_Y + 10,QJ_W,40,28,0xFFE08A);
   title.filters = qjOutline();
   qjText(f,"subtitle_txt","Your quests and adventures",0,QJ_FRAME_Y + 46,QJ_W,24,14,QJ_SOFT);
   qjText(f,"hint_txt","Click a quest for its objectives. Tick one to follow it on screen.",QJ_W / 2 - 40,QJ_TOOLBAR_Y + 11,QJ_LIST_X + QJ_LIST_W - QJ_W / 2 + 40 - 6,22,13,QJ_SOFT,"right");

   // The Main Menu's journal in a frosted socket, left of the title.
   var book = f.createEmptyMovieClip("book",f.getNextHighestDepth());
   book._x = Math.round((QJ_W - title.textWidth) / 2) - 44;
   book._y = QJ_FRAME_Y + 42;
   book.lineStyle(2,0xFFE08A,100);
   book.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:-34,y:-38,w:68,h:68,r:0});
   qjCircle(book,0,0,26);
   book.endFill();
   var art = book.attachMovie("sanctuaryJournalBook","art",1);
   art._width = art._height = 42;
   art._x = art._y = -21;
   book.filters = [new flash.filters.GlowFilter(QJ_GOLD,0.45,12,12,2,2)];

   qjBuildCount();
   qjBuildClose();
}

// --- quest count pill ------------------------------------------------------------------

function qjBuildCount()
{
   var pill = qjFrame.createEmptyMovieClip("count",qjFrame.getNextHighestDepth());
   pill._y = QJ_TOOLBAR_Y;
   pill.bg = pill.createEmptyMovieClip("bg",1);
   pill.value_txt = qjText(pill,"value_txt","0 / 0",14,6,120,28,20,QJ_INK,"left");
   pill.unit_txt = qjText(pill,"unit_txt","QUESTS",0,14,90,18,11,0x6A4A08,"left");
   pill.filters = [new flash.filters.GlowFilter(QJ_GOLD,0.4,12,12,2,2)];
   qjSetCount("0","0");
}

function qjSetCount(total, limit)
{
   var pill = qjFrame.count;
   pill.value_txt.text = (limit == undefined || String(limit) == "" ? String(total) : total + " / " + limit);
   pill.unit_txt._x = pill.value_txt._x + pill.value_txt.textWidth + 8;
   var w = Math.round(pill.unit_txt._x + pill.unit_txt.textWidth + 18);
   pill._x = QJ_LIST_X;
   var g = pill.bg;
   g.clear();
   g.lineStyle(1.5,0xFFF3C4,100);
   g.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:0,y:0,w:w,h:40,r:Math.PI / 2});
   qjRoundRect(g,0,0,w,40,20);
   g.endFill();
   qjGloss(g,6,2,w - 12,16,8,45);
}

// --- close button ----------------------------------------------------------------------

// The stock close button (an exitButton.swf clip with no instance name) at root depth 26.
function qjExit()
{
   return _root.getInstanceAtDepth(26 - 16384);
}

function qjDrawClose(over)
{
   var c = qjFrame.close;
   c.clear();
   c.lineStyle(1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-17,y:-17,w:34,h:34,r:Math.PI / 2});
   qjCircle(c,0,0,17);
   c.endFill();
   qjGloss(c,-11,-14,22,11,5,50);
   c.lineStyle(4,QJ_INK,100);
   c.moveTo(-6,-6);
   c.lineTo(6,6);
   c.moveTo(6,-6);
   c.lineTo(-6,6);
   c.filters = over ? [new flash.filters.GlowFilter(QJ_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

function qjHoverClose(over)
{
   qjDrawClose(over);
   gs.TweenLite.to(qjFrame.close,over ? 0.7 : 0.25,{_xscale:over ? 115 : 100,_yscale:over ? 115 : 100,_rotation:over ? 90 : 0,ease:over ? qjElasticOut : qjStrongOut});
}

// A gold circle in the frame's top right corner; the stock button moves over it, invisible, and keeps its click
// (it sends ExitButtonClicked).
function qjBuildClose()
{
   var c = qjFrame.createEmptyMovieClip("close",qjFrame.getNextHighestDepth());
   c._x = QJ_W - QJ_FRAME_X - 38;
   c._y = QJ_FRAME_Y + 38;
   qjDrawClose(false);

   var exit = qjExit();
   if(exit == undefined)
   {
      return;
   }
   exit._alpha = 0;
   var b = exit.getBounds(_root);
   if(b.xMax > b.xMin)
   {
      exit._x += c._x - (b.xMin + b.xMax) / 2;
      exit._y += c._y - (b.yMin + b.yMax) / 2;
   }
   var rollOver = exit.onRollOver;
   exit.onRollOver = function()
   {
      rollOver.call(this);
      qjHoverClose(true);
   };
   var rollOut = exit.onRollOut;
   exit.onRollOut = function()
   {
      rollOut.call(this);
      qjHoverClose(false);
   };
   var dragOut = exit.onDragOut;
   exit.onDragOut = function()
   {
      dragOut.call(this);
      qjHoverClose(false);
   };
}

// --- quest cards -----------------------------------------------------------------------

function qjDrawEntry(e)
{
   var c = e.qjCard;
   var h = e.bg._height + 4;
   var over = e.qjOver;
   var tracked = e.radio.getFill();
   e.qjH = e.bg._height;
   e.qjTracked = tracked;
   c.clear();
   qjGradientRect(c,QJ_CARD_X,6,QJ_CARD_W,h,18,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over || tracked ? 0xFFE08A : QJ_RIM,over ? 100 : (tracked ? 85 : 40),over || tracked ? 2 : 1.5);
   qjGloss(c,QJ_CARD_X + 8,8,QJ_CARD_W - 16,26,14,over ? 22 : 14);
   if(tracked)
   {
      c.lineStyle();
      c.beginGradientFill("linear",[QJ_GOLD,QJ_GOLD],[0,28],[0,255],{matrixType:"box",x:QJ_CARD_X,y:0,w:QJ_CARD_W,h:10,r:0});
      qjRoundRect(c,QJ_CARD_X + 2,8,QJ_CARD_W - 4,h - 4,16);
      c.endFill();
   }
   // The badge's well: a recess with a frosted glow behind the job badge or quest icon.
   qjPanel(c,QJ_CARD_X + 8,12,132,82,16,40,25);
   c.lineStyle();
   c.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[55,25,0],[0,120,255],{matrixType:"box",x:QJ_CARD_X + 14,y:4,w:120,h:98,r:0});
   qjRoundRect(c,QJ_CARD_X + 10,14,128,78,15);
   c.endFill();
   // The date's chip and the progress bar (the stock bar is hidden).
   qjPanel(c,488,16,86,26,13,45,25);
   qjBar(c,QJ_BAR_X,22,QJ_BAR_W,12,e.questProgress);
   e.prog.progtext._x = QJ_BAR_X + QJ_BAR_W / 2 - e.prog._x - e.prog.progtext._width / 2;
   e.prog.progtext._y = 22;
   c.filters = over ? [new flash.filters.GlowFilter(QJ_GOLD,0.55,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
}

// The Drop Quest button (an FRButtonV2 at half scale that grows to 300 wide to confirm): a navy pill that follows
// its art's bounds, gold rimmed on hover.
function qjDrawQuit(e)
{
   var q = e.quitbtn;
   var g = e.qjQuit;
   var b = q.UP.getBounds(e);
   e.qjQuitW = b.xMax - b.xMin;
   g.clear();
   if(!(b.xMax > b.xMin))
   {
      return;
   }
   var h = b.yMax - b.yMin;
   var over = q.qjOver;
   qjGradientRect(g,b.xMin,b.yMin,b.xMax - b.xMin,h,h / 2,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : QJ_RIM,over ? 100 : 70,1.5);
   qjGloss(g,b.xMin + h / 3,b.yMin + 1,b.xMax - b.xMin - h / 1.5,h * 0.45,h * 0.2,30);
   g.filters = over ? [new flash.filters.GlowFilter(QJ_GOLD,0.6,10,10,2,2)] : [];
}

function qjStyleEntry(e)
{
   if(e.qjCard == undefined)
   {
      // Under everything in the entry (its bg is at depth 1), and the pill under the Drop Quest button (depth 21).
      e.qjCard = e.createEmptyMovieClip("qjCard",-16384);
      e.qjQuit = e.createEmptyMovieClip("qjQuit",21 - 1 - 16384);
      e.quitbtn.UP._alpha = e.quitbtn.HOVER._alpha = e.quitbtn.DOWN._alpha = 0;
      qjLightText(e.quitbtn.label,0xFFFFFF);
      var lo = new Object();
      lo.entry = e;
      lo.state = function(type, data)
      {
         var q = this.entry.quitbtn;
         if(type == "rollover" || type == "dragover")
         {
            q.qjOver = true;
         }
         else if(type == "rollout" || type == "dragout" || type == "releaseoutside")
         {
            q.qjOver = false;
         }
         qjDrawQuit(this.entry);
      };
      e.quitbtn.addListener(lo);
      // The rows' stock plate stays as the hit area, unseen; the card follows its height as it expands, and the
      // Drop Quest pill follows the button as it grows to confirm.
      e.qjCard.onEnterFrame = function()
      {
         var entry = this._parent;
         if(entry.bg._height != entry.qjH || entry.radio.getFill() != entry.qjTracked)
         {
            qjDrawEntry(entry);
         }
         var b = entry.quitbtn.UP.getBounds(entry);
         if(b.xMax - b.xMin != entry.qjQuitW)
         {
            qjDrawQuit(entry);
         }
      };
   }
   e.bg._alpha = 0;
   e.prog.progressbar_background_mc._visible = false;
   e.prog.progress._visible = false;
   e.prog.icon._visible = false;
   e.memberBadge_mc._visible = false;
   qjLightText(e.title_txt,0xFFE08A);
   qjLightText(e.description_txt,0xFFFFFF);
   qjLightText(e.date_txt,QJ_SOFT);
   qjLightText(e.prog.progtext,0xFFFFFF);
   qjDrawEntry(e);
   qjDrawQuit(e);
}

function qjHoverEntry(e, over)
{
   if(e.qjCard == undefined || e.qjOver == over)
   {
      return;
   }
   e.qjOver = over;
   qjDrawEntry(e);
}

// QuestEntry's methods, wrapped before any entry is attached (Lua adds them with addQuest). The constructor copies
// rollover/rollout onto the row's hit areas, where this is the hit area and _parent the entry.
function qjPatchEntries()
{
   var proto = _global.FRQuestEntry.prototype;
   var loadGraphics = proto.loadGraphics;
   proto.loadGraphics = function()
   {
      var r = loadGraphics.apply(this,arguments);
      qjStyleEntry(this);
      return r;
   };
   var updateProgress = proto.updateProgress;
   proto.updateProgress = function()
   {
      var r = updateProgress.apply(this,arguments);
      if(this.qjCard != undefined)
      {
         qjDrawEntry(this);
      }
      return r;
   };
   var displayObjectives = proto.displayObjectives;
   proto.displayObjectives = function()
   {
      var r = displayObjectives.apply(this,arguments);
      this.memberBadge_mc._visible = false;
      var i;
      for(i = 1; i <= this.objectivesNum; i++)
      {
         qjLightText(this["o" + i].title,0xFFFFFF);
         qjLightText(this["o" + i].description,QJ_SOFT);
      }
      return r;
   };
   var rollover = proto.rollover;
   proto.rollover = function()
   {
      var r = rollover.apply(this,arguments);
      qjHoverEntry(this._parent,true);
      return r;
   };
   var rollout = proto.rollout;
   proto.rollout = function()
   {
      var r = rollout.apply(this,arguments);
      qjHoverEntry(this._parent,false);
      return r;
   };
}

// --- build -----------------------------------------------------------------------------

function qjBuild()
{
   qjBuildFrame();

   // browser holds the stock frame, title art, count plate, column titles and the members-only button around its
   // list; a mask keeps only the list (and the rows' hit areas) showing. The title icon goes too.
   var m = _root.createEmptyMovieClip("sanctuaryJournalMask",_root.getNextHighestDepth());
   m.beginFill(0,100);
   qjRoundRect(m,QJ_LIST_X,QJ_LIST_Y + QJ_STRIP_H,QJ_LIST_W,QJ_LIST_BOTTOM - QJ_LIST_Y - QJ_STRIP_H,0);
   m.endFill();
   browser.setMask(m);
   browser.frame_mc._visible = false;
   browser.decoration._visible = false;
   browser.sortingbar_background_mc._visible = false;
   browser.member_mc._visible = false;
   iconHolder_mc._alpha = 0;

   // The pager moves under the list, centered.
   var pb = pageTurner_mc.getBounds(_root);
   if(pb.xMax > pb.xMin)
   {
      pageTurner_mc._x += Math.round(QJ_LIST_X + QJ_LIST_W / 2 - (pb.xMin + pb.xMax) / 2);
      pageTurner_mc._y += Math.round(QJ_LIST_BOTTOM + 12 - pb.yMin);
   }

   qjPatchEntries();
   qjSetCount(0,"");
}

// Lua sets the count with the quest limit; the pill shows it.
var qjSetQuestLimit = SetQuestLimit;
SetQuestLimit = function(total, limit)
{
   qjSetQuestLimit.apply(_root,arguments);
   qjSetCount(total,limit);
};

// Membership no longer exists: nothing shows as members only.
var qjSetIsMember = SetIsMember;
SetIsMember = function(is)
{
   qjSetIsMember.apply(_root,arguments);
   browser.member_mc._visible = false;
};

qjBuild();
// ===== End Sanctuary Hero's Journal =====

// ===== Sanctuary Quick Match =====
// Appended to quickMatch's main timeline by build_client_mods.py.
// The matchmaking HUD (wndQuickMatch: the queue button by the minimap and the Matchmaking Status card it pops open)
// in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as). The button is a navy glass disc in a gold ring
// with the group art in a frosted socket and the queue count on a gold badge; the card is navy glass with the title
// in gold on a recessed bar (a gold chevron says it opens the activity list, as the stock bar does) and each queue is
// a glass row with its wait in gold and a small red glass leave button.
//
// Lua (quickMatch.lua) drives it as stock: clearItems, addItem and bigWindowShow; the movie sends leaveQ(id),
// titlebarPressed, WindowShow() and sounds. The stock clips stay the hit areas and keep their handlers: their art is
// invisible (the rows' highlight, the leave X's states, the button's plates and badge) and this layer, which has no
// mouse handlers, is drawn under or inside them. The card's backdrop grows with a tween, so the glass follows it.

var QM_CARD_W = 350;
var QM_BUTTON_R = 24;
var QM_ROW_W = 322;
var QM_ROW_H = 19;
var QM_GOLD = 0xFFC531;
var QM_RIM = 0x9DB8FF;
var QM_SOFT = 0xC9D6FF;
var QM_INK = 0x3A2400;

var qmCard;

// --- drawing helpers -------------------------------------------------------------------

function qmRoundRect(mc, x, y, w, h, r)
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

function qmGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   qmRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function qmCircle(mc, x, y, r)
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

function qmGoldFill(mc, x, y, w, h, over)
{
   mc.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
}

function qmSparkle(mc, x, y, r, alpha)
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

function qmOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function qmShadow()
{
   return [new flash.filters.DropShadowFilter(1,90,0x0A1030,0.9,2,2,1,1)];
}

function qmText(mc, name, txt, x, y, w, h, size, color, align)
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

// Recolours a stock field (keeps its font and size) and outlines it.
function qmColor(t, color, filters)
{
   var f = new TextFormat();
   f.color = color;
   t.setTextFormat(f);
   t.setNewTextFormat(f);
   t.filters = filters;
}

// Penner's elastic ease out; this movie carries no easing classes of its own.
function qmElasticOut(t, b, c, d)
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

// --- the queue button ------------------------------------------------------------------

function qmBuildButton()
{
   var b = mcButton;
   // The stock purple plates (shown and hidden by the rollovers) and the red badge go; they stay as hit areas.
   b.mcUp._alpha = b.mcRoll._alpha = 0;
   b.mcText._alpha = 0;
   b.qmDisc = b.createEmptyMovieClip("qmDisc",-16384);
   var badge = b.qmBadge = b.createEmptyMovieClip("qmBadge",100);
   badge._x = 40;
   badge._y = 40;
   badge.lineStyle(2,0xFFF3C4,100);
   qmGoldFill(badge,-11,-11,22,22,false);
   qmCircle(badge,0,0,11);
   badge.endFill();
   badge.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.7,4,4,1,2)];
   badge.count_txt = qmText(badge,"count_txt","",-14,-10,28,20,13,QM_INK);
   qmDrawButton(false);
}

function qmDrawButton(over)
{
   var d = mcButton.qmDisc;
   var c = QM_BUTTON_R;
   d.clear();
   d.lineStyle();
   d.beginFill(0x050A24,40);
   qmCircle(d,c,c + 3,c + 1);
   d.endFill();
   d.lineStyle(3,over ? 0xFFE08A : QM_GOLD,100);
   d.beginGradientFill("radial",over ? [0x6A90F5,0x2643A8] : [0x4A70DC,0x172A6E],[100,100],[0,255],{matrixType:"box",x:0,y:-10,w:c * 2,h:c * 2,r:0});
   qmCircle(d,c,c,c);
   d.endFill();
   // The frosted socket behind the group art (it's made for light plates).
   d.lineStyle(1.5,0xFFFFFF,85);
   d.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:c - 22,y:c - 25,w:44,h:44,r:0});
   qmCircle(d,c,c,18);
   d.endFill();
   d.filters = over ? [new flash.filters.GlowFilter(QM_GOLD,0.75,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,6,6,1,2)];
}

function qmSyncButton()
{
   var b = mcButton;
   var over = b.mcRoll._visible == true;
   if(over != b.qmOver)
   {
      b.qmOver = over;
      qmDrawButton(over);
      gs.TweenLite.to(b.mcIcon,0.6,{_xscale:over ? 112 : 100,_yscale:over ? 112 : 100,_x:over ? 6 : 8,_y:over ? 6 : 8,ease:qmElasticOut});
   }
   var n = b.mcText.txt.text;
   if(n != b.qmBadge.count_txt.text)
   {
      b.qmBadge.count_txt.text = n;
   }
}

// --- the Matchmaking Status card --------------------------------------------------------

function qmBuildCard()
{
   var win = bigWindow;
   win.mcBackground._alpha = 0;
   win.mcTitlebar._alpha = 0;
   qmCard = win.createEmptyMovieClip("qmCard",-16384);
   qmCard.bg = qmCard.createEmptyMovieClip("bg",1);
   qmCard.bar = qmCard.createEmptyMovieClip("bar",2);
   qmCard.bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,16,16,1,2)];
   qmColor(win.mcTxtTitleHolder.txtTitle,0xFFE08A,qmOutline());
   qmDrawBar(false);
}

function qmDrawCard(h)
{
   var g = qmCard.bg;
   g.clear();
   qmGradientRect(g,0,0,QM_CARD_W,h,20,0x2E4FB0,0x141F55,100,QM_RIM,80,2.5);
   g.lineStyle();
   g.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-120,y:-160,w:480,h:360,r:0});
   qmRoundRect(g,2,2,QM_CARD_W - 4,h - 4,18);
   g.endFill();
   // Sparkles along the top (fixed seed, same every time).
   var s = 2828;
   var i;
   for(i = 0; i < 10; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 26 + s % (QM_CARD_W - 52);
      s = (s * 1103515245 + 12345) % 2147483648;
      qmSparkle(g,x,8 + s % 30,1 + s % 2,20 + s % 40);
   }
}

// The title bar (the stock one is invisible and still opens the activity list): recessed, lighter on hover.
function qmDrawBar(over)
{
   var b = qmCard.bar;
   var bar = bigWindow.mcTitlebar;
   var x = bar._x;
   var y = bar._y;
   var w = bar._width;
   var h = bar._height;
   b.clear();
   qmGradientRect(b,x,y,w,h,h / 2,over ? 0x2B4FA8 : 0x0A1440,over ? 0x17286A : 0x0A1440,over ? 90 : 45,over ? 0xFFE08A : QM_RIM,over ? 90 : 30,1);
   // A gold chevron at the right end.
   b.lineStyle(3,0xFFE08A,over ? 100 : 75);
   b.moveTo(x + w - 22,y + h / 2 - 6);
   b.lineTo(x + w - 15,y + h / 2);
   b.lineTo(x + w - 22,y + h / 2 + 6);
}

function qmSyncCard()
{
   var h = Math.round(bigWindow.mcBackground._height);
   if(h != qmCard.h)
   {
      qmCard.h = h;
      qmDrawCard(h);
   }
   // The stock rollover grows the title to 105%.
   var over = bigWindow.mcTxtTitleHolder._xscale > 100;
   if(over != qmCard.over)
   {
      qmCard.over = over;
      qmDrawBar(over);
   }
}

// --- rows ------------------------------------------------------------------------------

function qmStyleHeader(header)
{
   qmColor(header.mcName.txtName,0xFFE08A,qmShadow());
   qmColor(header.mcWait.txtWait,0xFFE08A,qmShadow());
   qmColor(header.mcAvgWait.txtAvgWait,0xFFE08A,qmShadow());
}

function qmStyleRow(row)
{
   // The highlight is the row's hover hit area; it stays invisible.
   row.mcHigh._alpha = 0;
   row.qmPlate = row.createEmptyMovieClip("qmPlate",-16384);
   qmDrawRow(row,false);
   qmColor(row.mcName.txtName,0xFFFFFF,qmShadow());
   qmColor(row.mcWait.txtWait,0xFFE08A,qmShadow());
   qmColor(row.mcAvgWait.txtAvgWait,QM_SOFT,qmShadow());
   // The leave button: a small red glass disc with a white X under its (invisible) stock art.
   row.mcX.up._alpha = row.mcX.roll._alpha = 0;
   row.mcX.qmDisc = row.mcX.createEmptyMovieClip("qmDisc",-16384);
   qmDrawLeave(row.mcX,false);
}

function qmDrawRow(row, over)
{
   var p = row.qmPlate;
   p.clear();
   if(over)
   {
      qmGradientRect(p,2,0,QM_ROW_W,QM_ROW_H,9,0x3F66D6,0x1D307A,100,0xFFE08A,90,1.5);
   }
   else
   {
      qmGradientRect(p,2,0,QM_ROW_W,QM_ROW_H,9,0x22408F,0x152458,85,QM_RIM,25,1);
   }
   p.filters = over ? [new flash.filters.GlowFilter(QM_GOLD,0.35,8,8,2,2)] : [];
}

function qmDrawLeave(x, over)
{
   var d = x.qmDisc;
   d.clear();
   d.lineStyle(1.5,0xFFC9C0,100);
   d.beginGradientFill("linear",over ? [0xF07A6A,0xB0302A] : [0xE0604A,0x9A2420],[100,100],[0,255],{matrixType:"box",x:0,y:0,w:16,h:16,r:Math.PI / 2});
   qmCircle(d,8,8,8);
   d.endFill();
   d.lineStyle(2.5,0xFFFFFF,100);
   d.moveTo(5,5);
   d.lineTo(11,11);
   d.moveTo(11,5);
   d.lineTo(5,11);
   d.filters = over ? [new flash.filters.GlowFilter(0xFF7A6A,0.8,8,8,2,2)] : [];
}

// --- stock hooks -----------------------------------------------------------------------

// The handlers addItem gives each row are looked up by name when it runs, so the wrappers below are the ones used.
var qmStockItemRollover = itemRollover;
itemRollover = function()
{
   var r = qmStockItemRollover.apply(this,arguments);
   this._alpha = 0;
   if(this._parent.qmPlate != undefined)
   {
      qmDrawRow(this._parent,true);
   }
   return r;
};
var qmStockItemRollout = itemRollout;
itemRollout = function()
{
   var r = qmStockItemRollout.apply(this,arguments);
   this._alpha = 0;
   if(this._parent.qmPlate != undefined)
   {
      qmDrawRow(this._parent,false);
   }
   return r;
};
var qmStockXRollover = xRollover;
xRollover = function()
{
   var r = qmStockXRollover.apply(this,arguments);
   if(this.qmDisc != undefined)
   {
      qmDrawLeave(this,true);
   }
   return r;
};
var qmStockXRollout = xRollout;
xRollout = function()
{
   var r = qmStockXRollout.apply(this,arguments);
   if(this.qmDisc != undefined)
   {
      qmDrawLeave(this,false);
   }
   return r;
};

// Lua's calls: each new list starts with its header, each queue adds a row.
var qmStockClearItems = clearItems;
clearItems = function()
{
   var r = qmStockClearItems.apply(_root,arguments);
   qmStyleHeader(body.itemHeader);
   return r;
};
var qmStockAddItem = addItem;
addItem = function(myName, myID, waitTime, avgTime)
{
   var r = qmStockAddItem.apply(_root,arguments);
   var row = body["item_" + myID];
   if(row != undefined && row.qmPlate == undefined)
   {
      qmStyleRow(row);
   }
   return r;
};

function qmBuild()
{
   qmBuildButton();
   qmBuildCard();
   // The header the stock code made before this ran.
   qmStyleHeader(body.itemHeader);
   var sync = _root.createEmptyMovieClip("qmSync",-16384);
   sync.onEnterFrame = function()
   {
      qmSyncButton();
      qmSyncCard();
   };
}

qmBuild();
// ===== End Sanctuary Quick Match =====

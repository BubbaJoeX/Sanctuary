// ===== Sanctuary Friend Picker =====
// Appended to friendPicker's main timeline by build_client_mods.py.
// Redraws the friend picker (wndFriendPicker: the bubble beside a game lobby's empty slot that lists the friends
// who can be invited into it) in the friends dock's style (ClientMods/social/friends_dock.as): a navy glass bubble
// still pointing at the slot, an "Invite a Friend" title in gold with a gold close circle, and glass friend rows in
// a recessed list, each with its presence dot. A hovered row lifts into gold with an Invite chip. The stock list
// has no scrollbar symbol in this movie, so the mouse wheel scrolls it and a slim gold thumb shows where it is.
// The stock clips keep their roles: the row highlights are the hit areas (a click still sends FriendSelected) and
// the stock close button (now invisible, under the gold circle) still sends CloseWindow.

// The bubble, in panel_mc's coordinates (the stock bubble spans x 0..238, its pointer's tip near the left edge).
var FPK_LEFT = 24;
var FPK_RIGHT = 244;
var FPK_TIP_X = 5;
// Where the pointer sits, as a fraction of the panel's height (the stock arrow's place).
var FPK_TIP_Y = 0.415;
var FPK_LIST_X = 36;
var FPK_LIST_Y = 54;
var FPK_LIST_W = 196;
var FPK_ROW_W = 188;
var FPK_ROW_H = 28;
var FPK_CLOSE_X = 222;
var FPK_CLOSE_Y = 25;
var FPK_GOLD = 0xFFC531;
var FPK_GOLD_LIGHT = 0xFFE08A;
var FPK_GOLD_DARK = 0xF5A623;
var FPK_RIM = 0x9DB8FF;
var FPK_SOFT = 0xC9D6FF;
var FPK_INK = 0x3A2400;
var FPK_GREEN = 0x5BE37D;
var FPK_AMBER = 0xFFC531;
var FPK_GREY = 0x8391B8;

var fpkFrame;

// --- drawing helpers -------------------------------------------------------------------

function fpkRoundRect(mc, x, y, w, h, r)
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

function fpkGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   fpkRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function fpkCircle(mc, x, y, r)
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

function fpkText(mc, name, txt, x, y, w, h, size, color, align)
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

function fpkSparkle(mc, x, y, r, alpha)
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

function fpkOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function fpkPresenceDot(g, x, y, color, glow)
{
   g.lineStyle();
   if(glow)
   {
      g.beginGradientFill("radial",[color,color],[55,0],[0,255],{matrixType:"box",x:x - 9,y:y - 9,w:18,h:18,r:0});
      fpkCircle(g,x,y,9);
      g.endFill();
   }
   g.lineStyle(1.5,0x0A1030,70);
   g.beginGradientFill("radial",[0xFFFFFF,color,color],[100,100,100],[0,90,255],{matrixType:"box",x:x - 6,y:y - 7,w:11,h:11,r:0});
   fpkCircle(g,x,y,4.5);
   g.endFill();
}

// A clip just above an unnamed timeline shape (timeline depth shapeDepth) and below the clip's other children
// (the friends dock's trick): made at the bottom, then swapped into the shape's place.
function fpkAboveShape(parent, name, shapeDepth)
{
   var mc = parent.createEmptyMovieClip(name,-16384);
   mc.swapDepths(shapeDepth - 16384);
   return mc;
}

// The body's gradient colour at a fraction of its height, so the pointer matches where it joins.
function fpkBodyColor(t)
{
   var r = Math.round(0x2E + (0x14 - 0x2E) * t);
   var g = Math.round(0x4F + (0x1F - 0x4F) * t);
   var b = Math.round(0xB0 + (0x55 - 0xB0) * t);
   return r << 16 | g << 8 | b;
}

// --- the bubble ------------------------------------------------------------------------

function fpkDrawFrame(h)
{
   var bg = fpkFrame.bg;
   var w = FPK_RIGHT - FPK_LEFT;
   bg.clear();
   fpkGradientRect(bg,FPK_LEFT,0,w,h,24,0x2E4FB0,0x141F55,100,FPK_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:FPK_LEFT - 90,y:-140,w:300,h:300,r:0});
   fpkRoundRect(bg,FPK_LEFT + 2,2,w - 4,h - 4,22);
   bg.endFill();

   // The pointer at the lobby slot: filled over the body's rim where they join, rimmed along its outer edges.
   var ty = Math.round(h * FPK_TIP_Y);
   bg.beginFill(fpkBodyColor(ty / h),100);
   bg.moveTo(FPK_LEFT + 6,ty - 13);
   bg.lineTo(FPK_TIP_X,ty);
   bg.lineTo(FPK_LEFT + 6,ty + 13);
   bg.lineTo(FPK_LEFT + 6,ty - 13);
   bg.endFill();
   bg.lineStyle(3,FPK_RIM,80);
   bg.moveTo(FPK_LEFT + 1,ty - 15);
   bg.lineTo(FPK_TIP_X,ty);
   bg.lineTo(FPK_LEFT + 1,ty + 15);

   // The header band and its sparkles.
   bg.lineStyle();
   bg.beginFill(0x0A1440,30);
   fpkRoundRect(bg,FPK_LEFT + 8,8,w - 16,36,18);
   bg.endFill();
   var s = 2911;
   var i;
   for(i = 0; i < 10; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = FPK_LEFT + 18 + s % (w - 70);
      s = (s * 1103515245 + 12345) % 2147483648;
      fpkSparkle(bg,x,12 + s % 28,1 + s % 2,20 + s % 40);
   }

   // The list's recess.
   fpkGradientRect(bg,FPK_LIST_X - 4,FPK_LIST_Y - 4,FPK_LIST_W + 8,h - FPK_LIST_Y - 6,14,0x0A1440,0x0A1440,38,FPK_RIM,22,1);
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   fpkFrame.empty_txt._y = FPK_LIST_Y + (h - FPK_LIST_Y - 14) / 2 - 22;
}

function fpkDrawClose(over)
{
   var c = fpkFrame.close;
   c.clear();
   c.lineStyle(2,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB43C] : [FPK_GOLD_LIGHT,FPK_GOLD_DARK],[100,100],[0,255],{matrixType:"box",x:-12,y:-12,w:24,h:24,r:Math.PI / 2});
   fpkCircle(c,0,0,12);
   c.endFill();
   c.lineStyle(3,FPK_INK,100);
   c.moveTo(-4.5,-4.5);
   c.lineTo(4.5,4.5);
   c.moveTo(4.5,-4.5);
   c.lineTo(-4.5,4.5);
   c.filters = over ? [new flash.filters.GlowFilter(FPK_GOLD,0.8,12,12,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.55,5,5,1,2)];
   gs.TweenLite.to(c,0.5,{_rotation:over ? 90 : 0,_xscale:over ? 112 : 100,_yscale:over ? 112 : 100,ease:mx.transitions.easing.Elastic.easeOut});
}

function fpkBuild()
{
   var panel = _root.panel_mc;
   // Under the stock front side (timeline depth 1) and its list; the mouse goes through it.
   fpkFrame = panel.createEmptyMovieClip("sanctuaryFrame",-16384);
   fpkFrame.bg = fpkFrame.createEmptyMovieClip("bg",1);
   fpkFrame.thumb = fpkFrame.createEmptyMovieClip("thumb",2);
   var title = fpkText(fpkFrame,"title_txt","Invite a Friend",FPK_LEFT + 14,13,FPK_CLOSE_X - 16 - FPK_LEFT - 14,26,18,FPK_GOLD_LIGHT);
   title.filters = fpkOutline();
   fpkFrame.empty_txt = fpkText(fpkFrame,"empty_txt","None of your friends can join right now.",FPK_LIST_X + 8,0,FPK_LIST_W - 16,44,13,FPK_SOFT);
   fpkFrame.empty_txt.multiline = fpkFrame.empty_txt.wordWrap = true;
   fpkFrame.empty_txt._visible = false;

   // The gold close circle sits over everything (it has no mouse handlers); the stock button is its hit area.
   fpkFrame.close = panel.createEmptyMovieClip("sanctuaryClose",5000);
   fpkFrame.close._x = FPK_CLOSE_X;
   fpkFrame.close._y = FPK_CLOSE_Y;
   fpkDrawClose(false);
   var exit = panel.frontSide_mc.exitButton;
   exit._alpha = 0;
   exit._width = exit._height = 26;
   exit._x = FPK_CLOSE_X - 13;
   exit._y = FPK_CLOSE_Y - 13;
   exit.onRollOver = function()
   {
      fpkDrawClose(true);
   };
   exit.onRollOut = exit.onDragOut = function()
   {
      fpkDrawClose(false);
   };
   panel.frontSide_mc.background_mc._alpha = 0;
}

// Places the list in its recess (the stock list starts at the top and is 178 wide) and redraws the bubble.
function fpkLayout(panel)
{
   var h = panel.getHeight();
   var list = panel.m_listView;
   list._x = FPK_LIST_X;
   list._y = FPK_LIST_Y;
   list.mask_mc._width = FPK_LIST_W;
   list.setHeight(h - FPK_LIST_Y - 14);
   fpkDrawFrame(h);
}

// --- rows ------------------------------------------------------------------------------

function fpkPresence(model)
{
   if(!model.getIsOnline())
   {
      return FPK_GREY;
   }
   if(model.getInEncounter() || !model.getIsLocal())
   {
      return FPK_AMBER;
   }
   return FPK_GREEN;
}

function fpkStyleRow(view)
{
   view.fpkRow = view.createEmptyMovieClip("sanctuaryRow",-16384);
   // The stock highlight is the row's hit area: stretched over the whole row; the stock code shows it (alpha
   // 100) on hover, so the hover look is drawn inside it, just above its own bar, undoing the stretch.
   var hilite = view.hilite_mc;
   hilite._x = hilite._y = 0;
   hilite._xscale = FPK_ROW_W / 160 * 100;
   hilite._yscale = FPK_ROW_H / 20 * 100;
   var hv = view.fpkHover = fpkAboveShape(hilite,"sanctuaryHover",1);
   hv._xscale = 100 / (FPK_ROW_W / 160);
   hv._yscale = 100 / (FPK_ROW_H / 20);
   fpkGradientRect(hv,0,1,FPK_ROW_W,FPK_ROW_H - 2,10,0x3F66D6,0x1D307A,100,FPK_GOLD_LIGHT,100,1.5);
   hv.lineStyle();
   hv.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[20,0],[0,255],{matrixType:"box",x:0,y:2,w:10,h:12,r:Math.PI / 2});
   fpkRoundRect(hv,3,2,FPK_ROW_W - 6,11,6);
   hv.endFill();
   // The Invite chip along the right end.
   hv.lineStyle(1,0xFFF3C4,100);
   hv.beginGradientFill("linear",[FPK_GOLD_LIGHT,FPK_GOLD_DARK],[100,100],[0,255],{matrixType:"box",x:0,y:5,w:10,h:FPK_ROW_H - 10,r:Math.PI / 2});
   fpkRoundRect(hv,FPK_ROW_W - 54,5,48,FPK_ROW_H - 10,(FPK_ROW_H - 10) / 2);
   hv.endFill();
   fpkText(hv,"invite_txt","Invite",FPK_ROW_W - 54,6,48,18,11,FPK_INK);
   hv.filters = [new flash.filters.GlowFilter(FPK_GOLD,0.5,10,10,2,2)];

   view.m_bullet._visible = false;
   var t = view.name_txt;
   var f = new TextFormat();
   f.size = 14;
   t.setNewTextFormat(f);
   t._x = 26;
   t._y = 4;
   t._width = FPK_ROW_W - 26 - 58;
   t._height = 22;
   t.filters = fpkOutline();
}

function fpkDrawRow(view)
{
   var model = view.m_itemModel;
   var online = model.getIsOnline();
   var g = view.fpkRow;
   g.clear();
   fpkGradientRect(g,0,1,FPK_ROW_W,FPK_ROW_H - 2,10,0x284A9E,0x162660,online ? 80 : 45,FPK_RIM,online ? 30 : 16,1);
   g.lineStyle();
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[online ? 12 : 6,0],[0,255],{matrixType:"box",x:0,y:2,w:10,h:12,r:Math.PI / 2});
   fpkRoundRect(g,3,2,FPK_ROW_W - 6,11,6);
   g.endFill();
   fpkPresenceDot(g,13,FPK_ROW_H / 2,fpkPresence(model),online);
   // The stock name colour is orange for friends online elsewhere; names stay white here (the dot says it).
   view.name_txt.textColor = 0xFFFFFF;
}

// --- scrolling -------------------------------------------------------------------------

// The list's rows and how far they can scroll ([content height, visible height]).
function fpkScrollRange()
{
   var list = _root.panel_mc.m_listView;
   return [list.container_mc._height,list.mask_mc._height];
}

var fpkWheel = new Object();
fpkWheel.onMouseWheel = function(delta)
{
   var list = _root.panel_mc.m_listView;
   if(!list.mask_mc.hitTest(_root._xmouse,_root._ymouse,false))
   {
      return;
   }
   var range = fpkScrollRange();
   if(range[0] <= range[1])
   {
      return;
   }
   var y = list.container_mc._y + (delta > 0 ? FPK_ROW_H : - FPK_ROW_H);
   list.container_mc._y = Math.max(range[1] - range[0],Math.min(0,y));
};

// The slim gold thumb along the list's right edge, and the empty line.
function fpkSync()
{
   var panel = _root.panel_mc;
   var list = panel.m_listView;
   var range = fpkScrollRange();
   var t = fpkFrame.thumb;
   var key = range[0] > range[1] ? Math.round(list.container_mc._y) + "," + Math.round(range[0]) + "," + Math.round(range[1]) : "";
   if(key != t.key)
   {
      t.key = key;
      t.clear();
      if(key != "")
      {
         // Keeps the rows in range after some leave the list.
         list.container_mc._y = Math.max(range[1] - range[0],Math.min(0,list.container_mc._y));
         var x = FPK_LIST_X + FPK_LIST_W - 3;
         var h = Math.max(18,range[1] * range[1] / range[0]);
         var y = FPK_LIST_Y + (range[1] - h) * (- list.container_mc._y) / (range[0] - range[1]);
         t.lineStyle(1,0xFFF3C4,80);
         t.beginGradientFill("linear",[FPK_GOLD_LIGHT,FPK_GOLD_DARK],[100,100],[0,255],{matrixType:"box",x:x - 2.5,y:y,w:5,h:h,r:0});
         fpkRoundRect(t,x - 2.5,y,5,h,2.5);
         t.endFill();
      }
   }
   fpkFrame.empty_txt._visible = panel.getItemCount() == 0;
}

// --- stock hooks -----------------------------------------------------------------------

function fpkPatch()
{
   var view = CharacterItemView.prototype;
   var reDraw = view.reDraw;
   view.reDraw = function()
   {
      if(this.fpkRow == undefined)
      {
         fpkStyleRow(this);
      }
      var r = reDraw.apply(this,arguments);
      fpkDrawRow(this);
      return r;
   };

   var panel = ExpandablePanel.prototype;
   var setHeight = panel.setHeight;
   panel.setHeight = function(height)
   {
      var r = setHeight.apply(this,arguments);
      fpkLayout(this);
      return r;
   };
}

fpkPatch();
fpkBuild();
fpkLayout(_root.panel_mc);
// Rows the stock code made before this ran.
var fpkItems = _root.panel_mc.m_listView.m_items;
var fpkI;
for(fpkI = 0; fpkI < fpkItems.length; fpkI++)
{
   fpkItems[fpkI].reDraw();
}
_root.panel_mc.m_listView.rePosition();
Mouse.addListener(fpkWheel);
fpkFrame.onEnterFrame = fpkSync;
// ===== End Sanctuary Friend Picker =====

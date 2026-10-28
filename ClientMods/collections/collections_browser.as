// ===== Sanctuary Collections =====
// Appended to CollectionsBrowser's main timeline by build_client_mods.py.
// Redraws the Collections window in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass
// frame with a gold title, a sidebar of category rows (the category's icon in a frosted socket, the selected row
// rimmed in gold with a pointer at the cards) and glass collection cards (the collection's icon in a round frosted
// socket, its name and a gold progress bar from the stock "1 / 8", rimmed in gold with a check once complete).
// The panel over the cards names the category and counts its finished collections. The stock art is unnamed
// shapes inside the window and its list renderers, which ActionScript can't hide, so the stock window stays
// whole but invisible (_alpha 0, still taking the mouse) and this layer above it follows its lists every frame:
// their renderers, data, hover and selection, and the cards' scrollbar. Clicks, Lua calls and tooltips stay stock.
// The title's book is the Main Menu's Collections art (sanctuaryCollectionsBook, TEXTURE_SYMBOLS).

var COL_W = 1024;
var COL_H = 768;
var COL_HEADER_H = 88;
var COL_PANEL_Y = 112;
var COL_PANEL_H = 524;
// The two stock lists, moved a little so each sits centered in its panel (the stock top is 137.5).
var COL_NAV_X = 34;
var COL_LIST_X = 322;
var COL_LIST_Y = 149.5;
var COL_SIDE_X = 24;
var COL_SIDE_W = 280;
var COL_CARDS_X = 310;
var COL_CARDS_W = 690;
// A category row inside its 255x47 cell (view.CollectionsBrowser.configUI).
var COL_ROW_X = 1;
var COL_ROW_Y = 3;
var COL_ROW_W = 252;
var COL_ROW_H = 41;
// A card inside its 213x155 cell, its socket and progress bar. The stock icon is 64px: 115% here, 126% hovered.
var COL_CARD_X = 5;
var COL_CARD_Y = 4;
var COL_CARD_W = 203;
var COL_CARD_H = 147;
var COL_SOCKET_Y = 52;
var COL_SOCKET_R = 38;
var COL_ICON_SCALE = 115;
var COL_ICON_HOVER = 126;
var COL_BAR_Y = 127;
var COL_BAR_H = 16;
var COL_CLOSE_X = 968;
var COL_CLOSE_Y = 60;
var COL_MENU_Y = 696;
var COL_GOLD = 0xFFC531;
var COL_RIM = 0x9DB8FF;
var COL_SOFT = 0xC9D6FF;
var COL_INK = 0x3A2400;

var colRoot;
var colNavSkins = [];
var colCardSkins = [];

// --- drawing helpers -------------------------------------------------------------------

function colRoundRect(mc, x, y, w, h, r)
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

function colGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   colRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function colCircle(mc, x, y, r)
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
function colSocket(mc, x, y, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.25,y:y - r * 1.4,w:r * 2.5,h:r * 2.5,r:0});
   colCircle(mc,x,y,r);
   mc.endFill();
}

function colGoldFill(mc, x, y, w, h)
{
   mc.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
}

function colText(mc, name, txt, x, y, w, h, size, color, align)
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
   t.baseY = y;
   return t;
}

// Sets a field's text, shrinking its font until it fits (one line, or the field's height when it wraps).
function colSetFitted(t, txt)
{
   var f = t.getTextFormat();
   f.size = t.baseSize;
   t.setNewTextFormat(f);
   t.text = txt;
   while((t.wordWrap ? t.textHeight > t._height - 4 : t.textWidth > t._width - 6) && f.size > 10)
   {
      f.size -= 1;
      t.setTextFormat(f);
   }
}

function colSparkle(mc, x, y, r, alpha)
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

function colOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// An image holder the client's loader can fill (it sizes images to the holder's original bounds), centered.
function colImageHolder(parent, size)
{
   var holder = parent.createEmptyMovieClip("holder",1);
   holder.beginFill(0,0);
   colRoundRect(holder,0,0,size,size,0);
   holder.endFill();
   holder._x = holder._y = - size / 2;
   return holder;
}

// Penner's elastic ease out; this movie only carries mx.transitions.easing.Strong.
function colElasticOut(t, b, c, d)
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

function colStrong()
{
   return mx.transitions.easing.Strong.easeOut;
}

// A gfx Button's look while the mouse is on it (Button.stateMap: release plays on into over).
function colIsOver(button)
{
   return button.state == "over" || button.state == "down" || button.state == "release";
}

// "1 / 8" (BadgeItemRenderer.buildCollection) -> [1, 8]; the card's data if the text has no two numbers.
function colParseProgress(s, data)
{
   s = String(s);
   var nums = [];
   var digits = "";
   var i;
   for(i = 0; i <= s.length; i++)
   {
      var c = s.charAt(i);
      if(c >= "0" && c <= "9")
      {
         digits += c;
      }
      else if(digits.length > 0)
      {
         nums.push(Number(digits));
         digits = "";
      }
   }
   if(nums.length >= 2)
   {
      return [nums[0],nums[1]];
   }
   return colCount(data);
}

// A collection's [collected, total], counted like BadgeItemRenderer.buildCollection.
function colCount(data)
{
   var got = 0;
   var i;
   for(i = 0; i < data.collectablesArray.length; i++)
   {
      if(data.collectablesArray[i] == "1")
      {
         got++;
      }
   }
   var total = Number(data.count);
   return [got,isNaN(total) ? 0 : total];
}

// Moves a stock clip so its bounds center on (x, y).
function colCenterOn(mc, x, y)
{
   var b = mc.getBounds(mc._parent);
   mc._x += x - (b.xMin + b.xMax) / 2;
   mc._y += y - (b.yMin + b.yMax) / 2;
}

// --- frame -----------------------------------------------------------------------------

function colDrawFrame(bg)
{
   colGradientRect(bg,12,12,COL_W - 24,COL_H - 24,30,0x2E4FB0,0x141F55,100,COL_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-150,y:-300,w:900,h:700,r:0});
   colRoundRect(bg,14,14,COL_W - 28,COL_H - 28,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   colRoundRect(bg,16,16,COL_W - 32,COL_HEADER_H,26);
   bg.endFill();

   // Sparkles in the header (fixed seed, same every time).
   var s = 5151;
   var i;
   for(i = 0; i < 30; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 40 + s % (COL_W - 80);
      s = (s * 1103515245 + 12345) % 2147483648;
      colSparkle(bg,x,24 + s % 76,1 + s % 3,25 + s % 50);
   }

   // The sidebar and card panels, each with a gold rule under its caption.
   colGradientRect(bg,COL_SIDE_X,COL_PANEL_Y,COL_SIDE_W,COL_PANEL_H,22,0x0A1440,0x0A1440,38,COL_RIM,22,1);
   colGradientRect(bg,COL_CARDS_X,COL_PANEL_Y,COL_CARDS_W,COL_PANEL_H,22,0x0A1440,0x0A1440,38,COL_RIM,22,1);
   colRule(bg,COL_SIDE_X + 16,COL_SIDE_W - 32);
   colRule(bg,COL_CARDS_X + 16,COL_CARDS_W - 32);
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
}

function colRule(mc, x, w)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[COL_GOLD,COL_GOLD,COL_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:x,y:0,w:w,h:2,r:0});
   colRoundRect(mc,x,COL_PANEL_Y + 33,w,2,1);
   mc.endFill();
}

function colBuildTitle()
{
   var title = colText(colRoot,"title_txt","Collections",0,22,COL_W,40,28,0xFFE08A);
   title.filters = colOutline();
   colText(colRoot,"subtitle_txt","Things you've found",0,58,COL_W,22,14,COL_SOFT);
   var book = colRoot.attachMovie("sanctuaryCollectionsBook","book",colRoot.getNextHighestDepth());
   book._width = book._height = 46;
   book._x = Math.round((COL_W - title.textWidth) / 2) - 56;
   book._y = 18;
   book.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,6,6,1,2)];
}

// The card panel's header: the selected category's icon and name, and how many of its collections are done.
function colBuildHeader()
{
   var h = colRoot.createEmptyMovieClip("header",colRoot.getNextHighestDepth());
   var cy = COL_PANEL_Y + 16;
   colSocket(h,COL_CARDS_X + 30,cy,14,0xFFFFFF,85,1.5);
   var icon = h.createEmptyMovieClip("icon",h.getNextHighestDepth());
   icon._x = COL_CARDS_X + 30;
   icon._y = cy;
   colImageHolder(icon,26);
   var mask = h.createEmptyMovieClip("iconMask",h.getNextHighestDepth());
   mask.beginFill(0,100);
   colCircle(mask,COL_CARDS_X + 30,cy,12.5);
   mask.endFill();
   icon.setMask(mask);
   h.name_txt = colText(h,"name_txt","",COL_CARDS_X + 52,cy - 15,380,30,20,0xFFFFFF,"left");
   h.name_txt.filters = colOutline();
   h.count_txt = colText(h,"count_txt","",COL_CARDS_X + COL_CARDS_W - 236,cy - 16,220,20,13,0xFFE08A,"right");
   h.count_txt.filters = colOutline();
   h.bar = h.createEmptyMovieClip("bar",h.getNextHighestDepth());

   var side = colText(colRoot,"categories_txt","Categories",COL_SIDE_X + 18,cy - 13,COL_SIDE_W - 36,26,17,0xFFFFFF,"left");
   side.filters = colOutline();
   colRoot.empty_txt = colText(colRoot,"empty_txt","",COL_CARDS_X + 40,COL_PANEL_Y + 200,COL_CARDS_W - 80,80,18,COL_SOFT);
   colRoot.empty_txt.multiline = colRoot.empty_txt.wordWrap = true;
   colRoot.empty_txt._visible = false;
}

function colDrawHeaderBar(done, total)
{
   var b = colRoot.header.bar;
   var w = 140;
   var x = COL_CARDS_X + COL_CARDS_W - 16 - w;
   var y = COL_PANEL_Y + 24;
   b.clear();
   if(total <= 0)
   {
      return;
   }
   b.lineStyle(1,COL_RIM,30);
   b.beginFill(0x0A1440,60);
   colRoundRect(b,x,y,w,7,3.5);
   b.endFill();
   if(done > 0)
   {
      b.lineStyle();
      colGoldFill(b,x,y,w,7);
      colRoundRect(b,x,y,Math.max(7,Math.round(w * done / total)),7,3.5);
      b.endFill();
   }
}

// The gold close button and Main Menu pill, over the stock buttons (invisible hit areas, moved under them).
function colBuildButtons()
{
   var close = colRoot.close = colRoot.createEmptyMovieClip("close",colRoot.getNextHighestDepth());
   close._x = COL_CLOSE_X;
   close._y = COL_CLOSE_Y;
   close.draw = function(over)
   {
      this.clear();
      this.lineStyle(2,0xFFF3C4,100);
      this.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-17,y:-17,w:34,h:34,r:Math.PI / 2});
      colCircle(this,0,0,17);
      this.endFill();
      this.lineStyle(4,COL_INK,100);
      this.moveTo(-6,-6);
      this.lineTo(6,6);
      this.moveTo(6,-6);
      this.lineTo(-6,6);
      this.filters = over ? [new flash.filters.GlowFilter(COL_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,6,6,1,2)];
   };
   close.draw(false);

   var pill = colRoot.menuPill = colRoot.createEmptyMovieClip("menuPill",colRoot.getNextHighestDepth());
   pill._x = COL_W / 2;
   pill._y = COL_MENU_Y;
   pill.bg = pill.createEmptyMovieClip("bg",1);
   pill.draw = function(over)
   {
      var g = this.bg;
      g.clear();
      g.lineStyle(2,0xFFF3C4,100);
      g.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-92,y:-21,w:184,h:42,r:Math.PI / 2});
      colRoundRect(g,-92,-21,184,42,21);
      g.endFill();
      g.lineStyle();
      g.beginFill(0xFFFFFF,over ? 40 : 30);
      colRoundRect(g,-82,-17,164,12,6);
      g.endFill();
      g.filters = over ? [new flash.filters.GlowFilter(COL_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,8,8,1,2)];
   };
   pill.draw(false);
   colText(pill,"label_txt","Main Menu",-92,-12,184,26,18,COL_INK);
}

function colSyncButton(mine, stock, over)
{
   if(over == mine.over)
   {
      return;
   }
   mine.over = over;
   mine.draw(over);
   if(mine == colRoot.close)
   {
      gs.TweenLite.to(mine,0.6,{_rotation:over ? 90 : 0,_xscale:over ? 112 : 100,_yscale:over ? 112 : 100,ease:colElasticOut});
   }
   else
   {
      gs.TweenLite.to(mine,0.3,{_xscale:over ? 106 : 100,_yscale:over ? 106 : 100,ease:colStrong()});
   }
}

// --- category rows ---------------------------------------------------------------------

function colMakeRow(layer, i)
{
   var skin = layer.createEmptyMovieClip("row" + i,layer.getNextHighestDepth());
   skin.index = i;
   skin.anim = skin.createEmptyMovieClip("anim",1);
   var body = skin.body = skin.anim.createEmptyMovieClip("body",1);
   body.bg = body.createEmptyMovieClip("bg",1);
   var cx = COL_ROW_X + 23;
   var cy = COL_ROW_Y + COL_ROW_H / 2;
   var icon = body.icon = body.createEmptyMovieClip("icon",2);
   icon._x = cx;
   icon._y = cy;
   colImageHolder(icon,32);
   var mask = body.createEmptyMovieClip("iconMask",3);
   mask.beginFill(0,100);
   colCircle(mask,cx,cy,15);
   mask.endFill();
   icon.setMask(mask);
   body.label_txt = colText(body,"label_txt","",COL_ROW_X + 46,cy - 13,COL_ROW_W - 54,26,16,0xFFFFFF,"left");
   body.label_txt.filters = colOutline();
   return skin;
}

function colUpdateRow(skin, r)
{
   var d = r.data;
   if(d == skin.data)
   {
      return;
   }
   skin.data = d;
   colSetFitted(skin.body.label_txt,String(d.label));
   _root.frSetIconByIndex(skin.body.icon.holder,d.iconId);
   // Slides in when it first shows something.
   skin.anim._alpha = 0;
   skin.anim._x = -14;
   gs.TweenLite.to(skin.anim,0.35,{_alpha:100,_x:0,delay:skin.index * 0.03,ease:colStrong()});
}

function colDrawRow(skin, changed)
{
   var b = skin.body;
   var g = b.bg;
   var x = COL_ROW_X;
   var y = COL_ROW_Y;
   var w = COL_ROW_W;
   var h = COL_ROW_H;
   var sel = skin.selected;
   var over = skin.over;
   g.clear();
   if(sel)
   {
      colGradientRect(g,x,y,w,h,14,0x2B4FA8,0x17286A,100,COL_GOLD,100,2);
      // The gold wash from the left, and the pointer at the cards.
      g.lineStyle();
      g.beginGradientFill("linear",[COL_GOLD,COL_GOLD],[over ? 60 : 45,0],[0,210],{matrixType:"box",x:x,y:y,w:w,h:h,r:0});
      colRoundRect(g,x + 1,y + 1,w - 2,h - 2,13);
      g.endFill();
      g.lineStyle(1,0xFFF3C4,80);
      colGoldFill(g,x + w,y + h / 2 - 8,9,16);
      g.moveTo(x + w + 1,y + h / 2 - 8);
      g.lineTo(x + w + 9,y + h / 2);
      g.lineTo(x + w + 1,y + h / 2 + 8);
      g.lineTo(x + w + 1,y + h / 2 - 8);
      g.endFill();
   }
   else if(over)
   {
      colGradientRect(g,x,y,w,h,14,0x3F66D6,0x1D307A,100,0xFFE08A,100,1.5);
   }
   else
   {
      colGradientRect(g,x,y,w,h,14,0x22408F,0x152458,90,COL_RIM,30,1);
   }
   // Glassy highlight across the top.
   g.lineStyle();
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[over || sel ? 20 : 12,0],[0,255],{matrixType:"box",x:0,y:y + 2,w:10,h:16,r:Math.PI / 2});
   colRoundRect(g,x + 3,y + 2,w - 6,16,9);
   g.endFill();
   colSocket(g,x + 23,y + h / 2,17,sel ? COL_GOLD : 0xFFFFFF,sel ? 100 : 80,sel ? 2 : 1.5);
   g.filters = sel ? [new flash.filters.GlowFilter(COL_GOLD,0.4,10,10,2,2)] : (over ? [new flash.filters.GlowFilter(COL_GOLD,0.35,10,10,2,2)] : []);
   b.label_txt.textColor = sel ? 0xFFE08A : 0xFFFFFF;

   if(changed)
   {
      gs.TweenLite.to(b,0.25,{_x:over || sel ? 4 : 0,ease:colStrong()});
      gs.TweenLite.to(b.icon,0.7,{_xscale:over ? 115 : 100,_yscale:over ? 115 : 100,_rotation:over ? -8 : 0,ease:colElasticOut});
   }
}

// --- collection cards ------------------------------------------------------------------

function colMakeCard(layer, i)
{
   var skin = layer.createEmptyMovieClip("card" + i,layer.getNextHighestDepth());
   skin.index = i;
   skin.anim = skin.createEmptyMovieClip("anim",1);
   var body = skin.body = skin.anim.createEmptyMovieClip("body",1);
   body.bg = body.createEmptyMovieClip("bg",1);
   var cx = COL_CARD_X + COL_CARD_W / 2;
   var icon = body.icon = body.createEmptyMovieClip("icon",2);
   icon._x = cx;
   icon._y = COL_SOCKET_Y;
   icon._xscale = icon._yscale = COL_ICON_SCALE;
   colImageHolder(icon,64);
   // Rounds the icon into the socket (and crops it there when it grows on hover).
   var mask = body.createEmptyMovieClip("iconMask",3);
   mask.beginFill(0,100);
   colCircle(mask,cx,COL_SOCKET_Y,COL_SOCKET_R - 3);
   mask.endFill();
   icon.setMask(mask);

   // The check on a finished collection's socket.
   var check = body.check = body.createEmptyMovieClip("check",4);
   check._x = cx + 31;
   check._y = COL_SOCKET_Y - 27;
   check.lineStyle(2,0xFFF3C4,100);
   colGoldFill(check,-12,-12,24,24);
   colCircle(check,0,0,12);
   check.endFill();
   check.lineStyle(3.5,COL_INK,100);
   check.moveTo(-5.5,0);
   check.lineTo(-1.5,4.5);
   check.lineTo(6,-4.5);
   check.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];
   check._visible = false;

   var name = body.name_txt = colText(body,"name_txt","",COL_CARD_X + 8,COL_SOCKET_Y + COL_SOCKET_R + 3,COL_CARD_W - 16,34,14,0xFFFFFF);
   name.multiline = name.wordWrap = true;
   name.filters = colOutline();
   body.progress_txt = colText(body,"progress_txt","",COL_CARD_X + 20,COL_BAR_Y - 2,COL_CARD_W - 40,20,12,0xFFFFFF);
   body.progress_txt.filters = colOutline();
   return skin;
}

function colUpdateCard(skin, r)
{
   var d = r.data;
   var progress = colParseProgress(r.statusTxt.text,d);
   var key = progress[0] + " / " + progress[1];
   if(d == skin.data && key == skin.progressKey)
   {
      return;
   }
   var fresh = d != skin.data;
   skin.data = d;
   skin.progressKey = key;
   skin.got = progress[0];
   skin.total = progress[1];
   skin.complete = skin.total > 0 && skin.got >= skin.total;
   skin.body.progress_txt.text = key;
   if(fresh)
   {
      // One line sits in the middle of the two-line space.
      var t = skin.body.name_txt;
      colSetFitted(t,String(d.name));
      t._y = t.baseY + Math.max(0,(t._height - 4 - t.textHeight) / 2);
      _root.frSetIconByIndex(skin.body.icon.holder,d.iconId,d.tintId);
      skin.anim._alpha = 0;
      skin.anim._y = 12;
      gs.TweenLite.to(skin.anim,0.35,{_alpha:100,_y:0,delay:skin.index * 0.03,ease:colStrong()});
   }
   colDrawCard(skin,false);
}

function colDrawCard(skin, changed)
{
   var b = skin.body;
   var g = b.bg;
   var x = COL_CARD_X;
   var y = COL_CARD_Y;
   var w = COL_CARD_W;
   var h = COL_CARD_H;
   var cx = x + w / 2;
   var over = skin.over;
   var done = skin.complete;
   g.clear();
   if(over)
   {
      colGradientRect(g,x,y,w,h,18,0x3F66D6,0x1D307A,100,done ? COL_GOLD : 0xFFE08A,100,done ? 3 : 2);
   }
   else
   {
      colGradientRect(g,x,y,w,h,18,0x284A9E,0x162660,100,done ? COL_GOLD : COL_RIM,done ? 100 : 40,done ? 3 : 1.5);
   }
   g.lineStyle();
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[16,0],[0,255],{matrixType:"box",x:0,y:y + 3,w:10,h:46,r:Math.PI / 2});
   colRoundRect(g,x + 3,y + 3,w - 6,46,15);
   g.endFill();

   // A soft halo behind the socket, gold once the collection is done.
   var halo = done ? COL_GOLD : 0x8FB4FF;
   g.beginGradientFill("radial",[halo,halo],[over ? 60 : 40,0],[0,255],{matrixType:"box",x:cx - 50,y:COL_SOCKET_Y - 50,w:100,h:100,r:0});
   colCircle(g,cx,COL_SOCKET_Y,48);
   g.endFill();
   g.lineStyle();
   g.beginFill(0x050A24,30);
   colCircle(g,cx,COL_SOCKET_Y + 3,COL_SOCKET_R + 1);
   g.endFill();
   colSocket(g,cx,COL_SOCKET_Y,COL_SOCKET_R,done ? COL_GOLD : 0xFFFFFF,done ? 100 : 85,done ? 3 : 2);

   // The progress bar, its track recessed and the fill gold with a shine.
   var bx = x + 20;
   var bw = w - 40;
   g.lineStyle(1,COL_RIM,30);
   g.beginFill(0x0A1440,65);
   colRoundRect(g,bx,COL_BAR_Y,bw,COL_BAR_H,COL_BAR_H / 2);
   g.endFill();
   if(skin.total > 0 && skin.got > 0)
   {
      var fw = Math.max(COL_BAR_H - 2,Math.round((bw - 2) * Math.min(1,skin.got / skin.total)));
      g.lineStyle();
      colGoldFill(g,bx,COL_BAR_Y,bw,COL_BAR_H);
      colRoundRect(g,bx + 1,COL_BAR_Y + 1,fw,COL_BAR_H - 2,(COL_BAR_H - 2) / 2);
      g.endFill();
      if(fw > 12)
      {
         g.beginFill(0xFFFFFF,40);
         colRoundRect(g,bx + 5,COL_BAR_Y + 3,fw - 8,3,1.5);
         g.endFill();
      }
   }
   b.check._visible = done;
   b.progress_txt.textColor = done ? 0xFFF3C4 : 0xFFFFFF;

   if(over)
   {
      g.filters = [new flash.filters.GlowFilter(COL_GOLD,0.55,14,14,2,2)];
   }
   else if(done)
   {
      g.filters = [new flash.filters.GlowFilter(COL_GOLD,0.35,10,10,2,2)];
   }
   else
   {
      g.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
   }

   if(changed)
   {
      gs.TweenLite.to(b,0.25,{_y:skin.down ? 1 : (over ? -3 : 0),ease:colStrong()});
      gs.TweenLite.to(b.icon,0.8,{_xscale:over ? COL_ICON_HOVER : COL_ICON_SCALE,_yscale:over ? COL_ICON_HOVER : COL_ICON_SCALE,_rotation:over ? -6 : 0,ease:colElasticOut});
   }
}

// --- following the stock lists ---------------------------------------------------------

// One skin per stock renderer: placed on it, refreshed when its data changes, redrawn when its state does.
function colSyncList(list, skins, layer, make, update, draw)
{
   var renderers = list.renderers;
   var n = renderers == undefined ? 0 : renderers.length;
   var count = Math.max(n,skins.length);
   var i;
   for(i = 0; i < count; i++)
   {
      var r = renderers[i];
      var skin = skins[i];
      if(i >= n || r._parent == undefined || !r._visible || r.data == undefined)
      {
         if(skin != undefined)
         {
            skin._visible = false;
            skin.data = undefined;
         }
         continue;
      }
      if(skin == undefined)
      {
         skin = skins[i] = make(layer,i);
      }
      var p = {x:0,y:0};
      r.localToGlobal(p);
      colRoot.globalToLocal(p);
      skin._x = p.x;
      skin._y = p.y;
      skin._visible = true;
      var over = colIsOver(r);
      var down = r.state == "down";
      var selected = r.selected == true;
      var changed = over != skin.over || down != skin.down || selected != skin.selected;
      skin.over = over;
      skin.down = down;
      skin.selected = selected;
      update(skin,r);
      if(changed || skin.drawn != true)
      {
         skin.drawn = true;
         draw(skin,changed);
      }
   }
}

// The cards' scrollbar (stock, invisible, attached when there are more than 9): a slim track with a gold thumb.
function colSyncScrollBar(sb)
{
   var s = colRoot.scroll;
   if(sb == undefined || sb == null || sb._parent == undefined || !sb._visible)
   {
      s._visible = false;
      s.key = undefined;
      return;
   }
   s._visible = true;
   var b = sb.getBounds(colRoot);
   var t = sb.thumb.getBounds(colRoot);
   var key = Math.round(b.xMin) + "," + Math.round(b.yMin) + "," + Math.round(b.yMax) + "," + Math.round(t.yMin) + "," + Math.round(t.yMax) + "," + sb.thumb._visible;
   if(key == s.key)
   {
      return;
   }
   s.key = key;
   var cx = Math.round((b.xMin + b.xMax) / 2);
   s.clear();
   s.lineStyle(1,COL_RIM,30);
   s.beginFill(0x0A1440,55);
   colRoundRect(s,cx - 6,b.yMin + 20,12,b.yMax - b.yMin - 40,6);
   s.endFill();
   // Chevrons where the stock arrows are.
   s.lineStyle(3,COL_SOFT,80);
   s.moveTo(cx - 6,b.yMin + 12);
   s.lineTo(cx,b.yMin + 6);
   s.lineTo(cx + 6,b.yMin + 12);
   s.moveTo(cx - 6,b.yMax - 12);
   s.lineTo(cx,b.yMax - 6);
   s.lineTo(cx + 6,b.yMax - 12);
   if(sb.thumb._visible && t.yMax > t.yMin)
   {
      s.lineStyle(1,0xFFF3C4,80);
      colGoldFill(s,cx - 5,t.yMin,10,t.yMax - t.yMin);
      colRoundRect(s,cx - 5,t.yMin + 1,10,Math.max(10,t.yMax - t.yMin - 2),5);
      s.endFill();
   }
}

function colSyncHeader(win)
{
   var h = colRoot.header;
   var sel = win.navigationList.selectedIndex;
   var cat = win.collectionNavigation[sel];
   var data = win.collectionData;
   var done = 0;
   var total = data.length;
   var i;
   for(i = 0; i < total; i++)
   {
      var c = colCount(data[i]);
      if(c[1] > 0 && c[0] >= c[1])
      {
         done++;
      }
   }
   var label = cat == undefined ? "" : String(cat.label);
   var key = sel + "|" + label + "|" + done + "|" + total;
   if(key == h.key)
   {
      return;
   }
   if(cat != h.cat)
   {
      h.cat = cat;
      if(cat != undefined)
      {
         _root.frSetIconByIndex(h.icon.holder,cat.iconId);
      }
      h.icon._visible = cat != undefined;
   }
   h.key = key;
   colSetFitted(h.name_txt,label);
   h.count_txt.text = total > 0 ? done + " of " + total + " complete" : "";
   colDrawHeaderBar(done,total);
}

function colSync()
{
   var win = _root.CollectionsBrowser;
   colSyncList(win.navigationList,colNavSkins,colRoot.navLayer,colMakeRow,colUpdateRow,colDrawRow);
   colSyncList(win.collectionList,colCardSkins,colRoot.cardLayer,colMakeCard,colUpdateCard,colDrawCard);
   colSyncScrollBar(win.collectionList.scrollBar);
   colSyncHeader(win);
   colSyncButton(colRoot.close,win.exitBookButton,colIsOver(win.exitBookButton));
   colSyncButton(colRoot.menuPill,win.mainMenuButton,colIsOver(win.mainMenuButton));
   // The stock "no collections" line (Lua's string), shown where the stock shows it.
   var empty = win.noCollectionsTxt;
   if(colRoot.empty_txt.text != empty.text)
   {
      colRoot.empty_txt.text = empty.text;
   }
   colRoot.empty_txt._visible = empty._visible && empty.text.length > 1;
}

function colBuild()
{
   var win = _root.CollectionsBrowser;
   // The stock window is the hit layer: invisible, but its lists, buttons and scrollbar still take the mouse.
   win._alpha = 0;
   win.navigationList._x = COL_NAV_X;
   win.collectionList._x = COL_LIST_X;
   win.navigationList._y = win.collectionList._y = COL_LIST_Y;
   colCenterOn(win.exitBookButton,COL_CLOSE_X,COL_CLOSE_Y);
   colCenterOn(win.mainMenuButton,COL_W / 2,COL_MENU_Y);

   // Has no mouse handlers, so the mouse goes through it to the stock window under it.
   colRoot = _root.createEmptyMovieClip("sanctuaryCollections",_root.getNextHighestDepth());
   colDrawFrame(colRoot.createEmptyMovieClip("bg",1));
   colRoot.navLayer = colRoot.createEmptyMovieClip("navLayer",2);
   colRoot.cardLayer = colRoot.createEmptyMovieClip("cardLayer",3);
   colRoot.scroll = colRoot.createEmptyMovieClip("scroll",4);
   colBuildTitle();
   colBuildHeader();
   colBuildButtons();
   colRoot.onEnterFrame = colSync;
   colSync();
}

colBuild();
// ===== End Sanctuary Collections =====

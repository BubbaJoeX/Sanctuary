// ===== Sanctuary Player Inspect =====
// Appended to PlayerInspect's main timeline by build_client_mods.py.
// Redraws the inspect window (another player's equipment, opened from their player card) in the equipment pane's
// style (ClientMods/equipment/equipment_pane.as): a navy glass frame that follows the stock backdrop as it grows
// with the rows, a recessed header with the player's portrait in a gold-rimmed frosted socket, their name as the
// gold title and their guild in soft blue, and each equipped item as a glass card with its art a little bigger in
// a frosted socket (empty slots are recessed). Cards lift with a gold glow and pop their art on hover, where the
// stock shows the item's tooltip. The jobs flyout is a navy glass bubble. Members Only and Station Cash store
// badges are gone (clicking an item still does what it did). The close button is the navigation bar's.

var PIN_GOLD = 0xFFC531;
var PIN_RIM = 0x9DB8FF;
var PIN_SOFT = 0xC9D6FF;
// The header band, from the backdrop's top; the rows start lower to make room for its gold rule.
var PIN_BAND_Y = 6;
var PIN_BAND_H = 82;
var PIN_ROWS_Y = 120;
// An item's card inside its DisplayItem (the stock plate is a 66px shape at (19, 0)) and the items' spacing.
var PIN_SPACER = 106;
var PIN_CARD = [3,-9,98,117];
// Rows are stacked flush by their height, so each card keeps a clear strip under it as the gap to the next row.
var PIN_ROW_GAP = 8;
var PIN_SOCKET_X = 52;
var PIN_SOCKET_Y = 33;
var PIN_SOCKET = 70;
// The item icon (64px art) resting and hovered; the stock hover is 130%.
var PIN_ICON_SCALE = 104;
var PIN_ICON_HOVER = 118;

var pinFrame;

// --- drawing helpers -------------------------------------------------------------------

function pinRoundRect(mc, x, y, w, h, r)
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

function pinGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   pinRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function pinSparkle(mc, x, y, r, alpha)
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

function pinOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function pinShadow()
{
   return [new flash.filters.DropShadowFilter(1,90,0x0A1030,0.9,2,2,1,1)];
}

// The frosted socket behind stock art (the icons are made for light plates), a rounded square around (cx, cy).
function pinSocket(mc, cx, cy, size, rim, rimAlpha, rimWidth)
{
   var h = size / 2;
   mc.lineStyle();
   mc.beginFill(0x050A24,30);
   pinRoundRect(mc,cx - h,cy - h + 3,size,size,size * 0.22);
   mc.endFill();
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:cx - size * 0.65,y:cy - size * 0.75,w:size * 1.3,h:size * 1.3,r:0});
   pinRoundRect(mc,cx - h,cy - h,size,size,size * 0.22);
   mc.endFill();
}

function pinMask(parent, name, cx, cy, size)
{
   var m = parent.createEmptyMovieClip(name,parent.getNextHighestDepth());
   var h = size / 2;
   m.beginFill(0,100);
   pinRoundRect(m,cx - h,cy - h,size,size,size * 0.2);
   m.endFill();
   return m;
}

// Puts a stock field in a box and shrinks its font from size until the text fits (frSetTextPreserveWidth
// puts fields back in their stock boxes every time it's called).
function pinFit(t, x, y, w, h, size, color, align)
{
   t._x = x;
   t._y = y;
   t._width = w;
   t._height = h;
   var f = t.getTextFormat();
   f.size = size;
   f.color = color;
   f.align = align;
   t.setTextFormat(f);
   while((t.textHeight > h - 4 || t.textWidth > w - 4) && f.size > 9)
   {
      f.size -= 1;
      t.setTextFormat(f);
   }
}

function pinElastic()
{
   return mx.transitions.easing.Elastic.easeOut;
}

function pinStrong()
{
   return mx.transitions.easing.Strong.easeOut;
}

// --- frame -----------------------------------------------------------------------------

// Drawn from the stock backdrop's bounds, which grow with the rows (repositionEquipment).
function pinDrawFrame()
{
   var bg = pinFrame.bg;
   var x = background._x + 4;
   var y = background._y + 4;
   var w = background._width - 8;
   var h = background._height - 8;
   var key = x + "," + y + "," + w + "," + h;
   if(key == pinFrame.key)
   {
      return;
   }
   pinFrame.key = key;
   bg.clear();
   pinGradientRect(bg,x,y,w,h,30,0x2E4FB0,0x141F55,100,PIN_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:x - 140,y:y - 260,w:w + 200,h:520,r:0});
   pinRoundRect(bg,x + 2,y + 2,w - 4,h - 4,28);
   bg.endFill();

   // The header band behind the portrait, badge and name, with sparkles and a gold rule under it.
   var by = background._y + PIN_BAND_Y;
   bg.lineStyle(1,PIN_RIM,22);
   bg.beginFill(0x0A1440,38);
   pinRoundRect(bg,x + 8,by,w - 16,PIN_BAND_H,24);
   bg.endFill();
   var s = 3131;
   var i;
   for(i = 0; i < 18; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + 20 + s % (w - 40);
      s = (s * 1103515245 + 12345) % 2147483648;
      pinSparkle(bg,sx,by + 6 + s % (PIN_BAND_H - 12),1 + s % 2,18 + s % 40);
   }
   bg.lineStyle();
   bg.beginGradientFill("linear",[PIN_GOLD,PIN_GOLD,PIN_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:x + 30,y:0,w:w - 60,h:2,r:0});
   pinRoundRect(bg,x + 30,by + PIN_BAND_H + 6,w - 60,2,1);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
}

function pinBuildFrame()
{
   // Under the backdrop (the movie's first depth), which stays as the layout's measure, invisible.
   pinFrame = _root.createEmptyMovieClip("sanctuaryInspect",-16384);
   pinFrame.bg = pinFrame.createEmptyMovieClip("bg",1);
   background._alpha = 0;
   pinDrawFrame();
}

// --- header ----------------------------------------------------------------------------

function pinStyleInfo(info)
{
   if(!info.sanctuary)
   {
      info.sanctuary = true;
      // The portrait: its brown frame becomes a gold-rimmed frosted socket (between the frame and the picture).
      var p = info.portrait;
      p.background._alpha = 0;
      var sock = p.createEmptyMovieClip("sanctuarySocket",-16382);
      pinSocket(sock,0,-2.5,80,PIN_GOLD,100,3);
      sock.filters = [new flash.filters.GlowFilter(PIN_GOLD,0.35,10,10,2,2)];
      // The picture loads into its holder, so the mask on the icon stays.
      p.icon.setMask(pinMask(p,"sanctuaryMask",0,-2.5,74));
      p.icon._xscale = p.icon._yscale = 106;
   }
   // The name is the gold title, the guild under it in soft blue.
   pinFit(info.name,info.name._x,14,205,32,24,0xFFE08A,"left");
   info.name.filters = pinOutline();
   pinFit(info.guild,info.guild._x,46,205,24,15,PIN_SOFT,"left");
   info.guild.filters = pinShadow();
}

// The jobs flyout's bubble (its stock art is the flyout's background clip, sized to the badges).
function pinDrawFlyout(w, h)
{
   var fly = inspect.display.JobsFlyout.m_This;
   if(fly == undefined)
   {
      return;
   }
   fly.background._alpha = 0;
   var b = fly.sanctuaryBubble;
   if(b == undefined)
   {
      b = fly.sanctuaryBubble = fly.createEmptyMovieClip("sanctuaryBubble",-16384);
      b.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.6,10,10,1,2)];
   }
   var t = 12;
   b.clear();
   pinGradientRect(b,t,0,w - t,h,18,0x2E4FB0,0x162660,97,0xFFE08A,90,2);
   // The tail, pointing at the job badge.
   b.lineStyle(2,0xFFE08A,90);
   b.beginFill(0x24409A,97);
   b.moveTo(t + 1,h / 2 - 9);
   b.lineTo(0,h / 2);
   b.lineTo(t + 1,h / 2 + 9);
   b.endFill();
   b.lineStyle();
   b.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[18,0],[0,255],{matrixType:"box",x:0,y:3,w:10,h:30,r:Math.PI / 2});
   pinRoundRect(b,t + 3,3,w - t - 6,30,14);
   b.endFill();
}

// --- items -----------------------------------------------------------------------------

function pinDrawCard(item)
{
   var c = item.sanctuaryCard;
   var x = PIN_CARD[0];
   var y = PIN_CARD[1];
   var w = PIN_CARD[2];
   var h = PIN_CARD[3];
   var empty = item.sanctuaryEmpty;
   var over = item.sanctuaryOver;
   c.clear();
   c.lineStyle();
   c.beginFill(0,0);
   pinRoundRect(c,x,y,w,h + PIN_ROW_GAP,0);
   c.endFill();
   if(over)
   {
      pinGradientRect(c,x,y,w,h,16,0x3F66D6,0x1D307A,100,0xFFE08A,100,2);
   }
   else if(empty)
   {
      pinGradientRect(c,x,y,w,h,16,0x0A1440,0x0A1440,40,PIN_RIM,25,1);
   }
   else
   {
      pinGradientRect(c,x,y,w,h,16,0x284A9E,0x162660,100,PIN_RIM,40,1.5);
   }
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[empty ? 6 : 16,0],[0,255],{matrixType:"box",x:0,y:y + 3,w:10,h:36,r:Math.PI / 2});
   pinRoundRect(c,x + 3,y + 3,w - 6,36,13);
   c.endFill();
   if(over)
   {
      c.filters = [new flash.filters.GlowFilter(PIN_GOLD,0.55,14,14,2,2)];
   }
   else if(empty)
   {
      c.filters = [];
   }
   else
   {
      c.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
   }

   // The socket: frosted for an item, a navy recess with a soft ring for an empty slot.
   var s = item.sanctuarySocket;
   s.clear();
   if(empty)
   {
      var r = PIN_SOCKET / 2;
      s.lineStyle(2,PIN_RIM,50);
      s.beginFill(0x0A1440,55);
      pinRoundRect(s,PIN_SOCKET_X - r,PIN_SOCKET_Y - r,PIN_SOCKET,PIN_SOCKET,PIN_SOCKET * 0.22);
      s.endFill();
   }
   else
   {
      pinSocket(s,PIN_SOCKET_X,PIN_SOCKET_Y,PIN_SOCKET,over ? 0xFFE08A : 0xFFFFFF,over ? 100 : 85,2);
   }
}

// A slot is empty when Lua sent no item for it, or one without art (its fields all -1).
function pinIsEmpty(item)
{
   var data = item.item;
   return data == undefined || !(Number(data[inspect.item.BaseAttributes.ICON_ID]) > 0);
}

function pinStyleItem(item)
{
   if(item.sanctuaryCard == undefined)
   {
      item.sanctuaryCard = item.createEmptyMovieClip("sanctuaryCard",-16384);
      // The stock plate is a shape at the first depth; the socket goes over it, under the icon.
      item.icon.swapDepths(1001);
      item.sanctuarySocket = item.createEmptyMovieClip("sanctuarySocket",1000);
      item.icon._xscale = item.icon._yscale = PIN_ICON_SCALE;
      item.icon.setMask(pinMask(item,"sanctuaryMask",PIN_SOCKET_X,PIN_SOCKET_Y,PIN_SOCKET - 4));
   }
   // Members-only and Station Cash no longer exist.
   item.membersOnly._visible = false;
   item.storeItem._visible = false;
   item.sanctuaryEmpty = pinIsEmpty(item);
   pinDrawCard(item);
   pinFit(item.label,PIN_CARD[0] + 4,PIN_SOCKET_Y + PIN_SOCKET / 2 + 3,PIN_CARD[2] - 8,34,15,item.sanctuaryEmpty ? PIN_SOFT : 0xFFFFFF,"center");
   item.label.filters = pinOutline();
}

// --- stock hooks -----------------------------------------------------------------------

function pinPatch()
{
   inspect.display.DisplayRow.SPACER = PIN_SPACER;

   // Each item is styled as it's added, then the row is laid out again (its size changed), which also has
   // repositionEquipment resize the window to fit.
   var row = inspect.display.DisplayRow.prototype;
   var addChild = row.addChild;
   row.addChild = function(obj)
   {
      var r = addChild.apply(this,arguments);
      pinStyleItem(this.m_Items[this.m_Items.length - 1]);
      this.update();
      return r;
   };

   var item = inspect.display.DisplayItem.prototype;
   var update = item.update;
   item.update = function()
   {
      var r = update.apply(this,arguments);
      pinStyleItem(this);
      return r;
   };
   // The stock hover pops the icon to 130%; here the bigger icon pops, and the card lifts and glows.
   item.scaleIcon = function(pct)
   {
      var over = pct > 100;
      if(this.sanctuaryCard != undefined)
      {
         this.sanctuaryOver = over;
         pinDrawCard(this);
         gs.TweenLite.to(this.sanctuaryCard,0.25,{_y:over ? -3 : 0,ease:pinStrong()});
         gs.TweenLite.to(this.sanctuarySocket,0.25,{_y:over ? -3 : 0,ease:pinStrong()});
      }
      var s = over ? PIN_ICON_HOVER : PIN_ICON_SCALE;
      gs.TweenLite.to(this.icon,over ? 0.6 : 0.2,{_xscale:s,_yscale:s,_y:PIN_SOCKET_Y - 1 + (over ? -3 : 0),ease:over ? pinElastic() : pinStrong()});
   };

   var info = inspect.display.DisplayPlayerInfo.prototype;
   var infoUpdate = info.update;
   info.update = function()
   {
      var r = infoUpdate.apply(this,arguments);
      pinStyleInfo(this);
      return r;
   };

   var resize = inspect.display.JobsFlyout.resize;
   inspect.display.JobsFlyout.resize = function(width, height)
   {
      var r = resize.apply(this,arguments);
      pinDrawFlyout(width,height);
      return r;
   };

   var mini = inspect.display.MiniJobBadge.prototype;
   var miniLoad = mini.onLoad;
   mini.onLoad = function()
   {
      var r = miniLoad.apply(this,arguments);
      this.label.textColor = 0xFFFFFF;
      this.label.filters = pinOutline();
      return r;
   };
}

// The frame follows the backdrop, which grows with the rows.
var pinRepositionEquipment = repositionEquipment;
repositionEquipment = function()
{
   var r = pinRepositionEquipment.apply(this,arguments);
   pinDrawFrame();
   return r;
};

// The achievement points sit on the trophy; gold with an outline, and a soft gold halo behind the trophy.
var pinSetAchievementScoreText = setAchievementScoreText_lua;
setAchievementScoreText_lua = function(points)
{
   var r = pinSetAchievementScoreText.apply(this,arguments);
   var t = m_achievementScore.achievementText;
   t.textColor = 0xFFE08A;
   t.filters = pinOutline();
   return r;
};

pinPatch();
rowStartY = PIN_ROWS_Y;
pinBuildFrame();
var pinHalo = m_achievementScore.createEmptyMovieClip("sanctuaryHalo",-16384);
pinHalo.beginGradientFill("radial",[PIN_GOLD,PIN_GOLD],[45,0],[0,255],{matrixType:"box",x:0.5,y:-37,w:74,h:74,r:0});
pinHalo.moveTo(0.5,0);
pinHalo.curveTo(0.5,-37,37.5,-37);
pinHalo.curveTo(74.5,-37,74.5,0);
pinHalo.curveTo(74.5,37,37.5,37);
pinHalo.curveTo(0.5,37,0.5,0);
pinHalo.endFill();
// ===== End Sanctuary Player Inspect =====

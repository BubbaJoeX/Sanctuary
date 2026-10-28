// ===== Sanctuary Mystery Chest =====
// Appended to MysteryChest's main timeline by build_client_mods.py.
// Redraws the Mystery Chest window (pick a chest, play its random activity) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as, collections/collections_browser.as): a navy glass frame with the
// header in gold, a gold close button and a glass help button, a recessed list of chests as glass rows (the chest
// in a round frosted socket, its name, blurb and a NOW / Tomorrow chip; the selected row rimmed in gold with a
// gold wash, hover lighter), a glass detail card with the chest's art in a rounded frame, and a gold Play pill
// that carries the activity's icon in a frosted medallion (recessed while the chest isn't available). The stock
// buttons and rows keep their roles (invisible where they're redrawn, still taking the mouse) and Lua fills them
// through setHeader_lua, setTotalItems_lua and setItemBackFromLua. Members-only badges stay hidden.

var MCH_W = 700;
var MCH_H = 720;
// The frame (the stock backdrop starts 39px down), the list's panel and the detail card.
var MCH_TOP = 40;
var MCH_LIST = [24,140,284,524];
var MCH_CARD = [316,140,362,524];
// The list's solid fill: each row's cover is drawn in it, over the stock row plate (a plain shape).
var MCH_PANEL = 0x111D52;
// A row inside its 160px slot (the stock plate is 227x158), its socket, and the chest art (a 64px icon at 128%).
var MCH_ROW = [4,3,219,152];
var MCH_SOCKET_X = 52.5;
var MCH_SOCKET_Y = 82;
var MCH_SOCKET_R = 40;
var MCH_ICON_SCALE = 115;
var MCH_ICON_HOVER = 126;
// The detail art's frame (the stock frame shape's border is masked off).
var MCH_ART = [323,155,332,279];
var MCH_CLOSE_X = 656;
var MCH_HELP_X = 614;
var MCH_BUTTON_Y = 84;
var MCH_GOLD = 0xFFC531;
var MCH_RIM = 0x9DB8FF;
var MCH_SOFT = 0xC9D6FF;
var MCH_INK = 0x3A2400;

var mchFrame;

// --- drawing helpers -------------------------------------------------------------------

function mchRoundRect(mc, x, y, w, h, r)
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

function mchGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   mchRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function mchCircle(mc, x, y, r)
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
function mchSocket(mc, x, y, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.25,y:y - r * 1.4,w:r * 2.5,h:r * 2.5,r:0});
   mchCircle(mc,x,y,r);
   mc.endFill();
}

function mchGoldFill(mc, x, y, w, h, over)
{
   mc.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
}

function mchText(mc, name, txt, x, y, w, h, size, color, align)
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

// Recolours a stock field's text (its fitters keep the format they find, so this sticks).
function mchColor(t, color, size)
{
   var f = t.getTextFormat();
   f.color = color;
   if(size != undefined)
   {
      f.size = size;
   }
   t.setTextFormat(f);
}

function mchSparkle(mc, x, y, r, alpha)
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

function mchOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function mchShadow()
{
   return [new flash.filters.DropShadowFilter(1,90,0x0A1030,0.9,2,2,1,1)];
}

function mchRule(mc, x, y, w)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[MCH_GOLD,MCH_GOLD,MCH_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:x,y:0,w:w,h:2,r:0});
   mchRoundRect(mc,x,y,w,2,1);
   mc.endFill();
}

// Penner's elastic ease out; this movie only carries mx.transitions.easing.Strong.
function mchElasticOut(t, b, c, d)
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

function mchStrong()
{
   return mx.transitions.easing.Strong.easeOut;
}

// Moves a stock clip so its bounds center on (x, y).
function mchCenterOn(mc, x, y)
{
   var b = mc.getBounds(_root);
   mc._x += x - (b.xMin + b.xMax) / 2;
   mc._y += y - (b.yMin + b.yMax) / 2;
}

// --- frame -----------------------------------------------------------------------------

function mchDrawFrame(bg)
{
   mchGradientRect(bg,4,MCH_TOP,MCH_W - 8,MCH_H - MCH_TOP - 14,30,0x2E4FB0,0x141F55,100,MCH_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-150,y:MCH_TOP - 300,w:820,h:700,r:0});
   mchRoundRect(bg,6,MCH_TOP + 2,MCH_W - 12,MCH_H - MCH_TOP - 18,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   mchRoundRect(bg,16,MCH_TOP + 12,MCH_W - 32,72,26);
   bg.endFill();

   // Sparkles in the header (fixed seed, same every time).
   var s = 7373;
   var i;
   for(i = 0; i < 26; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 34 + s % (MCH_W - 68);
      s = (s * 1103515245 + 12345) % 2147483648;
      mchSparkle(bg,x,MCH_TOP + 18 + s % 60,1 + s % 3,22 + s % 48);
   }

   // The list's panel (solid, so the rows' covers vanish into it) and the detail card.
   bg.lineStyle(1,MCH_RIM,25);
   bg.beginFill(MCH_PANEL,100);
   mchRoundRect(bg,MCH_LIST[0],MCH_LIST[1],MCH_LIST[2],MCH_LIST[3],22);
   bg.endFill();
   mchGradientRect(bg,MCH_CARD[0],MCH_CARD[1],MCH_CARD[2],MCH_CARD[3],22,0x284A9E,0x162660,100,MCH_RIM,40,1.5);
   bg.lineStyle();
   bg.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[16,0],[0,255],{matrixType:"box",x:0,y:MCH_CARD[1] + 3,w:10,h:46,r:Math.PI / 2});
   mchRoundRect(bg,MCH_CARD[0] + 3,MCH_CARD[1] + 3,MCH_CARD[2] - 6,46,15);
   bg.endFill();

   // The art's frame: a shadow and a frosted mat.
   var a = MCH_ART;
   bg.beginFill(0x050A24,35);
   mchRoundRect(bg,a[0] - 4,a[1] - 1,a[2] + 8,a[3] + 8,20);
   bg.endFill();
   bg.lineStyle(2,0xFFFFFF,85);
   bg.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:a[0] - 40,y:a[1] - 60,w:a[2] + 80,h:a[3] + 80,r:0});
   mchRoundRect(bg,a[0] - 3,a[1] - 3,a[2] + 6,a[3] + 6,19);
   bg.endFill();
   mchRule(bg,MCH_CARD[0] + 24,a[1] + a[3] + 52,MCH_CARD[2] - 48);
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
}

// The gold close circle and the glass help circle, drawn under the stock buttons (invisible over them).
function mchMakeCircle(name, gold)
{
   var c = mchFrame.createEmptyMovieClip(name,mchFrame.getNextHighestDepth());
   c.gold = gold;
   c.draw = function(over)
   {
      this.clear();
      if(this.gold)
      {
         this.lineStyle(2,0xFFF3C4,100);
         mchGoldFill(this,-17,-17,34,34,over);
         mchCircle(this,0,0,17);
         this.endFill();
         this.lineStyle(4,MCH_INK,100);
         this.moveTo(-6,-6);
         this.lineTo(6,6);
         this.moveTo(6,-6);
         this.lineTo(-6,6);
      }
      else
      {
         this.lineStyle(2,over ? 0xFFE08A : MCH_RIM,over ? 100 : 70);
         this.beginGradientFill("linear",over ? [0x3F66D6,0x1D307A] : [0x284A9E,0x162660],[100,100],[0,255],{matrixType:"box",x:-17,y:-17,w:34,h:34,r:Math.PI / 2});
         mchCircle(this,0,0,17);
         this.endFill();
      }
      this.filters = over ? [new flash.filters.GlowFilter(MCH_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,6,6,1,2)];
   };
   if(!gold)
   {
      var q = mchText(c,"q_txt","?",-17,-14,34,28,20,0xFFE08A);
      q.filters = mchShadow();
   }
   c.draw(false);
   return c;
}

// The Play pill: gold, with a frosted medallion at its left for the activity's icon and the label at its right.
function mchMakePlay()
{
   var p = mchFrame.createEmptyMovieClip("play",mchFrame.getNextHighestDepth());
   p.bg = p.createEmptyMovieClip("bg",1);
   p.label_txt = mchText(p,"label_txt","Play!",72,-15,96,32,22,MCH_INK);
   p.draw = function(over, enabled)
   {
      var g = this.bg;
      g.clear();
      if(enabled)
      {
         g.lineStyle(2,0xFFF3C4,100);
         mchGoldFill(g,0,-26,180,52,over);
      }
      else
      {
         g.lineStyle(2,MCH_RIM,40);
         g.beginFill(0x0A1440,60);
      }
      mchRoundRect(g,10,-26,170,52,26);
      g.endFill();
      g.lineStyle();
      g.beginFill(0xFFFFFF,enabled ? (over ? 40 : 30) : 8);
      mchRoundRect(g,30,-22,140,12,6);
      g.endFill();
      g.beginFill(0x050A24,30);
      mchCircle(g,36.5,4,37);
      g.endFill();
      mchSocket(g,36.5,0,37,enabled ? MCH_GOLD : 0xFFFFFF,enabled ? 100 : 70,3);
      g.filters = over && enabled ? [new flash.filters.GlowFilter(MCH_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,8,8,1,2)];
      this.label_txt.textColor = enabled ? MCH_INK : MCH_SOFT;
   };
   p.draw(false,true);
   return p;
}

function mchBuildFrame()
{
   mchFrame = _root.createEmptyMovieClip("sanctuaryMysteryChest",-16384);
   mchDrawFrame(mchFrame.createEmptyMovieClip("bg",1));
   mchFrame.scroll = mchFrame.createEmptyMovieClip("scroll",2);
   mchFrame.close = mchMakeCircle("close",true);
   mchFrame.help = mchMakeCircle("help",false);
   mchFrame.play = mchMakePlay();

   // The stock backdrop and the list's panel (an unnamed sprite at depth 38).
   background._visible = false;
   _root.getInstanceAtDepth(38 - 16384)._visible = false;
   exit_mc.up_mc._alpha = exit_mc.over_mc._alpha = exit_mc.down_mc._alpha = 0;
   help_icon_mc.up_mc._alpha = help_icon_mc.over_mc._alpha = help_icon_mc.down_mc._alpha = 0;
   play_mc.up_mc._alpha = play_mc.over_mc._alpha = play_mc.down_mc._alpha = 0;
   mchCenterOn(exit_mc,MCH_CLOSE_X,MCH_BUTTON_Y);
   mchCenterOn(help_icon_mc,MCH_HELP_X,MCH_BUTTON_Y);
   mchFrame.close._x = MCH_CLOSE_X;
   mchFrame.help._x = MCH_HELP_X;
   mchFrame.close._y = mchFrame.help._y = MCH_BUTTON_Y;
   // The Play button: its activity icon (73px, at its top left) sits in the pill's medallion.
   play_mc._x = MCH_CARD[0] + MCH_CARD[2] / 2 - 95;
   play_mc._y = MCH_CARD[1] + MCH_CARD[3] - 76;
   mchFrame.play._x = play_mc._x;
   mchFrame.play._y = play_mc._y + 35;

   // The chest's art, its corners rounded and the stock frame's white border masked off.
   var a = MCH_ART;
   var mask = _root.createEmptyMovieClip("sanctuaryArtMask",_root.getNextHighestDepth());
   mask.beginFill(0,100);
   mchRoundRect(mask,a[0],a[1],a[2],a[3],16);
   mask.endFill();
   largeImageHolder.setMask(mask);

   header_txt.filters = mchOutline();
   title_txt.filters = mchOutline();
   desc_txt.filters = mchShadow();
   mchStyleTexts();
}

function mchStyleTexts()
{
   mchColor(header_txt,0xFFE08A);
   mchColor(title_txt,0xFFE08A,20);
   mchColor(desc_txt,MCH_SOFT);
   title_txt._y = MCH_ART[1] + MCH_ART[3] + 14;
   desc_txt._y = MCH_ART[1] + MCH_ART[3] + 60;
   desc_txt._height = 86;
}

// --- rows ------------------------------------------------------------------------------

function mchStyleRow(item)
{
   if(item.sanctuaryRow != undefined)
   {
      return;
   }
   // The selection highlight goes on top (invisible: it tells which row is selected), and the row's drawing takes
   // its depth, over the stock plate (a plain shape at the row's first depth) and under its art and text.
   item.hilite_mc.swapDepths(1000);
   item.hilite_mc._alpha = 0;
   item.sanctuaryRow = item.createEmptyMovieClip("sanctuaryRow",-16382);
   item.icon._xscale = item.icon._yscale = MCH_ICON_SCALE;
   item.icon._x = MCH_SOCKET_X;
   item.icon._y = MCH_SOCKET_Y;
   var mask = item.createEmptyMovieClip("sanctuaryIconMask",item.getNextHighestDepth());
   mask.beginFill(0,100);
   mchCircle(mask,MCH_SOCKET_X,MCH_SOCKET_Y,MCH_SOCKET_R - 3);
   mask.endFill();
   item.icon.setMask(mask);
   item.label.filters = mchOutline();
   item.label._x = 14;
   item.label._y = 10;
   item.txtDesc.filters = mchShadow();
   item.txtDesc._x = 100;
   item.txtDesc._y = 44;
   item.txtTimeLeftLabel.filters = item.availibleNow_text.filters = item.availibleTomorrow_text.filters = mchShadow();
   // Hover: the row is the stock button (it has onPress), so these sit on the same clip.
   item.onRollOver = function()
   {
      this.sanctuaryOver = true;
      mchDrawRow(this,true);
   };
   item.onRollOut = item.onDragOut = function()
   {
      this.sanctuaryOver = false;
      mchDrawRow(this,true);
   };
}

function mchDrawRow(item, changed)
{
   var g = item.sanctuaryRow;
   var sel = item.hilite_mc._visible == true;
   var over = item.sanctuaryOver == true;
   var now = item.availibleNow_text._visible == true;
   var x = MCH_ROW[0];
   var y = MCH_ROW[1];
   var w = MCH_ROW[2];
   var h = MCH_ROW[3];
   g.clear();
   // The cover, in the panel's colour, over the stock plate.
   g.lineStyle();
   g.beginFill(MCH_PANEL,100);
   mchRoundRect(g,-1,-1,229,160,4);
   g.endFill();
   if(sel)
   {
      mchGradientRect(g,x,y,w,h,16,0x2B4FA8,0x17286A,100,MCH_GOLD,100,2);
      g.lineStyle();
      g.beginGradientFill("linear",[MCH_GOLD,MCH_GOLD],[over ? 55 : 40,0],[0,200],{matrixType:"box",x:x,y:y,w:w,h:h,r:0});
      mchRoundRect(g,x + 1,y + 1,w - 2,h - 2,15);
      g.endFill();
   }
   else if(over)
   {
      mchGradientRect(g,x,y,w,h,16,0x3F66D6,0x1D307A,100,0xFFE08A,100,1.5);
   }
   else
   {
      mchGradientRect(g,x,y,w,h,16,0x22408F,0x152458,100,MCH_RIM,30,1);
   }
   g.lineStyle();
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[over || sel ? 20 : 12,0],[0,255],{matrixType:"box",x:0,y:y + 2,w:10,h:30,r:Math.PI / 2});
   mchRoundRect(g,x + 3,y + 2,w - 6,30,14);
   g.endFill();
   g.beginFill(0x050A24,30);
   mchCircle(g,MCH_SOCKET_X,MCH_SOCKET_Y + 3,MCH_SOCKET_R + 1);
   g.endFill();
   mchSocket(g,MCH_SOCKET_X,MCH_SOCKET_Y,MCH_SOCKET_R,sel ? MCH_GOLD : 0xFFFFFF,sel ? 100 : 85,sel ? 3 : 2);
   // The availability chip along the bottom: gold for now, navy for tomorrow.
   var cy = 124;
   if(now)
   {
      g.lineStyle(1,0xFFF3C4,80);
      g.beginFill(0x0A1440,55);
   }
   else
   {
      g.lineStyle(1,MCH_RIM,40);
      g.beginFill(0x0A1440,55);
   }
   mchRoundRect(g,16,cy,w - 24,22,11);
   g.endFill();

   mchColor(item.label,sel ? 0xFFE08A : 0xFFFFFF);
   mchColor(item.txtDesc,MCH_SOFT);
   mchColor(item.txtTimeLeftLabel,MCH_SOFT);
   mchColor(item.availibleNow_text,0xFFE08A);
   mchColor(item.availibleTomorrow_text,0xFFFFFF);
   g.filters = sel ? [new flash.filters.GlowFilter(MCH_GOLD,0.4,10,10,2,2)] : [];

   if(changed)
   {
      var s = over ? MCH_ICON_HOVER : MCH_ICON_SCALE;
      gs.TweenLite.to(item.icon,0.7,{_xscale:s,_yscale:s,_rotation:over ? -6 : 0,ease:mchElasticOut});
   }
}

function mchSyncRows()
{
   var sc = m_list.m_scrollee;
   var i = 0;
   while(sc["item" + i] != undefined)
   {
      var item = sc["item" + i];
      mchStyleRow(item);
      var key = item.hilite_mc._visible + "|" + item.sanctuaryOver + "|" + item.availibleNow_text._visible + "|" + item.m_data.index;
      if(key != item.sanctuaryKey)
      {
         item.sanctuaryKey = key;
         mchDrawRow(item,false);
      }
      i++;
   }
}

// The list's scrollbar (stock, invisible, attached when there are more than three chests): a slim track with a
// gold thumb.
function mchSyncScrollBar()
{
   var sb = m_list.m_scrollbar;
   var s = mchFrame.scroll;
   if(sb == undefined || sb._parent == undefined || !sb._visible)
   {
      s.clear();
      s.key = undefined;
      return;
   }
   sb._alpha = 0;
   var b = sb.getBounds(_root);
   var t = sb.thumb_mc.getBounds(_root);
   var key = Math.round(b.xMin) + "," + Math.round(b.yMin) + "," + Math.round(b.yMax) + "," + Math.round(t.yMin) + "," + Math.round(t.yMax);
   if(key == s.key)
   {
      return;
   }
   s.key = key;
   var cx = Math.round((b.xMin + b.xMax) / 2);
   s.clear();
   s.lineStyle(1,MCH_RIM,30);
   s.beginFill(0x0A1440,80);
   mchRoundRect(s,cx - 5,b.yMin + 18,10,b.yMax - b.yMin - 36,5);
   s.endFill();
   s.lineStyle(3,MCH_SOFT,80);
   s.moveTo(cx - 6,b.yMin + 12);
   s.lineTo(cx,b.yMin + 6);
   s.lineTo(cx + 6,b.yMin + 12);
   s.moveTo(cx - 6,b.yMax - 12);
   s.lineTo(cx,b.yMax - 6);
   s.lineTo(cx + 6,b.yMax - 12);
   if(t.yMax > t.yMin)
   {
      s.lineStyle(1,0xFFF3C4,80);
      mchGoldFill(s,cx - 4,t.yMin,8,t.yMax - t.yMin,false);
      mchRoundRect(s,cx - 4,t.yMin + 1,8,Math.max(10,t.yMax - t.yMin - 2),4);
      s.endFill();
   }
}

// --- buttons ---------------------------------------------------------------------------

function mchIconOver(btn)
{
   return btn.over_mc._visible == true || btn.down_mc._visible == true;
}

function mchSyncCircle(c, btn)
{
   var over = mchIconOver(btn);
   if(over == c.over)
   {
      return;
   }
   c.over = over;
   c.draw(over);
   if(c.gold)
   {
      gs.TweenLite.to(c,0.6,{_rotation:over ? 90 : 0,_xscale:over ? 112 : 100,_yscale:over ? 112 : 100,ease:mchElasticOut});
   }
   else
   {
      gs.TweenLite.to(c,0.3,{_xscale:over ? 110 : 100,_yscale:over ? 110 : 100,ease:mchStrong()});
   }
}

function mchSyncPlay()
{
   var p = mchFrame.play;
   var enabled = play_mc.isEnabled() != false;
   var over = enabled && mchIconOver(play_mc);
   if(over == p.over && enabled == p.enabled)
   {
      return;
   }
   p.over = over;
   p.enabled = enabled;
   p.draw(over,enabled);
   gs.TweenLite.to(p,0.3,{_xscale:over ? 105 : 100,_yscale:over ? 105 : 100,ease:mchStrong()});
}

function mchSync()
{
   mchSyncRows();
   mchSyncScrollBar();
   mchSyncCircle(mchFrame.close,exit_mc);
   mchSyncCircle(mchFrame.help,help_icon_mc);
   mchSyncPlay();
}

// --- Lua hooks -------------------------------------------------------------------------

var mchSetHeader = setHeader_lua;
setHeader_lua = function(str)
{
   var r = mchSetHeader.apply(_root,arguments);
   mchColor(header_txt,0xFFE08A);
   return r;
};

// Selecting a chest fills the detail card through the stock fitters, which put the fields back in their boxes.
var mchOnItemSelect = onItemSelect;
onItemSelect = function(item)
{
   var r = mchOnItemSelect.apply(_root,arguments);
   mchStyleTexts();
   return r;
};

// Each row's text is set when it's given a chest; recoloured on the next frame's sync.
var mchDrawItem = VertListItem.prototype.drawItem;
VertListItem.prototype.drawItem = function()
{
   var r = mchDrawItem.apply(this,arguments);
   this.sanctuaryKey = undefined;
   return r;
};

mchBuildFrame();
mchFrame.onEnterFrame = mchSync;
// ===== End Sanctuary Mystery Chest =====

// ===== Sanctuary Farm-O-Matic =====
// Appended to farmomatic's main timeline by build_client_mods.py.
// The Farm-O-Matic (wndFarmOMatic: the farm machine that turns two harvest samples into a seed blueprint) in
// the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass dialog with the machine in a
// frosted socket, the title in gold and the instructions in soft blue, and the two sample hoppers as glass
// cards. Each card has a gold number badge, the sample's icon a little bigger in a big frosted socket (a "+"
// while it's empty), its quantity on a gold chip and its name in a strip below; hovering lights the card in
// gold. A second slot the farm hasn't unlocked yet is dimmed with a padlock. GO! is a gold pill that stays
// navy until a sample is in, the close button is the gold circle, and while the sample list is open
// (setEnabled_lua) a navy veil sits over the window. The stock buttons stay as invisible hit areas (their art
// is hidden), so every click, tooltip and Lua event is the stock one.

var FOM_W = 450;
var FOM_H = 420;
var FOM_GOLD = 0xFFC531;
var FOM_RIM = 0x9DB8FF;
var FOM_SOFT = 0xC9D6FF;
var FOM_INK = 0x3A2400;
// The slot cards: left edge (the icon is centered on the card), top, size, and the socket's center and radius.
var FOM_CARD_X = [28,238];
var FOM_CARD_Y = 112;
var FOM_CARD_W = 180;
var FOM_CARD_H = 218;
var FOM_SOCKET_Y = 192;
var FOM_SOCKET_R = 50;
var FOM_STRIP_Y = 266;
// The stock slot icon (64px) is drawn at (-37,-48) in the slot button, 3px higher while hovered.
var FOM_ICON_SCALE = 115;
// GO! as a gold pill centered under the cards.
var FOM_GO_X = 225;
var FOM_GO_Y = 370;
var FOM_GO_W = 180;
var FOM_GO_H = 46;
// The machine (a root shape at 10,10, 64px) sits in a frosted socket in the header.
var FOM_ICON_X = 42;
var FOM_ICON_Y = 42;

var fomFrame;
var fomVeil;
var fomCards = [];
var fomGoOver = false;

// --- drawing helpers -------------------------------------------------------------------

function fomRoundRect(mc, x, y, w, h, r)
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

function fomGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   fomRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

// The gold of the close button, GO! and the chips.
function fomGoldRect(mc, x, y, w, h, r, over)
{
   mc.lineStyle(over ? 2 : 1.5,0xFFF3C4,over ? 100 : 90);
   mc.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   fomRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   // A glassy shine across the top half.
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h / 2,r:Math.PI / 2});
   fomRoundRect(mc,x + 3,y + 2,w - 6,h / 2,Math.min(r,h / 4));
   mc.endFill();
}

function fomCircle(mc, x, y, r)
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

// A round frosted socket (the stock icons are made for light plates), with a soft shadow under it.
function fomSocket(mc, x, y, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle();
   mc.beginFill(0x050A24,35);
   fomCircle(mc,x,y + 3,r + 1);
   mc.endFill();
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.2,y:y - r * 1.35,w:r * 2.4,h:r * 2.4,r:0});
   fomCircle(mc,x,y,r);
   mc.endFill();
   mc.lineStyle(1,0xFFFFFF,70);
   fomCircle(mc,x,y,r + 3);
}

function fomText(mc, name, txt, x, y, w, h, size, color, align)
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

function fomSparkle(mc, x, y, r, alpha)
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
function fomSparkles(mc, seed, count, x, y, w, h)
{
   var s = seed;
   var i;
   for(i = 0; i < count; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + s % w;
      s = (s * 1103515245 + 12345) % 2147483648;
      fomSparkle(mc,sx,y + s % h,1 + s % 3,25 + s % 50);
   }
}

function fomOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// A thin gold rule that fades out at both ends.
function fomGoldRule(mc, x, y, w)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[FOM_GOLD,FOM_GOLD,FOM_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:x,y:y,w:w,h:2,r:0});
   fomRoundRect(mc,x,y,w,2,1);
   mc.endFill();
}

// Restyles a stock text field (keeping its text) and shrinks it until it fits, as frSetTextPreserveWidthV3 does.
function fomFit(t, x, y, w, h, size, color, align)
{
   t._x = x;
   t._y = y;
   t._width = w;
   t._height = h;
   t.autoSize = "none";
   t.embedFonts = true;
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = align;
   t.setTextFormat(f);
   t.setNewTextFormat(f);
   // frSetTextPreserveWidthV3 starts from the size it first saw; this is the new one.
   t.defaultFontSize = size;
   while((t.textWidth > w - 4 || t.textHeight > h - 2) && f.size > 9)
   {
      f.size -= 1;
      t.setTextFormat(f);
   }
   t.textColor = color;
}

// Recolors a stock text field after stock code set its text (it keeps the format it had).
function fomColor(t, color)
{
   var f = new TextFormat();
   f.color = color;
   t.setTextFormat(f);
   t.textColor = color;
}

// --- frame -----------------------------------------------------------------------------

function fomDrawFrame()
{
   var bg = fomFrame.bg;
   bg.clear();
   fomGradientRect(bg,6,6,FOM_W - 12,FOM_H - 12,30,0x2E4FB0,0x141F55,100,FOM_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-120,y:-220,w:520,h:460,r:0});
   fomRoundRect(bg,8,8,FOM_W - 16,FOM_H - 16,28);
   bg.endFill();

   // The header band, with a few sparkles and a gold rule under it.
   bg.beginFill(0x0A1440,30);
   fomRoundRect(bg,12,12,FOM_W - 24,90,26);
   bg.endFill();
   fomSparkles(bg,4242,18,96,16,260,30);
   fomGoldRule(bg,135,100,180);

   // The machine's socket, in gold.
   fomSocket(bg,FOM_ICON_X,FOM_ICON_Y,30,FOM_GOLD,100,2.5);
}

// The gold round close button, centered under the stock one (an invisible hit area).
function fomDrawClose(over)
{
   var c = fomFrame.close;
   c.clear();
   var r = 17;
   c.lineStyle(over ? 2 : 1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   fomCircle(c,0,0,r);
   c.endFill();
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r,r:Math.PI / 2});
   fomRoundRect(c,-11,-15,22,12,6);
   c.endFill();
   c.lineStyle(4,FOM_INK,100);
   c.moveTo(-6,-6);
   c.lineTo(6,6);
   c.moveTo(6,-6);
   c.lineTo(-6,6);
   c.filters = over ? [new flash.filters.GlowFilter(FOM_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
}

// GO! is navy with a gold rim until a sample is in (goButton_mc.enable), then a gold pill.
function fomDrawGo()
{
   var g = fomFrame.go;
   var on = goButton_mc.isEnabled();
   var over = on && fomGoOver;
   var x = - FOM_GO_W / 2;
   var y = - FOM_GO_H / 2;
   g.bg.clear();
   if(on)
   {
      fomGoldRect(g.bg,x,y,FOM_GO_W,FOM_GO_H,FOM_GO_H / 2,over);
   }
   else
   {
      fomGradientRect(g.bg,x,y,FOM_GO_W,FOM_GO_H,FOM_GO_H / 2,0x2B4FA8,0x17286A,100,FOM_GOLD,70,2);
   }
   g.label_txt.textColor = on ? FOM_INK : 0x8FA3D8;
   if(over)
   {
      g.filters = [new flash.filters.GlowFilter(FOM_GOLD,0.8,16,16,2,2)];
   }
   else if(on)
   {
      g.filters = [new flash.filters.GlowFilter(FOM_GOLD,0.35,10,10,2,2),new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,10,10,1,2)];
   }
   else
   {
      g.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,10,10,1,2)];
   }
}

// --- slot cards ------------------------------------------------------------------------

function fomSlotData(i)
{
   return i == 0 ? m_slot1Data : m_slot2Data;
}

function fomDrawCard(i)
{
   var c = fomCards[i];
   var slot = c.slot;
   var x = FOM_CARD_X[i];
   var y = FOM_CARD_Y;
   var w = FOM_CARD_W;
   var h = FOM_CARD_H;
   var cx = x + w / 2;
   var locked = slot.enabled == false;
   var over = c.over && !locked;
   var data = fomSlotData(i);
   var full = data.itemId != undefined;
   var bg = c.bg;
   bg.clear();
   fomGradientRect(bg,x,y,w,h,22,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : (full ? FOM_GOLD : FOM_RIM),over || full ? 100 : 45,over || full ? 2.5 : 2);
   // Glassy sheen across the top.
   bg.lineStyle();
   bg.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[over ? 22 : 14,0],[0,255],{matrixType:"box",x:0,y:y + 3,w:10,h:70,r:Math.PI / 2});
   fomRoundRect(bg,x + 3,y + 3,w - 6,70,19);
   bg.endFill();

   // The name strip along the bottom.
   bg.beginFill(0x0A1440,over ? 30 : 45);
   bg.moveTo(x + 2,FOM_STRIP_Y);
   bg.lineTo(x + w - 2,FOM_STRIP_Y);
   bg.lineTo(x + w - 2,y + h - 20);
   bg.curveTo(x + w - 2,y + h - 2,x + w - 20,y + h - 2);
   bg.lineTo(x + 20,y + h - 2);
   bg.curveTo(x + 2,y + h - 2,x + 2,y + h - 20);
   bg.lineTo(x + 2,FOM_STRIP_Y);
   bg.endFill();
   bg.lineStyle(1,over ? 0xFFE08A : FOM_RIM,over ? 70 : 25);
   bg.moveTo(x + 14,FOM_STRIP_Y);
   bg.lineTo(x + w - 14,FOM_STRIP_Y);

   // The socket: gold-ringed once it holds a sample or while hovered.
   fomSocket(bg,cx,FOM_SOCKET_Y,FOM_SOCKET_R,over || full ? 0xFFE08A : 0xFFFFFF,over || full ? 100 : 85,over || full ? 3 : 2);

   // An empty slot shows a "+", a locked one a padlock (drawn about the socket's center, where the clip sits).
   var g = c.glyph;
   g.clear();
   if(locked)
   {
      g.lineStyle(5,0x6F86C4,100);
      g.moveTo(-11,-2);
      g.lineTo(-11,-12);
      g.curveTo(-11,-25,0,-25);
      g.curveTo(11,-25,11,-12);
      g.lineTo(11,-2);
      g.lineStyle(1.5,0x3D5291,100);
      g.beginGradientFill("linear",[0x8FA3D8,0x5A70B0],[100,100],[0,255],{matrixType:"box",x:-18,y:-4,w:36,h:30,r:Math.PI / 2});
      fomRoundRect(g,-18,-4,36,30,6);
      g.endFill();
      g.lineStyle();
      g.beginFill(0x24356E,100);
      fomCircle(g,0,8,4);
      g.endFill();
   }
   else if(!full)
   {
      g.lineStyle(7,over ? 0xF5A623 : 0x7F98D6,over ? 100 : 80);
      g.moveTo(0,-18);
      g.lineTo(0,18);
      g.moveTo(-18,0);
      g.lineTo(18,0);
   }

   // The quantity's gold chip, bottom right on the socket.
   var q = c.chip;
   q.clear();
   var qty = c.qty.text;
   if(full && qty != undefined && qty != "" && qty != " ")
   {
      fomGoldRect(q,cx + 14,FOM_SOCKET_Y + 28,54,26,13,false);
   }

   c.hint_txt.text = locked ? "Locked" : (full ? "" : "Choose a sample");
   c.hint_txt.textColor = locked ? 0x8FA3D8 : FOM_SOFT;
   c.name._visible = full;
   fomColor(c.name,over ? 0xFFE08A : 0xFFFFFF);
   fomColor(c.qty,FOM_INK);

   c.badge_txt.textColor = FOM_INK;
   bg.filters = over ? [new flash.filters.GlowFilter(FOM_GOLD,0.6,16,16,2,2)] : [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,8,8,1,2)];
   c._alpha = locked ? 60 : 100;
}

function fomBuildCard(i)
{
   var slot = i == 0 ? slot1_mc : slot2_mc;
   var x = FOM_CARD_X[i];
   var cx = x + FOM_CARD_W / 2;
   var c = fomFrame.createEmptyMovieClip("card" + i,10 + i);
   c.slot = slot;
   c.bg = c.createEmptyMovieClip("bg",1);
   c.glyph = c.createEmptyMovieClip("glyph",2);
   c.glyph._x = cx;
   c.glyph._y = FOM_SOCKET_Y;
   c.chip = c.createEmptyMovieClip("chip",3);

   // The slot number on a gold disc, top left.
   var b = c.createEmptyMovieClip("badge",4);
   b.lineStyle(1.5,0xFFF3C4,100);
   b.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x + 7,y:FOM_CARD_Y + 7,w:28,h:28,r:Math.PI / 2});
   fomCircle(b,x + 21,FOM_CARD_Y + 21,14);
   b.endFill();
   b.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];
   c.badge_txt = fomText(c,"badge_txt",String(i + 1),x + 7,FOM_CARD_Y + 9,28,24,17,FOM_INK);

   c.hint_txt = fomText(c,"hint_txt","",x + 8,FOM_STRIP_Y + 14,FOM_CARD_W - 16,24,15,FOM_SOFT);
   c.hint_txt.filters = fomOutline();

   // The stock hopper art goes (the states stay as the hit area); the icon grows into the socket.
   slot.up_mc._alpha = slot.over_mc._alpha = slot.down_mc._alpha = 0;
   var d = 64 * (FOM_ICON_SCALE - 100) / 200;
   slot.m_icon_up._xscale = slot.m_icon_up._yscale = FOM_ICON_SCALE;
   slot.m_icon_over._xscale = slot.m_icon_over._yscale = FOM_ICON_SCALE;
   slot.m_icon_up._x = slot.m_icon_over._x = -37 - d;
   slot.m_icon_up._y = -48 - d;
   slot.m_icon_over._y = -51 - d;

   // The sample's name in the strip and its quantity on the chip (stock fields Lua's data lands in).
   c.name = i == 0 ? slot1_txt : slot2_txt;
   c.qty = i == 0 ? slot1Quantity_txt : slot2Quantity_txt;
   fomFit(c.name,x + 8,FOM_STRIP_Y + 13,FOM_CARD_W - 16,28,17,0xFFFFFF,"center");
   c.name.filters = fomOutline();
   fomFit(c.qty,cx + 14,FOM_SOCKET_Y + 29,54,24,15,FOM_INK,"center");

   slot.addListener(fomSlotListener);
   fomCards[i] = c;
   fomDrawCard(i);
}

// The slots and GO! broadcast their hover (FRIconButton); the stock listener (_root) still shows the tooltip.
var fomSlotListener = new Object();
fomSlotListener.onHoverOver = function(btn)
{
   var i = btn == slot1_mc ? 0 : 1;
   fomCards[i].over = true;
   fomDrawCard(i);
   // The empty slot's "+" pops.
   if(btn.enabled != false)
   {
      fomCards[i].glyph._xscale = fomCards[i].glyph._yscale = 100;
      gs.TweenLite.from(fomCards[i].glyph,0.6,{_xscale:80,_yscale:80,ease:mx.transitions.easing.Elastic.easeOut});
   }
};
fomSlotListener.onHoverOut = function(btn)
{
   var i = btn == slot1_mc ? 0 : 1;
   fomCards[i].over = false;
   fomDrawCard(i);
};

var fomButtonListener = new Object();
fomButtonListener.onHoverOver = function(btn)
{
   if(btn == exitButton_mc)
   {
      fomDrawClose(true);
      gs.TweenLite.to(fomFrame.close,0.5,{_xscale:114,_yscale:114,ease:mx.transitions.easing.Elastic.easeOut});
   }
   else if(btn == goButton_mc)
   {
      fomGoOver = true;
      fomDrawGo();
      if(goButton_mc.isEnabled())
      {
         gs.TweenLite.to(fomFrame.go,0.5,{_xscale:106,_yscale:106,ease:mx.transitions.easing.Elastic.easeOut});
      }
   }
};
fomButtonListener.onHoverOut = function(btn)
{
   if(btn == exitButton_mc)
   {
      fomDrawClose(false);
      gs.TweenLite.to(fomFrame.close,0.3,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
   }
   else if(btn == goButton_mc)
   {
      fomGoOver = false;
      fomDrawGo();
      gs.TweenLite.to(fomFrame.go,0.3,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
   }
};

// --- build -----------------------------------------------------------------------------

function fomBuild()
{
   // The stock panel stays (invisible) so the window keeps its hit area; the blocker still blocks while
   // disabled, under the veil instead of the stock art.
   bg_mc._alpha = 0;
   blocker_mc._alpha = 0;

   fomFrame = _root.createEmptyMovieClip("sanctuaryFarmOMatic",-16384);
   fomFrame.bg = fomFrame.createEmptyMovieClip("bg",1);
   fomFrame.bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   fomDrawFrame();

   // Title and instructions: the stock fields (set from the string table), restyled.
   fomFit(title_txt,80,15,290,38,26,0xFFE08A,"center");
   title_txt.filters = fomOutline();
   fomFit(body_txt,62,54,326,44,14,FOM_SOFT,"center");
   body_txt.filters = fomOutline();

   fomBuildCard(0);
   fomBuildCard(1);

   // GO!: the stock button is stretched over the pill as its hit area, its art hidden. enable() resets the
   // button's own alpha, so the states are hidden instead.
   var g = fomFrame.createEmptyMovieClip("go",20);
   g._x = FOM_GO_X;
   g._y = FOM_GO_Y;
   g.bg = g.createEmptyMovieClip("bg",1);
   g.label_txt = fomText(g,"label_txt","GO!",- FOM_GO_W / 2,-15,FOM_GO_W,30,22,FOM_INK);
   goButton_mc.up_mc._alpha = goButton_mc.over_mc._alpha = goButton_mc.down_mc._alpha = 0;
   goButton_mc._xscale = FOM_GO_W / 77 * 100;
   goButton_mc._yscale = FOM_GO_H / 72 * 100;
   goButton_mc._x = FOM_GO_X - FOM_GO_W / 2;
   goButton_mc._y = FOM_GO_Y - FOM_GO_H / 2;
   var enable = goButton_mc.enable;
   goButton_mc.enable = function(is)
   {
      enable.apply(this,arguments);
      fomDrawGo();
   };
   goButton_mc.addListener(fomButtonListener);
   fomDrawGo();

   // The close button: the gold circle under the stock one (an invisible hit area).
   fomFrame.close = fomFrame.createEmptyMovieClip("close",21);
   exitButton_mc.up_mc._alpha = exitButton_mc.over_mc._alpha = exitButton_mc.down_mc._alpha = 0;
   fomFrame.close._x = exitButton_mc._x + exitButton_mc._width / 2;
   fomFrame.close._y = exitButton_mc._y + exitButton_mc._height / 2;
   exitButton_mc.addListener(fomButtonListener);
   fomDrawClose(false);

   // The veil over everything while the sample list is open (no mouse handlers: the blocker takes the clicks).
   fomVeil = _root.createEmptyMovieClip("sanctuaryFarmOMaticVeil",_root.getNextHighestDepth());
   fomVeil.beginFill(0x0A1440,45);
   fomRoundRect(fomVeil,6,6,FOM_W - 12,FOM_H - 12,30);
   fomVeil.endFill();
   fomVeil._visible = blocker_mc._visible;
}

// --- Lua -------------------------------------------------------------------------------

// A sample picked from the list lands in the slot that was clicked.
var fomSetItemToSlot = setItemToSlot_lua;
setItemToSlot_lua = function(guid, itemId, name, iconId, tint, quantity)
{
   var result = fomSetItemToSlot.apply(_root,arguments);
   var i = m_currentSlot == slot2_mc ? 1 : 0;
   if(m_currentSlot != undefined)
   {
      gs.TweenLite.from(m_currentSlot.m_icon_up,0.6,{_alpha:0,ease:mx.transitions.easing.Strong.easeOut});
      fomDrawCard(i);
   }
   return result;
};

// The second slot unlocks with the farm's level.
var fomEnableSecondSlot = enableSecondSlot_lua;
enableSecondSlot_lua = function(is)
{
   var result = fomEnableSecondSlot.apply(_root,arguments);
   // The card shows the dimming; the slot's own art is hidden, so its alpha only dims the icon.
   slot2_mc._alpha = 100;
   fomDrawCard(1);
   return result;
};

var fomSetEnabled = setEnabled_lua;
setEnabled_lua = function(is)
{
   var result = fomSetEnabled.apply(_root,arguments);
   fomVeil._visible = blocker_mc._visible;
   return result;
};

fomBuild();
// ===== End Sanctuary Farm-O-Matic =====

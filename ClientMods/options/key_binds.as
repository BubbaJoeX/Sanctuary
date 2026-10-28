// ===== Sanctuary Key Bindings =====
// Appended to keybinds' main timeline by build_client_mods.py.
// The Key Bindings window (wndKeyBinds, opened from the Game Options window's Key Binding row) in the Game Options
// window's style (ClientMods/options/game_options.as): a navy glass frame with a gold title and close button, a
// recessed list of glass rows whose keys sit in recessed key caps (gold while the window listens for a new key, red
// when two actions share one), gold section headings, gold Back and Use Defaults pills, the row's undo button as a
// small gold disc, and a slim scrollbar with a gold thumb.
//
// Lua (KeyBinds.lua) fills the list through addMenuOption -> renderSection / renderItem, and the rows talk back to
// it as stock (startKeyListening, setKey, setToDefault, saveKeytoSetLater). The stock Back, Use Defaults and close
// buttons (FRIconButtons) and the scrollbar (FRScrollBarV2.swf) stay where the mouse goes, invisible, over this
// layer. The row plates are plain shapes, so each row's plate is drawn just above it (the red key field is moved one
// depth up to make room). The stock title is unnamed text at root depth 33, dropped at build time (REMOVED_DEPTHS).

var KB_W = 376;
var KB_H = 576;
var KB_HEADER_H = 52;
// The list: its panel, the visible part (stock: y 70, 335 high) and the stock scrollbar's x.
var KB_PANEL = [26,88,324,388];
var KB_VIEW_Y = 100;
var KB_VIEW_H = 362;
// A row's stock plate (270x52) and its key field (81x38 art, drawn 1.481 wide).
var KB_ROW_W = 270;
var KB_ROW_H = 52;
var KB_FIELD_W = 81;
var KB_FIELD_H = 38;
var KB_FIELD_SCALE = 1.481;
var KB_CLOSE_X = 340;
var KB_CLOSE_Y = 38;
var KB_PILL_Y = 512;
var KB_BACK = [30,104];
var KB_DEFAULT = [146,200];
var KB_GOLD = 0xFFC531;
var KB_RIM = 0x9DB8FF;
var KB_SOFT = 0xC9D6FF;
var KB_INK = 0x3A2400;

var kbRoot;

// --- drawing helpers -------------------------------------------------------------------

function kbRoundRect(mc, x, y, w, h, r)
{
   kbRoundRect2(mc,x,y,w,h,r,r);
}

// A rounded rectangle with separate corner radii across and down (for art drawn inside a stretched clip).
function kbRoundRect2(mc, x, y, w, h, rx, ry)
{
   mc.moveTo(x + rx,y);
   mc.lineTo(x + w - rx,y);
   mc.curveTo(x + w,y,x + w,y + ry);
   mc.lineTo(x + w,y + h - ry);
   mc.curveTo(x + w,y + h,x + w - rx,y + h);
   mc.lineTo(x + rx,y + h);
   mc.curveTo(x,y + h,x,y + h - ry);
   mc.lineTo(x,y + ry);
   mc.curveTo(x,y,x + rx,y);
}

function kbGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   kbRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function kbCircle(mc, x, y, r)
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

function kbGoldFill(mc, x, y, w, h, over)
{
   mc.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
}

// A glassy highlight across the top of a plate.
function kbGlint(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:0,y:y,w:10,h:h,r:Math.PI / 2});
   kbRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function kbSparkle(mc, x, y, r, alpha)
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

function kbOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function kbShadow()
{
   return [new flash.filters.DropShadowFilter(1,90,0x0A1030,0.9,2,2,1,1)];
}

function kbText(mc, name, txt, x, y, w, h, size, color, align)
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

function kbRule(mc, x, w, y)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[KB_GOLD,KB_GOLD,KB_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:x,y:0,w:w,h:2,r:0});
   kbRoundRect(mc,x,y,w,2,1);
   mc.endFill();
}

// Penner's elastic ease out; this movie only carries mx.transitions.easing.Strong.
function kbElasticOut(t, b, c, d)
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

function kbStrong()
{
   return mx.transitions.easing.Strong.easeOut;
}

// An FRIconButton is under the mouse (its over or pressed art is the one showing).
function kbIsOver(btn)
{
   return btn.over_mc._visible == true || btn.down_mc._visible == true;
}

// --- frame -----------------------------------------------------------------------------

function kbDrawFrame(bg)
{
   kbGradientRect(bg,4,4,KB_W - 8,KB_H - 8,30,0x2E4FB0,0x141F55,100,KB_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-150,y:-260,w:520,h:560,r:0});
   kbRoundRect(bg,6,6,KB_W - 12,KB_H - 12,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   kbRoundRect(bg,12,12,KB_W - 24,KB_HEADER_H,24);
   bg.endFill();

   // Sparkles in the header (fixed seed, same every time).
   var s = 7373;
   var i;
   for(i = 0; i < 16; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 30 + s % (KB_W - 60);
      s = (s * 1103515245 + 12345) % 2147483648;
      kbSparkle(bg,x,20 + s % 38,1 + s % 3,25 + s % 50);
   }

   kbGradientRect(bg,KB_PANEL[0],KB_PANEL[1],KB_PANEL[2],KB_PANEL[3],22,0x0A1440,0x0A1440,38,KB_RIM,22,1);
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
}

function kbBuildTitle()
{
   var title = kbText(kbRoot,"title_txt","Key Bindings",0,20,KB_W,40,26,0xFFE08A);
   title.filters = kbOutline();
   var hint = kbText(kbRoot,"hint_txt","Click a key, then press the new one",0,66,KB_W,20,12,KB_SOFT);
   hint.filters = kbShadow();
}

// The gold close circle and the two gold pills, under the stock buttons (invisible hit areas, moved and sized onto them).
function kbBuildButtons()
{
   var close = kbRoot.close = kbRoot.createEmptyMovieClip("close",kbRoot.getNextHighestDepth());
   close._x = KB_CLOSE_X;
   close._y = KB_CLOSE_Y;
   close.draw = function(over)
   {
      this.clear();
      this.lineStyle(2,0xFFF3C4,100);
      kbGoldFill(this,-17,-17,34,34,over);
      kbCircle(this,0,0,17);
      this.endFill();
      this.lineStyle(4,KB_INK,100);
      this.moveTo(-6,-6);
      this.lineTo(6,6);
      this.moveTo(6,-6);
      this.lineTo(-6,6);
      this.filters = over ? [new flash.filters.GlowFilter(KB_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,6,6,1,2)];
   };
   close.draw(false);
   kbPlaceHit(close_btn,KB_CLOSE_X - 18,KB_CLOSE_Y - 18,36,36);

   kbRoot.back = kbMakePill("back","Back",KB_BACK[0],KB_BACK[1]);
   kbPlaceHit(back_btn,KB_BACK[0],KB_PILL_Y - 20,KB_BACK[1],40);
   kbRoot.defaults = kbMakePill("defaults","Use Defaults",KB_DEFAULT[0],KB_DEFAULT[1]);
   kbPlaceHit(default_btn,KB_DEFAULT[0],KB_PILL_Y - 20,KB_DEFAULT[1],40);
}

function kbMakePill(name, label, x, w)
{
   var pill = kbRoot.createEmptyMovieClip(name,kbRoot.getNextHighestDepth());
   pill._x = x + w / 2;
   pill._y = KB_PILL_Y;
   pill.w = w;
   pill.bg = pill.createEmptyMovieClip("bg",1);
   pill.draw = function(over)
   {
      var g = this.bg;
      var h = this.w / 2;
      g.clear();
      g.lineStyle(2,0xFFF3C4,100);
      kbGoldFill(g,- h,-20,this.w,40,over);
      kbRoundRect(g,- h,-20,this.w,40,20);
      g.endFill();
      g.lineStyle();
      g.beginFill(0xFFFFFF,over ? 40 : 30);
      kbRoundRect(g,- h + 10,-16,this.w - 20,11,5.5);
      g.endFill();
      g.filters = over ? [new flash.filters.GlowFilter(KB_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,8,8,1,2)];
   };
   pill.draw(false);
   kbText(pill,"label_txt",label,- w / 2,-12,w,26,17,KB_INK);
   return pill;
}

// A stock button becomes an invisible hit area of the given box (FRIconButton art is registered top left).
function kbPlaceHit(btn, x, y, w, h)
{
   btn._alpha = 0;
   btn._xscale = btn._yscale = 100;
   var nw = btn._width;
   var nh = btn._height;
   btn._xscale = w / nw * 100;
   btn._yscale = h / nh * 100;
   btn._x = x;
   btn._y = y;
}

function kbSyncButton(mine, stock)
{
   var over = kbIsOver(stock);
   // FRIconButton.enable sets its alpha; it stays a hit area only.
   stock._alpha = 0;
   if(over == mine.over)
   {
      return;
   }
   mine.over = over;
   mine.draw(over);
   if(mine == kbRoot.close)
   {
      gs.TweenLite.to(mine,0.6,{_rotation:over ? 90 : 0,_xscale:over ? 112 : 100,_yscale:over ? 112 : 100,ease:kbElasticOut});
   }
   else
   {
      gs.TweenLite.to(mine,0.3,{_xscale:over ? 106 : 100,_yscale:over ? 106 : 100,ease:kbStrong()});
   }
}

// --- rows ------------------------------------------------------------------------------

// A row's glass plate, just above its stock plate (a plain shape at the row's first depth), and its key caps.
function kbStyleRow(item)
{
   if(item.kbPlate != undefined)
   {
      return;
   }
   // The red field sits at the depth right above the stock plate; one up, so the plate can take its place.
   item.redField_mc.swapDepths(-16381);
   item.kbPlate = item.createEmptyMovieClip("kbPlate",-16382);
   kbDrawRow(item,false);
   kbDrawCap(item.blueField_mc,false,false);
   kbDrawCap(item.redField_mc,true,false);
   item.name_txt.textColor = 0xFFFFFF;
   item.name_txt.filters = kbShadow();
   item.input_txt.textColor = 0xFFFFFF;
   item.input_txt.filters = kbShadow();
   kbStyleUndo(item.button_mc);
}

function kbDrawRow(item, listening)
{
   var p = item.kbPlate;
   p.clear();
   if(listening)
   {
      kbGradientRect(p,0,0,KB_ROW_W,KB_ROW_H,12,0x3F66D6,0x1D307A,100,0xFFE08A,100,1.5);
   }
   else
   {
      kbGradientRect(p,0,0,KB_ROW_W,KB_ROW_H,12,0x22408F,0x152458,100,KB_RIM,30,1);
   }
   kbGlint(p,3,2,KB_ROW_W - 6,14,8,listening ? 20 : 12);
   p.filters = listening ? [new flash.filters.GlowFilter(KB_GOLD,0.35,10,10,2,2)] : [];
}

// The key cap, drawn inside the stock field (stretched 1.481 across) over its art: recessed navy, red for a clash,
// gold-rimmed while it waits for a key.
function kbDrawCap(field, red, listening)
{
   var c = field.kbCap;
   if(c == undefined)
   {
      c = field.kbCap = field.createEmptyMovieClip("kbCap",1);
   }
   var w = KB_FIELD_W;
   var h = KB_FIELD_H;
   var rx = 10 / KB_FIELD_SCALE;
   c.clear();
   if(red)
   {
      c.lineStyle(1.5,0xFF8A7A,100);
      c.beginGradientFill("linear",[0x8A1E2A,0x4A0C18],[100,100],[0,255],{matrixType:"box",x:0,y:0,w:w,h:h,r:Math.PI / 2});
   }
   else
   {
      c.lineStyle(listening ? 2 : 1,listening ? KB_GOLD : KB_RIM,listening ? 100 : 45);
      c.beginGradientFill("linear",[0x081030,0x14246A],[100,100],[0,255],{matrixType:"box",x:0,y:0,w:w,h:h,r:Math.PI / 2});
   }
   kbRoundRect2(c,0,0,w,h,rx,10);
   c.endFill();
   // A soft light along the bottom edge, as on a key.
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[0,red ? 14 : 10],[0,255],{matrixType:"box",x:0,y:h - 12,w:10,h:12,r:Math.PI / 2});
   kbRoundRect2(c,2,h - 12,w - 4,10,rx * 0.8,5);
   c.endFill();
   field.filters = listening && !red ? [new flash.filters.GlowFilter(KB_GOLD,0.7,10,10,2,2)] : [];
}

// The row's undo / conflict button: a small gold disc (red while it shows the conflict help), under its icon.
function kbStyleUndo(btn)
{
   if(btn.kbDisc != undefined)
   {
      return;
   }
   btn.up_mc._alpha = btn.over_mc._alpha = btn.down_mc._alpha = 0;
   btn.kbDisc = btn.createEmptyMovieClip("kbDisc",-16384);
   kbDrawUndo(btn,false);
}

function kbDrawUndo(btn, over)
{
   var d = btn.kbDisc;
   // The button's art is 77x72 at 41.7%; the disc is about 30px across on screen.
   var cx = 38.5;
   var cy = 36;
   var r = 36;
   var conflict = btn._parent.getIsConflicted() == true;
   d.clear();
   d.lineStyle(4,conflict ? 0xFFB3A8 : 0xFFF3C4,100);
   if(conflict)
   {
      d.beginGradientFill("linear",over ? [0xF07A6A,0xB0302A] : [0xE0604A,0x9A2420],[100,100],[0,255],{matrixType:"box",x:cx - r,y:cy - r,w:r * 2,h:r * 2,r:Math.PI / 2});
   }
   else
   {
      kbGoldFill(d,cx - r,cy - r,r * 2,r * 2,over);
   }
   kbCircle(d,cx,cy,r);
   d.endFill();
   d.filters = over ? [new flash.filters.GlowFilter(conflict ? 0xFF7A6A : KB_GOLD,0.8,10,10,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.55,4,4,1,2)];
   // The stock arrows are dark green; ink on gold. The conflict "?" keeps its own colours.
   var ink = new Object();
   if(!conflict)
   {
      ink = {ra:0,rb:0x3A,ga:0,gb:0x24,ba:0,bb:0,aa:100,ab:0};
   }
   else
   {
      ink = {ra:100,rb:0,ga:100,gb:0,ba:100,bb:0,aa:100,ab:0};
   }
   new Color(btn.m_icon_up).setTransform(ink);
   new Color(btn.m_icon_over).setTransform(ink);
}

function kbPatchButtons()
{
   var proto = FRIconButton.prototype;
   var states = ["setStateUp","setStateOver","setStateDown","setIcon"];
   var i;
   for(i = 0; i < states.length; i++)
   {
      kbWrapState(proto,states[i]);
   }
}

function kbWrapState(proto, name)
{
   var stock = proto[name];
   proto[name] = function()
   {
      var r = stock.apply(this,arguments);
      if(this.kbDisc != undefined)
      {
         kbDrawUndo(this,this.over_mc._visible == true || this.down_mc._visible == true);
      }
      return r;
   };
}

// The conflict state swaps the field and the button's icon; the cap and disc follow it.
function kbPatchRows()
{
   var proto = KeyBindingItem.prototype;
   var setShowConflicted = proto.setShowConflicted;
   proto.setShowConflicted = function(show)
   {
      var r = setShowConflicted.apply(this,arguments);
      if(this.button_mc.kbDisc != undefined)
      {
         kbDrawUndo(this.button_mc,false);
      }
      return r;
   };
   var setKey = proto.setKey;
   proto.setKey = function(nameStr, value)
   {
      var r = setKey.apply(this,arguments);
      if(this.button_mc.kbDisc != undefined)
      {
         kbDrawUndo(this.button_mc,false);
      }
      return r;
   };
}

function kbStyleHeading(heading)
{
   var t = heading.title;
   t.textColor = 0xFFE08A;
   t.filters = kbOutline();
   var r = heading.createEmptyMovieClip("kbRule",-16384);
   kbRule(r,4,KB_ROW_W - 8,heading._height - 3);
}

// --- the list's view and scrollbar -----------------------------------------------------

function kbBuildList()
{
   // Moved down for the hint line, and a little taller; onScroll places the list from the scrollbar's y.
   var dy = KB_VIEW_Y - LIST_Y;
   m_scrollbar._y = KB_VIEW_Y;
   m_list._y += dy;
   m_mask._y = KB_VIEW_Y - LIST_SPACER;
   m_mask._height = KB_VIEW_H + LIST_SPACER + LIST_SPACER;
   m_scrollbar.setHeight(KB_VIEW_H);
   kbRoot.scroll = kbRoot.createEmptyMovieClip("scroll",kbRoot.getNextHighestDepth());
}

// The stock scrollbar stays the hit area (invisible); a slim track and gold thumb follow it.
function kbSyncScroll()
{
   var sb = m_scrollbar;
   var s = kbRoot.scroll;
   sb.background_mc._alpha = sb.channel_mc._alpha = sb.thumb_mc._alpha = sb.upArrow_btn._alpha = sb.downArrow_btn._alpha = 0;
   var overflow = m_list._height > KB_VIEW_H + 2;
   var b = sb.getBounds(kbRoot);
   var t = sb.thumb_mc.getBounds(kbRoot);
   var key = overflow + "," + Math.round(b.xMin) + "," + Math.round(t.yMin) + "," + Math.round(t.yMax);
   if(key == s.key)
   {
      return;
   }
   s.key = key;
   s.clear();
   if(!overflow || sb == undefined)
   {
      return;
   }
   var cx = Math.round((b.xMin + b.xMax) / 2);
   s.lineStyle();
   s.beginFill(0x0A1440,60);
   kbRoundRect(s,cx - 3,KB_VIEW_Y,6,KB_VIEW_H,3);
   s.endFill();
   if(t.yMax > t.yMin)
   {
      s.lineStyle(1,0xFFF3C4,90);
      kbGoldFill(s,cx - 3,t.yMin,6,t.yMax - t.yMin,false);
      kbRoundRect(s,cx - 3,t.yMin,6,Math.max(10,t.yMax - t.yMin),3);
      s.endFill();
   }
}

// --- every frame -----------------------------------------------------------------------

function kbSync()
{
   kbSyncButton(kbRoot.close,close_btn);
   kbSyncButton(kbRoot.back,back_btn);
   kbSyncButton(kbRoot.defaults,default_btn);
   kbSyncScroll();
   // The row waiting for a key is lit gold.
   if(m_currentItem != kbRoot.listening)
   {
      var old = kbRoot.listening;
      if(old.kbPlate != undefined)
      {
         kbDrawRow(old,false);
         kbDrawCap(old.blueField_mc,false,false);
      }
      kbRoot.listening = m_currentItem;
      if(m_currentItem.kbPlate != undefined)
      {
         kbDrawRow(m_currentItem,true);
         kbDrawCap(m_currentItem.blueField_mc,false,true);
      }
   }
}

// --- Lua -------------------------------------------------------------------------------

// Lua's addMenuOption calls these by name for each heading and key row.
var kbStockRenderItem = renderItem;
renderItem = function()
{
   var r = kbStockRenderItem.apply(_root,arguments);
   kbStyleRow(m_list["item" + m_itemCount]);
   return r;
};
var kbStockRenderSection = renderSection;
renderSection = function()
{
   var r = kbStockRenderSection.apply(_root,arguments);
   kbStyleHeading(m_list["item" + m_itemCount]);
   return r;
};

function kbBuild()
{
   bg_mc._alpha = 0;
   // Has no mouse handlers, under everything stock.
   kbRoot = _root.createEmptyMovieClip("kbShell",-16384);
   kbDrawFrame(kbRoot.createEmptyMovieClip("bg",1));
   kbBuildTitle();
   kbBuildButtons();
   kbBuildList();
   // Patched once (Lua's Reset may rerun this frame); the mark is on the class itself.
   if(KeyBindingItem.prototype.kbPatched != true)
   {
      KeyBindingItem.prototype.kbPatched = true;
      kbPatchButtons();
      kbPatchRows();
   }
   kbRoot.onEnterFrame = kbSync;
}

kbBuild();
// ===== End Sanctuary Key Bindings =====

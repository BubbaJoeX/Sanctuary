// ===== Sanctuary furniture tray style =====
// Appended to housingEditPanel's main timeline by build_client_mods.py, after housing/tray.as (which widens the tray
// to 10 items a page and adds the letter search).
// Redraws the decorate-mode tray in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): one navy glass
// tray with three recesses. On the left the item count sits on a glass card beside the chest in a frosted socket,
// with a gold bar for how full the house is. In the middle each item is a glass card with its icon in a frosted
// socket (gold rim and glow on hover) and its quantity on a gold tag, between gold page arrows. On the right Housing
// Options is a card with the house in a frosted socket that lights up gold on hover. The category buttons above are
// frosted discs in navy rims, the selected one ringed in gold, and the search is a glass tab of navy letter chips.
// The stock clips keep their roles (hit areas, Lua callbacks, tooltips): their art is hidden (alpha 0) and the new
// look is drawn under them or inside them; the stock items panel shape is dropped at build time (REMOVED_DEPTHS).

var HS_W = 800 + SANCTUARY_TRAY_EXTRA;
// The tray's band (category buttons ride its top edge) and the three recesses below it.
var HS_TOP = 62;
var HS_BOTTOM = 196;
var HS_PANEL_Y = 92;
var HS_PANEL_H = 96;
var HS_LEFT = [10,126];
var HS_MID = [142,HS_W - 284];
var HS_RIGHT = [HS_W - 136,126];
// The page arrows move in from the recess edges, clear of the first and last cards.
var HS_ARROW_SHIFT = 7;
var HS_GOLD = 0xFFC531;
var HS_RIM = 0x9DB8FF;
var HS_SOFT = 0xC9D6FF;
var HS_INK = 0x3A2400;

// --- drawing helpers -------------------------------------------------------------------

function hsRoundRect(mc, x, y, w, h, r)
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

function hsGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   hsRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function hsCircle(mc, x, y, r)
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

// The frosted plate behind item art: the stock icons are made for light plates.
function hsFrosted(mc, x, y, w, h, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - w * 0.2,y:y - h * 0.25,w:w * 1.4,h:h * 1.4,r:0});
   hsRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function hsFrostedCircle(mc, x, y, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.2,y:y - r * 1.3,w:r * 2.6,h:r * 2.6,r:0});
   hsCircle(mc,x,y,r);
   mc.endFill();
}

// The gold of the page arrows, tags and selected chips.
function hsGoldRect(mc, x, y, w, h, r, over)
{
   mc.lineStyle(over ? 2 : 1.5,0xFFF3C4,over ? 100 : 90);
   mc.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   hsRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h / 2,r:Math.PI / 2});
   hsRoundRect(mc,x + 3,y + 2,w - 6,h / 2,Math.min(r,h / 4));
   mc.endFill();
}

function hsSparkle(mc, x, y, r, alpha)
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
function hsSparkles(mc, seed, count, x, y, w, h)
{
   var s = seed;
   var i;
   for(i = 0; i < count; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + s % w;
      s = (s * 1103515245 + 12345) % 2147483648;
      hsSparkle(mc,sx,y + s % h,1 + s % 3,25 + s % 50);
   }
}

function hsOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function hsShadow(distance, blur, alpha)
{
   return [new flash.filters.DropShadowFilter(distance,90,0x050A24,alpha,blur,blur,1,2)];
}

function hsGlow(color, blur)
{
   return [new flash.filters.GlowFilter(color,0.8,blur,blur,2,2)];
}

// A thin gold rule that fades out at both ends.
function hsGoldRule(mc, x, y, w)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[HS_GOLD,HS_GOLD,HS_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:x,y:y,w:w,h:2,r:0});
   hsRoundRect(mc,x,y,w,2,1);
   mc.endFill();
}

function hsRecess(mc, x, y, w, h)
{
   hsGradientRect(mc,x,y,w,h,20,0x0A1440,0x0A1440,45,HS_RIM,25,1);
}

// The instance at a timeline depth of _root (unnamed, or sharing its name with another).
function hsAtDepth(depth)
{
   return _root.getInstanceAtDepth(depth - 16384);
}

// --- FRIconButton skins ------------------------------------------------------------------
// A skinned button has hsDraw(state); its states call it after the stock code has swapped the art.

var hsStateUp = FRIconButton.prototype.setStateUp;
FRIconButton.prototype.setStateUp = function()
{
   hsStateUp.apply(this,arguments);
   if(this.hsDraw != undefined)
   {
      this.hsDraw("up");
   }
};

var hsStateOver = FRIconButton.prototype.setStateOver;
FRIconButton.prototype.setStateOver = function()
{
   hsStateOver.apply(this,arguments);
   if(this.hsDraw != undefined && this.m_isEnabled)
   {
      this.hsDraw("over");
   }
};

var hsStateDown = FRIconButton.prototype.setStateDown;
FRIconButton.prototype.setStateDown = function()
{
   hsStateDown.apply(this,arguments);
   if(this.hsDraw != undefined && this.m_isEnabled)
   {
      this.hsDraw("down");
   }
};

// Hides a button's state art (its hit area stays) and gives it a skin clip under everything in it.
function hsSkin(btn, draw)
{
   btn.up_mc._alpha = btn.over_mc._alpha = btn.down_mc._alpha = 0;
   btn.hsSkinMc = btn.createEmptyMovieClip("hsSkinMc",-16384);
   btn.hsDraw = draw;
   btn.hsDraw("up");
}

// --- frame -----------------------------------------------------------------------------

var hsFrame;

function hsDrawFrame()
{
   var bg = hsFrame.bg;
   bg.clear();
   hsGradientRect(bg,4,HS_TOP,HS_W - 8,HS_BOTTOM - HS_TOP,26,0x2E4FB0,0x141F55,100,HS_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-160,y:-160,w:760,h:420,r:0});
   hsRoundRect(bg,6,HS_TOP + 2,HS_W - 12,HS_BOTTOM - HS_TOP - 4,24);
   bg.endFill();
   hsSparkles(bg,6161,22,30,HS_TOP + 6,HS_W - 60,20);

   hsRecess(bg,HS_LEFT[0],HS_PANEL_Y,HS_LEFT[1],HS_PANEL_H);
   hsRecess(bg,HS_MID[0],HS_PANEL_Y,HS_MID[1],HS_PANEL_H);
   hsGoldRule(bg,HS_MID[0] + 60,HS_PANEL_Y + HS_PANEL_H - 5,HS_MID[1] - 120);

   // The chest beside the count (a stock shape at 20..44, 138..162) sits in a frosted socket.
   hsFrostedCircle(bg,32,150,17,HS_RIM,70,1.5);
}

// The capacity bar under the item count: how full the house is.
function hsDrawCapacity(cur, max)
{
   var c = hsFrame.capacity;
   c.clear();
   var x = HS_LEFT[0] + 10;
   var w = HS_LEFT[1] - 20;
   var y = 172;
   hsGradientRect(c,x,y,w,8,4,0x0A1440,0x0A1440,80,HS_RIM,35,1);
   var f = Number(max) > 0 ? Math.max(0,Math.min(1,Number(cur) / Number(max))) : 0;
   if(f > 0)
   {
      // Gold while there's room, warmer as the house fills up.
      var full = f >= 0.9;
      c.lineStyle();
      c.beginGradientFill("linear",full ? [0xFFB0A0,0xE0503A] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:8,r:Math.PI / 2});
      hsRoundRect(c,x + 1,y + 1,Math.max(6,(w - 2) * f),6,3);
      c.endFill();
   }
}

// The Housing Options card, lit gold while hovered (optionsButton_mc's skin draws here).
function hsDrawOptions(state)
{
   var o = hsFrame.options;
   o.clear();
   var over = state == "over" || state == "down";
   if(over)
   {
      hsGradientRect(o,HS_RIGHT[0],HS_PANEL_Y,HS_RIGHT[1],HS_PANEL_H,20,0x3F66D6,0x1D307A,90,0xFFE08A,100,2);
   }
   else
   {
      hsRecess(o,HS_RIGHT[0],HS_PANEL_Y,HS_RIGHT[1],HS_PANEL_H);
   }
   if(optionsButton_mc._visible)
   {
      // The house and gear (optionsButton_mc's icon) in a frosted socket.
      hsFrostedCircle(o,HS_RIGHT[0] + HS_RIGHT[1] / 2 - 4,151,27,over ? 0xFFE08A : HS_RIM,over ? 100 : 70,over ? 2.5 : 1.5);
   }
   o.filters = over ? hsGlow(HS_GOLD,16) : [];
}

function hsBuildFrame()
{
   hsFrame = _root.createEmptyMovieClip("hsFrame",-16384);
   hsFrame.bg = hsFrame.createEmptyMovieClip("bg",1);
   hsFrame.bg.filters = hsShadow(6,18,0.6);
   hsFrame.capacity = hsFrame.createEmptyMovieClip("capacity",2);
   hsFrame.options = hsFrame.createEmptyMovieClip("options",3);

   // The stock art: the tray backing and the two side boxes stay as invisible hit areas (the left one has the
   // count's tooltip), and the furniture tray's own panel (housing/tray.as) gives way to the recess.
   bg_mc._alpha = 0;
   hsAtDepth(4)._alpha = 0;
   hsAtDepth(6)._alpha = 0;
   _root.sanctuaryPanel_mc.clear();

   // The count: white with the dark outline (the "Items/Limit" label above it shares its instance name).
   var f = fixtureCount_txt.getTextFormat();
   f.size = 16;
   f.color = 0xFFFFFF;
   fixtureCount_txt.setTextFormat(f);
   fixtureCount_txt.setNewTextFormat(f);
   fixtureCount_txt.filters = hsOutline();
   noItems_mc.filters = hsOutline();

   hsDrawFrame();
   hsDrawCapacity(0,0);
}

// --- category buttons ------------------------------------------------------------------

// A frosted disc in a navy glass rim: gold for the selected category, lit and lifted on hover.
function hsDrawCategory(state)
{
   var s = this.hsSkinMc;
   s.clear();
   this.hsState = state;
   var selected = this == this._parent.hsSelected;
   var over = state == "over" || state == "down";
   var cx = 25.2;
   var cy = 25.9;
   if(selected)
   {
      s.lineStyle(2,0xFFF3C4,100);
      s.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:cx - 25,y:cy - 25,w:50,h:50,r:Math.PI / 2});
   }
   else
   {
      s.lineStyle(over ? 2 : 1.5,over ? 0xFFE08A : HS_RIM,over ? 100 : 80);
      s.beginGradientFill("linear",over ? [0x4F78E6,0x1D307A] : [0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:cx - 25,y:cy - 25,w:50,h:50,r:Math.PI / 2});
   }
   hsCircle(s,cx,cy,25);
   s.endFill();
   hsFrostedCircle(s,cx,cy,20.5,selected ? 0xFFF3C4 : 0x0A1440,selected ? 100 : 40,1);
   if(selected)
   {
      this.filters = hsGlow(HS_GOLD,14);
   }
   else if(over)
   {
      this.filters = hsGlow(0xFFE08A,10);
   }
   else
   {
      this.filters = hsShadow(3,8,0.55);
   }
   gs.TweenLite.to(this,0.3,{_y:over ? -5 : 0,ease:mx.transitions.easing.Strong.easeOut});
}

function hsRedrawCategories(cat)
{
   var i;
   for(i = 0; i < cat.m_itemArray.length; i++)
   {
      var b = cat.m_itemArray[i];
      if(b.hsDraw != undefined)
      {
         b.hsDraw(b.hsState == undefined ? "up" : b.hsState);
      }
   }
}

var hsCatAddItem = CategoryButtons.prototype.addItem;
CategoryButtons.prototype.addItem = function(catId, iconId, name)
{
   hsCatAddItem.apply(this,arguments);
   // The spinning rays behind the selected button give way to the gold ring.
   this.shine._alpha = 0;
   delete this.shine.onEnterFrame;
   hsSkin(this.m_itemArray[this.m_itemArray.length - 1],hsDrawCategory);
};

var hsCatOnClick = CategoryButtons.prototype.onClick;
CategoryButtons.prototype.onClick = function(btn)
{
   hsCatOnClick.apply(this,arguments);
   this.hsSelected = btn;
   hsRedrawCategories(this);
};

// --- items -----------------------------------------------------------------------------

// The glass card behind an item (it covers the stock slot art, a shape under the icon).
function hsDrawItem(item, over)
{
   var c = item.hsCard;
   c.clear();
   hsGradientRect(c,-35,-35,70,70,16,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : HS_RIM,over ? 100 : 55,over ? 2.5 : 1.5);
   hsFrosted(c,-30,-30,60,60,12,0x0A1440,30,1);
   item.filters = over ? hsGlow(HS_GOLD,14) : hsShadow(3,8,0.5);
}

// The quantity: dark digits on a gold tag in place of the stock pill.
function hsDrawQuantity(q)
{
   if(q.hsTag == undefined)
   {
      q.hsTag = q.createEmptyMovieClip("hsTag",-16384);
      q.bg._alpha = 0;
      q.amount.filters = [];
   }
   var t = q.hsTag;
   t.clear();
   if(!q._visible)
   {
      return;
   }
   q.amount.textColor = HS_INK;
   var w = Math.max(22,q.amount.textWidth + 14);
   var cx = q.amount._x + q.amount._width / 2;
   hsGoldRect(t,cx - w / 2,4,w,21,10.5,false);
   t.filters = hsShadow(2,5,0.5);
}

function hsStyleItem(item)
{
   if(item == undefined || item.hsCard != undefined)
   {
      return;
   }
   // The card goes between the stock slot shape and the icon and quantity, which move above it.
   item.m_icon.swapDepths(20);
   item.m_quantityInfo.swapDepths(30);
   item.hsCard = item.createEmptyMovieClip("hsCard",10);
   // Drags over the item light it too (the stock constructor copied the unwrapped handlers).
   item.onDragOver = FRBarItem.prototype.onRollOver;
   item.onDragOut = FRBarItem.prototype.onRollOut;
   hsDrawItem(item,false);
   hsDrawQuantity(item.m_quantityInfo);
}

function hsStyleItems()
{
   var pages = [items_mc.m_fiveItems1_mc,items_mc.m_fiveItems2_mc];
   var p;
   var i;
   for(p = 0; p < pages.length; p++)
   {
      for(i = 0; i < SANCTUARY_TRAY_ITEMS; i++)
      {
         hsStyleItem(pages[p]["item" + i]);
      }
   }
}

var hsItemRollOver = FRBarItem.prototype.onRollOver;
FRBarItem.prototype.onRollOver = function()
{
   hsItemRollOver.apply(this,arguments);
   if(this.hsCard != undefined)
   {
      hsDrawItem(this,true);
   }
};

var hsItemRollOut = FRBarItem.prototype.onRollOut;
FRBarItem.prototype.onRollOut = function()
{
   hsItemRollOut.apply(this,arguments);
   if(this.hsCard != undefined)
   {
      hsDrawItem(this,false);
   }
};

var hsItemReleaseOutside = FRBarItem.prototype.onReleaseOutside;
FRBarItem.prototype.onReleaseOutside = function()
{
   if(hsItemReleaseOutside != undefined)
   {
      hsItemReleaseOutside.apply(this,arguments);
   }
   else
   {
      gs.TweenLite.to(this,0.5,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
   }
   if(this.hsCard != undefined)
   {
      hsDrawItem(this,false);
   }
};

var hsItemSetCount = FRBarItem.prototype.setCount;
FRBarItem.prototype.setCount = function(c)
{
   hsItemSetCount.apply(this,arguments);
   if(this.hsCard != undefined)
   {
      hsDrawQuantity(this.m_quantityInfo);
   }
};

// --- page arrows -----------------------------------------------------------------------

// A gold circle with a dark chevron; the right arrow is the left one mirrored, so it points right.
function hsDrawArrow(state)
{
   var s = this.hsSkinMc;
   s.clear();
   var over = state == "over" || state == "down";
   var cx = 15.5;
   var cy = 24.5;
   var r = 15;
   s.lineStyle(over ? 2 : 1.5,0xFFF3C4,100);
   s.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:cx - r,y:cy - r,w:r * 2,h:r * 2,r:Math.PI / 2});
   hsCircle(s,cx,cy,r);
   s.endFill();
   s.lineStyle();
   s.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:cx - r,y:cy - r,w:r * 2,h:r,r:Math.PI / 2});
   hsRoundRect(s,cx - 10,cy - 13,20,11,5);
   s.endFill();
   s.lineStyle(4,HS_INK,100);
   s.moveTo(cx + 3,cy - 7);
   s.lineTo(cx - 4,cy);
   s.lineTo(cx + 3,cy + 7);
   this.filters = over ? hsGlow(HS_GOLD,14) : hsShadow(3,8,0.5);
   s._y = state == "down" ? 1 : 0;
}

// --- radios (Lock in farming mode, Snap to Grid) ---------------------------------------

// A navy box that fills gold with a dark check when on, drawn over the stock dot.
function hsDrawRadio(rad)
{
   var b = rad.hsBox;
   b.clear();
   var on = rad.fill_mc._visible;
   if(on)
   {
      hsGoldRect(b,0,-1,21,21,6,false);
      b.lineStyle(3,HS_INK,100);
      b.moveTo(5,9);
      b.lineTo(9,14);
      b.lineTo(16,4);
   }
   else
   {
      hsGradientRect(b,0,-1,21,21,6,0x0A1440,0x1D307A,100,HS_RIM,70,1.5);
   }
   b.filters = on ? hsGlow(HS_GOLD,8) : [];
}

function hsSkinRadio(rad)
{
   rad.fill_mc._alpha = 0;
   rad.hsBox = rad.createEmptyMovieClip("hsBox",rad.getNextHighestDepth());
   rad.txt.filters = hsOutline();
   hsDrawRadio(rad);
}

var hsRadioSetFill = FRRadioButton.prototype.setFill;
FRRadioButton.prototype.setFill = function(f)
{
   hsRadioSetFill.apply(this,arguments);
   if(this.hsBox != undefined)
   {
      hsDrawRadio(this);
   }
};

// --- search (housing/tray.as's letter search) ------------------------------------------

function hsDrawKey(key, over)
{
   key.clear();
   var back = key.letter == "<";
   if(back || over)
   {
      hsGoldRect(key,0,0,16,16,5,over);
   }
   else
   {
      hsGradientRect(key,0,0,16,16,5,0x2E4FB0,0x162660,100,HS_RIM,55,1);
   }
   key.t.textColor = back || over ? HS_INK : 0xFFFFFF;
   key.filters = over ? hsGlow(HS_GOLD,8) : [];
}

// The search becomes a glass tab over the tray's right end: a recessed query pill and navy letter chips.
function hsStyleSearch()
{
   var bar = _root.sanctuarySearch_mc;
   if(bar == undefined)
   {
      return;
   }
   bar.clear();
   hsGradientRect(bar,-6,-2,248,60,14,0x2E4FB0,0x141F55,100,HS_RIM,80,2);
   bar.lineStyle();
   bar.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[30,0],[0,255],{matrixType:"box",x:-60,y:-60,w:240,h:140,r:0});
   hsRoundRect(bar,-4,0,244,56,12);
   bar.endFill();
   hsGradientRect(bar,0,0,236,18,9,0x0A1440,0x0A1440,80,HS_RIM,45,1);
   // A small magnifier at the pill's right end.
   bar.lineStyle(2,HS_GOLD,100);
   hsCircle(bar,224,8,4);
   bar.moveTo(227,11);
   bar.lineTo(231,15);
   bar.filters = hsShadow(4,12,0.55);

   var i;
   for(i = 0; i < 27; i++)
   {
      var key = bar["key" + i + "_mc"];
      if(key == undefined)
      {
         continue;
      }
      hsDrawKey(key,false);
      key.onRollOver = function()
      {
         hsDrawKey(this,true);
         fscommand("FR_event","PlaySound(DockOver)");
      };
      key.onRollOut = key.onReleaseOutside = function()
      {
         hsDrawKey(this,false);
      };
   }
}

// --- Lua -------------------------------------------------------------------------------

var hsSetCounts = SetCounts;
SetCounts = function(curFixtureCount, maxFixtureCount, curLandMarkCount, maxLandMarkCount)
{
   hsSetCounts.apply(_root,arguments);
   hsDrawCapacity(curFixtureCount,maxFixtureCount);
};

// Farming swaps Housing Options for the Lock radio; the card follows.
var hsSetFarmingMode = setFarmingMode_lua;
setFarmingMode_lua = function(is)
{
   hsSetFarmingMode.apply(_root,arguments);
   hsDrawOptions("up");
};

// --- build -----------------------------------------------------------------------------

function hsBuild()
{
   hsBuildFrame();

   items_mc.m_left_mc._x += HS_ARROW_SHIFT;
   items_mc.m_right_mc._x -= HS_ARROW_SHIFT;
   hsSkin(items_mc.m_left_mc,hsDrawArrow);
   hsSkin(items_mc.m_right_mc,hsDrawArrow);

   hsStyleItems();

   // Housing Options: its label and icon stay; the card under it lights up with its states.
   optionsButton_mc.up_mc.textField_txt.filters = optionsButton_mc.over_mc.textField_txt.filters = optionsButton_mc.down_mc.textField_txt.filters = hsOutline();
   optionsButton_mc.hsDraw = hsDrawOptions;
   hsDrawOptions("up");

   hsSkinRadio(lockRadio_mc);
   hsSkinRadio(gridRadio_mc);

   hsStyleSearch();

   // Category buttons added before this ran (none in game: Lua adds them later).
   if(catButtons != undefined)
   {
      catButtons.shine._alpha = 0;
      var i;
      for(i = 0; i < catButtons.m_itemArray.length; i++)
      {
         hsSkin(catButtons.m_itemArray[i],hsDrawCategory);
      }
   }
}

hsBuild();
// ===== End Sanctuary furniture tray style =====

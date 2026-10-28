// ===== Sanctuary Player Trade =====
// Appended to player_trade's main timeline by build_client_mods.py.
// Redraws the trade window in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass frame
// with the title in gold and three recessed bays (your stuff, your offer, your partner's offer) with a gold
// exchange medallion between the offers. Items are glass cards (or rows in list view) like the equipment pane's
// (ClientMods/equipment/equipment_pane.as): the art a little bigger in a frosted socket with rounded corners, the
// selected item rimmed in gold, hovered ones lifting with a gold glow. Coin offers sit in gold-rimmed panels, the
// grid/list toggles and scrollbars are gold, and an offer that's been accepted is ringed in gold with a Ready pill.
// The stock clips keep their roles (hit areas, the quantity arrows, the coin stepper, Lua's Invoke targets);
// their art is hidden and drawn over. Members Only badges are gone. There is no close button: the trade is
// accepted or cancelled from the navigation bar under the window, as stock.

var PTR_GOLD = 0xFFC531;
var PTR_RIM = 0x9DB8FF;
var PTR_SOFT = 0xC9D6FF;
var PTR_INK = 0x3A2400;
// The frame and the three bays (x, y, w, h), in the window's coordinates.
var PTR_FRAME = [54,64,932,576];
var PTR_BAYS = [[68,146,286,478],[362,146,272,478],[694,146,276,478]];
var PTR_STRIP_Y = 170;
var PTR_STRIP_H = 42;
// The inventory pane sits 10px further left than stock, so its cards are centered in their bay.
var PTR_INVENTORY_SHIFT = -10;
// An item card inside its 226x96 cell, its socket and thumbnail (the stock icon is 64px).
var PTR_CARD = [4,3,216,90];
var PTR_SOCKET = [10,10,76,76];
var PTR_ICON_X = 48;
var PTR_ICON_Y = 48;
var PTR_ICON_SCALE = 112;
var PTR_ICON_HOVER = 122;
var PTR_LABEL = [92,10,120,54];

var ptrRoot;
var ptrTop;

// --- drawing helpers -------------------------------------------------------------------

function ptrRoundRect(mc, x, y, w, h, r)
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

function ptrGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   ptrRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function ptrCircle(mc, x, y, r)
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

function ptrText(mc, name, txt, x, y, w, h, size, color, align)
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

function ptrSparkle(mc, x, y, r, alpha)
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

function ptrOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function ptrShadow()
{
   return [new flash.filters.DropShadowFilter(1,90,0x0A1030,0.9,2,2,1,1)];
}

function ptrGoldFill(mc, x, y, w, h)
{
   mc.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
}

// Recolours a stock field, now and for text set later.
function ptrColor(t, color, size)
{
   var f = new TextFormat();
   f.color = color;
   if(size != undefined)
   {
      f.size = size;
   }
   t.setNewTextFormat(f);
   t.setTextFormat(f);
}

// Rarity colours made for the stock beige cards can be too dark for navy; they're lifted halfway to white.
function ptrLight(c)
{
   var r = c >> 16 & 0xFF;
   var g = c >> 8 & 0xFF;
   var b = c & 0xFF;
   if(isNaN(c) || r * 0.3 + g * 0.59 + b * 0.11 >= 150)
   {
      return isNaN(c) ? 0xFFFFFF : c;
   }
   return Math.round((r + 255) / 2) << 16 | Math.round((g + 255) / 2) << 8 | Math.round((b + 255) / 2);
}

function ptrLightLabel(t)
{
   var f = t.getTextFormat();
   var c = ptrLight(Number(f.color));
   if(c != f.color)
   {
      f.color = c;
      t.setTextFormat(f);
   }
}

// Bounds in the window's coordinates; stock buttons (Button objects, registered at their top left) have no getBounds.
function ptrBounds(mc)
{
   if(typeof(mc) == "movieclip")
   {
      return mc.getBounds(_root);
   }
   var a = {x:mc._x,y:mc._y};
   var b = {x:mc._x + mc._width,y:mc._y + mc._height};
   mc._parent.localToGlobal(a);
   mc._parent.localToGlobal(b);
   _root.globalToLocal(a);
   _root.globalToLocal(b);
   return {xMin:a.x,yMin:a.y,xMax:b.x,yMax:b.y};
}

// --- frame -----------------------------------------------------------------------------

function ptrDrawFrame(bg)
{
   var f = PTR_FRAME;
   ptrGradientRect(bg,f[0],f[1],f[2],f[3],30,0x2E4FB0,0x141F55,100,PTR_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:f[0] - 160,y:f[1] - 300,w:1000,h:720,r:0});
   ptrRoundRect(bg,f[0] + 2,f[1] + 2,f[2] - 4,f[3] - 4,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   ptrRoundRect(bg,f[0] + 12,f[1] + 12,f[2] - 24,62,26);
   bg.endFill();

   // Sparkles in the header (fixed seed, same every time).
   var s = 7373;
   var i;
   for(i = 0; i < 30; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = f[0] + 30 + s % (f[2] - 60);
      s = (s * 1103515245 + 12345) % 2147483648;
      ptrSparkle(bg,x,f[1] + 18 + s % 52,1 + s % 3,25 + s % 50);
   }

   // The bays, each with a header strip.
   for(i = 0; i < PTR_BAYS.length; i++)
   {
      var b = PTR_BAYS[i];
      ptrGradientRect(bg,b[0],b[1],b[2],b[3],22,0x0A1440,0x0A1440,38,PTR_RIM,22,1);
      ptrGradientRect(bg,b[0] + 8,PTR_STRIP_Y,b[2] - 16,PTR_STRIP_H,PTR_STRIP_H / 2,0x0A1440,0x0A1440,50,PTR_RIM,30,1);
      bg.lineStyle();
      bg.beginGradientFill("linear",[PTR_GOLD,PTR_GOLD,PTR_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:b[0] + 24,y:0,w:b[2] - 48,h:2,r:0});
      ptrRoundRect(bg,b[0] + 24,PTR_STRIP_Y + PTR_STRIP_H + 2,b[2] - 48,2,1);
      bg.endFill();
   }
   ptrDrawExchange(bg);
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
}

// The medallion between the two offers: a gold ring with an arrow each way.
function ptrDrawExchange(bg)
{
   var cx = (PTR_BAYS[1][0] + PTR_BAYS[1][2] + PTR_BAYS[2][0]) / 2;
   var cy = 360;
   bg.lineStyle();
   bg.beginGradientFill("radial",[PTR_GOLD,PTR_GOLD],[40,0],[0,255],{matrixType:"box",x:cx - 40,y:cy - 40,w:80,h:80,r:0});
   ptrCircle(bg,cx,cy,40);
   bg.endFill();
   bg.lineStyle(3,PTR_GOLD,100);
   bg.beginGradientFill("radial",[0x4A70DC,0x172A6E],[100,100],[0,255],{matrixType:"box",x:cx - 24,y:cy - 34,w:48,h:48,r:0});
   ptrCircle(bg,cx,cy,22);
   bg.endFill();
   bg.lineStyle();
   ptrGoldFill(bg,cx - 12,cy - 12,24,10);
   bg.moveTo(cx - 12,cy - 9);
   bg.lineTo(cx + 3,cy - 9);
   bg.lineTo(cx + 3,cy - 14);
   bg.lineTo(cx + 13,cy - 6);
   bg.lineTo(cx + 3,cy + 2);
   bg.lineTo(cx + 3,cy - 3);
   bg.lineTo(cx - 12,cy - 3);
   bg.lineTo(cx - 12,cy - 9);
   bg.endFill();
   bg.beginFill(PTR_SOFT,100);
   bg.moveTo(cx + 12,cy + 9);
   bg.lineTo(cx - 3,cy + 9);
   bg.lineTo(cx - 3,cy + 14);
   bg.lineTo(cx - 13,cy + 6);
   bg.lineTo(cx - 3,cy - 2);
   bg.lineTo(cx - 3,cy + 3);
   bg.lineTo(cx + 12,cy + 3);
   bg.lineTo(cx + 12,cy + 9);
   bg.endFill();
}

// Your coin offer (the stepper) and the coins you have left, and your partner's coin offer.
function ptrDrawCoins(bg)
{
   var b = PTR_BAYS[1];
   ptrGradientRect(bg,b[0] + 12,522,b[2] - 24,94,20,0x0A1440,0x0A1440,50,PTR_GOLD,55,1.5);
   bg.lineStyle();
   bg.beginGradientFill("linear",[PTR_GOLD,PTR_GOLD,PTR_GOLD],[0,60,0],[0,128,255],{matrixType:"box",x:b[0] + 30,y:0,w:b[2] - 60,h:1,r:0});
   ptrRoundRect(bg,b[0] + 30,569,b[2] - 60,1,0.5);
   bg.endFill();

   // The stepper's field: a recessed pill; its arrows are gold.
   var sb = ptrBounds(stepper.background);
   ptrGradientRect(bg,sb.xMin,sb.yMin + 1,sb.xMax - sb.xMin,sb.yMax - sb.yMin - 2,(sb.yMax - sb.yMin - 2) / 2,0x050A24,0x0A1440,85,0xFFE08A,80,1.5);
   ptrChevron(bg,ptrBounds(stepper.up),-1);
   ptrChevron(bg,ptrBounds(stepper.down),1);

   var cb = ptrBounds(plyr2.coins.background);
   ptrGradientRect(bg,cb.xMin + 2,cb.yMin + 2,cb.xMax - cb.xMin - 4,cb.yMax - cb.yMin - 4,(cb.yMax - cb.yMin - 4) / 2,0x284A9E,0x162660,100,PTR_GOLD,90,2);
   bg.lineStyle();
   bg.beginFill(0xFFFFFF,14);
   ptrRoundRect(bg,cb.xMin + 12,cb.yMin + 5,cb.xMax - cb.xMin - 24,(cb.yMax - cb.yMin) * 0.3,(cb.yMax - cb.yMin) * 0.15);
   bg.endFill();
}

// A gold triangle centered in a stock arrow button's bounds, pointing up (-1) or down (1).
function ptrChevron(mc, b, dir)
{
   var cx = (b.xMin + b.xMax) / 2;
   var cy = (b.yMin + b.yMax) / 2;
   mc.lineStyle(1,0xFFF3C4,90);
   ptrGoldFill(mc,cx - 6,cy - 5,12,10);
   mc.moveTo(cx - 6,cy - 4 * dir);
   mc.lineTo(cx + 6,cy - 4 * dir);
   mc.lineTo(cx,cy + 5 * dir);
   mc.lineTo(cx - 6,cy - 4 * dir);
   mc.endFill();
}

// The other player's portrait: a gold-rimmed frosted plate where the stock frame was.
function ptrDrawPortrait(bg)
{
   var p = ptrBounds(plyr2.p2info.background);
   var s = Math.min(p.xMax - p.xMin,p.yMax - p.yMin) + 6;
   var cx = (p.xMin + p.xMax) / 2;
   var cy = (p.yMin + p.yMax) / 2;
   bg.lineStyle(2.5,PTR_GOLD,100);
   bg.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:cx - s * 0.65,y:cy - s * 0.75,w:s * 1.3,h:s * 1.3,r:0});
   ptrRoundRect(bg,cx - s / 2,cy - s / 2,s,s,s * 0.24);
   bg.endFill();
}

// --- stock chrome ----------------------------------------------------------------------

function ptrHideChrome()
{
   background._alpha = decoration._alpha = wndIcon._alpha = coinBackground._alpha = 0;
   plyr1.background._alpha = plyr1.bottomLine._alpha = 0;
   plyr2.background._alpha = plyr2.listBackground._alpha = 0;
   // The partner pane's header bar (an unnamed sprite at its depth 12).
   plyr2.getInstanceAtDepth(12 - 16384)._alpha = 0;
   plyr2.coins.background._alpha = plyr2.p2info.background._alpha = 0;
   var p = plyr1.pTrade;
   p.listBackground._alpha = p.buttonViewTitleBar._alpha = 0;
   plyr1.pInventory.buttonViewTitleBar._alpha = 0;
   stepper.background._alpha = stepper.up._alpha = stepper.down._alpha = 0;
   var views = [plyr1.pInventory,plyr1.pTrade,plyr2];
   var i;
   for(i = 0; i < views.length; i++)
   {
      views[i].buttonsView_btn._alpha = views[i].listView_btn._alpha = 0;
      // The accepted ring: the stock green frame, turned gold.
      if(views[i].accepted != undefined)
      {
         new Color(views[i].accepted).setTransform({ra:0,ga:0,ba:0,rb:255,gb:197,bb:49,aa:100,ab:0});
         views[i].accepted.filters = [new flash.filters.GlowFilter(PTR_GOLD,0.7,14,14,2,2)];
      }
   }
}

function ptrStyleText()
{
   var f = PTR_FRAME;
   // The title (the stock field, localized) in gold across the header.
   wndTitle._x = f[0];
   wndTitle._width = f[2];
   wndTitle._y = f[1] + 14;
   wndTitle._height = 40;
   var tf = wndTitle.getTextFormat();
   tf.size = 28;
   tf.color = 0xFFE08A;
   tf.align = "center";
   wndTitle.setTextFormat(tf);
   wndTitle.filters = ptrOutline();
   ptrText(ptrRoot,"subtitle_txt","Both players accept before anything changes hands",f[0],f[1] + 50,f[2],22,14,PTR_SOFT).filters = ptrShadow();

   // The bays' captions (stock fields) centered in their strips.
   var inv = plyr1.pInventory.title_obj.title;
   ptrColor(inv,0xFFFFFF);
   inv.filters = ptrOutline();
   ptrColor(plyr1.pTrade.label,0xFFFFFF);
   plyr1.pTrade.label.filters = ptrOutline();
   ptrColor(plyr2.p2info.pName,0xFFFFFF);
   plyr2.p2info.pName.filters = ptrOutline();

   // Coins: your offer in the stepper, what you'd have left, and your partner's offer, in gold.
   ptrColor(totalCoins,0xFFE08A);
   totalCoins.filters = ptrOutline();
   ptrColor(my_coins_label,PTR_SOFT);
   my_coins_label.filters = ptrShadow();
   ptrColor(plyr2.coins.coinAmt,0xFFE08A);
   plyr2.coins.coinAmt.filters = ptrOutline();
   stepper.output.filters = ptrShadow();
}

// --- item cards (grid view) ------------------------------------------------------------

function ptrDrawCard(b, over, down)
{
   var c = b.sanctuaryCard;
   var sel = b.selection._visible;
   var y = PTR_CARD[1] + (down ? 2 : 0);
   var x = PTR_CARD[0];
   var w = PTR_CARD[2];
   var h = PTR_CARD[3];
   c.clear();
   if(over)
   {
      ptrGradientRect(c,x,y,w,h,18,0x4069DA,0x1E3486,100,sel ? PTR_GOLD : 0xFFE08A,100,sel ? 3 : 2);
   }
   else
   {
      ptrGradientRect(c,x,y,w,h,18,0x2B4FA8,0x17286A,100,sel ? PTR_GOLD : PTR_RIM,sel ? 100 : 40,sel ? 3 : 1.5);
   }
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[16,0],[0,255],{matrixType:"box",x:0,y:y + 3,w:10,h:40,r:Math.PI / 2});
   ptrRoundRect(c,x + 3,y + 3,w - 6,40,15);
   c.endFill();
   // The thumbnail's socket: frosted white, so the stock icons (made for light plates) read well.
   var s = PTR_SOCKET;
   c.lineStyle(2,sel ? PTR_GOLD : 0xFFFFFF,sel ? 100 : 85);
   c.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:s[0] - 14,y:y + s[1] - 20,w:s[2] + 28,h:s[3] + 28,r:0});
   ptrRoundRect(c,s[0],y - PTR_CARD[1] + s[1],s[2],s[3],14);
   c.endFill();
   if(over)
   {
      c.filters = [new flash.filters.GlowFilter(PTR_GOLD,0.55,14,14,2,2)];
   }
   else if(sel)
   {
      c.filters = [new flash.filters.GlowFilter(PTR_GOLD,0.35,10,10,2,2)];
   }
   else
   {
      c.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
   }
}

function ptrStyleCard(b)
{
   if(b.sanctuaryCard)
   {
      return;
   }
   b.background._alpha = b.states._alpha = b.iconBackground._alpha = b.selection._alpha = 0;
   b.hitArea._alpha = 0;
   b.sanctuaryCard = b.createEmptyMovieClip("sanctuaryCard",-16384);
   b.icon._x = PTR_ICON_X;
   b.icon._y = PTR_ICON_Y;
   b.icon._xscale = b.icon._yscale = PTR_ICON_SCALE;
   // Rounds off the thumbnail's corners (and crops it to the socket when it grows on hover). It loads into
   // icon.holder, so the mask on the icon stays.
   var s = PTR_SOCKET;
   var mask = b.createEmptyMovieClip("sanctuaryIconMask",b.getNextHighestDepth());
   mask.beginFill(0,100);
   ptrRoundRect(mask,s[0] + 2,s[1] + 2,s[2] - 4,s[3] - 4,12);
   mask.endFill();
   b.icon.setMask(mask);

   var l = PTR_LABEL;
   b.label._x = l[0];
   b.label._y = l[1];
   b.label._width = l[2];
   b.label._height = l[3];
   b.label.filters = ptrShadow();
   // The stack size as a badge on the socket's corner, the no-trade lock in the card's corner.
   b.quantityObject._x = s[0] + s[2] - 26;
   b.quantityObject._y = s[1] + s[3] - 14;
   b.lock._x = PTR_CARD[0] + PTR_CARD[2] - 34;
   b.lock._y = PTR_CARD[1] + PTR_CARD[3] - 36;
   b.lock._xscale = b.lock._yscale = 80;
   ptrDrawCard(b,false,false);
}

function ptrPatchCards()
{
   var proto = TradeItemButton.prototype;
   TradeItemButton.iconOrigY = PTR_ICON_Y;
   TradeItemButton.labelOrigY = PTR_LABEL[1];

   // Styled before TradeItem.onLoad applies the queued item, so the name is fitted to the new label size.
   var onLoad = proto.onLoad;
   proto.onLoad = function()
   {
      ptrStyleCard(this);
      return onLoad.apply(this,arguments);
   };

   // Members Only no longer exists: no badge.
   var setup = proto.setupPropertiesDisplay;
   proto.setupPropertiesDisplay = function()
   {
      this._membersOnlyIcon = false;
      var r = setup.apply(this,arguments);
      this.properties._visible = false;
      return r;
   };

   var onSetItem = proto.onSetItem;
   proto.onSetItem = function()
   {
      var r = onSetItem.apply(this,arguments);
      ptrLightLabel(this.label);
      return r;
   };

   var select = proto.select;
   proto.select = function(value)
   {
      var r = select.apply(this,arguments);
      if(this.sanctuaryCard)
      {
         ptrDrawCard(this,false,false);
      }
      return r;
   };

   // The stock states only switch the beige plates (hidden) and pop the icon; this redraws the card instead.
   proto.updateState = function(mode)
   {
      var over = mode == 2 || mode == 3;
      var down = mode == 3 ? TradeItemButton.pressDisplacement : 0;
      if(this.sanctuaryCard)
      {
         ptrDrawCard(this,over,mode == 3);
      }
      if(over)
      {
         gs.TweenLite.to(this.icon,0.6,{_xscale:PTR_ICON_HOVER,_yscale:PTR_ICON_HOVER,ease:mx.transitions.easing.Elastic.easeOut});
      }
      else
      {
         gs.TweenLite.to(this.icon,0.15,{_xscale:PTR_ICON_SCALE,_yscale:PTR_ICON_SCALE});
      }
      this.icon._y = PTR_ICON_Y + down;
      this.label._y = PTR_LABEL[1] + down;
      this.properties._y = TradeItemButton.propOrigY + down;
   };
}

// The stack size: a navy pill with a gold rim and gold numbers, sized to the label.
function ptrPatchQuantity()
{
   var proto = fr.extras.FRItemButtonQuantityDisplay.prototype;
   var resize = proto.resize;
   proto.resize = function()
   {
      var r = resize.apply(this,arguments);
      this.background._alpha = 0;
      var p = this.sanctuaryPill;
      if(p == undefined)
      {
         p = this.sanctuaryPill = this.createEmptyMovieClip("sanctuaryPill",-16384);
         ptrColor(this.label,0xFFE08A);
         this.label.filters = ptrOutline();
      }
      p.clear();
      p.lineStyle(1.5,PTR_GOLD,100);
      p.beginFill(0x0A1440,90);
      ptrRoundRect(p,0,0,Math.ceil(this.label._width) + this.label._x * 2,this.label._height + 4,(this.label._height + 4) / 2);
      p.endFill();
      return r;
   };
}

// --- item rows (list view) -------------------------------------------------------------

function ptrDrawRow(row, over)
{
   var r = row.sanctuaryRow;
   var w = row.hitArea._width - 4;
   r.clear();
   if(over)
   {
      ptrGradientRect(r,0,1,w,30,12,0x4069DA,0x1E3486,100,0xFFE08A,100,1.5);
   }
   else if(row.selected)
   {
      ptrGradientRect(r,0,1,w,30,12,0x2B4FA8,0x17286A,100,PTR_GOLD,100,2);
   }
   else
   {
      r.lineStyle();
      r.beginFill(0x0A1440,row.index % 2 == 0 ? 18 : 34);
      ptrRoundRect(r,0,1,w,30,12);
      r.endFill();
   }
   // A small frosted socket behind the icon.
   var cx = row.icon._x;
   var cy = row.icon._y;
   r.lineStyle(1.5,row.selected ? PTR_GOLD : 0xFFFFFF,row.selected ? 100 : 80);
   r.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:cx - 16,y:cy - 18,w:32,h:32,r:0});
   ptrCircle(r,cx,cy,13);
   r.endFill();
}

function ptrPatchRows()
{
   var proto = TradeItemList.prototype;
   var onLoad = proto.onLoad;
   proto.onLoad = function()
   {
      if(!this.sanctuaryRow)
      {
         this.shade._alpha = this.highlight._alpha = this.hitArea._alpha = 0;
         this.sanctuaryRow = this.createEmptyMovieClip("sanctuaryRow",-16384);
         this.label.filters = ptrShadow();
         ptrColor(this.ItemQuantity,0xFFE08A);
         this.ItemQuantity.filters = ptrShadow();
      }
      return onLoad.apply(this,arguments);
   };
   var onSetItem = proto.onSetItem;
   proto.onSetItem = function()
   {
      var r = onSetItem.apply(this,arguments);
      // Members Only no longer exists.
      this.membersOnlyIcon._visible = false;
      ptrLightLabel(this.label);
      return r;
   };
   var updateState = proto.updateState;
   proto.updateState = function(mode)
   {
      var r = updateState.apply(this,arguments);
      if(this.sanctuaryRow)
      {
         ptrDrawRow(this,mode == 2 || mode == 3);
      }
      return r;
   };
}

// --- following the panes ---------------------------------------------------------------

// The grid/list toggle: a pill around the two (invisible) stock buttons, the current mode's in a gold circle.
function ptrSyncToggle(view, mc)
{
   var mode = view.item_display.mode;
   if(mc.mode == mode)
   {
      return;
   }
   mc.mode = mode;
   var g = ptrBounds(view.buttonsView_btn);
   var l = ptrBounds(view.listView_btn);
   var gx = (g.xMin + g.xMax) / 2;
   var lx = (l.xMin + l.xMax) / 2;
   var cy = (g.yMin + g.yMax) / 2;
   var list = mode == 1;
   mc.clear();
   ptrGradientRect(mc,gx - 22,cy - 19,lx - gx + 44,38,19,0x0A1440,0x0A1440,55,PTR_RIM,30,1);
   var cx = list ? lx : gx;
   mc.lineStyle(1,0xFFF3C4,80);
   ptrGoldFill(mc,cx - 15,cy - 15,30,30);
   ptrCircle(mc,cx,cy,15);
   mc.endFill();
   // Grid glyph: four squares.
   mc.lineStyle();
   mc.beginFill(list ? PTR_SOFT : PTR_INK,100);
   ptrRoundRect(mc,gx - 7,cy - 7,6,6,1);
   ptrRoundRect(mc,gx + 1,cy - 7,6,6,1);
   ptrRoundRect(mc,gx - 7,cy + 1,6,6,1);
   ptrRoundRect(mc,gx + 1,cy + 1,6,6,1);
   mc.endFill();
   // List glyph: three lines.
   mc.beginFill(list ? PTR_INK : PTR_SOFT,100);
   ptrRoundRect(mc,lx - 7,cy - 7,14,3,1);
   ptrRoundRect(mc,lx - 7,cy - 1,14,3,1);
   ptrRoundRect(mc,lx - 7,cy + 5,14,3,1);
   mc.endFill();
}

// A pane's scrollbar (stock, invisible): a slim navy track with a gold thumb.
function ptrSyncScrollBar(sb, s)
{
   if(sb == undefined || !sb._visible)
   {
      s._visible = false;
      s.key = undefined;
      return;
   }
   // enableScrollBar puts the stock parts back to 50% or 100%.
   sb.background_mc._alpha = sb.channel_mc._alpha = sb.thumb_mc._alpha = 0;
   sb.upArrow_btn._alpha = sb.downArrow_btn._alpha = 0;
   s._visible = true;
   var b = ptrBounds(sb.channel_mc);
   var t = ptrBounds(sb.thumb_mc);
   var key = Math.round(b.xMin) + "," + Math.round(b.yMin) + "," + Math.round(b.yMax) + "," + Math.round(t.yMin) + "," + Math.round(t.yMax);
   if(key == s.key)
   {
      return;
   }
   s.key = key;
   var cx = Math.round((b.xMin + b.xMax) / 2);
   var u = ptrBounds(sb.upArrow_btn);
   var d = ptrBounds(sb.downArrow_btn);
   s.clear();
   s.lineStyle(1,PTR_RIM,30);
   s.beginFill(0x0A1440,55);
   ptrRoundRect(s,cx - 6,b.yMin,12,b.yMax - b.yMin,6);
   s.endFill();
   // Chevrons where the stock arrows are.
   s.lineStyle(3,PTR_SOFT,80);
   var uy = (u.yMin + u.yMax) / 2;
   var dy = (d.yMin + d.yMax) / 2;
   s.moveTo(cx - 6,uy + 3);
   s.lineTo(cx,uy - 3);
   s.lineTo(cx + 6,uy + 3);
   s.moveTo(cx - 6,dy - 3);
   s.lineTo(cx,dy + 3);
   s.lineTo(cx + 6,dy - 3);
   if(t.yMax > t.yMin)
   {
      s.lineStyle(1,0xFFF3C4,80);
      ptrGoldFill(s,cx - 5,t.yMin,10,t.yMax - t.yMin);
      ptrRoundRect(s,cx - 5,t.yMin + 1,10,Math.max(10,t.yMax - t.yMin - 2),5);
      s.endFill();
   }
}

// A "Ready" pill over an accepted offer, fading in and out with the stock ring (PlayerAccept / OtherAccept).
function ptrSyncReady(view, pill)
{
   var a = view.accepted;
   pill._visible = a._visible && a._alpha > 0;
   if(!pill._visible)
   {
      return;
   }
   pill._alpha = a._alpha;
   if(pill.drawn != true)
   {
      pill.drawn = true;
      var b = ptrBounds(a);
      pill._x = (b.xMin + b.xMax) / 2;
      pill._y = b.yMax - 4;
   }
}

function ptrMakeReady(name)
{
   var p = ptrTop.createEmptyMovieClip(name,ptrTop.getNextHighestDepth());
   p.lineStyle(2,0xFFF3C4,100);
   ptrGoldFill(p,-50,-15,100,30);
   ptrRoundRect(p,-50,-15,100,30,15);
   p.endFill();
   p.lineStyle(3.5,PTR_INK,100);
   p.moveTo(-36,0);
   p.lineTo(-31,5);
   p.lineTo(-23,-5);
   ptrText(p,"label_txt","Ready",-22,-12,64,24,16,PTR_INK);
   p.filters = [new flash.filters.GlowFilter(PTR_GOLD,0.7,14,14,2,2)];
   p._visible = false;
   return p;
}

function ptrSync()
{
   var views = [plyr1.pInventory,plyr1.pTrade,plyr2];
   var i;
   for(i = 0; i < views.length; i++)
   {
      ptrSyncToggle(views[i],ptrRoot["toggle" + i]);
      ptrSyncScrollBar(views[i].scrollbar,ptrRoot["scroll" + i]);
   }
   ptrSyncReady(plyr1.pTrade,ptrTop.mine);
   ptrSyncReady(plyr2,ptrTop.theirs);
}

function ptrBuild()
{
   plyr1.pInventory._x += PTR_INVENTORY_SHIFT;
   // Under everything (no mouse handlers): the frame, bays, coin panels, toggles and scrollbars.
   ptrRoot = _root.createEmptyMovieClip("sanctuaryTrade",-16384);
   var bg = ptrRoot.createEmptyMovieClip("bg",1);
   ptrDrawFrame(bg);
   ptrDrawCoins(bg);
   ptrDrawPortrait(bg);
   var i;
   for(i = 0; i < 3; i++)
   {
      ptrRoot.createEmptyMovieClip("toggle" + i,10 + i);
      ptrRoot.createEmptyMovieClip("scroll" + i,20 + i);
   }
   // Over the offers (no mouse handlers either), below the quantity arrows the stock attaches at depth 0.
   ptrTop = _root.createEmptyMovieClip("sanctuaryTradeTop",-100);
   ptrTop.mine = ptrMakeReady("mine");
   ptrTop.theirs = ptrMakeReady("theirs");
   ptrHideChrome();
   ptrStyleText();
   ptrRoot.onEnterFrame = ptrSync;
   ptrSync();
}

ptrPatchCards();
ptrPatchQuantity();
ptrPatchRows();
ptrBuild();
// ===== End Sanctuary Player Trade =====

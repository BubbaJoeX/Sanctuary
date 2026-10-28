// ===== Sanctuary Merchant Store =====
// Appended to NPCMerchantV2's main timeline by build_client_mods.py.
// Redraws the NPC merchant window (wndNpcMerchantV2; the old wndNpcMerchant's Lua opens this one too) in the Main
// Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass frame with the coin crest in a gold-rimmed
// frosted socket over the merchant's name in gold, a gold close button, Buy / Sell / Buy Back as a tab strip (the open
// one a gold pill) over navy dropdowns, and the wares as glass cards: the art in a frosted socket, the name in gold
// and the price on a coin chip. Items the merchant won't take back (the item's NoSale flag, which the stock card
// showed as a maroon card) carry a "No resale" ribbon ("Can't sell" in the Sell tab, dimmed); items you can't
// afford or that aren't for sale are dimmed with a lock chip. The detail panel shows the art in a glowing frosted
// socket over one price (membership no longer exists: the server charges everyone the member price, which is the
// one shown), a - n + quantity stepper, the total in gold and a big gold Buy pill, with the coin wallet under it.
// Everything is drawn under or over the stock clips, which keep their roles (hit areas, Lua callbacks, tooltips,
// the preview); every fscommand stays the stock one. The coin is a client texture (TEXTURE_SYMBOLS).

// The frame (the stage is 840 x 720).
var MS_FRAME_X = 6;
var MS_FRAME_Y = 30;
var MS_FRAME_W = 828;
var MS_FRAME_H = 684;
var MS_CREST_X = 420;
var MS_CREST_Y = 32;
var MS_CLOSE_X = 798;
var MS_CLOSE_Y = 64;
// The tab strip (the stock buttons are 148 wide) and the dropdown row under it.
var MS_TAB_X = [40,192,344];
var MS_TAB_Y = 112;
var MS_TAB_W = 148;
var MS_TAB_H = 34;
var MS_TAB_MODES = ["CoinStore","PlayerSell","BuyBack"];
var MS_TAB_LABELS = ["Buy","Sell","Buy Back"];
var MS_COMBO_Y = 152;
// The card list: its recess, the list's top left, the row pitch (stock 160: the cards had no gap), the second
// card's x (stock 230) and the list's height (stock 480). VertList shows (height / pitch) + 1 rows and only adds its
// scrollbar past three rows, so the height stays three whole rows.
var MS_RECESS = [36,194,516,508];
var MS_LIST_X = 48;
var MS_LIST_Y = 202;
var MS_ROW_H = 164;
var MS_CARD2_X = 233;
var MS_LIST_H = 3 * MS_ROW_H;
// A card: the stock art is 228 x 159 (its gold bar is a plain shape, so the card is drawn opaque over all of it).
var MS_CARD_W = 228;
var MS_CARD_H = 159;
var MS_ICON_X = 52;
var MS_ICON_Y = 52;
var MS_ICON_SCALE = 118;
var MS_ICON_HOVER = 128;
// The detail panel, in sidePanel's coordinates (centered on x 0; sidePanel is drawn at 96%).
var MS_SIDE_Y = 108;
var MS_SIDE_SCALE = 96;
var MS_SIDE_W = 276;
var MS_SIDE_H = 616;
var MS_ART_Y = 84;
var MS_PRICE_Y = 310;
var MS_WALLET_Y = 552;
var MS_GOLD = 0xFFC531;
var MS_RIM = 0x9DB8FF;
var MS_SOFT = 0xC9D6FF;
var MS_INK = 0x3A2400;

// --- drawing helpers -------------------------------------------------------------------

function msRoundRect(mc, x, y, w, h, r)
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

function msGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   msRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

// The gold of the close button, the selected tab and the Buy pill.
function msGoldRect(mc, x, y, w, h, r, over)
{
   mc.lineStyle(over ? 2 : 1.5,0xFFF3C4,over ? 100 : 90);
   mc.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   msRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   // A glassy shine across the top half.
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h / 2,r:Math.PI / 2});
   msRoundRect(mc,x + 3,y + 2,w - 6,h / 2,Math.min(r,h / 4));
   mc.endFill();
}

// The frosted plate behind item art: the stock icons are made for light plates.
function msFrosted(mc, x, y, w, h, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - w * 0.2,y:y - h * 0.25,w:w * 1.4,h:h * 1.4,r:0});
   msRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function msCircle(mc, x, y, r)
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

function msText(mc, name, txt, x, y, w, h, size, color, align)
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

function msSparkle(mc, x, y, r, alpha)
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
function msSparkles(mc, seed, count, x, y, w, h)
{
   var s = seed;
   var i;
   for(i = 0; i < count; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + s % w;
      s = (s * 1103515245 + 12345) % 2147483648;
      msSparkle(mc,sx,y + s % h,1 + s % 3,25 + s % 50);
   }
}

function msOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function msShadow()
{
   return [new flash.filters.DropShadowFilter(2,90,0x0A1030,0.9,3,3,1,1)];
}

// A thin gold rule that fades out at both ends.
function msGoldRule(mc, x, y, w)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[MS_GOLD,MS_GOLD,MS_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:x,y:y,w:w,h:2,r:0});
   msRoundRect(mc,x,y,w,2,1);
   mc.endFill();
}

// A padlock glyph centered on (x, y).
function msPadlock(mc, x, y, color)
{
   mc.lineStyle(2,color,100);
   mc.moveTo(x - 3.5,y - 1);
   mc.lineTo(x - 3.5,y - 3.5);
   mc.curveTo(x - 3.5,y - 7,x,y - 7);
   mc.curveTo(x + 3.5,y - 7,x + 3.5,y - 3.5);
   mc.lineTo(x + 3.5,y - 1);
   mc.lineStyle();
   mc.beginFill(color,100);
   msRoundRect(mc,x - 5.5,y - 1.5,11,8.5,2);
   mc.endFill();
}

// A round navy chip rimmed in gold with a padlock: the card's "can't buy this" marker.
function msLockChip(mc, x, y, r)
{
   mc.lineStyle(1.5,MS_GOLD,100);
   mc.beginGradientFill("linear",[0x2B4FA8,0x13215A],[100,100],[0,255],{matrixType:"box",x:x - r,y:y - r,w:r * 2,h:r * 2,r:Math.PI / 2});
   msCircle(mc,x,y,r);
   mc.endFill();
   msPadlock(mc,x,y + 1,0xFFE08A);
}

// Sets a text field's size, color and alignment (keeping its font), for the text it has and anything set later.
function msFormat(t, size, color, align)
{
   if(t == undefined)
   {
      return;
   }
   var f = new TextFormat();
   if(size != undefined)
   {
      f.size = size;
   }
   if(color != undefined)
   {
      f.color = color;
   }
   if(align != undefined)
   {
      f.align = align;
   }
   t.setTextFormat(f);
   var n = t.getNewTextFormat();
   if(size != undefined)
   {
      n.size = size;
   }
   if(color != undefined)
   {
      n.color = color;
   }
   if(align != undefined)
   {
      n.align = align;
   }
   t.setNewTextFormat(n);
}

function msPlace(t, x, y, w, h)
{
   t.autoSize = "none";
   t._x = x;
   t._y = y;
   t._width = w;
   t._height = h;
}

// Hides a clip's unnamed children (stock art placed without an instance name), found by enumeration and,
// in case that misses them, by their timeline depths.
function msHideUnnamed(mc, depths)
{
   var name;
   for(name in mc)
   {
      var o = mc[name];
      if((typeof o == "movieclip" || o instanceof TextField) && o._parent == mc && name.substr(0,8) == "instance")
      {
         o._visible = false;
      }
   }
   var i;
   for(i = 0; i < depths.length; i++)
   {
      var d = mc.getInstanceAtDepth(depths[i] - 16384);
      if(d != undefined && d._name.substr(0,8) == "instance")
      {
         d._visible = false;
      }
   }
}

// 999966151 -> "999,966,151".
function msThousands(value)
{
   var s = String(value);
   if(s.length == 0 || isNaN(Number(s)) || s.indexOf(".") >= 0 || s.indexOf(",") >= 0)
   {
      return s;
   }
   var sign = "";
   if(s.charAt(0) == "-")
   {
      sign = "-";
      s = s.substr(1);
   }
   var out = "";
   while(s.length > 3)
   {
      out = "," + s.substr(s.length - 3) + out;
      s = s.substr(0,s.length - 3);
   }
   return sign + s + out;
}

// Fits text into a fixed field, cutting it at a word with "..." when it runs over.
function msClipText(t, s)
{
   s = s == undefined ? "" : String(s);
   if(s.length > 160)
   {
      s = s.substr(0,160);
   }
   t.text = s;
   var cut = s;
   while(t.textHeight > t._height - 4 && cut.length > 0)
   {
      var space = cut.lastIndexOf(" ");
      cut = space > 0 ? cut.substr(0,space) : cut.substr(0,cut.length - 1);
      t.text = cut + "...";
   }
}

// Centers a clip on (x, y) in its parent's coordinates.
function msCenterAt(mc, x, y)
{
   var b = mc.getBounds(mc._parent);
   mc._x += x - (b.xMin + b.xMax) / 2;
   mc._y += y - (b.yMin + b.yMax) / 2;
}

// Scales and moves a stock button (a plain Button, art from its top left; flipped ones stay flipped) to a rectangle.
function msFitButton(b, x, y, w, h)
{
   var flip = b._yscale < 0;
   b._xscale = 100;
   b._yscale = flip ? -100 : 100;
   var bw = b._width;
   var bh = b._height;
   if(bw > 0 && bh > 0)
   {
      b._xscale = w / bw * 100;
      b._yscale = (flip ? -1 : 1) * h / bh * 100;
   }
   b._x = x;
   b._y = flip ? y + h : y;
}

// --- frame -----------------------------------------------------------------------------

var msFrame;
var msHits;
var msPanel;
var msTabOver = -1;
var msCloseOver = false;

function msPanelRect(mc, x, y, w, h)
{
   msGradientRect(mc,x,y,w,h,22,0x0A1440,0x0A1440,40,MS_RIM,25,1);
}

function msDrawFrame()
{
   var bg = msFrame.bg;
   bg.clear();
   msGradientRect(bg,MS_FRAME_X,MS_FRAME_Y,MS_FRAME_W,MS_FRAME_H,30,0x2E4FB0,0x141F55,100,MS_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-200,y:-340,w:880,h:720,r:0});
   msRoundRect(bg,MS_FRAME_X + 2,MS_FRAME_Y + 2,MS_FRAME_W - 4,MS_FRAME_H - 4,28);
   bg.endFill();

   // The header band, with a few sparkles either side of the crest.
   bg.beginFill(0x0A1440,30);
   msRoundRect(bg,MS_FRAME_X + 8,MS_FRAME_Y + 6,MS_FRAME_W - 16,70,24);
   bg.endFill();
   msSparkles(bg,4141,16,240,40,130,58);
   msSparkles(bg,7171,16,470,40,130,58);
   msGoldRule(bg,MS_CREST_X - 90,104,180);

   // The cards' recess.
   msPanelRect(bg,MS_RECESS[0],MS_RECESS[1],MS_RECESS[2],MS_RECESS[3]);
}

// The coin crest: a frosted socket rimmed in gold, half over the frame's top edge.
function msDrawCrest()
{
   var c = msFrame.crest;
   c.clear();
   c.lineStyle();
   c.beginGradientFill("radial",[0xFFE08A,0xFFE08A],[55,0],[0,255],{matrixType:"box",x:-58,y:-58,w:116,h:116,r:0});
   msCircle(c,0,0,58);
   c.endFill();
   c.lineStyle(3,MS_GOLD,100);
   c.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:-40,y:-44,w:84,h:84,r:0});
   msCircle(c,0,0,31);
   c.endFill();
   c.lineStyle(1.5,0xFFF3C4,90);
   msCircle(c,0,0,35);
   msSparkle(c,-30,-24,2,80);
   msSparkle(c,33,18,1.5,70);
   c.filters = [new flash.filters.GlowFilter(MS_GOLD,0.5,14,14,2,2),new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
}

// The gold round close button, under the stock one (an invisible hit area).
function msDrawClose(over)
{
   var c = msFrame.close;
   c.clear();
   var r = 17;
   c.lineStyle(over ? 2 : 1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   msCircle(c,0,0,r);
   c.endFill();
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r,r:Math.PI / 2});
   msRoundRect(c,-11,-15,22,12,6);
   c.endFill();
   c.lineStyle(4,MS_INK,100);
   c.moveTo(-6,-6);
   c.lineTo(6,6);
   c.moveTo(6,-6);
   c.lineTo(-6,6);
   c.filters = over ? [new flash.filters.GlowFilter(MS_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
}

// FRIconButton broadcasts its hover to listeners; its click still goes to the stock onClick (Exit()).
var msCloseListener = new Object();
msCloseListener.onHoverOver = function()
{
   msDrawClose(true);
   gs.TweenLite.to(msFrame.close,0.5,{_xscale:114,_yscale:114,ease:mx.transitions.easing.Elastic.easeOut});
};
msCloseListener.onHoverOut = function()
{
   msDrawClose(false);
   gs.TweenLite.to(msFrame.close,0.3,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
};

// Buy / Sell / Buy Back: a navy track with the open tab as a gold pill. The stock tabs (gfx Buttons, text only) are
// invisible hit areas over the pills; the pills' padding around their text answers clicks the same way.
function msDrawTabs()
{
   var t = msFrame.tabs;
   t.clear();
   var shown = buyButton._visible != false;
   var i;
   for(i = 0; i < 3; i++)
   {
      msFrame["tab" + i]._visible = shown;
   }
   msHits._visible = shown && msFrame._visible;
   if(!shown)
   {
      return;
   }
   msGradientRect(t,MS_TAB_X[0] - 4,MS_TAB_Y - 4,MS_TAB_X[2] + MS_TAB_W - MS_TAB_X[0] + 8,MS_TAB_H + 8,(MS_TAB_H + 8) / 2,0x0A1440,0x0A1440,55,MS_RIM,30,1);
   for(i = 0; i < 3; i++)
   {
      var x = MS_TAB_X[i];
      var selected = _root.m_windowMode == MS_TAB_MODES[i];
      var over = msTabOver == i;
      var label = msFrame["tab" + i];
      if(selected)
      {
         msGoldRect(t,x,MS_TAB_Y,MS_TAB_W,MS_TAB_H,MS_TAB_H / 2,over);
         label.textColor = MS_INK;
         label.filters = [];
      }
      else
      {
         if(over)
         {
            msGradientRect(t,x,MS_TAB_Y,MS_TAB_W,MS_TAB_H,MS_TAB_H / 2,0x3F66D6,0x1D307A,100,0xFFE08A,90,1.5);
         }
         label.textColor = over ? 0xFFE08A : 0xFFFFFF;
         label.filters = msShadow();
      }
   }
   t.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.4,6,6,1,2)];
}

function msTabIndex(button)
{
   if(button == buyButton)
   {
      return 0;
   }
   if(button == sellButton)
   {
      return 1;
   }
   if(button == buyBackButton)
   {
      return 2;
   }
   return -1;
}

// The pill under the mouse: a pill is two hit areas (the stock tab over its text, ours around it), and either one's
// roll out can come after the other's roll over.
function msTabUnderMouse()
{
   var x = _root._xmouse;
   var y = _root._ymouse;
   var i;
   for(i = 0; i < 3; i++)
   {
      if(x >= MS_TAB_X[i] && x <= MS_TAB_X[i] + MS_TAB_W && y >= MS_TAB_Y && y <= MS_TAB_Y + MS_TAB_H)
      {
         return i;
      }
   }
   return -1;
}

// Listeners on the stock tabs (gfx.events: scope _root, callback by name), next to the stock ones.
function msTabRollOver(event)
{
   msTabOver = msTabIndex(event.target);
   msDrawTabs();
}

function msTabRollOut(event)
{
   msTabOver = msTabUnderMouse();
   msDrawTabs();
}

// The pills' padding (the stock tabs only take clicks on their text): clips above the stock blocker and below the
// tabs, so the stock tabs keep every click on them. A click runs the stock tab's own handler.
function msBuildTabHits()
{
   var handlers = ["_handleBuyButton","_handleSellButton","_handleBuyBackButton"];
   var i;
   for(i = 0; i < 3; i++)
   {
      var h = msHits.createEmptyMovieClip("tab" + i,i + 1);
      h.beginFill(0,0);
      msRoundRect(h,MS_TAB_X[i],MS_TAB_Y,MS_TAB_W,MS_TAB_H,MS_TAB_H / 2);
      h.endFill();
      h.index = i;
      h.handler = handlers[i];
      h.onRollOver = function()
      {
         _root[this.handler + "RollOver"]();
         msTabOver = this.index;
         msDrawTabs();
      };
      h.onRollOut = h.onReleaseOutside = function()
      {
         msTabOver = msTabUnderMouse();
         msDrawTabs();
      };
      h.onRelease = function()
      {
         _root[this.handler + "Click"]();
      };
   }
}

// The traveling merchant's timer (SetMerchantTimer): a navy chip on the header's left with a little clock.
function msDrawTimer()
{
   var c = msFrame.timer;
   c.clear();
   if(txtTime._visible == false)
   {
      return;
   }
   msGradientRect(c,28,50,204,32,16,0x1B2F78,0x13215A,100,MS_GOLD,90,1.5);
   c.lineStyle(2,0xFFE08A,100);
   msCircle(c,46,66,8);
   c.moveTo(46,66);
   c.lineTo(46,61);
   c.moveTo(46,66);
   c.lineTo(50,68);
}

// "24 items" on the dropdown row's left, when the category dropdown (Sell only) isn't there.
function msUpdateCount()
{
   var t = msFrame.count_txt;
   var n = Number(_root.m_totalItems);
   if(catSorts._visible || isNaN(n) || buyButton._visible == false)
   {
      t.text = "";
      return;
   }
   var what = n == 1 ? "1 item" : msThousands(n) + " items";
   if(_root.m_windowMode == "PlayerSell")
   {
      t.text = what + " you can sell";
   }
   else if(_root.m_windowMode == "BuyBack")
   {
      t.text = what + " to buy back";
   }
   else
   {
      t.text = what + " for sale";
   }
}

function msBuildFrame()
{
   msFrame = _root.createEmptyMovieClip("sanctuaryMerchant",-16384);
   msFrame.bg = msFrame.createEmptyMovieClip("bg",1);
   msFrame.bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   msFrame.tabs = msFrame.createEmptyMovieClip("tabs",2);
   msFrame.timer = msFrame.createEmptyMovieClip("timer",3);
   msFrame.close = msFrame.createEmptyMovieClip("close",4);
   msFrame.crest = msFrame.createEmptyMovieClip("crest",5);
   msFrame.crest._x = MS_CREST_X;
   msFrame.crest._y = MS_CREST_Y;
   var coin = msFrame.crest.attachMovie("sanctuaryMerchantCoin","coin",1);
   coin._width = coin._height = 50;
   coin._x = coin._y = -25;
   msDrawFrame();
   msDrawCrest();

   // The stock frame, the coin/SC logo (replaced by the crest), the tab bar's backing and the list's stray backing.
   // Preview mode toggles their _visible, so they're hidden with _alpha.
   panelBackground_mc._alpha = 0;
   header_icon._alpha = 0;
   header_bar_mc._alpha = 0;
   list_background._alpha = 0;

   // The title: the stock field (setTitle_lua writes "<merchant> Store"), bigger and in gold.
   msPlace(headerText,MS_CREST_X - 210,64,420,40);
   headerText.multiline = false;
   headerText.wordWrap = false;
   msFormat(headerText,28,0xFFE08A,"center");
   headerText.filters = msOutline();

   // The close button: the stock one stays as an invisible hit area over the gold circle.
   exitButton_mc._alpha = 0;
   exitButton_mc._x = MS_CLOSE_X - exitButton_mc._width / 2;
   exitButton_mc._y = MS_CLOSE_Y - exitButton_mc._height / 2;
   exitButton_mc.addListener(msCloseListener);
   msFrame.close._x = MS_CLOSE_X;
   msFrame.close._y = MS_CLOSE_Y;
   msDrawClose(false);

   // The tabs: the stock buttons over the pills (their text field is the hit area), and labels of our own.
   var buttons = [buyButton,sellButton,buyBackButton];
   var i;
   for(i = 0; i < 3; i++)
   {
      var b = buttons[i];
      b._alpha = 0;
      // Its text field sits 8px in and 6px down.
      b._x = MS_TAB_X[i] - 7;
      b._y = MS_TAB_Y - 1;
      b.addEventListener("rollOver",_root,"msTabRollOver");
      b.addEventListener("rollOut",_root,"msTabRollOut");
      b.addEventListener("releaseOutside",_root,"msTabRollOut");
      msText(msFrame,"tab" + i,MS_TAB_LABELS[i],MS_TAB_X[i],MS_TAB_Y + 6,MS_TAB_W,26,17,0xFFFFFF);
   }
   msHits = _root.createEmptyMovieClip("sanctuaryMerchantHits",-16300);
   msBuildTabHits();

   // The timer chip's text: the stock fields.
   msPlace(txtTimeLeftLabel,56,58,80,20);
   msFormat(txtTimeLeftLabel,13,MS_SOFT,"right");
   msPlace(txtTime,142,54,86,26);
   msFormat(txtTime,17,0xFFE08A,"left");
   txtTime.filters = txtTimeLeftLabel.filters = msShadow();

   // The dropdowns, the item count beside them and the empty-list message.
   cbSorts._x = 294;
   cbSorts._y = MS_COMBO_Y;
   catSorts._x = 34;
   catSorts._y = MS_COMBO_Y;
   msStyleCombo(cbSorts);
   msStyleCombo(catSorts);
   msFrame.count_txt = msText(msFrame,"count_txt","",44,MS_COMBO_Y + 9,236,24,15,MS_SOFT,"left");
   msFrame.count_txt.filters = msShadow();
   msFormat(txtNoItems,17,MS_SOFT,"center");
   txtNoItems.filters = msOutline();

   // Redraws what follows stock state Lua and the stock code change without telling us.
   msFrame.onEnterFrame = function()
   {
      var key = _root.m_windowMode + "," + buyButton._visible + "," + txtTime._visible + "," + catSorts._visible + "," + _root.m_totalItems;
      if(key != this.key)
      {
         this.key = key;
         msDrawTabs();
         msDrawTimer();
         msUpdateCount();
      }
   };
}

// --- dropdowns -------------------------------------------------------------------------

// SnsComboBox: the collapsed box becomes a navy pill with a gold chevron, the list a navy panel. Its art stays
// (invisible) as the hit area; the skins follow its size and open state.
function msDrawCombo(s)
{
   var cb = s.cb;
   var box = cb.main.mcCollapsed;
   var w = box._width;
   var h = Math.max(box._height - 4,30);
   var y = box._y + 2;
   var open = cb.boolIsOpen == true;
   s.clear();
   msGradientRect(s,0,y,w,h,h / 2,0x1B2F78,0x13215A,100,open ? MS_GOLD : MS_RIM,open ? 100 : 55,1.5);
   s.lineStyle();
   s.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[14,0],[0,255],{matrixType:"box",x:0,y:y,w:w,h:h / 2,r:Math.PI / 2});
   msRoundRect(s,4,y + 2,w - 8,h / 2,h / 4);
   s.endFill();
   var cx = w - 20;
   var cy = y + h / 2;
   var d = open ? -1 : 1;
   s.lineStyle(3,0xFFE08A,100);
   s.moveTo(cx - 6,cy - 3 * d);
   s.lineTo(cx,cy + 3 * d);
   s.lineTo(cx + 6,cy - 3 * d);

   var list = cb.mcDropDownBg;
   var bg = list.bg;
   var p = list.msSkin;
   p.clear();
   msGradientRect(p,bg._x,bg._y,bg._width,bg._height,12,0x1B2F78,0x101B4E,97,MS_GOLD,80,1.5);
}

function msStyleCombo(cb)
{
   if(cb == undefined || cb.main == undefined)
   {
      return;
   }
   cb.main.mcCollapsed._alpha = 0;
   cb.main.arrow._alpha = 0;
   cb.mcDropDownBg.bg._alpha = 0;
   cb.mcDropDownBg.msSkin = cb.mcDropDownBg.createEmptyMovieClip("msSkin",-16384);
   cb.main.txtValue.textColor = 0xFFFFFF;
   cb.main.txtValue.filters = msShadow();
   var s = cb.main.createEmptyMovieClip("msSkin",-16384);
   s.cb = cb;
   s.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.4,6,6,1,2)];
   s.onEnterFrame = function()
   {
      var box = this.cb.main.mcCollapsed;
      var bg = this.cb.mcDropDownBg.bg;
      var key = box._width + "," + box._height + "," + this.cb.boolIsOpen + "," + bg._x + "," + bg._y + "," + bg._width + "," + bg._height;
      if(key != this.key)
      {
         this.key = key;
         msDrawCombo(this);
      }
   };
}

// The dropdown rows: white, gold on hover (the stock hover was a pale blue).
function msPatchCombo()
{
   var proto = SnsComboBox.prototype;
   var setupItems = proto._setupItems;
   proto._setupItems = function()
   {
      var result = setupItems.apply(this,arguments);
      var name;
      for(name in this.mcTextHolder)
      {
         var row = this.mcTextHolder[name];
         if(typeof row == "movieclip" && row.txtLabel != undefined)
         {
            row.txtLabel.textColor = 0xFFFFFF;
            row.onRollOver = function()
            {
               this.txtLabel.textColor = 0xFFE08A;
            };
            row.onRollOut = function()
            {
               this.txtLabel.textColor = 0xFFFFFF;
            };
         }
      }
      return result;
   };
}

// --- cards -----------------------------------------------------------------------------

// 0 normal, 1 can't afford, 2 not for sale (the merchant's CanBuy), 3 can't be sold (Sell tab, NoSale).
function msCardState(it)
{
   var d = it.m_data;
   var mode = _root.m_windowMode;
   if(mode == "CoinStore")
   {
      if(d.canBuy != undefined && Number(d.canBuy) != 1)
      {
         return 2;
      }
      var price = Number(_root.m_member ? d.memberPrice : d.price);
      var coins = Number(_root.m_coinAmount);
      if(!isNaN(price) && !isNaN(coins) && price > coins)
      {
         return 1;
      }
   }
   else if(mode == "PlayerSell" && Number(d.isSale) == 1)
   {
      return 3;
   }
   return 0;
}

function msDrawCard(it)
{
   var c = it.msCard;
   if(c == undefined)
   {
      return;
   }
   var over = it.msOver == true;
   var selected = it.msSelected == true;
   var state = msCardState(it);
   var dim = state != 0;
   c.clear();
   // Opaque over the whole stock footprint, its gold bar included.
   if(over)
   {
      msGradientRect(c,-1,-1,MS_CARD_W + 2,MS_CARD_H + 2,12,0x4069DA,0x1E3486,100,selected ? MS_GOLD : 0xFFE08A,100,selected ? 3 : 2);
   }
   else if(selected)
   {
      msGradientRect(c,-1,-1,MS_CARD_W + 2,MS_CARD_H + 2,12,0x3159BC,0x1A2D74,100,MS_GOLD,100,3);
   }
   else
   {
      msGradientRect(c,-1,-1,MS_CARD_W + 2,MS_CARD_H + 2,12,0x2B4FA8,0x17286A,100,MS_RIM,40,1.5);
   }
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[16,0],[0,255],{matrixType:"box",x:0,y:2,w:10,h:56,r:Math.PI / 2});
   msRoundRect(c,4,2,MS_CARD_W - 8,56,12);
   c.endFill();
   // The footer under a faint rule: the description (or what you own, in the Sell and Buy Back tabs) and the
   // colour and gender badges.
   c.beginFill(MS_RIM,22);
   msRoundRect(c,14,106,MS_CARD_W - 28,1.5,0.5);
   c.endFill();
   msFrosted(c,10,10,84,84,16,selected ? MS_GOLD : 0xFFFFFF,selected ? 100 : 85,2);

   // The price on a coin chip: navy rimmed in gold, the amount in white like the window's other text.
   var p = it.txtPrice;
   if(p._visible != false && p.text.length > 0)
   {
      var w = Math.min(Math.round(p.textWidth) + 46,MS_CARD_W - 106);
      msGradientRect(c,102,65,w,28,14,0x1B2F78,0x101B4E,100,MS_GOLD,100,1.5);
   }

   // Little glass discs behind the colour and gender badges.
   var badges = [it.palate_icon_mc,it.mcMaleIcon,it.mcFemaleIcon];
   var i;
   for(i = 0; i < badges.length; i++)
   {
      var b = badges[i];
      if(b._visible)
      {
         var bb = b.getBounds(it);
         c.lineStyle(1,MS_RIM,50);
         c.beginFill(0x13215A,85);
         msCircle(c,(bb.xMin + bb.xMax) / 2,(bb.yMin + bb.yMax) / 2,13);
         c.endFill();
      }
   }

   if(over)
   {
      c.filters = [new flash.filters.GlowFilter(MS_GOLD,0.55,14,14,2,2)];
   }
   else if(selected)
   {
      c.filters = [new flash.filters.GlowFilter(MS_GOLD,0.4,12,12,2,2)];
   }
   else
   {
      c.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
   }

   // Dimmed with a lock chip when it can't be bought (or sold); a veil over everything, no mouse handlers.
   var v = it.msVeil;
   v.clear();
   if(dim)
   {
      v.lineStyle();
      v.beginFill(0x0A1030,over ? 28 : 42);
      msRoundRect(v,-1,-1,MS_CARD_W + 2,MS_CARD_H + 2,12);
      v.endFill();
      msLockChip(v,88,16,12);
   }

   // The stock maroon card meant the item's NoSale flag: the merchant won't take it back. Now a plum ribbon.
   var tag = it.msTag;
   var noSale = Number(it.m_data.isSale) == 1;
   tag._visible = noSale;
   if(noSale)
   {
      tag.label_txt.text = _root.m_windowMode == "PlayerSell" ? "Can't sell" : "No resale";
   }
}

function msStyleCard(it)
{
   if(it.msCard != undefined)
   {
      return;
   }
   // The icon moves above the card (the stock art up to the gold bar's depth stays under it) and is rounded off.
   it.icon.swapDepths(100);
   it.msCard = it.createEmptyMovieClip("msCard",-16374);
   it.icon._x = MS_ICON_X;
   it.icon._y = MS_ICON_Y;
   it.icon._xscale = it.icon._yscale = MS_ICON_SCALE;
   var mask = it.createEmptyMovieClip("msIconMask",101);
   mask.beginFill(0,100);
   msRoundRect(mask,12,12,80,80,14);
   mask.endFill();
   it.icon.setMask(mask);

   // Membership no longer exists: no Members Only badge.
   it.mcMembersOnly._alpha = 0;

   msPlace(it.label,102,8,118,54);
   msFormat(it.label,14,0xFFE08A,"left");
   it.label.filters = msShadow();
   msPlace(it.txtPrice,130,68,90,24);
   msFormat(it.txtPrice,15,0xFFFFFF,"left");
   it.txtPrice.filters = msOutline();
   it.currency_type._x = 116;
   it.currency_type._y = 79;
   it.msDesc = msText(it,"msDesc","",12,112,204,40,12,MS_SOFT,"left");
   it.msDesc.multiline = it.msDesc.wordWrap = true;
   it.msDesc.filters = msShadow();
   msPlace(it.txtCustomizable,14,122,110,22);
   msFormat(it.txtCustomizable,13,MS_SOFT,"left");
   msPlace(it.txtInventoryQuantity,100,119,60,24);
   msFormat(it.txtInventoryQuantity,16,0xFFFFFF,"left");
   it.txtInventoryQuantity.filters = msOutline();
   it.palate_icon_mc._x = 168;
   it.palate_icon_mc._y = 121;
   msCenterAt(it.mcMaleIcon,202,132);
   msCenterAt(it.mcFemaleIcon,202,132);

   it.msVeil = it.createEmptyMovieClip("msVeil",200);
   var tag = it.msTag = it.createEmptyMovieClip("msTag",201);
   tag.lineStyle(1.5,0xF0B8FF,80);
   tag.beginGradientFill("linear",[0x7A3490,0x45185C],[100,100],[0,255],{matrixType:"box",x:16,y:84,w:72,h:20,r:Math.PI / 2});
   msRoundRect(tag,16,84,72,20,10);
   tag.endFill();
   tag.label_txt = msText(tag,"label_txt","No resale",16,85,72,18,11,0xFFFFFF);
   tag.label_txt.filters = msShadow();
   tag._visible = false;
}

function msPatchCards()
{
   var proto = PanelListItem.prototype;
   var drawItem = proto.drawItem;
   proto.drawItem = function()
   {
      msStyleCard(this);
      var result = drawItem.apply(this,arguments);
      this.label.textColor = 0xFFE08A;
      this.txtPrice.text = msThousands(this.txtPrice.text);
      this.txtPrice.textColor = 0xFFFFFF;
      this.mcMembersOnly._visible = false;
      // What you own (Sell) or sold (Buy Back), in the footer: "You have 5".
      this.txtCustomizable.text = _root.m_windowMode == "BuyBack" ? "Quantity" : "You have";
      this.txtInventoryQuantity._x = 14 + Math.round(this.txtCustomizable.textWidth) + 8;
      this.txtInventoryQuantity._y = 119;
      this.txtInventoryQuantity.textColor = 0xFFFFFF;
      // The colour badge sits left of the gender one, or in its place when there's none.
      var gender = this.mcMaleIcon._visible || this.mcFemaleIcon._visible;
      this.palate_icon_mc._x = gender ? 168 : 192;
      // The description fills the footer in the Buy tab, beside the badges.
      var badges = this.palate_icon_mc._visible ? (gender ? 2 : 1) : (gender ? 1 : 0);
      this.msDesc._visible = this.txtCustomizable._visible == false;
      this.msDesc._width = badges == 0 ? 204 : (badges == 1 ? 176 : 150);
      msClipText(this.msDesc,this.m_data.description);
      msDrawCard(this);
      return result;
   };
   var setSelected = proto.setSelected;
   proto.setSelected = function(s)
   {
      var result = setSelected.apply(this,arguments);
      this.msSelected = s;
      msDrawCard(this);
      return result;
   };
   var rollOver = proto.rollOver;
   proto.rollOver = function()
   {
      var result = rollOver.apply(this,arguments);
      this.msOver = true;
      msDrawCard(this);
      gs.TweenLite.to(this.icon,0.6,{_xscale:MS_ICON_HOVER,_yscale:MS_ICON_HOVER,ease:mx.transitions.easing.Elastic.easeOut});
      return result;
   };
   var rollOut = proto.rollOut;
   proto.rollOut = function()
   {
      var result = rollOut.apply(this,arguments);
      this.msOver = false;
      msDrawCard(this);
      gs.TweenLite.to(this.icon,0.2,{_xscale:MS_ICON_SCALE,_yscale:MS_ICON_SCALE});
      return result;
   };

   // Rows a little further apart, and the second card moved over, so the cards have gutters.
   var row = PanelRow.prototype;
   row.getItemHeight = function()
   {
      return MS_ROW_H;
   };
   var setIndex = row.setIndex;
   row.setIndex = function(index)
   {
      this.item2._x = MS_CARD2_X;
      return setIndex.apply(this,arguments);
   };

   // The list's scrollbar gets a skin, and a wheel step (the stock list never set one, so the wheel did nothing).
   var list = VertList.prototype;
   var init = list.init;
   list.init = function(w, h)
   {
      var result = init.call(this,w,MS_LIST_H);
      if(this.m_totalItems > this.getViewItemTotal() && this.m_scrollbar != undefined)
      {
         this.m_scrollbar.setItemHeight(40);
         msSkinScrollbar(this.m_scrollbar);
      }
      return result;
   };
}

// Redraws the cards on screen, e.g. when the coin count changes what's affordable.
function msRedrawCards()
{
   var items = m_list.m_itemArray;
   var i;
   for(i = 0; i < items.length; i++)
   {
      if(items[i].item1 != undefined)
      {
         msDrawCard(items[i].item1);
         msDrawCard(items[i].item2);
      }
   }
}

// --- scrollbar -------------------------------------------------------------------------

// FRScrollBarV2 keeps resetting its parts' alpha, so the skin is drawn over the stock scrollbar, which still takes
// the clicks: a navy capsule with chevrons where the arrows are and a gold thumb.
function msDrawScrollbar(s)
{
   var sb = s.sb;
   var t = sb.thumb_mc;
   var h = sb.background_mc._height;
   var w = Math.max(sb.background_mc._width,16);
   s.clear();
   if(!(h > 0))
   {
      return;
   }
   msGradientRect(s,0,0,w,h,w / 2,0x1B2F78,0x13215A,100,MS_RIM,40,1);
   s.lineStyle();
   s.beginFill(0x050A24,60);
   msRoundRect(s,w / 2 - 3,22,6,h - 44,3);
   s.endFill();
   s.lineStyle(2.5,MS_SOFT,80);
   s.moveTo(w / 2 - 5,13);
   s.lineTo(w / 2,8);
   s.lineTo(w / 2 + 5,13);
   s.moveTo(w / 2 - 5,h - 13);
   s.lineTo(w / 2,h - 8);
   s.lineTo(w / 2 + 5,h - 13);
   var enabled = t._alpha > 60;
   s.lineStyle(1,0xFFF3C4,enabled ? 90 : 40);
   s.beginGradientFill("linear",[0xFFE08A,0xF5A623],[enabled ? 100 : 45,enabled ? 100 : 45],[0,255],{matrixType:"box",x:w / 2 - 6,y:t._y,w:12,h:t._height,r:0});
   msRoundRect(s,w / 2 - 6,t._y,12,Math.max(t._height,14),6);
   s.endFill();
}

function msSkinScrollbar(sb)
{
   var s = sb.createEmptyMovieClip("sanctuarySkin",sb.getNextHighestDepth());
   s.sb = sb;
   s.onEnterFrame = function()
   {
      var t = this.sb.thumb_mc;
      var key = t._y + "," + t._height + "," + t._alpha + "," + this.sb.background_mc._height;
      if(key != this.key)
      {
         this.key = key;
         msDrawScrollbar(this);
      }
   };
}

// --- detail panel ----------------------------------------------------------------------

var msBuyOver = false;

// The labels follow the tab: buying, selling (what you get) or buying back.
function msPriceLabels()
{
   if(_root.m_windowMode == "PlayerSell")
   {
      return ["Sells for","You get","Sell"];
   }
   if(_root.m_windowMode == "BuyBack")
   {
      return ["Price","Total","Buy Back"];
   }
   return ["Price","Total","Buy"];
}

// The Buy pill: gold when the stock button is enabled, navy (and saying why) when not.
function msDrawBuy()
{
   var b = msPanel.buy;
   var enabled = sidePanel.pWidget.buy_mc.isEnabled() != false;
   var over = msBuyOver && enabled;
   var label = msPriceLabels()[2];
   b.bg.clear();
   if(enabled)
   {
      msGoldRect(b.bg,-126,-23,252,46,23,over);
   }
   else
   {
      msGradientRect(b.bg,-126,-23,252,46,23,0x2B4FA8,0x17286A,100,MS_GOLD,45,2);
      var item = sidePanel.curSelectedItem;
      if(_root.m_windowMode == "CoinStore" && item.iconId != 0 && item != undefined)
      {
         if(Number(item.canBuy) != 1)
         {
            label = "Not for sale";
         }
         else if(Number(sidePanel.m_coinWallet) < Number(sidePanel.m_totalPrice))
         {
            label = "Not enough coins";
         }
      }
   }
   b.label_txt.text = label;
   b.label_txt.textColor = enabled ? MS_INK : MS_SOFT;
   b.filters = over ? [new flash.filters.GlowFilter(MS_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,10,10,1,2)];
}

var msBuyListener = new Object();
msBuyListener.onHoverOver = function()
{
   msBuyOver = true;
   msDrawBuy();
   if(sidePanel.pWidget.buy_mc.isEnabled() != false)
   {
      gs.TweenLite.to(msPanel.buy,0.4,{_xscale:105,_yscale:105,ease:mx.transitions.easing.Elastic.easeOut});
   }
};
msBuyListener.onHoverOut = function()
{
   msBuyOver = false;
   msDrawBuy();
   gs.TweenLite.to(msPanel.buy,0.3,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
};

// The - n + stepper: chips over the stock arrows (which stay the hit areas) beside the stock count.
function msDrawStepper()
{
   var inc = sidePanel.mcItemIncrementer;
   var s = inc.msSkin;
   var enabled = inc.enabled != false;
   s.clear();
   msGradientRect(s,-1,-1,44,42,12,0x0A1440,0x13215A,100,MS_RIM,45,1.5);
   if(enabled)
   {
      msGoldRect(s,-32,5,30,30,15,false);
      msGoldRect(s,46,5,30,30,15,false);
   }
   else
   {
      msGradientRect(s,-32,5,30,30,15,0x2B4FA8,0x17286A,100,MS_GOLD,45,1.5);
      msGradientRect(s,46,5,30,30,15,0x2B4FA8,0x17286A,100,MS_GOLD,45,1.5);
   }
   var ink = enabled ? MS_INK : MS_SOFT;
   s.lineStyle(3,ink,100);
   s.moveTo(-22,20);
   s.lineTo(-12,20);
   s.moveTo(56,20);
   s.lineTo(66,20);
   s.moveTo(61,15);
   s.lineTo(61,25);
}

// A gold disc behind the preview (magnifier / close) button and a navy one behind the red lock.
function msDrawArtButtons()
{
   var a = msPanel.artButtons;
   a.clear();
   var sp = sidePanel;
   var btn = sp.btnShowPreview._visible ? sp.btnShowPreview : sp.btnClosePreview;
   if(btn._visible)
   {
      var on = btn.enabled != false && btn._alpha > 60;
      var bb = btn.getBounds(sp);
      a.lineStyle(1.5,0xFFF3C4,on ? 100 : 50);
      a.beginGradientFill("linear",[0xFFE08A,0xF5A623],[on ? 100 : 45,on ? 100 : 45],[0,255],{matrixType:"box",x:bb.xMin,y:bb.yMin,w:bb.xMax - bb.xMin,h:bb.yMax - bb.yMin,r:Math.PI / 2});
      msCircle(a,(bb.xMin + bb.xMax) / 2,(bb.yMin + bb.yMax) / 2,19);
      a.endFill();
   }
   if(sp.redlock._visible)
   {
      var lb = sp.redlock.getBounds(sp);
      a.lineStyle(1.5,MS_GOLD,100);
      a.beginGradientFill("linear",[0x2B4FA8,0x13215A],[100,100],[0,255],{matrixType:"box",x:lb.xMin,y:lb.yMin,w:lb.xMax - lb.xMin,h:lb.yMax - lb.yMin,r:Math.PI / 2});
      msCircle(a,(lb.xMin + lb.xMax) / 2,(lb.yMin + lb.yMax) / 2,20);
      a.endFill();
   }
}

// Lays the price block out after the stock code has written it: one price (the stock list price, member discount and
// strike-through are gone), the stepper, the total in gold and the Buy pill. Amounts get thousands separators.
function msRefreshPanel()
{
   var sp = sidePanel;
   var pw = sp.pWidget;
   var labels = msPriceLabels();

   pw.tfYourPriceLabel.text = labels[0];
   msFormat(pw.tfYourPriceLabel,15,MS_SOFT,"left");
   msPlace(pw.tfYourPriceLabel,14,48,110,22);
   pw.tfYourPriceValue.text = msThousands(pw.tfYourPriceValue.text);
   msFormat(pw.tfYourPriceValue,20,0xFFFFFF,"right");
   msPlace(pw.tfYourPriceValue,100,44,140,28);
   pw.sc_coin_icon._x = 240 - pw.tfYourPriceValue.textWidth - 18;
   pw.sc_coin_icon._y = 58;

   pw.tfTotalPriceLabel.text = labels[1];
   msFormat(pw.tfTotalPriceLabel,17,MS_SOFT,"left");
   msPlace(pw.tfTotalPriceLabel,14,148,110,24);
   pw.tfTotalPriceValue.text = msThousands(pw.tfTotalPriceValue.text);
   msFormat(pw.tfTotalPriceValue,24,0xFFE08A,"right");
   msPlace(pw.tfTotalPriceValue,90,142,150,32);
   pw.sc_coin_icon3._x = 240 - pw.tfTotalPriceValue.textWidth - 20;
   pw.sc_coin_icon3._y = 159;

   // Gone with membership: the list price, its strike-through and the member discount.
   pw.tfListPriceLabel._visible = pw.tfListPriceValue._visible = false;
   pw.tfDiscountLabel._visible = pw.tfDiscountValue._visible = pw.tfDiscountPercentValue._visible = false;
   pw.mcStrikeThrough._visible = pw.sc_coin_icon2._visible = false;
   pw.mcMembersOnlyButton._visible = false;
   pw.mcRentalSC._visible = false;

   sp.ItemName.textColor = 0xFFE08A;
   msPlace(sp.ItemName,-128,164,256,36);
   sp.ItemDescription.textColor = MS_SOFT;
   sp.ItemDescription._x = -124;
   sp.ItemDescription._y = 200;
   sp.ItemDescription._width = 248;

   msPanel.placeholder._visible = sp.mcNoItem._visible;
   msPanel.qty_txt._visible = sp.mcItemIncrementer._visible;
   msDrawStepper();
   msDrawArtButtons();
   msDrawBuy();
}

function msBuildPanel()
{
   var sp = sidePanel;
   sp._xscale = sp._yscale = MS_SIDE_SCALE;
   sp._y = MS_SIDE_Y;
   // The stock panel art (an unnamed clip) and the price widget's divider; the "x" before the stepper stays if it's
   // reachable (it reads as "times" beside the stepper anyway).
   msHideUnnamed(sp,[1]);
   msHideUnnamed(sp.pWidget,[2,19]);

   msPanel = sp.createEmptyMovieClip("sanctuaryPanel",-16384);
   var c = msPanel.createEmptyMovieClip("bg",1);
   var x = - MS_SIDE_W / 2;
   msGradientRect(c,x,0,MS_SIDE_W,MS_SIDE_H,24,0x0A1440,0x0A1440,40,MS_RIM,25,1);
   // A soft gold glow behind the art, with a few sparkles.
   c.lineStyle();
   c.beginGradientFill("radial",[0xFFE08A,0x8FB4FF],[40,0],[0,255],{matrixType:"box",x:-120,y:MS_ART_Y - 120,w:240,h:240,r:0});
   msRoundRect(c,x + 2,2,MS_SIDE_W - 4,200,22);
   c.endFill();
   msSparkles(c,5353,14,-110,10,220,140);
   // The art's frosted socket.
   msFrosted(c,-71,MS_ART_Y - 71,142,142,24,0xFFFFFF,90,2.5);
   c.filters = [];
   msPanel.socketGlow = msPanel.createEmptyMovieClip("socketGlow",2);
   msPanel.socketGlow.lineStyle(2.5,0xFFE08A,70);
   msRoundRect(msPanel.socketGlow,-71,MS_ART_Y - 71,142,142,24);
   msPanel.socketGlow.filters = [new flash.filters.GlowFilter(0xFFE08A,0.6,18,18,2,2)];
   // Until an item is picked, the socket holds a faint coin (the stock showed a red X).
   var ph = msPanel.placeholder = msPanel.attachMovie("sanctuaryMerchantCoin","placeholder",3);
   ph._width = ph._height = 76;
   ph._x = -38;
   ph._y = MS_ART_Y - 38;
   ph._alpha = 35;
   sp.mcNoItem._alpha = 0;

   // The price block's rows: the price chip, the quantity row, a gold rule and the total.
   var r = msPanel.createEmptyMovieClip("rows",4);
   var py = MS_PRICE_Y;
   msGradientRect(r,-126,py + 40,252,36,18,0x1B2F78,0x13215A,100,MS_RIM,35,1);
   msGoldRule(r,-110,py + 134,220);
   msGradientRect(r,-126,py + 140,252,40,20,0x0A1440,0x0A1440,55,MS_GOLD,60,1.5);
   msPanel.qty_txt = msText(msPanel,"qty_txt","Quantity",-112,py + 95,110,24,15,MS_SOFT,"left");
   msPanel.qty_txt.filters = msShadow();

   msPanel.artButtons = msPanel.createEmptyMovieClip("artButtons",5);
   msPanel.artButtons.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.5,6,6,1,2)];

   // The Buy pill, under the stock button (stretched over it, invisibly).
   var b = msPanel.buy = msPanel.createEmptyMovieClip("buy",6);
   b._x = 0;
   b._y = py + 207;
   b.bg = b.createEmptyMovieClip("bg",1);
   msText(b,"label_txt","Buy",-126,-14,252,30,22,MS_INK);

   // The art, its preview and lock buttons on the socket's corners, the name and the description.
   sp.ItemIconLarge._x = 0;
   sp.ItemIconLarge._y = MS_ART_Y;
   sp.mcMemberGraphic._alpha = 0;
   sp.btnShowPreview._x = -60 - sp.btnShowPreview._width / 2;
   sp.btnShowPreview._y = MS_ART_Y + 58 - sp.btnShowPreview._height / 2;
   sp.btnClosePreview._x = -60 - sp.btnClosePreview._width / 2;
   sp.btnClosePreview._y = MS_ART_Y + 58 - sp.btnClosePreview._height / 2;
   var art = [sp.btnShowPreview,sp.btnClosePreview];
   var i;
   for(i = 0; i < art.length; i++)
   {
      art[i].UP._alpha = art[i].HOVER._alpha = art[i].DOWN._alpha = 0;
      art[i].CONTENT.filters = msShadow();
   }
   sp.redlock._xscale = sp.redlock._yscale = 62;
   msCenterAt(sp.redlock,60,MS_ART_Y + 58);
   msFormat(sp.ItemName,19,0xFFE08A,"center");
   sp.ItemName.filters = msOutline();
   msFormat(sp.ItemDescription,13,MS_SOFT,"center");
   sp.ItemDescription.filters = msShadow();
   msPlace(sp.txtClickAColorLabel,-112,252,224,20);
   msFormat(sp.txtClickAColorLabel,13,0xFFE08A,"center");
   sp.txtClickAColorLabel.filters = msShadow();
   sp.mcTintSelect._y = 268;

   // The price widget's stock rows move into ours.
   var pw = sp.pWidget;
   pw._x = -126;
   pw._y = py;
   pw.tfListPriceLabel._alpha = pw.tfListPriceValue._alpha = 0;
   pw.tfDiscountLabel._alpha = pw.tfDiscountValue._alpha = pw.tfDiscountPercentValue._alpha = 0;
   pw.mcStrikeThrough._alpha = pw.sc_coin_icon2._alpha = 0;
   pw.mcMembersOnlyButton._alpha = 0;
   pw.mcMembersOnlyButton._x = -5000;
   pw.mcRentalSC._alpha = 0;
   pw.tfYourPriceLabel.filters = pw.tfTotalPriceLabel.filters = msShadow();
   pw.tfYourPriceValue.filters = pw.tfTotalPriceValue.filters = msOutline();
   pw.sc_coin_icon3._xscale = pw.sc_coin_icon3._yscale = 118;

   // The stepper: the stock arrows become the - and + chips' hit areas either side of the stock count.
   var inc = sp.mcItemIncrementer;
   inc._x = -126 + 176;
   inc._y = py + 86;
   msFitButton(inc.down,-32,5,30,30);
   msFitButton(inc.up,46,5,30,30);
   inc.msSkin = inc.createEmptyMovieClip("msSkin",5 - 16384);
   inc.count.textColor = 0xFFFFFF;
   inc.count.filters = msOutline();
   var buy = pw.buy_mc;
   buy.up_mc._alpha = buy.over_mc._alpha = buy.down_mc._alpha = 0;
   var bb = buy.getBounds(pw);
   if(bb.xMax > bb.xMin && bb.yMax > bb.yMin)
   {
      buy._xscale *= 252 / (bb.xMax - bb.xMin);
      buy._yscale *= 46 / (bb.yMax - bb.yMin);
      bb = buy.getBounds(pw);
      buy._x += 0 - bb.xMin;
      buy._y += 184 - bb.yMin;
   }
   buy.addListener(msBuyListener);

   // The wallet: a navy pill rimmed in gold, the coin in a navy socket and the amount in white with separators
   // (the stock wallet, whose plate is a plain shape, is hidden; Station Cash no longer exists).
   sp.coinWallet._visible = false;
   var w = msPanel.wallet = sp.createEmptyMovieClip("sanctuaryWallet",sp.getNextHighestDepth());
   w._y = MS_WALLET_Y;
   w.lineStyle(2,MS_GOLD,100);
   w.beginGradientFill("linear",[0x2B4FA8,0x13215A],[100,100],[0,255],{matrixType:"box",x:-126,y:0,w:252,h:52,r:Math.PI / 2});
   msRoundRect(w,-126,0,252,52,26);
   w.endFill();
   w.lineStyle(1.5,0xFFF3C4,90);
   w.beginGradientFill("radial",[0x2B4FA8,0x13215A],[100,100],[0,255],{matrixType:"box",x:-120,y:2,w:44,h:44,r:0});
   msCircle(w,-100,26,21);
   w.endFill();
   w.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
   var coin = w.attachMovie("sanctuaryMerchantCoin","coin",1);
   coin._width = coin._height = 36;
   coin._x = -118;
   coin._y = 8;
   w.amount_txt = msText(w,"amount_txt","",-70,11,184,32,24,0xFFFFFF,"left");
   w.amount_txt.filters = msOutline();
   if(_root.m_coinAmount != undefined)
   {
      msSetWallet(_root.m_coinAmount);
   }

   // Follows what the stock code changes without telling us (incrementer, preview, lock, Buy state).
   msPanel.onEnterFrame = function()
   {
      var pw = sidePanel.pWidget;
      var inc = sidePanel.mcItemIncrementer;
      var key = _root.m_windowMode + "," + pw.tfYourPriceValue.text + "," + pw.tfTotalPriceValue.text + "," + pw.buy_mc.isEnabled() + "," + inc._visible + "," + inc.enabled + "," + sidePanel.btnShowPreview._visible + "," + sidePanel.btnShowPreview._alpha + "," + sidePanel.btnClosePreview._visible + "," + sidePanel.redlock._visible + "," + sidePanel.mcNoItem._visible + "," + sidePanel.m_coinWallet;
      if(key != this.key)
      {
         msRefreshPanel();
         this.key = _root.m_windowMode + "," + pw.tfYourPriceValue.text + "," + pw.tfTotalPriceValue.text + "," + pw.buy_mc.isEnabled() + "," + inc._visible + "," + inc.enabled + "," + sidePanel.btnShowPreview._visible + "," + sidePanel.btnShowPreview._alpha + "," + sidePanel.btnClosePreview._visible + "," + sidePanel.redlock._visible + "," + sidePanel.mcNoItem._visible + "," + sidePanel.m_coinWallet;
      }
   };
}

// The wallet's amount, shrunk to fit if it's very large.
function msSetWallet(value)
{
   var t = msPanel.wallet.amount_txt;
   if(t == undefined)
   {
      return;
   }
   var size = 24;
   msFormat(t,size);
   t.text = msThousands(value);
   while(t.textWidth > t._width - 6 && size > 12)
   {
      size -= 2;
      msFormat(t,size);
   }
}

function msPatchPanel()
{
   var proto = SidePanel.prototype;
   var populate = proto.populate;
   proto.populate = function(item)
   {
      var result = populate.apply(this,arguments);
      if(msPanel != undefined)
      {
         msRefreshPanel();
      }
      return result;
   };
   // Coins with thousands separators in our wallet (the stock one, still written, is hidden).
   var setCoins = proto.setCoins;
   proto.setCoins = function(value)
   {
      var result = setCoins.apply(this,arguments);
      msSetWallet(value);
      return result;
   };
}

// --- Lua and stock hooks ---------------------------------------------------------------

// Membership no longer exists, and the server charges everyone the member price (CoinStoreSellToClientRequest's
// GetMemberPurchasePrice; the login reply says everyone is a member). So the client always shows that price.
var msSetIsMember = SetIsMember_lua;
SetIsMember_lua = function(n)
{
   return msSetIsMember.call(_root,1);
};
m_member = true;

var msSetTitle = setTitle_lua;
setTitle_lua = function(title)
{
   var result = msSetTitle.apply(_root,arguments);
   msFormat(headerText,28,0xFFE08A,"center");
   // Long names shrink to fit.
   var size = 28;
   while(headerText.textWidth > headerText._width - 8 && size > 14)
   {
      size -= 2;
      msFormat(headerText,size,0xFFE08A,"center");
   }
   return result;
};

var msSetWindowMode = setWindowMode_lua;
setWindowMode_lua = function(str)
{
   var result = msSetWindowMode.apply(_root,arguments);
   msDrawTabs();
   msUpdateCount();
   return result;
};

var msSetCoinAmount = setCoinAmount_lua;
setCoinAmount_lua = function(value)
{
   var result = msSetCoinAmount.apply(_root,arguments);
   msRedrawCards();
   return result;
};

var msInit = init;
init = function()
{
   var result = msInit.apply(_root,arguments);
   m_list._x = MS_LIST_X;
   m_list._y = MS_LIST_Y;
   return result;
};

// Preview mode hides the window but the detail panel (which slides over); our frame goes and comes back with it.
var msSetPreviewMode = setPreviewMode;
setPreviewMode = function()
{
   var result = msSetPreviewMode.apply(_root,arguments);
   if(_root.m_previewMode)
   {
      msFrame._visible = false;
      msHits._visible = false;
   }
   return result;
};

var msResetAfterPreview = resetAfterPreview;
resetAfterPreview = function()
{
   var result = msResetAfterPreview.apply(_root,arguments);
   msFrame._visible = true;
   msDrawTabs();
   return result;
};

msPatchCombo();
msPatchCards();
msPatchPanel();
msBuildFrame();
msBuildPanel();
msRefreshPanel();
// ===== End Sanctuary Merchant Store =====

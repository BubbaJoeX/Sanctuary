// ===== Sanctuary Action Bar =====
// Appended to navigation's main timeline by build_client_mods.py, after navigation/coin_store_tabs.as.
// The bottom tab bar (wndNavigationBar) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as). It is
// the pet care bar while you're with your pet (treats, grooming, food, tricks and gear, then the X that sends the
// pet home) and the Marketplace's tab bar, so both get it: a navy glass tray with each tab's icon in a frosted
// round socket that lifts and rims in gold on hover, the open tab ringed in gold, and the bar buttons (the X) as
// gold circles in their own glass disc. The stock plates stay as invisible hit areas, so every click, tooltip and
// Lua event is the stock one; the tray and sockets follow the stock layout (redraw, setWidth).

var PA_GOLD = 0xFFC531;
var PA_RIM = 0x9DB8FF;
var PA_SOFT = 0xC9D6FF;
var PA_INK = 0x3A2400;
// fr.core.FRButton's plate is 77x72 at its origin; the gold circle sits in its middle.
var PA_BAR_CX = 38.5;
var PA_BAR_CY = 36;
var PA_BAR_R = 24;
// The glass disc behind each bar button.
var PA_BAR_DISC_R = 37;

// --- drawing helpers -------------------------------------------------------------------

function paRoundRect(mc, x, y, w, h, r)
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

function paCircle(mc, x, y, r)
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

function paGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   paRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function paSparkle(mc, x, y, r, alpha)
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

// The navy glass of the frame, as a pill or a disc (r = h / 2).
function paGlass(mc, x, y, w, h)
{
   var r = h / 2;
   mc.lineStyle(3,PA_RIM,80);
   mc.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   paRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   mc.lineStyle();
   mc.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:x - h,y:y - h * 1.4,w:Math.min(w,h * 6),h:h * 2.6,r:0});
   paRoundRect(mc,x + 2,y + 2,w - 4,h - 4,r - 2);
   mc.endFill();
   paGloss(mc,x + r * 0.7,y + 4,w - r * 1.4,h * 0.36,h * 0.18,18);
}

// --- tray and tab sockets ------------------------------------------------------------------

function paDrawTray(nb)
{
   nb.background._alpha = 0;
   var t = nb.sanctuaryTray;
   if(t == undefined)
   {
      t = nb.sanctuaryTray = nb.createEmptyMovieClip("sanctuaryTray",-16384);
      t.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   }
   var w = nb.background._width;
   var h = nb.background._height;
   t.clear();
   if(nb.buttons.length == 0 || w <= 0)
   {
      return;
   }
   paGlass(t,0,0,w,h);
   // A recessed groove the sockets sit in, and a few sparkles (fixed seed).
   t.lineStyle(1,PA_RIM,22);
   t.beginFill(0x0A1440,30);
   paRoundRect(t,10,8,w - 20,h - 16,(h - 16) / 2);
   t.endFill();
   var s = 6161;
   var i;
   for(i = 0; i < 8; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 30 + s % Math.max(1,Math.round(w - 60));
      s = (s * 1103515245 + 12345) % 2147483648;
      paSparkle(t,x,5 + s % 6,1 + s % 2,20 + s % 40);
   }
}

// A tab's socket, centered on its (invisible) hit plate: frosted, white rimmed; gold rimmed and glowing when
// hovered, ringed in gold while its page is open.
function paDrawSocket(b)
{
   var c = b.sanctuarySocket;
   var over = b.sanctuaryOver && b.boolIsEnabled;
   var selected = b.sanctuarySelected;
   var x = c.cx;
   var y = c.cy;
   var r = c.r;
   c.clear();
   c.lineStyle();
   c.beginFill(0x050A24,40);
   paCircle(c,x,y + 3,r + 1);
   c.endFill();
   if(selected)
   {
      c.lineStyle(3,PA_GOLD,100);
      c.beginGradientFill("radial",[0xFFE08A,0xFFC531],[45,0],[0,255],{matrixType:"box",x:x - r - 8,y:y - r - 8,w:r * 2 + 16,h:r * 2 + 16,r:0});
      paCircle(c,x,y,r + 5);
      c.endFill();
   }
   c.lineStyle(over || selected ? 3 : 2,over || selected ? 0xFFE08A : 0xFFFFFF,over || selected ? 100 : 85);
   c.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r - 6,y:y - r - 10,w:r * 2 + 8,h:r * 2 + 8,r:0});
   paCircle(c,x,y,r);
   c.endFill();
   paGloss(c,x - r * 0.6,y - r + 3,r * 1.2,r * 0.55,r * 0.27,55);
   if(over)
   {
      c.filters = [new flash.filters.GlowFilter(PA_GOLD,0.85,16,16,2,2)];
   }
   else if(selected)
   {
      c.filters = [new flash.filters.GlowFilter(PA_GOLD,0.5,12,12,2,2)];
   }
   else
   {
      c.filters = [];
   }
   gs.TweenLite.to(c,0.25,{_y:over ? -3 : 0,ease:mx.transitions.easing.Back.easeOut});
}

function paStyleTab(b)
{
   if(b.sanctuarySocket)
   {
      return;
   }
   // The beige plates and the spinning selection glow; HIT keeps the clicks.
   b.UP._alpha = b.HOVER._alpha = b.DOWN._alpha = 0;
   b.SELECTION._alpha = 0;
   var c = b.sanctuarySocket = b.createEmptyMovieClip("sanctuarySocket",-16384);
   var bounds = b.HIT.getBounds(b);
   if(bounds.xMax > bounds.xMin)
   {
      c.cx = (bounds.xMin + bounds.xMax) / 2;
      c.cy = (bounds.yMin + bounds.yMax) / 2 - 2;
      c.r = (bounds.xMax - bounds.xMin) / 2 - 5;
   }
   else
   {
      c.cx = 0;
      c.cy = 30;
      c.r = 27;
   }
   b.sanctuaryOver = false;
   b.sanctuarySelected = b.SELECTION._visible == true;
}

function paPatchTabs()
{
   var proto = NavigationButton.prototype;
   var updateState = proto.updateState;
   proto.updateState = function(mode, hideTooltip)
   {
      updateState.apply(this,arguments);
      paStyleTab(this);
      this.sanctuaryOver = mode == 2 || mode == 3;
      paDrawSocket(this);
   };
   var select = proto.select;
   proto.select = function()
   {
      select.apply(this,arguments);
      paStyleTab(this);
      this.sanctuarySelected = true;
      paDrawSocket(this);
   };
   var unselect = proto.unselect;
   proto.unselect = function()
   {
      unselect.apply(this,arguments);
      paStyleTab(this);
      this.sanctuarySelected = false;
      paDrawSocket(this);
   };

   var tray = NavigationBarNavigationButtons.prototype;
   var setWidth = tray.setWidth;
   tray.setWidth = function(value)
   {
      setWidth.apply(this,arguments);
      paDrawTray(this);
   };
}

// --- bar buttons (the X) ------------------------------------------------------------------

function paDrawBarTray(bb)
{
   bb.background._alpha = 0;
   var t = bb.sanctuaryTray;
   if(t == undefined)
   {
      t = bb.sanctuaryTray = bb.createEmptyMovieClip("sanctuaryTray",-16384);
      t.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   }
   t.clear();
   var i;
   for(i = 0; i < bb.buttons.length; i++)
   {
      var b = bb.buttons[i];
      var r = PA_BAR_DISC_R;
      paGlass(t,b._x + PA_BAR_CX - r,b._y + PA_BAR_CY - r,r * 2,r * 2);
   }
}

function paDrawBarButton(b, mode)
{
   var c = b.sanctuaryChip;
   var over = mode == 2;
   var down = mode == 3;
   var x = PA_BAR_CX;
   var y = PA_BAR_CY + (down ? 1 : 0);
   var r = PA_BAR_R;
   // Gold for the beige graphic (the usual one), green and red kept for those.
   var colors;
   var ink = PA_INK;
   if(b.graphicID == fr.core.FRButton.GREEN)
   {
      colors = over ? [0xD2FAB0,0x6CCB3C] : [0xB6F08A,0x4CAF2E];
      ink = 0x123A06;
   }
   else if(b.graphicID == fr.core.FRButton.RED)
   {
      colors = over ? [0xFFB8AC,0xE85040] : [0xFF9C8C,0xD9402E];
      ink = 0xFFFFFF;
   }
   else
   {
      colors = over ? [0xFFF0B0,0xFFB838] : down ? [0xF5B840,0xD98A10] : [0xFFE08A,0xF5A623];
   }
   c.clear();
   c.lineStyle(1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",colors,[100,100],[0,255],{matrixType:"box",x:x - r,y:y - r,w:r * 2,h:r * 2,r:Math.PI / 2});
   paCircle(c,x,y,r);
   c.endFill();
   paGloss(c,x - r * 0.65,y - r * 0.85,r * 1.3,r * 0.65,r * 0.3,down ? 25 : 55);
   // The X is drawn; any other icon (back, check, door...) stays the stock one, over the circle.
   var cross = b.iconID == fr.core.FRButton.X;
   b.ICON_UP._alpha = b.ICON_HOVER._alpha = cross ? 0 : 100;
   if(cross)
   {
      c.lineStyle(5,ink,100);
      c.moveTo(x - 8,y - 8);
      c.lineTo(x + 8,y + 8);
      c.moveTo(x + 8,y - 8);
      c.lineTo(x - 8,y + 8);
   }
   c.filters = over || down ? [new flash.filters.GlowFilter(PA_GOLD,0.85,16,16,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

function paStyleBarButton(b)
{
   if(b.sanctuaryChip)
   {
      return;
   }
   b.UP._alpha = b.HOVER._alpha = b.DOWN._alpha = 0;
   b.sanctuaryChip = b.createEmptyMovieClip("sanctuaryChip",-16384);
   b.sanctuaryMode = 1;
}

function paPatchBarButtons()
{
   var proto = fr.core.FRButton.prototype;
   var updateState = proto.updateState;
   proto.updateState = function(mode)
   {
      updateState.apply(this,arguments);
      paStyleBarButton(this);
      this.sanctuaryMode = mode;
      paDrawBarButton(this,mode);
   };
   var setIcon = proto.setIcon;
   proto.setIcon = function(value)
   {
      setIcon.apply(this,arguments);
      paStyleBarButton(this);
      paDrawBarButton(this,this.sanctuaryMode);
   };
   var setGraphic = proto.setGraphic;
   proto.setGraphic = function(value)
   {
      setGraphic.apply(this,arguments);
      paStyleBarButton(this);
      paDrawBarButton(this,this.sanctuaryMode);
   };

   var tray = NavigationBarBarButtons.prototype;
   var setWidth = tray.setWidth;
   tray.setWidth = function(value)
   {
      setWidth.apply(this,arguments);
      paDrawBarTray(this);
   };
}

paPatchTabs();
paPatchBarButtons();
paDrawTray(Bar.navButtons);
paDrawBarTray(Bar.barButtons);
// ===== End Sanctuary Action Bar =====

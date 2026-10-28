// ===== Sanctuary housing sub menu =====
// Appended to housingSubMenu's main timeline by build_client_mods.py.
// The small pop-up row of house buttons (housingItemSelected, or the store's storeNavItemSelected) in the Main
// Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass bar rimmed in gold with a pointer under its
// middle, each house a frosted disc in a navy rim that lifts and rings gold on hover, and gold page arrows once there
// are more than four. Membership no longer exists, so nothing is greyed out or sent to the membership page. The stock
// clips keep their roles (hit areas, tooltips, Lua's items): their art is hidden (alpha 0) and the new look is drawn
// under them; the stock pointer (a shape) is covered by the new one.

var HM_GOLD = 0xFFC531;
var HM_RIM = 0x9DB8FF;
var HM_INK = 0x3A2400;
// The bar's top and height in subMenu_mc (the stock backing sits at y 19, 49 tall with its shadow).
var HM_BAR_Y = 19;
var HM_BAR_H = 44;

function hmRoundRect(mc, x, y, w, h, r)
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

function hmCircle(mc, x, y, r)
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

function hmFrostedCircle(mc, x, y, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.2,y:y - r * 1.3,w:r * 2.6,h:r * 2.6,r:0});
   hmCircle(mc,x,y,r);
   mc.endFill();
}

function hmShadow(distance, blur, alpha)
{
   return [new flash.filters.DropShadowFilter(distance,90,0x050A24,alpha,blur,blur,1,2)];
}

function hmGlow(color, blur)
{
   return [new flash.filters.GlowFilter(color,0.8,blur,blur,2,2)];
}

// --- bar -------------------------------------------------------------------------------

// The bar, as wide as the stock backing (HousingSubMenu sizes it to the buttons), and its pointer.
function hmDrawBar()
{
   var w = subMenu_mc.background_mc._width;
   var x = - w / 2;
   var b = subMenu_mc.hmBar;
   b.clear();
   b.lineStyle(2.5,HM_GOLD,90);
   b.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:x,y:HM_BAR_Y,w:w,h:HM_BAR_H,r:Math.PI / 2});
   hmRoundRect(b,x,HM_BAR_Y,w,HM_BAR_H,HM_BAR_H / 2);
   b.endFill();
   b.lineStyle();
   b.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[28,0],[0,255],{matrixType:"box",x:x,y:HM_BAR_Y,w:w,h:HM_BAR_H / 2,r:Math.PI / 2});
   hmRoundRect(b,x + 8,HM_BAR_Y + 2,w - 16,HM_BAR_H / 2 - 2,HM_BAR_H / 4);
   b.endFill();

   // The pointer covers the stock one (a shape at -8..9, 50..69) and runs into the bar's bottom.
   var p = subMenu_mc.hmPointer;
   p.clear();
   p.lineStyle();
   p.beginFill(0x141F55,100);
   p.moveTo(-13,48);
   p.lineTo(13,48);
   p.lineTo(0,73);
   p.lineTo(-13,48);
   p.endFill();
   p.lineStyle(2.5,HM_GOLD,90);
   p.moveTo(-10,HM_BAR_Y + HM_BAR_H - 1);
   p.lineTo(0,73);
   p.lineTo(10,HM_BAR_Y + HM_BAR_H - 1);
}

// --- buttons ---------------------------------------------------------------------------

var hmStateUp = FRIconButton.prototype.setStateUp;
FRIconButton.prototype.setStateUp = function()
{
   hmStateUp.apply(this,arguments);
   if(this.hmDraw != undefined)
   {
      this.hmDraw("up");
   }
};

var hmStateOver = FRIconButton.prototype.setStateOver;
FRIconButton.prototype.setStateOver = function()
{
   hmStateOver.apply(this,arguments);
   if(this.hmDraw != undefined && this.m_isEnabled)
   {
      this.hmDraw("over");
   }
};

var hmStateDown = FRIconButton.prototype.setStateDown;
FRIconButton.prototype.setStateDown = function()
{
   hmStateDown.apply(this,arguments);
   if(this.hmDraw != undefined && this.m_isEnabled)
   {
      this.hmDraw("down");
   }
};

// A house: a frosted disc in a navy glass rim, ringed gold and lifted while hovered.
function hmDrawHouse(state)
{
   var s = this.hmSkin;
   s.clear();
   var over = state == "over" || state == "down";
   var cx = 25.2;
   var cy = 25.9;
   s.lineStyle(over ? 2.5 : 1.5,over ? 0xFFE08A : HM_RIM,100);
   s.beginGradientFill("linear",over ? [0xFFE08A,0xF5A623] : [0x3F66D6,0x162660],[100,100],[0,255],{matrixType:"box",x:cx - 25,y:cy - 25,w:50,h:50,r:Math.PI / 2});
   hmCircle(s,cx,cy,25);
   s.endFill();
   hmFrostedCircle(s,cx,cy,20.5,0x0A1440,40,1);
   s.filters = over ? hmGlow(HM_GOLD,14) : hmShadow(3,8,0.55);
   // The lift is from where HousingSubMenu put the button (it's placed after its item is set, so it's read here).
   if(this.hmBaseY == undefined)
   {
      if(state == "up")
      {
         return;
      }
      this.hmBaseY = this._y;
   }
   gs.TweenLite.to(this,0.25,{_y:this.hmBaseY - (over ? 4 : 0),ease:mx.transitions.easing.Strong.easeOut});
}

// Gold page arrows; the right one is the left one mirrored.
function hmDrawArrow(state)
{
   var s = this.hmSkin;
   s.clear();
   var over = state == "over" || state == "down";
   var cx = 14;
   var cy = 17.5;
   var r = 13;
   s.lineStyle(over ? 2 : 1.5,0xFFF3C4,100);
   s.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:cx - r,y:cy - r,w:r * 2,h:r * 2,r:Math.PI / 2});
   hmCircle(s,cx,cy,r);
   s.endFill();
   s.lineStyle(3.5,HM_INK,100);
   s.moveTo(cx + 3,cy - 6);
   s.lineTo(cx - 3,cy);
   s.lineTo(cx + 3,cy + 6);
   this.filters = over ? hmGlow(HM_GOLD,12) : hmShadow(3,8,0.5);
}

function hmSkin(btn, draw)
{
   if(btn == undefined || btn.hmSkin != undefined)
   {
      return;
   }
   btn.up_mc._alpha = btn.over_mc._alpha = btn.down_mc._alpha = 0;
   btn.hmSkin = btn.createEmptyMovieClip("hmSkin",-16384);
   btn.hmDraw = draw;
   btn.hmDraw("up");
}

// HousingButton (the house buttons) re-sets its art's alpha with each item, and greyed out members-only houses.
var hmSetItemData = HousingButton.prototype.setItemData;
HousingButton.prototype.setItemData = function(d)
{
   hmSetItemData.apply(this,arguments);
   this.memberOnly_mc._visible = false;
   this.m_icon_up._alpha = this.m_icon_over._alpha = 100;
   hmSkin(this,hmDrawHouse);
   this.up_mc._alpha = this.over_mc._alpha = this.down_mc._alpha = 0;
};

var hmAddItem = HousingSubMenu.prototype.addItem;
HousingSubMenu.prototype.addItem = function(guid, iconId, name, isMemberOnly)
{
   hmAddItem.apply(this,arguments);
   hmDrawBar();
};

var hmResetItems = HousingSubMenu.prototype.resetItems;
HousingSubMenu.prototype.resetItems = function()
{
   hmResetItems.apply(this,arguments);
   hmDrawBar();
};

function hmBuild()
{
   subMenu_mc.background_mc._alpha = 0;
   subMenu_mc.hmBar = subMenu_mc.createEmptyMovieClip("hmBar",-16384);
   subMenu_mc.hmBar.filters = hmShadow(5,14,0.6);
   // Above the stock pointer; it has no mouse handlers, so it doesn't take the buttons' clicks.
   subMenu_mc.hmPointer = subMenu_mc.createEmptyMovieClip("hmPointer",30);
   hmDrawBar();

   var items = subMenu_mc.items_mc;
   hmSkin(items.m_left_mc,hmDrawArrow);
   hmSkin(items.m_right_mc,hmDrawArrow);
   var pages = [items.m_fiveItems1_mc,items.m_fiveItems2_mc];
   var p;
   var i;
   for(p = 0; p < pages.length; p++)
   {
      for(i = 0; i < 4; i++)
      {
         hmSkin(pages[p]["item" + i],hmDrawHouse);
      }
   }
}

hmBuild();
// ===== End Sanctuary housing sub menu =====

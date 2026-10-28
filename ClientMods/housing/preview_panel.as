// ===== Sanctuary house preview =====
// Appended to HousingPreview's main timeline by build_client_mods.py, after HousingPreview/no_station_cash.as.
// The purchase panel shown while previewing a house (wndHousingPreview) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass card with the house's art in a gold-ringed frosted socket, its
// name in gold, the price on a navy pill with the coin (TEXTURE_SWAPS puts the coin store's coin in place of the
// Station Cash logo; the wallet's logo goes with the wallet), the description in soft blue and a gold Buy pill that
// lights up on hover. The stock clips keep their roles (hit areas, the stock shimmer over Buy, Lua callbacks): the
// panel and button art is hidden (alpha 0) and the new look is drawn under it.

var HP_W = 195;
var HP_H = 260;
var HP_GOLD = 0xFFC531;
var HP_RIM = 0x9DB8FF;
var HP_SOFT = 0xC9D6FF;
var HP_INK = 0x3A2400;
// The house art (64px, centered at 42.5, 44) and its socket's radius: the panel is too small to show it bigger.
var HP_SOCKET_R = 34;

function hpRoundRect(mc, x, y, w, h, r)
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

function hpGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   hpRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function hpFrosted(mc, x, y, w, h, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - w * 0.2,y:y - h * 0.25,w:w * 1.4,h:h * 1.4,r:0});
   hpRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function hpSparkle(mc, x, y, r, alpha)
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

function hpOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function hpShadow(distance, blur, alpha)
{
   return [new flash.filters.DropShadowFilter(distance,90,0x050A24,alpha,blur,blur,1,2)];
}

function hpColor(t, color)
{
   t.textColor = color;
   t.filters = hpOutline();
}

var hpPanel = mcPurchasePanel;

// The card, the art's socket and the price pill.
function hpDrawCard()
{
   var c = hpPanel.hpCard;
   c.clear();
   hpGradientRect(c,2,2,HP_W - 4,HP_H - 4,24,0x2E4FB0,0x141F55,100,HP_RIM,80,3);
   c.lineStyle();
   c.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-90,y:-120,w:300,h:300,r:0});
   hpRoundRect(c,4,4,HP_W - 8,HP_H - 8,22);
   c.endFill();
   var s = 5151;
   var i;
   for(i = 0; i < 8; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 86 + s % 100;
      s = (s * 1103515245 + 12345) % 2147483648;
      hpSparkle(c,x,8 + s % 10,1 + s % 2,25 + s % 50);
   }

   // The art's socket: gold rimmed, under the icon (centered at 42.5, 44).
   var r = HP_SOCKET_R;
   hpGradientRect(c,42.5 - r - 3,44 - r - 3,r * 2 + 6,r * 2 + 6,18,0xFFE08A,0xF5A623,100,0xFFF3C4,100,1.5);
   hpFrosted(c,42.5 - r,44 - r,r * 2,r * 2,15,0x0A1440,30,1);

   // The price: a navy pill rimmed in gold behind the coin and amount.
   hpGradientRect(c,88,47,100,30,15,0x2B4FA8,0x13215A,100,HP_GOLD,100,2);

   // The description's recess.
   hpGradientRect(c,8,84,HP_W - 16,52,14,0x0A1440,0x0A1440,40,HP_RIM,25,1);
}

// Buy: a gold pill under the stock button's (hidden) plates; its BUY label is turned dark ink.
function hpDrawBuy(over, down)
{
   var b = hpPanel.btnBuy.hpSkin;
   b.clear();
   var x = 10;
   var y = 8;
   var w = 180;
   var h = 52;
   b.lineStyle(over ? 2.5 : 2,0xFFF3C4,100);
   b.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   hpRoundRect(b,x,y,w,h,h / 2);
   b.endFill();
   b.lineStyle();
   b.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h / 2,r:Math.PI / 2});
   hpRoundRect(b,x + 6,y + 2,w - 12,h / 2 - 1,12);
   b.endFill();
   b._y = down ? 1 : 0;
   b.filters = over ? [new flash.filters.GlowFilter(HP_GOLD,0.85,16,16,2,2)] : hpShadow(4,10,0.55);
}

function hpBuild()
{
   hpPanel.hpCard = hpPanel.createEmptyMovieClip("hpCard",-16384);
   hpPanel.hpCard.filters = hpShadow(6,18,0.6);
   hpPanel.bg._alpha = 0;

   hpColor(hpPanel.txtName,0xFFE08A);
   hpColor(hpPanel.txtPrice,0xFFFFFF);
   hpColor(hpPanel.txtDescription,HP_SOFT);
   hpColor(hpPanel.txtProcessing,0xFFE08A);
   // The price is the coin and amount on their pill (the socket leaves no room for the Price: label); the wallet's
   // coin goes with the wallet.
   hpPanel.txtPriceLabel._visible = false;
   hpPanel.getInstanceAtDepth(8 - 16384)._x = 96;
   hpPanel.txtPrice._x = 124;
   // The name clears the socket's ring, and the description sits in its recess.
   hpPanel.txtName._x += 8;
   hpPanel.txtDescription._y = 86;
   hpPanel.getInstanceAtDepth(26 - 16384)._visible = false;

   // Buy: the plates are hidden (they keep the hit area), the label goes dark, and the button's own hover hooks
   // (unused by the stock panel) redraw the pill.
   var buy = hpPanel.btnBuy;
   buy.UP._alpha = buy.DOWN._alpha = buy.HOVER._alpha = 0;
   new Color(buy.CONTENT).setTransform({ra:0,rb:0x3A,ga:0,gb:0x24,ba:0,bb:0x00,aa:100,ab:0});
   buy.hpSkin = buy.createEmptyMovieClip("hpSkin",-16384);
   buy.externalRollOver = function()
   {
      hpDrawBuy(true,false);
      gs.TweenLite.to(this.hpSkin,0.5,{_xscale:104,_yscale:104,_x:-4,ease:mx.transitions.easing.Elastic.easeOut});
   };
   buy.externalRollOut = function()
   {
      hpDrawBuy(false,false);
      gs.TweenLite.to(this.hpSkin,0.5,{_xscale:100,_yscale:100,_x:0,ease:mx.transitions.easing.Elastic.easeOut});
   };
   buy.externalPress = function()
   {
      hpDrawBuy(true,false);
   };
   hpDrawBuy(false,false);

   hpDrawCard();
}

// Lua's texts arrive in the stock colors; recolor them after every update.
var hpSetData = HousingPreview.prototype.setData;
HousingPreview.prototype.setData = function()
{
   hpSetData.apply(this,arguments);
   hpColor(this.txtName,0xFFE08A);
   hpColor(this.txtDescription,HP_SOFT);
   hpColor(this.txtPrice,0xFFFFFF);
};

hpBuild();
// ===== End Sanctuary house preview =====

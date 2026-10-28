// ===== Sanctuary Interaction Prompt =====
// Appended to interactionPrompt's main timeline by build_client_mods.py.
// The "Press X or click" prompt that pops up by NPCs, doors and props (wndInteractionPrompt) in the Main Menu's
// style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass card with sparkles, the name on a gold pill tab over
// its top edge, the key as a frosted keycap with a gold rim and the mouse in a frosted socket. Hovering the card
// rims it in gold (the stock hover still bounces the mouse). The stock card, title plate and keycap stay as
// invisible clips (the card is the click target that sends PromptSelected), and everything is redrawn from their
// bounds after Lua's SetName, SetKeyName and ShowMouseImage resize and move them.

var IP_RIM = 0x9DB8FF;
var IP_GOLD = 0xFFC531;
var IP_INK = 0x3A2400;
var IP_SOFT = 0xC9D6FF;

// --- drawing helpers -------------------------------------------------------------------

function ipRoundRect(mc, x, y, w, h, r)
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

function ipCircle(mc, x, y, r)
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

function ipGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   ipRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function ipSparkle(mc, x, y, r, alpha)
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

function ipOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// --- the card --------------------------------------------------------------------------

function ipDrawCard()
{
   var card = _root.sanctuaryPromptCard;
   if(card == undefined)
   {
      card = _root.createEmptyMovieClip("sanctuaryPromptCard",-16384);
   }
   card.clear();
   var b = bg_mc.getBounds(_root);
   var x = b.xMin + 1.5;
   var y = b.yMin + 1.5;
   var w = b.xMax - b.xMin - 3;
   var h = b.yMax - b.yMin - 5;
   var r = 24;
   card.lineStyle(card.over ? 3 : 2.5,card.over ? 0xFFE08A : IP_RIM,card.over ? 100 : 80);
   card.beginGradientFill("linear",card.over ? [0x3F66D6,0x1D307A] : [0x2E4FB0,0x141F55],[97,97],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   ipRoundRect(card,x,y,w,h,r);
   card.endFill();
   card.lineStyle();
   card.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:x - w * 0.25,y:y - h * 0.7,w:w * 0.9,h:h * 1.6,r:0});
   ipRoundRect(card,x + 2,y + 2,w - 4,h - 4,r - 2);
   card.endFill();
   ipGloss(card,x + 14,y + 3,w - 28,h * 0.32,10,18);
   var s = 3131;
   var i;
   for(i = 0; i < 6; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + 20 + s % Math.round(w - 40);
      s = (s * 1103515245 + 12345) % 2147483648;
      ipSparkle(card,sx,y + 8 + s % Math.round(h - 16),1 + s % 2,15 + s % 30);
   }
   card.filters = card.over ? [new flash.filters.GlowFilter(IP_GOLD,0.75,16,16,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,10,10,1,2)];
}

// The name on a gold pill tab, drawn in title_mc so it shows and hides with it.
function ipDrawTitle()
{
   var plate = title_mc.titleBg_mc;
   var tab = title_mc.sanctuaryTab;
   if(tab == undefined)
   {
      tab = title_mc.createEmptyMovieClip("sanctuaryTab",-16384);
   }
   tab.clear();
   plate._alpha = 0;
   var b = plate.getBounds(title_mc);
   var x = b.xMin + 1;
   var y = b.yMin + 1;
   var w = b.xMax - b.xMin - 2;
   var h = b.yMax - b.yMin - 3;
   tab.lineStyle(1.5,0xFFF3C4,100);
   tab.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   ipRoundRect(tab,x,y,w,h,h / 2);
   tab.endFill();
   ipGloss(tab,x + h * 0.4,y + 2,w - h * 0.8,h * 0.42,h * 0.2,55);
   tab.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
   title_mc.nameTitle.textColor = IP_INK;
   title_mc.nameTitle.filters = [];
}

// The key as a frosted keycap with a gold rim (and a darker lip, so it reads as a key).
function ipDrawKey()
{
   var key = textAnd_mc.button_mc;
   var cap = key.sanctuaryCap;
   if(cap == undefined)
   {
      cap = key.createEmptyMovieClip("sanctuaryCap",-16384);
   }
   cap.clear();
   key.bg._alpha = 0;
   var b = key.bg.getBounds(key);
   var x = b.xMin + 1;
   var y = b.yMin + 1;
   var w = b.xMax - b.xMin - 2;
   var h = b.yMax - b.yMin - 2;
   var r = Math.min(12,h / 3);
   cap.lineStyle();
   cap.beginFill(0x7F98D8,100);
   ipRoundRect(cap,x,y + 3,w,h - 3,r);
   cap.endFill();
   cap.lineStyle(2,0xFFE08A,100);
   cap.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - w * 0.2,y:y - h * 0.4,w:w * 1.4,h:h * 1.5,r:0});
   ipRoundRect(cap,x,y,w,h - 4,r);
   cap.endFill();
   ipGloss(cap,x + 5,y + 2,w - 10,(h - 4) * 0.4,r * 0.6,70);
   cap.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.55,5,5,1,2)];
   key.key_txt.textColor = 0x1A2C70;
   key.key_txt.filters = [];
}

// The mouse in a frosted socket (drawn inside its clip, so it moves, hides and bounces with it).
function ipDrawMouse()
{
   var pic = textAnd_mc.mousePic_mc;
   if(pic.sanctuarySocket != undefined)
   {
      return;
   }
   var sock = pic.createEmptyMovieClip("sanctuarySocket",-16384);
   sock.lineStyle(2,0xFFE08A,100);
   // Inside the stock picture's 51px square, so SetKeyName's layout (which measures the clip) doesn't change.
   sock.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:-34,y:-36,w:62,h:66,r:0});
   ipCircle(sock,0,0,24.5);
   sock.endFill();
   ipGloss(sock,-17,-23,34,16,8,60);
   sock.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.55,5,5,1,2)];
}

// "Press" white and "or" soft blue, outlined.
function ipStyleText()
{
   var k;
   for(k in textAnd_mc)
   {
      var t = textAnd_mc[k];
      if(t instanceof TextField)
      {
         t.textColor = t == textAnd_mc.or_txt ? IP_SOFT : 0xFFFFFF;
         t.filters = ipOutline();
      }
   }
}

function ipRedraw()
{
   ipDrawCard();
   ipDrawTitle();
   ipDrawKey();
   ipDrawMouse();
   ipStyleText();
}

bg_mc._alpha = 0;
ipRedraw();

// Lua's calls resize and move the stock clips: redraw from them afterwards.
var ipSetName = SetName;
SetName = function()
{
   var result = ipSetName.apply(_root,arguments);
   ipRedraw();
   return result;
};
var ipSetKeyName = SetKeyName;
SetKeyName = function()
{
   var result = ipSetKeyName.apply(_root,arguments);
   ipRedraw();
   return result;
};
var ipShowMouseImage = ShowMouseImage;
ShowMouseImage = function()
{
   var result = ipShowMouseImage.apply(_root,arguments);
   ipRedraw();
   return result;
};

// The card lights up gold on hover; the stock handlers (mouse bounce, sound, PromptSelected) still run.
var ipRollOver = bg_mc.onRollOver;
var ipRollOut = bg_mc.onRollOut;
bg_mc.onRollOver = function()
{
   ipRollOver.apply(this,arguments);
   _root.sanctuaryPromptCard.over = true;
   ipDrawCard();
};
bg_mc.onRollOut = bg_mc.onReleaseOutside = function()
{
   ipRollOut.apply(this,arguments);
   _root.sanctuaryPromptCard.over = false;
   ipDrawCard();
};
// ===== End Sanctuary Interaction Prompt =====

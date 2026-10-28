// ===== Sanctuary message window =====
// Appended to messageWindow's main timeline by build_client_mods.py.
// Redraws the generic message box (wndMessageWindow, and wndUtilityWindow's level-up and game panels) as a compact
// card in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): navy glass with a gold title over a gold
// hairline, white body text (links stay gold), the first of Lua's buttons a gold pill and the rest navy, and the
// round OK button a gold circle. The stock plates go invisible; the new art is drawn into free depths inside the
// stock buttons (between their plate and their label or icon), so every click, callback and teletype stays stock.

var MW_GOLD = 0xFFC531;
var MW_RIM = 0x9DB8FF;
var MW_SOFT = 0xC9D6FF;
var MW_INK = 0x3A2400;
var MW_H = 340;
// Free depths: a textButton's state clips hold their plate at 1 and label at 3; a circleButton holds its state art
// at 1-5 and icons at 7 and 9.
var MW_PILL_DEPTH = 2 - 16384;
var MW_DISC_DEPTH = 6 - 16384;

var mwFrame;

// --- drawing helpers -------------------------------------------------------------------

function mwRoundRect(mc, x, y, w, h, r)
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

function mwGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   mwRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function mwGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   mwRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function mwCircle(mc, x, y, r)
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

function mwSparkle(mc, x, y, r, alpha)
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

function mwOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Recolours a stock field (all of its text and what it's given next) and outlines it. A field with a style sheet
// (the html body) keeps its colours: Flash won't restyle it.
function mwLightText(t, color, outline)
{
   if(t == undefined)
   {
      return;
   }
   if(t.styleSheet == undefined)
   {
      t.textColor = color;
      var f = t.getNewTextFormat();
      f.color = color;
      t.setNewTextFormat(f);
   }
   t.filters = outline == false ? [] : mwOutline();
}

// --- frame -----------------------------------------------------------------------------

// The card spans the stock plate's width (setWidth_lua narrows it) and the movie's height.
function mwDrawFrame()
{
   var b = noButtonBG_mc.getBounds(_root);
   var x = b.xMax > b.xMin ? Math.round(b.xMin) + 6 : 6;
   var w = b.xMax > b.xMin ? Math.round(b.xMax - b.xMin) - 12 : 414;
   var h = MW_H - 10;
   var bg = mwFrame.bg;
   bg.clear();
   mwGradientRect(bg,x,4,w,h,26,0x2E4FB0,0x141F55,100,MW_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:x - 120,y:-200,w:w * 0.9 + 120,h:420,r:0});
   mwRoundRect(bg,x + 2,6,w - 4,h - 4,24);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   mwRoundRect(bg,x + 4,8,w - 8,52,22);
   bg.endFill();
   bg.beginGradientFill("linear",[MW_GOLD,MW_GOLD,MW_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:x + 30,y:0,w:w - 60,h:2,r:0});
   mwRoundRect(bg,x + 30,61,w - 60,2,1);
   bg.endFill();
   var s = 4040;
   var i;
   for(i = 0; i < 16; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + 20 + s % (w - 40);
      s = (s * 1103515245 + 12345) % 2147483648;
      mwSparkle(bg,sx,14 + s % 40,1 + s % 2,20 + s % 45);
   }
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
}

function mwBuildFrame()
{
   noButtonBG_mc._alpha = 0;
   bg_mc._alpha = 0;
   mwFrame = _root.createEmptyMovieClip("sanctuaryMessage",-16384);
   mwFrame.bg = mwFrame.createEmptyMovieClip("bg",1);
   mwDrawFrame();
   mwLightText(header_txt,0xFFE08A);
   mwLightText(body_txt,0xFFFFFF);
   mwStyleDisc(closebtn);
}

// --- buttons ---------------------------------------------------------------------------

// Lua's buttons: a pill in each state clip (up, over, down), gold for the first button and navy for the rest.
function mwStyleButton(btn, gold)
{
   var states = [btn.up_mc,btn.over_mc,btn.down_mc];
   var i;
   for(i = 0; i < states.length; i++)
   {
      var s = states[i];
      if(s == undefined)
      {
         continue;
      }
      var g = s.createEmptyMovieClip("mwPill",MW_PILL_DEPTH);
      var over = i == 1;
      var down = i == 2;
      if(gold)
      {
         g.lineStyle(1.5,0xFFF3C4,100);
         g.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : (down ? [0xF5A623,0xE09016] : [0xFFE08A,0xF5A623]),[100,100],[0,255],{matrixType:"box",x:0,y:0,w:100,h:32,r:Math.PI / 2});
         mwRoundRect(g,0,0,100,32,16);
         g.endFill();
         mwGloss(g,8,1,84,13,6,down ? 25 : 50);
      }
      else
      {
         mwGradientRect(g,0,0,100,32,16,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : MW_RIM,over ? 100 : 75,over ? 2 : 1.5);
         mwGloss(g,8,1,84,13,6,down ? 12 : 24);
      }
      g.filters = over ? [new flash.filters.GlowFilter(MW_GOLD,0.7,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
      mwLightText(s.textField_txt,gold ? MW_INK : 0xFFFFFF,!gold);
   }
}

// The round OK button: a gold disc over its stock state art, under its icon.
function mwDrawDisc(btn, over)
{
   var g = btn.mwDisc;
   var r = btn.mwR;
   g.clear();
   g.lineStyle(1.5,0xFFF3C4,100);
   g.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:btn.mwX - r,y:btn.mwY - r,w:2 * r,h:2 * r,r:Math.PI / 2});
   mwCircle(g,btn.mwX,btn.mwY,r);
   g.endFill();
   mwGloss(g,btn.mwX - r * 0.65,btn.mwY - r * 0.85,r * 1.3,r * 0.7,r * 0.3,50);
   g.filters = over ? [new flash.filters.GlowFilter(MW_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

function mwStyleDisc(btn)
{
   if(btn == undefined)
   {
      return;
   }
   var b = btn.up_mc.getBounds(btn);
   btn.mwX = b.xMax > b.xMin ? (b.xMin + b.xMax) / 2 : 27;
   btn.mwY = b.yMax > b.yMin ? (b.yMin + b.yMax) / 2 : 25;
   btn.mwR = b.xMax > b.xMin ? Math.round(Math.min(b.xMax - b.xMin,b.yMax - b.yMin) / 2) - 2 : 22;
   btn.mwDisc = btn.createEmptyMovieClip("mwDisc",MW_DISC_DEPTH);
   mwDrawDisc(btn,false);
   var lo = new Object();
   lo.onHoverOver = function(target)
   {
      mwDrawDisc(btn,true);
   };
   lo.onHoverOut = function(target)
   {
      mwDrawDisc(btn,false);
   };
   btn.addListener(lo);
}

// --- Lua -------------------------------------------------------------------------------

var mwAddButton = addButton_lua;
addButton_lua = function(label, luaCallBack)
{
   var r = mwAddButton.apply(_root,arguments);
   mwStyleButton(m_buttonArray[m_buttonArray.length - 1],m_buttonArray.length == 1);
   return r;
};

var mwSetWidth = setWidth_lua;
setWidth_lua = function(w)
{
   var r = mwSetWidth.apply(_root,arguments);
   mwDrawFrame();
   return r;
};

var mwSetMessage = SetMessage;
SetMessage = function(title, message, htmlMessage)
{
   var r = mwSetMessage.apply(_root,arguments);
   mwLightText(header_txt,0xFFE08A);
   mwLightText(body_txt,0xFFFFFF);
   return r;
};

mwBuildFrame();
// ===== End Sanctuary message window =====

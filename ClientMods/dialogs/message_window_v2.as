// ===== Sanctuary message window V2 =====
// Appended to messageWindowV2's main timeline by build_client_mods.py.
// Redraws the paged message window (wndMessageWindowV2: Chatty's messages, with an optional picture and pose) as a
// card in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): navy glass with sparkles across its top,
// white outlined text, the picture in a frosted frame, a glass pager pill around "1/3" and its arrows, and the
// buttons as pills (the close or first button gold, the rest navy). The stock plate (an unnamed sprite at depth 1)
// goes invisible and the pills are drawn into a free depth inside each button state, so clicks stay stock.

var MV_W = 426;
var MV_H = 500;
var MV_GOLD = 0xFFC531;
var MV_RIM = 0x9DB8FF;
var MV_INK = 0x3A2400;
// A textButton's state clips hold their plate at depth 1 and label at 3.
var MV_PILL_DEPTH = 2 - 16384;

var mvFrame;

// --- drawing helpers -------------------------------------------------------------------

function mvRoundRect(mc, x, y, w, h, r)
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

function mvGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   mvRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function mvGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   mvRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function mvSparkle(mc, x, y, r, alpha)
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

function mvOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Recolours a stock field (all of its text and what it's given next) and outlines it.
function mvLightText(t, color, outline)
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
   t.filters = outline == false ? [] : mvOutline();
}

// --- frame -----------------------------------------------------------------------------

function mvBuildFrame()
{
   _root.getInstanceAtDepth(1 - 16384)._alpha = 0;
   mvFrame = _root.createEmptyMovieClip("sanctuaryMessage",-16384);
   var bg = mvFrame.createEmptyMovieClip("bg",1);
   mvGradientRect(bg,4,4,MV_W - 8,MV_H - 8,28,0x2E4FB0,0x141F55,100,MV_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-140,y:-260,w:520,h:560,r:0});
   mvRoundRect(bg,6,6,MV_W - 12,MV_H - 12,26);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   mvRoundRect(bg,10,58,MV_W - 20,MV_H - 136,20);
   bg.endFill();
   var s = 5353;
   var i;
   for(i = 0; i < 20; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 26 + s % (MV_W - 52);
      s = (s * 1103515245 + 12345) % 2147483648;
      mvSparkle(bg,x,12 + s % 38,1 + s % 3,20 + s % 50);
   }
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   // The pager pill behind "1/3" and its arrows (shown with them).
   var p = mvFrame.pager = mvFrame.createEmptyMovieClip("pager",2);
   mvGradientRect(p,140,12,148,34,17,0x284A9E,0x162660,100,MV_RIM,70,1.5);
   mvGloss(p,148,13,132,14,7,22);
   p._visible = count_txt._visible;

   // The picture's frame (setImage_lua fills m_target, 128 square).
   var f = mvFrame.picture = mvFrame.createEmptyMovieClip("picture",3);
   f.lineStyle(2,0xFFE08A,100);
   f.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:m_target._x - 30,y:m_target._y - 36,w:188,h:188,r:0});
   mvRoundRect(f,m_target._x - 8,m_target._y - 8,144,144,18);
   f.endFill();
   f.filters = [new flash.filters.GlowFilter(MV_GOLD,0.4,12,12,2,2)];
   f._visible = m_target._visible;

   mvLightText(body_txt,0xFFFFFF);
   mvLightText(count_txt,0xFFFFFF);
   mvStyleButton(closebtn,true);
}

// --- buttons ---------------------------------------------------------------------------

// A pill in each state clip (up, over, down) of a textButton, under its label: gold or navy.
function mvStyleButton(btn, gold)
{
   if(btn == undefined)
   {
      return;
   }
   var states = [btn.up_mc,btn.over_mc,btn.down_mc];
   var i;
   for(i = 0; i < states.length; i++)
   {
      var s = states[i];
      if(s == undefined)
      {
         continue;
      }
      var g = s.createEmptyMovieClip("mvPill",MV_PILL_DEPTH);
      var over = i == 1;
      var down = i == 2;
      if(gold)
      {
         g.lineStyle(1.5,0xFFF3C4,100);
         g.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : (down ? [0xF5A623,0xE09016] : [0xFFE08A,0xF5A623]),[100,100],[0,255],{matrixType:"box",x:0,y:0,w:100,h:32,r:Math.PI / 2});
         mvRoundRect(g,0,0,100,32,16);
         g.endFill();
         mvGloss(g,8,1,84,13,6,down ? 25 : 50);
      }
      else
      {
         mvGradientRect(g,0,0,100,32,16,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : MV_RIM,over ? 100 : 75,over ? 2 : 1.5);
         mvGloss(g,8,1,84,13,6,down ? 12 : 24);
      }
      g.filters = over ? [new flash.filters.GlowFilter(MV_GOLD,0.7,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
      mvLightText(s.textField_txt,gold ? MV_INK : 0xFFFFFF,!gold);
   }
}

// --- Lua -------------------------------------------------------------------------------

var mvAddButton = addButton_lua;
addButton_lua = function(label, luaCallBack)
{
   var r = mvAddButton.apply(_root,arguments);
   mvStyleButton(m_buttonArray[m_buttonArray.length - 1],m_buttonArray.length == 1);
   return r;
};

var mvSetCurrentPage = setCurrentPage_lua;
setCurrentPage_lua = function(currentPage, total)
{
   var r = mvSetCurrentPage.apply(_root,arguments);
   mvFrame.pager._visible = count_txt._visible;
   return r;
};

var mvSetImage = setImage_lua;
setImage_lua = function(id)
{
   var r = mvSetImage.apply(_root,arguments);
   mvFrame.picture._visible = true;
   return r;
};

var mvReset = reset_lua;
reset_lua = function()
{
   var r = mvReset.apply(_root,arguments);
   mvFrame.picture._visible = false;
   return r;
};

var mvSetMessage = SetMessage;
SetMessage = function(title, message, htmlMessage)
{
   var r = mvSetMessage.apply(_root,arguments);
   mvLightText(body_txt,0xFFFFFF);
   return r;
};

mvBuildFrame();
// ===== End Sanctuary message window V2 =====

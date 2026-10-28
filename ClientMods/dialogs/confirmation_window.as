// ===== Sanctuary confirmation =====
// Appended to ConfirmationWindow's main timeline by build_client_mods.py.
// Redraws the Yes/No confirmation (wndConfirmationWindow) as a compact card in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): navy glass that grows with its message, outlined text, a gold hairline
// over the answers, and Yes as a gold pill beside a navy No (both lift and glow gold on hover). The stock answers
// are CLIK buttons whose art lives on their own timelines, so they're hidden and the pills call the window's own
// accepted() and declined() (the same sound and Lua response, password included); resize() still lays them out
// and the pills follow.

var CW_GOLD = 0xFFC531;
var CW_RIM = 0x9DB8FF;
var CW_INK = 0x3A2400;
var CW_PILL_W = 120;
var CW_PILL_H = 40;

var cwWin = window;

// --- drawing helpers -------------------------------------------------------------------

function cwRoundRect(mc, x, y, w, h, r)
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

function cwGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   cwRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function cwGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   cwRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function cwSparkle(mc, x, y, r, alpha)
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

function cwText(mc, name, txt, x, y, w, h, size, color)
{
   mc.createTextField(name,mc.getNextHighestDepth(),x,y,w,h);
   var t = mc[name];
   t.embedFonts = true;
   t.selectable = false;
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = "center";
   t.setNewTextFormat(f);
   t.text = txt;
   return t;
}

function cwOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function cwStrongOut(t, b, c, d)
{
   t = t / d - 1;
   return c * (t * t * t * t * t + 1) + b;
}

// --- card ------------------------------------------------------------------------------

function cwDrawCard()
{
   var bg = cwWin.m_Background;
   var g = cwWin.cwCard;
   var w = bg._width;
   var h = bg._height;
   g.clear();
   cwGradientRect(g,2,2,w - 4,h - 4,26,0x2E4FB0,0x141F55,100,CW_RIM,80,3);
   g.lineStyle();
   g.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-140,y:-220,w:w * 0.9 + 140,h:400,r:0});
   cwRoundRect(g,4,4,w - 8,h - 8,24);
   g.endFill();
   var s = 3131;
   var i;
   for(i = 0; i < 16; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 24 + s % (w - 48);
      s = (s * 1103515245 + 12345) % 2147483648;
      cwSparkle(g,x,10 + s % 22,1 + s % 2,20 + s % 45);
   }
   // A gold hairline over the answers.
   var y = Math.round(cwWin.m_YesButton._y) - 14;
   g.beginGradientFill("linear",[CW_GOLD,CW_GOLD,CW_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:30,y:0,w:w - 60,h:2,r:0});
   cwRoundRect(g,30,y,w - 60,2,1);
   g.endFill();
   g.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
}

// --- answers ---------------------------------------------------------------------------

function cwDrawPill(p, over)
{
   var g = p.bg;
   var w = CW_PILL_W;
   var h = CW_PILL_H;
   g.clear();
   if(p.cwGold)
   {
      g.lineStyle(1.5,0xFFF3C4,100);
      g.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- w / 2,y:- h / 2,w:w,h:h,r:Math.PI / 2});
      cwRoundRect(g,- w / 2,- h / 2,w,h,h / 2);
      g.endFill();
      cwGloss(g,- w / 2 + 8,- h / 2 + 2,w - 16,16,8,50);
   }
   else
   {
      cwGradientRect(g,- w / 2,- h / 2,w,h,h / 2,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : CW_RIM,over ? 100 : 75,over ? 2 : 1.5);
      cwGloss(g,- w / 2 + 8,- h / 2 + 2,w - 16,16,8,24);
   }
   p.filters = over ? [new flash.filters.GlowFilter(CW_GOLD,0.75,16,16,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
}

function cwMakePill(name, label, gold, answer)
{
   var p = cwWin.createEmptyMovieClip(name,cwWin.getNextHighestDepth());
   p.cwGold = gold;
   p.bg = p.createEmptyMovieClip("bg",1);
   var t = cwText(p,"label_txt",label,- CW_PILL_W / 2,-13,CW_PILL_W,28,19,gold ? CW_INK : 0xFFFFFF);
   if(!gold)
   {
      t.filters = cwOutline();
   }
   cwDrawPill(p,false);
   p.onRollOver = function()
   {
      cwDrawPill(this,true);
      cwWin.m_OnRollOver();
      gs.TweenLite.to(this,0.3,{_xscale:106,_yscale:106,ease:cwStrongOut});
   };
   p.onRollOut = p.onReleaseOutside = function()
   {
      cwDrawPill(this,false);
      gs.TweenLite.to(this,0.3,{_xscale:100,_yscale:100,ease:cwStrongOut});
   };
   p.onRelease = function()
   {
      cwWin[answer]();
   };
   return p;
}

// The pills sit where resize() put the stock buttons, centered on each.
function cwPlacePills()
{
   var list = [[cwWin.cwYes,cwWin.m_YesButton],[cwWin.cwNo,cwWin.m_NoButton]];
   var i;
   for(i = 0; i < list.length; i++)
   {
      var b = list[i][1].getBounds(cwWin);
      if(b.xMax > b.xMin)
      {
         list[i][0]._x = Math.round((b.xMin + b.xMax) / 2);
         list[i][0]._y = Math.round((b.yMin + b.yMax) / 2);
      }
   }
   // Keep them apart: resize() spaces the stock buttons by their own (narrower) width.
   var c = cwWin.m_Background._width / 2;
   cwWin.cwYes._x = Math.min(cwWin.cwYes._x,c - CW_PILL_W / 2 - 10);
   cwWin.cwNo._x = Math.max(cwWin.cwNo._x,c + CW_PILL_W / 2 + 10);
}

function cwBuild()
{
   cwWin.m_Background._alpha = 0;
   cwWin.cwCard = cwWin.createEmptyMovieClip("cwCard",-16384);
   cwWin.m_Message.filters = cwOutline();
   cwMakePill("cwYes","Yes",true,"accepted");
   cwMakePill("cwNo","No",false,"declined");
   // Hidden through CLIK's own visible too, so a redraw doesn't bring them back.
   cwWin.m_YesButton.visible = false;
   cwWin.m_NoButton.visible = false;
   cwWin.m_YesButton._visible = false;
   cwWin.m_NoButton._visible = false;
   cwDrawCard();
   cwPlacePills();

   var proto = _global.ConfirmationWindow.prototype;
   var resize = proto.resize;
   proto.resize = function()
   {
      var r = resize.apply(this,arguments);
      cwDrawCard();
      cwPlacePills();
      return r;
   };
}

cwBuild();
// ===== End Sanctuary confirmation =====

// ===== Sanctuary Target Frame =====
// Appended to target's main timeline by build_client_mods.py.
// The target frame (the selected creature's name and health, wndTarget, 398x45) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass pill with a gold medallion holding a reticle at its left
// end, a recessed track with a glossy red health bar that slides to each new value and leaves a gold trail
// behind when health drops, and the name in outlined white over the bar. Lua still drives the stock setTarget and
// setHealth; they're wrapped to move the new bar and follow the stock show/hide. The stock frame and bar are
// made invisible (they stay where they were, so nothing that measures them changes).

var TG_W = 398;
var TG_H = 45;
var TG_RIM = 0x9DB8FF;
var TG_GOLD = 0xFFC531;
var TG_INK = 0x3A2400;
// The medallion and the bar's track, in stage coordinates.
var TG_MEDAL_X = 23;
var TG_MEDAL_Y = 22.5;
var TG_MEDAL_R = 17;
var TG_BAR_X = 46;
var TG_BAR_Y = 12;
var TG_BAR_W = 340;
var TG_BAR_H = 21;

// --- drawing helpers -------------------------------------------------------------------

function tgRoundRect(mc, x, y, w, h, r)
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

function tgCircle(mc, x, y, r)
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

function tgGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   tgRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function tgSparkle(mc, x, y, r, alpha)
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

// --- the frame -------------------------------------------------------------------------

function tgBuild()
{
   var f = _root.createEmptyMovieClip("sanctuaryTarget",-16384);

   // Navy glass pill.
   var g = f.createEmptyMovieClip("glass",1);
   g.lineStyle(2,TG_RIM,80);
   g.beginGradientFill("linear",[0x2E4FB0,0x141F55],[96,96],[0,255],{matrixType:"box",x:0,y:0,w:TG_W,h:TG_H,r:Math.PI / 2});
   tgRoundRect(g,1.5,1.5,TG_W - 3,TG_H - 3,(TG_H - 3) / 2);
   g.endFill();
   g.lineStyle();
   g.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[30,0],[0,255],{matrixType:"box",x:-40,y:-50,w:240,h:110,r:0});
   tgRoundRect(g,3,3,TG_W - 6,TG_H - 6,(TG_H - 6) / 2);
   g.endFill();
   tgGloss(g,20,3,TG_W - 40,15,7,18);
   var s = 1717;
   var i;
   for(i = 0; i < 5; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = TG_BAR_X + 30 + s % (TG_BAR_W - 60);
      s = (s * 1103515245 + 12345) % 2147483648;
      tgSparkle(g,sx,s % 2 == 0 ? 6 : TG_H - 6,1,20 + s % 25);
   }

   // The track the bar runs in.
   var t = f.createEmptyMovieClip("track",2);
   t.lineStyle(1,TG_RIM,30);
   t.beginGradientFill("linear",[0x050A24,0x0A1440],[70,50],[0,255],{matrixType:"box",x:TG_BAR_X,y:TG_BAR_Y,w:TG_BAR_W,h:TG_BAR_H,r:Math.PI / 2});
   tgRoundRect(t,TG_BAR_X - 2,TG_BAR_Y - 2,TG_BAR_W + 4,TG_BAR_H + 4,(TG_BAR_H + 4) / 2);
   t.endFill();

   // The gold trail (where health was) under the red bar.
   f.trail = f.createEmptyMovieClip("trail",3);
   f.trail.ratio = 1;
   f.bar = f.createEmptyMovieClip("bar",4);
   f.bar.ratio = 1;

   // The medallion: a gold coin with a dark reticle.
   var m = f.createEmptyMovieClip("medal",5);
   m._x = TG_MEDAL_X;
   m._y = TG_MEDAL_Y;
   m.lineStyle(1.5,0xFFF3C4,100);
   m.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-TG_MEDAL_R,y:-TG_MEDAL_R,w:TG_MEDAL_R * 2,h:TG_MEDAL_R * 2,r:Math.PI / 2});
   tgCircle(m,0,0,TG_MEDAL_R);
   m.endFill();
   tgGloss(m,-TG_MEDAL_R * 0.65,-TG_MEDAL_R * 0.85,TG_MEDAL_R * 1.3,TG_MEDAL_R * 0.7,TG_MEDAL_R * 0.3,55);
   m.lineStyle(2,TG_INK,100);
   tgCircle(m,0,0,7.5);
   m.moveTo(0,-11.5);
   m.lineTo(0,-4.5);
   m.moveTo(0,4.5);
   m.lineTo(0,11.5);
   m.moveTo(-11.5,0);
   m.lineTo(-4.5,0);
   m.moveTo(4.5,0);
   m.lineTo(11.5,0);
   m.lineStyle();
   m.beginFill(0xD62F2F,100);
   tgCircle(m,0,0,2.5);
   m.endFill();
   m.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,5,5,1,2)];

   tgDrawBar(f.trail);
   tgDrawBar(f.bar);
   return f;
}

// Draws a bar clip at its ratio: the gold trail or the glossy red health bar.
function tgDrawBar(bar)
{
   bar.clear();
   var w = Math.round(TG_BAR_W * Math.max(0,Math.min(1,bar.ratio)));
   if(w < 2)
   {
      return;
   }
   var h = TG_BAR_H;
   var r = Math.min(h / 2,w / 2);
   bar.lineStyle();
   if(bar._name == "trail")
   {
      bar.beginGradientFill("linear",[0xFFF3C4,0xF5A623],[85,85],[0,255],{matrixType:"box",x:TG_BAR_X,y:TG_BAR_Y,w:TG_BAR_W,h:h,r:Math.PI / 2});
      tgRoundRect(bar,TG_BAR_X,TG_BAR_Y,w,h,r);
      bar.endFill();
      return;
   }
   bar.beginGradientFill("linear",[0xFF8A7A,0xD62F2F,0x8E1616],[100,100,100],[0,140,255],{matrixType:"box",x:TG_BAR_X,y:TG_BAR_Y,w:TG_BAR_W,h:h,r:Math.PI / 2});
   tgRoundRect(bar,TG_BAR_X,TG_BAR_Y,w,h,r);
   bar.endFill();
   if(w > 10)
   {
      tgGloss(bar,TG_BAR_X + 3,TG_BAR_Y + 2,w - 6,h * 0.45,h * 0.22,45);
   }
}

function tgSetRatio(ratio)
{
   var f = _root.sanctuaryTarget;
   if(isNaN(ratio))
   {
      return;
   }
   ratio = Math.max(0,Math.min(1,ratio));
   // Healing: both bars grow together. Damage: the red bar drops at once and the gold trail follows.
   gs.TweenLite.to(f.bar,0.3,{ratio:ratio,onUpdate:tgDrawBar,onUpdateParams:[f.bar]});
   if(ratio >= f.trail.ratio)
   {
      gs.TweenLite.to(f.trail,0.3,{ratio:ratio,onUpdate:tgDrawBar,onUpdateParams:[f.trail]});
   }
   else
   {
      gs.TweenLite.to(f.trail,0.6,{ratio:ratio,delay:0.35,onUpdate:tgDrawBar,onUpdateParams:[f.trail]});
   }
}

function tgStyleName()
{
   mcTxt.txt.textColor = 0xFFFFFF;
   mcTxt.txt.filters = [new flash.filters.GlowFilter(0x0A1030,0.95,3,3,5,2)];
}

var tgFrame = tgBuild();
mcBG._alpha = 0;
mcBar._alpha = 0;
// The name sits centered on the bar (the field is centered text the width of the stock bar).
mcTxt._x += TG_BAR_X + TG_BAR_W / 2 - (mcTxt._x + mcTxt.txt._x + mcTxt.txt._width / 2);
tgStyleName();

// The name is restyled after Lua sets it (frSetTextPreserveWidth resets its format).
var tgSetTarget = setTarget;
setTarget = function()
{
   var result = tgSetTarget.apply(_root,arguments);
   tgStyleName();
   return result;
};

var tgSetHealth = setHealth;
setHealth = function(_hp, _max)
{
   var result = tgSetHealth.apply(_root,arguments);
   tgFrame._visible = mcBG._visible;
   if(mcBG._visible)
   {
      tgSetRatio(_hp / _max);
   }
   else
   {
      // Hidden: snap, so the next target doesn't animate from this one.
      gs.TweenLite.killTweensOf(tgFrame.bar);
      gs.TweenLite.killTweensOf(tgFrame.trail);
      tgFrame.bar.ratio = tgFrame.trail.ratio = 1;
      tgDrawBar(tgFrame.bar);
      tgDrawBar(tgFrame.trail);
   }
   return result;
};
// ===== End Sanctuary Target Frame =====

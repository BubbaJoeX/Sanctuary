// ===== Sanctuary Action Progress =====
// Appended to actionProgress's main timeline by build_client_mods.py.
// The action progress bar (wndActionProgress: the farm's timed actions, such as planting, tending and harvesting,
// started by FactoryHandler.CreateActionProgressBar) in the Main Menu's style (ClientMods/MyStuffBrowser/
// main_menu.as), as a HUD piece without the window shell: a navy glass capsule with a gold rim around a dark
// recessed track, a glossy rounded green fill carrying moving stripes while it runs, and the seconds left in
// clean white text. When the action completes the fill turns gold and flashes. The stock track and its gloss are
// root shapes dropped at build time (REMOVED_DEPTHS); greenBar_mc stays (invisible) as the stock code's clock,
// and the skin follows its width every frame.

var AP_W = 300;
var AP_H = 50;
var AP_GOLD = 0xFFC531;
var AP_RIM = 0x9DB8FF;
// The track inside the capsule.
var AP_TRACK_X = 14;
var AP_TRACK_Y = 15;
var AP_TRACK_W = 272;
var AP_TRACK_H = 20;
// Fill colours: light, mid and dark (green while running, gold when done).
var AP_GREEN = [0xC4F59A,0x5DBE35,0x2E7A16];
var AP_DONE = [0xFFF1B8,0xFFC531,0xD98A10];

var apBar;

// --- drawing helpers -------------------------------------------------------------------

function apRoundRect(mc, x, y, w, h, r)
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

function apGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   apRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function apText(mc, name, txt, x, y, w, h, size, color, align)
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

function apSparkle(mc, x, y, r, alpha)
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

// --- the bar ---------------------------------------------------------------------------

function apDrawCapsule()
{
   var bg = apBar.bg;
   bg.clear();
   apGradientRect(bg,3,5,AP_W - 6,AP_H - 10,(AP_H - 10) / 2,0x2E4FB0,0x141F55,100,0xFFE08A,90,2.5);
   // Glassy sheen across the top.
   bg.lineStyle();
   bg.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[26,0],[0,255],{matrixType:"box",x:0,y:7,w:10,h:16,r:Math.PI / 2});
   apRoundRect(bg,16,7,AP_W - 32,14,7);
   bg.endFill();

   // The dark recessed track.
   var t = apBar.track;
   t.clear();
   t.lineStyle(1.5,AP_RIM,30);
   t.beginGradientFill("linear",[0x050A24,0x0E1A52],[90,90],[0,255],{matrixType:"box",x:AP_TRACK_X,y:AP_TRACK_Y,w:AP_TRACK_W,h:AP_TRACK_H,r:Math.PI / 2});
   apRoundRect(t,AP_TRACK_X,AP_TRACK_Y,AP_TRACK_W,AP_TRACK_H,AP_TRACK_H / 2);
   t.endFill();
   t.lineStyle();
   t.beginGradientFill("linear",[0x000000,0x000000],[45,0],[0,255],{matrixType:"box",x:AP_TRACK_X,y:AP_TRACK_Y,w:AP_TRACK_W,h:AP_TRACK_H * 0.5,r:Math.PI / 2});
   apRoundRect(t,AP_TRACK_X + 1,AP_TRACK_Y + 1,AP_TRACK_W - 2,AP_TRACK_H * 0.5,AP_TRACK_H * 0.25);
   t.endFill();
}

// A glossy rounded fill w wide: light top, the colour, a darker base, and a white sheen on the upper half.
function apDrawFill(w, colors)
{
   var f = apBar.fill;
   var m = apBar.mask;
   var x = AP_TRACK_X;
   var y = AP_TRACK_Y;
   var h = AP_TRACK_H;
   var r = h / 2;
   f.clear();
   m.clear();
   if(w <= 0)
   {
      return;
   }
   w = Math.max(h,w);
   f.lineStyle(1,colors[0],100);
   f.beginGradientFill("linear",[colors[0],colors[1],colors[2]],[100,100,100],[0,110,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   apRoundRect(f,x,y,w,h,r);
   f.endFill();
   f.lineStyle();
   f.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[60,5],[0,255],{matrixType:"box",x:x,y:y + 2,w:w,h:h * 0.42,r:Math.PI / 2});
   apRoundRect(f,x + r * 0.5,y + 2,w - r,h * 0.42,h * 0.2);
   f.endFill();
   f.filters = [new flash.filters.GlowFilter(colors[1],0.5,10,10,1,2)];
   m.beginFill(0,100);
   apRoundRect(m,x,y,w,h,r);
   m.endFill();
}

// Diagonal stripes, a period wider than the track so they can scroll.
function apDrawStripes()
{
   var s = apBar.stripes;
   s.clear();
   s.lineStyle();
   var x;
   for(x = AP_TRACK_X - 24; x < AP_TRACK_X + AP_TRACK_W + 24; x += 14)
   {
      s.beginFill(0xFFFFFF,100);
      s.moveTo(x,AP_TRACK_Y + AP_TRACK_H);
      s.lineTo(x + 7,AP_TRACK_Y + AP_TRACK_H);
      s.lineTo(x + 7 + AP_TRACK_H * 0.6,AP_TRACK_Y);
      s.lineTo(x + AP_TRACK_H * 0.6,AP_TRACK_Y);
      s.lineTo(x,AP_TRACK_Y + AP_TRACK_H);
      s.endFill();
   }
}

// Seconds left, from the stock clock (m_startTime, m_totalTime): "3.4s" under ten seconds, "12s" above.
function apTimeLeft(pct)
{
   var total = Number(m_totalTime);
   if(isNaN(total) || total <= 0 || m_startTime == undefined)
   {
      return "";
   }
   if(pct >= 100)
   {
      return "Done!";
   }
   var left = total - (new Date().getTime() - m_startTime) / 1000;
   left = Math.max(0,left);
   if(left < 10)
   {
      return (Math.ceil(left * 10) / 10) + "s";
   }
   return Math.ceil(left) + "s";
}

function apUpdate()
{
   var pct = Math.max(0,Math.min(100,greenBar_mc._xscale));
   if(isNaN(pct))
   {
      pct = 0;
   }
   var done = pct >= 100;
   var w = Math.round(AP_TRACK_W * pct / 100);
   if(w != apBar.lastW || done != apBar.done)
   {
      apDrawFill(w,done ? AP_DONE : AP_GREEN);
      if(done && !apBar.done)
      {
         // A gold flash as it completes.
         apBar.flash._alpha = 100;
      }
      apBar.lastW = w;
      apBar.done = done;
   }
   // The movie has no tween library: the flash fades and a restarted bar fades in by hand.
   if(apBar.flash._alpha > 0)
   {
      apBar.flash._alpha = Math.max(0,apBar.flash._alpha - 6);
   }
   if(apBar._alpha < 100)
   {
      apBar._alpha = Math.min(100,apBar._alpha + 12);
   }
   apBar.stripes._visible = w > 0 && !done;
   apBar.stripes._x = (apBar.stripes._x + 0.9) % 14;
   // The spark at the fill's head.
   apBar.spark._visible = w > 6 && !done;
   apBar.spark._x = AP_TRACK_X + Math.max(AP_TRACK_H,w) - 6;
   apBar.spark._rotation += 6;
   var label = apTimeLeft(pct);
   if(label != apBar.time_txt.text)
   {
      apBar.time_txt.text = label;
      apBar.time_txt.textColor = done ? 0xFFE08A : 0xFFFFFF;
   }
}

function apBuild()
{
   // The stock fill stays (invisible): setTimeLeft scales it, and the skin reads it.
   greenBar_mc._alpha = 0;

   apBar = _root.createEmptyMovieClip("sanctuaryActionProgress",-16384);
   apBar.bg = apBar.createEmptyMovieClip("bg",1);
   apBar.bg.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.6,12,12,1,2)];
   apBar.track = apBar.createEmptyMovieClip("track",2);
   apBar.fill = apBar.createEmptyMovieClip("fill",3);
   apBar.stripes = apBar.createEmptyMovieClip("stripes",4);
   apBar.stripes._alpha = 20;
   apBar.mask = apBar.createEmptyMovieClip("mask",5);
   apBar.stripes.setMask(apBar.mask);
   apBar.spark = apBar.createEmptyMovieClip("spark",6);
   apSparkle(apBar.spark,0,0,3,90);
   apBar.spark._y = AP_TRACK_Y + AP_TRACK_H / 2;
   apBar.spark.filters = [new flash.filters.GlowFilter(0xFFFFFF,0.8,8,8,2,2)];
   apBar.flash = apBar.createEmptyMovieClip("flash",7);
   apBar.flash.beginFill(0xFFF3C4,60);
   apRoundRect(apBar.flash,3,5,AP_W - 6,AP_H - 10,(AP_H - 10) / 2);
   apBar.flash.endFill();
   apBar.flash._alpha = 0;
   apBar.time_txt = apText(apBar,"time_txt","",AP_TRACK_X,AP_TRACK_Y - 1,AP_TRACK_W,22,15,0xFFFFFF);
   apBar.time_txt.filters = [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
   apDrawCapsule();
   apDrawStripes();
   apBar.onEnterFrame = apUpdate;
   apUpdate();
}

// A new action restarts the bar (setTime_lua): back to green from the start.
var apSetTime = setTime_lua;
setTime_lua = function(seconds)
{
   var result = apSetTime.apply(_root,arguments);
   apBar.done = false;
   apBar.lastW = -1;
   apBar._alpha = 0;
   apUpdate();
   return result;
};

apBuild();
// ===== End Sanctuary Action Progress =====

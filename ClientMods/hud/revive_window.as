// ===== Sanctuary Revive Window =====
// Appended to revive_window's main timeline by build_client_mods.py.
// The knocked-out window (wndRespawn, 360x220) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as):
// a navy glass panel with sparkles, the stock starburst with "You got knocked out!" in white over a navy shadow,
// the countdown in a frosted dial with a gold rim inside a navy recess, and the Recover Now / Revive Here / Revive at
// safe location buttons as gold pills (lighter with a glow on hover, deeper when pressed). The stock panel is a root
// shape dropped at build time (REMOVED_DEPTHS); the timer disc is an unnamed shape inside respawnDisplay, so the
// dial is drawn over it in the free timeline depth just above. The buttons keep their FRIconButton behaviour (their
// stock state plates are made invisible and the gold pill is drawn in each state clip).

var RV_W = 360;
var RV_H = 220;
var RV_RIM = 0x9DB8FF;
var RV_GOLD = 0xFFC531;
var RV_INK = 0x3A2400;
var RV_SOFT = 0xC9D6FF;
// The countdown disc in respawnDisplay (the stock 75px disc at 30, 30.4, scaled 1.133).
var RV_DIAL_X = 72.5;
var RV_DIAL_Y = 72.9;
var RV_DIAL_R = 41;

// --- drawing helpers -------------------------------------------------------------------

function rvRoundRect(mc, x, y, w, h, r)
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

function rvCircle(mc, x, y, r)
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

function rvGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   rvRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function rvSparkle(mc, x, y, r, alpha)
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

function rvOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// --- the panel -------------------------------------------------------------------------

function rvBuildPanel()
{
   var p = _root.createEmptyMovieClip("sanctuaryRevive",-16384);
   var x = 4;
   var y = 4;
   var w = RV_W - 8;
   var h = RV_H - 10;
   p.lineStyle(3,RV_RIM,80);
   p.beginGradientFill("linear",[0x2E4FB0,0x141F55],[97,97],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   rvRoundRect(p,x,y,w,h,28);
   p.endFill();
   p.lineStyle();
   p.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[36,0],[0,255],{matrixType:"box",x:-70,y:-90,w:280,h:240,r:0});
   rvRoundRect(p,x + 3,y + 3,w - 6,h - 6,25);
   p.endFill();
   rvGloss(p,x + 24,y + 4,w - 48,26,12,16);

   // The recess the countdown and buttons slide through (the stock respawnMask's area).
   var b = respawnMask.getBounds(_root);
   p.lineStyle(1.5,RV_RIM,28);
   p.beginGradientFill("linear",[0x0A1440,0x0A1440],[50,32],[0,255],{matrixType:"box",x:b.xMin,y:b.yMin,w:b.xMax - b.xMin,h:b.yMax - b.yMin,r:Math.PI / 2});
   rvRoundRect(p,b.xMin - 4,b.yMin - 2,b.xMax - b.xMin + 8,b.yMax - b.yMin + 4,18);
   p.endFill();

   var s = 9090;
   var i;
   for(i = 0; i < 8; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = 20 + s % 320;
      s = (s * 1103515245 + 12345) % 2147483648;
      rvSparkle(p,sx,12 + s % 18,1 + s % 2,18 + s % 32);
   }
   p.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,10,10,1,2)];
}

// The countdown dial: a frosted disc with a gold rim over the stock glass disc (the red numerals read well on it).
function rvBuildDial()
{
   var d = respawnDisplay.createEmptyMovieClip("sanctuaryDial",-16382);
   d.lineStyle();
   d.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:RV_DIAL_X - RV_DIAL_R - 4,y:RV_DIAL_Y - RV_DIAL_R - 4,w:RV_DIAL_R * 2 + 8,h:RV_DIAL_R * 2 + 8,r:Math.PI / 2});
   rvCircle(d,RV_DIAL_X,RV_DIAL_Y,RV_DIAL_R + 4);
   d.endFill();
   d.lineStyle(1.5,0xFFF3C4,100);
   d.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:RV_DIAL_X - RV_DIAL_R * 1.3,y:RV_DIAL_Y - RV_DIAL_R * 1.4,w:RV_DIAL_R * 2.4,h:RV_DIAL_R * 2.6,r:0});
   rvCircle(d,RV_DIAL_X,RV_DIAL_Y,RV_DIAL_R - 1);
   d.endFill();
   rvGloss(d,RV_DIAL_X - RV_DIAL_R * 0.62,RV_DIAL_Y - RV_DIAL_R * 0.88,RV_DIAL_R * 1.24,RV_DIAL_R * 0.6,RV_DIAL_R * 0.3,60);
   d.filters = [new flash.filters.GlowFilter(RV_GOLD,0.5,12,12,2,2)];
}

// A gold pill in each state clip of an FRIconButton (up, over: lighter and glowing, down: deeper).
function rvStyleButton(btn)
{
   var states = [btn.up_mc,btn.over_mc,btn.down_mc];
   var i;
   for(i = 0; i < states.length; i++)
   {
      var st = states[i];
      var plate = st.getInstanceAtDepth(-16383);
      if(st == undefined || plate == undefined)
      {
         continue;
      }
      plate._alpha = 0;
      var pill = st.sanctuaryPill;
      if(pill == undefined)
      {
         pill = st.createEmptyMovieClip("sanctuaryPill",-16384);
         var b = plate.getBounds(st);
         var x = b.xMin + 2;
         var y = b.yMin + 2;
         var w = b.xMax - b.xMin - 4;
         var h = b.yMax - b.yMin - 4;
         var r = Math.min(h / 2,20);
         var top = i == 1 ? 0xFFF0B0 : i == 2 ? 0xF5B23A : 0xFFE08A;
         var bottom = i == 1 ? 0xFFB838 : i == 2 ? 0xD98A12 : 0xF5A623;
         pill.lineStyle(1.5,0xFFF3C4,100);
         pill.beginGradientFill("linear",[top,bottom],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
         rvRoundRect(pill,x,y,w,h,r);
         pill.endFill();
         if(i != 2)
         {
            rvGloss(pill,x + r * 0.6,y + 2,w - r * 1.2,h * 0.42,h * 0.2,55);
         }
         pill.filters = i == 1 ? [new flash.filters.GlowFilter(RV_GOLD,0.85,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
      }
      st.textField_txt.textColor = RV_INK;
      st.textField_txt.filters = [];
   }
}

function rvStyleText()
{
   koTxt.textColor = 0xFFFFFF;
   koShadowTxt.textColor = 0x141F55;
   koTxt.filters = [new flash.filters.GlowFilter(0x0A1030,0.6,4,4,2,2)];
   respawnDisplay.recoverTitle.textColor = 0xFFE08A;
   respawnDisplay.seconds_title.textColor = RV_SOFT;
   respawnDisplay.recoverTitle.filters = rvOutline();
   respawnDisplay.seconds_title.filters = rvOutline();
}

rvBuildPanel();
rvBuildDial();
rvStyleText();
rvStyleButton(respawnDisplay.respawnBtn);
rvStyleButton(twoButtons_mc.paidRevive_mc);
rvStyleButton(twoButtons_mc.safeRevive_mc);

// The two revive buttons get their text from Lua: keep it dark on the gold.
var rvShowTwoButtons = showTwoButtons_lua;
showTwoButtons_lua = function()
{
   var result = rvShowTwoButtons.apply(_root,arguments);
   rvStyleButton(twoButtons_mc.paidRevive_mc);
   rvStyleButton(twoButtons_mc.safeRevive_mc);
   return result;
};
// ===== End Sanctuary Revive Window =====

// ===== Sanctuary Progress Bar =====
// Appended to FRProgressBar's main timeline by build_client_mods.py.
// The red, green or blue progress bar with an icon (wndFRProgressBar and the pet's wndPetProgressBar, same movie) in
// the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass pill, the icon in a frosted socket
// with a gold rim, and a recessed track with a glossy fill in the mode's colour (setMode: 1 red, 2 green, 3 blue)
// that follows the stock mask as setProgress tweens it, with a light sweep along it. The stock plate, bar and overlay
// are made invisible (the stock mask and its tween keep running and the fill reads its scale).

var PB_W = 264;
var PB_H = 50;
var PB_RIM = 0x9DB8FF;
var PB_GOLD_LIGHT = 0xFFE08A;
// The stock bar's visible span (the mask's 221px from x 29.65) and its height.
var PB_TRACK_X = 30;
var PB_TRACK_Y = 11;
var PB_TRACK_W = 220;
var PB_TRACK_H = 25;

// Fill gradients (top, middle, bottom) per stock mode; 0 (never set) shows red like the stock bar.
var PB_COLORS = [[0xFF8A7A,0xD62F2F,0x8E1616],[0xFF8A7A,0xD62F2F,0x8E1616],[0xC8F27A,0x58B82A,0x2C7A12],[0x9AD6FF,0x3A86E8,0x1A4FA8]];

function pbRoundRect(mc, x, y, w, h, r)
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

function pbCircle(mc, x, y, r)
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

function pbGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   pbRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function pbBuild()
{
   bg._alpha = 0;
   bar._alpha = 0;
   barO._alpha = 0;

   var f = _root.createEmptyMovieClip("sanctuaryBar",-16384);
   var g = f.createEmptyMovieClip("glass",1);
   g.lineStyle(2.5,PB_RIM,80);
   g.beginGradientFill("linear",[0x2E4FB0,0x141F55],[96,96],[0,255],{matrixType:"box",x:0,y:0,w:PB_W,h:PB_H,r:Math.PI / 2});
   pbRoundRect(g,2,2,PB_W - 4,PB_H - 4,(PB_H - 4) / 2);
   g.endFill();
   g.lineStyle();
   g.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[30,0],[0,255],{matrixType:"box",x:-30,y:-50,w:170,h:110,r:0});
   pbRoundRect(g,4,4,PB_W - 8,PB_H - 8,(PB_H - 8) / 2);
   g.endFill();
   pbGloss(g,22,4,PB_W - 44,13,6,18);
   g.lineStyle(1,PB_RIM,30);
   g.beginGradientFill("linear",[0x050A24,0x0A1440],[75,50],[0,255],{matrixType:"box",x:PB_TRACK_X,y:PB_TRACK_Y,w:PB_TRACK_W,h:PB_TRACK_H,r:Math.PI / 2});
   pbRoundRect(g,PB_TRACK_X - 2,PB_TRACK_Y - 2,PB_TRACK_W + 4,PB_TRACK_H + 4,(PB_TRACK_H + 4) / 2);
   g.endFill();

   f.fill = f.createEmptyMovieClip("fill",2);
   f.sweep = f.createEmptyMovieClip("sweep",3);
   f.sweep.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF,0xFFFFFF],[0,50,0],[0,128,255],{matrixType:"box",x:-30,y:0,w:60,h:PB_TRACK_H,r:0});
   f.sweep.moveTo(-30,PB_TRACK_Y);
   f.sweep.lineTo(30,PB_TRACK_Y);
   f.sweep.lineTo(30,PB_TRACK_Y + PB_TRACK_H);
   f.sweep.lineTo(-30,PB_TRACK_Y + PB_TRACK_H);
   f.sweep.lineTo(-30,PB_TRACK_Y);
   f.sweep.endFill();
   f.sweepMask = f.createEmptyMovieClip("sweepMask",4);
   f.sweep.setMask(f.sweepMask);

   // The icon's socket, under the stock icon (iconBG, a timeline clip above this layer).
   var ib = iconBG.getBounds(_root);
   var cx = (ib.xMin + ib.xMax) / 2;
   var cy = (ib.yMin + ib.yMax) / 2;
   var r = Math.max(ib.xMax - ib.xMin,ib.yMax - ib.yMin) / 2 + 3;
   var s = f.createEmptyMovieClip("socket",5);
   s.lineStyle(2,PB_GOLD_LIGHT,100);
   s.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:cx - r * 1.3,y:cy - r * 1.4,w:r * 2.4,h:r * 2.6,r:0});
   pbCircle(s,cx,cy,r);
   s.endFill();
   pbGloss(s,cx - r * 0.62,cy - r * 0.85,r * 1.24,r * 0.6,r * 0.3,55);
   s.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,5,5,1,2)];

   f.ratio = -1;
   f.mode = -1;
   f.onEnterFrame = pbUpdate;
}

// Follows the stock mask's scale (setProgress tweens it) and the mode.
function pbUpdate()
{
   var ratio = Math.max(0,Math.min(1,mask_mc._xscale / 100));
   if(ratio > 0.05)
   {
      this.sweep._x += 5;
      if(this.sweep._x > PB_TRACK_X + PB_TRACK_W * ratio + 30)
      {
         this.sweep._x = PB_TRACK_X - 30;
      }
   }
   var m = mode >= 1 && mode <= 3 ? mode : 0;
   if(ratio == this.ratio && m == this.mode)
   {
      return;
   }
   this.ratio = ratio;
   this.mode = m;
   var fill = this.fill;
   fill.clear();
   this.sweepMask.clear();
   var w = Math.round(PB_TRACK_W * ratio);
   if(w < 3)
   {
      return;
   }
   var h = PB_TRACK_H;
   var r = Math.min(h / 2,w / 2);
   var c = PB_COLORS[m];
   fill.lineStyle();
   fill.beginGradientFill("linear",c,[100,100,100],[0,140,255],{matrixType:"box",x:PB_TRACK_X,y:PB_TRACK_Y,w:PB_TRACK_W,h:h,r:Math.PI / 2});
   pbRoundRect(fill,PB_TRACK_X,PB_TRACK_Y,w,h,r);
   fill.endFill();
   if(w > 10)
   {
      pbGloss(fill,PB_TRACK_X + 4,PB_TRACK_Y + 2,w - 8,h * 0.42,h * 0.2,50);
   }
   this.sweepMask.beginFill(0,100);
   pbRoundRect(this.sweepMask,PB_TRACK_X,PB_TRACK_Y,w,h,r);
   this.sweepMask.endFill();
}

pbBuild();
// ===== End Sanctuary Progress Bar =====

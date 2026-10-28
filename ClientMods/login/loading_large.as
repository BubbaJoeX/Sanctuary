// ===== Sanctuary Large Loading =====
// Appended to loading_large's main timeline by build_client_mods.py (which has none, so FRAME_SCRIPTS gives it an
// empty one). The large "Loading" indicator (HUD:ShowLargeLoading, e.g. while the character creator builds the
// hero) in the Sanctuary loading screen's colours: a navy glass card with a glass spinner disc, a gold arc
// running round a frosted centre that holds the Free Realms tree in the OSFR loading screen's blue
// (sanctuaryLoadingTree, TEXTURE_SYMBOLS), and the stock "Loading" line in white with the dark outline.
// The stock orange ring is hidden. Nothing here needs the world to be loaded.

var LL_W = 350;
var LL_H = 170;
var LL_SPIN_Y = 60;
var LL_RIM = 0x9DB8FF;
var LL_GOLD = 0xFFC531;

// --- drawing helpers -------------------------------------------------------------------

function llRoundRect(mc, x, y, w, h, r)
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

function llCircle(mc, x, y, r)
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

function llSparkle(mc, x, y, r, alpha)
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

// The loading spinner shared by the loading widgets: a glass disc, a gold arc that fades to its tail and spins,
// and the tree (or, without the symbol, a gold sparkle) in a frosted centre that gently breathes.
function llSpinner(parent, name, depth, cx, cy, r)
{
   var s = parent.createEmptyMovieClip(name,depth);
   s._x = cx;
   s._y = cy;
   var d = s.createEmptyMovieClip("disc",1);
   d.lineStyle();
   d.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[45,0],[0,255],{matrixType:"box",x:- r * 1.7,y:- r * 1.7,w:r * 3.4,h:r * 3.4,r:0});
   llCircle(d,0,0,r * 1.7);
   d.endFill();
   d.lineStyle(2,LL_RIM,80);
   d.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   llCircle(d,0,0,r);
   d.endFill();
   d.lineStyle(r * 0.16,0x0A1440,70);
   llCircle(d,0,0,r * 0.78);
   d.lineStyle(2,0xFFFFFF,90);
   d.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:- r * 0.75,y:- r * 0.8,w:r * 1.4,h:r * 1.4,r:0});
   llCircle(d,0,0,r * 0.58);
   d.endFill();

   // The arc: 140 degrees, solid gold at its head, fading out toward its tail.
   var a = s.createEmptyMovieClip("arc",2);
   var ar = r * 0.78;
   var step = 10 * Math.PI / 180;
   var i;
   for(i = 0; i < 14; i++)
   {
      var t0 = i * step;
      var t1 = t0 + step;
      var tm = t0 + step / 2;
      var cr = ar / Math.cos(step / 2);
      a.lineStyle(r * 0.16,i > 10 ? 0xFFE08A : LL_GOLD,8 + i * 7);
      a.moveTo(Math.cos(t0) * ar,Math.sin(t0) * ar);
      a.curveTo(Math.cos(tm) * cr,Math.sin(tm) * cr,Math.cos(t1) * ar,Math.sin(t1) * ar);
   }
   llSparkle(a,Math.cos(14 * step) * ar,Math.sin(14 * step) * ar,r * 0.09,100);
   a.filters = [new flash.filters.GlowFilter(LL_GOLD,0.7,8,8,2,2)];
   a.onEnterFrame = function()
   {
      this._rotation += 9;
   };

   var c = s.createEmptyMovieClip("centre",3);
   var tree = c.attachMovie("sanctuaryLoadingTree","tree",1);
   if(tree != undefined)
   {
      tree._width = tree._height = r * 0.86;
      tree._x = tree._y = - r * 0.43;
   }
   else
   {
      c.beginFill(LL_GOLD,100);
      c.moveTo(0,- r * 0.4);
      c.curveTo(0,0,r * 0.4,0);
      c.curveTo(0,0,0,r * 0.4);
      c.curveTo(0,0,- r * 0.4,0);
      c.curveTo(0,0,0,- r * 0.4);
      c.endFill();
   }
   c.t = 0;
   c.onEnterFrame = function()
   {
      this.t += 0.08;
      this._xscale = this._yscale = 100 + 5 * Math.sin(this.t);
   };
   s.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,10,10,1,2)];
   return s;
}

// --- build -----------------------------------------------------------------------------

function llBuild()
{
   var f = _root.createEmptyMovieClip("sanctuaryLargeLoading",-16384);
   var bg = f.createEmptyMovieClip("bg",1);
   bg.lineStyle(3,LL_RIM,80);
   bg.beginGradientFill("linear",[0x2E4FB0,0x141F55],[92,92],[0,255],{matrixType:"box",x:40,y:4,w:270,h:160,r:Math.PI / 2});
   llRoundRect(bg,40,4,LL_W - 80,LL_H - 8,28);
   bg.endFill();
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[30,0],[0,255],{matrixType:"box",x:-20,y:-120,w:300,h:260,r:0});
   llRoundRect(bg,42,6,LL_W - 84,LL_H - 12,26);
   bg.endFill();
   var s = 3434;
   var i;
   for(i = 0; i < 10; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 56 + s % (LL_W - 112);
      s = (s * 1103515245 + 12345) % 2147483648;
      if(x > 110 && x < 240)
      {
         continue;
      }
      llSparkle(bg,x,16 + s % 90,1 + s % 2,25 + s % 50);
   }
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   llSpinner(f,"spinner",2,LL_W / 2,LL_SPIN_Y,40);

   progress._visible = false;
   loading.filters = [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

llBuild();
// ===== End Sanctuary Large Loading =====

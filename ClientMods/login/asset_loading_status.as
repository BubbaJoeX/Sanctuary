// ===== Sanctuary Asset Loading Status =====
// Appended to AssetLoadingStatus's main timeline by build_client_mods.py.
// The "Loading / Please wait" card shown while assets stream in (assetloadingstatus.lua) in the Sanctuary loading
// screen's colours, instead of the stock trooper helmet in a blue ring (art from another game on the same engine):
// a navy glass card, a glass spinner disc with a gold arc running round a frosted centre that holds the Free
// Realms tree in the OSFR loading screen's blue (sanctuaryLoadingTree, TEXTURE_SYMBOLS), "Loading" in gold and
// "Please wait" in soft blue, both outlined. The stock ring and helmet are hidden; the view still centres itself.

var AL_RIM = 0x9DB8FF;
var AL_GOLD = 0xFFC531;
// The stock ring's centre and the card, in the view's coordinates (its texts span 0..365 x 100..195).
var AL_SPIN_X = 181.5;
var AL_SPIN_Y = 53;

// --- drawing helpers -------------------------------------------------------------------

function alRoundRect(mc, x, y, w, h, r)
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

function alCircle(mc, x, y, r)
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

function alSparkle(mc, x, y, r, alpha)
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

// A glass disc, a gold arc that fades to its tail and spins, and the tree (or, without the symbol, a gold
// sparkle) in a frosted centre that gently breathes.
function alSpinner(parent, name, depth, cx, cy, r)
{
   var s = parent.createEmptyMovieClip(name,depth);
   s._x = cx;
   s._y = cy;
   var d = s.createEmptyMovieClip("disc",1);
   d.lineStyle();
   d.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[45,0],[0,255],{matrixType:"box",x:- r * 1.6,y:- r * 1.6,w:r * 3.2,h:r * 3.2,r:0});
   alCircle(d,0,0,r * 1.6);
   d.endFill();
   d.lineStyle(2,AL_RIM,80);
   d.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   alCircle(d,0,0,r);
   d.endFill();
   d.lineStyle(r * 0.16,0x0A1440,70);
   alCircle(d,0,0,r * 0.78);
   d.lineStyle(2,0xFFFFFF,90);
   d.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:- r * 0.75,y:- r * 0.8,w:r * 1.4,h:r * 1.4,r:0});
   alCircle(d,0,0,r * 0.58);
   d.endFill();

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
      a.lineStyle(r * 0.16,i > 10 ? 0xFFE08A : AL_GOLD,8 + i * 7);
      a.moveTo(Math.cos(t0) * ar,Math.sin(t0) * ar);
      a.curveTo(Math.cos(tm) * cr,Math.sin(tm) * cr,Math.cos(t1) * ar,Math.sin(t1) * ar);
   }
   alSparkle(a,Math.cos(14 * step) * ar,Math.sin(14 * step) * ar,r * 0.09,100);
   a.filters = [new flash.filters.GlowFilter(AL_GOLD,0.7,8,8,2,2)];
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
      c.beginFill(AL_GOLD,100);
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

function alRecolor(t, color)
{
   var f = new TextFormat();
   f.color = color;
   t.setTextFormat(f);
   t.filters = [new flash.filters.GlowFilter(0x0A1030,0.9,4,4,4,2)];
}

function alStyleTexts()
{
   alRecolor(mcContent.lblProcessing,0xFFE08A);
   alRecolor(mcContent.txtInfo,0xC9D6FF);
}

function alBuild()
{
   var v = mcContent;
   // The stock ring and helmet (unnamed clips; the disc under them is covered by the spinner).
   var n;
   for(n in v)
   {
      if(typeof v[n] == "movieclip" && v[n]._parent == v)
      {
         v[n]._visible = false;
      }
   }
   var card = v.createEmptyMovieClip("sanctuaryCard",-16384);
   card.lineStyle(3,AL_RIM,80);
   card.beginGradientFill("linear",[0x2E4FB0,0x141F55],[94,94],[0,255],{matrixType:"box",x:-12,y:-16,w:389,h:226,r:Math.PI / 2});
   alRoundRect(card,-12,-16,389,226,30);
   card.endFill();
   card.lineStyle();
   card.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-120,y:-200,w:440,h:380,r:0});
   alRoundRect(card,-10,-14,385,222,28);
   card.endFill();
   var s = 3535;
   var i;
   for(i = 0; i < 16; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 6 + s % 353;
      s = (s * 1103515245 + 12345) % 2147483648;
      if(x > 110 && x < 255)
      {
         continue;
      }
      alSparkle(card,x,-4 + s % 96,1 + s % 3,25 + s % 50);
   }
   card.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   alSpinner(v,"sanctuarySpinner",v.getNextHighestDepth(),AL_SPIN_X,AL_SPIN_Y,46);
   alStyleTexts();
}

// The view sets its two lines from the string table each time it's resized or shown.
var alSwfResize = AssetLoadingView.prototype.swfResize;
AssetLoadingView.prototype.swfResize = function()
{
   alSwfResize.apply(this,arguments);
   alStyleTexts();
};

alBuild();
// ===== End Sanctuary Asset Loading Status =====

// ===== Sanctuary Loading Progress =====
// Appended to loading_progress's main timeline by build_client_mods.py.
// The small download indicator (how many assets are still streaming in) in the Sanctuary loading screen's colours:
// a glass spinner disc with a gold arc round the queue count (gold, outlined), and the hover tooltip ("12
// loading...") as a navy glass pill. The stock ring stays as the invisible hover area that shows the tooltip.

var LP_RIM = 0x9DB8FF;
var LP_GOLD = 0xFFC531;

// --- drawing helpers -------------------------------------------------------------------

function lpRoundRect(mc, x, y, w, h, r)
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

function lpCircle(mc, x, y, r)
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

// A glass disc with a gold arc that fades to its tail and spins (the count sits in the middle).
function lpSpinner(parent, name, depth, cx, cy, r)
{
   var s = parent.createEmptyMovieClip(name,depth);
   s._x = cx;
   s._y = cy;
   var d = s.createEmptyMovieClip("disc",1);
   d.lineStyle(1.5,LP_RIM,80);
   d.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   lpCircle(d,0,0,r);
   d.endFill();
   d.lineStyle(r * 0.18,0x0A1440,70);
   lpCircle(d,0,0,r * 0.76);
   d.lineStyle();
   d.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[30,0],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r,r:Math.PI / 2});
   lpRoundRect(d,- r * 0.6,- r * 0.9,r * 1.2,r * 0.6,r * 0.3);
   d.endFill();

   var a = s.createEmptyMovieClip("arc",2);
   var ar = r * 0.76;
   var step = 10 * Math.PI / 180;
   var i;
   for(i = 0; i < 14; i++)
   {
      var t0 = i * step;
      var t1 = t0 + step;
      var tm = t0 + step / 2;
      var cr = ar / Math.cos(step / 2);
      a.lineStyle(r * 0.18,i > 10 ? 0xFFE08A : LP_GOLD,8 + i * 7);
      a.moveTo(Math.cos(t0) * ar,Math.sin(t0) * ar);
      a.curveTo(Math.cos(tm) * cr,Math.sin(tm) * cr,Math.cos(t1) * ar,Math.sin(t1) * ar);
   }
   a.filters = [new flash.filters.GlowFilter(LP_GOLD,0.7,6,6,2,2)];
   a.onEnterFrame = function()
   {
      this._rotation += 9;
   };
   s.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,6,6,1,2)];
   return s;
}

// The tooltip's pill, in the tooltip's coordinates where the stock bar is (setDownloadQueue sizes it 230x40).
function lpDrawTooltip()
{
   var p = tooltip.sanctuaryPill;
   p.clear();
   p.lineStyle(2,LP_RIM,70);
   p.beginGradientFill("linear",[0x2E4FB0,0x141F55],[96,96],[0,255],{matrixType:"box",x:-94,y:10,w:230,h:40,r:Math.PI / 2});
   lpRoundRect(p,-94,10,230,40,20);
   p.endFill();
   p.lineStyle();
   p.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[18,0],[0,255],{matrixType:"box",x:-90,y:12,w:222,h:16,r:Math.PI / 2});
   lpRoundRect(p,-88,12,218,16,8);
   p.endFill();
}

function lpBuild()
{
   var f = _root.createEmptyMovieClip("sanctuaryLoadingProgress",-16384);
   var b = loading.getBounds(_root);
   lpSpinner(f,"spinner",1,(b.xMin + b.xMax) / 2,(b.yMin + b.yMax) / 2,24);
   // The stock ring keeps its hover (it shows the tooltip), unseen.
   loading._alpha = 0;

   var fmt = queue.getTextFormat();
   fmt.color = 0xFFE08A;
   // A little smaller, so three digits fit inside the arc.
   fmt.size = 15;
   queue.setTextFormat(fmt);
   queue.setNewTextFormat(fmt);
   queue.filters = [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
   queue._y += 2;

   tooltip.bg._alpha = 0;
   tooltip.sanctuaryPill = tooltip.createEmptyMovieClip("sanctuaryPill",-16384);
   tooltip.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
   tooltip.info1.filters = [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
   lpDrawTooltip();
}

lpBuild();
// ===== End Sanctuary Loading Progress =====

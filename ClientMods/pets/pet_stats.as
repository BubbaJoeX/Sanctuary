// ===== Sanctuary Pet Stats =====
// Appended to aboutpet's main timeline by build_client_mods.py.
// The pet's Stats card (wndPetDetails, under the name bar while you're with your pet) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass card with the title in gold, and modern rounded bars
// for happiness (gold, the big one) and the three needs (food red, grooming green, play blue), glossy fills in
// dark recessed tracks with the percent beside them. The needs' icons sit in frosted sockets ringed in their
// colour. When a need is rising or falling (setBarMotion) its fill carries moving stripes, as the stock bars
// carried moving arrows. The icons are the movie's own (a root shape); the stock inner panel, title plate and
// bar troughs are root shapes dropped at build time (REMOVED_DEPTHS) and the frame and bars are hidden here.

var PS_W = 361;
var PS_H = 290;
var PS_GOLD = 0xFFC531;
var PS_RIM = 0x9DB8FF;
var PS_SOFT = 0xC9D6FF;
// Where the bars end and the percent column starts.
var PS_BAR_RIGHT = 294;
var PS_PCT_X = 298;
var PS_PCT_W = 50;

// One row per pool (updatePool's poolId: mood, food, groom, play): the icon's center in the stock art, the
// socket radius, where the bar starts, its height, and the fill's light, mid and dark colours.
var PS_ROWS = [
   [50,99,23,80,22,0xFFF1B8,0xFFC531,0xD98A10],
   [70,147,19,96,16,0xFFA394,0xEC4A36,0xA8261A],
   [70,191,19,96,16,0xC4F59A,0x5DBE35,0x2E7A16],
   [70,235.5,19,96,16,0xB4DCFF,0x3D8EF0,0x1A4FA8]
];

var psCard;
var psValues = [100,100,100,100];
var psSpeeds = [0,0,0,0];

// --- drawing helpers -------------------------------------------------------------------

function psRoundRect(mc, x, y, w, h, r)
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

function psGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   psRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function psCircle(mc, x, y, r)
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

function psText(mc, name, txt, x, y, w, h, size, color, align)
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

function psSparkle(mc, x, y, r, alpha)
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

function psOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// --- card ------------------------------------------------------------------------------

function psDrawCard()
{
   var bg = psCard.bg;
   bg.clear();
   psGradientRect(bg,4,4,PS_W - 8,PS_H - 8,26,0x2E4FB0,0x141F55,100,PS_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-120,y:-200,w:440,h:380,r:0});
   psRoundRect(bg,6,6,PS_W - 12,PS_H - 12,24);
   bg.endFill();

   // The title strip, with a few sparkles (fixed seed) and a gold underline.
   psGradientRect(bg,16,14,PS_W - 32,42,21,0x0A1440,0x0A1440,40,PS_RIM,25,1);
   var s = 3131;
   var i;
   for(i = 0; i < 10; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 34 + s % (PS_W - 68);
      s = (s * 1103515245 + 12345) % 2147483648;
      psSparkle(bg,x,20 + s % 30,1 + s % 2,15 + s % 35);
   }
   bg.lineStyle();
   bg.beginGradientFill("linear",[PS_GOLD,PS_GOLD,PS_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:60,y:0,w:PS_W - 120,h:2,r:0});
   psRoundRect(bg,60,54,PS_W - 120,2,1);
   bg.endFill();

   // The recess the bars sit in.
   psGradientRect(bg,16,66,PS_W - 32,PS_H - 82,20,0x0A1440,0x0A1440,32,PS_RIM,20,1);
}

// A frosted socket for a row's icon, ringed in the row's colour.
function psDrawSocket(mc, row)
{
   var x = row[0];
   var y = row[1];
   var r = row[2];
   mc.lineStyle();
   mc.beginFill(0x050A24,35);
   psCircle(mc,x,y + 2,r + 1);
   mc.endFill();
   mc.lineStyle(2.5,row[6],100);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r - 6,y:y - r - 8,w:r * 2 + 8,h:r * 2 + 8,r:0});
   psCircle(mc,x,y,r);
   mc.endFill();
   mc.lineStyle(1,0xFFFFFF,70);
   psCircle(mc,x,y,r + 2.5);
}

// The dark recessed track a fill runs in.
function psDrawTrack(mc, x, y, w, h)
{
   var r = h / 2;
   mc.lineStyle(1.5,PS_RIM,30);
   mc.beginGradientFill("linear",[0x050A24,0x0E1A52],[85,85],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   psRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   // Inner shadow along the top.
   mc.lineStyle();
   mc.beginGradientFill("linear",[0x000000,0x000000],[45,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h * 0.5,r:Math.PI / 2});
   psRoundRect(mc,x + 1,y + 1,w - 2,h * 0.5,r * 0.5);
   mc.endFill();
}

// A glossy rounded fill w wide: light top, the colour, a darker base, and a white sheen on the upper half.
function psDrawFill(mc, x, y, w, h, row)
{
   var r = h / 2;
   mc.lineStyle(1,row[5],100);
   mc.beginGradientFill("linear",[row[5],row[6],row[7]],[100,100,100],[0,110,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   psRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[60,5],[0,255],{matrixType:"box",x:x,y:y + 2,w:w,h:h * 0.42,r:Math.PI / 2});
   psRoundRect(mc,x + r * 0.5,y + 2,w - r,h * 0.42,h * 0.2);
   mc.endFill();
}

// Diagonal stripes, a little wider than the bar so they can scroll a full period either way.
function psDrawStripes(mc, w, h)
{
   mc.clear();
   mc.lineStyle();
   var x;
   for(x = -24; x < w + 24; x += 12)
   {
      mc.beginFill(0xFFFFFF,100);
      mc.moveTo(x,h);
      mc.lineTo(x + 6,h);
      mc.lineTo(x + 6 + h * 0.6,0);
      mc.lineTo(x + h * 0.6,0);
      mc.lineTo(x,h);
      mc.endFill();
   }
}

function psMakeRow(i)
{
   var row = PS_ROWS[i];
   var h = row[4];
   var y = row[1] - h / 2;
   var w = PS_BAR_RIGHT - row[3];
   var bar = psCard.createEmptyMovieClip("bar" + i,psCard.getNextHighestDepth());
   bar._x = row[3];
   bar._y = y;
   bar.row = row;
   bar.w = w;
   bar.h = h;
   bar.track = bar.createEmptyMovieClip("track",1);
   psDrawTrack(bar.track,0,0,w,h);
   bar.fill = bar.createEmptyMovieClip("fill",2);
   bar.fill.filters = [new flash.filters.GlowFilter(row[6],0.45,8,8,1,2)];
   bar.stripes = bar.createEmptyMovieClip("stripes",3);
   psDrawStripes(bar.stripes,w,h);
   bar.stripes._alpha = 0;
   bar.mask = bar.createEmptyMovieClip("mask",4);
   bar.stripes.setMask(bar.mask);
   bar.speed = 0;
   bar.onEnterFrame = function()
   {
      if(this.speed != 0)
      {
         var x = this.stripes._x + this.speed * 0.8;
         // One stripe period is 12px.
         if(x > 0)
         {
            x -= 12;
         }
         else if(x < -12)
         {
            x += 12;
         }
         this.stripes._x = x;
      }
   };
   bar.pct = psText(psCard,"pct" + i,"",PS_PCT_X,row[1] - 12,PS_PCT_W,24,i == 0 ? 17 : 15,i == 0 ? 0xFFE08A : 0xFFFFFF,"center");
   bar.pct.filters = psOutline();
   psDrawSocket(psCard.sockets,row);
   return bar;
}

function psDrawBar(i)
{
   var bar = psCard["bar" + i];
   var v = Number(psValues[i]);
   if(isNaN(v))
   {
      v = 0;
   }
   v = Math.max(0,Math.min(100,v));
   var w = v > 0 ? Math.max(bar.h,Math.round(bar.w * v / 100)) : 0;
   bar.fill.clear();
   bar.mask.clear();
   if(w > 0)
   {
      psDrawFill(bar.fill,0,0,w,bar.h,bar.row);
      bar.mask.beginFill(0,100);
      psRoundRect(bar.mask,0,0,w,bar.h,bar.h / 2);
      bar.mask.endFill();
   }
   bar.speed = Number(psSpeeds[i]);
   if(isNaN(bar.speed))
   {
      bar.speed = 0;
   }
   bar.stripes._alpha = bar.speed != 0 && w > 0 ? 22 : 0;
   bar.pct.text = Math.round(v) + "%";
}

function psBuild()
{
   // The stock frame and bars stay (invisible) so the window keeps its hit area and Lua its masks.
   bg._alpha = 0;
   moodbar._alpha = foodbar._alpha = groombar._alpha = playbar._alpha = 0;

   psCard = _root.createEmptyMovieClip("sanctuaryStats",-16384);
   psCard.bg = psCard.createEmptyMovieClip("bg",1);
   psCard.bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   psCard.sockets = psCard.createEmptyMovieClip("sockets",2);
   psDrawCard();

   var i;
   for(i = 0; i < PS_ROWS.length; i++)
   {
      psMakeRow(i);
      psValues[i] = pools[i]._xscale;
      psDrawBar(i);
   }

   // "Stats", centered in gold.
   WindowTitle._x = (PS_W - WindowTitle._width) / 2;
   var f = new TextFormat();
   f.color = 0xFFE08A;
   f.align = "center";
   WindowTitle.setNewTextFormat(f);
   WindowTitle.setTextFormat(f);
   WindowTitle.filters = psOutline();
}

// --- Lua -------------------------------------------------------------------------------

var psUpdatePool = updatePool;
updatePool = function(poolId, poolValue)
{
   psUpdatePool.apply(_root,arguments);
   psValues[poolId] = pools[poolId]._xscale;
   psDrawBar(poolId);
};

// Food, grooming and play (1-3) rise or fall; mood has no motion.
var psSetBarMotion = setBarMotion;
setBarMotion = function(poolId, speed)
{
   psSetBarMotion.apply(_root,arguments);
   psSpeeds[poolId] = speed;
   psDrawBar(poolId);
};

psBuild();
// ===== End Sanctuary Pet Stats =====

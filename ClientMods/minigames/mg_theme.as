// ===== Sanctuary Minigame Theme =====
// Shared by the minigame movies: build_client_mods.py appends it to each one's main timeline ahead of that movie's
// own patch (minigames/<movie>.as), which draws with it. It holds the Main Menu's palette
// (ClientMods/MyStuffBrowser/main_menu.as) and its drawing helpers, plus "skins": a gold or navy glass button drawn
// under a stock button, which follows the stock clip's position, scale, visibility and alpha every frame (so the
// stock tweens and pulses still show) while the stock art goes invisible and keeps taking the mouse. Hovering is
// read with hitTest, so no stock mouse handler is replaced. Text the stock code recolours is put back to the light
// colour it was given (mgLight). Easing is done here too, since not every movie has TweenLite compiled in.

var MG_GOLD = 0xFFC531;
var MG_GOLD_LIGHT = 0xFFE08A;
var MG_GOLD_DEEP = 0xF5A623;
var MG_GOLD_RIM = 0xFFF3C4;
var MG_RIM = 0x9DB8FF;
var MG_SOFT = 0xC9D6FF;
var MG_INK = 0x3A2400;
var MG_NAVY = 0x0A1440;
var MG_SHADOW = 0x050A24;

// --- drawing helpers -------------------------------------------------------------------

function mgRoundRect(mc, x, y, w, h, r)
{
   r = Math.min(r,Math.min(w,h) / 2);
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

function mgCircle(mc, x, y, r)
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

function mgGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   mgRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

// A recess: flat navy glass with a faint rim.
function mgPanel(mc, x, y, w, h, r, alpha, rimAlpha)
{
   mc.lineStyle(1,MG_RIM,rimAlpha);
   mc.beginFill(MG_NAVY,alpha);
   mgRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

// The soft white shine across the top of a card.
function mgGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   mgRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function mgSparkle(mc, x, y, r, alpha)
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

// Sparkles scattered over a strip (fixed seed, the same every time).
function mgSparkles(mc, seed, x, y, w, h, count)
{
   var s = seed;
   var i;
   for(i = 0; i < count; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + s % w;
      s = (s * 1103515245 + 12345) % 2147483648;
      mgSparkle(mc,sx,y + s % h,1 + s % 3,25 + s % 50);
   }
}

// The Main Menu's frame: navy gradient, pale rim, a soft highlight from the top left, a darker header band
// (headerH, 0 for none) with sparkles, and the drop shadow (on mc itself, so give the frame its own clip).
function mgFrame(mc, x, y, w, h, r, headerH, seed)
{
   mgGradientRect(mc,x,y,w,h,r,0x2E4FB0,0x141F55,100,MG_RIM,80,3);
   mc.lineStyle();
   mc.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:x - w * 0.15,y:y - h * 0.4,w:w * 0.9,h:h * 0.9,r:0});
   mgRoundRect(mc,x + 2,y + 2,w - 4,h - 4,r - 2);
   mc.endFill();
   if(headerH > 0)
   {
      mc.beginFill(MG_NAVY,30);
      mgRoundRect(mc,x + 4,y + 4,w - 8,headerH,r - 4);
      mc.endFill();
      mgSparkles(mc,seed,x + 28,y + 10,w - 56,Math.max(10,headerH - 20),Math.round(w / 34));
   }
   mc.filters = [new flash.filters.DropShadowFilter(6,90,MG_SHADOW,0.6,18,18,1,2)];
}

// A compact HUD plate: the frame's glass in a small size, rim in pale blue or gold.
function mgPlate(mc, x, y, w, h, r, gold)
{
   mgGradientRect(mc,x,y,w,h,r,0x2E4FB0,0x141F55,92,gold ? MG_GOLD : MG_RIM,gold ? 100 : 80,2);
   mgGloss(mc,x + 4,y + 2,w - 8,Math.min(h * 0.45,24),Math.max(2,r - 3),30);
   mc.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.55,8,8,1,2)];
}

// A gold progress bar in a navy track (f 0..1); the meters' own fills use mgFill.
function mgBar(mc, x, y, w, h, f)
{
   if(isNaN(f) || f < 0)
   {
      f = 0;
   }
   else if(f > 1)
   {
      f = 1;
   }
   mc.lineStyle(1,MG_RIM,30);
   mc.beginFill(MG_NAVY,70);
   mgRoundRect(mc,x,y,w,h,h / 2);
   mc.endFill();
   if(f > 0)
   {
      mgFill(mc,x,y,Math.max(h,w * f),h,h / 2,MG_GOLD_LIGHT,MG_GOLD_DEEP);
   }
}

// A glossy rounded fill (bars and meter segments).
function mgFill(mc, x, y, w, h, r, top, bottom)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[top,bottom],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   mgRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   if(w > 2 * r && h > 4)
   {
      mc.beginFill(0xFFFFFF,40);
      mgRoundRect(mc,x + r * 0.6,y + 1,w - r * 1.2,Math.max(1,h * 0.3),h * 0.15);
      mc.endFill();
   }
}

// Frosted sockets behind item art: the stock icons are made for light plates.
function mgSocket(mc, x, y, w, h, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - w * 0.18,y:y - h * 0.24,w:w * 1.36,h:h * 1.36,r:0});
   mgRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function mgSocketCircle(mc, x, y, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.3,y:y - r * 1.5,w:r * 2.6,h:r * 2.6,r:0});
   mgCircle(mc,x,y,r);
   mc.endFill();
}

function mgStar(mc, x, y, r, color, alpha)
{
   var i;
   mc.lineStyle();
   mc.beginFill(color,alpha);
   for(i = 0; i <= 10; i++)
   {
      var a = - Math.PI / 2 + i * Math.PI / 5;
      var d = i % 2 == 0 ? r : r * 0.45;
      if(i == 0)
      {
         mc.moveTo(x + Math.cos(a) * d,y + Math.sin(a) * d);
      }
      else
      {
         mc.lineTo(x + Math.cos(a) * d,y + Math.sin(a) * d);
      }
   }
   mc.endFill();
}

function mgOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function mgShadow()
{
   return [new flash.filters.DropShadowFilter(4,90,MG_SHADOW,0.5,10,10,1,2)];
}

function mgGoldGlow()
{
   return [new flash.filters.GlowFilter(MG_GOLD,0.8,16,16,2,2)];
}

function mgText(mc, name, txt, x, y, w, h, size, color, align)
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
   t.baseSize = size;
   return t;
}

// An empty clip below every timeline child of parent (the lowest free depth).
function mgUnder(parent, name)
{
   var d = -16384;
   while(parent.getInstanceAtDepth(d) != undefined)
   {
      d++;
   }
   return parent.createEmptyMovieClip(name,d);
}

// An empty clip just below clip, inside its parent.
function mgBelow(clip, name)
{
   var p = clip._parent;
   var d = clip.getDepth() - 1;
   while(p.getInstanceAtDepth(d) != undefined)
   {
      d--;
   }
   return p.createEmptyMovieClip(name,d);
}

// --- the driver: skins, held alphas and light text, every frame ------------------------

function mgDriver()
{
   var d = _root.mgDriverClip;
   if(d == undefined)
   {
      // It draws nothing. It takes the lowest free dynamic depth, so the stock code's getNextHighestDepth still has
      // room above it, and a timeline rewind (which only clears timeline depths) leaves it be.
      var depth = 0;
      while(_root.getInstanceAtDepth(depth) != undefined)
      {
         depth++;
      }
      d = _root.createEmptyMovieClip("mgDriverClip",depth);
      d.skins = [];
      d.texts = [];
      d.holds = [];
      d.onEnterFrame = mgTick;
   }
   return d;
}

function mgTick()
{
   var i;
   var x = _root._xmouse;
   var y = _root._ymouse;
   for(i = 0; i < this.holds.length; i++)
   {
      if(this.holds[i]._alpha != 0)
      {
         this.holds[i]._alpha = 0;
      }
   }
   for(i = 0; i < this.texts.length; i++)
   {
      var t = this.texts[i];
      if(t.htmlText != t.mgLast || t.textColor != t.mgColor)
      {
         t.textColor = t.mgColor;
         t.mgLast = t.htmlText;
      }
   }
   for(i = 0; i < this.skins.length; i++)
   {
      mgSkinTick(this.skins[i],x,y);
   }
}

// Keeps a stock clip invisible (alpha 0, so it still takes the mouse) even when the stock code fades it in.
function mgHold(clip)
{
   if(clip == undefined)
   {
      return;
   }
   clip._alpha = 0;
   mgDriver().holds.push(clip);
}

// A stock text field in a light colour with the dark outline; kept that colour when the stock code sets its text.
function mgLight(t, color, outline)
{
   if(t == undefined)
   {
      return;
   }
   t.mgColor = color == undefined ? 0xFFFFFF : color;
   t.textColor = t.mgColor;
   t.mgLast = t.htmlText;
   if(outline != false)
   {
      t.filters = mgOutline();
   }
   mgDriver().texts.push(t);
}

// A skin under a stock button. kind: "gold" (pill), "navy" (glass pill), "red" (pill), "close" (gold circle with
// an X), "left"/"right" (gold circle with a chevron), "check" (gold circle with a tick). label is plain text drawn
// on it ("" for none). rect is the area to cover in the stock clip's own coordinates (default its bounds).
// hideStock: true makes the stock clip invisible (default); false leaves it (e.g. its label) showing on top.
function mgSkin(stock, kind, label, rect, hideStock)
{
   if(stock == undefined)
   {
      return undefined;
   }
   if(rect == undefined)
   {
      rect = stock.getBounds(stock);
      rect = {x:rect.xMin,y:rect.yMin,w:rect.xMax - rect.xMin,h:rect.yMax - rect.yMin};
   }
   var s = mgBelow(stock,"mgSkin_" + stock._name);
   s.stock = stock;
   s.kind = kind;
   s.w = rect.w;
   s.h = rect.h;
   s.hideStock = hideStock != false;
   s.alphaWant = stock._alpha;
   s.over = false;
   s.scale = 100;
   s.speed = 0;
   s.body = s.createEmptyMovieClip("body",1);
   s.body._x = rect.x + rect.w / 2;
   s.body._y = rect.y + rect.h / 2;
   s.bg = s.body.createEmptyMovieClip("bg",1);
   if(label != undefined && label.length > 0)
   {
      var size = Math.max(12,Math.min(20,Math.round(rect.h * 0.46)));
      s.label = mgText(s.body,"label_txt",label,- rect.w / 2,- size * 0.72,rect.w,size * 1.6,size,kind == "navy" || kind == "red" ? 0xFFFFFF : MG_INK);
      if(kind == "navy" || kind == "red")
      {
         s.label.filters = mgOutline();
      }
   }
   mgDrawSkin(s,false);
   mgDriver().skins.push(s);
   mgSkinTick(s,_root._xmouse,_root._ymouse);
   return s;
}

function mgSkinTick(s, x, y)
{
   var st = s.stock;
   if(st._parent == undefined)
   {
      s._visible = false;
      return;
   }
   s._x = st._x;
   s._y = st._y;
   s._xscale = st._xscale;
   s._yscale = st._yscale;
   s._rotation = st._rotation;
   s._visible = st._visible;
   if(s.hideStock)
   {
      if(st._alpha != 0)
      {
         s.alphaWant = st._alpha;
         st._alpha = 0;
      }
   }
   else
   {
      s.alphaWant = st._alpha;
   }
   s._alpha = s.alphaWant;
   var over = st._visible && st.enabled != false && s.alphaWant > 30 && st.hitTest(x,y,false);
   if(over != s.over)
   {
      s.over = over;
      mgDrawSkin(s,over);
   }
   // A springy grow on hover (elastic, like the Main Menu's tiles).
   var target = over ? 108 : 100;
   s.speed = s.speed * 0.62 + (target - s.scale) * 0.28;
   s.scale += s.speed;
   if(Math.abs(s.speed) < 0.05 && Math.abs(target - s.scale) < 0.1)
   {
      s.scale = target;
      s.speed = 0;
   }
   s.body._xscale = s.body._yscale = s.scale;
}

function mgDrawSkin(s, over)
{
   var g = s.bg;
   var w = s.w;
   var h = s.h;
   var k = s.kind;
   g.clear();
   if(k == "close" || k == "left" || k == "right" || k == "check")
   {
      var r = Math.min(w,h) / 2;
      mgGoldCircle(g,r,over);
      g.lineStyle(Math.max(2,r * 0.24),MG_INK,100);
      var a = r * 0.36;
      if(k == "close")
      {
         g.moveTo(- a,- a);
         g.lineTo(a,a);
         g.moveTo(a,- a);
         g.lineTo(- a,a);
      }
      else if(k == "check")
      {
         g.moveTo(- a * 1.1,0);
         g.lineTo(- a * 0.25,a * 0.85);
         g.lineTo(a * 1.15,- a * 0.8);
      }
      else
      {
         var dir = k == "left" ? -1 : 1;
         g.moveTo(- dir * a * 0.5,- a);
         g.lineTo(dir * a * 0.6,0);
         g.lineTo(- dir * a * 0.5,a);
      }
      s.body.filters = over ? mgGoldGlow() : [new flash.filters.DropShadowFilter(2,90,MG_SHADOW,0.6,6,6,1,2)];
      return;
   }
   var rr = h / 2;
   if(k == "navy")
   {
      mgGradientRect(g,- w / 2,- h / 2,w,h,rr,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? MG_GOLD_LIGHT : MG_RIM,over ? 100 : 60,1.5);
   }
   else if(k == "red")
   {
      mgGradientRect(g,- w / 2,- h / 2,w,h,rr,over ? 0xFF8A80 : 0xF0605A,over ? 0xD03A3A : 0xB52A2A,100,0xFFD0C8,100,1.5);
   }
   else
   {
      mgGradientRect(g,- w / 2,- h / 2,w,h,rr,over ? 0xFFF0B0 : MG_GOLD_LIGHT,over ? 0xFFB838 : MG_GOLD_DEEP,100,MG_GOLD_RIM,100,1.5);
   }
   mgGloss(g,- w / 2 + rr * 0.5,- h / 2 + 2,w - rr,h * 0.4,h * 0.2,k == "navy" ? 25 : 50);
   s.body.filters = over ? mgGoldGlow() : [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.55,8,8,1,2)];
}

// The gold round button (close, arrows): gradient, rim and a shine, centered on 0,0.
function mgGoldCircle(g, r, over)
{
   g.lineStyle(1.5,MG_GOLD_RIM,100);
   g.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [MG_GOLD_LIGHT,MG_GOLD_DEEP],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   mgCircle(g,0,0,r);
   g.endFill();
   mgGloss(g,- r * 0.65,- r * 0.82,r * 1.3,r * 0.65,r * 0.3,50);
}

// Penner's easings for TweenLite (not every movie has mx.transitions.easing compiled in).
function mgElasticOut(t, b, c, d)
{
   if(t == 0)
   {
      return b;
   }
   t /= d;
   if(t >= 1)
   {
      return b + c;
   }
   var p = d * 0.3;
   return c * Math.pow(2,-10 * t) * Math.sin((t * d - p / 4) * (2 * Math.PI) / p) + c + b;
}

function mgStrongOut(t, b, c, d)
{
   t = t / d - 1;
   return c * (t * t * t * t * t + 1) + b;
}

// Hides every movie clip directly under parent except the names in keep (an object used as a set).
function mgHideChildren(parent, keep)
{
   var name;
   for(name in parent)
   {
      var c = parent[name];
      if(typeof c == "movieclip" && c._parent == parent && keep[name] != true)
      {
         c._visible = false;
      }
   }
}
// ===== End Sanctuary Minigame Theme =====

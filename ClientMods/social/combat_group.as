// ===== Sanctuary Group Frames =====
// Appended to combatGroupWindow's main timeline by build_client_mods.py.
// Redraws the player's unit frame and the group member frames under it (wndCombatGroup, top left of the HUD) in the
// Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as). Every portrait sits in a navy disc inside a gold ring
// (round masks on the portrait and its red damage flashes), with a gold crown on the group leader's. The player's
// frame is a navy glass plate behind glossy rounded health and energy bars (red and blue, a light trail easing
// after each hit) and a gold job XP bar under the job title; the job switch arrows are gold chevrons in frosted
// discs, and the "open My Stuff" highlight is a gold glow around the ring. Members' mini health bars match, and the
// kick / leave button is a small red glass disc with a white X. The stock clips keep their roles: their art goes
// invisible but stays the hit areas, and Lua's calls (SetHealth, SetPower, GroupDataUpdate, setPlayerLevelExp,
// SetMode, ...) run as before, with the new look redrawn after them.

// The player's plate, in root coordinates (the portrait is centered on (45, 49), the bars start at x 87).
var CGW_PLATE = [44,7,224,84];
var CGW_RING_R = 40;
var CGW_PHOTO_R = 35;
var CGW_GOLD = 0xFFC531;
var CGW_GOLD_LIGHT = 0xFFE08A;
var CGW_GOLD_DARK = 0xF5A623;
var CGW_RIM = 0x9DB8FF;
var CGW_INK = 0x3A2400;

var cgwHud;

// --- drawing helpers -------------------------------------------------------------------

function cgwRoundRect(mc, x, y, w, h, r)
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

function cgwGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   cgwRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function cgwCircle(mc, x, y, r)
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

function cgwSparkle(mc, x, y, r, alpha)
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

function cgwOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function cgwCircleMask(parent, name, r)
{
   var m = parent.createEmptyMovieClip(name,parent.getNextHighestDepth());
   m.beginFill(0,100);
   cgwCircle(m,0,0,r);
   m.endFill();
   return m;
}

// A glossy bar: colours top to bottom, and a white sheen across its top half.
function cgwGlossBar(mc, x, y, w, h, colors)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",colors,[100,100,100],[0,120,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   cgwRoundRect(mc,x,y,w,h,h / 2);
   mc.endFill();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[45,0],[0,255],{matrixType:"box",x:x,y:y + 1,w:w,h:h * 0.5,r:Math.PI / 2});
   cgwRoundRect(mc,x + h * 0.3,y + 1,Math.max(0,w - h * 0.6),h * 0.45,h * 0.22);
   mc.endFill();
}

// A recessed track for a bar.
function cgwTrack(mc, x, y, w, h)
{
   mc.lineStyle(1,CGW_RIM,40);
   mc.beginFill(0x0A1440,80);
   cgwRoundRect(mc,x - 1,y - 1,w + 2,h + 2,(h + 2) / 2);
   mc.endFill();
}

// A rectangle mask whose _xscale is the fill fraction (0..100), anchored at x.
function cgwBarMask(parent, name, x, y, w, h)
{
   var m = parent[name];
   if(m == undefined)
   {
      m = parent.createEmptyMovieClip(name,parent.getNextHighestDepth());
   }
   m.clear();
   m.beginFill(0,100);
   cgwRoundRect(m,0,y,w,h,0);
   m.endFill();
   m._x = x;
   return m;
}

// --- the player's plate ----------------------------------------------------------------

function cgwBuildPlate()
{
   // Under every stock clip; no mouse handlers.
   cgwHud = _root.createEmptyMovieClip("sanctuaryHud",-16384);
   var g = cgwHud.createEmptyMovieClip("plate",1);
   var x = CGW_PLATE[0];
   var y = CGW_PLATE[1];
   var w = CGW_PLATE[2];
   var h = CGW_PLATE[3];
   cgwGradientRect(g,x,y,w,h,24,0x2E4FB0,0x141F55,88,CGW_RIM,70,2);
   g.lineStyle();
   g.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[30,0],[0,255],{matrixType:"box",x:x - 40,y:y - 90,w:260,h:180,r:0});
   cgwRoundRect(g,x + 2,y + 2,w - 4,h - 4,22);
   g.endFill();
   var s = 6161;
   var i;
   for(i = 0; i < 8; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + 50 + s % (w - 64);
      s = (s * 1103515245 + 12345) % 2147483648;
      cgwSparkle(g,sx,y + 4 + s % 6,1 + s % 2,18 + s % 30);
   }
   g.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,12,12,1,2)];
}

// --- portraits -------------------------------------------------------------------------

// The gold ring (with the leader's crown) over a portrait, and the navy disc behind it.
function cgwStylePortrait(clip)
{
   var p = clip.mcPortrait;
   if(p.cgwRing != undefined)
   {
      return;
   }
   // The stock square frame (its gold and blue plates) goes; the disc is drawn inside it, so the leave
   // animation (which scales the frame) still shrinks it.
   p.bg.gold._alpha = p.bg.blue._alpha = 0;
   var disc = p.bg.createEmptyMovieClip("sanctuaryDisc",p.bg.getNextHighestDepth());
   disc.lineStyle();
   disc.beginFill(0x050A24,40);
   cgwCircle(disc,0,3,CGW_RING_R + 1);
   disc.endFill();
   disc.beginGradientFill("radial",[0x4A70DC,0x172A6E],[100,100],[0,255],{matrixType:"box",x:- CGW_RING_R,y:- CGW_RING_R - 12,w:CGW_RING_R * 2,h:CGW_RING_R * 2,r:0});
   cgwCircle(disc,0,0,CGW_RING_R);
   disc.endFill();

   p.portrait.setMask(cgwCircleMask(p,"sanctuaryPhotoMask",CGW_PHOTO_R));
   p.mcRedFlash.setMask(cgwCircleMask(p,"sanctuaryFlashMask",CGW_RING_R - 1));
   p.mcRedFlash2.setMask(cgwCircleMask(p,"sanctuaryFlashMask2",CGW_RING_R - 1));

   // The stock strip under the portrait (a plain blue shape at the clip's first timeline depth, which shows under
   // the player's portrait) is covered by a navy pill swapped in just above it; members' bars sit on it.
   var strip = clip.createEmptyMovieClip("sanctuaryStrip",-16384);
   strip.swapDepths(1 - 16384);
   cgwGradientRect(strip,2,80,84,15,7.5,0x284A9E,0x162660,100,CGW_RIM,45,1);

   var ring = p.cgwRing = p.createEmptyMovieClip("sanctuaryRing",p.getNextHighestDepth());
   ring.glow = ring.createEmptyMovieClip("glow",1);
   ring.glow.lineStyle(10,CGW_GOLD,60);
   cgwCircle(ring.glow,0,0,CGW_RING_R);
   ring.glow.filters = [new flash.filters.BlurFilter(10,10,2)];
   ring.glow._alpha = 0;
   ring.lineStyle(5,CGW_GOLD,100);
   cgwCircle(ring,0,0,CGW_RING_R - 1);
   ring.lineStyle(1.5,0xFFF3C4,90);
   cgwCircle(ring,0,0,CGW_RING_R + 1.5);
   ring.lineStyle(1.5,0x8A5A00,70);
   cgwCircle(ring,0,0,CGW_RING_R - 3.5);
   ring.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];
   ring.crown = ring.createEmptyMovieClip("crown",2);
   cgwDrawCrown(ring.crown);
   // Tilted on the ring's top left corner: straight above it the player's would be cut off by the window's top.
   ring.crown._x = -27;
   ring.crown._y = -26;
   ring.crown._rotation = -35;
   ring.crown._visible = false;
}

function cgwDrawCrown(c)
{
   c.lineStyle(2,0x3A2400,90);
   c.beginGradientFill("linear",[0xFFF3C4,CGW_GOLD_LIGHT,CGW_GOLD_DARK],[100,100,100],[0,90,255],{matrixType:"box",x:-15,y:-16,w:30,h:18,r:Math.PI / 2});
   c.moveTo(-14,2);
   c.lineTo(-15,-12);
   c.lineTo(-7,-5);
   c.lineTo(0,-16);
   c.lineTo(7,-5);
   c.lineTo(15,-12);
   c.lineTo(14,2);
   c.lineTo(-14,2);
   c.endFill();
   c.lineStyle();
   c.beginFill(0xE5334B,100);
   cgwCircle(c,0,-3,2.5);
   c.endFill();
   c.beginFill(0xFFFFFF,90);
   cgwCircle(c,-15,-12,1.8);
   cgwCircle(c,0,-16,1.8);
   cgwCircle(c,15,-12,1.8);
   c.endFill();
   c.filters = [new flash.filters.GlowFilter(CGW_GOLD,0.6,8,8,2,2)];
}

function cgwSetGlow(clip, on)
{
   var ring = clip.mcPortrait.cgwRing;
   if(ring.glowOn == on)
   {
      return;
   }
   ring.glowOn = on;
   gs.TweenLite.to(ring.glow,0.25,{_alpha:on ? 100 : 0});
}

// The player's "open My Stuff" highlight (a yellow square the stock code shows on hover) becomes the ring's glow.
function cgwSyncHighlight()
{
   var h = Player.portrait_highlight;
   cgwSetGlow(Player,h._visible && h._alpha > 0);
   h._alpha = 0;
}

function cgwWrapPlayerPortrait()
{
   var p = Player.mcPortrait;
   var names = ["onPress","onRollOver","onRollOut"];
   var i;
   for(i = 0; i < names.length; i++)
   {
      cgwWrapHandler(p,names[i],cgwSyncHighlight);
   }
}

// Calls after(this) after a stock handler (keeping its arguments and return value).
function cgwWrapHandler(mc, name, after)
{
   var stock = mc[name];
   if(stock == undefined || stock.cgwWrapped)
   {
      return;
   }
   var f = function()
   {
      var r = stock.apply(this,arguments);
      after(this);
      return r;
   };
   f.cgwWrapped = true;
   mc[name] = f;
}

// --- health and energy -----------------------------------------------------------------

function cgwStylePools(p)
{
   if(p.cgwBars != undefined)
   {
      return;
   }
   var fb = p.poolsBarFrame.getBounds(p);
   p.cgwBox = [fb.xMin,fb.yMin,fb.xMax - fb.xMin,fb.yMax - fb.yMin];
   p.poolsBarFrame._alpha = p.hpool._alpha = p.hpool2._alpha = p.ppool._alpha = p.ppool2._alpha = 0;
   // Under the stock numbers.
   var b = p.cgwBars = p.createEmptyMovieClip("sanctuaryBars",-16384);
   b.track = b.createEmptyMovieClip("track",1);
   b.hTrail = b.createEmptyMovieClip("hTrail",2);
   b.hFill = b.createEmptyMovieClip("hFill",3);
   b.pTrail = b.createEmptyMovieClip("pTrail",4);
   b.pFill = b.createEmptyMovieClip("pFill",5);
   p.healthText.filters = p.powerText.filters = cgwOutline();
   p.cgwPower = undefined;
}

// Lays the bars out: health on top and energy under it, or health alone (members get no energy from Lua).
function cgwLayoutBars(p)
{
   var power = p.m_MaxPower != undefined && Number(p.m_MaxPower) > 0;
   if(p.cgwPower === power)
   {
      return;
   }
   p.cgwPower = power;
   var b = p.cgwBars;
   var x = p.cgwBox[0];
   var y = p.cgwBox[1];
   var w = p.cgwBox[2];
   var h = p.cgwBox[3];
   var hh = power ? h / 2 - 1.5 : h;
   b.track.clear();
   cgwTrack(b.track,x,y,w,hh);
   b.hTrail.clear();
   b.hTrail.beginFill(0xFFD3D3,60);
   cgwRoundRect(b.hTrail,x,y,w,hh,hh / 2);
   b.hTrail.endFill();
   b.hFill.clear();
   cgwGlossBar(b.hFill,x,y,w,hh,[0xFF8A8A,0xE5334B,0xA8162F]);
   b.hTrail.setMask(cgwBarMask(b,"hTrailMask",x,y,w,hh));
   b.hFill.setMask(cgwBarMask(b,"hFillMask",x,y,w,hh));
   b.pTrail.clear();
   b.pFill.clear();
   b.pTrail._visible = b.pFill._visible = power;
   if(power)
   {
      var py = y + h / 2 + 1.5;
      cgwTrack(b.track,x,py,w,hh);
      b.pTrail.beginFill(0xCFE8FF,60);
      cgwRoundRect(b.pTrail,x,py,w,hh,hh / 2);
      b.pTrail.endFill();
      cgwGlossBar(b.pFill,x,py,w,hh,[0x9AD6FF,0x2F8BEA,0x1752B0]);
      b.pTrail.setMask(cgwBarMask(b,"pTrailMask",x,py,w,hh));
      b.pFill.setMask(cgwBarMask(b,"pFillMask",x,py,w,hh));
   }
}

function cgwFraction(cur, max)
{
   var c = Number(cur);
   var m = Number(max);
   if(isNaN(c) || isNaN(m) || m <= 0)
   {
      return 0;
   }
   return Math.max(0,Math.min(1,c / m));
}

// The fill jumps to the new value and the light trail eases after it (like the stock masks).
function cgwDrawBars(p)
{
   cgwStylePools(p);
   cgwLayoutBars(p);
   var b = p.cgwBars;
   var hp = cgwFraction(p.m_Health,p.m_MaxHealth) * 100;
   b.hFillMask._xscale = hp;
   gs.TweenLite.to(b.hTrailMask,0.8,{_xscale:hp,ease:mx.transitions.easing.Strong.easeInOut});
   // Low health: the bar glows red while the stock pulse runs.
   b.hFill.filters = p.hp_is_pulsating ? [new flash.filters.GlowFilter(0xFF4040,0.9,10,10,3,2)] : [];
   if(p.cgwPower)
   {
      var pp = cgwFraction(p.m_Power,p.m_MaxPower) * 100;
      b.pFillMask._xscale = pp;
      gs.TweenLite.to(b.pTrailMask,0.8,{_xscale:pp,ease:mx.transitions.easing.Strong.easeInOut});
   }
}

// --- the job XP bar --------------------------------------------------------------------

function cgwStyleXp(pm)
{
   if(pm.cgwXp != undefined)
   {
      return;
   }
   pm.barHolder_mc._alpha = pm.xpbar._alpha = 0;
   // Swapped into the stock frame's place (an unnamed shape at timeline depth 11), so it covers it and stays
   // under the title (12) and the level badge (13). If the swap doesn't take it sits under the frame instead.
   var g = pm.cgwXp = pm.createEmptyMovieClip("sanctuaryXp",-16384);
   g.swapDepths(11 - 16384);
   var x = 24;
   var y = 7;
   var w = 141;
   var h = 20;
   cgwTrack(g,x,y,w,h);
   g.fill = g.createEmptyMovieClip("fill",1);
   cgwGlossBar(g.fill,x,y,w,h,[0xFFF0B0,CGW_GOLD_LIGHT,CGW_GOLD_DARK]);
   g.fill.setMask(cgwBarMask(g,"fillMask",x,y,w,h));
   g.fillMask._xscale = Math.max(0,Math.min(100,pm.mask_mc._xscale));
   pm.title_txt.filters = cgwOutline();
   pm.level_mc.level_txt.filters = cgwOutline();
}

function cgwDrawXp(pm)
{
   cgwStyleXp(pm);
   var pct = Math.max(0,Math.min(100,pm.mask_mc._xscale));
   gs.TweenLite.to(pm.cgwXp.fillMask,0.6,{_xscale:pct,ease:mx.transitions.easing.Strong.easeOut});
   // At the job's top rank the bar glows (the stock bar turns gold).
   pm.cgwXp.fill.filters = Number(pm.level_mc.level_txt.text) >= 20 ? [new flash.filters.GlowFilter(CGW_GOLD,0.7,10,10,2,2)] : [];
}

// --- the job switch arrows -------------------------------------------------------------

function cgwStyleArrow(a)
{
   if(a.cgwArt != undefined)
   {
      return;
   }
   a.UP._alpha = a.ROLL._alpha = a.DOWN._alpha = 0;
   var g = a.cgwArt = a.createEmptyMovieClip("sanctuaryArrow",-16384);
   g.lineStyle(2,CGW_RIM,80);
   g.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:-19,y:-21,w:38,h:38,r:0});
   cgwCircle(g,0,0,15);
   g.endFill();
   // A left chevron (arrowRight is the same clip mirrored).
   g.lineStyle(1,0x8A5A00,90);
   g.beginGradientFill("linear",[CGW_GOLD_LIGHT,CGW_GOLD_DARK],[100,100],[0,255],{matrixType:"box",x:-8,y:-9,w:16,h:18,r:Math.PI / 2});
   g.moveTo(5,-9);
   g.lineTo(-6,0);
   g.lineTo(5,9);
   g.lineTo(5,3);
   g.lineTo(1,0);
   g.lineTo(5,-3);
   g.lineTo(5,-9);
   g.endFill();
   g.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];
}

function cgwArrowHover(a)
{
   var over = a.ROLL._visible || a.DOWN._visible;
   a.cgwArt.filters = over ? [new flash.filters.GlowFilter(CGW_GOLD,0.8,10,10,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];
}

function cgwWrapArrows()
{
   var arrows = [jobBadge.arrowLeft,jobBadge.arrowRight];
   var names = ["onPress","onRollOver","onRollOut","onDragOut","onRelease"];
   var i;
   var k;
   for(i = 0; i < arrows.length; i++)
   {
      cgwStyleArrow(arrows[i]);
      for(k = 0; k < names.length; k++)
      {
         cgwWrapHandler(arrows[i],names[k],cgwArrowHover);
      }
   }
}

// --- the kick / leave button -------------------------------------------------------------

function cgwStyleKick(clip)
{
   var red = clip.buttonKick.mcBG.RED;
   if(red == undefined)
   {
      return;
   }
   clip.buttonKick.text._alpha = 0;
   if(red.cgwArt != undefined)
   {
      return;
   }
   red.UP._alpha = red.ROLL._alpha = red.DOWN._alpha = 0;
   // RED's states are 77x72 around its origin (the button is drawn at 28.6%).
   var g = red.cgwArt = red.createEmptyMovieClip("sanctuaryKick",-16384);
   g.lineStyle(5,0xFFD3D3,90);
   g.beginGradientFill("radial",[0xFF8A8A,0xE5334B,0x8E1027],[100,100,100],[0,140,255],{matrixType:"box",x:-40,y:-46,w:80,h:80,r:0});
   cgwCircle(g,0,0,32);
   g.endFill();
   g.lineStyle();
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[45,0],[0,255],{matrixType:"box",x:-24,y:-30,w:48,h:28,r:Math.PI / 2});
   cgwRoundRect(g,-22,-28,44,24,12);
   g.endFill();
   g.lineStyle(10,0xFFFFFF,100);
   g.moveTo(-11,-11);
   g.lineTo(11,11);
   g.moveTo(11,-11);
   g.lineTo(-11,11);
   g.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,10,10,1,2)];
}

// --- stock hooks -----------------------------------------------------------------------

function cgwStyleClip(clip)
{
   cgwStylePortrait(clip);
   if(clip.mcPools != undefined)
   {
      cgwStylePools(clip.mcPools);
   }
}

function cgwPatch()
{
   var pools = StatusPools.prototype;
   var setHealth = pools.setHealth;
   pools.setHealth = function(cur, max)
   {
      var r = setHealth.apply(this,arguments);
      cgwDrawBars(this);
      return r;
   };
   var setPower = pools.setPower;
   pools.setPower = function(cur, max)
   {
      var r = setPower.apply(this,arguments);
      cgwDrawBars(this);
      return r;
   };

   var bar = FRLevelProgressBar.prototype;
   var setPercent = bar.setPercent;
   bar.setPercent = function(percent)
   {
      var r = setPercent.apply(this,arguments);
      cgwDrawXp(this);
      return r;
   };
   var setLevel = bar.setLevel;
   bar.setLevel = function(rank)
   {
      var r = setLevel.apply(this,arguments);
      cgwDrawXp(this);
      return r;
   };
}

var cgwCreatePlayerClip = createPlayerClip;
createPlayerClip = function(obj)
{
   var r = cgwCreatePlayerClip.apply(_root,arguments);
   // The player's job arrows get their handlers here.
   if(obj.index == 0)
   {
      cgwWrapArrows();
   }
   return r;
};

var cgwUpdatePlayerClip = updatePlayerClip;
updatePlayerClip = function(obj, clip)
{
   var r = cgwUpdatePlayerClip.apply(_root,arguments);
   cgwStyleClip(clip);
   clip.mcPortrait.cgwRing.crown._visible = obj.rank == "Leader";
   if(clip.index > 0)
   {
      // Members' portraits glow while hovered (the stock rollovers show their name).
      cgwWrapHandler(clip.mcPortrait,"onRollOver",function(p)
      {
         cgwSetGlow(p._parent,true);
      });
      cgwWrapHandler(clip.mcPortrait,"onRollOut",function(p)
      {
         cgwSetGlow(p._parent,false);
      });
      clip.mcPortrait.onDragOut = clip.mcPortrait.onRollOut;
   }
   else
   {
      cgwWrapPlayerPortrait();
   }
   return r;
};

var cgwSetupKickButton = setupKickButton;
setupKickButton = function(clip)
{
   var r = cgwSetupKickButton.apply(_root,arguments);
   if(clip != undefined)
   {
      cgwStyleKick(clip);
   }
   return r;
};

var cgwShowGroupMembers = showGroupMembers;
showGroupMembers = function()
{
   var r = cgwShowGroupMembers.apply(_root,arguments);
   cgwSyncHighlight();
   return r;
};

cgwPatch();
cgwBuildPlate();
cgwStyleClip(Player);
cgwStylePools(mcPools);
cgwStyleXp(progress_mc);
cgwStyleArrow(jobBadge.arrowLeft);
cgwStyleArrow(jobBadge.arrowRight);
cgwWrapPlayerPortrait();
cgwSyncHighlight();
// ===== End Sanctuary Group Frames =====

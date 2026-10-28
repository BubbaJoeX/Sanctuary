// ===== Sanctuary Minigame Tutorial =====
// Appended to minigameTutorial's main timeline (after minigames/mg_theme.as) by build_client_mods.py.
// The how-to-play card a minigame opens with (wndMiniGameTutorial) in the Main Menu's style: a navy glass frame with
// the game's icon in a gold-rimmed frosted medallion over its top edge, the game's name in gold, the help pages in a
// recess, a navy glass pager with gold arrows, a gold "don't show again" checkbox and the close button as a gold
// disc (an X, or a tick on the last page, where the stock button turns green). The stock frame, buttons, pager and
// checkbox stay as invisible hit areas, so Close, the paging and the checkbox work as before.

var MTU_FRAME = {x:6,y:34,w:531,h:542};
var MTU_HELP = {x:16,y:146,w:511,h:346};
// The game icon's medallion, and the close disc's centre in the close button's coordinates.
var MTU_MEDAL = {x:280,y:56,r:46};
var MTU_CLOSE = {x:38,y:35,r:27};

function mtuBuild()
{
   var t = mcTutorial;
   if(t == undefined)
   {
      return;
   }
   mgHold(t.mcBase);
   var f = mgUnder(t,"sanctuaryTutorial");
   var bg = f.createEmptyMovieClip("bg",1);
   var r = MTU_FRAME;
   mgFrame(bg,r.x,r.y,r.w,r.h,30,0,9191);
   mgSparkles(bg,9292,r.x + 30,r.y + 8,r.w - 60,96,22);
   var h = MTU_HELP;
   mgPanel(bg,h.x,h.y,h.w,h.h,20,45,35);
   // A gold rule under the name.
   bg.lineStyle();
   bg.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:120,y:0,w:303,h:2,r:0});
   mgRoundRect(bg,120,138,303,2,1);
   bg.endFill();

   // The game's icon in a gold-rimmed frosted medallion with a gold halo.
   var m = f.createEmptyMovieClip("medal",2);
   var c = MTU_MEDAL;
   m.lineStyle();
   m.beginGradientFill("radial",[MG_GOLD,MG_GOLD],[55,0],[0,255],{matrixType:"box",x:c.x - c.r - 26,y:c.y - c.r - 26,w:(c.r + 26) * 2,h:(c.r + 26) * 2,r:0});
   mgCircle(m,c.x,c.y,c.r + 26);
   m.endFill();
   mgSocketCircle(m,c.x,c.y,c.r,MG_GOLD,100,4);
   m.lineStyle(1.5,MG_GOLD_RIM,90);
   mgCircle(m,c.x,c.y,c.r + 3);
   m.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];

   // The name sits between the medallion and the pages.
   t.tfGame._y = 106;
   mgLight(t.tfGame,MG_GOLD_LIGHT);

   // The pager moves right a little, clear of the "don't show again" label.
   t.pageController._x = 212;
   mtuPager(t.pageController);
   mtuClose(t.mcClose);
   mtuCheckbox(t.mcShowAgain);
}

// --- pager (fr.display.PageController) -------------------------------------------------

// A navy glass pill with gold arrow discs and a recessed page count, drawn over the stock pager's backing and
// arrows (which stay underneath as the hit areas) and under its label.
function mtuPager(pc)
{
   if(pc == undefined)
   {
      return;
   }
   pc.label.filters = mgOutline();
   var a = mgBelow(pc.label,"mtuPagerArt");
   a.pc = pc;
   a.onEnterFrame = mtuPagerTick;
}

function mtuPagerTick()
{
   var p = this.pc.previous;
   var n = this.pc.next;
   var key = (p.enabled != false) + "," + (p.HOVER._visible || p.DOWN._visible) + "," + (n.enabled != false) + "," + (n.HOVER._visible || n.DOWN._visible);
   if(key == this.key)
   {
      return;
   }
   this.key = key;
   var g = this;
   g.clear();
   mgGradientRect(g,0,1,178,43,21.5,0x284A9E,0x162660,100,MG_RIM,60,1.5);
   mgGloss(g,10,2,158,16,8,25);
   mgPanel(g,52,7,74,30,15,70,25);
   mtuPagerArrow(g,22,22.5,-1,p.enabled != false,p.HOVER._visible || p.DOWN._visible);
   mtuPagerArrow(g,156,22.5,1,n.enabled != false,n.HOVER._visible || n.DOWN._visible);
   g.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.55,8,8,1,2)];
}

function mtuPagerArrow(g, x, y, dir, enabled, over)
{
   var r = over && enabled ? 17 : 15;
   if(enabled)
   {
      g.lineStyle(1.5,MG_GOLD_RIM,100);
      g.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [MG_GOLD_LIGHT,MG_GOLD_DEEP],[100,100],[0,255],{matrixType:"box",x:x - r,y:y - r,w:r * 2,h:r * 2,r:Math.PI / 2});
   }
   else
   {
      g.lineStyle(1.5,MG_RIM,40);
      g.beginFill(0x3A4F8A,60);
   }
   mgCircle(g,x,y,r);
   g.endFill();
   g.lineStyle(3.5,enabled ? MG_INK : MG_SOFT,enabled ? 100 : 50);
   g.moveTo(x - dir * 3,y - 6);
   g.lineTo(x + dir * 4,y);
   g.lineTo(x - dir * 3,y + 6);
}

// --- close (fr.core.FRButton): a gold disc with an X, or a tick when the stock button is green -------

function mtuClose(b)
{
   if(b == undefined)
   {
      return;
   }
   mgHold(b.UP);
   mgHold(b.HOVER);
   mgHold(b.DOWN);
   mgHold(b.ICON_UP);
   mgHold(b.ICON_HOVER);
   var a = mgBelow(b,"mtuCloseArt");
   a.btn = b;
   a.scale = 100;
   a.speed = 0;
   a.disc = a.createEmptyMovieClip("disc",1);
   a.onEnterFrame = mtuCloseTick;
}

function mtuCloseTick()
{
   var b = this.btn;
   this._x = b._x;
   this._y = b._y;
   this._visible = b._visible;
   var done = b.graphicID == 1;
   var over = b.HOVER._visible || b.DOWN._visible;
   var key = done + "," + over;
   var d = this.disc;
   if(key != this.key)
   {
      this.key = key;
      var c = MTU_CLOSE;
      d._x = c.x;
      d._y = c.y;
      d.clear();
      mgGoldCircle(d,c.r,over);
      d.lineStyle(5,MG_INK,100);
      if(done)
      {
         d.moveTo(-10,0);
         d.lineTo(-3,8);
         d.lineTo(11,-8);
      }
      else
      {
         d.moveTo(-8,-8);
         d.lineTo(8,8);
         d.moveTo(8,-8);
         d.lineTo(-8,8);
      }
      d.filters = over ? mgGoldGlow() : [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];
   }
   // A springy grow on hover.
   var target = over ? 110 : 100;
   this.speed = this.speed * 0.62 + (target - this.scale) * 0.28;
   this.scale += this.speed;
   d._xscale = d._yscale = this.scale;
}

// --- the "don't show again" checkbox ---------------------------------------------------

function mtuCheckbox(box)
{
   if(box == undefined)
   {
      return;
   }
   mgHold(box.mcCheck.mcImage);
   mgHold(box.mcCheckOff.mcImage);
   var a = box.createEmptyMovieClip("mtuBox",box.getNextHighestDepth());
   a.box = box;
   a.onEnterFrame = function()
   {
      var on = this.box.mcCheck._visible == true;
      if(on == this.on)
      {
         return;
      }
      this.on = on;
      this.clear();
      if(on)
      {
         mgGradientRect(this,1,1,28,28,8,MG_GOLD_LIGHT,MG_GOLD_DEEP,100,MG_GOLD_RIM,100,2);
         this.lineStyle(4,MG_INK,100);
         this.moveTo(8,15);
         this.lineTo(13,21);
         this.lineTo(22,9);
      }
      else
      {
         mgGradientRect(this,1,1,28,28,8,0x0A1440,0x162660,90,MG_GOLD,100,2);
      }
      this.filters = [new flash.filters.DropShadowFilter(2,90,MG_SHADOW,0.5,5,5,1,2)];
   };
}

mtuBuild();
// ===== End Sanctuary Minigame Tutorial =====

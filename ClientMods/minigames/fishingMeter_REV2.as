// ===== Sanctuary Fishing Tension Meter =====
// Appended to fishingMeter_REV2's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The line
// tension meter (wndFishingTensionMeter) as a navy glass capsule with a gold rim around a strong, glossy fill that
// keeps the stock colours' meaning (red at the top where the line snaps, bright cyan in the middle, blue at the
// slack end), with tick marks, a gold level line that follows the bobber, and the snap and lost-fish icons in
// frosted medallions at the ends. Near the top the rim glows red. The bobber (or the fish icon Lua puts on it) gets
// a dark outline so it reads against the fill. The stock tube goes invisible.
// setTension moves the bobber with gotoAndStop, which runs this frame's script again when it lands on frame 1, so
// everything here is safe to run twice and the drawing is made again if a rewind took it away.

var FM_X = 24;
var FM_W = 38;
var FM_TOP = 50;
var FM_BOTTOM = 404;
// The bobber's center (the stock pointer's 64px holder at its scale) at no tension and at full tension.
var FM_SLACK_Y = 375;
var FM_TAUT_Y = 81;

function fmEnsure()
{
   if(_root.sanctuaryTension._parent != undefined)
   {
      return;
   }
   var m = mgUnder(_root,"sanctuaryTension");
   m.capsule = m.createEmptyMovieClip("capsule",1);
   m.level = m.createEmptyMovieClip("level",2);
   m.sockets = m.createEmptyMovieClip("sockets",3);
   fmDrawCapsule(m.capsule);
   fmDrawSockets(m.sockets);
   m.level.filters = [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,3,2)];
   m.onEnterFrame = fmTick;
}

// The stock tube stays invisible and the bobber outlined, even if a rewind placed them again.
function fmStyleStock()
{
   fishMeter._alpha = 0;
   if(fishPointer1.fmStyled != true)
   {
      fishPointer1.fmStyled = true;
      fishPointer1.filters = [new flash.filters.GlowFilter(0x0A1030,0.85,5,5,2,2)];
   }
}

function fmDrawCapsule(g)
{
   var x = FM_X;
   var w = FM_W;
   var top = FM_TOP;
   var h = FM_BOTTOM - FM_TOP;
   mgGradientRect(g,x,top,w,h,w / 2,0x2E4FB0,0x141F55,95,MG_GOLD,100,2.5);
   // The fill: red (snaps) at the top, cyan in the middle, blue at the slack end.
   var fx = x + 6;
   var fw = w - 12;
   var ft = top + 8;
   var fh = h - 16;
   g.lineStyle(1,0x050A24,60);
   g.beginGradientFill("linear",[0xFF3B30,0xFF7A2E,0x3D6BFF,0x26E6FF,0x26E6FF,0x2F7BFF,0x2B4BD8],[100,100,100,100,100,100,100],[0,22,60,115,150,205,255],{matrixType:"box",x:fx,y:ft,w:fw,h:fh,r:Math.PI / 2});
   mgRoundRect(g,fx,ft,fw,fh,fw / 2);
   g.endFill();
   // A glossy stripe down the left of the fill and a darker edge on the right, so it reads as a tube.
   g.lineStyle();
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:fx + 3,y:0,w:fw * 0.45,h:10,r:0});
   mgRoundRect(g,fx + 3,ft + 10,fw * 0.4,fh - 20,fw * 0.2);
   g.endFill();
   g.beginGradientFill("linear",[0x050A24,0x050A24],[0,35],[0,255],{matrixType:"box",x:fx + fw * 0.6,y:0,w:fw * 0.4,h:10,r:0});
   mgRoundRect(g,fx + fw * 0.6,ft + 6,fw * 0.4 - 1,fh - 12,fw * 0.2);
   g.endFill();
   // Ticks every tenth of the bobber's travel, longer at the half.
   var i;
   for(i = 1; i < 10; i++)
   {
      var y = FM_SLACK_Y + (FM_TAUT_Y - FM_SLACK_Y) * i / 10;
      g.lineStyle(i == 5 ? 2 : 1.2,0xFFFFFF,i == 5 ? 80 : 50);
      g.moveTo(x + w - 2,y);
      g.lineTo(x + w + (i == 5 ? 9 : 5),y);
   }
   g.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];
}

// Frosted medallions behind the snap icon (top) and the lost-fish icon (bottom), with gold rims.
function fmDrawSockets(g)
{
   mgSocketCircle(g,36,40,21,MG_GOLD,100,2.5);
   mgSocketCircle(g,38,421,21,MG_GOLD,100,2.5);
   g.filters = [new flash.filters.DropShadowFilter(2,90,MG_SHADOW,0.6,6,6,1,2)];
}

// The gold level line at the bobber, and the red glow when the line is about to snap.
function fmTick()
{
   var p = fishPointer1;
   var l = this.level;
   l.clear();
   if(p._visible)
   {
      var y = p._y + 32 * p._yscale / 100;
      l.lineStyle(3,MG_GOLD_LIGHT,100);
      l.moveTo(FM_X + 2,y);
      l.lineTo(FM_X + FM_W - 2,y);
      l.lineStyle();
      l.beginFill(MG_GOLD,100);
      l.moveTo(FM_X + FM_W + 2,y);
      l.lineTo(FM_X + FM_W + 11,y - 6);
      l.lineTo(FM_X + FM_W + 11,y + 6);
      l.lineTo(FM_X + FM_W + 2,y);
      l.endFill();
   }
   if(_root._currentframe > 170)
   {
      var k = (Math.sin(getTimer() / 110) + 1) / 2;
      this.capsule.filters = [new flash.filters.GlowFilter(0xFF3B30,0.5 + 0.4 * k,12 + 8 * k,12 + 8 * k,2,2),new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];
      this.danger = true;
   }
   else if(this.danger)
   {
      this.capsule.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];
      this.danger = false;
   }
}

// Lua sets the tension with setTension (a gotoAndStop); the drawing is checked after every move.
var fmSetTension = setTension;
setTension = function(numTension)
{
   var result = fmSetTension.apply(_root,arguments);
   fmEnsure();
   fmStyleStock();
   return result;
};

fmEnsure();
fmStyleStock();
// ===== End Sanctuary Fishing Tension Meter =====

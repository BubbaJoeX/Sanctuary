// ===== Sanctuary Minigame Timer =====
// Appended to timer's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The countdown and
// stopwatch every minigame shows (wndFlashTimer, and the same movie in wndRaceTimer and wndPiratesTimer) are bare
// digits; they get a navy glass pill with a gold rim and a gold stopwatch glyph that fits itself around the digits
// in use (it grows and shrinks as minutes and hours come and go). When the time runs short (the stock code switches
// to its red digits) the rim turns red and the pill pulses, so it reads at a glance. The digits are untouched.

var TM_PAD = 14;
var TM_TOP = 6;
var TM_H = 60;
var TM_GLYPH = 40;

var tmPlate;
var tmLeft = -1;
var tmHurry = false;

function tmBuild()
{
   tmPlate = mgUnder(_root,"sanctuaryTimer");
   tmPlate.bg = tmPlate.createEmptyMovieClip("bg",1);
   tmPlate.glyph = tmPlate.createEmptyMovieClip("glyph",2);
   tmPlate._visible = false;
   tmPlate.onEnterFrame = tmPulse;
}

// The leftmost digit on show (d0..d6, the colons c1/c2 and the point p1), or -1 when the timer is blank.
function tmLeftmost()
{
   var names = ["d0","d1","c1","d2","d3","c2","d4","d5","p1","d6"];
   var i;
   for(i = 0; i < names.length; i++)
   {
      var d = _root[names[i]];
      if(d.getDigit != undefined && tmDigitShown(d))
      {
         return d._x;
      }
   }
   return -1;
}

function tmDigitShown(d)
{
   var name;
   for(name in d)
   {
      if(typeof d[name] == "movieclip" && d[name]._visible)
      {
         return true;
      }
   }
   return false;
}

function tmRedraw()
{
   var left = tmLeftmost();
   var hurry = prefix == "r";
   if(left == tmLeft && hurry == tmHurry)
   {
      return;
   }
   tmLeft = left;
   tmHurry = hurry;
   tmPlate._visible = left >= 0;
   if(left < 0)
   {
      return;
   }
   var right = d6._x + 24 + TM_PAD;
   var x = left - 24 - TM_PAD;
   // Room for the stopwatch glyph on the left unless all the digits are in use.
   var glyph = x - TM_GLYPH >= 2;
   if(glyph)
   {
      x -= TM_GLYPH;
   }
   var g = tmPlate.bg;
   g.clear();
   mgGradientRect(g,x,TM_TOP,right - x,TM_H,TM_H / 2,0x2E4FB0,0x141F55,92,hurry ? 0xFF6A5A : MG_GOLD,100,2.5);
   mgGloss(g,x + 10,TM_TOP + 3,right - x - 20,TM_H * 0.42,14,30);
   g.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];

   var c = tmPlate.glyph;
   c.clear();
   c._visible = glyph;
   if(glyph)
   {
      c._x = x + 30;
      c._y = TM_TOP + TM_H / 2 + 2;
      tmDrawStopwatch(c,hurry ? 0xFF8A70 : MG_GOLD_LIGHT,hurry ? 0xD03A3A : MG_GOLD_DEEP);
   }
}

function tmDrawStopwatch(c, top, bottom)
{
   // The crown and button, the face and two hands.
   c.lineStyle(1.5,MG_GOLD_RIM,100);
   c.beginGradientFill("linear",[top,bottom],[100,100],[0,255],{matrixType:"box",x:-4,y:-21,w:8,h:6,r:Math.PI / 2});
   mgRoundRect(c,-4,-21,8,6,2);
   c.endFill();
   c.beginGradientFill("linear",[top,bottom],[100,100],[0,255],{matrixType:"box",x:-14,y:-14,w:28,h:28,r:Math.PI / 2});
   mgCircle(c,0,0,14);
   c.endFill();
   c.lineStyle();
   c.beginFill(MG_NAVY,90);
   mgCircle(c,0,0,9.5);
   c.endFill();
   c.lineStyle(2.5,0xFFFFFF,100);
   c.moveTo(0,0);
   c.lineTo(0,-7);
   c.moveTo(0,0);
   c.lineTo(5,2);
   c.filters = [new flash.filters.DropShadowFilter(2,90,MG_SHADOW,0.6,4,4,1,2)];
}

// In a hurry the pill breathes a red glow; otherwise it rests.
function tmPulse()
{
   if(!tmHurry)
   {
      this.filters = [];
      return;
   }
   var k = (Math.sin(getTimer() / 160) + 1) / 2;
   this.filters = [new flash.filters.GlowFilter(0xFF4A3A,0.35 + 0.45 * k,10 + 10 * k,10 + 10 * k,2,2)];
}

// Every tick of the countdown or stopwatch goes through setTimer, and resetTimer blanks it.
var tmSetTimer = setTimer;
setTimer = function(s, r)
{
   var result = tmSetTimer.apply(_root,arguments);
   tmRedraw();
   return result;
};

tmBuild();
tmRedraw();
// ===== End Sanctuary Minigame Timer =====

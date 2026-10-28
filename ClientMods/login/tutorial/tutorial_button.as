// ===== Sanctuary Tutorial Button =====
// Appended to tutorialButton's main timeline by build_client_mods.py.
// The little "?" HUD button that reopens the tutorial (showTutorialButton()), in the Main Menu's style: a navy glass
// disc with a pale blue rim and a gold "?", glowing gold and growing a little on hover. The stock button stays
// over it as the invisible hit area (its own roll handlers still show the Tutorial tooltip).

var TBU_RIM = 0x9DB8FF;
var TBU_GOLD = 0xFFC531;

function tbuCircle(mc, x, y, r)
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

function tbuDraw(over)
{
   var d = tbuDisc;
   var r = 21;
   d.bg.clear();
   d.bg.lineStyle(2,over ? 0xFFE08A : TBU_RIM,over ? 100 : 85);
   d.bg.beginGradientFill("linear",over ? [0x3F66D6,0x1D307A] : [0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   tbuCircle(d.bg,0,0,r);
   d.bg.endFill();
   d.bg.lineStyle();
   d.bg.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[35,0],[0,255],{matrixType:"box",x:-14,y:-19,w:28,h:14,r:Math.PI / 2});
   tbuCircle(d.bg,0,-12,7);
   d.bg.endFill();
   d._xscale = d._yscale = over ? 110 : 100;
   d.filters = over ? [new flash.filters.GlowFilter(TBU_GOLD,0.8,10,10,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];
}

var tbuDisc;

function tbuBuild()
{
   var d = tbuDisc = _root.createEmptyMovieClip("sanctuaryHelp",-16384);
   d._x = button._x;
   d._y = button._y;
   d.bg = d.createEmptyMovieClip("bg",1);
   d.createTextField("mark_txt",2,-20,-17,40,34);
   var t = d.mark_txt;
   t.embedFonts = true;
   t.selectable = false;
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = 26;
   f.color = 0xFFE08A;
   f.align = "center";
   t.setNewTextFormat(f);
   t.text = "?";
   t.filters = [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
   tbuDraw(false);
   button._alpha = 0;

   var roll = button.onRollOver;
   button.onRollOver = button.onDragOver = function()
   {
      roll.call(this);
      tbuDraw(true);
   };
   var out = button.onRollOut;
   button.onRollOut = button.onDragOut = function()
   {
      out.call(this);
      tbuDraw(false);
   };
}

tbuBuild();
// ===== End Sanctuary Tutorial Button =====

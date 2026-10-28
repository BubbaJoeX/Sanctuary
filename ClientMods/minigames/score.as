// ===== Sanctuary Minigame Score =====
// Appended to score's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The in-game score
// (wndFlashScore) is bare digits under a "Score" label; they get a navy glass plate with a gold rim that fits
// itself around the digits in use (growing to the left as the score gains digits, like minigames/timer.as), and the
// label sits on a navy tab with a gold rim on top of it, in light gold with the dark outline. The digits and their
// count-up are untouched.

var SCO_RIGHT = 217;
var SCO_TOP = 27;
var SCO_H = 58;
var SCO_PAD = 26;
// The label's tab, centered on the stock label (a centered field, 63..214).
var SCO_TAB_X = 138;
var SCO_TAB_Y = 3;
var SCO_TAB_H = 28;

var scoPlate;
var scoLabel;
var scoTabW = 100;
var scoLeft = -1;

function scoBuild()
{
   scoPlate = mgUnder(_root,"sanctuaryScore");
   scoPlate.bg = scoPlate.createEmptyMovieClip("bg",1);
   scoPlate.tab = scoPlate.createEmptyMovieClip("tab",2);
   scoPlate._visible = false;

   // The label is an unnamed field; when it can be found it's restyled and the tab fits it.
   scoLabel = scoFindLabel();
   if(scoLabel != undefined)
   {
      mgLight(scoLabel,MG_GOLD_LIGHT);
      if(scoLabel.textWidth > 0)
      {
         scoTabW = Math.min(150,Math.round(scoLabel.textWidth + 30));
      }
   }
}

function scoFindLabel()
{
   var name;
   for(name in _root)
   {
      if(_root[name] instanceof TextField)
      {
         return _root[name];
      }
   }
   return undefined;
}

// The leftmost digit on show (d0 is the units, d6 the millions, c1/c2 the separators), or -1 when blank.
function scoLeftmost()
{
   var names = ["d6","c2","d5","d4","d3","c1","d2","d1","d0"];
   var i;
   for(i = 0; i < names.length; i++)
   {
      var d = _root[names[i]];
      if(d.getDigit != undefined && scoDigitShown(d))
      {
         return d._x;
      }
   }
   return -1;
}

function scoDigitShown(d)
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

function scoRedraw()
{
   var left = scoLeftmost();
   if(left == scoLeft)
   {
      return;
   }
   scoLeft = left;
   scoPlate._visible = left >= 0;
   if(left < 0)
   {
      return;
   }
   // Never narrower than the label's tab.
   var x = Math.max(2,Math.min(left - SCO_PAD,SCO_TAB_X - scoTabW / 2 - 10));
   var g = scoPlate.bg;
   g.clear();
   mgGradientRect(g,x,SCO_TOP,SCO_RIGHT - x,SCO_H,SCO_H / 2,0x2E4FB0,0x141F55,92,MG_GOLD,100,2.5);
   mgGloss(g,x + 10,SCO_TOP + 3,SCO_RIGHT - x - 20,SCO_H * 0.42,14,30);
   g.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];

   var t = scoPlate.tab;
   t.clear();
   mgGradientRect(t,SCO_TAB_X - scoTabW / 2,SCO_TAB_Y,scoTabW,SCO_TAB_H,SCO_TAB_H / 2,0x284A9E,0x162660,100,MG_GOLD,100,2);
   mgGloss(t,SCO_TAB_X - scoTabW / 2 + 8,SCO_TAB_Y + 2,scoTabW - 16,SCO_TAB_H * 0.4,6,30);
}

// Every change of the shown score (showScore, setScore and its count-up tick) goes through updateScore.
var scoUpdateScore = updateScore;
updateScore = function()
{
   var result = scoUpdateScore.apply(_root,arguments);
   scoRedraw();
   return result;
};

scoBuild();
scoRedraw();
// ===== End Sanctuary Minigame Score =====

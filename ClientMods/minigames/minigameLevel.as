// ===== Sanctuary Minigame Level =====
// Appended to minigameLevel's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The level
// counter some minigames show (wndMiniGameLevel) is bare digits under a "Level" label; it gets the score's look
// (minigames/score.as): a navy glass plate with a gold rim behind the digits and the label on a gold-rimmed navy tab
// in light gold with the dark outline. The plate covers both digit slots (the stock movie names both of them d0, so
// only one is ever set), so it doesn't change size. The digits are untouched.

var MLV_RIGHT = 217;
var MLV_TOP = 27;
var MLV_H = 58;
// The second digit slot is at 172.5, the first at 200 (each 48 wide).
var MLV_DIGITS_LEFT = 140;
// The label's tab, centered on the stock label (a centered field, 33..211).
var MLV_TAB_X = 122;
var MLV_TAB_Y = 3;
var MLV_TAB_H = 28;

var mlvPlate;
var mlvTabW = 110;

function mlvBuild()
{
   mlvPlate = mgUnder(_root,"sanctuaryLevel");
   var label = mlvFindLabel();
   if(label != undefined)
   {
      mgLight(label,MG_GOLD_LIGHT);
      if(label.textWidth > 0)
      {
         mlvTabW = Math.min(176,Math.round(label.textWidth + 30));
      }
   }

   var x = Math.min(MLV_DIGITS_LEFT,MLV_TAB_X - mlvTabW / 2 - 10);
   var g = mlvPlate.createEmptyMovieClip("bg",1);
   mgGradientRect(g,x,MLV_TOP,MLV_RIGHT - x,MLV_H,MLV_H / 2,0x2E4FB0,0x141F55,92,MG_GOLD,100,2.5);
   mgGloss(g,x + 10,MLV_TOP + 3,MLV_RIGHT - x - 20,MLV_H * 0.42,14,30);
   g.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];
   // A small gold star at the plate's left end, when the label leaves room for it.
   if(MLV_DIGITS_LEFT - x >= 36)
   {
      var s = mlvPlate.createEmptyMovieClip("star",2);
      mgStar(s,x + 30,MLV_TOP + MLV_H / 2 + 1,14,MG_GOLD,100);
      mgStar(s,x + 30,MLV_TOP + MLV_H / 2,8,0xFFF3C4,90);
      s.filters = [new flash.filters.DropShadowFilter(2,90,MG_SHADOW,0.6,4,4,1,2)];
   }

   var t = mlvPlate.createEmptyMovieClip("tab",3);
   mgGradientRect(t,MLV_TAB_X - mlvTabW / 2,MLV_TAB_Y,mlvTabW,MLV_TAB_H,MLV_TAB_H / 2,0x284A9E,0x162660,100,MG_GOLD,100,2);
   mgGloss(t,MLV_TAB_X - mlvTabW / 2 + 8,MLV_TAB_Y + 2,mlvTabW - 16,MLV_TAB_H * 0.4,6,30);
}

function mlvFindLabel()
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

mlvBuild();
// ===== End Sanctuary Minigame Level =====

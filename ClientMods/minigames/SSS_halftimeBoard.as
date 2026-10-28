// ===== Sanctuary Soccer Halftime Board =====
// Appended to SSS_halftimeBoard's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The halftime
// and final score board (wndSSSHalftimeBoard): its outer frame (a plain shape, dropped with REMOVED_DEPTHS) becomes
// the Main Menu's navy glass frame with a sparkling header, "Halftime!" / "Game Over!" in gold with the dark
// outline, a gold "VS" medallion between the two team panels, and the stats in soft blue and white. The team
// panels and their big coloured digits are the stock ones (they show each team's colour). The beige exit button is
// the gold close circle in the top right corner; it still closes the board at halftime and leaves the game at the
// end.

var HTB_X = 4;
var HTB_Y = 4;
var HTB_W = 687;
var HTB_H = 668;
var HTB_HEADER_H = 58;

function htbBuild()
{
   var f = mgUnder(_root,"sanctuaryHalftimeBoard");
   var bg = f.createEmptyMovieClip("bg",1);
   mgFrame(bg,HTB_X,HTB_Y,HTB_W,HTB_H,34,HTB_HEADER_H,4848);
   // A recess under the team panels and the stats.
   var p = f.createEmptyMovieClip("panels",2);
   mgPanel(p,40,88,606,500,26,35,22);
   // A gold hairline under the header.
   p.lineStyle();
   p.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:120,y:0,w:446,h:2,r:0});
   mgRoundRect(p,120,HTB_Y + HTB_HEADER_H + 2,446,2,1);
   p.endFill();

   // "VS" between the two team names, above the panels.
   var vs = _root.createEmptyMovieClip("sanctuaryVersus",_root.getNextHighestDepth());
   vs._x = 343.5;
   vs._y = 86;
   vs.lineStyle(2,MG_GOLD_RIM,100);
   vs.beginGradientFill("linear",[MG_GOLD_LIGHT,MG_GOLD_DEEP],[100,100],[0,255],{matrixType:"box",x:-22,y:-22,w:44,h:44,r:Math.PI / 2});
   mgCircle(vs,0,0,22);
   vs.endFill();
   mgGloss(vs,-15,-19,30,14,6,50);
   mgText(vs,"vs_txt","VS",-22,-13,44,26,17,MG_INK);
   vs.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];

   mgLight(bg.title,MG_GOLD_LIGHT);
   mgLight(bg.leftTeamName,0xFFFFFF);
   mgLight(bg.rightTeamName,0xFFFFFF);
   mgLight(bg.gameEndMessage,MG_GOLD_LIGHT);
   var sides = ["left","right"];
   var stats = ["Shots","Knockdowns","Pickups","Steals"];
   var i;
   var j;
   for(i = 0; i < 2; i++)
   {
      for(j = 0; j < stats.length; j++)
      {
         mgLight(bg[sides[i] + stats[j] + "Label"],MG_SOFT);
         mgLight(bg[sides[i] + stats[j] + "Value"],0xFFFFFF);
      }
   }

   // The exit button (a stock FRButton, its art 77x72) moves to the top right corner as the gold close circle.
   exitBtn._xscale = exitBtn._yscale = 48;
   exitBtn._x = HTB_X + HTB_W - 36 - 18.5;
   exitBtn._y = HTB_Y + 36 - 17.3;
   mgSkin(exitBtn,"close","",{x:0,y:0,w:77,h:72});
}

htbBuild();
// ===== End Sanctuary Soccer Halftime Board =====

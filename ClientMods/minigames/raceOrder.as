// ===== Sanctuary Race Order =====
// Appended to raceOrder's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The live standings
// during a race (wndRaceOrder): four rows, each either a racer (small) or you (large). Every row becomes a navy glass
// pill with the place in a gold, silver or bronze disc (navy from 4th on) and the portrait in a frosted socket; your
// own row is a bigger card rimmed and glowing in gold, so you find yourself at a glance. A glass tray with a
// "Standings" tab holds the list. The stock code keeps choosing which row shows (setPlace / setPlayerPlace).

var RO_MEDALS = [[0xFFE08A,0xF5A623,0xFFF3C4],[0xF4F7FF,0xAFC0DA,0xFFFFFF],[0xF2B27A,0xB8662E,0xFFD9B0]];

function roBuild()
{
   var tray = mgUnder(_root,"sanctuaryRaceOrder");
   var g = tray.createEmptyMovieClip("bg",1);
   mgGradientRect(g,0,18,270,196,18,0x2E4FB0,0x141F55,70,MG_RIM,60,1.5);
   mgGloss(g,8,20,254,30,12,18);
   g.filters = mgShadow();
   // The tab on top.
   var tab = tray.createEmptyMovieClip("tab",2);
   mgGradientRect(tab,12,0,108,24,12,MG_GOLD_LIGHT,MG_GOLD_DEEP,100,MG_GOLD_RIM,100,1.5);
   mgGloss(tab,18,1,96,10,5,45);
   mgText(tab,"label_txt","STANDINGS",12,3,108,20,13,MG_INK);

   var i;
   for(i = 1; i <= 4; i++)
   {
      roStyleRow(_root["pos" + i],i,false);
      roStyleRow(_root["pos" + i + "large"],i,true);
   }
}

function roChild(row, prefix)
{
   var name;
   for(name in row)
   {
      if(name.indexOf(prefix) == 0)
      {
         return row[name];
      }
   }
   return undefined;
}

function roStyleRow(row, place, large)
{
   if(row == undefined)
   {
      return;
   }
   var medal = place <= 3 ? RO_MEDALS[place - 1] : undefined;
   var g = mgUnder(row,"sanctuaryCard");
   var portrait = roChild(row,large ? "largePortrait" : "smallPortrait");
   var px = portrait._x;
   var py = portrait._y;
   if(large)
   {
      // You: a gold rimmed card, the place disc and a gold ringed socket.
      mgGradientRect(g,-2,-6,262,46,23,0x3F66D6,0x1D307A,100,MG_GOLD,100,2.5);
      mgGloss(g,10,-4,238,16,8,25);
      roDisc(g,25,18,19,medal);
      roSocket(g,px,py,30,MG_GOLD);
      g.filters = [new flash.filters.GlowFilter(MG_GOLD,0.7,14,14,2,2)];
   }
   else
   {
      mgGradientRect(g,-2,-1,222,28,14,0x284A9E,0x162660,92,MG_RIM,45,1.5);
      mgGloss(g,8,0,204,10,5,18);
      roDisc(g,17,12.5,13,medal);
      roSocket(g,px,py,17,medal != undefined ? medal[2] : MG_RIM);
      g.filters = mgShadow();
   }
   roCenter(roChild(row,"place"));
   var name;
   for(name in row)
   {
      if(row[name] instanceof TextField)
      {
         if(name.indexOf("place") == 0 && place <= 3)
         {
            // Dark on the podium's gold, silver and bronze discs.
            mgLight(row[name],MG_INK,false);
         }
         else
         {
            mgLight(row[name],large && name.indexOf("name") == 0 ? MG_GOLD_LIGHT : 0xFFFFFF);
         }
      }
   }
}

// The place text sits centered in its disc.
function roCenter(t)
{
   var f = t.getTextFormat();
   f.align = "center";
   t.setTextFormat(f);
   t.setNewTextFormat(f);
}

// The place disc: gold, silver or bronze for the podium, navy from 4th on.
function roDisc(g, x, y, r, medal)
{
   g.lineStyle(1.5,medal != undefined ? medal[2] : MG_RIM,medal != undefined ? 100 : 70);
   g.beginGradientFill("linear",medal != undefined ? [medal[0],medal[1]] : [0x3F66D6,0x1D307A],[100,100],[0,255],{matrixType:"box",x:x - r,y:y - r,w:r * 2,h:r * 2,r:Math.PI / 2});
   mgCircle(g,x,y,r);
   g.endFill();
   mgGloss(g,x - r * 0.65,y - r * 0.85,r * 1.3,r * 0.6,r * 0.3,45);
}

function roSocket(g, x, y, r, rim)
{
   g.lineStyle(2,rim,100);
   g.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.3,y:y - r * 1.5,w:r * 2.6,h:r * 2.6,r:0});
   mgCircle(g,x,y,r);
   g.endFill();
}

roBuild();
// ===== End Sanctuary Race Order =====

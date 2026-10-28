// ===== Sanctuary Race Results =====
// Appended to raceResults' main timeline (after minigames/mg_theme.as) by build_client_mods.py. The race's
// results board (wndRaceResults) in the Main Menu's style: a navy glass frame with "Race Results" in gold between
// two chequered flags, the column names (Place, Name, Best Lap, Total Time) in a recessed gold band, and each racer
// on a glass card with the place in a gold, silver or bronze pill (navy from 4th on), the portrait in a frosted
// socket and a green tick on it once they've finished (their times in gold; racers still out are softer blue).
// The rows sit a little closer so the title fits; cards slide in as Lua fills them (setPlayerResults).

var RR_W = 520;
var RR_H = 700;
var RR_HEADER_Y = 60;
var RR_ROW_Y = 104;
var RR_ROW_PITCH = 58.5;
var RR_CARD_W = 494;
var RR_PORTRAIT_SCALE = 78;
// Pill colours for 1st, 2nd and 3rd (top, bottom, rim); the rest are navy.
var RR_MEDALS = [[0xFFE08A,0xF5A623,0xFFF3C4],[0xF4F7FF,0xAFC0DA,0xFFFFFF],[0xF2B27A,0xB8662E,0xFFD9B0]];

var rrRows = [];

function rrBuild()
{
   var f = mgUnder(_root,"sanctuaryRaceResults");
   var bg = f.createEmptyMovieClip("bg",1);
   mgFrame(bg,4,4,RR_W - 8,RR_H - 8,26,48,7311);
   var title = mgText(f,"title_txt","Race Results",0,10,RR_W,40,28,MG_GOLD_LIGHT);
   title.filters = mgOutline();
   var flags = f.createEmptyMovieClip("flags",f.getNextHighestDepth());
   var half = title.textWidth / 2;
   rrFlag(flags,RR_W / 2 - half - 46,18,false);
   rrFlag(flags,RR_W / 2 + half + 18,18,true);

   // The column names: a recessed band with a gold hairline under it.
   var band = f.createEmptyMovieClip("band",f.getNextHighestDepth());
   mgPanel(band,12,RR_HEADER_Y - 4,RR_W - 24,38,19,50,30);
   band.lineStyle();
   band.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:40,y:0,w:RR_W - 80,h:2,r:0});
   mgRoundRect(band,40,RR_HEADER_Y + 34,RR_W - 80,2,1);
   band.endFill();

   var name;
   for(name in _root)
   {
      var c = _root[name];
      if(typeof c == "movieclip" && c._parent == _root && name.indexOf("pos") != 0 && c != f && c.getDepth() > -16384)
      {
         if(c.getDepth() < 0)
         {
            c._y = RR_HEADER_Y;
            rrLightAll(c,MG_GOLD_LIGHT,18);
         }
      }
   }

   var i;
   for(i = 1; i <= 10; i++)
   {
      rrStyleRow(_root["pos" + i + "large"],i);
   }
}

// A small chequered flag on a pole, 28 wide; mirrored points it the other way.
function rrFlag(mc, x, y, mirrored)
{
   var g = mc.createEmptyMovieClip("flag" + mc.getNextHighestDepth(),mc.getNextHighestDepth());
   g._x = mirrored ? x + 28 : x;
   g._y = y;
   g._xscale = mirrored ? -100 : 100;
   g.lineStyle(3,MG_GOLD_LIGHT,100);
   g.moveTo(2,0);
   g.lineTo(2,26);
   g.lineStyle();
   var r;
   var c;
   for(r = 0; r < 3; r++)
   {
      for(c = 0; c < 4; c++)
      {
         g.beginFill((r + c) % 2 == 0 ? 0xFFFFFF : 0x101830,100);
         g.moveTo(4 + c * 6,1 + r * 5 + c * 0.5);
         g.lineTo(10 + c * 6,1 + r * 5 + (c + 1) * 0.5);
         g.lineTo(10 + c * 6,6 + r * 5 + (c + 1) * 0.5);
         g.lineTo(4 + c * 6,6 + r * 5 + c * 0.5);
         g.endFill();
      }
   }
   g.filters = mgOutline();
}

// The column names, a little smaller than stock so "Total Time" stays inside the frame.
function rrLightAll(mc, color, size)
{
   var name;
   for(name in mc)
   {
      if(mc[name] instanceof TextField)
      {
         var f = mc[name].getTextFormat();
         f.size = size;
         mc[name].setTextFormat(f);
         mgLight(mc[name],color);
      }
   }
}

// Finds a row's fields by their name's start (place1stlarge, name1stlarge, fastestlap1st, totaltime1st, largePortrait1st).
function rrChild(row, prefix)
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

function rrStyleRow(row, place)
{
   if(row == undefined)
   {
      return;
   }
   row._y = RR_ROW_Y + (place - 1) * RR_ROW_PITCH;
   row.rrPlace = place;
   row.rrBaseX = row._x;
   row.rrFinished = false;
   row.rrShown = false;
   row.card = mgUnder(row,"sanctuaryCard");
   row.place_txt = rrChild(row,"place");
   row.name_txt = rrChild(row,"name");
   row.best_txt = rrChild(row,"fastestlap");
   row.total_txt = rrChild(row,"totaltime");
   // Scaled before Lua's first portrait load, which records the holder's size and fits the portrait to it.
   var portrait = rrChild(row,"largePortrait");
   portrait._xscale = portrait._yscale = RR_PORTRAIT_SCALE;
   row.portraitX = portrait._x;
   row.portraitY = portrait._y;
   var fmt = row.place_txt.getTextFormat();
   fmt.align = "center";
   row.place_txt.setTextFormat(fmt);
   row.place_txt.setNewTextFormat(fmt);
   // Dark on the podium's gold, silver and bronze pills, white on navy.
   mgLight(row.place_txt,place <= 3 ? MG_INK : 0xFFFFFF,place > 3);
   mgLight(row.name_txt,0xFFFFFF);
   mgLight(row.best_txt,MG_SOFT);
   mgLight(row.total_txt,MG_SOFT);
   row.tick = row.createEmptyMovieClip("sanctuaryTick",row.getNextHighestDepth());
   rrDrawRow(row);
   rrRows.push(row);
}

function rrDrawRow(row)
{
   var g = row.card;
   var place = row.rrPlace;
   var medal = place <= 3 ? RR_MEDALS[place - 1] : undefined;
   g.clear();
   mgGradientRect(g,4,-11,RR_CARD_W,51,20,0x284A9E,0x162660,100,medal != undefined ? medal[2] : MG_RIM,medal != undefined ? 90 : 45,medal != undefined ? 2 : 1.5);
   mgGloss(g,12,-9,RR_CARD_W - 16,18,10,18);
   // The place pill.
   if(medal != undefined)
   {
      mgGradientRect(g,1,1,50,32,16,medal[0],medal[1],100,medal[2],100,1.5);
   }
   else
   {
      mgGradientRect(g,1,1,50,32,16,0x3F66D6,0x1D307A,100,MG_RIM,70,1.5);
   }
   mgGloss(g,8,2,38,12,6,45);
   // The portrait's frosted socket.
   var r = 24;
   g.lineStyle(2,medal != undefined ? medal[2] : MG_RIM,medal != undefined ? 100 : 60);
   g.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:row.portraitX - r * 1.3,y:row.portraitY - r * 1.5,w:r * 2.6,h:r * 2.6,r:0});
   mgRoundRect(g,row.portraitX - r,row.portraitY - r,r * 2,r * 2,12);
   g.endFill();
   g.filters = place == 1 ? [new flash.filters.GlowFilter(MG_GOLD,0.55,14,14,2,2)] : mgShadow();

   var t = row.tick;
   t.clear();
   t._visible = row.rrFinished;
   if(row.rrFinished)
   {
      var x = row.portraitX + 21;
      var y = row.portraitY + 20;
      t.lineStyle(1.5,0xFFFFFF,100);
      t.beginGradientFill("linear",[0x7BE36B,0x2E9E3A],[100,100],[0,255],{matrixType:"box",x:x - 10,y:y - 10,w:20,h:20,r:Math.PI / 2});
      mgCircle(t,x,y,10);
      t.endFill();
      t.lineStyle(3,0xFFFFFF,100);
      t.moveTo(x - 5,y);
      t.lineTo(x - 1.5,y + 4);
      t.lineTo(x + 5,y - 4);
      t.filters = [new flash.filters.DropShadowFilter(2,90,MG_SHADOW,0.6,4,4,1,2)];
   }
   row.best_txt.mgColor = row.total_txt.mgColor = row.rrFinished ? MG_GOLD_LIGHT : MG_SOFT;
}

// A card slides in from the right as it's first shown.
function rrSlideIn(row)
{
   row.rrT = 0;
   row._x = row.rrBaseX + 60;
   row._alpha = 0;
   row.card.onEnterFrame = function()
   {
      var r = this._parent;
      r.rrT += 0.12;
      var k = r.rrT >= 1 ? 1 : 1 - Math.pow(1 - r.rrT,4);
      r._x = r.rrBaseX + 60 * (1 - k);
      r._alpha = 100 * k;
      if(r.rrT >= 1)
      {
         r._x = r.rrBaseX;
         delete this.onEnterFrame;
      }
   };
}

// Lua fills a row per racer; the stock code paints finished racers green, which becomes the gold times and tick.
var rrSetPlayerResults = setPlayerResults;
setPlayerResults = function(place, guid, sname, best, total, finished)
{
   var result = rrSetPlayerResults.apply(_root,arguments);
   var row = _root["pos" + place + "large"];
   if(row != undefined && row.card != undefined)
   {
      row.rrFinished = finished == 1;
      rrDrawRow(row);
      if(!row.rrShown && row._visible)
      {
         row.rrShown = true;
         rrSlideIn(row);
      }
   }
   return result;
};

rrBuild();
// ===== End Sanctuary Race Results =====

// ===== Sanctuary Pirates Results =====
// Appended to pir_resultscreen_bbe's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The
// Pirates result screen (wndPiratesResultScreen) in the Main Menu's style: its sunburst frame (a plain shape, dropped
// with REMOVED_DEPTHS) becomes the navy glass frame with a sparkling header band behind the animated win/lose
// banner, the column headings sit on a gold-lined glass strip, and each player is a glass row with the place in a
// medallion (gold, silver and bronze for the top three) and any trophies in frosted sockets. The beige close button
// is the gold circle. The rows live inside the stock player slots, so they appear with them after the banner's
// transition; every stock field, holder and tooltip is untouched.

var PRS_X = 40;
var PRS_Y = 12;
var PRS_W = 944;
var PRS_H = 744;
var PRS_HEADER_H = 128;
// A player row, in its slot's coordinates (texts at y -5..21, the flag at -19..45, trophies at 25..57).
var PRS_ROW_X = -56;
var PRS_ROW_Y = -19;
var PRS_ROW_W = 856;
var PRS_ROW_H = 75;
var PRS_PLACE_X = -14;
var PRS_PLACE_Y = 8;
var PRS_TROPHY_X = [687,725,763];
var PRS_TROPHY_Y = 41;
// Place medallion rims: gold, silver, bronze, then the frame's pale rim.
var PRS_MEDALS = [MG_GOLD,0xDCE7FF,0xE0955A];

var prsFrame;

function prsBuild()
{
   prsFrame = mgUnder(_root,"sanctuaryPiratesResults");
   var bg = prsFrame.createEmptyMovieClip("bg",1);
   mgFrame(bg,PRS_X,PRS_Y,PRS_W,PRS_H,34,PRS_HEADER_H,8181);
   // A gold hairline under the header band.
   bg.lineStyle();
   bg.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:PRS_X + 120,y:0,w:PRS_W - 240,h:2,r:0});
   mgRoundRect(bg,PRS_X + 120,PRS_Y + PRS_HEADER_H + 6,PRS_W - 240,2,1);
   bg.endFill();

   // The column headings' strip.
   var h = mgUnder(textHeadings,"sanctuaryHeadings");
   mgPanel(h,-20,-3,856,30,15,55,30);
   h.lineStyle(1.5,MG_GOLD,60);
   h.moveTo(-6,27);
   h.lineTo(822,27);
   var names = ["placeText","flagText","nameText","goldText","shipsSunkText","scoreText"];
   var n;
   for(n = 0; n < names.length; n++)
   {
      mgLight(textHeadings[names[n]],MG_GOLD_LIGHT);
   }

   var i;
   for(i = 1; i <= 6; i++)
   {
      prsBuildRow(i);
   }

   // The close button is a plain Button (no hitTest of its own), so it reports its hover for the skin.
   close_btn.onRollOver = function()
   {
      this.mgHover = true;
   };
   close_btn.onRollOut = close_btn.onReleaseOutside = close_btn.onDragOut = function()
   {
      this.mgHover = false;
   };
   close_btn.hitTest = function()
   {
      return this.mgHover == true;
   };
   mgSkin(close_btn,"close","",{x:-38.5,y:-36,w:77,h:72});
}

function prsBuildRow(i)
{
   var slot = _root["playerSlot_" + i];
   var row = mgUnder(slot,"sanctuaryRow");
   var g = row.createEmptyMovieClip("glass",1);
   mgGradientRect(g,PRS_ROW_X,PRS_ROW_Y,PRS_ROW_W,PRS_ROW_H,20,i % 2 == 1 ? 0x284A9E : 0x223F8C,0x162660,80,i == 1 ? MG_GOLD : MG_RIM,i == 1 ? 90 : 35,i == 1 ? 2 : 1.5);
   mgGloss(g,PRS_ROW_X + 10,PRS_ROW_Y + 2,PRS_ROW_W - 20,22,10,i == 1 ? 22 : 14);
   // The place medallion.
   var rim = i <= 3 ? PRS_MEDALS[i - 1] : MG_RIM;
   g.lineStyle(2.5,rim,i <= 3 ? 100 : 60);
   g.beginGradientFill("radial",[0x2B4FA8,0x111E52],[100,100],[0,255],{matrixType:"box",x:PRS_PLACE_X - 18,y:PRS_PLACE_Y - 20,w:36,h:36,r:0});
   mgCircle(g,PRS_PLACE_X,PRS_PLACE_Y,17);
   g.endFill();
   if(i <= 3)
   {
      row.medal = row.createEmptyMovieClip("medal",2);
      row.medal.filters = [new flash.filters.GlowFilter(rim,0.5,10,10,2,2)];
      row.medal.lineStyle(1,rim,60);
      mgCircle(row.medal,PRS_PLACE_X,PRS_PLACE_Y,20);
   }
   row.sockets = row.createEmptyMovieClip("sockets",3);
   mgLight(slot.place_text,i == 1 ? MG_GOLD_LIGHT : 0xFFFFFF);
   mgLight(slot.playerName_text,0xFFFFFF);
   mgLight(slot.gold_text,MG_GOLD_LIGHT);
   mgLight(slot.shipSunk_text,0xFFFFFF);
   mgLight(slot.score_text,0xFFFFFF);
}

// Lua: populatePlayerTrophy(place, trophy, iconId, ...) loads a trophy icon; it gets a frosted socket.
var prsPopulatePlayerTrophy = populatePlayerTrophy;
populatePlayerTrophy = function(place, trophy, iconId, trophyTitle, trophyDesc)
{
   var result = prsPopulatePlayerTrophy.apply(_root,arguments);
   var row = _root["playerSlot_" + place].sanctuaryRow;
   if(iconId > -1 && trophy >= 1 && trophy <= 3 && row != undefined)
   {
      mgSocketCircle(row.sockets,PRS_TROPHY_X[trophy - 1],PRS_TROPHY_Y,17,MG_GOLD,90,1.5);
   }
   return result;
};

prsBuild();
// ===== End Sanctuary Pirates Results =====

// ===== Sanctuary Soccer Scoreboard =====
// Appended to SSS_score's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The soccer
// scoreboard (wndSSSScores): its two pale glass bars (a plain shape, dropped with REMOVED_DEPTHS) become navy glass
// rows. Each team's colour (the stock tint clips, which Lua colours with setTeamColors) is a glossy chip under its
// name, and the score sits in a dark socket on the right, rimmed in gold for the player's own team. The period
// ("SECOND HALF", "SUDDEN DEATH") is gold with the dark outline above them. The stock score fields and their
// flip animation are untouched, only outlined so they read on any team colour.

var SCB_ROW_Y = [26,78];
var SCB_ROW_H = 46;
var SCB_W = 148;
// The team colour chip and the score socket (the stock score clips are centered on x 118).
var SCB_CHIP_X = 6;
var SCB_CHIP_W = 86;
var SCB_SOCKET_X = 96;
var SCB_SOCKET_W = 46;

var scbLayer;
var scbColors = [0x3F66D6,0x3F66D6];
var scbMine = -1;

function scbBuild()
{
   scbLayer = mgUnder(_root,"sanctuaryScoreboard");
   var i;
   for(i = 0; i < 2; i++)
   {
      var row = scbLayer.createEmptyMovieClip("row" + i,i + 1);
      row._y = SCB_ROW_Y[i];
      row.plate = row.createEmptyMovieClip("plate",1);
      row.chip = row.createEmptyMovieClip("chip",2);
      row.socket = row.createEmptyMovieClip("socket",3);
      mgPlate(row.plate,0,0,SCB_W,SCB_ROW_H,14,false);
      _root["tint" + i]._visible = false;
      mgLight(_root["team" + i + "a"].txt,0xFFFFFF);
      mgLight(_root["team" + i + "b"].txt,0xFFFFFF);
      _root["score" + i].txt.filters = mgOutline();
      scbDrawRow(i);
   }
   mgLight(period.txt,MG_GOLD_LIGHT);
}

function scbDrawRow(i)
{
   var row = scbLayer["row" + i];
   var c = scbColors[i];
   var g = row.chip;
   g.clear();
   // The team's colour, a little lighter at the top and darker at the bottom, with a shine.
   g.lineStyle(1,0xFFFFFF,35);
   g.beginGradientFill("linear",[scbShade(c,1.25),scbShade(c,0.7)],[100,100],[0,255],{matrixType:"box",x:SCB_CHIP_X,y:4,w:SCB_CHIP_W,h:SCB_ROW_H - 8,r:Math.PI / 2});
   mgRoundRect(g,SCB_CHIP_X,4,SCB_CHIP_W,SCB_ROW_H - 8,10);
   g.endFill();
   mgGloss(g,SCB_CHIP_X + 4,5,SCB_CHIP_W - 8,14,6,35);

   var mine = i == scbMine;
   var s = row.socket;
   s.clear();
   s.lineStyle(mine ? 2.5 : 1.5,mine ? MG_GOLD : MG_RIM,mine ? 100 : 45);
   s.beginGradientFill("linear",[0x0A1440,0x1A2A6A],[95,95],[0,255],{matrixType:"box",x:SCB_SOCKET_X,y:3,w:SCB_SOCKET_W,h:SCB_ROW_H - 6,r:Math.PI / 2});
   mgRoundRect(s,SCB_SOCKET_X,3,SCB_SOCKET_W,SCB_ROW_H - 6,10);
   s.endFill();
   s.filters = mine ? [new flash.filters.GlowFilter(MG_GOLD,0.6,10,10,2,2)] : [];
}

// A colour scaled by f (above 1 lightens toward white).
function scbShade(c, f)
{
   var r = c >> 16 & 0xFF;
   var g = c >> 8 & 0xFF;
   var b = c & 0xFF;
   if(f > 1)
   {
      r += (255 - r) * (f - 1);
      g += (255 - g) * (f - 1);
      b += (255 - b) * (f - 1);
   }
   else
   {
      r *= f;
      g *= f;
      b *= f;
   }
   return Math.round(r) << 16 | Math.round(g) << 8 | Math.round(b);
}

// Lua: setTeamColors(team1, team2) colours the tint clips through setTeamColor; the chips follow.
var scbSetTeamColor = setTeamColor;
setTeamColor = function(mc, c)
{
   var result = scbSetTeamColor.apply(_root,arguments);
   var i = mc == tint0 ? 0 : (mc == tint1 ? 1 : -1);
   if(i >= 0 && !isNaN(Number(c)))
   {
      scbColors[i] = Number(c);
      scbDrawRow(i);
   }
   return result;
};

// Lua: setMyTeam(team) turns that team's score gold; its socket gets the gold rim.
var scbSetMyTeam = setMyTeam;
setMyTeam = function(team)
{
   var result = scbSetMyTeam.apply(_root,arguments);
   var i = Number(team);
   if(i == 0 || i == 1)
   {
      scbMine = i;
      scbDrawRow(0);
      scbDrawRow(1);
   }
   return result;
};

scbBuild();
// ===== End Sanctuary Soccer Scoreboard =====

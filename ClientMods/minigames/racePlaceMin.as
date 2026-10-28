// ===== Sanctuary Race Place =====
// Appended to racePlaceMin's main timeline (after minigames/mg_theme.as) by build_client_mods.py. Your place in a race
// (wndRacePlaceKeeper: the big "1st" and "/8") on a navy glass plate. The rim is gold, silver or bronze while you're on
// the podium and pale blue otherwise, and the plate pops when your place changes. The digits stay big: the place
// and its suffix in light gold, the field size in soft blue, all with the dark outline.

var RP_X = 14;
var RP_Y = 6;
var RP_W = 118;
var RP_H = 92;
var RP_MEDALS = [[MG_GOLD,0xFFF3C4],[0xDCE7FF,0xFFFFFF],[0xE09A5A,0xFFD9B0]];

var rpPlate;
var rpPlace = 0;

function rpBuild()
{
   rpPlate = mgUnder(_root,"sanctuaryRacePlace");
   rpPlate._x = RP_X + RP_W / 2;
   rpPlate._y = RP_Y + RP_H / 2;
   rpPlate.bg = rpPlate.createEmptyMovieClip("bg",1);
   rpPlate.pop = 0;
   rpPlate.onEnterFrame = rpTick;
   mgLight(placeKeeper.Place,MG_GOLD_LIGHT);
   mgLight(placeKeeper.PlaceSuffix,MG_GOLD_LIGHT);
   mgLight(placeKeeper.TotalPlayers,MG_SOFT);
   rpDraw();
}

function rpDraw()
{
   var g = rpPlate.bg;
   var medal = rpPlace >= 1 && rpPlace <= 3 ? RP_MEDALS[rpPlace - 1] : undefined;
   g.clear();
   mgGradientRect(g,- RP_W / 2,- RP_H / 2,RP_W,RP_H,30,0x2E4FB0,0x141F55,92,medal != undefined ? medal[0] : MG_RIM,medal != undefined ? 100 : 80,medal != undefined ? 3 : 2);
   mgGloss(g,- RP_W / 2 + 10,- RP_H / 2 + 3,RP_W - 20,RP_H * 0.4,16,30);
   if(medal != undefined)
   {
      // A thin inner line in the rim's light colour.
      g.lineStyle(1,medal[1],60);
      mgRoundRect(g,- RP_W / 2 + 4,- RP_H / 2 + 4,RP_W - 8,RP_H - 8,26);
   }
   rpPlate.medal = medal;
}

// The pop when the place changes, and a soft glow while you're on the podium.
function rpTick()
{
   if(this.pop > 0)
   {
      this.pop -= 0.08;
   }
   var k = this.pop > 0 ? Math.sin(this.pop * Math.PI) : 0;
   this._xscale = this._yscale = 100 + 10 * k;
   if(this.medal != undefined)
   {
      this.filters = [new flash.filters.GlowFilter(this.medal[0],0.45 + 0.4 * k,10 + 10 * k,10 + 10 * k,2,2),new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];
   }
   else
   {
      this.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];
   }
}

var rpSetPlace = setPlace;
setPlace = function(place)
{
   var result = rpSetPlace.apply(_root,arguments);
   var n = parseInt(String(place),10);
   if(!isNaN(n) && n != rpPlace)
   {
      rpPlace = n;
      rpDraw();
      rpPlate.pop = 1;
   }
   return result;
};

rpBuild();
// ===== End Sanctuary Race Place =====

// ===== Sanctuary Race Mini Map =====
// Appended to race_mini_map's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The track map in
// a race (wndRaceMiniMap) sits on a navy glass card with a gold rim, sized to the track Lua picks (initMap), so the
// pale track reads against any scenery. Your marker gets a gold ring with a ripple that keeps going out from it,
// so you find yourself on the map at once; the other racers keep their stock dots. The movie has no font.

var RM_PAD = 4;

var rmCard;

function rmBuild()
{
   rmCard = mgUnder(_root,"sanctuaryMiniMap");
   rmCard._visible = false;
}

function rmDrawCard()
{
   if(map == undefined)
   {
      rmCard._visible = false;
      return;
   }
   var x = Math.max(1,map._x - RM_PAD);
   var y = Math.max(1,map._y - RM_PAD);
   var w = Math.min(239 - x,map._width + RM_PAD * 2);
   var h = Math.min(239 - y,map._height + RM_PAD * 2);
   rmCard.clear();
   mgGradientRect(rmCard,x,y,w,h,22,0x2E4FB0,0x141F55,88,MG_GOLD,100,2.5);
   mgGloss(rmCard,x + 10,y + 3,w - 20,Math.min(50,h * 0.3),16,22);
   rmCard.lineStyle(1,MG_RIM,35);
   mgRoundRect(rmCard,x + 5,y + 5,w - 10,h - 10,18);
   rmCard.filters = [new flash.filters.DropShadowFilter(4,90,MG_SHADOW,0.6,10,10,1,2)];
   rmCard._visible = true;
}

// Your marker: a gold ring and a ripple, drawn in the marker so they follow it.
function rmMarkYou(dot)
{
   if(dot == undefined || dot.sanctuaryRing != undefined)
   {
      return;
   }
   var ring = dot.createEmptyMovieClip("sanctuaryRing",dot.getNextHighestDepth());
   ring.lineStyle(2.5,MG_GOLD_LIGHT,100);
   mgCircle(ring,0,0,10);
   ring.filters = [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,3,2)];
   var ripple = dot.createEmptyMovieClip("sanctuaryRipple",dot.getNextHighestDepth());
   ripple.lineStyle(2,MG_GOLD,100);
   mgCircle(ripple,0,0,10);
   ripple.t = 0;
   ripple.onEnterFrame = function()
   {
      this.t = (this.t + 0.035) % 1;
      this._xscale = this._yscale = 100 + 120 * this.t;
      this._alpha = 90 * (1 - this.t);
   };
}

var rmInitMap = initMap;
initMap = function(mapId)
{
   var result = rmInitMap.apply(_root,arguments);
   rmDrawCard();
   return result;
};

var rmUpdatePlayerPosition = updatePlayerPosition;
updatePlayerPosition = function(guid, place, x, y)
{
   var result = rmUpdatePlayerPosition.apply(_root,arguments);
   rmMarkYou(track["racer" + guid]);
   return result;
};

rmBuild();
// ===== End Sanctuary Race Mini Map =====

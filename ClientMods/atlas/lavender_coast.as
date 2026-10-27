// ===== Sanctuary: Lavender Coast's atlas page =====
// Appended to atlas's main timeline by build_client_mods.py.
// Lavender Coast is its own world zone (lavendercoast) south of Merry Vale, below the bottom edge of the Sacred Grove
// painting. Its page is a picture of the zone (atlas/lavendercoast.png, embedded as sanctuaryLavenderCoastPage), placed
// where the stock pages place theirs, and markers use mapOffsets.lavendercoast, so the warpstone, friends and the
// "You are here" flag land on it.
//  - The Sacred Grove page's "Lavender Coast" label, hidden in the stock movie, opens the page.
//  - Opening the atlas while south of the Sacred Grove painting opens the page straight away.
//  - On the page, the Sacred Grove button goes back without asking Lua (the atlas Lua opened is still Sacred Grove).

mapOffsets.lavendercoast = {zoomScaleX:0.413,zoomScaleY:0.413,mapOffsetX:552.4,mapOffsetY:-862};

// Ground south of this (world x) is past the Sacred Grove painting's bottom edge.
var LAVENDER_COAST_SOUTH = -2940;
var lavenderCoastAuto = false;
var lavenderCoastListening = false;

function showLavenderCoast()
{
   var page = base.attachMovie("sanctuaryLavenderCoastPage","lavendercoast",1);
   page._x = -144;
   page._y = -36;
   page.useHandCursor = false;
   page.onPress = function()
   {
      _root.sendClickedXY(_root._xmouse,_root._ymouse);
   };
   base.currentMovieClip = "lavendercoast";
   areaBeingViewed = "lavendercoast";
   updateAllMapIndicators();
   controls._x = 1026;
   controls._y = 820;
   sunstone_btn._visible = false;
   sunstonevalley_txt._visible = false;
   sacredgrove_btn._visible = true;
   sacredgrove_txt._visible = true;
   if(!lavenderCoastListening)
   {
      lavenderCoastListening = true;
      sacredgrove_btn.addListener(_root);
   }
   controls._visible = true;
}

// The Sacred Grove page's frame script hides its label on every attach, so a watcher shows it again once it has run.
function watchLavenderCoastLabel()
{
   if(_root.lavenderCoastWatch == undefined)
   {
      _root.createEmptyMovieClip("lavenderCoastWatch",9000);
   }
   _root.lavenderCoastWatch.onEnterFrame = function()
   {
      var label = _root.base.sacredgrove.lavendercoast;
      if(label == undefined || label.lavenderCoastLinked)
      {
         return;
      }
      label.lavenderCoastLinked = true;
      label._visible = true;
      label.onRollOver = function()
      {
         this._xscale = this._yscale = 110;
      };
      label.onRollOut = label.onDragOut = function()
      {
         this._xscale = this._yscale = 100;
      };
      label.onRelease = function()
      {
         this._xscale = this._yscale = 100;
         _root.showLavenderCoast();
      };
   };
}

// Opens the page once per atlas opening if the player is south of the Sacred Grove painting, whichever of the map
// and the player's position Lua sends first.
function autoShowLavenderCoast()
{
   var player = indicators["URHere"];
   if(lavenderCoastAuto && areaBeingViewed == "sacredgrove" && player != undefined && player.gameX < LAVENDER_COAST_SOUTH)
   {
      lavenderCoastAuto = false;
      showLavenderCoast();
   }
}

var stockSetMap = setMap_lua;
setMap_lua = function(str)
{
   stockSetMap.call(_root,str);
   lavenderCoastAuto = atlasName == "sacredgrove";
   watchLavenderCoastLabel();
   autoShowLavenderCoast();
};

var stockSetPlayerPosition = SetPlayerPosition;
SetPlayerPosition = function(gameX, gameZ)
{
   stockSetPlayerPosition.call(_root,gameX,gameZ);
   autoShowLavenderCoast();
};

var stockOnClick = onClick;
onClick = function(btn)
{
   if(btn == sacredgrove_btn && areaBeingViewed == "lavendercoast")
   {
      lavenderCoastAuto = false;
      base.attachMovie("sacredgrove","sacredgrove",1);
      base.currentMovieClip = "sacredgrove";
      areaBeingViewed = "sacredgrove";
      setControlLocation();
      updateAllMapIndicators();
      return;
   }
   stockOnClick.call(_root,btn);
};

watchLavenderCoastLabel();

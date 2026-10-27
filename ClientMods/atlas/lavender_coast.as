// ===== Sanctuary: Lavender Coast's atlas page =====
// Appended to atlas's main timeline by build_client_mods.py.
// Lavender Coast is its own world zone (lavendercoast) south of Merry Vale, below the bottom edge of the Sacred Grove
// painting. Its page is a picture of the zone (atlas/lavendercoast.png, embedded as sanctuaryLavenderCoastPage), placed
// where the stock pages place theirs, and markers use mapOffsets.lavendercoast, so the warpstone, friends and the
// "You are here" flag land on it.
//  - The Sacred Grove page's "Lavender Coast" label, hidden in the stock movie, opens the page.
//  - Opening the atlas anywhere on the coast opens the page straight away.
//  - On the page, the Sacred Grove button goes back without asking Lua (the atlas Lua opened is still Sacred Grove).

mapOffsets.lavendercoast = {zoomScaleX:0.413,zoomScaleY:0.413,mapOffsetX:552.4,mapOffsetY:-862};

// Ground south of this (world x) is Lavender Coast: FabledRealms has nothing to walk on past its Pass at -2125.
var LAVENDER_COAST_SOUTH = -2450;
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

function openLavenderCoast()
{
   this._xscale = this._yscale = 100;
   _root.showLavenderCoast();
}
function growLavenderCoastLabel()
{
   this._xscale = this._yscale = 110;
}
function shrinkLavenderCoastLabel()
{
   this._xscale = this._yscale = 100;
}

// The Sacred Grove page's frame script hides its label and gives it the stock "coming soon" rollover, whenever it runs,
// and Lua's removeAllClipsFromStage() removes every clip made on _root. So the watcher lives in base (placed on the
// timeline, which that leaves alone) and puts the label back every frame.
function watchLavenderCoastLabel()
{
   if(base.lavenderCoastWatch == undefined)
   {
      base.createEmptyMovieClip("lavenderCoastWatch",9000);
   }
   base.lavenderCoastWatch.onEnterFrame = function()
   {
      var label = _root.base.sacredgrove.lavendercoast;
      if(label == undefined)
      {
         return;
      }
      label._visible = true;
      if(label.onRelease != _root.openLavenderCoast)
      {
         label.onRelease = _root.openLavenderCoast;
         label.onRollOver = _root.growLavenderCoastLabel;
         label.onRollOut = label.onDragOut = _root.shrinkLavenderCoastLabel;
      }
   };
}

// Opens the page once per atlas opening if the player is on the coast, whichever of the map and the player's
// position Lua sends first.
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
   watchLavenderCoastLabel();
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

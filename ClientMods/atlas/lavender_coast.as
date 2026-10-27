// ===== Sanctuary: Lavender Coast's atlas page =====
// Appended to atlas's main timeline by build_client_mods.py.
// Lavender Coast is its own world zone (lavendercoast) south of Merry Vale, below the bottom edge of the Sacred Grove
// painting. Its page is a picture of the zone (atlas/lavendercoast.png, embedded as sanctuaryLavenderCoastPage), placed
// where the stock pages place theirs, and markers use mapOffsets.lavendercoast, so the warpstone, friends and the
// "You are here" flag land on it.
//  - The Sacred Grove page's "Lavender Coast" label, hidden in the stock movie, opens the page.
//  - Opening the atlas anywhere on the coast opens the page straight away.
//  - On the page, a Sacred Grove button beside the close button goes back without asking Lua (the atlas Lua opened
//    is still Sacred Grove). It is the stock page's "Sacred Grove" label (its text, lettering and arrow) copied onto a
//    plate like the close button's, with the arrow pointing north, where Sacred Grove is.

mapOffsets.lavendercoast = {zoomScaleX:0.413,zoomScaleY:0.413,mapOffsetX:552.4,mapOffsetY:-862};

// Ground south of this (world x) is Lavender Coast: FabledRealms has nothing to walk on past its Pass at -2125.
var LAVENDER_COAST_SOUTH = -2450;
var lavenderCoastAuto = false;

// The Sacred Grove button: below the map indicators (200 and up), like the close button, whose box starts at x 927
// and is centred on y 50.
var SACRED_GROVE_BUTTON_DEPTH = 150;
var SACRED_GROVE_BUTTON_RIGHT = 915;
var SACRED_GROVE_BUTTON_MIDDLE = 50;

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
   sacredgrove_btn._visible = false;
   sacredgrove_txt._visible = false;
   showSacredGroveButton();
   controls._visible = true;
}

function showSacredGrove()
{
   lavenderCoastAuto = false;
   hideSacredGroveButton();
   base.attachMovie("sacredgrove","sacredgrove",1);
   base.currentMovieClip = "sacredgrove";
   areaBeingViewed = "sacredgrove";
   setControlLocation();
   updateAllMapIndicators();
}

function drawSacredGrovePlate(plate, left, top, right, bottom, over)
{
   var r = 12;
   plate.clear();
   plate.lineStyle(3,over ? 0xA8CCF4 : 0x6B93C4,over ? 90 : 70);
   plate.beginFill(0x324150,over ? 55 : 35);
   plate.moveTo(left + r,top);
   plate.lineTo(right - r,top);
   plate.curveTo(right,top,right,top + r);
   plate.lineTo(right,bottom - r);
   plate.curveTo(right,bottom,right - r,bottom);
   plate.lineTo(left + r,bottom);
   plate.curveTo(left,bottom,left,bottom - r);
   plate.lineTo(left,top + r);
   plate.curveTo(left,top,left + r,top);
   plate.endFill();
}

// Lua's removeAllClipsFromStage() removes clips made on _root, so this makes the button again when it's gone.
function showSacredGroveButton()
{
   var button = _root.lavenderCoastSacredGrove;
   if(button == undefined)
   {
      sacredgrove_txt.duplicateMovieClip("lavenderCoastSacredGrove",SACRED_GROVE_BUTTON_DEPTH);
      button = _root.lavenderCoastSacredGrove;
      frSetTextPreserveWidth(button.label,TOOLTIP_SacredGrove);
      var arrow;
      for(var name in button)
      {
         if(typeof(button[name]) == "movieclip")
         {
            arrow = button[name];
         }
      }
      // The label is centred in its field (about 96 wide in English), which spans the clip (the arrow is under it);
      // the arrow goes on its left, pointing up.
      var label = button.label;
      var box = button.getBounds(button);
      var middle = (box.xMin + box.xMax) / 2;
      var half = (label.textWidth > 0 ? label.textWidth : 96) / 2;
      var top = box.yMin + 2;
      var size = 24;
      var arrowLeft = middle - half - size - 6;
      arrow._rotation = 0;
      arrow._xscale = arrow._yscale = 100 * size / 28;
      arrow._x = arrowLeft;
      arrow._y = top + (label.textHeight - size) / 2;
      button.plateLeft = arrowLeft - 12;
      button.plateTop = top - 10;
      button.plateRight = middle + half + 14;
      button.plateBottom = top + label.textHeight + 10;
      button._x = SACRED_GROVE_BUTTON_RIGHT - button.plateRight;
      button._y = SACRED_GROVE_BUTTON_MIDDLE - (button.plateTop + button.plateBottom) / 2;

      // The plate sits under the label, which takes no clicks, so the whole plate is the button.
      var plate = _root.createEmptyMovieClip("lavenderCoastSacredGrovePlate",SACRED_GROVE_BUTTON_DEPTH - 1);
      plate._x = button._x;
      plate._y = button._y;
      drawSacredGrovePlate(plate,button.plateLeft,button.plateTop,button.plateRight,button.plateBottom,false);
      plate.onRelease = function()
      {
         _root.showSacredGrove();
      };
      plate.onRollOver = function()
      {
         var b = _root.lavenderCoastSacredGrove;
         _root.drawSacredGrovePlate(this,b.plateLeft,b.plateTop,b.plateRight,b.plateBottom,true);
      };
      plate.onRollOut = plate.onDragOut = function()
      {
         var b = _root.lavenderCoastSacredGrove;
         _root.drawSacredGrovePlate(this,b.plateLeft,b.plateTop,b.plateRight,b.plateBottom,false);
      };
   }
   button._visible = true;
   _root.lavenderCoastSacredGrovePlate._visible = true;
}

function hideSacredGroveButton()
{
   _root.lavenderCoastSacredGrove._visible = false;
   _root.lavenderCoastSacredGrovePlate._visible = false;
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
// timeline, which that leaves alone) and puts the label back every frame, and the Sacred Grove button on our page.
function watchLavenderCoastLabel()
{
   if(base.lavenderCoastWatch == undefined)
   {
      base.createEmptyMovieClip("lavenderCoastWatch",9000);
   }
   base.lavenderCoastWatch.onEnterFrame = function()
   {
      if(_root.areaBeingViewed == "lavendercoast")
      {
         _root.showSacredGroveButton();
      }
      else
      {
         _root.hideSacredGroveButton();
      }
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

watchLavenderCoastLabel();

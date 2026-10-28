// ===== Sanctuary Pirates Ship Selection =====
// Appended to pir_shipselection_bbe's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The
// Pirates ship and flag picker (wndPiratesShipSelection) in the Main Menu's style: its frame (a plain shape with the
// ship ornament, dropped with REMOVED_DEPTHS) becomes the navy glass frame with a gold title and the ship in a
// gold-ringed medallion over its top edge (sanctuaryPirShip, TEXTURE_SYMBOLS). The lists sit in glass recesses; the
// ship rows are glass cards and the flags sit in frosted sockets, lighter on hover and rimmed in gold when chosen
// (the stock highlights still mark the choice, invisibly). The close, arrow and Go buttons are gold. Every stock
// clip keeps its click.

var SHP_X = 22;
var SHP_Y = 98;
var SHP_W = 756;
var SHP_H = 578;
var SHP_MEDAL_X = 400;
var SHP_MEDAL_Y = 72;
var SHP_SHIP_W = 358;
var SHP_SHIP_H = 79;

var shpLayer;
var shpPlates = [];

function shpBuild()
{
   shpLayer = mgUnder(_root,"sanctuaryShipSelection");
   var bg = shpLayer.createEmptyMovieClip("bg",1);
   mgFrame(bg,SHP_X,SHP_Y,SHP_W,SHP_H,32,64,6262);

   var p = shpLayer.createEmptyMovieClip("panels",2);
   // The ship list, the chosen ship's portrait and the flags.
   mgPanel(p,60,176,445,294,20,40,25);
   mgPanel(p,523,176,221,294,20,40,25);
   mgPanel(p,60,483,682,124,20,40,25);
   // Name strips for the chosen ship and the flags.
   mgPanel(p,535,193,196,31,15,70,0);
   mgPanel(p,76,492,650,31,15,70,0);
   p.lineStyle(1.5,MG_GOLD,55);
   p.moveTo(552,223);
   p.lineTo(714,223);
   p.moveTo(110,522);
   p.lineTo(692,522);
   // A gold-rimmed recess behind the portrait.
   p.lineStyle(2,MG_GOLD,70);
   p.beginFill(MG_NAVY,50);
   mgRoundRect(p,544,284,180,168,16);
   p.endFill();

   // The ship medallion over the frame's top edge.
   var m = shpLayer.createEmptyMovieClip("medal",3);
   m._x = SHP_MEDAL_X;
   m._y = SHP_MEDAL_Y;
   m.lineStyle(3,MG_GOLD,100);
   m.beginGradientFill("radial",[0x3F66D6,0x162660],[100,100],[0,255],{matrixType:"box",x:-58,y:-62,w:116,h:116,r:0});
   mgCircle(m,0,0,44);
   m.endFill();
   m.lineStyle(1,MG_GOLD_RIM,70);
   mgCircle(m,0,0,39);
   mgSparkle(m,-26,-20,2,70);
   mgSparkle(m,28,14,1.5,60);
   var ship = m.attachMovie("sanctuaryPirShip","ship",1);
   ship._xscale = ship._yscale = 92;
   ship._x = -44;
   ship._y = -52;
   m.filters = [new flash.filters.GlowFilter(MG_GOLD,0.45,14,14,2,2),new flash.filters.DropShadowFilter(4,90,MG_SHADOW,0.6,10,10,1,2)];

   mgLight(windowText,MG_GOLD_LIGHT);
   mgLight(selectedShipText,0xFFFFFF);
   mgLight(decalText,0xFFFFFF);

   var i;
   for(i = 1; i <= 3; i++)
   {
      var slot = _root["ship_slot" + i];
      shpPlate(slot,slot.ship_slot_highlight,false);
      mgLight(slot.name_txt,MG_GOLD_LIGHT);
      mgLight(slot.sizeLabel_txt,MG_GOLD_LIGHT);
      mgLight(slot.cannonsLabel_txt,MG_GOLD_LIGHT);
      mgLight(slot.turningLabel_txt,MG_GOLD_LIGHT);
      mgLight(slot.cargoLabel_txt,MG_GOLD_LIGHT);
      mgLight(slot.size_txt,0xFFFFFF);
      mgLight(slot.cannons_txt,0xFFFFFF);
      mgLight(slot.turning_txt,0xFFFFFF);
      mgLight(slot.cargo_txt,0xFFFFFF);
   }
   for(i = 1; i <= 6; i++)
   {
      var decal = _root["decal_slot" + i];
      shpPlate(decal,decal.decal_highlight,true);
   }
   shpLayer.onEnterFrame = shpTick;

   // The buttons are plain Buttons, which have no hitTest; they report their hover for the skins.
   shpHoverShim(close_btn);
   shpHoverShim(action_button);
   shpHoverShim(left_decalArrow);
   shpHoverShim(right_decalArrow);
   mgSkin(close_btn,"close","",{x:-38.5,y:-36,w:77,h:72});
   mgSkin(action_button,"gold","Go!",{x:-38.5,y:-36,w:77,h:72});
   // The right arrow is the left one mirrored (_xscale -100), which its skin copies.
   mgSkin(left_decalArrow,"left","",{x:-2,y:0,w:32,h:33});
   mgSkin(right_decalArrow,"left","",{x:-2,y:0,w:32,h:33});
}

// A plate inside a ship row or flag slot, swapped into the stock highlight's depth: over the stock plate, under
// the texts and icon. The highlight goes on top, invisible; the stock code still shows and hides it.
function shpPlate(slot, highlight, flag)
{
   var p = slot.createEmptyMovieClip("sanctuaryPlate",slot.getNextHighestDepth());
   p.swapDepths(highlight);
   highlight._alpha = 0;
   p.slot = slot;
   p.highlight = highlight;
   p.flag = flag;
   p.state = -1;
   shpPlates.push(p);
   shpDraw(p,0);
}

function shpDraw(p, state)
{
   p.state = state;
   p.clear();
   var selected = state == 2;
   var over = state == 1;
   if(p.flag)
   {
      mgSocket(p,3,3,68,68,12,selected ? MG_GOLD : (over ? MG_GOLD_LIGHT : MG_RIM),selected || over ? 100 : 70,selected ? 3 : 2);
      mgGloss(p,8,5,58,22,9,40);
   }
   else
   {
      mgGradientRect(p,-1,-1,SHP_SHIP_W + 2,SHP_SHIP_H + 2,12,selected ? 0x4A6AD0 : (over ? 0x3F66D6 : 0x284A9E),selected ? 0x22337E : (over ? 0x1D307A : 0x162660),100,selected ? MG_GOLD : (over ? MG_GOLD_LIGHT : MG_RIM),selected || over ? 100 : 45,selected ? 3 : 2);
      mgGloss(p,6,1,SHP_SHIP_W - 12,24,10,selected ? 28 : 18);
   }
   p.filters = selected ? [new flash.filters.GlowFilter(MG_GOLD,0.6,12,12,2,2)] : [];
}

// Hover and selection, every frame (the stock code toggles the highlights itself).
function shpTick()
{
   var x = _root._xmouse;
   var y = _root._ymouse;
   var i;
   for(i = 0; i < shpPlates.length; i++)
   {
      var p = shpPlates[i];
      var state = p.highlight._visible ? 2 : (p.slot._visible && p.slot.hitTest(x,y,false) ? 1 : 0);
      if(state != p.state)
      {
         shpDraw(p,state);
      }
   }
}

function shpHoverShim(btn)
{
   btn.onRollOver = function()
   {
      this.mgHover = true;
   };
   btn.onRollOut = btn.onReleaseOutside = btn.onDragOut = function()
   {
      this.mgHover = false;
   };
   btn.hitTest = function()
   {
      return this.mgHover == true;
   };
}

shpBuild();
// ===== End Sanctuary Pirates Ship Selection =====

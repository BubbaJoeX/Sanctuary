// ===== Sanctuary Housing Options =====
// Appended to HousingOptions' main timeline by build_client_mods.py.
// Redraws the Housing Options window (the furniture tray's Housing Options button) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass frame with the title in gold and a gold close button, the
// six toggles (Lock, Snap to Grid, Free Camera, Auto Rotation, Hide Flora, Hide Pets) as glass rows in a recess with
// gold checkboxes, and the three actions (Housing Directory, Pack up All Items, Rename House) as glass cards with gold
// pills (Remove from the directory is a red one). The stock clips keep their roles (hit areas, Lua callbacks,
// tooltips): their art is hidden (alpha 0) and the new look is drawn under them.

var HO_W = 420;
var HO_H = 360;
// The toggles' recess and rows, and the action cards (each holds a stock label over a stock button).
var HO_LEFT = [20,66,178,264];
var HO_ROW_X = 28;
var HO_ROW_W = 162;
var HO_CARD_X = 206;
var HO_CARD_W = 190;
var HO_GOLD = 0xFFC531;
var HO_RIM = 0x9DB8FF;
var HO_SOFT = 0xC9D6FF;
var HO_INK = 0x3A2400;

// --- drawing helpers -------------------------------------------------------------------

function hoRoundRect(mc, x, y, w, h, r)
{
   mc.moveTo(x + r,y);
   mc.lineTo(x + w - r,y);
   mc.curveTo(x + w,y,x + w,y + r);
   mc.lineTo(x + w,y + h - r);
   mc.curveTo(x + w,y + h,x + w - r,y + h);
   mc.lineTo(x + r,y + h);
   mc.curveTo(x,y + h,x,y + h - r);
   mc.lineTo(x,y + r);
   mc.curveTo(x,y,x + r,y);
}

function hoGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
{
   if(rim != undefined)
   {
      mc.lineStyle(rimWidth,rim,rimAlpha);
   }
   else
   {
      mc.lineStyle();
   }
   mc.beginGradientFill("linear",[top,bottom],[alpha,alpha],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   hoRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function hoCircle(mc, x, y, r)
{
   var k = Math.tan(Math.PI / 8);
   var s = Math.sin(Math.PI / 4);
   mc.moveTo(x + r,y);
   mc.curveTo(x + r,y + k * r,x + s * r,y + s * r);
   mc.curveTo(x + k * r,y + r,x,y + r);
   mc.curveTo(x - k * r,y + r,x - s * r,y + s * r);
   mc.curveTo(x - r,y + k * r,x - r,y);
   mc.curveTo(x - r,y - k * r,x - s * r,y - s * r);
   mc.curveTo(x - k * r,y - r,x,y - r);
   mc.curveTo(x + k * r,y - r,x + s * r,y - s * r);
   mc.curveTo(x + r,y - k * r,x + r,y);
}

// A gold (or red) pill with a glassy shine across its top half.
function hoPill(mc, x, y, w, h, r, over, red)
{
   var colors;
   if(red)
   {
      colors = over ? [0xFF9C8A,0xD8402C] : [0xF2796A,0xB8301F];
   }
   else
   {
      colors = over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623];
   }
   mc.lineStyle(over ? 2 : 1.5,red ? 0xFFD6CC : 0xFFF3C4,over ? 100 : 90);
   mc.beginGradientFill("linear",colors,[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   hoRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h / 2,r:Math.PI / 2});
   hoRoundRect(mc,x + 3,y + 2,w - 6,h / 2,Math.min(r,h / 4));
   mc.endFill();
}

function hoSparkle(mc, x, y, r, alpha)
{
   mc.lineStyle();
   mc.beginFill(0xFFFFFF,alpha);
   mc.moveTo(x,y - r * 2);
   mc.curveTo(x,y,x + r * 2,y);
   mc.curveTo(x,y,x,y + r * 2);
   mc.curveTo(x,y,x - r * 2,y);
   mc.curveTo(x,y,x,y - r * 2);
   mc.endFill();
}

function hoSparkles(mc, seed, count, x, y, w, h)
{
   var s = seed;
   var i;
   for(i = 0; i < count; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + s % w;
      s = (s * 1103515245 + 12345) % 2147483648;
      hoSparkle(mc,sx,y + s % h,1 + s % 3,25 + s % 50);
   }
}

function hoOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function hoShadow(distance, blur, alpha)
{
   return [new flash.filters.DropShadowFilter(distance,90,0x050A24,alpha,blur,blur,1,2)];
}

function hoGlow(color, blur)
{
   return [new flash.filters.GlowFilter(color,0.8,blur,blur,2,2)];
}

function hoGoldRule(mc, x, y, w)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[HO_GOLD,HO_GOLD,HO_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:x,y:y,w:w,h:2,r:0});
   hoRoundRect(mc,x,y,w,2,1);
   mc.endFill();
}

// Hides a clip's unnamed children (stock art placed without an instance name), found by enumeration and, in case
// that misses them, at depth 1 (where the buttons' plates sit); alpha 0 keeps their hit areas.
function hoHideUnnamed(mc)
{
   var name;
   for(name in mc)
   {
      if(typeof mc[name] == "movieclip" && mc[name]._parent == mc && name.substr(0,8) == "instance")
      {
         mc[name]._alpha = 0;
      }
   }
   var plate = mc.getInstanceAtDepth(1 - 16384);
   if(plate != undefined && plate._name.substr(0,8) == "instance")
   {
      plate._alpha = 0;
   }
}

// --- FRIconButton skins ------------------------------------------------------------------
// A skinned button has hoDraw(state); its states call it after the stock code has swapped the art.

var hoStateUp = FRIconButton.prototype.setStateUp;
FRIconButton.prototype.setStateUp = function()
{
   hoStateUp.apply(this,arguments);
   if(this.hoDraw != undefined)
   {
      this.hoDraw("up");
   }
};

var hoStateOver = FRIconButton.prototype.setStateOver;
FRIconButton.prototype.setStateOver = function()
{
   hoStateOver.apply(this,arguments);
   if(this.hoDraw != undefined && this.m_isEnabled)
   {
      this.hoDraw("over");
   }
};

var hoStateDown = FRIconButton.prototype.setStateDown;
FRIconButton.prototype.setStateDown = function()
{
   hoStateDown.apply(this,arguments);
   if(this.hoDraw != undefined && this.m_isEnabled)
   {
      this.hoDraw("down");
   }
};

// The action buttons: their plates (unnamed clips) are hidden and a pill is drawn under their labels.
function hoDrawPill(state)
{
   var s = this.hoSkinMc;
   s.clear();
   var over = state == "over" || state == "down";
   hoPill(s,0,0,99.5,37,18.5,over,this.hoRed);
   s._y = state == "down" ? 1 : 0;
   this.filters = over ? hoGlow(this.hoRed ? 0xFF7A5C : HO_GOLD,14) : hoShadow(3,8,0.55);
}

function hoSkinPill(btn, red)
{
   var states = [btn.up_mc,btn.over_mc,btn.down_mc];
   var i;
   for(i = 0; i < states.length; i++)
   {
      hoHideUnnamed(states[i]);
      var t = states[i].textField_txt;
      t.textColor = red ? 0xFFFFFF : HO_INK;
      t.filters = red ? [new flash.filters.GlowFilter(0x5A0E06,0.8,3,3,3,2)] : [];
   }
   btn.hoRed = red;
   btn.hoSkinMc = btn.createEmptyMovieClip("hoSkinMc",-16384);
   btn.hoDraw = hoDrawPill;
   btn.hoDraw("up");
}

// --- frame -----------------------------------------------------------------------------

var hoFrame;

function hoDrawFrame()
{
   var bg = hoFrame.bg;
   bg.clear();
   hoGradientRect(bg,6,6,HO_W - 12,HO_H - 12,30,0x2E4FB0,0x141F55,100,HO_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-160,y:-220,w:520,h:460,r:0});
   hoRoundRect(bg,8,8,HO_W - 16,HO_H - 16,28);
   bg.endFill();
   // The header band, with a few sparkles and a gold rule under the title.
   bg.beginFill(0x0A1440,30);
   hoRoundRect(bg,10,10,HO_W - 20,46,26);
   bg.endFill();
   hoSparkles(bg,2727,16,40,14,300,36);
   hoGoldRule(bg,HO_W / 2 - 90,56,180);

   // The toggles' recess and their rows.
   hoGradientRect(bg,HO_LEFT[0],HO_LEFT[1],HO_LEFT[2],HO_LEFT[3],20,0x0A1440,0x0A1440,40,HO_RIM,25,1);
   var radios = hoRadios();
   var i;
   for(i = 0; i < radios.length; i++)
   {
      hoGradientRect(bg,HO_ROW_X,radios[i]._y - 7,HO_ROW_W,34,17,0x284A9E,0x162660,100,HO_RIM,40,1);
   }

   // The action cards: each runs from its label to under its button.
   var labels = [textField_1_txt,textField_2_txt,textField_3_txt];
   var buttons = [greenButton1_mc,greenButton2_mc,greenButton3_mc];
   for(i = 0; i < labels.length; i++)
   {
      var top = labels[i]._y - 6;
      var bottom = buttons[i]._y + buttons[i]._height + 5;
      hoGradientRect(bg,HO_CARD_X,top,HO_CARD_W,bottom - top,18,0x284A9E,0x162660,100,HO_RIM,45,1.5);
   }
}

// The gold round close button, centered on the stock one (an invisible hit area).
function hoDrawClose(state)
{
   var c = hoFrame.close;
   c.clear();
   var over = state == "over" || state == "down";
   var r = 17;
   c.lineStyle(over ? 2 : 1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   hoCircle(c,0,0,r);
   c.endFill();
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r,r:Math.PI / 2});
   hoRoundRect(c,-11,-15,22,12,6);
   c.endFill();
   c.lineStyle(4,HO_INK,100);
   c.moveTo(-6,-6);
   c.lineTo(6,6);
   c.moveTo(6,-6);
   c.lineTo(-6,6);
   c.filters = over ? hoGlow(HO_GOLD,14) : hoShadow(3,8,0.5);
   gs.TweenLite.to(c,0.3,{_xscale:over ? 112 : 100,_yscale:over ? 112 : 100,ease:mx.transitions.easing.Strong.easeOut});
}

function hoRadios()
{
   return [lockRadio_mc,gridRadio_mc,freeCamRadio_mc,toggleAutoOrient_mc,hideFloraRadio_mc,hidePetsRadio_mc];
}

function hoBuildFrame()
{
   hoFrame = _root.createEmptyMovieClip("hoFrame",-16384);
   hoFrame.bg = hoFrame.createEmptyMovieClip("bg",1);
   hoFrame.bg.filters = hoShadow(6,18,0.6);
   hoFrame.close = hoFrame.createEmptyMovieClip("close",2);

   // The stock window art stays as an invisible hit area.
   messageWindow._alpha = 0;

   // The title: the stock field (set from the string table), centered in gold.
   var f = title_txt.getTextFormat();
   f.size = 26;
   f.color = 0xFFE08A;
   f.align = "center";
   title_txt.setTextFormat(f);
   title_txt.setNewTextFormat(f);
   title_txt._y = 14;
   title_txt.filters = hoOutline();

   var labels = [textField_1_txt,textField_2_txt,textField_3_txt];
   var i;
   for(i = 0; i < labels.length; i++)
   {
      var lf = labels[i].getTextFormat();
      lf.color = 0xFFFFFF;
      lf.align = "center";
      labels[i].setTextFormat(lf);
      labels[i].setNewTextFormat(lf);
      labels[i].filters = hoOutline();
   }

   // The close button: the stock one is the hit area, the gold circle sits on its center.
   exit_mc.up_mc._alpha = exit_mc.over_mc._alpha = exit_mc.down_mc._alpha = 0;
   var b = exit_mc.getBounds(_root);
   hoFrame.close._x = Math.round((b.xMin + b.xMax) / 2) - 2;
   hoFrame.close._y = Math.round((b.yMin + b.yMax) / 2) + 2;
   exit_mc.hoDraw = hoDrawClose;
   hoDrawClose("up");

   hoDrawFrame();
}

// --- toggles ---------------------------------------------------------------------------

// A navy box that fills gold with a dark check when on, drawn over the stock dot.
function hoDrawRadio(rad)
{
   var b = rad.hoBox;
   b.clear();
   var on = rad.fill_mc._visible;
   if(on)
   {
      hoPill(b,0,-1,21,21,6,false,false);
      b.lineStyle(3,HO_INK,100);
      b.moveTo(5,9);
      b.lineTo(9,14);
      b.lineTo(16,4);
   }
   else
   {
      hoGradientRect(b,0,-1,21,21,6,0x0A1440,0x1D307A,100,HO_RIM,70,1.5);
   }
   b.filters = on ? hoGlow(HO_GOLD,8) : [];
}

function hoSkinRadio(rad)
{
   rad.fill_mc._alpha = 0;
   rad.hoBox = rad.createEmptyMovieClip("hoBox",rad.getNextHighestDepth());
   rad.txt.textColor = 0xFFFFFF;
   rad.txt.filters = hoOutline();
   hoDrawRadio(rad);
}

var hoRadioSetFill = FRRadioButton.prototype.setFill;
FRRadioButton.prototype.setFill = function(f)
{
   hoRadioSetFill.apply(this,arguments);
   if(this.hoBox != undefined)
   {
      hoDrawRadio(this);
   }
};

// setText re-sets the label (frSetTextPreserveWidth), so it's recolored after.
var hoRadioSetText = FRRadioButton.prototype.setText;
FRRadioButton.prototype.setText = function(str)
{
   hoRadioSetText.apply(this,arguments);
   if(this.hoBox != undefined)
   {
      this.txt.textColor = 0xFFFFFF;
   }
};

// --- Lua -------------------------------------------------------------------------------

// Rename's label changes with the house; setText keeps the dark ink format.
var hoSetRenameBtnText = SetRenameBtnText;
SetRenameBtnText = function(s)
{
   hoSetRenameBtnText.apply(_root,arguments);
   greenButton3_mc.up_mc.textField_txt.textColor = greenButton3_mc.over_mc.textField_txt.textColor = greenButton3_mc.down_mc.textField_txt.textColor = HO_INK;
};

// --- build -----------------------------------------------------------------------------

function hoBuild()
{
   hoBuildFrame();
   hoSkinPill(greenButton1_mc,false);
   hoSkinPill(greenButton2_mc,false);
   hoSkinPill(greenButton3_mc,false);
   hoSkinPill(deleteButton_mc,true);
   var radios = hoRadios();
   var i;
   for(i = 0; i < radios.length; i++)
   {
      hoSkinRadio(radios[i]);
   }
}

hoBuild();
// ===== End Sanctuary Housing Options =====

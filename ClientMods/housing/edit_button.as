// ===== Sanctuary Decorate button =====
// Appended to housingEditButton's main timeline by build_client_mods.py.
// The button shown in your house (Decorate, then the planning and exit-tool modes) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a gold pill with its label in dark ink, lighter on hover, in place of the
// stock blue bar. The window is only 300x50 and clips the movie, so the pill keeps a small shadow and lights up
// without a glow. The stock button keeps its role (hit area, tooltip, Lua's labels and icons): its plates are
// hidden (alpha 0) and the pill is drawn under its label.

var HB_W = 300;
var HB_H = 50;
var HB_INK = 0x3A2400;

function hbRoundRect(mc, x, y, w, h, r)
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

// The pill: gold with a glassy shine across its top half, a white rim and two sparkles; lighter while hovered.
function hbDraw(state)
{
   var s = button.hbSkin;
   s.clear();
   var over = state == "over" || state == "down";
   var x = 3;
   var y = 2;
   var w = HB_W - 6;
   var h = 41;
   s.lineStyle(2,0xFFF3C4,100);
   s.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   hbRoundRect(s,x,y,w,h,h / 2);
   s.endFill();
   s.lineStyle();
   s.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[over ? 70 : 55,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h / 2,r:Math.PI / 2});
   hbRoundRect(s,x + 8,y + 2,w - 16,h / 2 - 1,10);
   s.endFill();
   // A warm glow from the middle when hovered (a glow filter would be clipped by the small window).
   if(over)
   {
      s.beginGradientFill("radial",[0xFFFFFF,0xFFFFFF],[45,0],[0,255],{matrixType:"box",x:x + w / 2 - 90,y:y - 20,w:180,h:80,r:0});
      hbRoundRect(s,x + 2,y + 2,w - 4,h - 4,h / 2 - 2);
      s.endFill();
   }
   s._y = state == "down" ? 1 : 0;
   s.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.55,5,5,1,2)];
}

// The labels in dark ink (Lua's mode changes re-set them with the stock white format).
function hbColorLabels()
{
   var states = [button.up_mc,button.over_mc,button.down_mc];
   var i;
   for(i = 0; i < states.length; i++)
   {
      var t = states[i].textField_txt;
      t.textColor = HB_INK;
      var f = t.getNewTextFormat();
      f.color = HB_INK;
      t.setNewTextFormat(f);
      t.filters = [new flash.filters.GlowFilter(0xFFF3C4,0.6,2,2,2,2)];
   }
}

var hbStateUp = FRIconButton.prototype.setStateUp;
FRIconButton.prototype.setStateUp = function()
{
   hbStateUp.apply(this,arguments);
   if(this.hbSkin != undefined)
   {
      hbDraw("up");
   }
};

var hbStateOver = FRIconButton.prototype.setStateOver;
FRIconButton.prototype.setStateOver = function()
{
   hbStateOver.apply(this,arguments);
   if(this.hbSkin != undefined && this.m_isEnabled)
   {
      hbDraw("over");
   }
};

var hbStateDown = FRIconButton.prototype.setStateDown;
FRIconButton.prototype.setStateDown = function()
{
   hbStateDown.apply(this,arguments);
   if(this.hbSkin != undefined && this.m_isEnabled)
   {
      hbDraw("down");
   }
};

var hbSetToPlanningMode = setToPlanningMode_lua;
setToPlanningMode_lua = function()
{
   hbSetToPlanningMode.apply(_root,arguments);
   hbColorLabels();
};

var hbSetToExitToolMode = setToExitToolMode_lua;
setToExitToolMode_lua = function(iconId)
{
   hbSetToExitToolMode.apply(_root,arguments);
   hbColorLabels();
};

var hbSetToHousingMode = setToHousingMode_lua;
setToHousingMode_lua = function()
{
   hbSetToHousingMode.apply(_root,arguments);
   hbColorLabels();
};

function hbBuild()
{
   // Each state's plate is a clip named button; the label stays.
   button.up_mc.button._alpha = button.over_mc.button._alpha = button.down_mc.button._alpha = 0;
   button.hbSkin = button.createEmptyMovieClip("hbSkin",-16384);
   hbColorLabels();
   hbDraw("up");
}

hbBuild();
// ===== End Sanctuary Decorate button =====

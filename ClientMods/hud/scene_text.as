// ===== Sanctuary Scene Text =====
// Appended to scenetext's main timeline by build_client_mods.py.
// The text that floats in the world (wndSceneText): name plates, loot and star pop-ups, effect callouts, damage
// numbers, overhead chat and the edge-of-screen quest arrows, in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): light text with a dark navy outline in place of the stock grey drop
// shadow, loot and star icons in frosted sockets with a gold rim, the dark red effect callouts lifted to a light
// coral, and the quest arrows' labels outlined with "Active Quest!" in gold. Overhead chat keeps the player's own
// bubble and text colours (dark text on a light bubble gets no outline). Every text goes through
// scenetext.AnyText's SetTextField or PrepareTextField, which are wrapped on the class prototype;
// ShowDirectionalIndicator is wrapped to style each arrow's label.

var ST_GOLD_LIGHT = 0xFFE08A;
var ST_SOFT = 0xC9D6FF;
// Text darker than this (0..255 luma) is left as it is (it sits on a light chat bubble).
var ST_MIN_LUMA = 110;
// The stock effect callout colour (dark red, unreadable over the world) and what it becomes.
var ST_EFFECT_COLOR = 11141120;
var ST_EFFECT_LIGHT = 0xFF7A6B;

function stLuma(c)
{
   return 0.299 * (c >> 16 & 0xFF) + 0.587 * (c >> 8 & 0xFF) + 0.114 * (c & 0xFF);
}

function stOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.95,3,3,5,2)];
}

function stRoundRect(mc, x, y, w, h, r)
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

// A frosted socket with a gold rim under a pop-up's icon (inside the icon clip, at its holder's bounds).
function stSocket(icon)
{
   if(icon == undefined || icon.sanctuarySocket != undefined)
   {
      return;
   }
   var b = icon.iconHolder.getBounds(icon);
   var w = b.xMax - b.xMin;
   var h = b.yMax - b.yMin;
   if(w < 4 || h < 4)
   {
      return;
   }
   var scale = Math.max(Math.abs(icon._xscale) / 100,0.05);
   var sock = icon.createEmptyMovieClip("sanctuarySocket",-16384);
   icon.sanctuarySocket = sock;
   sock.lineStyle(2 / scale,ST_GOLD_LIGHT,100);
   sock.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:b.xMin - w * 0.15,y:b.yMin - h * 0.25,w:w * 1.3,h:h * 1.4,r:0});
   stRoundRect(sock,b.xMin - 2,b.yMin - 2,w + 4,h + 4,Math.min(w,h) * 0.3);
   sock.endFill();
}

// Name plates and overhead chat: an outline instead of the grey drop shadow, for light text.
var stSetTextField = scenetext.AnyText.prototype.SetTextField;
scenetext.AnyText.prototype.SetTextField = function(s, fontSize, color, shadowed)
{
   var result = stSetTextField.apply(this,arguments);
   var main = this.theMovieClip.theTextField;
   if(main != undefined && stLuma(Number(color)) >= ST_MIN_LUMA)
   {
      main.filters = stOutline();
      this.theMovieClip.theTextFieldShadow._visible = false;
   }
   return result;
};

// The pop-ups (loot, stars, effects, damage numbers): outlined, with the effect red lifted and icons in sockets.
var stPrepareTextField = scenetext.AnyText.prototype.PrepareTextField;
scenetext.AnyText.prototype.PrepareTextField = function(t, s, fontSize, color)
{
   if(Number(color) == ST_EFFECT_COLOR)
   {
      arguments[3] = ST_EFFECT_LIGHT;
   }
   var result = stPrepareTextField.apply(this,arguments);
   if(t._name != "theTextField" && t._name != "theTextFieldShadow")
   {
      t.filters = stOutline();
      stSocket(t._parent.Icon);
   }
   return result;
};

// The quest arrows at the screen edge: outlined labels, the distance in soft blue, "Active Quest!" in gold.
var stShowDirectionalIndicator = ShowDirectionalIndicator;
ShowDirectionalIndicator = function(guid)
{
   var result = stShowDirectionalIndicator.apply(_root,arguments);
   var label = _root["a" + guid].label;
   if(label != undefined && !label.sanctuaryStyled)
   {
      label.sanctuaryStyled = true;
      label.name.textColor = 0xFFFFFF;
      label.distance.textColor = ST_SOFT;
      label.selectedInd.textColor = ST_GOLD_LIGHT;
      label.name.filters = label.distance.filters = label.selectedInd.filters = stOutline();
   }
   return result;
};
// ===== End Sanctuary Scene Text =====

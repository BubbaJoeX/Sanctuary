// ===== Sanctuary Notifications =====
// Appended to notifications' main timeline by build_client_mods.py.
// The big centre-screen notifications (wndNotification: objective complete/failed, awards, ability and job level
// ups, "Now entering" under the region badges, pet trick calls) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): the message flies in on a navy glass ribbon with a gold rim and gold
// sparkle stars at its ends, in outlined text. Lua's colour for the message is kept (a colour too dark to read on
// the ribbon becomes gold). notifyMessage and notifyPetMessage are wrapped (every notification goes through them,
// so the ribbon rides the stock fly-in and zoom-out tweens); the badges, rays, icons and their timing are untouched.
// playZoneAnimation is not changed: Sanctuary's region badges (REGION_BADGES, lua/client_extensions.lua) still load
// into it the same way.

var NT_RIM = 0x9DB8FF;
var NT_GOLD = 0xFFC531;
var NT_GOLD_LIGHT = 0xFFE08A;
// Message colours darker than this (0..255 luma) are shown in gold.
var NT_MIN_LUMA = 110;

// --- drawing helpers -------------------------------------------------------------------

function ntRoundRect(mc, x, y, w, h, r)
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

function ntSparkle(mc, x, y, r, color, alpha)
{
   mc.lineStyle();
   mc.beginFill(color,alpha);
   mc.moveTo(x,y - r * 2);
   mc.curveTo(x,y,x + r * 2,y);
   mc.curveTo(x,y,x,y + r * 2);
   mc.curveTo(x,y,x - r * 2,y);
   mc.curveTo(x,y,x,y - r * 2);
   mc.endFill();
}

function ntOutline(size)
{
   return [new flash.filters.GlowFilter(0x0A1030,0.95,size,size,5,2)];
}

function ntLightColor(color)
{
   var c = Number(color);
   if(isNaN(c))
   {
      return NT_GOLD_LIGHT;
   }
   var luma = 0.299 * (c >> 16 & 0xFF) + 0.587 * (c >> 8 & 0xFF) + 0.114 * (c & 0xFF);
   return luma < NT_MIN_LUMA ? NT_GOLD_LIGHT : c;
}

// Lua's colour on a message field (the stock code tints it with a Color, which dims an outline with it, so the
// tint is cleared and the colour set as the text's own).
function ntColorText(t, color)
{
   if(t == undefined)
   {
      return;
   }
   new Color(t).setTransform({ra:100,rb:0,ga:100,gb:0,ba:100,bb:0,aa:100,ab:0});
   t.textColor = ntLightColor(color);
}

// --- the ribbon ------------------------------------------------------------------------

// A navy glass ribbon behind a notifier's message (one or two lines), drawn under its text fields.
function ntRibbon(mc, twoLines)
{
   var t = mc.messageText1;
   var tw = t.textWidth;
   if(twoLines)
   {
      tw = Math.max(tw,mc.messageText2nd1.textWidth);
   }
   if(tw < 2)
   {
      return;
   }
   var cx = t._x + t._width / 2;
   var w = tw + 96;
   var x = cx - w / 2;
   var y = t._y - 6;
   var h = twoLines ? 92 : 50;
   var r = 25;
   var rib = mc.createEmptyMovieClip("sanctuaryRibbon",-16384);
   rib.lineStyle(2.5,NT_GOLD_LIGHT,90);
   rib.beginGradientFill("linear",[0x2E4FB0,0x141F55],[94,94],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   ntRoundRect(rib,x,y,w,h,r);
   rib.endFill();
   rib.lineStyle();
   rib.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:x - w * 0.1,y:y - h,w:w * 0.7,h:h * 2.4,r:0});
   ntRoundRect(rib,x + 3,y + 3,w - 6,h - 6,r - 3);
   rib.endFill();
   rib.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[22,0],[0,255],{matrixType:"box",x:x,y:y + 3,w:w,h:18,r:Math.PI / 2});
   ntRoundRect(rib,x + 16,y + 3,w - 32,18,9);
   rib.endFill();
   // Gold stars at the ends and a few white sparkles along it.
   ntSparkle(rib,x + 4,y + h / 2,5,NT_GOLD_LIGHT,100);
   ntSparkle(rib,x + w - 4,y + h / 2,5,NT_GOLD_LIGHT,100);
   ntSparkle(rib,x + 16,y + 6,2.5,NT_GOLD_LIGHT,90);
   ntSparkle(rib,x + w - 16,y + h - 6,2.5,NT_GOLD_LIGHT,90);
   var s = 2468;
   var i;
   for(i = 0; i < 6; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + 40 + s % Math.round(w - 80);
      s = (s * 1103515245 + 12345) % 2147483648;
      ntSparkle(rib,sx,s % 2 == 0 ? y + 6 : y + h - 6,1 + s % 2,0xFFFFFF,25 + s % 35);
   }
   rib.filters = [new flash.filters.GlowFilter(NT_GOLD,0.55,18,18,2,2),new flash.filters.DropShadowFilter(4,90,0x050A24,0.5,10,10,1,2)];
}

// --- wrapped stock calls ---------------------------------------------------------------

var ntNotifyMessage = notifyMessage;
notifyMessage = function(myMessage, startDuration, endDelay, myTextColor, myMessage2ndLine)
{
   var result = ntNotifyMessage.apply(_root,arguments);
   var mc = notifierMC;
   var twoLines = myMessage2ndLine != undefined && myMessage2ndLine != "";
   // The stock drop shadow is replaced by an outline.
   mc.messageText2._visible = false;
   mc.messageText2nd2._visible = false;
   ntColorText(mc.messageText1,myTextColor);
   mc.messageText1.filters = ntOutline(4);
   if(twoLines)
   {
      ntColorText(mc.messageText2nd1,myTextColor);
      mc.messageText2nd1.filters = ntOutline(4);
   }
   ntRibbon(mc,twoLines);
   return result;
};

// Pet trick calls ("Get Ready!"): big outlined text, no ribbon (it scales up over the pet).
var ntNotifyPetMessage = notifyPetMessage;
notifyPetMessage = function(myMessage, startDuration, endDelay, myTextColor)
{
   var result = ntNotifyPetMessage.apply(_root,arguments);
   notifierMC.messageText2._visible = false;
   ntColorText(notifierMC.messageText1,myTextColor);
   notifierMC.messageText1.filters = ntOutline(6);
   return result;
};
// ===== End Sanctuary Notifications =====

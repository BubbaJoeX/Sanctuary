// ===== Sanctuary Small Notifications =====
// Appended to notificationSm's main timeline by build_client_mods.py.
// The stacked centre-screen lines (wndNotificationSm: "You got...", reward and status lines that rise and fade) in the
// Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): each line in outlined text with its icon in a small
// frosted socket with a gold rim. Lua's colour is kept unless it's too dark to read over the world (then gold).
// The lines stack by their text height, so they get no backing plates. notifyMessageSm is wrapped to style each new
// line after the stock code has laid it out.

var NS_GOLD_LIGHT = 0xFFE08A;
var NS_MIN_LUMA = 110;

function nsRoundRect(mc, x, y, w, h, r)
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

// A frosted socket under the icon's art, at the icon clip's own bounds (so the line's centring doesn't change).
function nsSocket(icon)
{
   if(icon == undefined || icon.sanctuarySocket != undefined || icon.iconHolder._visible == false)
   {
      return;
   }
   var b = icon.getBounds(icon);
   var w = b.xMax - b.xMin;
   var h = b.yMax - b.yMin;
   if(w < 4 || h < 4)
   {
      return;
   }
   var scale = Math.max(Math.abs(icon._xscale) / 100,0.05);
   var m = 2 / scale;
   var sock = icon.createEmptyMovieClip("sanctuarySocket",-16384);
   icon.sanctuarySocket = sock;
   sock.lineStyle(1.5 / scale,NS_GOLD_LIGHT,100);
   sock.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:b.xMin - w * 0.15,y:b.yMin - h * 0.25,w:w * 1.3,h:h * 1.4,r:0});
   nsRoundRect(sock,b.xMin + m,b.yMin + m,w - m * 2,h - m * 2,Math.min(w,h) * 0.26);
   sock.endFill();
}

function nsStyleLine(mc)
{
   var t = mc.messageText1;
   var c = t.getTextFormat().color;
   var luma = 0.299 * (c >> 16 & 0xFF) + 0.587 * (c >> 8 & 0xFF) + 0.114 * (c & 0xFF);
   if(c == undefined || luma < NS_MIN_LUMA)
   {
      var f = new TextFormat();
      f.color = NS_GOLD_LIGHT;
      t.setTextFormat(f);
   }
   t.filters = [new flash.filters.GlowFilter(0x0A1030,0.95,4,4,5,2)];
   mc.messageText2._visible = false;
   nsSocket(mc.Icon);
}

var nsNotifyMessageSm = notifyMessageSm;
notifyMessageSm = function()
{
   var result = nsNotifyMessageSm.apply(_root,arguments);
   nsStyleLine(notifierMC);
   return result;
};
// ===== End Sanctuary Small Notifications =====

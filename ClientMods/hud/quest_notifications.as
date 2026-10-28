// ===== Sanctuary Quest Notifications =====
// Appended to notificationsQuests' main timeline by build_client_mods.py.
// The quest and collection banners at the top of the screen (wndNotificationQuests: quest started, objective updated,
// complete or failed, collection progress) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): the
// headline bounces in on a navy glass ribbon with a gold rim, its icon (check, cross, exclamation, collection) in a
// frosted socket, in outlined text keeping the stock green, red or blue; the quest name follows in outlined white and
// the objective in soft blue, with any item icon in a small frosted socket. initMainNote and the subtext calls are
// wrapped (styling after the stock layout; the bounce, bloom and fade tweens are untouched). The white bloom copy of
// the headline gets no ribbon, so its flash stays the shape of the icon and text.

var NQ_RIM = 0x9DB8FF;
var NQ_GOLD = 0xFFC531;
var NQ_GOLD_LIGHT = 0xFFE08A;
var NQ_SOFT = 0xC9D6FF;

function nqRoundRect(mc, x, y, w, h, r)
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

function nqSparkle(mc, x, y, r, color, alpha)
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

function nqOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.95,4,4,5,2)];
}

// A frosted rounded socket into mc at the rectangle b (in mc's coordinates).
function nqSocket(mc, b, rimWidth)
{
   var w = b.xMax - b.xMin;
   var h = b.yMax - b.yMin;
   mc.lineStyle(rimWidth,NQ_GOLD_LIGHT,100);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:b.xMin - w * 0.15,y:b.yMin - h * 0.25,w:w * 1.3,h:h * 1.4,r:0});
   nqRoundRect(mc,b.xMin,b.yMin,w,h,Math.min(w,h) * 0.28);
   mc.endFill();
}

// The headline's ribbon and icon socket, under the icon and text.
function nqRibbon(mov)
{
   var rib = mov.sanctuaryRibbon;
   if(rib == undefined)
   {
      rib = mov.createEmptyMovieClip("sanctuaryRibbon",-16384);
   }
   rib.clear();
   var t = mov.message_txt;
   var hb = mov.holder.getBounds(mov);
   var iconOk = mov.holder._visible != false && hb.xMax - hb.xMin > 4;
   var x = (iconOk ? hb.xMin : t._x) - 14;
   var right = t._x + t.textWidth + 30;
   var y = Math.min(iconOk ? hb.yMin : t._y,t._y) - 8;
   var bottom = Math.max(iconOk ? hb.yMax : 0,t._y + t.textHeight + 4) + 8;
   var w = right - x;
   var h = bottom - y;
   var r = Math.min(h / 2,28);
   rib.lineStyle(2.5,NQ_GOLD_LIGHT,90);
   rib.beginGradientFill("linear",[0x2E4FB0,0x141F55],[94,94],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   nqRoundRect(rib,x,y,w,h,r);
   rib.endFill();
   rib.lineStyle();
   rib.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:x - w * 0.1,y:y - h,w:w * 0.7,h:h * 2.4,r:0});
   nqRoundRect(rib,x + 3,y + 3,w - 6,h - 6,r - 3);
   rib.endFill();
   rib.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[22,0],[0,255],{matrixType:"box",x:x,y:y + 3,w:w,h:h * 0.35,r:Math.PI / 2});
   nqRoundRect(rib,x + r * 0.6,y + 3,w - r * 1.2,h * 0.35,h * 0.15);
   rib.endFill();
   nqSparkle(rib,x + w - 4,y + h / 2,5,NQ_GOLD_LIGHT,100);
   nqSparkle(rib,x + w - 18,y + 7,2.5,NQ_GOLD_LIGHT,90);
   var s = 1357;
   var i;
   for(i = 0; i < 5; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + 70 + s % Math.max(1,Math.round(w - 110));
      s = (s * 1103515245 + 12345) % 2147483648;
      nqSparkle(rib,sx,s % 2 == 0 ? y + 6 : y + h - 6,1 + s % 2,0xFFFFFF,25 + s % 35);
   }
   if(iconOk)
   {
      nqSocket(rib,{xMin:hb.xMin + 2,yMin:hb.yMin + 2,xMax:hb.xMax - 2,yMax:hb.yMax - 2},2);
   }
   rib.filters = [new flash.filters.GlowFilter(NQ_GOLD,0.5,16,16,2,2)];
}

var nqInitMainNote = initMainNote;
initMainNote = function(mov)
{
   var result = nqInitMainNote.apply(_root,arguments);
   mov.message_txt.filters = nqOutline();
   if(mov == mainNote_mc)
   {
      nqRibbon(mov);
   }
   return result;
};

// The quest name in white and the objective (or collection item) in soft blue, outlined; item icons in sockets.
function nqStyleSubText(mc, color)
{
   if(mc == undefined)
   {
      return;
   }
   mc.message_txt.textColor = color;
   mc.message_txt.filters = nqOutline();
   if(mc.holder != undefined && mc.holder._visible != false && mc.sanctuarySocket == undefined)
   {
      var b = mc.holder.getBounds(mc);
      if(b.xMax - b.xMin > 4)
      {
         var sock = mc.createEmptyMovieClip("sanctuarySocket",-16384);
         nqSocket(sock,{xMin:b.xMin - 1,yMin:b.yMin - 1,xMax:b.xMax + 1,yMax:b.yMax + 1},1.5);
      }
   }
}

var nqSubText1 = makeQuestSubText1;
makeQuestSubText1 = function()
{
   var result = nqSubText1.apply(_root,arguments);
   nqStyleSubText(subText1_mc,0xFFFFFF);
   return result;
};
var nqSubText2 = makeQuestSubText2;
makeQuestSubText2 = function()
{
   var result = nqSubText2.apply(_root,arguments);
   nqStyleSubText(subText2_mc,NQ_SOFT);
   return result;
};
var nqMakeSubText = makeSubText;
makeSubText = function()
{
   var result = nqMakeSubText.apply(_root,arguments);
   nqStyleSubText(subText1_mc,0xFFFFFF);
   nqStyleSubText(subText2_mc,NQ_SOFT);
   return result;
};
// ===== End Sanctuary Quest Notifications =====

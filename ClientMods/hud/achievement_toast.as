// ===== Sanctuary Achievement Toast =====
// Appended to achievementNotification's main timeline by build_client_mods.py.
// The "Trophy earned" pop-up (wndAchievementNotification, 600x130) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass card with a gold rim and sparkles, its heading on a gold pill
// tab, the achievement's icon in a frosted socket, the name in outlined white over a soft blue description, and the
// trophy on a gold halo with the points under it. The card is a registered class (AchievementNotificationPanel);
// its setItem is wrapped to restyle each card once the stock code has sized it. The stock plate (bg_mc, which the
// stock code widens for long names) and the icon's dark square stay as invisible clips.

var AN_RIM = 0x9DB8FF;
var AN_GOLD = 0xFFC531;
var AN_GOLD_LIGHT = 0xFFE08A;
var AN_INK = 0x3A2400;
var AN_SOFT = 0xC9D6FF;

// --- drawing helpers -------------------------------------------------------------------

function anRoundRect(mc, x, y, w, h, r)
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

function anGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   anRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function anSparkle(mc, x, y, r, alpha)
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

function anOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// --- the card --------------------------------------------------------------------------

function anRestyle(panel)
{
   var card = panel.sanctuaryCard;
   if(card == undefined)
   {
      card = panel.createEmptyMovieClip("sanctuaryCard",-16384);
   }
   card.clear();
   panel.bg_mc._alpha = 0;

   // The card, inside the stock plate's bounds (its top 12px held a tab, so the card starts a little lower).
   var b = panel.bg_mc.getBounds(panel);
   var x = b.xMin + 2;
   var y = b.yMin + 12;
   var w = b.xMax - b.xMin - 4;
   var h = b.yMax - b.yMin - 16;
   card.lineStyle(2.5,AN_GOLD_LIGHT,85);
   card.beginGradientFill("linear",[0x2E4FB0,0x141F55],[97,97],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   anRoundRect(card,x,y,w,h,26);
   card.endFill();
   card.lineStyle();
   card.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:x - w * 0.2,y:y - h * 0.8,w:w * 0.8,h:h * 2,r:0});
   anRoundRect(card,x + 3,y + 3,w - 6,h - 6,23);
   card.endFill();
   anGloss(card,x + 20,y + 3,w - 40,h * 0.3,10,16);

   // A gold halo behind the trophy.
   var tb = panel.trophyHolder_mc.getBounds(panel);
   var tx = (tb.xMin + tb.xMax) / 2;
   var ty = (tb.yMin + tb.yMax) / 2;
   if(tb.xMax - tb.xMin > 4)
   {
      card.beginGradientFill("radial",[AN_GOLD,AN_GOLD],[55,0],[0,255],{matrixType:"box",x:tx - 60,y:ty - 60,w:120,h:120,r:0});
      card.moveTo(tx - 60,ty - 60);
      card.lineTo(tx + 60,ty - 60);
      card.lineTo(tx + 60,Math.min(ty + 60,y + h - 4));
      card.lineTo(tx - 60,Math.min(ty + 60,y + h - 4));
      card.lineTo(tx - 60,ty - 60);
      card.endFill();
   }

   var s = 5151;
   var i;
   for(i = 0; i < 7; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + 30 + s % Math.round(w - 60);
      s = (s * 1103515245 + 12345) % 2147483648;
      anSparkle(card,sx,y + 6 + s % Math.round(h - 12),1 + s % 2,18 + s % 32);
   }

   // The heading on a gold pill tab over the card's top edge.
   var head = panel.head_txt;
   var hb = head.getBounds(panel);
   var tw = Math.max(head.textWidth + 44,150);
   var hx = (hb.xMin + hb.xMax) / 2 - tw / 2;
   var hy = hb.yMin + (hb.yMax - hb.yMin) / 2 - 15;
   card.lineStyle(1.5,0xFFF3C4,100);
   card.beginGradientFill("linear",[AN_GOLD_LIGHT,0xF5A623],[100,100],[0,255],{matrixType:"box",x:hx,y:hy,w:tw,h:30,r:Math.PI / 2});
   anRoundRect(card,hx,hy,tw,30,15);
   card.endFill();
   anGloss(card,hx + 10,hy + 2,tw - 20,13,6,55);
   head.textColor = AN_INK;
   head.filters = [];

   panel.name_txt.textColor = 0xFFFFFF;
   panel.name_txt.filters = anOutline();
   panel.desc_txt.textColor = AN_SOFT;
   panel.desc_txt.filters = anOutline();
   panel.points_txt.textColor = 0xFFFFFF;
   panel.points_txt.filters = [new flash.filters.GlowFilter(0x3A2400,0.9,4,4,5,2)];

   // The icon in a frosted socket with a gold rim.
   var wrap = panel.iconWrapper;
   wrap.icon_BG._alpha = 0;
   var ib = wrap.icon_BG.getBounds(wrap);
   var sock = wrap.sanctuarySocket;
   if(sock == undefined)
   {
      sock = wrap.createEmptyMovieClip("sanctuarySocket",-16384);
   }
   sock.clear();
   var iw = ib.xMax - ib.xMin;
   var ih = ib.yMax - ib.yMin;
   sock.lineStyle(2,AN_GOLD_LIGHT,100);
   sock.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:ib.xMin - iw * 0.15,y:ib.yMin - ih * 0.25,w:iw * 1.3,h:ih * 1.4,r:0});
   anRoundRect(sock,ib.xMin + 2,ib.yMin + 2,iw - 4,ih - 4,Math.min(iw,ih) * 0.24);
   sock.endFill();
   anGloss(sock,ib.xMin + 6,ib.yMin + 4,iw - 12,ih * 0.35,8,45);
   sock.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.55,5,5,1,2)];
}

var anSetItem = AchievementNotificationPanel.prototype.setItem;
AchievementNotificationPanel.prototype.setItem = function()
{
   var result = anSetItem.apply(this,arguments);
   anRestyle(this);
   return result;
};
// ===== End Sanctuary Achievement Toast =====

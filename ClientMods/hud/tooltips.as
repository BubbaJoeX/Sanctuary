// ===== Sanctuary Tooltips =====
// Appended to ToolTipSystem's main timeline by build_client_mods.py.
// Every tooltip in the game (items, abilities, buffs, jobs, pets, recipes, collections, labels) in the Main Menu's
// style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass card with a pale rim, a soft highlight from the
// top left, a gold glow behind the title and a few sparkles, the title in gold (an item's rarity colour is kept),
// outlined light text, gold dividers and item art in a frosted socket. Each tooltip is a library symbol whose
// background (a 9-slice sprite) is always at timeline depth 1, so the card is drawn under it at exactly its
// bounds and the stock background is made invisible: the size the movie reports to Lua (sendSetToolTipPos, which
// every tooltip calls once it's laid out) doesn't change. Members Only badges are gone (memberships don't exist).

var TT_GOLD = 0xFFC531;
var TT_GOLD_LIGHT = 0xFFE08A;
var TT_RIM = 0x9DB8FF;
var TT_SOFT = 0xC9D6FF;
// Text darker than this (0..255 luma) is lifted to TT_SOFT: everything has to read on navy.
var TT_MIN_LUMA = 110;

// --- drawing helpers -------------------------------------------------------------------

function ttRoundRect(mc, x, y, w, h, r)
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

function ttSparkle(mc, x, y, r, alpha)
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

function ttOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function ttLuma(c)
{
   return 0.299 * (c >> 16 & 0xFF) + 0.587 * (c >> 8 & 0xFF) + 0.114 * (c & 0xFF);
}

// The navy glass card, inside x, y, w, h (the stock background's bounds; the rim stays inside them so the
// tooltip's size doesn't change). Small one-line labels get a pill.
function ttDrawCard(card, x, y, w, h)
{
   card.clear();
   var r = h < 48 ? Math.min(h / 2 - 1,14) : 16;
   card.lineStyle(2,TT_RIM,80);
   card.beginGradientFill("linear",[0x2E4FB0,0x141F55],[96,96],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   ttRoundRect(card,x + 1,y + 1,w - 2,h - 2,r);
   card.endFill();
   card.lineStyle();
   // A soft radial highlight from the top left.
   card.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:x - w * 0.3,y:y - h * 0.6,w:w * 1.1,h:h * 1.4,r:0});
   ttRoundRect(card,x + 3,y + 3,w - 6,h - 6,Math.max(r - 2,2));
   card.endFill();
   // A gold glow behind the title.
   if(h >= 48)
   {
      card.beginGradientFill("radial",[TT_GOLD,TT_GOLD],[22,0],[0,255],{matrixType:"box",x:x + w * 0.15,y:y - 40,w:w * 0.7,h:110,r:0});
      ttRoundRect(card,x + 3,y + 3,w - 6,Math.min(h - 6,70),Math.max(r - 2,2));
      card.endFill();
   }
   // Gloss along the top.
   var gh = Math.min(h * 0.45,26);
   card.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[20,0],[0,255],{matrixType:"box",x:x,y:y + 2,w:w,h:gh,r:Math.PI / 2});
   ttRoundRect(card,x + 4,y + 2,w - 8,gh,Math.min(gh / 2,r - 2));
   card.endFill();
   // A dark inner edge at the bottom, so the card sits on the world.
   card.beginGradientFill("linear",[0x050A24,0x050A24],[0,40],[0,255],{matrixType:"box",x:x,y:y + h - 14,w:w,h:12,r:Math.PI / 2});
   ttRoundRect(card,x + 3,y + h - 14,w - 6,11,Math.min(5,r - 2));
   card.endFill();
   // A few sparkles in the top right (fixed seed, so a tooltip looks the same each time).
   if(h >= 48 && w >= 120)
   {
      var s = 4242;
      var i;
      for(i = 0; i < 4; i++)
      {
         s = (s * 1103515245 + 12345) % 2147483648;
         var sx = x + w * 0.62 + s % Math.round(w * 0.3);
         s = (s * 1103515245 + 12345) % 2147483648;
         ttSparkle(card,sx,y + 8 + s % 22,1 + s % 2,18 + s % 30);
      }
   }
}

// A frosted socket under an icon clip's art (stock icons are drawn for light plates), at its own bounds.
function ttSocket(icon)
{
   if(icon == undefined || icon.sanctuarySocket != undefined)
   {
      return;
   }
   var b = icon.getBounds(icon);
   var w = b.xMax - b.xMin;
   var h = b.yMax - b.yMin;
   if(w < 4 || h < 4 || w > 400)
   {
      return;
   }
   var sock = icon.createEmptyMovieClip("sanctuarySocket",-16384);
   icon.sanctuarySocket = sock;
   // Keep the rim about 1.5px on screen whatever the icon's scale.
   var scale = Math.max(Math.abs(icon._xscale) / 100,0.05);
   var m = 2 / scale;
   sock.lineStyle(1.5 / scale,TT_GOLD_LIGHT,90);
   sock.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:b.xMin - w * 0.15,y:b.yMin - h * 0.25,w:w * 1.3,h:h * 1.4,r:0});
   ttRoundRect(sock,b.xMin + m,b.yMin + m,w - m * 2,h - m * 2,Math.min(w,h) * 0.24);
   sock.endFill();
}

// Lifts dark text and outlines every text field under clip; recurses into its child clips.
function ttLightText(clip, depth)
{
   if(depth > 5)
   {
      return;
   }
   var k;
   for(k in clip)
   {
      var o = clip[k];
      if(o instanceof TextField)
      {
         if(ttLuma(o.textColor) < TT_MIN_LUMA)
         {
            o.textColor = TT_SOFT;
         }
         o.filters = ttOutline();
      }
      else if(o instanceof MovieClip && o._parent == clip && k != "sanctuaryCard" && k != "sanctuarySocket")
      {
         // Gold dividers: the stock rule is a thin sprite (named divide, divider, divide2 or m_Divider, or unnamed).
         if(o._height <= 5 && o._width > 30)
         {
            var c = new Color(o);
            c.setTransform({ra:0,rb:255,ga:0,gb:224,ba:0,bb:138,aa:70,ab:0});
         }
         ttLightText(o,depth + 1);
      }
   }
}

// The title: gold, unless Lua gave the item a rarity colour light enough to read.
function ttGoldTitle(t)
{
   if(t == undefined || !(t instanceof TextField))
   {
      return;
   }
   t.textColor = TT_GOLD_LIGHT;
}

function ttRestyle(mc)
{
   if(mc == undefined)
   {
      return;
   }
   var bg = mc.getInstanceAtDepth(-16383);
   if(bg == undefined)
   {
      return;
   }
   var b = bg.getBounds(mc);
   var card = mc.sanctuaryCard;
   if(card == undefined)
   {
      card = mc.createEmptyMovieClip("sanctuaryCard",-16384);
   }
   bg._alpha = 0;
   ttDrawCard(card,b.xMin,b.yMin,b.xMax - b.xMin,b.yMax - b.yMin);

   ttLightText(mc,0);

   // Titles. Items show txtTitle (no rarity colour) or txtTitle2 (Lua's colour, kept unless too dark).
   if(mc.ItemTitle != undefined)
   {
      ttGoldTitle(mc.ItemTitle.txtTitle);
      if(ttLuma(mc.ItemTitle.txtTitle2.textColor) < TT_MIN_LUMA)
      {
         ttGoldTitle(mc.ItemTitle.txtTitle2);
      }
      if(mc.ItemTitle.theIcon.holder._visible != false)
      {
         ttSocket(mc.ItemTitle.theIcon);
      }
   }
   ttGoldTitle(mc.txtTitle);
   ttGoldTitle(mc.mcTitleHolder.txtTitle);
   ttGoldTitle(mc.mcTitleHolder.txtName);
   ttGoldTitle(mc.titlebar.txtName);
   ttGoldTitle(mc.name_txt);
   if(mc.txtName instanceof TextField)
   {
      ttGoldTitle(mc.txtName);
   }
   if(mc.mcTitleHolder.icon.holder._visible != false)
   {
      ttSocket(mc.mcTitleHolder.icon);
   }
   ttSocket(mc.titlebar.icon);
   // The label-with-icon tooltip's icon (no holder: frSetIconByIndex loads into the clip itself, and hides it
   // when there's no icon).
   if(mc.name instanceof TextField && mc.icon instanceof MovieClip && mc.icon._visible)
   {
      ttSocket(mc.icon);
   }
   // Plain labels: white text.
   if(mc.name instanceof TextField)
   {
      mc.name.textColor = 0xFFFFFF;
   }
   // Members Only is gone (setData below clears the flag; this catches any badge already attached).
   mc.members_only_badge._visible = false;
}

// Every tooltip reports its size once it's laid out: restyle it just before.
var ttSendPos = core.ToolTipBase.prototype.sendSetToolTipPos;
core.ToolTipBase.prototype.sendSetToolTipPos = function()
{
   ttRestyle(this.mc);
   return ttSendPos.apply(this,arguments);
};

// Memberships don't exist: no item says Members Only (the stock layout then has no badge to make room for).
function ttNoMembers(cls)
{
   var setData = cls.prototype.setData;
   cls.prototype.setData = function()
   {
      var result = setData.apply(this,arguments);
      if(this._dataObject.MembersOnly != undefined)
      {
         this._dataObject.MembersOnly = "0";
      }
      return result;
   };
}

ttNoMembers(ToolTipItemUR);
ttNoMembers(ToolTipItemPlayerInspect);
ttNoMembers(ToolTipRecipe);
// ===== End Sanctuary Tooltips =====

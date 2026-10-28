// ===== Sanctuary Recipe Details =====
// Appended to recipe_details' main timeline by build_client_mods.py.
// Redraws a recipe's page (opened from the recipe browser) like a collection's page
// (ClientMods/collections/collection_details.as, the same stock movie family): a navy glass frame with the result's
// art in a big round frosted socket, its name in gold over the description and a gold "3 of 4 ready" bar, the
// ingredients as glass cards with their art in a round frosted socket (rimmed in gold with a check once you have
// enough, dimmed while you don't) and a gold have/need count, a Result card whose item sits in a frosted socket, and
// gold Back and Make It pills (Make It recessed until every ingredient is ready). The stock art (runtime images and
// plain shapes) is hidden; the stock clips keep their roles: Lua fills them through displayRecipeData and
// setRecipeData, and the stock buttons (invisible, over the pills' drawing) still take the clicks and send
// openCollectionsBrowser (back to the browser) and selectRecipe (start making it).

var RCD_W = 800;
var RCD_H = 600;
// The header: the result's icon (a 128px holder centered here), the title and description column, the bar.
var RCD_ICON_X = 193.4;
var RCD_ICON_Y = 97;
var RCD_ICON_R = 70;
var RCD_TEXT_X = 289;
var RCD_TEXT_W = 470;
var RCD_BAR_Y = 182;
// The ingredients bay and the result card.
var RCD_BAY = [48,212,586,300];
var RCD_REWARDS = [644,212,132,300];
var RCD_REWARDS_X = 710;
var RCD_REWARDS_Y = 222;
// An ingredient card in its slot's coordinates (the slot's 90px plate is at its origin): the card, its socket.
var RCD_CARD = [-20,-6,129,136];
var RCD_SOCKET_X = 44.4;
var RCD_SOCKET_Y = 42;
var RCD_SOCKET_R = 38;
// The ingredient art (a 96px holder at -3.6): 78% fills the socket.
var RCD_ICON_SCALE = 78;
// The bottom row's pills.
var RCD_PILL_Y = 553;
var RCD_GOLD = 0xFFC531;
var RCD_RIM = 0x9DB8FF;
var RCD_SOFT = 0xC9D6FF;
var RCD_INK = 0x3A2400;

var rcdFrame;

// --- drawing helpers -------------------------------------------------------------------

function rcdRoundRect(mc, x, y, w, h, r)
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

function rcdGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   rcdRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function rcdCircle(mc, x, y, r)
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

// The frosted socket behind stock art (the icons are made for light plates).
function rcdSocket(mc, x, y, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.25,y:y - r * 1.4,w:r * 2.5,h:r * 2.5,r:0});
   rcdCircle(mc,x,y,r);
   mc.endFill();
}

function rcdGoldFill(mc, x, y, w, h)
{
   mc.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
}

function rcdText(mc, name, txt, x, y, w, h, size, color, align)
{
   mc.createTextField(name,mc.getNextHighestDepth(),x,y,w,h);
   var t = mc[name];
   t.embedFonts = true;
   t.selectable = false;
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = align == undefined ? "center" : align;
   t.setNewTextFormat(f);
   t.text = txt;
   return t;
}

// Sets a stock field's box and colour and shrinks its font from size until the text fits (the stock fitters put
// a field back in its stock box every time they're called).
function rcdFit(t, x, y, w, h, size, color, align)
{
   t._x = x;
   t._y = y;
   t._width = w;
   t._height = h;
   var f = t.getTextFormat();
   f.size = size;
   f.color = color;
   f.align = align;
   t.setTextFormat(f);
   while((t.textHeight > h - 4 || !t.wordWrap && t.textWidth > w - 4) && f.size > 9)
   {
      f.size -= 1;
      t.setTextFormat(f);
   }
}

function rcdSparkle(mc, x, y, r, alpha)
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

function rcdOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function rcdShadow()
{
   return [new flash.filters.DropShadowFilter(1,90,0x0A1030,0.9,2,2,1,1)];
}

function rcdRule(mc, x, y, w)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[RCD_GOLD,RCD_GOLD,RCD_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:x,y:0,w:w,h:2,r:0});
   rcdRoundRect(mc,x,y,w,2,1);
   mc.endFill();
}

// The gold check on a ready ingredient or recipe.
function rcdCheck(mc, x, y, r)
{
   mc.lineStyle(2,0xFFF3C4,100);
   rcdGoldFill(mc,x - r,y - r,r * 2,r * 2);
   rcdCircle(mc,x,y,r);
   mc.endFill();
   mc.lineStyle(r * 0.3,RCD_INK,100);
   mc.moveTo(x - r * 0.45,y);
   mc.lineTo(x - r * 0.12,y + r * 0.38);
   mc.lineTo(x + r * 0.5,y - r * 0.38);
}

function rcdStrong()
{
   return mx.transitions.easing.Strong.easeOut;
}

// --- frame -----------------------------------------------------------------------------

function rcdDrawFrame(bg)
{
   rcdGradientRect(bg,6,4,RCD_W - 12,RCD_H - 10,30,0x2E4FB0,0x141F55,100,RCD_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-150,y:-300,w:900,h:700,r:0});
   rcdRoundRect(bg,8,6,RCD_W - 16,RCD_H - 14,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   rcdRoundRect(bg,18,16,RCD_W - 36,184,26);
   bg.endFill();

   // Sparkles in the header (fixed seed, same every time), clear of the icon.
   var s = 6262;
   var i;
   for(i = 0; i < 20; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 280 + s % (RCD_W - 320);
      s = (s * 1103515245 + 12345) % 2147483648;
      rcdSparkle(bg,x,24 + s % 40,1 + s % 3,20 + s % 45);
   }

   // The header's socket: a soft halo, a shadow and the frosted disc.
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[45,0],[0,255],{matrixType:"box",x:RCD_ICON_X - 92,y:RCD_ICON_Y - 92,w:184,h:184,r:0});
   rcdCircle(bg,RCD_ICON_X,RCD_ICON_Y,92);
   bg.endFill();
   bg.beginFill(0x050A24,30);
   rcdCircle(bg,RCD_ICON_X,RCD_ICON_Y + 4,RCD_ICON_R + 1);
   bg.endFill();

   rcdGradientRect(bg,RCD_BAY[0],RCD_BAY[1],RCD_BAY[2],RCD_BAY[3],22,0x0A1440,0x0A1440,38,RCD_RIM,22,1);
   rcdGradientRect(bg,RCD_REWARDS[0],RCD_REWARDS[1],RCD_REWARDS[2],RCD_REWARDS[3],22,0x284A9E,0x162660,100,RCD_RIM,40,1.5);
   bg.lineStyle();
   bg.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[16,0],[0,255],{matrixType:"box",x:0,y:RCD_REWARDS[1] + 3,w:10,h:46,r:Math.PI / 2});
   rcdRoundRect(bg,RCD_REWARDS[0] + 3,RCD_REWARDS[1] + 3,RCD_REWARDS[2] - 6,46,15);
   bg.endFill();
   rcdRule(bg,RCD_REWARDS[0] + 14,RCD_REWARDS_Y + 38,RCD_REWARDS[2] - 28);
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
}

// The header's socket rim (gold once every ingredient is ready) and the progress bar under the description.
function rcdDrawHeader(got, total)
{
   var h = rcdFrame.header;
   var done = total > 0 && got >= total;
   h.clear();
   rcdSocket(h,RCD_ICON_X,RCD_ICON_Y,RCD_ICON_R,done ? RCD_GOLD : 0xFFFFFF,done ? 100 : 85,done ? 4 : 2.5);
   h.check._visible = done;
   rcdFrame.count_txt.text = total > 0 ? got + " of " + total + " ready" : "";
   rcdFrame.count_txt.textColor = done ? 0xFFE08A : 0xFFFFFF;
   if(total <= 0)
   {
      return;
   }
   var x = RCD_TEXT_X;
   var w = RCD_TEXT_W - 170;
   h.lineStyle(1,RCD_RIM,30);
   h.beginFill(0x0A1440,65);
   rcdRoundRect(h,x,RCD_BAR_Y,w,14,7);
   h.endFill();
   if(got > 0)
   {
      var fw = Math.max(12,Math.round((w - 2) * Math.min(1,got / total)));
      h.lineStyle();
      rcdGoldFill(h,x,RCD_BAR_Y,w,14);
      rcdRoundRect(h,x + 1,RCD_BAR_Y + 1,fw,12,6);
      h.endFill();
      if(fw > 12)
      {
         h.beginFill(0xFFFFFF,40);
         rcdRoundRect(h,x + 5,RCD_BAR_Y + 3,fw - 8,3,1.5);
         h.endFill();
      }
   }
}

// A gold pill drawn where a stock button sits (the button is invisible over it and takes the click).
function rcdMakePill(name, label, w)
{
   var pill = rcdFrame.createEmptyMovieClip(name,rcdFrame.getNextHighestDepth());
   pill.w = w;
   pill.bg = pill.createEmptyMovieClip("bg",1);
   pill.label_txt = rcdText(pill,"label_txt",label,-w / 2,-12,w,26,18,RCD_INK);
   pill.draw = function(over, enabled)
   {
      var g = this.bg;
      var hw = this.w / 2;
      g.clear();
      if(enabled)
      {
         g.lineStyle(2,0xFFF3C4,100);
         g.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- hw,y:-21,w:this.w,h:42,r:Math.PI / 2});
      }
      else
      {
         g.lineStyle(2,RCD_RIM,40);
         g.beginFill(0x0A1440,60);
      }
      rcdRoundRect(g,- hw,-21,this.w,42,21);
      g.endFill();
      g.lineStyle();
      g.beginFill(0xFFFFFF,enabled ? (over ? 40 : 30) : 8);
      rcdRoundRect(g,10 - hw,-17,this.w - 20,12,6);
      g.endFill();
      g.filters = over && enabled ? [new flash.filters.GlowFilter(RCD_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,8,8,1,2)];
      this.label_txt.textColor = enabled ? RCD_INK : RCD_SOFT;
   };
   pill.draw(false,true);
   return pill;
}

// Follows a stock FRButton: centers it on the pill, and mirrors its hover and enabled state.
function rcdSyncPill(pill, btn, x)
{
   pill._x = x;
   pill._y = RCD_PILL_Y;
   pill._visible = btn._visible;
   var b = btn.getBounds(_root);
   if(b.xMax > b.xMin)
   {
      btn._x += x - (b.xMin + b.xMax) / 2;
      btn._y += RCD_PILL_Y - (b.yMin + b.yMax) / 2;
   }
   var enabled = btn.enabled != false && btn._alpha > 60;
   var over = enabled && (btn.HOVER._visible || btn.DOWN._visible);
   if(over == pill.over && enabled == pill.enabled)
   {
      return;
   }
   pill.over = over;
   pill.enabled = enabled;
   pill.draw(over,enabled);
   gs.TweenLite.to(pill,0.3,{_xscale:over ? 106 : 100,_yscale:over ? 106 : 100,ease:rcdStrong()});
}

// Stock FRButton art (state clips, icons and label), invisible: the button still takes the mouse.
function rcdHideButton(btn)
{
   btn.UP._alpha = btn.HOVER._alpha = btn.DOWN._alpha = 0;
   btn.ICON_UP._alpha = btn.ICON_HOVER._alpha = 0;
   btn.label._alpha = 0;
}

function rcdBuildFrame()
{
   rcdFrame = _root.createEmptyMovieClip("sanctuaryRecipeDetails",-16384);
   rcdDrawFrame(rcdFrame.createEmptyMovieClip("bg",1));
   var h = rcdFrame.header = rcdFrame.createEmptyMovieClip("header",2);
   var check = h.check = h.createEmptyMovieClip("check",1);
   rcdCheck(check,RCD_ICON_X + 52,RCD_ICON_Y - 50,16);
   check.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];
   check._visible = false;
   rcdFrame.count_txt = rcdText(rcdFrame,"count_txt","",RCD_TEXT_X + RCD_TEXT_W - 160,RCD_BAR_Y - 5,160,22,14,0xFFFFFF,"right");
   rcdFrame.count_txt.filters = rcdOutline();
   rcdFrame.back = rcdMakePill("back","Back",150);
   rcdFrame.make = rcdMakePill("make","Make It",190);

   // The stock art: the runtime panel images, the slot plate behind the title and the bottom panel shape.
   top_bg._visible = top_title_bg._visible = top_icon_slot_bg._visible = false;
   bottom_bg._visible = false;
   resultheader._visible = false;
   rcdHideButton(backbtn);
   rcdHideButton(closebtn);

   // The result's art: rounded into its socket.
   var mask = _root.createEmptyMovieClip("sanctuaryIconMask",_root.getNextHighestDepth());
   mask.beginFill(0,100);
   rcdCircle(mask,RCD_ICON_X,RCD_ICON_Y,RCD_ICON_R - 4);
   mask.endFill();
   collectionHeaderIcon.setMask(mask);
   collectionHeaderIcon._xscale = collectionHeaderIcon._yscale = 100;
   collectionHeaderIcon._x = RCD_ICON_X - 64;
   collectionHeaderIcon._y = RCD_ICON_Y - 64;

   // The rewards card: the stock list without its plate and separator, centered in the card.
   rewards._x = RCD_REWARDS_X;
   rewards._y = RCD_REWARDS_Y;
   rewards.background._alpha = 0;
   rewards.seperator._alpha = 0;
   rewards.header.filters = rcdOutline();
   rcdStyleRewardsHeader();
   // The slots' stock plates (runtime images, reloaded into their holders, so the plate clip stays hidden).
   var i;
   for(i = 1; i <= 8; i++)
   {
      _root["slot" + i].bg._visible = false;
   }
   rcdDrawHeader(0,0);
}

// --- the header ------------------------------------------------------------------------

function rcdStyleHeader()
{
   collectionHeader.filters = rcdOutline();
   rcdFit(collectionHeader,RCD_TEXT_X,24,RCD_TEXT_W,40,26,0xFFE08A,"left");
   collectionDescription.filters = rcdShadow();
   rcdFit(collectionDescription,RCD_TEXT_X,66,RCD_TEXT_W,106,15,RCD_SOFT,"left");
}

function rcdStyleRewardsHeader()
{
   var t = rewards.header;
   var f = t.getTextFormat();
   f.color = 0xFFE08A;
   t.setTextFormat(f);
}

// --- ingredients -----------------------------------------------------------------------

// The glass card under a slot's art (a clip in the slot under its stock children), and its art and name.
function rcdDrawSlot(slot)
{
   var c = slot.sanctuaryCard;
   if(c == undefined)
   {
      c = slot.sanctuaryCard = slot.createEmptyMovieClip("sanctuaryCard",-16384);
      c.check = c.createEmptyMovieClip("check",1);
      rcdCheck(c.check,RCD_SOCKET_X + 30,RCD_SOCKET_Y - 28,12);
      c.check.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];
      // The art is rounded into the socket.
      var mask = slot.createEmptyMovieClip("sanctuaryIconMask",slot.getNextHighestDepth());
      mask.beginFill(0,100);
      rcdCircle(mask,RCD_SOCKET_X,RCD_SOCKET_Y,RCD_SOCKET_R - 3);
      mask.endFill();
      slot.icon.setMask(mask);
      slot.bg._visible = false;
   }
   var done = slot.sanctuaryFound == true;
   var x = RCD_CARD[0];
   var y = RCD_CARD[1];
   var w = RCD_CARD[2];
   var h = RCD_CARD[3];
   c.clear();
   rcdGradientRect(c,x,y,w,h,18,0x284A9E,0x162660,100,done ? RCD_GOLD : RCD_RIM,done ? 100 : 40,done ? 3 : 1.5);
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[16,0],[0,255],{matrixType:"box",x:0,y:y + 3,w:10,h:44,r:Math.PI / 2});
   rcdRoundRect(c,x + 3,y + 3,w - 6,44,15);
   c.endFill();
   var halo = done ? RCD_GOLD : 0x8FB4FF;
   c.beginGradientFill("radial",[halo,halo],[done ? 45 : 30,0],[0,255],{matrixType:"box",x:RCD_SOCKET_X - 50,y:RCD_SOCKET_Y - 50,w:100,h:100,r:0});
   rcdCircle(c,RCD_SOCKET_X,RCD_SOCKET_Y,48);
   c.endFill();
   c.beginFill(0x050A24,30);
   rcdCircle(c,RCD_SOCKET_X,RCD_SOCKET_Y + 3,RCD_SOCKET_R + 1);
   c.endFill();
   rcdSocket(c,RCD_SOCKET_X,RCD_SOCKET_Y,RCD_SOCKET_R,done ? RCD_GOLD : 0xFFFFFF,done ? 100 : 85,done ? 3 : 2);
   c.check._visible = done;
   c.filters = done ? [new flash.filters.GlowFilter(RCD_GOLD,0.35,10,10,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];

   slot.icon._xscale = slot.icon._yscale = RCD_ICON_SCALE;
   slot.icon._x = RCD_SOCKET_X - 48 * RCD_ICON_SCALE / 100;
   slot.icon._y = RCD_SOCKET_Y - 48 * RCD_ICON_SCALE / 100;
   // The stock fades an ingredient you don't have enough of (setRecipeData), and that stays.
   slot.name.filters = rcdOutline();
   rcdFit(slot.name,x + 6,RCD_SOCKET_Y + RCD_SOCKET_R + 3,w - 12,28,14,done ? 0xFFFFFF : 0xE4ECFF,"center");
   // How many you have of how many it takes.
   var n = slot.numbers_txt;
   n.filters = rcdOutline();
   n.wordWrap = false;
   rcdFit(n,x + 6,RCD_SOCKET_Y + RCD_SOCKET_R + 29,w - 12,22,13,done ? 0xFFE08A : RCD_SOFT,"center");
}

// Counts the shown ingredients and the ready ones, for the header's bar.
function rcdRefresh()
{
   var got = 0;
   var total = 0;
   var i;
   for(i = 1; i <= 8; i++)
   {
      var slot = _root["slot" + i];
      if(slot._visible)
      {
         total++;
         if(slot.sanctuaryFound)
         {
            got++;
         }
      }
   }
   rcdDrawHeader(got,total);
}

// --- rewards ---------------------------------------------------------------------------

// Each reward's art sits in a frosted socket (RewardItem attaches its icon when it updates).
function rcdPatchRewards()
{
   var update = RewardItem.prototype.update;
   RewardItem.prototype.update = function()
   {
      var r = update.apply(this,arguments);
      var s = this.sanctuarySocket;
      if(s == undefined)
      {
         s = this.sanctuarySocket = this.createEmptyMovieClip("sanctuarySocket",-16384);
      }
      s.clear();
      if(this.icon != undefined)
      {
         // Rows are 64px apart: the 64px art sits at 58px inside a 62px socket.
         this.icon._xscale = this.icon._yscale = 90;
         this.icon._x = -28.8;
         this.icon._y = 3.2;
         s.lineStyle();
         s.beginFill(0x050A24,30);
         rcdCircle(s,0,34,31);
         s.endFill();
         rcdSocket(s,0,32,31,0xFFFFFF,85,2);
      }
      return r;
   };
   // The coin and experience rewards' amounts, in gold.
   var listUpdate = RewardsList.prototype.update;
   RewardsList.prototype.update = function()
   {
      var r = listUpdate.apply(this,arguments);
      var i;
      for(i = this.offset; i < Math.min(this.offset + this.rows,this.m_DisplayList.length); i++)
      {
         var item = this["reward_item[ " + i + " ]"];
         if(item.label != undefined)
         {
            var f = item.label.getTextFormat();
            f.color = 0xFFE08A;
            item.label.setTextFormat(f);
            item.label.filters = rcdOutline();
         }
      }
      return r;
   };
   var setText = RewardsList.prototype.setText;
   RewardsList.prototype.setText = function(str)
   {
      var r = setText.apply(this,arguments);
      if(String(str).indexOf("S##") == 0)
      {
         this.header.text = "Result";
      }
      rcdStyleRewardsHeader();
      return r;
   };
}

// --- Lua hooks -------------------------------------------------------------------------

var rcdDisplayRecipeData = displayRecipeData;
displayRecipeData = function(id, name, icon, desc, memberOnly, tint, rewardId)
{
   var r = rcdDisplayRecipeData.apply(_root,arguments);
   var i;
   for(i = 1; i <= 8; i++)
   {
      _root["slot" + i].sanctuaryFound = false;
   }
   // The stock result header is a string key this client doesn't have; plain text instead.
   rewards.header.text = "Result";
   rcdStyleRewardsHeader();
   rcdStyleHeader();
   rcdRefresh();
   return r;
};

// Each ingredient fills the next slot; it's ready when you have as many as it takes (the stock leaves its art
// at full alpha then).
var rcdSetRecipeData = setRecipeData;
setRecipeData = function(name, iconId, desc, requiredCount, currentCount, tint)
{
   var slot = _root["slot" + m_slotCount];
   var r = rcdSetRecipeData.apply(_root,arguments);
   slot.sanctuaryFound = Number(currentCount) >= Number(requiredCount);
   rcdDrawSlot(slot);
   rcdRefresh();
   return r;
};

rcdPatchRewards();
rcdBuildFrame();
rcdFrame.onEnterFrame = function()
{
   rcdSyncPill(this.back,backbtn,RCD_W / 2 - 100);
   rcdSyncPill(this.make,closebtn,RCD_W / 2 + 100);
};
// ===== End Sanctuary Recipe Details =====

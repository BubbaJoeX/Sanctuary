// ===== Sanctuary lot panel =====
// Appended to housingSidePanel's main timeline by build_client_mods.py.
// Redraws the lot panel (top right in a house: its name, owner and rating, and Submit to the Housing Directory for
// the owner or Vote and Report for a visitor) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a
// navy glass card with the house's medallion in a gold-rimmed frosted disc over its top-left corner, a caption and
// sparkles in the header, the name in gold over the owner in soft blue, gold rating stars, and gold pills for Submit
// and Vote (a red one for Delete). The collapse button is a navy glass disc rimmed in gold, and the collapsed panel
// is a glass tile with a navy name tag. The stock clips keep their roles (hit areas, Lua callbacks, tooltips): their
// art is hidden (alpha 0) and the new look is drawn under them. Lua re-sets the texts, so they're recolored after.

var HL_W = 182;
var HL_H = 346;
// The medallion: the house art's center (houseRatingIcon in take_mc, a shape in vote_mc) and the disc's radius.
var HL_MEDAL_X = 30;
var HL_MEDAL_Y = 21;
var HL_MEDAL_R = 33;
var HL_GOLD = 0xFFC531;
var HL_RIM = 0x9DB8FF;
var HL_SOFT = 0xC9D6FF;
var HL_INK = 0x3A2400;

// --- drawing helpers -------------------------------------------------------------------

function hlRoundRect(mc, x, y, w, h, r)
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

function hlGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   hlRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function hlCircle(mc, x, y, r)
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

function hlFrostedCircle(mc, x, y, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.2,y:y - r * 1.3,w:r * 2.6,h:r * 2.6,r:0});
   hlCircle(mc,x,y,r);
   mc.endFill();
}

// A gold (or red) pill with a glassy shine across its top half.
function hlPill(mc, x, y, w, h, r, over, red)
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
   hlRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h / 2,r:Math.PI / 2});
   hlRoundRect(mc,x + 3,y + 2,w - 6,h / 2,Math.min(r,h / 4));
   mc.endFill();
}

function hlText(mc, name, txt, x, y, w, h, size, color, align)
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

function hlSparkle(mc, x, y, r, alpha)
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

function hlSparkles(mc, seed, count, x, y, w, h)
{
   var s = seed;
   var i;
   for(i = 0; i < count; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + s % w;
      s = (s * 1103515245 + 12345) % 2147483648;
      hlSparkle(mc,sx,y + s % h,1 + s % 3,25 + s % 50);
   }
}

function hlOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function hlShadow(distance, blur, alpha)
{
   return [new flash.filters.DropShadowFilter(distance,90,0x050A24,alpha,blur,blur,1,2)];
}

function hlGlow(color, blur)
{
   return [new flash.filters.GlowFilter(color,0.8,blur,blur,2,2)];
}

function hlGoldRule(mc, x, y, w)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[HL_GOLD,HL_GOLD,HL_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:x,y:y,w:w,h:2,r:0});
   hlRoundRect(mc,x,y,w,2,1);
   mc.endFill();
}

// Sets a text field's color and outline (Lua's texts come with the stock yellow and white).
function hlColor(t, color)
{
   if(t == undefined)
   {
      return;
   }
   t.textColor = color;
   t.filters = hlOutline();
}

// --- FRIconButton skins ------------------------------------------------------------------
// A skinned button has hlDraw(state); its states call it after the stock code has swapped the art.

var hlStateUp = FRIconButton.prototype.setStateUp;
FRIconButton.prototype.setStateUp = function()
{
   hlStateUp.apply(this,arguments);
   if(this.hlDraw != undefined)
   {
      this.hlDraw("up");
   }
};

var hlStateOver = FRIconButton.prototype.setStateOver;
FRIconButton.prototype.setStateOver = function()
{
   hlStateOver.apply(this,arguments);
   if(this.hlDraw != undefined && this.m_isEnabled)
   {
      this.hlDraw("over");
   }
};

var hlStateDown = FRIconButton.prototype.setStateDown;
FRIconButton.prototype.setStateDown = function()
{
   hlStateDown.apply(this,arguments);
   if(this.hlDraw != undefined && this.m_isEnabled)
   {
      this.hlDraw("down");
   }
};

// Hides a button's state art (its hit area stays) and gives it a skin clip at a timeline depth (under its art).
function hlSkin(btn, draw, depth)
{
   btn.up_mc._alpha = btn.over_mc._alpha = btn.down_mc._alpha = 0;
   btn.hlSkinMc = btn.createEmptyMovieClip("hlSkinMc",depth - 16384);
   btn.hlDraw = draw;
   btn.hlDraw("up");
}

// The label of a pill button: the stock label's text (Lua and setTextToFieldWidth set it) in dark ink, shrunk to fit.
function hlPillLabel(btn, x, y, w, h)
{
   var s = btn.hlSkinMc;
   if(s.label_txt == undefined)
   {
      hlText(s,"label_txt","",x,y + h / 2 - 11,w,24,16,HL_INK);
   }
   var t = s.label_txt;
   var f = t.getTextFormat();
   f.size = 16;
   t.setNewTextFormat(f);
   t.text = btn.up_mc.textField_txt.text;
   while(t.textWidth > w - 12 && f.size > 9)
   {
      f.size--;
      t.setTextFormat(f);
   }
}

// Submit and Vote: gold pills over the stock 100x36 buttons.
function hlDrawGoldButton(state)
{
   var s = this.hlSkinMc;
   s.clear();
   var over = state == "over" || state == "down";
   hlPill(s,0,0,100,36,18,over,false);
   hlPillLabel(this,0,0,100,36);
   s._y = state == "down" ? 1 : 0;
   this.filters = over ? hlGlow(HL_GOLD,14) : hlShadow(3,8,0.55);
}

// Delete: a red pill (its states sit centered on the button).
function hlDrawRedButton(state)
{
   var s = this.hlSkinMc;
   s.clear();
   var over = state == "over" || state == "down";
   hlPill(s,-50,-18,100,36,18,over,true);
   hlPillLabel(this,-50,-18,100,36);
   s.label_txt.textColor = 0xFFFFFF;
   s.label_txt.filters = [new flash.filters.GlowFilter(0x5A0E06,0.8,3,3,3,2)];
   s._y = state == "down" ? 1 : 0;
   this.filters = over ? hlGlow(0xFF7A5C,14) : hlShadow(3,8,0.55);
}

// Collapse: a navy glass disc rimmed in gold over the stock disc, under the arrow icons.
function hlDrawCollapse(state)
{
   var s = this.hlSkinMc;
   s.clear();
   var over = state == "over" || state == "down";
   var r = 22.5;
   s.lineStyle(over ? 3 : 2,over ? 0xFFE08A : HL_GOLD,100);
   s.beginGradientFill("linear",over ? [0x5A82EA,0x1D307A] : [0x3F66D6,0x141F55],[100,100],[0,255],{matrixType:"box",x:21 - r,y:21.5 - r,w:r * 2,h:r * 2,r:Math.PI / 2});
   hlCircle(s,21,21.5,r);
   s.endFill();
   s.lineStyle();
   s.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[40,0],[0,255],{matrixType:"box",x:21 - r,y:21.5 - r,w:r * 2,h:r,r:Math.PI / 2});
   hlRoundRect(s,8,2,26,14,7);
   s.endFill();
   this.filters = over ? hlGlow(HL_GOLD,14) : hlShadow(3,8,0.55);
}

// --- panel -----------------------------------------------------------------------------

var hlCard;
// "take" (your own house: Submit) or "vote" (visiting: rate it).
var hlView = "take";

function hlDrawCard()
{
   var c = hlCard.bg;
   c.clear();
   hlGradientRect(c,0,0,HL_W,HL_H,26,0x2E4FB0,0x141F55,100,HL_RIM,80,3);
   c.lineStyle();
   c.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-120,y:-160,w:360,h:380,r:0});
   hlRoundRect(c,2,2,HL_W - 4,HL_H - 4,24);
   c.endFill();
   // The header band, with a few sparkles.
   c.beginFill(0x0A1440,30);
   hlRoundRect(c,4,4,HL_W - 8,44,22);
   c.endFill();
   hlSparkles(c,4848,10,66,8,76,34);
   if(hlView == "take")
   {
      // Under the name, owner and rating.
      hlGoldRule(c,18,132,HL_W - 36);
   }

   // The medallion: a frosted disc in a gold ring over the card's corner, under the house art.
   var m = hlCard.medal;
   m.clear();
   m.lineStyle(3,HL_GOLD,100);
   m.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:HL_MEDAL_X - HL_MEDAL_R,y:HL_MEDAL_Y - HL_MEDAL_R,w:HL_MEDAL_R * 2,h:HL_MEDAL_R * 2,r:Math.PI / 2});
   hlCircle(m,HL_MEDAL_X,HL_MEDAL_Y,HL_MEDAL_R);
   m.endFill();
   hlFrostedCircle(m,HL_MEDAL_X,HL_MEDAL_Y,HL_MEDAL_R - 5,0xFFF3C4,100,1);

   hlCard.caption_txt.text = hlView == "take" ? "My Home" : "Visiting";
}

function hlBuildCard()
{
   hlCard = sidePanel_mc.createEmptyMovieClip("hlCard",-16384);
   hlCard.bg = hlCard.createEmptyMovieClip("bg",1);
   hlCard.bg.filters = hlShadow(6,18,0.6);
   hlCard.medal = hlCard.createEmptyMovieClip("medal",2);
   hlCard.medal.filters = hlGlow(HL_GOLD,12);
   hlCard.caption_txt = hlText(hlCard,"caption_txt","",62,16,84,22,15,0xFFE08A);
   hlCard.caption_txt.filters = hlOutline();

   // The stock card art stays as an invisible hit area (clicks on the panel don't reach the world).
   sidePanel_mc.background_mc._alpha = 0;
   sidePanel_mc.topPiece_mc._alpha = 0;
   hlDrawCard();
}

// The rating stars fill gold.
function hlGoldStars(stars)
{
   if(stars.star_tweener_mc != undefined)
   {
      new Color(stars.star_tweener_mc).setRGB(HL_GOLD);
   }
   stars.filters = hlShadow(2,4,0.6);
}

// Lua's texts arrive in the stock yellow and white; recolor them after every update.
function hlColorTexts()
{
   var take = sidePanel_mc.take_mc;
   var vote = sidePanel_mc.vote_mc;
   hlColor(take.house_name_txt,0xFFE08A);
   hlColor(take.player_name_txt,HL_SOFT);
   hlColor(take.instructions_txt,0xFFFFFF);
   hlColor(take.delete_instructions_txt,HL_SOFT);
   hlColor(vote.house_name_txt,0xFFE08A);
   hlColor(vote.player_name_txt,HL_SOFT);
   hlColor(vote.votes_desc_txt,HL_SOFT);
   hlColor(vote.rating_desc_txt,HL_SOFT);
   hlColor(vote.visits_txt,0xFFFFFF);
   hlColor(vote.instructions_txt,0xFFFFFF);
   take.takeButton_mc.hlDraw("up");
   take.deleteButton_mc.hlDraw("up");
   vote.voteBtn_mc.hlDraw("up");
}

function hlSetView(view)
{
   if(view != hlView)
   {
      hlView = view;
      hlDrawCard();
   }
   hlColorTexts();
}

// --- collapsed panel -------------------------------------------------------------------

// The collapsed panel: a glass tile rimmed in gold over the stock disc (a shape), under the house icon.
function hlDrawTile(over)
{
   var t = minPanel_mc.hlTile;
   t.clear();
   hlGradientRect(t,-30,-29,62,62,18,over ? 0x4F78E6 : 0x2E4FB0,over ? 0x1D307A : 0x141F55,100,over ? 0xFFE08A : HL_GOLD,100,2.5);
   hlFrostedCircle(t,1,2,23,0x0A1440,30,1);
   t.filters = over ? hlGlow(HL_GOLD,16) : hlShadow(4,10,0.6);
}

// The name tag beside it on hover: a navy glass pill sized like the stock one.
function hlDrawNameTag()
{
   var tag = minPanel_mc.nametag;
   var bg = tag.nametag_BG;
   var p = tag.hlPill;
   p.clear();
   hlGradientRect(p,bg._x,-13,bg._width - 8,30,15,0x2E4FB0,0x141F55,100,HL_GOLD,90,2);
   p.filters = hlShadow(3,8,0.55);
}

function hlBuildTile()
{
   // The icon moves above the tile, which takes its depth (a free one between the stock disc and the icon).
   minPanel_mc.icon.swapDepths(20);
   minPanel_mc.hlTile = minPanel_mc.createEmptyMovieClip("hlTile",8 - 16384);
   hlDrawTile(false);

   var tag = minPanel_mc.nametag;
   tag.nametag_BG._alpha = 0;
   tag.hlPill = tag.createEmptyMovieClip("hlPill",-16384);
   tag.questName.textColor = 0xFFE08A;
   tag.questName.filters = hlOutline();
   hlDrawNameTag();

   var over = minPanel_mc.onRollOver;
   minPanel_mc.onRollOver = function()
   {
      over.apply(this,arguments);
      hlDrawTile(true);
      gs.TweenLite.to(this.hlTile,0.6,{_xscale:110,_yscale:110,ease:mx.transitions.easing.Elastic.easeOut});
   };
   var out = minPanel_mc.onRollOut;
   minPanel_mc.onRollOut = function()
   {
      out.apply(this,arguments);
      hlDrawTile(false);
      gs.TweenLite.to(this.hlTile,0.6,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Elastic.easeOut});
   };
   minPanel_mc.onReleaseOutside = minPanel_mc.onRollOut;
}

// --- Lua -------------------------------------------------------------------------------

var hlPopulateVote = populateVoteWindow_lua;
populateVoteWindow_lua = function(houseName, playerName, currentVote, votes)
{
   hlPopulateVote.apply(_root,arguments);
   hlSetView("vote");
};

var hlPopulatePublish = populatePublishWindow_lua;
populatePublishWindow_lua = function(houseName, playerName, currentVote)
{
   hlPopulatePublish.apply(_root,arguments);
   hlSetView("take");
};

var hlShowTake = showTakeWindow_lua;
showTakeWindow_lua = function()
{
   hlShowTake.apply(_root,arguments);
   hlColorTexts();
};

var hlPublishHouse = publishHouseToLua_lua;
publishHouseToLua_lua = function()
{
   hlPublishHouse.apply(_root,arguments);
   hlColorTexts();
};

var hlDeleteHouse = deleteHouseToLua_lua;
deleteHouseToLua_lua = function()
{
   hlDeleteHouse.apply(_root,arguments);
   hlColorTexts();
};

var hlSetTagName = setTagName;
setTagName = function(sName)
{
   hlSetTagName.apply(_root,arguments);
   minPanel_mc.nametag.questName.textColor = 0xFFE08A;
   hlDrawNameTag();
};

// --- build -----------------------------------------------------------------------------

function hlBuild()
{
   hlBuildCard();

   var take = sidePanel_mc.take_mc;
   var vote = sidePanel_mc.vote_mc;
   // Pill buttons: their skins go under everything in them (their state art is hidden).
   hlSkin(take.takeButton_mc,hlDrawGoldButton,0);
   hlSkin(take.deleteButton_mc,hlDrawRedButton,0);
   hlSkin(vote.voteBtn_mc,hlDrawGoldButton,0);
   // The collapse button's disc goes over its stock disc (a shape at depth 1) and under its arrows.
   hlSkin(sidePanel_mc.centerer_mc.minimize_btn,hlDrawCollapse,3);
   vote.frIconButton_reports_mc.filters = hlShadow(3,8,0.55);

   hlGoldStars(take.resultStars_mc);
   hlGoldStars(vote.resultStars_mc);
   take.houseRatingIcon.filters = hlShadow(2,6,0.5);

   hlBuildTile();
   hlColorTexts();
}

hlBuild();
// ===== End Sanctuary lot panel =====

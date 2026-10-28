// ===== Sanctuary quest dialog =====
// Appended to queststart's main timeline by build_client_mods.py.
// Redraws the quest offer and turn-in dialog (wndQuestStart, wndQuestEnd) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): the NPC's speech bubble becomes a navy glass card with a gold New Quest
// (or Quest Complete) tab and a tail toward the NPC, the NPC's words are white, the quest details sit in a recess
// with the quest's icon in a frosted socket and gold dividers, the coin and experience rewards are glass chips, and
// the answers are pills on a glass tray (the first in gold, the rest navy; hovering lifts them with a gold glow).
// Every stock clip keeps its role: the bubble, panel and tray art go invisible (still the hit areas the stock code
// tweens and clicks) and the cards follow their size. Membership no longer exists, so no answer is greyed out as
// members only and the Upgrade badge is gone.

var QS_GOLD = 0xFFC531;
var QS_RIM = 0x9DB8FF;
var QS_SOFT = 0xC9D6FF;
var QS_INK = 0x3A2400;
// The bubble art's body is 360 of its 390 width (the rest is its tail); the answer tray's art is 24 + 44 + 50 tall
// with its own tail in the last 16.
var QS_BUBBLE_W = 360;
var QS_TRAY_TAIL = 16;

// --- drawing helpers -------------------------------------------------------------------

function qsRoundRect(mc, x, y, w, h, r)
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

function qsGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   qsRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function qsPanel(mc, x, y, w, h, r, alpha, rimAlpha)
{
   mc.lineStyle(1,QS_RIM,rimAlpha);
   mc.beginFill(0x0A1440,alpha);
   qsRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function qsGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   qsRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function qsCircle(mc, x, y, r)
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

function qsSparkle(mc, x, y, r, alpha)
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

// A gold hairline that fades out at both ends.
function qsDivider(mc, x, y, w)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[QS_GOLD,QS_GOLD,QS_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:x,y:0,w:w,h:2,r:0});
   qsRoundRect(mc,x,y,w,2,1);
   mc.endFill();
}

function qsText(mc, name, txt, x, y, w, h, size, color, align)
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

function qsOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Recolours a stock field (all of its text, html included, and what it's given next) and outlines it.
function qsLightText(t, color, outline)
{
   if(t == undefined)
   {
      return;
   }
   t.textColor = color;
   var f = t.getNewTextFormat();
   f.color = color;
   t.setNewTextFormat(f);
   t.filters = outline == false ? [] : qsOutline();
}

function qsStrongOut(t, b, c, d)
{
   t = t / d - 1;
   return c * (t * t * t * t * t + 1) + b;
}

// --- the bubble ------------------------------------------------------------------------

function qsDrawBubble()
{
   var bg = dialog.background;
   var c = dialog.qsCard;
   var h = Math.max(60,bg._height - 4);
   c.qsH = bg._height;
   c.clear();
   // The tail toward the NPC, then the card over its root.
   var ty = Math.round(h * 0.44);
   c.lineStyle(3,QS_RIM,80);
   c.beginFill(0x203A8A,100);
   c.moveTo(QS_BUBBLE_W - 4,ty - 18);
   c.lineTo(QS_BUBBLE_W + 22,ty);
   c.lineTo(QS_BUBBLE_W - 4,ty + 18);
   c.endFill();
   qsGradientRect(c,2,2,QS_BUBBLE_W - 4,h,26,0x2E4FB0,0x141F55,100,QS_RIM,80,3);
   c.lineStyle();
   c.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-120,y:-200,w:460,h:440,r:0});
   qsRoundRect(c,4,4,QS_BUBBLE_W - 8,h - 4,24);
   c.endFill();
   qsGloss(c,10,4,QS_BUBBLE_W - 20,40,20,16);
   // Sparkles across the top (fixed seed, same every time).
   var s = 7373;
   var i;
   for(i = 0; i < 12; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 24 + s % (QS_BUBBLE_W - 48);
      s = (s * 1103515245 + 12345) % 2147483648;
      qsSparkle(c,x,8 + s % 16,1 + s % 2,20 + s % 40);
   }
   c.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   qsDrawTab();
}

// The gold tab on the bubble's top edge: New Quest, or Quest Complete when it's the turn-in (Maximize()).
function qsDrawTab()
{
   var tab = dialog.qsTab;
   var label = dialog.isEnd ? "QUEST COMPLETE" : "NEW QUEST";
   if(tab.label_txt.text == label && tab.qsDrawn)
   {
      return;
   }
   tab.qsDrawn = true;
   tab.label_txt.text = label;
   var w = Math.round(tab.label_txt.textWidth + 44);
   tab.label_txt._x = - w / 2;
   tab.label_txt._width = w;
   var g = tab.bg;
   g.clear();
   g.lineStyle(1.5,0xFFF3C4,100);
   g.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- w / 2,y:-13,w:w,h:26,r:Math.PI / 2});
   qsRoundRect(g,- w / 2,-13,w,26,13);
   g.endFill();
   qsGloss(g,- w / 2 + 8,-12,w - 16,10,5,50);
   // A star on each side of the label.
   var k;
   for(k = -1; k <= 1; k += 2)
   {
      var sx = k * (w / 2 - 13);
      g.lineStyle();
      g.beginFill(QS_INK,80);
      var j;
      for(j = 0; j <= 10; j++)
      {
         var a = - Math.PI / 2 + j * Math.PI / 5;
         var d = j % 2 == 0 ? 6 : 2.6;
         if(j == 0)
         {
            g.moveTo(sx + Math.cos(a) * d,Math.sin(a) * d);
         }
         else
         {
            g.lineTo(sx + Math.cos(a) * d,Math.sin(a) * d);
         }
      }
      g.endFill();
   }
   tab.filters = [new flash.filters.GlowFilter(QS_GOLD,0.5,12,12,2,2),new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,6,6,1,2)];
}

// The quest panel: a recess under the stock plate (its art hidden; it stays the Show Details hit area), the gold
// dividers where the stock ones are, and a frosted socket behind the quest's icon.
function qsDrawPanel()
{
   var p = dialog.panel;
   var g = p.qsRecess;
   var h = p.background._height;
   g.qsH = h;
   g.qsDivY = p.bottomDivider._y;
   g.clear();
   qsPanel(g,2,2,p.background._width - 4,h - 4,18,40,28);
   qsGloss(g,8,3,p.background._width - 16,20,10,10);
   qsDivider(g,p.topDivider._x + 6,p.topDivider._y,p.background._width - 2 * p.topDivider._x - 12);
   if(p.minimizeObject.mode)
   {
      qsDivider(g,p.topDivider._x + 6,p.bottomDivider._y,p.background._width - 2 * p.topDivider._x - 12);
   }
}

function qsStyleBubble()
{
   var bg = dialog.background;
   bg._alpha = 0;
   dialog.qsCard = dialog.createEmptyMovieClip("qsCard",-16384);
   var tab = dialog.qsTab = dialog.createEmptyMovieClip("qsTab",dialog.getNextHighestDepth());
   tab._x = QS_BUBBLE_W / 2;
   tab._y = 2;
   tab.bg = tab.createEmptyMovieClip("bg",1);
   tab.label_txt = qsText(tab,"label_txt","",-80,-10,160,22,13,QS_INK);
   qsDrawBubble();
   // The NPC's words sit just under the tab.
   qsLightText(dialog.NPCText,0xFFFFFF);

   var p = dialog.panel;
   p.background._alpha = 0;
   p.topDivider._alpha = 0;
   p.bottomDivider._alpha = 0;
   p.qsRecess = p.createEmptyMovieClip("qsRecess",-16384);
   qsDrawPanel();
   qsLightText(p.minimizeObject.label,0xFFFFFF);

   // The card follows the stock art's height while the stock code tweens it (a cheap check each frame).
   dialog.qsCard.onEnterFrame = function()
   {
      if(dialog.background._height != this.qsH)
      {
         qsDrawBubble();
      }
      var r = dialog.panel.qsRecess;
      if(dialog.panel.background._height != r.qsH || dialog.panel.bottomDivider._y != r.qsDivY)
      {
         qsDrawPanel();
      }
      qsDrawTab();
   };

   var c = p.content;
   qsLightText(c.details.label,0xFFE08A);
   qsLightText(c.details.detail,0xFFFFFF);
   qsLightText(c.objectiveLabel,0xFFE08A);
   var sock = c.details.createEmptyMovieClip("qsSocket",-16384);
   sock.lineStyle(2,0xFFE08A,100);
   sock.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:-8,y:-10,w:84,h:84,r:0});
   qsCircle(sock,32,32,36);
   sock.endFill();
   sock.filters = [new flash.filters.GlowFilter(QS_GOLD,0.45,12,12,2,2)];

   qsDrawChips(c.rewards);
}

// The coin and experience rewards (200x60 clips whose class isn't a MovieClip, with no plate in game): glass chips
// with a gold rim, drawn in the reward view under them wherever its resized() puts them.
function qsDrawChips(view)
{
   var g = view.qsChips;
   if(g == undefined)
   {
      g = view.qsChips = view.createEmptyMovieClip("qsChips",-16384);
      g.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
   }
   g.clear();
   var list = [view.RewardCoins,view.RewardExp];
   var i;
   for(i = 0; i < list.length; i++)
   {
      var chip = list[i];
      if(chip == undefined || !chip._visible)
      {
         continue;
      }
      chip.background._alpha = 0;
      qsLightText(chip.label,0xFFFFFF);
      qsLightText(chip.x,0xFFE08A);
      qsGradientRect(g,chip._x + 2,chip._y + 6,196,48,24,0x284A9E,0x162660,100,0xFFE08A,70,1.5);
      qsGloss(g,chip._x + 10,chip._y + 7,180,18,9,18);
   }
}

// --- the answers -----------------------------------------------------------------------

function qsDrawTray()
{
   var g = Response.qsTray;
   var w = Response.top._width;
   var h = Response.bottom._y + Response.bottom._height - QS_TRAY_TAIL;
   g.qsW = w;
   g.qsH = h;
   g.clear();
   // The tail points up at the bubble.
   g.lineStyle(3,QS_RIM,80);
   g.beginFill(0x2A4AA2,100);
   g.moveTo(w / 2 - 18,6);
   g.lineTo(w / 2,-14);
   g.lineTo(w / 2 + 18,6);
   g.endFill();
   qsGradientRect(g,2,2,w - 4,h - 4,22,0x2A4AA2,0x162660,100,QS_RIM,80,3);
   qsGloss(g,10,4,w - 20,22,11,14);
   g.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,16,16,1,2)];
}

function qsDrawAnswer(b)
{
   var g = b.qsPill;
   var w = Math.max(160,Math.round(b.label._x + b.label._width + 6));
   var h = 44;
   var over = b.qsOver;
   var gold = b.qsGold;
   g.clear();
   if(gold)
   {
      g.lineStyle(1.5,0xFFF3C4,100);
      g.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:0,y:0,w:w,h:h,r:Math.PI / 2});
      qsRoundRect(g,0,0,w,h,h / 2);
      g.endFill();
      qsGloss(g,10,2,w - 20,18,9,50);
   }
   else
   {
      qsGradientRect(g,0,0,w,h,h / 2,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : QS_RIM,over ? 100 : 70,over ? 2 : 1.5);
      qsGloss(g,10,2,w - 20,18,9,22);
   }
   // The answer's icon sits in a socket at its left end.
   g.lineStyle(1.5,gold ? 0xFFF3C4 : QS_RIM,80);
   g.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:b.icon._x - 24,y:b.icon._y - 26,w:44,h:44,r:0});
   qsCircle(g,b.icon._x,b.icon._y,18);
   g.endFill();
   b.qsPillHolder.filters = over ? [new flash.filters.GlowFilter(QS_GOLD,0.75,16,16,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
}

function qsStyleAnswer(b, index)
{
   if(b == undefined || b.qsPill != undefined)
   {
      return;
   }
   b.UP._alpha = b.HOVER._alpha = b.DOWN._alpha = b.DISABLED._alpha = 0;
   b.qsGold = index == 0;
   b.qsPillHolder = b.createEmptyMovieClip("qsPillHolder",-16384);
   b.qsPill = b.qsPillHolder.createEmptyMovieClip("pill",1);
   qsLightText(b.label,b.qsGold ? QS_INK : 0xFFFFFF,!b.qsGold);
   var lo = new Object();
   lo.button = b;
   lo.state = function(type, params)
   {
      var btn = this.button;
      if(type == "rollover" || type == "dragover")
      {
         btn.qsOver = true;
      }
      else if(type == "rollout" || type == "dragout" || type == "releaseoutside")
      {
         btn.qsOver = false;
      }
      else
      {
         return;
      }
      qsDrawAnswer(btn);
      gs.TweenLite.to(btn.qsPillHolder,btn.qsOver ? 0.5 : 0.25,{_y:btn.qsOver ? -2 : 0,ease:qsStrongOut});
   };
   b.addListener(lo);
   qsDrawAnswer(b);
}

function qsStyleTray()
{
   Response.top._alpha = Response.middle._alpha = Response.bottom._alpha = Response.fill._alpha = 0;
   Response.Badge._visible = false;
   Response.qsTray = Response.createEmptyMovieClip("qsTray",-16384);
   qsDrawTray();

   var proto = _global.QuestStartDialogResponse.prototype;
   var addResponse = proto.addResponse;
   proto.addResponse = function()
   {
      var r = addResponse.apply(this,arguments);
      var list = _global.QuestStartDialogResponse.Responses;
      qsStyleAnswer(list[list.length - 1],list.length - 1);
      return r;
   };
   var resize = proto.resize;
   proto.resize = function()
   {
      var r = resize.apply(this,arguments);
      this.Badge._visible = false;
      if(this.qsTray != undefined && (this.top._width != this.qsTray.qsW || this.bottom._y + this.bottom._height - QS_TRAY_TAIL != this.qsTray.qsH))
      {
         qsDrawTray();
      }
      return r;
   };
}

// --- stock text and rewards --------------------------------------------------------------

function qsPatch()
{
   var proto = _global.QuestStartDialog.prototype;
   var setText = proto.setText;
   proto.setText = function()
   {
      var r = setText.apply(this,arguments);
      qsLightText(this.NPCText,0xFFFFFF);
      return r;
   };
   var onMinimize = proto.onMinimize;
   proto.onMinimize = function()
   {
      var r = onMinimize.apply(this,arguments);
      qsLightText(this.NPCText,0xFFFFFF);
      return r;
   };
   var onMaximize = proto.onMaximize;
   proto.onMaximize = function()
   {
      var r = onMaximize.apply(this,arguments);
      qsLightText(this.NPCText,0xFFFFFF);
      return r;
   };

   var viewProto = _global.QuestStartRewardView.prototype;
   var resized = viewProto.resized;
   viewProto.resized = function()
   {
      var r = resized.apply(this,arguments);
      qsDrawChips(this);
      return r;
   };

   // Reward group labels ("Choose one", "You get") on a navy chip instead of their icon plate.
   var labelProto = _global.QuestRewardGroupLabel.prototype;
   var labelGraphics = labelProto.loadGraphics;
   labelProto.loadGraphics = function()
   {
      var r = labelGraphics.apply(this,arguments);
      this.background._alpha = 0;
      return r;
   };
   var labelText = labelProto.setText;
   labelProto.setText = function()
   {
      var r = labelText.apply(this,arguments);
      this.background._alpha = 0;
      qsLightText(this.label,0xFFE08A);
      var g = this.qsChip;
      if(g == undefined)
      {
         g = this.qsChip = this.createEmptyMovieClip("qsChip",-16384);
      }
      g.clear();
      qsPanel(g,0,3,Math.round(this.label._x + this.label.textWidth + 16),26,13,50,30);
      return r;
   };
}

// Membership no longer exists: no answer is members only (the stock greys the first one out for non-members).
MembersOnly = function(value, isAMember)
{
   Response.setMembersOnly(0,1);
   Response.Badge._visible = false;
};

qsPatch();
qsStyleBubble();
qsStyleTray();
// ===== End Sanctuary quest dialog =====

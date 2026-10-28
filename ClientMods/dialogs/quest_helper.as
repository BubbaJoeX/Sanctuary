// ===== Sanctuary quest tracker =====
// Appended to quest_helper's main timeline by build_client_mods.py.
// Redraws the quest tracker on the HUD (wndQuestHelper) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as):
// the purple panel becomes a navy glass card that grows with the objectives, the quest's icon sits in a frosted
// medallion on its top edge, the title is gold and the objectives white (soft blue once done). Take Me There is a navy
// pill (gold rimmed on hover), the round buttons (minimize, Hero's Journal) are navy glass discs that turn gold on
// hover, and the minimized tracker is a gold-rimmed medallion with a navy name tag. Every stock clip keeps its role:
// the panel art goes invisible (still the hover area that shows the prev/next arrows) and the new art is drawn into
// free depths between the stock buttons' own layers, so their icons stay on top and their clicks are untouched.

var QH_W = 182;
var QH_GOLD = 0xFFC531;
var QH_RIM = 0x9DB8FF;
var QH_SOFT = 0xC9D6FF;
// Free depths inside the stock buttons: a circleButton has its rim shape at 1, state art at 2-6 and icons at 8 and
// 10; a rectButton's state clips have their plate at 1 and icon and label at 3 and 4.
var QH_DISC_DEPTH = 7 - 16384;
var QH_PILL_DEPTH = 2 - 16384;

// --- drawing helpers -------------------------------------------------------------------

function qhRoundRect(mc, x, y, w, h, r)
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

function qhGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   qhRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function qhGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   qhRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function qhCircle(mc, x, y, r)
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

// A frosted socket: the stock icons are made for light plates.
function qhSocket(mc, x, y, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.3,y:y - r * 1.5,w:r * 2.6,h:r * 2.6,r:0});
   qhCircle(mc,x,y,r);
   mc.endFill();
}

function qhText(mc, name, txt, x, y, w, h, size, color, align)
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

function qhOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Recolours a stock field (all of its text and what it's given next) and outlines it.
function qhLightText(t, color)
{
   if(t == undefined)
   {
      return;
   }
   t.textColor = color;
   var f = t.getNewTextFormat();
   f.color = color;
   t.setNewTextFormat(f);
   t.filters = qhOutline();
}

// --- the panel -------------------------------------------------------------------------

function qhDrawCard()
{
   var c = helperPanel_mc.qhCard;
   var h = Math.max(120,helperPanel_mc.topPiece_mc.back_mc._height);
   c.clear();
   qhGradientRect(c,2,14,QH_W - 4,h - 16,22,0x2E4FB0,0x141F55,92,QH_RIM,80,2.5);
   c.lineStyle();
   c.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-80,y:-110,w:260,h:260,r:0});
   qhRoundRect(c,4,16,QH_W - 8,h - 20,20);
   c.endFill();
   // The title's recess and a gold hairline under it.
   c.lineStyle(1,QH_RIM,22);
   c.beginFill(0x0A1440,35);
   qhRoundRect(c,8,58,QH_W - 16,Math.max(24,helperPanel_mc.questDesc._height + 10),12);
   c.endFill();
   var ly = helperPanel_mc.objectivesList._y - 6;
   if(helperPanel_mc.takeMeButton_mc._visible)
   {
      ly = helperPanel_mc.takeMeButton_mc._y - 5;
   }
   c.lineStyle();
   c.beginGradientFill("linear",[QH_GOLD,QH_GOLD,QH_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:14,y:0,w:QH_W - 28,h:2,r:0});
   qhRoundRect(c,14,ly,QH_W - 28,2,1);
   c.endFill();
   c.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,12,12,1,2)];
}

function qhBuildPanel()
{
   var p = helperPanel_mc;
   p.topPiece_mc.back_mc._alpha = 0;
   p.qhCard = p.createEmptyMovieClip("qhCard",-16384);

   // The quest's icon in a frosted medallion on the card's top edge.
   var b = p.questIcon.getBounds(p);
   var m = p.createEmptyMovieClip("qhMedallion",-16383);
   var cx = b.xMax > b.xMin ? (b.xMin + b.xMax) / 2 : 30;
   var cy = b.yMax > b.yMin ? (b.yMin + b.yMax) / 2 : 24;
   m.lineStyle(3,0x141F55,100);
   m.beginFill(0x203A8A,100);
   qhCircle(m,cx,cy,27);
   m.endFill();
   qhSocket(m,cx,cy,23,0xFFE08A,100,2);
   m.filters = [new flash.filters.GlowFilter(QH_GOLD,0.45,10,10,2,2)];
   var label = qhText(p,"qhTitle_txt","Tracking",cx + 30,22,90,22,14,0xFFE08A,"left");
   label.filters = qhOutline();

   qhLightText(p.questDesc.title,0xFFE08A);
   qhLightText(p.questDesc.desc,0xFFFFFF);
   qhStyleTakeMe(p.takeMeButton_mc);
   qhStyleDisc(p.centerer_mc.minimize_btn);
   qhStyleDisc(p.showQuestJournal_btn);
   qhDrawCard();
}

// --- buttons ---------------------------------------------------------------------------

// Take Me There: a pill drawn in each of its state clips over the stock plate, under its icon and label.
function qhStyleTakeMe(btn)
{
   var states = [btn.up_mc,btn.over_mc,btn.down_mc];
   var i;
   for(i = 0; i < states.length; i++)
   {
      var s = states[i];
      if(s == undefined)
      {
         continue;
      }
      var b = s.getBounds(s);
      var w = b.xMax > b.xMin ? Math.round(b.xMax) : 154;
      var g = s.createEmptyMovieClip("qhPill",QH_PILL_DEPTH);
      var over = i > 0;
      qhGradientRect(g,0,2,w,28,14,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : QH_RIM,over ? 100 : 75,over ? 2 : 1.5);
      qhGloss(g,8,3,w - 16,12,6,over ? 30 : 22);
      g.filters = over ? [new flash.filters.GlowFilter(QH_GOLD,0.6,10,10,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.5,6,6,1,2)];
   }
}

// The round buttons: a navy glass disc over the stock rim and state art (under the icon), gold rimmed on hover.
function qhDrawDisc(btn, over)
{
   var g = btn.qhDisc;
   g.clear();
   g.lineStyle(over ? 2.5 : 2,over ? 0xFFE08A : QH_RIM,over ? 100 : 85);
   g.beginGradientFill("linear",over ? [0x3F66D6,0x1D307A] : [0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:0,y:0,w:42,h:42,r:Math.PI / 2});
   qhCircle(g,21,21.5,20);
   g.endFill();
   qhGloss(g,9,4,24,13,6,over ? 40 : 30);
   g.filters = over ? [new flash.filters.GlowFilter(QH_GOLD,0.75,12,12,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.55,5,5,1,2)];
}

function qhStyleDisc(btn)
{
   if(btn == undefined)
   {
      return;
   }
   btn.qhDisc = btn.createEmptyMovieClip("qhDisc",QH_DISC_DEPTH);
   qhDrawDisc(btn,false);
   var lo = new Object();
   lo.onHoverOver = function(target)
   {
      qhDrawDisc(btn,true);
   };
   lo.onHoverOut = function(target)
   {
      qhDrawDisc(btn,false);
   };
   lo.onButtonRelease = function(target)
   {
      qhDrawDisc(btn,btn.hitTest(_root._xmouse,_root._ymouse,true));
   };
   btn.addListener(lo);
}

// --- minimized -------------------------------------------------------------------------

function qhBuildMinimized()
{
   var m = minPanel_mc;
   m.bg.holder1._alpha = 0;
   m.bg.holder2._alpha = 0;
   var g = m.createEmptyMovieClip("qhMedallion",-16384);
   g.lineStyle(3,0xFFE08A,100);
   g.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:-32,y:-32,w:64,h:64,r:Math.PI / 2});
   qhCircle(g,0,0,31);
   g.endFill();
   qhSocket(g,0,0,25,0xFFF3C4,90,1.5);
   g.filters = [new flash.filters.GlowFilter(QH_GOLD,0.55,12,12,2,2),new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
   m.nametag.nametag_BG._alpha = 0;
   m.nametag.qhTag = m.nametag.createEmptyMovieClip("qhTag",-16384);
   qhLightText(m.nametag.questName,0xFFFFFF);
   qhDrawTag();
}

// The quest's name beside the minimized medallion, on a navy pill where setQuestName puts the stock plate.
function qhDrawTag()
{
   var n = minPanel_mc.nametag;
   var g = n.qhTag;
   var x = n.nametag_BG._x;
   var w = n.nametag_BG._width;
   g.clear();
   qhGradientRect(g,x + 8,-17,w - 4,34,17,0x284A9E,0x162660,100,0xFFE08A,90,1.5);
   qhGloss(g,x + 16,-16,w - 20,14,7,22);
   g.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
}

// --- Lua -------------------------------------------------------------------------------

var qhSetWindowHeight = setWindowHeight;
setWindowHeight = function()
{
   var r = qhSetWindowHeight.apply(_root,arguments);
   qhDrawCard();
   return r;
};

var qhAddObjective = addObjective;
addObjective = function(questid, objectiveID, sName, iStatus)
{
   var r = qhAddObjective.apply(_root,arguments);
   qhStyleObjective(helperPanel_mc.objectivesList["objective_" + objectiveID]);
   return r;
};

var qhUpdateObjective = updateObjective;
updateObjective = function(questID, objectiveID, status, statusText)
{
   var r = qhUpdateObjective.apply(_root,arguments);
   qhStyleObjective(helperPanel_mc.objectivesList["objective_" + objectiveID]);
   return r;
};

// Objectives in white, done ones (status 3) in soft blue.
function qhStyleObjective(o)
{
   if(o != undefined)
   {
      qhLightText(o.name,o.statusId == 3 ? QH_SOFT : 0xFFFFFF);
   }
}

var qhSetQuestName = setQuestName;
setQuestName = function(sName)
{
   var r = qhSetQuestName.apply(_root,arguments);
   qhDrawTag();
   return r;
};

qhBuildPanel();
qhBuildMinimized();
// ===== End Sanctuary quest tracker =====

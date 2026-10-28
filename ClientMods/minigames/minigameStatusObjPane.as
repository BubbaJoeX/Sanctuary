// ===== Sanctuary Minigame Objectives Pane =====
// Appended to minigameStatusObjPane's main timeline (after minigames/mg_theme.as) by build_client_mods.py.
// The objectives pane of the minigame launch screen (wndMinigameStatusObjPane) in the Main Menu's style: a navy
// glass frame with the title in gold on a sparkling recessed strip, the objectives as glass rows (status icon in a
// frosted socket, a lighter row with a gold rim on hover) in a recess, a navy glass pager with gold arrows, and the
// Play / Cancel / Exit buttons as gold, navy glass or red pills that follow the stock buttons' colour, width tweens,
// hover and enabled state. The stock button and pager art stays as the invisible hit layer, so every click path is
// the stock one. Members-only no longer exists: no Members Only marks on objectives and no upgrade badge, and the
// members-only launch mode (displayMode 4, which hid Play behind the upgrade badge) opens as a normal launch.

var MSO_FRAME = {x:10,y:2,w:750,h:312};
var MSO_TITLE = {x:97,y:26,w:572,h:42};
var MSO_LIST = {x:72,y:58,w:680,h:164};

function msoBuild()
{
   var f = mgUnder(_root,"sanctuaryObjPane");
   var bg = f.createEmptyMovieClip("bg",1);
   mgFrame(bg,MSO_FRAME.x,MSO_FRAME.y,MSO_FRAME.w,MSO_FRAME.h,28,0,7272);
   mgSparkles(bg,7373,MSO_FRAME.x + 30,MSO_FRAME.y + 8,MSO_FRAME.w - 60,50,24);
   var t = MSO_TITLE;
   mgPanel(bg,t.x,t.y,t.w,t.h,t.h / 2,45,30);
   bg.lineStyle();
   bg.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:t.x + 40,y:0,w:t.w - 80,h:2,r:0});
   mgRoundRect(bg,t.x + 40,t.y + t.h - 3,t.w - 80,2,1);
   bg.endFill();
   var l = MSO_LIST;
   mgPanel(bg,l.x,l.y,l.w,l.h,20,30,20);

   mgHold(bg_holder);
   mgLight(objectivesHeader,MG_GOLD_LIGHT);
   upgrade._visible = false;

   msoPager(pageController);
   msoButton(actionbtn);
   msoButton(quitbtn);
}

// --- objective rows (fr.extras.ObjectiveButton, attached by addObjective2) ----------------

function msoStyleRow(row)
{
   mgHold(row.HOVER);
   mgHold(row.divider);
   mgHold(row.membericon);
   row.label.filters = mgOutline();
   row.msoBg = mgUnder(row,"msoBg");
   msoDrawRow(row);
}

function msoDrawRow(row)
{
   var g = row.msoBg;
   if(g == undefined)
   {
      return;
   }
   // The stock hover highlight still toggles (invisibly); it says whether the row is hovered.
   var over = row.HOVER._visible == true;
   if(over == row.msoOver)
   {
      return;
   }
   row.msoOver = over;
   g.clear();
   // Kept inside the stock row's bounds (-18..665), which the pager steps by.
   mgGradientRect(g,6,1,656,32,16,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,over ? 100 : 80,over ? MG_GOLD_LIGHT : MG_RIM,over ? 100 : 30,1.5);
   mgGloss(g,16,2,636,12,6,over ? 30 : 18);
   mgSocketCircle(g,30,17,14,over ? MG_GOLD : MG_RIM,over ? 100 : 70,1.5);
   g.filters = over ? [new flash.filters.GlowFilter(MG_GOLD,0.6,12,12,2,2)] : [];
}

var msoRowProto = _global.fr.extras.ObjectiveButton.prototype;
var msoRowLoadGraphics = msoRowProto.loadGraphics;
msoRowProto.loadGraphics = function()
{
   var result = msoRowLoadGraphics.apply(this,arguments);
   msoStyleRow(this);
   return result;
};
var msoRowState = msoRowProto.state;
msoRowProto.state = function(type, data)
{
   var result = msoRowState.apply(this,arguments);
   msoDrawRow(this);
   return result;
};

// --- pager (fr.display.PageController) -------------------------------------------------

// A navy glass pill with gold arrow discs and a recessed page count, drawn over the stock pager's backing and
// arrows (which stay underneath as the hit areas) and under its label.
function msoPager(pc)
{
   if(pc == undefined)
   {
      return;
   }
   pc.label.filters = mgOutline();
   var a = mgBelow(pc.label,"msoPagerArt");
   a.pc = pc;
   a.onEnterFrame = msoPagerTick;
}

function msoPagerTick()
{
   var pc = this.pc;
   var p = pc.previous;
   var n = pc.next;
   var key = (p.enabled != false) + "," + (p.HOVER._visible || p.DOWN._visible) + "," + (n.enabled != false) + "," + (n.HOVER._visible || n.DOWN._visible);
   if(key == this.key)
   {
      return;
   }
   this.key = key;
   var g = this;
   g.clear();
   mgGradientRect(g,0,1,178,43,21.5,0x284A9E,0x162660,100,MG_RIM,60,1.5);
   mgGloss(g,10,2,158,16,8,25);
   mgPanel(g,52,7,74,30,15,70,25);
   msoPagerArrow(g,22,22.5,-1,p.enabled != false,p.HOVER._visible || p.DOWN._visible);
   msoPagerArrow(g,156,22.5,1,n.enabled != false,n.HOVER._visible || n.DOWN._visible);
   g.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.55,8,8,1,2)];
}

function msoPagerArrow(g, x, y, dir, enabled, over)
{
   var r = over && enabled ? 17 : 15;
   if(enabled)
   {
      g.lineStyle(1.5,MG_GOLD_RIM,100);
      g.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [MG_GOLD_LIGHT,MG_GOLD_DEEP],[100,100],[0,255],{matrixType:"box",x:x - r,y:y - r,w:r * 2,h:r * 2,r:Math.PI / 2});
   }
   else
   {
      g.lineStyle(1.5,MG_RIM,40);
      g.beginFill(0x3A4F8A,60);
   }
   mgCircle(g,x,y,r);
   g.endFill();
   g.lineStyle(3.5,enabled ? MG_INK : MG_SOFT,enabled ? 100 : 50);
   g.moveTo(x - dir * 3,y - 6);
   g.lineTo(x + dir * 4,y);
   g.lineTo(x - dir * 3,y + 6);
}

// --- buttons (fr.buttons.FRButtonScalable) ---------------------------------------------

// A pill drawn inside the button under its icon and label, in the colour the stock button shows (green: gold,
// beige: navy glass, red: red), following its width (setWidth/tweenWidth) and hover; the stock plates stay invisible.
function msoButton(b)
{
   if(b == undefined)
   {
      return;
   }
   mgHold(b.UP);
   mgHold(b.HOVER);
   mgHold(b.DOWN);
   mgLight(b.label,0xFFFFFF,false);
   var a = mgUnder(b,"msoArt");
   a.btn = b;
   a.onEnterFrame = msoButtonTick;
}

function msoButtonTick()
{
   var b = this.btn;
   var kind = b.UP.RED._visible ? "red" : (b.UP.GREEN._visible ? "gold" : "navy");
   var over = b.HOVER._visible || b.DOWN._visible;
   var enabled = b.enabled != false;
   var x = Math.round(b.UP._x);
   var w = Math.round(b.UP._width);
   var h = Math.round(b.UP._height);
   var key = kind + over + enabled + x + "," + w + "," + h;
   if(key == this.key)
   {
      return;
   }
   this.key = key;
   var g = this;
   g.clear();
   var px = x + 4;
   var pw = Math.max(20,w - 8);
   var ph = Math.max(20,h - 16);
   var py = (h - ph) / 2;
   var hover = over && enabled;
   if(kind == "gold")
   {
      mgGradientRect(g,px,py,pw,ph,ph / 2,hover ? 0xFFF0B0 : MG_GOLD_LIGHT,hover ? 0xFFB838 : MG_GOLD_DEEP,100,MG_GOLD_RIM,100,2);
   }
   else if(kind == "red")
   {
      mgGradientRect(g,px,py,pw,ph,ph / 2,hover ? 0xFF8A80 : 0xF0605A,hover ? 0xD03A3A : 0xB52A2A,100,0xFFD0C8,100,2);
   }
   else
   {
      mgGradientRect(g,px,py,pw,ph,ph / 2,hover ? 0x3F66D6 : 0x284A9E,hover ? 0x1D307A : 0x162660,100,hover ? MG_GOLD_LIGHT : MG_RIM,hover ? 100 : 70,2);
   }
   mgGloss(g,px + ph * 0.3,py + 2,pw - ph * 0.6,ph * 0.42,ph * 0.2,kind == "navy" ? 25 : 45);
   g.filters = hover ? mgGoldGlow() : [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.55,8,8,1,2)];
   g._alpha = enabled ? 100 : 55;
   // Dark lettering on gold, white with the outline on the rest.
   b.label.mgColor = kind == "gold" ? MG_INK : 0xFFFFFF;
   b.label.textColor = b.label.mgColor;
   b.label.filters = kind == "gold" ? [] : mgOutline();
}

// --- Lua -------------------------------------------------------------------------------

// Members-only launches (mode 4: the upgrade badge instead of Play) open as normal launches (mode 1).
var msoDisplayMode = displayMode;
displayMode = function(mode)
{
   if(mode == 4)
   {
      mode = 1;
   }
   var result = msoDisplayMode.call(_root,mode);
   upgrade._visible = false;
   return result;
};

// No objective is marked members-only (the last argument).
var msoSetObjectiveData = setMiniGameObjectiveData;
setMiniGameObjectiveData = function(id, sObjName, iIsBonus, sDesc, iRewardStars, sProgress, sStatus, bMembersOnly)
{
   return msoSetObjectiveData.call(_root,id,sObjName,iIsBonus,sDesc,iRewardStars,sProgress,sStatus,false);
};

msoBuild();
// ===== End Sanctuary Minigame Objectives Pane =====

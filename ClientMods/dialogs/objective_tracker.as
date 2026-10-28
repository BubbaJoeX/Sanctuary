// ===== Sanctuary objectives =====
// Appended to objectiveHelper's main timeline by build_client_mods.py.
// Redraws the objectives list on the HUD (wndObjectives, and wndMiniGameTasks in minigames) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass card that grows with its list, a gold "Objectives (3)"
// title, and each objective a glass row with its status icon and white text; the one the game points you at is
// rimmed in gold. Membership no longer exists, so no objective is padded, dimmed or badged as members only. The
// stock frame goes invisible (the title is drawn anew from the count, so it doesn't depend on the string table)
// and every row keeps its stock hit area, glare animation and selection.

var OB_W = 185;
var OB_GOLD = 0xFFC531;
var OB_RIM = 0x9DB8FF;
var OB_SOFT = 0xC9D6FF;
// A row's plate (selectedBG) is 150 wide from x 0 and starts at y -12.5; itemHolder sits at 7,48.
var OB_ROW_W = 150;
var OB_ROW_Y = -12.5;

var obFrame;

// --- drawing helpers -------------------------------------------------------------------

function obRoundRect(mc, x, y, w, h, r)
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

function obGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   obRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function obGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   obRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function obText(mc, name, txt, x, y, w, h, size, color, align)
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

function obOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Recolours a stock field (html included, and what it's given next) and outlines it.
function obLightText(t, color)
{
   if(t == undefined)
   {
      return;
   }
   t.textColor = color;
   var f = t.getNewTextFormat();
   f.color = color;
   t.setNewTextFormat(f);
   t.filters = obOutline();
}

// --- frame -----------------------------------------------------------------------------

function obDrawFrame()
{
   var bg = obFrame.bg;
   var h = Math.max(70,frameBottom._y + 18);
   bg.clear();
   obGradientRect(bg,2,2,OB_W - 4,h - 4,18,0x2E4FB0,0x141F55,92,OB_RIM,80,2.5);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-90,y:-120,w:260,h:240,r:0});
   obRoundRect(bg,4,4,OB_W - 8,h - 8,16);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   obRoundRect(bg,5,5,OB_W - 10,28,14);
   bg.endFill();
   bg.beginGradientFill("linear",[OB_GOLD,OB_GOLD,OB_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:16,y:0,w:OB_W - 32,h:2,r:0});
   obRoundRect(bg,16,35,OB_W - 32,2,1);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,12,12,1,2)];
}

function obBuildFrame()
{
   frameTop._alpha = 0;
   frameBottom._alpha = 0;
   frameBG._alpha = 0;
   obFrame = _root.createEmptyMovieClip("sanctuaryObjectives",-16384);
   obFrame.bg = obFrame.createEmptyMovieClip("bg",1);
   var t = obFrame.title_txt = obText(obFrame,"title_txt","Objectives",6,8,OB_W - 12,24,16,0xFFE08A);
   t.filters = obOutline();
   obDrawFrame();
}

function obSetCount(n)
{
   obFrame.title_txt.text = n > 0 ? "Objectives (" + n + ")" : "Objectives";
}

// --- rows ------------------------------------------------------------------------------

function obDrawRow(row)
{
   var g = row.obCard;
   var h = row.selectedBG._height;
   if(!(h > 0))
   {
      h = 26;
   }
   var sel = row.selectedBG._visible;
   row.obH = h;
   row.obSel = sel;
   g.clear();
   obGradientRect(g,0,OB_ROW_Y,OB_ROW_W,h,10,sel ? 0x3352AE : 0x284A9E,sel ? 0x1B2E74 : 0x162660,sel ? 100 : 85,sel ? OB_GOLD : OB_RIM,sel ? 100 : 40,sel ? 2 : 1);
   if(sel)
   {
      g.lineStyle();
      g.beginGradientFill("linear",[OB_GOLD,OB_GOLD],[40,0],[0,255],{matrixType:"box",x:0,y:0,w:OB_ROW_W,h:10,r:0});
      obRoundRect(g,1,OB_ROW_Y + 1,OB_ROW_W - 2,h - 2,9);
      g.endFill();
   }
   obGloss(g,4,OB_ROW_Y + 1,OB_ROW_W - 8,Math.min(12,h / 2),6,sel ? 22 : 14);
   g.filters = sel ? [new flash.filters.GlowFilter(OB_GOLD,0.45,8,8,2,2)] : [];
}

function obStyleRow(row)
{
   if(row == undefined)
   {
      return;
   }
   if(row.obCard == undefined)
   {
      // Under the hit area (depth 1); the stock plate, separator and members-only badge go.
      row.obCard = row.createEmptyMovieClip("obCard",-16384);
      row.selectedBG._alpha = 0;
      row.line._alpha = 0;
      row.memberBadge._visible = false;
      obLightText(row.description.text,0xFFFFFF);
   }
   obDrawRow(row);
}

function obPatchRows()
{
   var proto = _global.ObjectiveListRow.prototype;
   var select = proto.select;
   proto.select = function()
   {
      var r = select.apply(this,arguments);
      if(this.obCard != undefined)
      {
         obDrawRow(this);
      }
      return r;
   };
   // Membership no longer exists: nothing is members only (the stock pads the text for a badge and dims it).
   proto.addProperty("membersOnly",function()
   {
      return false;
   },function(value)
   {
      this.gotMembersOnly = true;
      this.myMembersOnly = false;
      this.memberBadge._visible = false;
   });
}

// --- Lua -------------------------------------------------------------------------------

// A row has its height once objectiveDoneLoading has run (also after its text changes); the frame follows the list.
var obObjectiveDoneLoading = objectiveDoneLoading;
objectiveDoneLoading = function(obj, existingObjective)
{
   var r = obObjectiveDoneLoading.apply(_root,arguments);
   obStyleRow(obj);
   for(var name in item_display_container)
   {
      var row = item_display_container[name];
      if(row.obCard != undefined && (row.selectedBG._height != row.obH || row.selectedBG._visible != row.obSel))
      {
         obDrawRow(row);
      }
   }
   return r;
};

var obUpdateScrollBar = updateScrollBar;
updateScrollBar = function()
{
   var r = obUpdateScrollBar.apply(_root,arguments);
   obDrawFrame();
   return r;
};

var obSetTitle = setTitle;
setTitle = function(_text)
{
   var r = obSetTitle.apply(_root,arguments);
   obSetCount(numObjectives);
   return r;
};

var obResetData = resetData;
resetData = function()
{
   var r = obResetData.apply(_root,arguments);
   obSetCount(0);
   return r;
};

obPatchRows();
obBuildFrame();
// ===== End Sanctuary objectives =====

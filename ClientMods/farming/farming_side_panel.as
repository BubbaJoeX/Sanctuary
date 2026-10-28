// ===== Sanctuary Farming Side Panel =====
// Appended to FarmingSidePanel's main timeline by build_client_mods.py.
// The farm's side panel (wndFarmingSidePanel, on the right of the HUD while you're on a farm) in the Main Menu's
// style (ClientMods/MyStuffBrowser/main_menu.as), as a HUD card without the window shell: navy glass with the
// farm's house in a gold-ringed frosted socket, the farm's name in gold over its owner's in soft blue, and a
// gold minimize circle. Below, glossy rounded bars: the farm level on a gold medallion with a gold progress bar
// (MAX at the top level), and the items placed against the limit in green (amber near the limit, red when
// full), the item crate in a frosted socket. The tool in hand sits on a glass card under a gold rule, its icon
// in a frosted socket with its charges on a gold chip ("No tool in hand" otherwise). Minimized, the panel is a
// navy glass disc with a gold rim around the house that glows on hover, its name tag a navy pill. Membership
// no longer exists, so the Become a Member button never shows. The stock clips keep their roles (hit areas, the
// minimize tweens, Lua's text); their art is hidden. The crate is a client texture (TEXTURE_SYMBOLS).

var FSP_W = 182;
var FSP_TOP = 24;
var FSP_BOTTOM = 326;
var FSP_GOLD = 0xFFC531;
var FSP_RIM = 0x9DB8FF;
var FSP_SOFT = 0xC9D6FF;
var FSP_INK = 0x3A2400;
// The header's house socket, and where the names start.
var FSP_HOUSE_X = 36;
var FSP_HOUSE_Y = 60;
var FSP_HOUSE_R = 25;
var FSP_HOUSE_SCALE = 78;
var FSP_NAME_X = 68;
var FSP_NAME_W = 82;
// The level and items rows: their top, and the bars' left edge and width.
var FSP_LEVEL_Y = 106;
var FSP_ITEMS_Y = 158;
var FSP_BAR_X = 56;
var FSP_BAR_W = 114;
// The tool card. A stock divider line (an unnamed shape in sidePanel_mc) runs at y 244.5, so the card's gold
// lip is drawn over it there.
var FSP_TOOL_Y = 244.5;
var FSP_TOOL_H = 74;
// Bar colours: light, mid and dark.
var FSP_BAR_GOLD = [0xFFF1B8,0xFFC531,0xD98A10];
var FSP_BAR_GREEN = [0xC4F59A,0x5DBE35,0x2E7A16];
var FSP_BAR_AMBER = [0xFFE3A3,0xF5A623,0xB86A0C];
var FSP_BAR_RED = [0xFFA394,0xEC4A36,0xA8261A];

var fspPanel = sidePanel_mc;
var fspFrame;
var fspLevel;
var fspItems;
var fspTool;
var fspDisc;
var fspFarmLevel;
var fspProgress = 0;
var fspCur;
var fspMax;

// --- drawing helpers -------------------------------------------------------------------

function fspRoundRect(mc, x, y, w, h, r)
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

function fspGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   fspRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function fspGoldRect(mc, x, y, w, h, r, over)
{
   mc.lineStyle(over ? 2 : 1.5,0xFFF3C4,over ? 100 : 90);
   mc.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   fspRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h / 2,r:Math.PI / 2});
   fspRoundRect(mc,x + 3,y + 2,w - 6,h / 2,Math.min(r,h / 4));
   mc.endFill();
}

function fspCircle(mc, x, y, r)
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

// A round frosted socket with a soft shadow under it.
function fspSocket(mc, x, y, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle();
   mc.beginFill(0x050A24,35);
   fspCircle(mc,x,y + 2,r + 1);
   mc.endFill();
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.2,y:y - r * 1.35,w:r * 2.4,h:r * 2.4,r:0});
   fspCircle(mc,x,y,r);
   mc.endFill();
   mc.lineStyle(1,0xFFFFFF,70);
   fspCircle(mc,x,y,r + 2.5);
}

function fspText(mc, name, txt, x, y, w, h, size, color, align)
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

function fspSparkle(mc, x, y, r, alpha)
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

function fspOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function fspGoldRule(mc, x, y, w)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[FSP_GOLD,FSP_GOLD,FSP_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:x,y:y,w:w,h:2,r:0});
   fspRoundRect(mc,x,y,w,2,1);
   mc.endFill();
}

// Moves and restyles a stock text field. frSetTextPreserveWidthV3 shrinks text from the size it first saw,
// so that's set to the new size before Lua fills the field.
function fspPlace(t, x, y, w, h, size, color, align)
{
   t._x = x;
   t._y = y;
   t._width = w;
   t._height = h;
   t.autoSize = "none";
   t.embedFonts = true;
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = align;
   t.setTextFormat(f);
   t.setNewTextFormat(f);
   t.defaultFontSize = size;
   t.textColor = color;
   t.filters = fspOutline();
}

// The stock fields are HTML with <font color> (yellow, white); stock code keeps that colour when it sets text.
function fspColor(t, color)
{
   var f = new TextFormat();
   f.color = color;
   t.setTextFormat(f);
   t.setNewTextFormat(f);
   t.textColor = color;
}

// The dark recessed track a bar runs in.
function fspDrawTrack(mc, x, y, w, h)
{
   var r = h / 2;
   mc.lineStyle(1.5,FSP_RIM,30);
   mc.beginGradientFill("linear",[0x050A24,0x0E1A52],[85,85],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   fspRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   mc.lineStyle();
   mc.beginGradientFill("linear",[0x000000,0x000000],[45,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h * 0.5,r:Math.PI / 2});
   fspRoundRect(mc,x + 1,y + 1,w - 2,h * 0.5,r * 0.5);
   mc.endFill();
}

// A glossy rounded fill: light top, the colour, a darker base, and a white sheen on the upper half.
function fspDrawFill(mc, x, y, w, h, colors)
{
   var r = h / 2;
   mc.lineStyle(1,colors[0],100);
   mc.beginGradientFill("linear",[colors[0],colors[1],colors[2]],[100,100,100],[0,110,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   fspRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[60,5],[0,255],{matrixType:"box",x:x,y:y + 1.5,w:w,h:h * 0.42,r:Math.PI / 2});
   fspRoundRect(mc,x + r * 0.5,y + 1.5,Math.max(w - r,1),h * 0.42,h * 0.2);
   mc.endFill();
}

// A bar of value percent (0-100) on its track; empty draws no fill, small values the round end only.
function fspDrawBar(mc, x, y, w, h, percent, colors)
{
   mc.clear();
   fspDrawTrack(mc,x,y,w,h);
   var v = Math.max(0,Math.min(100,Number(percent)));
   if(isNaN(v) || v <= 0)
   {
      return;
   }
   fspDrawFill(mc,x,y,Math.max(h,Math.round(w * v / 100)),h,colors);
   mc.filters = [new flash.filters.GlowFilter(colors[1],0.4,8,8,1,2)];
}

// --- card ------------------------------------------------------------------------------

function fspDrawCard()
{
   var bg = fspFrame.bg;
   bg.clear();
   fspGradientRect(bg,2,FSP_TOP,FSP_W - 4,FSP_BOTTOM - FSP_TOP,24,0x2E4FB0,0x141F55,100,FSP_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-110,y:FSP_TOP - 170,w:300,h:330,r:0});
   fspRoundRect(bg,4,FSP_TOP + 2,FSP_W - 8,FSP_BOTTOM - FSP_TOP - 4,22);
   bg.endFill();

   // The header band, with a few sparkles (fixed seed) and a gold rule.
   bg.beginFill(0x0A1440,30);
   fspRoundRect(bg,8,FSP_TOP + 6,FSP_W - 16,66,20);
   bg.endFill();
   var s = 5151;
   var i;
   for(i = 0; i < 8; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 70 + s % 90;
      s = (s * 1103515245 + 12345) % 2147483648;
      fspSparkle(bg,x,FSP_TOP + 10 + s % 56,1 + s % 2,15 + s % 30);
   }
   fspGoldRule(bg,22,FSP_TOP + 72,FSP_W - 44);
   fspSocket(bg,FSP_HOUSE_X,FSP_HOUSE_Y,FSP_HOUSE_R,0xFFE08A,100,2.5);

   // The tool card's body (its gold lip is drawn over the stock divider, see fspBuild).
   fspGradientRect(bg,8,FSP_TOOL_Y,FSP_W - 16,FSP_TOOL_H,16,0x284A9E,0x162660,100,FSP_RIM,35,1.5);
   bg.lineStyle();
   bg.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[14,0],[0,255],{matrixType:"box",x:0,y:FSP_TOOL_Y + 3,w:10,h:30,r:Math.PI / 2});
   fspRoundRect(bg,11,FSP_TOOL_Y + 3,FSP_W - 22,30,13);
   bg.endFill();
}

function fspBuildLevel()
{
   fspLevel = fspFrame.createEmptyMovieClip("level",3);
   var m = fspLevel.createEmptyMovieClip("medal",1);
   m.lineStyle(1.5,0xFFF3C4,100);
   m.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:13,y:FSP_LEVEL_Y + 2,w:34,h:34,r:Math.PI / 2});
   fspCircle(m,30,FSP_LEVEL_Y + 19,17);
   m.endFill();
   m.lineStyle();
   m.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:13,y:FSP_LEVEL_Y + 2,w:34,h:17,r:Math.PI / 2});
   fspRoundRect(m,20,FSP_LEVEL_Y + 4,20,12,6);
   m.endFill();
   m.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,6,6,1,2)];
   fspLevel.level_txt = fspText(fspLevel,"level_txt","",13,FSP_LEVEL_Y + 8,34,24,17,FSP_INK);
   fspLevel.label_txt = fspText(fspLevel,"label_txt","Farm Level",FSP_BAR_X - 2,FSP_LEVEL_Y - 1,80,18,12,FSP_SOFT,"left");
   fspLevel.label_txt.filters = fspOutline();
   fspLevel.pct_txt = fspText(fspLevel,"pct_txt","",FSP_BAR_X + FSP_BAR_W - 60,FSP_LEVEL_Y - 1,60,18,12,0xFFFFFF,"right");
   fspLevel.pct_txt.filters = fspOutline();
   fspLevel.bar = fspLevel.createEmptyMovieClip("bar",fspLevel.getNextHighestDepth());
   fspDrawLevel();
}

function fspDrawLevel()
{
   var max = fspAtMaxLevel();
   fspLevel.level_txt.text = fspFarmLevel == undefined ? "" : String(fspFarmLevel);
   fspLevel.pct_txt.text = max ? "MAX" : Math.round(Math.max(0,Math.min(100,fspProgress))) + "%";
   fspLevel.pct_txt.textColor = max ? 0xFFE08A : 0xFFFFFF;
   fspDrawBar(fspLevel.bar,FSP_BAR_X,FSP_LEVEL_Y + 18,FSP_BAR_W,14,max ? 100 : fspProgress,FSP_BAR_GOLD);
   fspLevel._visible = fspPanel.farmLevel_mc._visible;
}

// At the top level the stock hides the progress bar.
function fspAtMaxLevel()
{
   return fspPanel.farmLevel_mc.progressBar_mc._visible == false;
}

function fspBuildItems()
{
   fspItems = fspFrame.createEmptyMovieClip("items",4);
   var s = fspItems.createEmptyMovieClip("socket",1);
   fspSocket(s,30,FSP_ITEMS_Y + 19,16,0xFFFFFF,85,2);
   var crate = fspItems.attachMovie("sanctuaryFarmItems","crate",2);
   crate._width = crate._height = 24;
   crate._x = 18;
   crate._y = FSP_ITEMS_Y + 7;
   fspItems.label_txt = fspText(fspItems,"label_txt","Items",FSP_BAR_X - 2,FSP_ITEMS_Y,60,18,12,FSP_SOFT,"left");
   fspItems.label_txt.filters = fspOutline();
   fspItems.count_txt = fspText(fspItems,"count_txt","",FSP_BAR_X + FSP_BAR_W - 70,FSP_ITEMS_Y,70,18,12,0xFFFFFF,"right");
   fspItems.count_txt.filters = fspOutline();
   fspItems.bar = fspItems.createEmptyMovieClip("bar",fspItems.getNextHighestDepth());
   fspDrawItems();
}

function fspDrawItems()
{
   var cur = Number(fspCur);
   var max = Number(fspMax);
   var pct = 0;
   if(!isNaN(cur) && !isNaN(max) && max > 0)
   {
      pct = cur / max * 100;
   }
   var colors = pct >= 100 ? FSP_BAR_RED : (pct >= 90 ? FSP_BAR_AMBER : FSP_BAR_GREEN);
   fspItems.count_txt.text = fspCur == undefined ? "" : fspCur + " / " + fspMax;
   fspItems.count_txt.textColor = pct >= 100 ? 0xFFA394 : 0xFFFFFF;
   fspDrawBar(fspItems.bar,FSP_BAR_X,FSP_ITEMS_Y + 19,FSP_BAR_W,10,pct,colors);
   fspItems._visible = fspPanel.itemLimit_mc._visible;
}

// The tool in hand: the stock panelInfo_mc (icon, name, charges) on the card, a socket and chip under it.
function fspBuildTool()
{
   var p = fspPanel.panelInfo_mc;
   p._x = 0;
   p._y = FSP_TOOL_Y;
   fspTool = p.createEmptyMovieClip("sanctuaryTool",-16384);
   fspTool.socket = fspTool.createEmptyMovieClip("socket",1);
   fspTool.chip = fspTool.createEmptyMovieClip("chip",2);
   var s = fspTool.socket;
   s.lineStyle();
   s.beginFill(0x050A24,35);
   fspRoundRect(s,15,13,52,52,14);
   s.endFill();
   s.lineStyle(2,0xFFE08A,100);
   s.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:5,y:-2,w:72,h:72,r:0});
   fspRoundRect(s,15,11,52,52,14);
   s.endFill();

   // The stock icon holder draws at about the icon's own size; it shrinks into the socket.
   p.InfoIcon._xscale = p.InfoIcon._yscale = 141;
   p.InfoIcon._x = 18;
   p.InfoIcon._y = 14;
   fspPlace(p.name_txt,74,9,FSP_W - 84,22,15,0xFFE08A,"left");
   fspPlace(p.consumible_txt,74,39,52,18,12,FSP_SOFT,"left");
   fspPlace(p.level_txt,126,37,44,20,14,FSP_INK,"center");
   p.level_txt.filters = [];

   // Shown when nothing is in hand (panelInfo_mc hidden).
   fspFrame.empty_txt = fspText(fspFrame,"empty_txt","No tool in hand",12,FSP_TOOL_Y + 26,FSP_W - 24,22,14,0x8FA3D8);
   fspFrame.empty_txt.filters = fspOutline();
   fspDrawTool();
}

function fspDrawTool()
{
   var p = fspPanel.panelInfo_mc;
   fspColor(p.name_txt,0xFFE08A);
   fspColor(p.consumible_txt,FSP_SOFT);
   fspColor(p.level_txt,FSP_INK);
   var c = fspTool.chip;
   c.clear();
   var n = p.level_txt.text;
   if(n != undefined && n != "" && n != " ")
   {
      fspGoldRect(c,124,36,48,22,11,false);
   }
   fspFrame.empty_txt._visible = !p._visible;
}

// --- minimized disc --------------------------------------------------------------------

function fspDrawDisc(over)
{
   var d = fspDisc;
   d.clear();
   d.lineStyle(over ? 3 : 2.5,over ? 0xFFE08A : FSP_GOLD,100);
   d.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:-31,y:-31,w:62,h:62,r:Math.PI / 2});
   fspCircle(d,0,0,30);
   d.endFill();
   d.lineStyle(1,0xFFFFFF,70);
   d.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:-28,y:-32,w:56,h:56,r:0});
   fspCircle(d,0,0,23);
   d.endFill();
   d.filters = over ? [new flash.filters.GlowFilter(FSP_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(4,90,0x050A24,0.6,10,10,1,2)];
}

// The minimized name tag: a navy pill behind the stock text, sized as setTagName sizes the stock plate.
function fspDrawTag()
{
   var tag = minPanel_mc.nametag;
   var q = tag.questName;
   var p = tag.sanctuaryPill;
   p.clear();
   var x = q._x - 14;
   var w = q._width + 20;
   fspGradientRect(p,x,-14,w,34,17,0x2E4FB0,0x141F55,100,0xFFE08A,90,2);
   p.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.6,10,10,1,2)];
   q.textColor = 0xFFFFFF;
   q.filters = fspOutline();
}

function fspBuildMin()
{
   // The stock frame images (loaded into bg's holders) and the icon under the house go; the house stays.
   minPanel_mc.bg._alpha = 0;
   minPanel_mc.icon._alpha = 0;
   fspDisc = minPanel_mc.createEmptyMovieClip("sanctuaryDisc",-16384);
   fspDrawDisc(false);
   minPanel_mc.nametag.nametag_BG._alpha = 0;
   minPanel_mc.nametag.createEmptyMovieClip("sanctuaryPill",-16384);
   var f = new TextFormat();
   f.font = "$MainFont";
   f.color = 0xFFFFFF;
   minPanel_mc.nametag.questName.setNewTextFormat(f);
   fspDrawTag();

   var rollOver = minPanel_mc.onRollOver;
   minPanel_mc.onRollOver = function()
   {
      rollOver.apply(this,arguments);
      fspDrawDisc(true);
      gs.TweenLite.to(fspDisc,0.5,{_xscale:110,_yscale:110,ease:mx.transitions.easing.Elastic.easeOut});
   };
   var rollOut = minPanel_mc.onRollOut;
   minPanel_mc.onRollOut = function()
   {
      rollOut.apply(this,arguments);
      fspDrawDisc(false);
      gs.TweenLite.to(fspDisc,0.3,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
   };
}

// --- build -----------------------------------------------------------------------------

function fspBuild()
{
   // The stock panel art goes; background_mc (transparent) stays as the panel's hit area.
   fspPanel.topPiece_mc._visible = false;
   fspPanel.farmLevel_mc._alpha = 0;
   fspPanel.farmLevel_mc.memberButton._visible = false;
   fspPanel.itemLimit_mc._alpha = 0;

   fspFrame = fspPanel.createEmptyMovieClip("sanctuaryFarm",-16384);
   fspFrame.bg = fspFrame.createEmptyMovieClip("bg",1);
   fspFrame.bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   fspDrawCard();
   fspFrame.caption_txt = fspText(fspFrame,"caption_txt","In hand",12,FSP_TOOL_Y - 21,FSP_W - 24,18,12,FSP_SOFT,"left");
   fspFrame.caption_txt.filters = fspOutline();

   // The house, in its socket.
   var house = fspPanel.houseRatingIcon;
   house._xscale = house._yscale = FSP_HOUSE_SCALE;
   house._x = FSP_HOUSE_X + 2 * FSP_HOUSE_SCALE / 100;
   house._y = FSP_HOUSE_Y - 1.6 * FSP_HOUSE_SCALE / 100;
   house.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.4,4,4,1,2)];

   fspPlace(fspPanel.farm_name_txt,FSP_NAME_X,FSP_TOP + 12,FSP_NAME_W,26,18,0xFFE08A,"left");
   fspPlace(fspPanel.player_name_txt,FSP_NAME_X,FSP_TOP + 38,FSP_NAME_W,22,13,FSP_SOFT,"left");

   fspBuildLevel();
   fspBuildItems();
   fspBuildTool();

   // The minimize button: a gold circle with a bar, drawn over the stock button (which keeps the clicks and
   // its tooltip; a clip with no mouse handlers doesn't take them). It rides on centerer_mc, which the stock
   // code scales in and out.
   var c = fspPanel.centerer_mc;
   c._x = FSP_W - 14;
   c._y = FSP_TOP + 14;
   var btn = c.minimize_btn;
   // Its frame images and glyph go; its dark disc (an unnamed shape) stays as the gold circle's glass rim.
   btn.up_mc._alpha = btn.over_mc._alpha = btn.down_mc._alpha = 0;
   btn.m_icon_up._alpha = btn.m_icon_over._alpha = 0;
   var b = btn.getBounds(c);
   var g = c.createEmptyMovieClip("sanctuaryMin",c.getNextHighestDepth());
   g._x = (b.xMin + b.xMax) / 2;
   g._y = (b.yMin + b.yMax) / 2;
   fspDrawMin(g,false);
   btn.addListener(fspMinListener);

   // The gold lip on the tool card, over the stock divider line at that height.
   var lip = fspPanel.createEmptyMovieClip("sanctuaryLip",fspPanel.getNextHighestDepth());
   lip.beginFill(0x1B2C6E,100);
   fspRoundRect(lip,14,FSP_TOOL_Y - 1.5,158,3,1.5);
   lip.endFill();
   lip.beginGradientFill("linear",[0xFFE08A,0xFFC531,0xFFE08A],[60,100,60],[0,128,255],{matrixType:"box",x:14,y:0,w:158,h:2,r:0});
   fspRoundRect(lip,14,FSP_TOOL_Y - 1.25,158,2.5,1.25);
   lip.endFill();
   lip.filters = [new flash.filters.GlowFilter(FSP_GOLD,0.4,6,6,1,2)];

   fspBuildMin();
}

function fspDrawMin(g, over)
{
   g.clear();
   var r = 15;
   g.lineStyle(over ? 2 : 1.5,0xFFF3C4,100);
   g.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   fspCircle(g,0,0,r);
   g.endFill();
   g.lineStyle();
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r,r:Math.PI / 2});
   fspRoundRect(g,-9,-12,18,10,5);
   g.endFill();
   g.lineStyle(4,FSP_INK,100);
   g.moveTo(-6,1);
   g.lineTo(6,1);
   g.filters = over ? [new flash.filters.GlowFilter(FSP_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
}

var fspMinListener = new Object();
fspMinListener.onHoverOver = function(btn)
{
   var g = fspPanel.centerer_mc.sanctuaryMin;
   fspDrawMin(g,true);
   gs.TweenLite.to(g,0.5,{_xscale:114,_yscale:114,ease:mx.transitions.easing.Elastic.easeOut});
};
fspMinListener.onHoverOut = function(btn)
{
   var g = fspPanel.centerer_mc.sanctuaryMin;
   fspDrawMin(g,false);
   gs.TweenLite.to(g,0.3,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
};

// --- Lua -------------------------------------------------------------------------------

var fspPopulateWindow = populateWindow_lua;
populateWindow_lua = function(farmName, playerName, farmLevel, progressLevel)
{
   var result = fspPopulateWindow.apply(_root,arguments);
   // Membership no longer exists.
   fspPanel.farmLevel_mc.memberButton._visible = false;
   fspFarmLevel = farmLevel;
   fspProgress = Number(progressLevel);
   if(isNaN(fspProgress))
   {
      fspProgress = 0;
   }
   fspColor(fspPanel.farm_name_txt,0xFFE08A);
   fspColor(fspPanel.player_name_txt,FSP_SOFT);
   fspDrawLevel();
   fspDrawTag();
   return result;
};

var fspHideLevelInfo = hideLevelInfo_lua;
hideLevelInfo_lua = function()
{
   var result = fspHideLevelInfo.apply(_root,arguments);
   fspDrawLevel();
   return result;
};

var fspSetCounts = SetCounts_lua;
SetCounts_lua = function(curFixtureCount, maxFixtureCount)
{
   var result = fspSetCounts.apply(_root,arguments);
   fspCur = curFixtureCount;
   fspMax = maxFixtureCount;
   fspDrawItems();
   return result;
};

var fspHideItemCount = hideItemCount_lua;
hideItemCount_lua = function()
{
   var result = fspHideItemCount.apply(_root,arguments);
   fspDrawItems();
   return result;
};

var fspShowItemCount = showItemCount_lua;
showItemCount_lua = function()
{
   var result = fspShowItemCount.apply(_root,arguments);
   fspDrawItems();
   return result;
};

var fspPopulateSubInfo = populateSubInfo_lua;
populateSubInfo_lua = function(iconId, name, consumible, consumeCount, consumibleProgress)
{
   var result = fspPopulateSubInfo.apply(_root,arguments);
   fspDrawTool();
   gs.TweenLite.from(fspPanel.panelInfo_mc.InfoIcon,0.6,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Elastic.easeOut});
   return result;
};

var fspClearExtraInfo = clearExtraInfo_lua;
clearExtraInfo_lua = function(iconId, name, consumible, consumeCount, consumibleProgress)
{
   var result = fspClearExtraInfo.apply(_root,arguments);
   fspDrawTool();
   return result;
};

var fspUpdateCharges = updateCharges_lua;
updateCharges_lua = function(n)
{
   var result = fspUpdateCharges.apply(_root,arguments);
   fspDrawTool();
   return result;
};

var fspSetTagName = setTagName;
setTagName = function(sName)
{
   var result = fspSetTagName.apply(_root,arguments);
   fspDrawTag();
   return result;
};

fspBuild();
// ===== End Sanctuary Farming Side Panel =====

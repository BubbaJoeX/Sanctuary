// ===== Sanctuary Character Custom Name =====
// Appended to characterCustomNameScreen's main timeline by build_client_mods.py.
// Restyles the custom name entry (type a name instead of spinning the name wheels) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass panel with the name in gold across its header, the
// field on a gold-rimmed glass plate, and Cancel / Go as a glass pill and a gold pill. The stock buttons stay as
// invisible hit areas over the pills (backButtonClick, nextButtonClick), and the field keeps its focus handling
// (the first focus asks Lua for the naming rules). The stock banner is a plain shape, dropped at build time
// (REMOVED_DEPTHS). Nothing here needs the world to be loaded.

var CU_W = 550;
var CU_H = 250;
var CU_PLATE_Y = 108;
var CU_PLATE_H = 48;
var CU_GOLD = 0xFFC531;
var CU_RIM = 0x9DB8FF;
var CU_SOFT = 0xC9D6FF;
var CU_INK = 0x3A2400;

var cuFrame;

// --- drawing helpers -------------------------------------------------------------------

function cuRoundRect(mc, x, y, w, h, r)
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

function cuGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   cuRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function cuGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   cuRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function cuSparkle(mc, x, y, r, alpha)
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

function cuText(mc, name, txt, x, y, w, h, size, color, align)
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

// Recolors a stock field, now and for any text set later.
function cuRecolor(t, color)
{
   var f = t.getTextFormat();
   f.color = color;
   t.setTextFormat(f);
   t.setNewTextFormat(f);
}

function cuOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Penner's elastic ease (the movie only has Strong compiled in).
function cuElasticOut(t, b, c, d)
{
   if(t == 0)
   {
      return b;
   }
   t /= d;
   if(t >= 1)
   {
      return b + c;
   }
   var p = d * 0.3;
   return c * Math.pow(2,-10 * t) * Math.sin((t * d - p / 4) * (2 * Math.PI) / p) + c + b;
}

// --- frame -----------------------------------------------------------------------------

function cuBuildFrame()
{
   var f = cuFrame = _root.createEmptyMovieClip("sanctuaryCharacterCustomName",-16384);
   var bg = f.createEmptyMovieClip("bg",1);
   cuGradientRect(bg,8,22,CU_W - 16,CU_H - 30,30,0x2E4FB0,0x141F55,100,CU_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-160,y:-180,w:640,h:400,r:0});
   cuRoundRect(bg,10,24,CU_W - 20,CU_H - 34,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   cuRoundRect(bg,12,26,CU_W - 24,60,26);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   var s = 2929;
   var i;
   for(i = 0; i < 14; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 30 + s % (CU_W - 60);
      s = (s * 1103515245 + 12345) % 2147483648;
      cuSparkle(bg,x,32 + s % 48,1 + s % 3,25 + s % 50);
   }
   // The field's plate.
   cuGradientRect(bg,56,CU_PLATE_Y,CU_W - 112,CU_PLATE_H,CU_PLATE_H / 2,0x0A1440,0x14246A,70,CU_GOLD,80,2);
   cuGloss(bg,64,CU_PLATE_Y + 2,CU_W - 128,CU_PLATE_H * 0.4,12,14);
   cuText(f,"hint_txt","Type your hero's name",0,CU_PLATE_Y - 22,CU_W,20,13,CU_SOFT);

   // The stars over the name (its last frame shows the field, so it keeps playing, unseen).
   nameBarBG._alpha = 0;
   cuRecolor(nameIsTxt,0xFFE08A);
   nameIsTxt.filters = cuOutline();
   nameIsTxt._y = 34;
   cuRecolor(nameField,0xFFFFFF);
   nameField.filters = cuOutline();
   nameField._y = CU_PLATE_Y + (CU_PLATE_H - nameField._height) / 2 + 1;
}

// --- Cancel / Go -----------------------------------------------------------------------

// Fits a stock button (an invisible hit area from now on) over the pill drawn for it.
function cuCoverButton(btn, x, y, w, h)
{
   btn._xscale = btn._yscale = 100;
   var b = btn.getBounds(btn);
   if(b.xMax <= b.xMin || b.yMax <= b.yMin)
   {
      return;
   }
   btn._xscale = w / (b.xMax - b.xMin) * 100;
   btn._yscale = h / (b.yMax - b.yMin) * 100;
   btn._x = x - b.xMin * btn._xscale / 100;
   btn._y = y - b.yMin * btn._yscale / 100;
   btn._alpha = 0;
}

function cuDrawPill(pill, over, down)
{
   var w = pill.w;
   var h = pill.h;
   var g = pill.bg;
   g.clear();
   if(pill.kind == "gold")
   {
      g.lineStyle(1.5,0xFFF3C4,100);
      g.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- w / 2,y:- h / 2,w:w,h:h,r:Math.PI / 2});
   }
   else
   {
      g.lineStyle(1.5,over ? 0xFFE08A : CU_RIM,over ? 100 : 60);
      g.beginGradientFill("linear",over ? [0x3F66D6,0x1D307A] : [0x284A9E,0x162660],[100,100],[0,255],{matrixType:"box",x:- w / 2,y:- h / 2,w:w,h:h,r:Math.PI / 2});
   }
   cuRoundRect(g,- w / 2,- h / 2,w,h,h / 2);
   g.endFill();
   cuGloss(g,- w / 2 + 8,- h / 2 + 2,w - 16,h * 0.42,h * 0.2,pill.kind == "gold" ? 55 : 25);
   pill.body._y = down ? 2 : 0;
   pill.filters = over ? [new flash.filters.GlowFilter(CU_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
}

function cuMakePill(btn, name, kind, label)
{
   var w = 140;
   var h = 46;
   var b = btn.getBounds(_root);
   var pill = cuFrame.createEmptyMovieClip(name,cuFrame.getNextHighestDepth());
   pill._x = b.xMax > b.xMin ? Math.round((b.xMin + b.xMax) / 2) : CU_W / 2;
   pill._y = b.yMax > b.yMin ? Math.round((b.yMin + b.yMax) / 2) : 185;
   pill.kind = kind;
   pill.w = w;
   pill.h = h;
   btn.sanctuaryPill = pill;
   pill.bg = pill.createEmptyMovieClip("bg",1);
   pill.body = pill.createEmptyMovieClip("body",2);
   var t = cuText(pill.body,"label_txt",label,- w / 2,-14,w,30,19,kind == "gold" ? CU_INK : 0xFFFFFF);
   if(kind != "gold")
   {
      t.filters = cuOutline();
   }
   cuCoverButton(btn,pill._x - w / 2,pill._y - h / 2,w,h);
   cuDrawPill(pill,false,false);
   btn.onDragOut = function()
   {
      FRIconButton.prototype.onDragOut.call(this);
      cuHoverPill(this,false);
   };
   return pill;
}

function cuHoverPill(btn, over)
{
   var pill = btn.sanctuaryPill;
   if(pill == undefined)
   {
      return;
   }
   cuDrawPill(pill,over,false);
   gs.TweenLite.to(pill,over ? 0.7 : 0.25,{_xscale:over ? 106 : 100,_yscale:over ? 106 : 100,ease:over ? cuElasticOut : mx.transitions.easing.Strong.easeOut});
}

// The buttons broadcast these to _root, their listener.
function onHoverOver(btn)
{
   cuHoverPill(btn,true);
}

function onHoverOut(btn)
{
   cuHoverPill(btn,false);
}

function onButtonPress(btn)
{
   if(btn.sanctuaryPill != undefined)
   {
      cuDrawPill(btn.sanctuaryPill,true,true);
   }
}

function onButtonRelease(btn)
{
   if(btn.sanctuaryPill != undefined)
   {
      cuDrawPill(btn.sanctuaryPill,btn.hitTest(_root._xmouse,_root._ymouse,true),false);
   }
}

// Lua names the field's heading; keep it gold.
var cuUpdateNameHeader = updateNameHeader;
updateNameHeader = function(name)
{
   cuUpdateNameHeader(name);
   cuRecolor(nameIsTxt,0xFFE08A);
};

cuBuildFrame();
cuFrame.cancel = cuMakePill(backButton_mc,"cancel","glass","Cancel");
cuFrame.go = cuMakePill(nextButton_mc,"go","gold","Go!");
// ===== End Sanctuary Character Custom Name =====

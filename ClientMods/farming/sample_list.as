// ===== Sanctuary Sample List =====
// Appended to sampleList's main timeline by build_client_mods.py.
// The Farm-O-Matic's sample picker (wndSampleList, opened beside the machine when a hopper is clicked) in the
// Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass flyout pointing at the machine, the
// title in gold under a gold rule, and a gold close circle. The samples are glass rows with the icon a little
// bigger in a frosted socket, the name in white and the count on a gold chip; the hovered row lights up with a
// gold rim and the picked one keeps a gold rim on a gold-tinted fill. The scrollbar is a slim navy capsule
// with a gold thumb, and the "how many?" picker that pops beside a row is a navy glass bubble with gold
// chevrons, the number in white and a gold check to confirm. The rows are a little taller (32px instead of
// 25), so the list's view size and row count are set to match. Everything is drawn under or over the stock
// clips, which keep their roles (hit areas, tooltips, Lua events).

var SMP_W = 342;
var SMP_H = 510;
var SMP_GOLD = 0xFFC531;
var SMP_RIM = 0x9DB8FF;
var SMP_SOFT = 0xC9D6FF;
var SMP_INK = 0x3A2400;
// The panel's right edge and the notch pointing at the machine.
var SMP_RIGHT = 300;
var SMP_NOTCH_Y = 216;
var SMP_NOTCH_X = 332;
// The list: where it sits, its view, and the row height (14 rows fill the view exactly).
var SMP_LIST_X = 10;
var SMP_LIST_Y = 52;
var SMP_LIST_W = 250;
var SMP_LIST_H = 448;
var SMP_ROW_H = 32;
var SMP_ROW_W = 246;
// The row's icon socket and chip (the stock icon is 24px, centered on icon_mc).
var SMP_ICON_X = 18;
var SMP_ICON_SCALE = 118;
var SMP_CHIP_X = 196;
var SMP_CHIP_W = 46;

var smpFrame;

// --- drawing helpers -------------------------------------------------------------------

function smpRoundRect(mc, x, y, w, h, r)
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

function smpGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   smpRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function smpGoldRect(mc, x, y, w, h, r, over)
{
   mc.lineStyle(over ? 2 : 1.5,0xFFF3C4,over ? 100 : 90);
   mc.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   smpRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h / 2,r:Math.PI / 2});
   smpRoundRect(mc,x + 3,y + 2,w - 6,h / 2,Math.min(r,h / 4));
   mc.endFill();
}

function smpCircle(mc, x, y, r)
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

function smpText(mc, name, txt, x, y, w, h, size, color, align)
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

function smpSparkle(mc, x, y, r, alpha)
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

function smpOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Restyles a stock text field in place: size, color and alignment.
function smpRestyle(t, size, color, align)
{
   t.embedFonts = true;
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = align;
   t.setTextFormat(f);
   t.setNewTextFormat(f);
   t.textColor = color;
}

// --- frame -----------------------------------------------------------------------------

// The flyout's outline: a rounded panel with a notch on the right pointing at the machine.
function smpPanelPath(mc, x, y, w, h, r)
{
   mc.moveTo(x + r,y);
   mc.lineTo(x + w - r,y);
   mc.curveTo(x + w,y,x + w,y + r);
   mc.lineTo(x + w,SMP_NOTCH_Y - 26);
   mc.lineTo(SMP_NOTCH_X,SMP_NOTCH_Y);
   mc.lineTo(x + w,SMP_NOTCH_Y + 26);
   mc.lineTo(x + w,y + h - r);
   mc.curveTo(x + w,y + h,x + w - r,y + h);
   mc.lineTo(x + r,y + h);
   mc.curveTo(x,y + h,x,y + h - r);
   mc.lineTo(x,y + r);
   mc.curveTo(x,y,x + r,y);
}

function smpDrawFrame()
{
   var bg = smpFrame.bg;
   bg.clear();
   bg.lineStyle(3,SMP_RIM,80);
   bg.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:4,y:4,w:SMP_RIGHT,h:SMP_H - 8,r:Math.PI / 2});
   smpPanelPath(bg,6,4,SMP_RIGHT - 6,SMP_H - 8,26);
   bg.endFill();
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-120,y:-200,w:420,h:440,r:0});
   smpRoundRect(bg,8,6,SMP_RIGHT - 10,SMP_H - 12,24);
   bg.endFill();

   // The header band with a few sparkles (fixed seed) and a gold rule.
   bg.beginFill(0x0A1440,30);
   smpRoundRect(bg,12,9,SMP_RIGHT - 18,36,18);
   bg.endFill();
   var s = 2727;
   var i;
   for(i = 0; i < 10; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 30 + s % 200;
      s = (s * 1103515245 + 12345) % 2147483648;
      smpSparkle(bg,x,14 + s % 26,1 + s % 2,15 + s % 35);
   }
   bg.beginGradientFill("linear",[SMP_GOLD,SMP_GOLD,SMP_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:60,y:0,w:SMP_RIGHT - 120,h:2,r:0});
   smpRoundRect(bg,60,46,SMP_RIGHT - 120,2,1);
   bg.endFill();

   // The recess the list sits in.
   smpGradientRect(bg,SMP_LIST_X - 4,SMP_LIST_Y - 3,SMP_LIST_W + 36,SMP_LIST_H + 6,16,0x0A1440,0x0A1440,38,SMP_RIM,22,1);
}

function smpDrawClose(over)
{
   var c = smpFrame.close;
   c.clear();
   var r = 14;
   c.lineStyle(over ? 2 : 1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   smpCircle(c,0,0,r);
   c.endFill();
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r,r:Math.PI / 2});
   smpRoundRect(c,-9,-12,18,10,5);
   c.endFill();
   c.lineStyle(3.5,SMP_INK,100);
   c.moveTo(-5,-5);
   c.lineTo(5,5);
   c.moveTo(5,-5);
   c.lineTo(-5,5);
   c.filters = over ? [new flash.filters.GlowFilter(SMP_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
}

var smpCloseListener = new Object();
smpCloseListener.onHoverOver = function(btn)
{
   smpDrawClose(true);
   gs.TweenLite.to(smpFrame.close,0.5,{_xscale:114,_yscale:114,ease:mx.transitions.easing.Elastic.easeOut});
};
smpCloseListener.onHoverOut = function(btn)
{
   smpDrawClose(false);
   gs.TweenLite.to(smpFrame.close,0.3,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
};

// --- rows ------------------------------------------------------------------------------

function smpDrawRow(item)
{
   var s = item.smpSkin;
   if(s == undefined)
   {
      return;
   }
   var over = item.hilite_mc._visible == true;
   var sel = item.selected_mc._visible == true;
   var gold = over || sel;
   var w = SMP_ROW_W;
   var h = SMP_ROW_H - 4;
   s.clear();
   if(sel && !over)
   {
      smpGradientRect(s,1,2,w,h,h / 2,0x5A5A9E,0x2A2F6E,100,SMP_GOLD,100,2);
   }
   else
   {
      smpGradientRect(s,1,2,w,h,h / 2,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : SMP_RIM,gold ? 100 : 35,gold ? 2 : 1);
   }
   s.lineStyle();
   s.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[over ? 22 : 12,0],[0,255],{matrixType:"box",x:0,y:3,w:10,h:h / 2,r:Math.PI / 2});
   smpRoundRect(s,6,3,w - 10,h / 2,h / 4);
   s.endFill();

   // The icon's frosted socket.
   var cy = SMP_ROW_H / 2;
   s.lineStyle(gold ? 2 : 1.5,gold ? 0xFFE08A : 0xFFFFFF,gold ? 100 : 85);
   s.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:SMP_ICON_X - 16,y:cy - 18,w:32,h:32,r:0});
   smpCircle(s,SMP_ICON_X,cy,14);
   s.endFill();

   // The count's gold chip.
   smpGoldRect(s,SMP_CHIP_X,cy - 10,SMP_CHIP_W,20,10,over);

   s.filters = over ? [new flash.filters.GlowFilter(SMP_GOLD,0.55,10,10,2,2)] : (sel ? [new flash.filters.GlowFilter(SMP_GOLD,0.35,8,8,2,2)] : []);
   item.title_txt.textColor = over || sel ? 0xFFE08A : 0xFFFFFF;
   item.quantity_txt.textColor = SMP_INK;
}

function smpStyleRow(item)
{
   if(item.smpSkin != undefined)
   {
      return;
   }
   item.smpSkin = item.createEmptyMovieClip("smpSkin",-16384);
   // The stock row art goes; its background stays as the (taller) hit area.
   item.bg_mc._yscale = item.bg_mc._yscale * (SMP_ROW_H - 2) / item.bg_mc._height;
   item.bg_mc._y = 1;
   item.hilite_mc._alpha = item.selected_mc._alpha = 0;
   item.icon_mc._x = SMP_ICON_X;
   item.icon_mc._y = SMP_ROW_H / 2;
   item.icon_mc._xscale = item.icon_mc._yscale = SMP_ICON_SCALE;
   item.title_txt._x = 38;
   item.title_txt._y = (SMP_ROW_H - 22) / 2;
   item.title_txt._width = SMP_CHIP_X - 44;
   item.title_txt._height = 22;
   smpRestyle(item.title_txt,15,0xFFFFFF,"left");
   item.title_txt.filters = smpOutline();
   item.quantity_txt._x = SMP_CHIP_X;
   item.quantity_txt._y = (SMP_ROW_H - 20) / 2;
   item.quantity_txt._width = SMP_CHIP_W;
   item.quantity_txt._height = 20;
   smpRestyle(item.quantity_txt,13,SMP_INK,"center");

   // The hover is the stock background's (it shows hilite_mc and the tooltip); the skin follows it.
   var bg = item.bg_mc;
   var rollOver = bg.onRollOver;
   bg.onRollOver = function()
   {
      rollOver.apply(this,arguments);
      smpDrawRow(item);
      gs.TweenLite.to(item.icon_mc,0.5,{_xscale:SMP_ICON_SCALE + 16,_yscale:SMP_ICON_SCALE + 16,ease:mx.transitions.easing.Elastic.easeOut});
   };
   var rollOut = bg.onRollOut;
   bg.onRollOut = function()
   {
      rollOut.apply(this,arguments);
      smpDrawRow(item);
      gs.TweenLite.to(item.icon_mc,0.3,{_xscale:SMP_ICON_SCALE,_yscale:SMP_ICON_SCALE,ease:mx.transitions.easing.Strong.easeOut});
   };
   // A press without a release outside would leave the row lit (the list may scroll it away).
   bg.onReleaseOutside = function()
   {
      item.hilite_mc._visible = false;
      smpDrawRow(item);
      gs.TweenLite.to(item.icon_mc,0.3,{_xscale:SMP_ICON_SCALE,_yscale:SMP_ICON_SCALE,ease:mx.transitions.easing.Strong.easeOut});
   };
}

function smpPatchRows()
{
   var proto = sampleList.VertListItem.prototype;
   proto.getItemHeight = function()
   {
      return SMP_ROW_H;
   };
   var drawItem = proto.drawItem;
   proto.drawItem = function()
   {
      smpStyleRow(this);
      drawItem.apply(this,arguments);
      // drawItem alternates the background's alpha; the skin is the row now.
      this.bg_mc._alpha = 0;
      smpDrawRow(this);
   };
   var setSelected = proto.setSelected;
   proto.setSelected = function(s)
   {
      setSelected.apply(this,arguments);
      smpDrawRow(this);
   };

   // The view fits 14 of the taller rows (stock: 18 of 25px in 450px).
   var list = sampleList.VertList.prototype;
   list.getViewItemTotal = function()
   {
      return Math.round(SMP_LIST_H / SMP_ROW_H);
   };
   list.setTotalItems = function(n)
   {
      this.m_totalItems = n;
      this.init(SMP_LIST_W,SMP_LIST_H);
      if(this.m_scrollbar != undefined)
      {
         smpSkinScrollbar(this.m_scrollbar);
      }
   };
}

// --- scrollbar -------------------------------------------------------------------------

// FRScrollBarV2 keeps resetting its parts' alpha (enableScrollBar), so the skin is drawn over the stock
// scrollbar, which still takes the clicks: a navy capsule with chevrons where the arrows are and a gold thumb.
function smpDrawScrollbar(s)
{
   var sb = s.sb;
   var t = sb.thumb_mc;
   var h = sb.background_mc._height;
   var w = 28;
   s.clear();
   smpGradientRect(s,2,0,w - 4,h,12,0x1B2F78,0x13215A,100,SMP_RIM,40,1);
   s.lineStyle();
   s.beginFill(0x050A24,60);
   smpRoundRect(s,w / 2 - 3,24,6,h - 48,3);
   s.endFill();
   s.lineStyle(2.5,SMP_SOFT,80);
   s.moveTo(w / 2 - 5,13);
   s.lineTo(w / 2,8);
   s.lineTo(w / 2 + 5,13);
   s.moveTo(w / 2 - 5,h - 13);
   s.lineTo(w / 2,h - 8);
   s.lineTo(w / 2 + 5,h - 13);
   var enabled = t._alpha > 60;
   s.lineStyle(1,0xFFF3C4,enabled ? 90 : 40);
   s.beginGradientFill("linear",[0xFFE08A,0xF5A623],[enabled ? 100 : 45,enabled ? 100 : 45],[0,255],{matrixType:"box",x:w / 2 - 6,y:t._y,w:12,h:t._height,r:0});
   smpRoundRect(s,w / 2 - 6,t._y,12,Math.max(t._height,14),6);
   s.endFill();
}

function smpSkinScrollbar(sb)
{
   if(sb.smpSkin != undefined)
   {
      return;
   }
   var s = sb.smpSkin = sb.createEmptyMovieClip("sanctuarySkin",sb.getNextHighestDepth());
   s.sb = sb;
   s.onEnterFrame = function()
   {
      var t = this.sb.thumb_mc;
      var key = t._y + "," + t._height + "," + t._alpha + "," + this.sb.background_mc._height;
      if(key != this.key)
      {
         this.key = key;
         smpDrawScrollbar(this);
      }
   };
}

// --- the "how many?" picker ------------------------------------------------------------

// The stock picker (a beige tag) stays underneath as the hit areas: up and down, the number field, and the
// tag itself, which submits. Its art includes an unnamed shape, so the skin is drawn over it (a clip with no
// mouse handlers doesn't take clicks), with the number mirrored from the stock field.
function smpDrawPicker(inc)
{
   var s = inc.smpSkin;
   var focused = Selection.getFocus() == String(inc.output_txt);
   s.clear();
   // The bubble, pointing back at the row on its left.
   s.lineStyle(2,0xFFE08A,90);
   s.beginGradientFill("linear",[0x3F66D6,0x1D307A],[100,100],[0,255],{matrixType:"box",x:0,y:0,w:70,h:62,r:Math.PI / 2});
   s.moveTo(14,1);
   s.lineTo(58,1);
   s.curveTo(70,1,70,13);
   s.lineTo(70,49);
   s.curveTo(70,61,58,61);
   s.lineTo(14,61);
   s.curveTo(2,61,2,49);
   s.lineTo(2,36);
   s.lineTo(-6,28);
   s.lineTo(2,20);
   s.lineTo(2,13);
   s.curveTo(2,1,14,1);
   s.endFill();
   // The number's recess.
   smpGradientRect(s,7,17,34,22,8,0x050A24,0x0E1A52,85,focused ? 0xFFE08A : SMP_RIM,focused ? 100 : 40,focused ? 2 : 1);
   // Up and down chevrons in gold.
   s.lineStyle(3.5,0xFFE08A,100);
   s.moveTo(17,13);
   s.lineTo(24,6);
   s.lineTo(31,13);
   s.moveTo(17,44);
   s.lineTo(24,51);
   s.lineTo(31,44);
   // The gold check that confirms (the tag behind takes the click).
   s.lineStyle(1.5,0xFFF3C4,100);
   s.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:44,y:17,w:22,h:22,r:Math.PI / 2});
   smpCircle(s,55,28,11);
   s.endFill();
   s.lineStyle(3,SMP_INK,100);
   s.moveTo(50,28);
   s.lineTo(54,32);
   s.lineTo(60,24);
}

function smpStylePicker(inc)
{
   if(inc.smpSkin != undefined)
   {
      return;
   }
   inc.output_txt._alpha = 0;
   var s = inc.smpSkin = inc.createEmptyMovieClip("smpSkin",inc.getNextHighestDepth());
   s.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.6,10,10,1,2)];
   var num = inc.createEmptyMovieClip("smpNumber",inc.getNextHighestDepth());
   num.txt = smpText(num,"txt","",7,18,34,22,15,0xFFFFFF);
   num.txt.filters = smpOutline();
   num.inc = inc;
   num.onEnterFrame = function()
   {
      var focused = Selection.getFocus() == String(this.inc.output_txt);
      var v = this.inc.output_txt.text;
      if(v != this.txt.text || focused != this.focused)
      {
         this.focused = focused;
         this.txt.text = v;
         smpDrawPicker(this.inc);
      }
   };
   smpDrawPicker(inc);
}

function smpPatchPicker()
{
   var proto = fr.display.SampleIncrementer.prototype;
   var onLoad = proto.onLoad;
   proto.onLoad = function()
   {
      onLoad.apply(this,arguments);
      smpStylePicker(this);
   };
}

// --- build -----------------------------------------------------------------------------

function smpBuild()
{
   // The stock flyout stays (invisible) so the window keeps its hit area.
   bg_mc._alpha = 0;

   smpFrame = _root.createEmptyMovieClip("sanctuarySampleList",-16384);
   smpFrame.bg = smpFrame.createEmptyMovieClip("bg",1);
   smpFrame.bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   smpFrame.close = smpFrame.createEmptyMovieClip("close",2);
   smpDrawFrame();

   // "Samples", centered in gold.
   title_txt._x = 40;
   title_txt._y = 11;
   title_txt._width = 216;
   title_txt._height = 32;
   smpRestyle(title_txt,22,0xFFE08A,"center");
   title_txt.filters = smpOutline();

   // "You don't have any samples": soft, centered in the recess.
   emptyMsg_txt._x = SMP_LIST_X + 10;
   emptyMsg_txt._y = 170;
   emptyMsg_txt._width = SMP_LIST_W + 10;
   emptyMsg_txt._height = 160;
   smpRestyle(emptyMsg_txt,17,SMP_SOFT,"center");
   emptyMsg_txt.filters = smpOutline();

   m_list._x = SMP_LIST_X;
   m_list._y = SMP_LIST_Y;

   // The close button: a gold circle under the stock one (an invisible hit area), a little bigger.
   exitButton_mc.up_mc._alpha = exitButton_mc.over_mc._alpha = exitButton_mc.down_mc._alpha = 0;
   exitButton_mc._xscale = exitButton_mc._yscale = 40;
   exitButton_mc._x = 270 - exitButton_mc._width / 2;
   exitButton_mc._y = 27 - exitButton_mc._height / 2;
   smpFrame.close._x = 270;
   smpFrame.close._y = 27;
   exitButton_mc.addListener(smpCloseListener);
   smpDrawClose(false);
}

// The "how many?" picker pops beside the picked row; the rows are taller, so it moves down to match.
var smpOnItemSelect = onItemSelect;
onItemSelect = function(item)
{
   var result = smpOnItemSelect.apply(_root,arguments);
   if(m_inc != undefined && m_inc._visible)
   {
      m_inc._x = SMP_LIST_X + SMP_LIST_W - 4;
      m_inc._y = SMP_LIST_Y + item._y + m_currentScrollY + SMP_ROW_H / 2 - 28;
   }
   return result;
};

// The text field's empty message shows when there's nothing to pick; Lua sets it once.
var smpSetTotalItems = setTotalItems_lua;
setTotalItems_lua = function(n)
{
   var result = smpSetTotalItems.apply(_root,arguments);
   emptyMsg_txt.textColor = SMP_SOFT;
   return result;
};

smpPatchRows();
smpPatchPicker();
smpBuild();
// ===== End Sanctuary Sample List =====

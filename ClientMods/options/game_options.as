// ===== Sanctuary Game Options =====
// Appended to optionsPanel's main timeline by build_client_mods.py.
// The Game Options window (wndOptionsPanel, opened by the system menu's Game Options row) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass frame with a gold title and close button, navy category rows
// (the selected one in gold) and glass option rows with gold checkboxes, radios and slider knobs, each slider with a
// readout. The rows scroll with the mouse wheel when a page is taller than the panel. The Video Capture category is
// left out (screenshots and recording are gone).
//
// How the window sets options: Lua (options.lua, Options:UpdateSettings) calls AddMenuOption for every row of
// the client's Resources/OptionElements.txt (the BaseClient.Options table, each value read through its VALUEFUNC),
// then parseTree. A row sends fscommand("FR_event","setOption(<OPTION_NAME>,<value>)") and the Lua handler
// (Main_wndOptionsPanel_swfOptionsPanel_OnUserEvent) runs the setter it knows for that name, then Ui.SaveUserOptions.
//
// That handler also knows two settings whose rows are commented out of OptionElements.txt, so the stock window never
// shows them. They are on a new More Options page:
//   setShowAllNamePlates   Ui.ShowNamePlates / Ui.HideNamePlates, saved as [NamePlateSettings] ShowAllNamePlates
//                          (startup.lua applies it at login; default 1).
//   SetOptionShowTrainers  [MinimapOptions] ShowTrainers (minimap.lua's Configure turns the minimap's trainer dots,
//                          indicator type 7, on or off whenever the map loads; default 1).
// The movie can't ask Lua for a value it wasn't sent, so a setting shows On/Off once it's known: from its own row,
// if OptionElements.txt has one (then it isn't repeated here), from goSetValue (which client Lua can call), or once
// picked here. Until then neither half of its switch is lit and the row names the client's default.
//
// The stock close button (an unnamed exitButton.swf clip at root depth 246) is dropped at build time (REMOVED_DEPTHS);
// the gold one sends its event, ExitButtonClicked.

var GO_W = 1014;
var GO_H = 738;
var GO_HEADER_H = 74;
var GO_PANEL_Y = 104;
var GO_PANEL_H = 596;
var GO_NAV_X = 24;
var GO_NAV_W = 224;
var GO_LIST_X = 256;
var GO_LIST_W = 734;
// The stock rule under the panel captions, and the visible part of the option list (browser coordinates).
var GO_RULE_Y = 164;
var GO_VIEW_Y = 166;
var GO_VIEW_H = 504;
// A navigation row's plate, inside the 204 wide navigation box (rows are indented by their _x).
var GO_NAV_ROW_W = 199;
// Option rows: the stock row is 320 wide (OptionsItem's bg); these are wider, with the stock controls moved in a little.
var GO_STOCK_ROW_W = 320;
var GO_ROW_W = 704;
var GO_ROW_INSET = 14;
var GO_SLIDER_X = 300;
var GO_CLOSE_X = 966;
var GO_CLOSE_Y = 46;
var GO_GOLD = 0xFFC531;
var GO_RIM = 0x9DB8FF;
var GO_SOFT = 0xC9D6FF;
var GO_INK = 0x3A2400;

// The More Options page: its category id, and the row type only this mod draws.
var GO_MORE_ID = "goMore";
var GO_SWITCH = "goSwitch";
var GO_NOTE = "goNote";
// [option name the Lua handler knows, label, caption, client default]
var GO_OPTIONS = [
   ["setShowAllNamePlates","Show everyone's names","The name plates over the players and characters around you.",1],
   ["SetOptionShowTrainers","Trainers on the minimap","Trainer dots on the minimap, from the next time the map loads.",1]
];

// Option name -> "1" or "0", once known.
var goKnown = new Object();
var goRoot;

// --- drawing helpers -------------------------------------------------------------------

function goRoundRect(mc, x, y, w, h, r)
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

function goGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   goRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function goCircle(mc, x, y, r)
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

function goGoldFill(mc, x, y, w, h, over)
{
   mc.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
}

// A glassy highlight across the top of a plate.
function goGlint(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:0,y:y,w:10,h:h,r:Math.PI / 2});
   goRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function goSparkle(mc, x, y, r, alpha)
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

function goOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function goText(mc, name, txt, x, y, w, h, size, color, align)
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

// Penner's elastic ease out; this movie only carries mx.transitions.easing.Strong, None and Bounce.
function goElasticOut(t, b, c, d)
{
   if(t == 0)
   {
      return b;
   }
   t /= d;
   if(t == 1)
   {
      return b + c;
   }
   var p = d * 0.3;
   return c * Math.pow(2,-10 * t) * Math.sin((t * d - p / 4) * (2 * Math.PI) / p) + c + b;
}

function goStrong()
{
   return mx.transitions.easing.Strong.easeOut;
}

// "1", 1, "true" -> "1"; "0", 0, "false", "" -> "0".
function goBool(value)
{
   var s = String(value);
   return s == "1" || s == "true" ? "1" : "0";
}

// --- frame -----------------------------------------------------------------------------

function goDrawFrame(bg)
{
   goGradientRect(bg,6,6,GO_W - 12,GO_H - 12,30,0x2E4FB0,0x141F55,100,GO_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-150,y:-300,w:900,h:700,r:0});
   goRoundRect(bg,8,8,GO_W - 16,GO_H - 16,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   goRoundRect(bg,12,12,GO_W - 24,GO_HEADER_H,26);
   bg.endFill();

   // Sparkles in the header (fixed seed, same every time).
   var s = 6262;
   var i;
   for(i = 0; i < 30; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 40 + s % (GO_W - 80);
      s = (s * 1103515245 + 12345) % 2147483648;
      goSparkle(bg,x,22 + s % 58,1 + s % 3,25 + s % 50);
   }

   // The category and option panels, each with a gold rule under its caption.
   goGradientRect(bg,GO_NAV_X,GO_PANEL_Y,GO_NAV_W,GO_PANEL_H,22,0x0A1440,0x0A1440,38,GO_RIM,22,1);
   goGradientRect(bg,GO_LIST_X,GO_PANEL_Y,GO_LIST_W,GO_PANEL_H,22,0x0A1440,0x0A1440,38,GO_RIM,22,1);
   goRule(bg,GO_NAV_X + 16,GO_NAV_W - 32,GO_RULE_Y);
   goRule(bg,GO_LIST_X + 16,GO_LIST_W - 32,GO_RULE_Y);
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
}

function goRule(mc, x, w, y)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[GO_GOLD,GO_GOLD,GO_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:x,y:0,w:w,h:2,r:0});
   goRoundRect(mc,x,y,w,2,1);
   mc.endFill();
}

function goBuildTitle()
{
   var title = goText(goRoot,"title_txt","Game Options",0,18,GO_W,40,28,0xFFE08A);
   title.filters = goOutline();
   goText(goRoot,"subtitle_txt","Sound, video, controls and more",0,54,GO_W,22,14,GO_SOFT);
   var hint = goText(goRoot,"hint_txt","Changes are saved as you make them",GO_LIST_X,GO_PANEL_Y + GO_PANEL_H - 24,GO_LIST_W,20,12,GO_SOFT);
   hint._alpha = 70;
}

// The gold close button, sending what the stock exit button (FRExitButton) sends.
function goBuildClose()
{
   var close = _root.createEmptyMovieClip("goClose",_root.getNextHighestDepth());
   close._x = GO_CLOSE_X;
   close._y = GO_CLOSE_Y;
   close.draw = function(over)
   {
      this.clear();
      this.lineStyle(2,0xFFF3C4,100);
      goGoldFill(this,-17,-17,34,34,over);
      goCircle(this,0,0,17);
      this.endFill();
      this.lineStyle(4,GO_INK,100);
      this.moveTo(-6,-6);
      this.lineTo(6,6);
      this.moveTo(6,-6);
      this.lineTo(-6,6);
      this.filters = over ? [new flash.filters.GlowFilter(GO_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,6,6,1,2)];
   };
   close.draw(false);
   close.onRollOver = function()
   {
      this.draw(true);
      fscommand("FR_event","PlaySound(DockOver)");
      gs.TweenLite.to(this,0.6,{_rotation:90,_xscale:112,_yscale:112,ease:goElasticOut});
   };
   close.onRollOut = close.onReleaseOutside = function()
   {
      this.draw(false);
      gs.TweenLite.to(this,0.6,{_rotation:0,_xscale:100,_yscale:100,ease:goElasticOut});
   };
   close.onRelease = function()
   {
      fscommand("FR_event","PlaySound(DockSelect)");
      fscommand("FR_event","ExitButtonClicked");
   };
}

// The stock frame art goes (the gear on its crown too); the captions turn gold and white.
function goRestyleChrome()
{
   browser.frame_mc._visible = false;
   // Its art is loaded by loadGraphics, which sets _visible itself, so alpha.
   browser.sortingbar_background_mc._alpha = 0;
   iconHolder_mc._visible = false;
   browser.sectionTitle.textColor = 0xFFE08A;
   browser.sectionTitle.filters = goOutline();
   browser.JobSort.textColor = 0xFFFFFF;
   browser.JobSort.filters = goOutline();
}

// --- category rows (fr.display.TreeNavigation / NavigationElement) -----------------------

function goDrawNav(item)
{
   var p = item.goPlate;
   if(p == undefined)
   {
      // Under the stock children; it has no mouse handlers, so the row keeps its own.
      p = item.goPlate = item.createEmptyMovieClip("goPlate",-16384);
      item.highlight._alpha = 0;
      new Color(item.mcExpandArrow).setRGB(0xFFE08A);
   }
   var w = GO_NAV_ROW_W - item._x;
   var sel = item.boolIsSelected;
   var over = item.goOver;
   var root = String(item.data.parentCatId) == "0";
   var y = 2;
   var h = 31;
   p.clear();
   if(sel)
   {
      goGradientRect(p,0,y,w,h,12,0x2B4FA8,0x17286A,100,GO_GOLD,100,2);
      p.lineStyle();
      p.beginGradientFill("linear",[GO_GOLD,GO_GOLD],[over ? 60 : 45,0],[0,210],{matrixType:"box",x:0,y:y,w:w,h:h,r:0});
      goRoundRect(p,1,y + 1,w - 2,h - 2,11);
      p.endFill();
   }
   else if(over)
   {
      goGradientRect(p,0,y,w,h,12,0x3F66D6,0x1D307A,100,0xFFE08A,100,1.5);
   }
   else if(root)
   {
      goGradientRect(p,0,y,w,h,12,0x22408F,0x152458,90,GO_RIM,30,1);
   }
   else
   {
      goGradientRect(p,0,y,w,h,12,0x1B3478,0x111D4A,70,GO_RIM,18,1);
   }
   goGlint(p,3,y + 2,w - 6,12,8,over || sel ? 20 : 12);
   // The stock rows carry no icons (catIcon 0): a gold bead on top-level rows, a soft dash under them.
   p.lineStyle();
   if(root)
   {
      p.lineStyle(1,0xFFF3C4,90);
      goGoldFill(p,13,y + h / 2 - 5,10,10,over);
      goCircle(p,18,y + h / 2,5);
      p.endFill();
   }
   else
   {
      p.beginFill(sel ? GO_GOLD : GO_SOFT,sel ? 100 : 60);
      goRoundRect(p,14,y + h / 2 - 1.5,12,3,1.5);
      p.endFill();
   }
   p.filters = sel || over ? [new flash.filters.GlowFilter(GO_GOLD,0.35,10,10,2,2)] : [];
   item.txtLabel.textColor = sel ? 0xFFE08A : 0xFFFFFF;
   item.txtLabel.filters = goOutline();

   // The More Options row is flagged as new.
   if(item.catId == GO_MORE_ID && item.goChip == undefined)
   {
      var chip = item.goChip = item.createEmptyMovieClip("goChip",item.getNextHighestDepth());
      chip._x = w - 44;
      chip._y = y + h / 2 - 8;
      chip.lineStyle(1,0xFFF3C4,100);
      goGoldFill(chip,0,0,36,16,false);
      goRoundRect(chip,0,0,36,16,8);
      chip.endFill();
      goText(chip,"new_txt","NEW",0,0,36,16,10,GO_INK);
   }
}

function goPatchNavigation()
{
   var tree = fr.display.TreeNavigation.prototype;
   var setElementData = tree._setElementData;
   tree._setElementData = function(item, data)
   {
      var result = setElementData.apply(this,arguments);
      goDrawNav(item);
      return result;
   };

   // Rows are created later (parseTree), so their constructors pick these up for onRollOver and onRollOut.
   var nav = fr.display.NavigationElement.prototype;
   var hooks = [["select",undefined],["unselect",undefined],["_handleRollOver",true],["_handleRollOut",false]];
   var i;
   for(i = 0; i < hooks.length; i++)
   {
      goHookNav(nav,hooks[i][0],hooks[i][1]);
   }
}

function goHookNav(proto, name, over)
{
   var stock = proto[name];
   proto[name] = function()
   {
      var result = stock.apply(this,arguments);
      if(over != undefined)
      {
         this.goOver = over;
      }
      if(this.goPlate != undefined)
      {
         goDrawNav(this);
      }
      return result;
   };
}

// --- option rows (OptionsItem and its toggle, radio, slider, button and label) ---------------

function goDrawRow(row)
{
   var p = row.goPlate;
   var over = row.goOver;
   var h = row.goH;
   p.clear();
   if(over)
   {
      goGradientRect(p,0,5,GO_ROW_W,h,12,0x3F66D6,0x1D307A,100,0xFFE08A,90,1.5);
   }
   else
   {
      goGradientRect(p,0,5,GO_ROW_W,h,12,0x22408F,0x152458,85,GO_RIM,25,1);
   }
   goGlint(p,3,7,GO_ROW_W - 6,12,8,over ? 20 : 12);
   p.filters = over ? [new flash.filters.GlowFilter(GO_GOLD,0.3,10,10,2,2)] : [];

   // A button row (Key Binding) is clicked anywhere on it; the pill says so.
   if(row.goKind == "button")
   {
      var x = GO_ROW_W - 112;
      p.lineStyle(1.5,0xFFF3C4,100);
      goGoldFill(p,x,9,100,h - 8,over);
      goRoundRect(p,x,9,100,h - 8,(h - 8) / 2);
      p.endFill();
   }
}

// The toggle's and radio's bullet, redrawn as a gold checkbox or radio (the stock art stays, invisible).
function goDrawCheck(toggle, on)
{
   var c = toggle.goCheck;
   if(c == undefined)
   {
      c = toggle.goCheck = toggle.createEmptyMovieClip("goCheck",toggle.getNextHighestDepth());
      // A little left of the stock bullet, clear of the label.
      c._x = toggle.bullet._x - 4;
      c._y = toggle.bullet._y;
   }
   toggle.bullet._alpha = 0;
   var radio = toggle.m_node.getData().OptionType == OptionsItem.TYPE_RADIO;
   c.clear();
   if(radio)
   {
      c.lineStyle(1.5,on ? GO_GOLD : GO_RIM,on ? 100 : 60);
      c.beginFill(0x0A1440,60);
      goCircle(c,0,0,11);
      c.endFill();
      if(on)
      {
         c.lineStyle(1,0xFFF3C4,100);
         goGoldFill(c,-6,-6,12,12,false);
         goCircle(c,0,0,6);
         c.endFill();
      }
   }
   else if(on)
   {
      c.lineStyle(1.5,0xFFF3C4,100);
      goGoldFill(c,-11,-11,22,22,false);
      goRoundRect(c,-11,-11,22,22,6);
      c.endFill();
      c.lineStyle(3.5,GO_INK,100);
      c.moveTo(-5.5,0);
      c.lineTo(-1.5,4.5);
      c.lineTo(6,-5);
   }
   else
   {
      c.lineStyle(1.5,GO_RIM,60);
      c.beginFill(0x0A1440,60);
      goRoundRect(c,-11,-11,22,22,6);
      c.endFill();
   }
   c.filters = on ? [new flash.filters.GlowFilter(GO_GOLD,0.6,8,8,2,2)] : [];
   // Pops when it changes, not when the row is first drawn.
   if(c.goOn != undefined && c.goOn != on)
   {
      c._xscale = c._yscale = 70;
      gs.TweenLite.to(c,0.6,{_xscale:100,_yscale:100,ease:goElasticOut});
   }
   c.goOn = on;
}

// The slider: a navy track, a gold fill (inside the stock fill clip, so the stock mask still sizes it), a gold
// knob on the stock dragger (which keeps its press handlers) and a readout of the value.
function goSkinSlider(row, detail, data)
{
   var s = detail.slider;
   s._x = GO_SLIDER_X;
   s.line.holder._alpha = 0;
   s.line.filled.holder._alpha = 0;
   s.dragger.holder._alpha = 0;

   var track = s.line.createEmptyMovieClip("goTrack",-16384);
   goGradientRect(track,-6,-2.5,112,8,4,0x0A1440,0x0A1440,70,GO_RIM,35,1);

   // The fill clip sits 7.5 above the line, and the mask shows it from 0 to the dragger.
   var fill = s.line.filled.createEmptyMovieClip("goFill",s.line.filled.getNextHighestDepth());
   fill.lineStyle();
   goGoldFill(fill,0,5,100,8,false);
   goRoundRect(fill,0,5,100,8,4);
   fill.endFill();

   var knob = s.dragger.createEmptyMovieClip("goKnob",s.dragger.getNextHighestDepth());
   knob.lineStyle(2,0xFFF3C4,100);
   goGoldFill(knob,-10,-9,20,20,false);
   goCircle(knob,0,1,10);
   knob.endFill();
   knob.lineStyle();
   knob.beginFill(0xFFFFFF,55);
   goCircle(knob,-3,-2.5,3.5);
   knob.endFill();
   knob.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];

   var readout = goText(detail,"goReadout_txt","",GO_SLIDER_X + 122,9,90,26,16,0xFFE08A,"left");
   readout.filters = goOutline();
   // Follows the knob, while it's dragged too (the stock code moves it on enter frame and tweens it on release).
   readout.goLast = -1;
   detail.goReadout = readout;
   detail.onEnterFrame = function()
   {
      var x = Math.round(this.slider.dragger._x);
      if(x == this.goReadout.goLast)
      {
         return;
      }
      this.goReadout.goLast = x;
      var steps = Number(data.IntParam1);
      this.goReadout.text = steps > 0 ? Math.round(x * steps / 100) + " / " + steps : x + "%";
   };
}

function goHookRowHover(row)
{
   var bg = row.bg;
   var over = bg.onRollOver;
   var out = bg.onRollOut;
   bg.onRollOver = bg.onDragOver = function()
   {
      over.apply(this,arguments);
      row.goOver = true;
      goDrawRow(row);
   };
   bg.onRollOut = bg.onDragOut = function()
   {
      out.apply(this,arguments);
      row.goOver = false;
      goDrawRow(row);
   };
}

// Called once the stock setData has built the row's control.
function goSkinRow(row, data)
{
   var type = data.OptionType;
   var detail = row.itemDetail;
   row.div._visible = false;
   row.bg.holder._alpha = 0;
   // The whole plate is the row's hit area (the stock one is 320 wide).
   row.bg._xscale = 100 * GO_ROW_W / GO_STOCK_ROW_W;
   if(detail == undefined)
   {
      return;
   }
   if(type == OptionsItem.TYPE_LABEL)
   {
      detail._x = 6;
      detail.displayName.textColor = 0xFFE08A;
      detail.displayName.filters = goOutline();
      return;
   }
   detail._x = GO_ROW_INSET;
   detail.displayName.textColor = 0xFFFFFF;
   detail.displayName.filters = goOutline();
   row.goKind = type;
   row.goH = 30;
   row.goPlate = row.createEmptyMovieClip("goPlate",-16384);
   if(type == OptionsItem.TYPE_SLIDER)
   {
      goSkinSlider(row,detail,data);
   }
   else if(type == OptionsItem.TYPE_BUTTON)
   {
      var open = goText(row,"goOpen_txt","Open",GO_ROW_W - 112,11,100,24,15,GO_INK);
   }
   goHookRowHover(row);
   goDrawRow(row);
}

function goPatchOptionRows()
{
   var item = OptionsItem.prototype;
   var setData = item.setData;
   item.setData = function(node)
   {
      var result = setData.apply(this,arguments);
      var data = node.getData();
      if(data.OptionType == GO_SWITCH)
      {
         goBuildSwitch(this,node);
      }
      else if(data.OptionType == GO_NOTE)
      {
         goBuildNote(this,node);
      }
      else
      {
         goSkinRow(this,data);
      }
      return result;
   };

   // Toggles and radios (OptionsRadio inherits it) draw their state here.
   var toggle = OptionsToggle.prototype;
   var setSelectedGraphic = toggle.setSelectedGraphic;
   toggle.setSelectedGraphic = function(n)
   {
      var result = setSelectedGraphic.apply(this,arguments);
      goDrawCheck(this,n == 1);
      return result;
   };
}

// --- More Options page --------------------------------------------------------------------

// The page's caption: soft text, no plate.
function goBuildNote(row, node)
{
   row.div._visible = false;
   row.bg.holder._alpha = 0;
   var t = goText(row,"goNote_txt",node.getData().DisplayName,GO_ROW_INSET,4,GO_ROW_W - 2 * GO_ROW_INSET,40,14,GO_SOFT,"left");
   t.multiline = t.wordWrap = true;
}

// A setting the movie may not know the value of: label, caption, and an On/Off switch whose halves each send
// their value (setOption, like every stock row).
function goBuildSwitch(row, node)
{
   var data = node.getData();
   row.goNode = node;
   row.goKind = GO_SWITCH;
   row.goH = 50;
   row.div._visible = false;
   row.bg.holder._alpha = 0;
   row.bg._xscale = 100 * GO_ROW_W / GO_STOCK_ROW_W;
   row.goPlate = row.createEmptyMovieClip("goPlate",-16384);
   goDrawRow(row);

   var label = goText(row,"goLabel_txt",data.DisplayName,GO_ROW_INSET + 4,9,420,26,18,0xFFFFFF,"left");
   label.filters = goOutline();
   row.goCaption = goText(row,"goCaption_txt","",GO_ROW_INSET + 4,33,470,20,12,GO_SOFT,"left");

   var seg = row.goSeg = row.createEmptyMovieClip("goSeg",row.getNextHighestDepth());
   seg._x = GO_ROW_W - 196;
   seg._y = 15;
   seg.bg = seg.createEmptyMovieClip("bg",1);
   goGradientRect(seg.bg,0,0,180,30,15,0x0A1440,0x0A1440,60,GO_RIM,35,1);
   goMakeHalf(row,seg,"on","On","1",2);
   goMakeHalf(row,seg,"off","Off","0",92);
   goDrawSwitch(row);
}

function goMakeHalf(row, seg, name, label, value, x)
{
   var half = seg[name] = seg.createEmptyMovieClip(name,seg.getNextHighestDepth());
   half._x = x;
   half._y = 2;
   half.value = value;
   half.face = half.createEmptyMovieClip("face",1);
   half.label_txt = goText(half,"label_txt",label,0,3,86,22,15,0xFFFFFF);
   half.onRollOver = function()
   {
      this.over = true;
      goDrawSwitch(row);
      fscommand("FR_event","PlaySound(DockOver)");
   };
   half.onRollOut = half.onReleaseOutside = function()
   {
      this.over = false;
      goDrawSwitch(row);
   };
   half.onRelease = function()
   {
      var data = row.goNode.getData();
      fscommand("FR_event","PlaySound(DockSelect)");
      fscommand("FR_event","setOption(" + data.OptionName + "," + this.value + ")");
      goKnown[data.OptionName] = this.value;
      data.Value = this.value;
      goDrawSwitch(row);
      this._xscale = this._yscale = 90;
      gs.TweenLite.to(this,0.5,{_xscale:100,_yscale:100,ease:goElasticOut});
   };
}

function goDrawSwitch(row)
{
   var data = row.goNode.getData();
   var value = String(data.Value);
   var known = value == "1" || value == "0";
   var halves = [row.goSeg.on,row.goSeg.off];
   var i;
   for(i = 0; i < halves.length; i++)
   {
      var half = halves[i];
      var sel = known && half.value == value;
      var f = half.face;
      f.clear();
      if(sel)
      {
         f.lineStyle(1.5,0xFFF3C4,100);
         goGoldFill(f,0,0,86,26,half.over);
         goRoundRect(f,0,0,86,26,13);
         f.endFill();
         goGlint(f,4,2,78,10,5,35);
      }
      else if(half.over)
      {
         goGradientRect(f,0,0,86,26,13,0x3F66D6,0x1D307A,100,0xFFE08A,90,1.5);
      }
      half.label_txt.textColor = sel ? GO_INK : (half.over ? 0xFFFFFF : GO_SOFT);
      half.label_txt.filters = sel ? [] : goOutline();
      f.filters = sel ? [new flash.filters.GlowFilter(GO_GOLD,0.5,10,10,2,2)] : [];
   }
   row.goCaption.text = data.Tooltip + (known ? "" : "  Client default: " + (String(data.DefaultValue) == "1" ? "On" : "Off") + ".");
}

// Finds the row Lua sent for a setting, if OptionElements.txt has one.
function goFindOption(node, optionName)
{
   if(node.getData().OptionName == optionName)
   {
      return node;
   }
   var i;
   for(i = 0; i < node.getChildCount(); i++)
   {
      var found = goFindOption(node.getChildNode(i),optionName);
      if(found != undefined)
      {
         return found;
      }
   }
   return undefined;
}

// Adds the More Options category and its rows (with the stock AddMenuOption, so the stock list builds them).
function goAddMorePage()
{
   if(findNodeById(baseNode,GO_MORE_ID) != undefined)
   {
      return;
   }
   var rows = [];
   var i;
   for(i = 0; i < GO_OPTIONS.length; i++)
   {
      // Already in the window with its live value (a patched OptionElements.txt): not repeated here.
      if(goFindOption(baseNode,GO_OPTIONS[i][0]) == undefined)
      {
         rows.push(GO_OPTIONS[i]);
      }
   }
   if(rows.length == 0)
   {
      return;
   }
   AddMenuOption(GO_MORE_ID,"base","More Options","","1",GO_MORE_ID,"0","0",OptionsItem.TYPE_MENU,"",0,0,0);
   AddMenuOption(GO_MORE_ID + "Note",GO_MORE_ID,"Settings the Free Realms client supports but never showed.","","0","0","","",GO_NOTE,"",0,0,0);
   for(i = 0; i < rows.length; i++)
   {
      var name = rows[i][0];
      var value = goKnown[name] == undefined ? "" : goKnown[name];
      AddMenuOption(GO_MORE_ID + i,GO_MORE_ID,rows[i][1],rows[i][2],"0","0",value,name,GO_SWITCH,"",0,0,rows[i][3]);
   }
}

// Lua can tell the window a setting's current value, e.g. from Options:UpdateSettings:
//   m:Invoke("goSetValue", "setShowAllNamePlates", Options:CheckStoredOption("NamePlateSettings", "ShowAllNamePlates", 1))
goSetValue = function(optionName, value)
{
   var v = goBool(value);
   goKnown[optionName] = v;
   var i;
   for(i = 0; i < GO_OPTIONS.length; i++)
   {
      var node = findNodeById(baseNode,GO_MORE_ID + i);
      if(node != undefined && node.getData().OptionName == optionName)
      {
         node.getData().Value = v;
         var row = qList[GO_MORE_ID + i];
         if(row.goSeg != undefined)
         {
            goDrawSwitch(row);
         }
      }
   }
};

// --- scrolling ------------------------------------------------------------------------------

function goContentHeight()
{
   var b = qList.getBounds(qList);
   return b.yMax > 0 ? b.yMax + 12 : 0;
}

function goScrollBy(dy)
{
   var minY = LIST_Y - Math.max(0,goContentHeight() - GO_VIEW_H);
   qList._y = Math.max(minY,Math.min(LIST_Y,qList._y + dy));
}

// The list's mask, the mouse wheel and a gold scroll thumb (the stock scrollbar is never shown).
function goBuildScrolling()
{
   var mask = browser.createEmptyMovieClip("goListMask",browser.getNextHighestDepth());
   mask.beginFill(0,100);
   goRoundRect(mask,GO_LIST_X + 2,GO_VIEW_Y,GO_LIST_W - 4,GO_VIEW_H,0);
   mask.endFill();
   qList.setMask(mask);

   if(_global.goWheel != undefined)
   {
      Mouse.removeListener(_global.goWheel);
   }
   var wheel = _global.goWheel = new Object();
   wheel.onMouseWheel = function(delta)
   {
      var x = _root._xmouse;
      var y = _root._ymouse;
      if(x >= GO_LIST_X && x <= GO_LIST_X + GO_LIST_W && y >= GO_VIEW_Y && y <= GO_VIEW_Y + GO_VIEW_H)
      {
         goScrollBy(delta * 30);
      }
   };
   Mouse.addListener(wheel);

   goRoot.thumb = goRoot.createEmptyMovieClip("thumb",goRoot.getNextHighestDepth());
}

function goDrawThumb()
{
   var t = goRoot.thumb;
   var content = goContentHeight();
   var key = content + "," + qList._y;
   if(key == t.key)
   {
      return;
   }
   t.key = key;
   t.clear();
   if(content <= GO_VIEW_H)
   {
      return;
   }
   var x = GO_LIST_X + GO_LIST_W - 12;
   var h = Math.max(30,GO_VIEW_H * GO_VIEW_H / content);
   var y = GO_VIEW_Y + (GO_VIEW_H - h) * (LIST_Y - qList._y) / (content - GO_VIEW_H);
   t.lineStyle();
   t.beginFill(0x0A1440,60);
   goRoundRect(t,x,GO_VIEW_Y,6,GO_VIEW_H,3);
   t.endFill();
   t.lineStyle(1,0xFFF3C4,90);
   goGoldFill(t,x,y,6,h,false);
   goRoundRect(t,x,y,6,h,3);
   t.endFill();
}

// --- every frame: the navigation box's art and the scroll thumb ------------------------------

function goSync()
{
   // The stock selected-group plates and the light blue hover-scroll bars over and under the categories; gold
   // chevrons stand in for the bars' arrows while the categories overflow.
   m_tree_mc.mcSelectedBarTop._alpha = 0;
   m_tree_mc.mcSelectedBarBottom._alpha = 0;
   m_tree_mc.mcNavTop._alpha = 0;
   m_tree_mc.mcNavBottom._alpha = 0;
   var box = m_tree_mc.navbox;
   var overflow = box.mcContent._height > box.mask._height + 4;
   if(overflow != goRoot.chevrons._visible)
   {
      goRoot.chevrons._visible = overflow;
   }
   goDrawThumb();
}

function goBuildChevrons()
{
   var c = goRoot.chevrons = goRoot.createEmptyMovieClip("chevrons",goRoot.getNextHighestDepth());
   var x = GO_NAV_X + GO_NAV_W / 2;
   var top = m_tree_mc._y + m_tree_mc.mcNavTop._y + 14;
   var bottom = m_tree_mc._y + m_tree_mc.mcNavBottom._y + 12;
   c.lineStyle(3,0xFFE08A,90);
   c.moveTo(x - 8,top + 4);
   c.lineTo(x,top - 4);
   c.lineTo(x + 8,top + 4);
   c.moveTo(x - 8,bottom - 4);
   c.lineTo(x,bottom + 4);
   c.lineTo(x + 8,bottom - 4);
   c._visible = false;
}

// --- Lua -------------------------------------------------------------------------------

// Screenshots and video recording are gone (hud/no_video_capture.as), so the Video Capture category and its rows
// are never added and the tree closes up: the category (submenu vidcapsettings), its Capture Resolution group
// (vidcapres) and anything sending a capture setting.
var GO_DROPPED_MENUS = {vidcapsettings:true,vidcapres:true};
var GO_DROPPED_OPTIONS = {toggleVideoCaptureUI:true,setVideoCaptureQuality:true,setVideoCaptureResolution:true};
var goStockAddMenuOption = AddMenuOption;
AddMenuOption = function(Id, Section, DisplayName, Tooltip, HasSubMenu, SubMenuName, Value, OptionName, OptionType, Callback, Disabled, IntParam1, DefaultValue)
{
   if(GO_DROPPED_MENUS[String(SubMenuName)] == true || GO_DROPPED_MENUS[String(Section)] == true || GO_DROPPED_OPTIONS[String(OptionName)] == true)
   {
      return undefined;
   }
   return goStockAddMenuOption.apply(_root,arguments);
};

// Lua calls parseTree once every row is in; the More Options page goes in first.
var goStockParseTree = parseTree;
parseTree = function()
{
   goAddMorePage();
   return goStockParseTree.apply(_root,arguments);
};

// Each page starts at its top.
var goStockSelectItem = onNavigationSelectItem;
onNavigationSelectItem = function(selectedCategory)
{
   var result = goStockSelectItem.apply(_root,arguments);
   qList._y = LIST_Y;
   return result;
};

function goBuild()
{
   // Has no mouse handlers, under everything stock.
   goRoot = _root.createEmptyMovieClip("goShell",-16384);
   goDrawFrame(goRoot.createEmptyMovieClip("bg",1));
   goBuildTitle();
   goRestyleChrome();
   goBuildChevrons();
   goBuildScrolling();
   goBuildClose();
   // Patched once per VM (Reset may rerun this frame).
   if(_global.goPatched != true)
   {
      _global.goPatched = true;
      goPatchNavigation();
      goPatchOptionRows();
   }
   goRoot.onEnterFrame = goSync;
}

goBuild();
// ===== End Sanctuary Game Options =====

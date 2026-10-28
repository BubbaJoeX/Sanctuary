// ===== Sanctuary Equipment =====
// Appended to equipmentscreen_new's main timeline by build_client_mods.py.
// Redraws the equipment pane in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass
// frame, a header with the selected slot's icon and a grid/list toggle, and new item cards and list rows.
// The job and slot wheels (CarouselVert, from the stock carouselVert.swf library) become dials: the selected
// entry sits in a gold socket that points at the next column, the rest curve away along an arc, shrinking and
// fading, and the mouse wheel turns them. Thumbnails are a little bigger, in a frosted socket that rounds off
// their edges. Everything is drawn over the stock clips, which keep their roles (hit areas, Lua callbacks).

var EQ_W = 742;
var EQ_H = 650;
var EQ_WHEEL_Y = 46;
var EQ_WHEEL_H = 566;
// How far the wheels' outer entries curve back (px), and the disc behind each entry.
var EQ_ARC = 36;
var EQ_DISC_R = 36;
// The slot wheel sits a little right of the stock spot, so the job wheel's pointer clears its socket.
var EQ_SLOT_WHEEL_X = 134;
// The card thumbnail: its center and scale (the stock icon is 64px), resting and hovered.
var EQ_ICON_X = 51;
var EQ_ICON_Y = 51;
var EQ_ICON_SCALE = 118;
var EQ_ICON_HOVER = 124;
var EQ_LABEL_X = 100;
var EQ_LABEL_Y = 14;
var EQ_GOLD = 0xFFC531;
var EQ_RIM = 0x9DB8FF;
var EQ_SOFT = 0xC9D6FF;

// --- drawing helpers -------------------------------------------------------------------

function eqRoundRect(mc, x, y, w, h, r)
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

function eqGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   eqRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function eqCircle(mc, x, y, r)
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

function eqText(mc, name, txt, x, y, w, h, size, color, align)
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

function eqSparkle(mc, x, y, r, alpha)
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

function eqOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// --- frame -----------------------------------------------------------------------------

var eqFrame;

function eqDrawFrame()
{
   var bg = eqFrame.bg;
   bg.clear();
   eqGradientRect(bg,4,4,EQ_W - 8,EQ_H - 8,30,0x2E4FB0,0x141F55,100,EQ_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-160,y:-320,w:820,h:680,r:0});
   eqRoundRect(bg,6,6,EQ_W - 12,EQ_H - 12,28);
   bg.endFill();

   if(m_isEquipScreen)
   {
      // The bay the two wheels sit in.
      eqGradientRect(bg,14,14,232,EQ_H - 28,24,0x0A1440,0x0A1440,30,EQ_RIM,18,1);
      var s = 1717;
      var i;
      for(i = 0; i < 9; i++)
      {
         s = (s * 1103515245 + 12345) % 2147483648;
         var x = 26 + s % 200;
         s = (s * 1103515245 + 12345) % 2147483648;
         eqSparkle(bg,x,22 + s % 18,1 + s % 2,20 + s % 40);
      }
   }
   eqFrame.jobCaption._visible = eqFrame.slotCaption._visible = m_isEquipScreen;
   eqDrawHeader();
}

function eqDrawHeader()
{
   var h = eqFrame.header;
   h.clear();
   var x = TitleBar._x;
   var w = 630 - x;
   eqGradientRect(h,x,TitleBar._y - 3,w,40,20,0x0A1440,0x0A1440,50,EQ_RIM,30,1);
   // A gold underline under the title.
   h.lineStyle();
   h.beginGradientFill("linear",[EQ_GOLD,EQ_GOLD,EQ_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:x + 20,y:0,w:w - 40,h:2,r:0});
   eqRoundRect(h,x + 20,TitleBar._y + 35,w - 40,2,1);
   h.endFill();
   eqDrawToggle();
}

// The grid/list toggle, under the stock view buttons (now invisible hit areas).
function eqDrawToggle()
{
   var t = eqFrame.toggle;
   t.clear();
   eqGradientRect(t,636,14,84,38,19,0x0A1440,0x0A1440,50,EQ_RIM,30,1);
   var list = item_display.mode == 1 || item_display.mode == 3;
   var cx = list ? 698 : 658;
   t.lineStyle(1,0xFFF3C4,80);
   t.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:cx - 16,y:17,w:32,h:32,r:Math.PI / 2});
   eqCircle(t,cx,33,16);
   t.endFill();

   // Grid glyph: four squares.
   var c = list ? EQ_SOFT : 0x3A2400;
   t.lineStyle();
   t.beginFill(c,100);
   eqRoundRect(t,651,26,6,6,1);
   eqRoundRect(t,659,26,6,6,1);
   eqRoundRect(t,651,34,6,6,1);
   eqRoundRect(t,659,34,6,6,1);
   t.endFill();
   // List glyph: three lines.
   c = list ? 0x3A2400 : EQ_SOFT;
   t.beginFill(c,100);
   eqRoundRect(t,691,26,14,3,1);
   eqRoundRect(t,691,32,14,3,1);
   eqRoundRect(t,691,38,14,3,1);
   t.endFill();
}

// The header shows the selected slot's icon (the item equipped there), left of the title.
function eqUpdateHeaderIcon()
{
   var data = m_catCarousel.m_selectedItem.dataObj;
   var icon = eqFrame.headerIcon;
   icon._x = TitleBar._x + 18;
   icon._y = TitleBar._y + 1;
   if(m_isEquipScreen && data != undefined && Number(data.iconID) > 0)
   {
      _root.frSetIconByIndex(icon.holder,data.iconID,data.tintValue);
      icon._visible = true;
   }
   else
   {
      icon._visible = false;
   }
}

function eqBuildFrame()
{
   eqFrame = _root.createEmptyMovieClip("sanctuaryFrame",-16384);
   eqFrame.bg = eqFrame.createEmptyMovieClip("bg",1);
   eqFrame.bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   eqFrame.header = eqFrame.createEmptyMovieClip("header",2);
   eqFrame.toggle = eqFrame.createEmptyMovieClip("toggle",3);

   var icon = eqFrame.createEmptyMovieClip("headerIcon",4);
   icon.holder = icon.createEmptyMovieClip("holder",1);
   icon.holder.beginFill(0,0);
   eqRoundRect(icon.holder,0,0,64,64,0);
   icon.holder.endFill();
   icon._xscale = icon._yscale = 50;
   icon._visible = false;

   eqFrame.jobCaption = eqText(eqFrame,"jobCaption","Job",m_jobCarousel._x,EQ_WHEEL_Y - 32,103,24,16,EQ_SOFT);
   eqFrame.slotCaption = eqText(eqFrame,"slotCaption","Slot",m_catCarousel._x,EQ_WHEEL_Y - 32,103,24,16,EQ_SOFT);
   eqFrame.jobCaption.filters = eqFrame.slotCaption.filters = eqOutline();

   background._visible = false;
   divLine1_mc._alpha = divLine2_mc._alpha = 0;
   TitleBar.bg_mc._alpha = 0;
   TitleBar.title.filters = eqOutline();
   buttonsView_btn._alpha = listView_btn._alpha = 0;
   eqDrawFrame();
}

// --- wheels ----------------------------------------------------------------------------

function eqDrawDisc(item, over)
{
   var d = item.sanctuaryDisc;
   var r = EQ_DISC_R;
   var y = item.icon._y;
   d.clear();
   d.lineStyle();
   d.beginFill(0x050A24,35);
   eqCircle(d,0,y + 3,r + 1);
   d.endFill();
   d.lineStyle(2,over ? 0xFFE08A : EQ_RIM,over ? 100 : 60);
   d.beginGradientFill("radial",over ? [0x6A90F5,0x2643A8] : [0x4A70DC,0x172A6E],[100,100],[0,255],{matrixType:"box",x:- r,y:y - r - 14,w:r * 2,h:r * 2,r:0});
   eqCircle(d,0,y,r);
   d.endFill();
   // Glassy highlight across the top.
   d.lineStyle();
   d.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[28,0],[0,255],{matrixType:"box",x:- r,y:y - r,w:r * 2,h:r,r:Math.PI / 2});
   eqRoundRect(d,- r * 0.7,y - r + 4,r * 1.4,r * 0.7,r * 0.35);
   d.endFill();
}

function eqStyleWheelItem(item)
{
   // JobItem's and CategoryButton's stock plates; their hit areas stay (an invisible clip still takes the mouse).
   item.up_mc._alpha = item.over_mc._alpha = item.down_mc._alpha = 0;
   item.UP._alpha = item.HOVER._alpha = item.DOWN._alpha = item.SELECTION._alpha = 0;
   item.hitArea._alpha = 0;
   item.sanctuaryDisc = item.createEmptyMovieClip("sanctuaryDisc",-16384);
   item.label.filters = eqOutline();
   eqDrawDisc(item,false);
}

// The rail behind a wheel: a capsule, the gold socket at its middle with a pointer to the next column,
// and chevrons where the (now invisible) stock arrows are.
function eqDrawTrack(car)
{
   var t = car.sanctuaryTrack;
   var w = car.m_bg._width;
   var h = car.m_bg._height;
   var cx = w / 2;
   var cy = h / 2;
   t.clear();
   t.lineStyle(1,EQ_RIM,25);
   t.beginGradientFill("linear",[0x0A1440,0x0A1440,0x0A1440],[15,55,15],[0,128,255],{matrixType:"box",x:0,y:0,w:w,h:h,r:Math.PI / 2});
   eqRoundRect(t,cx - 46,0,92,h,46);
   t.endFill();

   t.lineStyle();
   t.beginGradientFill("radial",[0xFFE08A,0xFFC531],[45,0],[0,255],{matrixType:"box",x:cx - 74,y:cy - 74,w:148,h:148,r:0});
   eqCircle(t,cx,cy,74);
   t.endFill();
   // The selected entry pops to 120% (CarouselVert.popSelectedItem), just inside the ring.
   var ring = EQ_DISC_R * 1.2 + 5;
   t.lineStyle(1,0xFFF3C4,60);
   eqCircle(t,cx,cy,ring + 4);
   t.lineStyle(3,EQ_GOLD,100);
   eqCircle(t,cx,cy,ring);
   t.lineStyle(1,0xFFF3C4,80);
   t.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:0,y:cy - 9,w:10,h:18,r:Math.PI / 2});
   t.moveTo(cx + ring + 1,cy - 9);
   t.lineTo(cx + ring + 10,cy);
   t.lineTo(cx + ring + 1,cy + 9);
   t.lineTo(cx + ring + 1,cy - 9);
   t.endFill();

   t.lineStyle(3,EQ_SOFT,80);
   t.moveTo(cx - 10,22);
   t.lineTo(cx,12);
   t.lineTo(cx + 10,22);
   t.moveTo(cx - 10,h - 22);
   t.lineTo(cx,h - 12);
   t.lineTo(cx + 10,h - 22);
}

function eqStyleWheel(car)
{
   car._y = EQ_WHEEL_Y;
   car.m_bg._alpha = 0;
   car.m_selector._visible = false;
   car.m_arrowUp._alpha = car.m_arrowDown._alpha = 0;
   car.sanctuaryTrack = car.createEmptyMovieClip("sanctuaryTrack",-16384);
   // Rebuilds the entries (styled by addViewItem below) and recenters the first, like createCarousels.
   car.setHeight(EQ_WHEEL_H);
   car.setSelectedIndex(0);
   eqDrawTrack(car);
}

function eqPatchWheels()
{
   var proto = CarouselVert.prototype;
   // A shorter mask inset: the outer entries fade out instead of being cut off by the arrows.
   CarouselVert.MASK_INSET_Y = 30;

   var addViewItem = proto.addViewItem;
   proto.addViewItem = function()
   {
      var item = addViewItem.call(this);
      eqStyleWheelItem(item);
      return item;
   };

   // The arc: entries shrink, fade and curve back the further they are from the socket.
   proto.scaleItem = function(mov)
   {
      var c = this.m_bg._height / 2;
      var d = (mov._y - c) / c;
      if(d > 1)
      {
         d = 1;
      }
      else if(d < -1)
      {
         d = -1;
      }
      var a = Math.abs(d);
      mov._xscale = mov._yscale = 100 - 36 * a;
      mov._alpha = 100 - 85 * a * a;
      mov._x = this.m_bg._width / 2 - EQ_ARC * (1 - Math.cos(d * 1.2));
   };

   var setDisabled = proto.setDisabled;
   proto.setDisabled = function(is)
   {
      setDisabled.call(this,is);
      // The click blocker is the stock column art; keep it, invisibly.
      this.m_disabled._alpha = 0;
   };

   var jobStates = ["setStateUp","setStateOver","setStateDown"];
   var i;
   for(i = 0; i < jobStates.length; i++)
   {
      eqWrapJobState(jobStates[i],i > 0);
   }
   var updateState = CategoryButton.prototype.updateState;
   CategoryButton.prototype.updateState = function(mode)
   {
      updateState.call(this,mode);
      if(this.sanctuaryDisc)
      {
         eqDrawDisc(this,mode == 2 || mode == 3);
      }
   };
}

function eqWrapJobState(name, over)
{
   var stock = JobItem.prototype[name];
   JobItem.prototype[name] = function()
   {
      stock.call(this);
      if(this.sanctuaryDisc)
      {
         eqDrawDisc(this,over);
      }
   };
}

// The mouse wheel turns whichever wheel it's over, a step at a time.
function eqWheelUnderMouse()
{
   if(!m_isEquipScreen)
   {
      return undefined;
   }
   var cars = [m_jobCarousel,m_catCarousel];
   var i;
   for(i = 0; i < cars.length; i++)
   {
      if(cars[i].hitTest(_root._xmouse,_root._ymouse,false))
      {
         return cars[i];
      }
   }
   return undefined;
}

var eqWheelListener = new Object();
eqWheelListener.onMouseWheel = function(delta)
{
   var car = eqWheelUnderMouse();
   if(car == undefined || car.m_disabled != undefined)
   {
      return;
   }
   var index = car.getSelectedIndex();
   if(index != undefined)
   {
      car.setSelectedIndex(delta > 0 ? index - 1 : index + 1);
   }
};
// The item list's scrollbar scrolls on the wheel anywhere in the window; not while it's over a wheel.
eqWheelListener.onMouseMove = function()
{
   scrollbar.boolEnableWheel = eqWheelUnderMouse() == undefined;
};

// --- cards (grid view) -----------------------------------------------------------------

function eqDrawCard(b, over, down)
{
   var c = b.sanctuaryCard;
   var equipped = b.sanctuaryEquipped && !b.noneItem;
   var y = down ? 6 : 4;
   c.clear();
   if(over)
   {
      eqGradientRect(c,4,y,218,94,18,0x4069DA,0x1E3486,100,equipped ? EQ_GOLD : 0xFFE08A,100,equipped ? 3 : 2);
   }
   else
   {
      eqGradientRect(c,4,y,218,94,18,0x2B4FA8,0x17286A,100,equipped ? EQ_GOLD : EQ_RIM,equipped ? 100 : 40,equipped ? 3 : 1.5);
   }
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[16,0],[0,255],{matrixType:"box",x:0,y:y + 3,w:10,h:40,r:Math.PI / 2});
   eqRoundRect(c,7,y + 3,212,40,15);
   c.endFill();

   // The thumbnail's socket: frosted white, so the stock icons (made for light plates) read well.
   if(b.noneItem)
   {
      c.lineStyle(2,EQ_RIM,50);
      c.beginFill(0x0A1440,40);
      eqRoundRect(c,11,y + 7,80,80,14);
      c.endFill();
      c.lineStyle(4,EQ_RIM,60);
      eqCircle(c,51,y + 47,18);
      c.moveTo(38,y + 60);
      c.lineTo(64,y + 34);
   }
   else
   {
      c.lineStyle(2,equipped ? EQ_GOLD : 0xFFFFFF,equipped ? 100 : 85);
      c.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:-4,y:y - 6,w:110,h:110,r:0});
      eqRoundRect(c,11,y + 7,80,80,14);
      c.endFill();
   }

   c.state_txt._visible = equipped;
   c.state_txt._y = y + 64;
   if(over)
   {
      c.filters = [new flash.filters.GlowFilter(EQ_GOLD,0.55,14,14,2,2)];
   }
   else if(equipped)
   {
      c.filters = [new flash.filters.GlowFilter(EQ_GOLD,0.35,10,10,2,2)];
   }
   else
   {
      c.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
   }
}

function eqStyleCard(b)
{
   if(b.sanctuaryCard)
   {
      return;
   }
   b.background._alpha = b.states._alpha = b.iconBackground._alpha = b.selection._alpha = 0;
   b.hitArea._alpha = 0;
   var c = b.sanctuaryCard = b.createEmptyMovieClip("sanctuaryCard",-16384);
   var state = eqText(c,"state_txt","Equipped",EQ_LABEL_X,68,114,22,13,EQ_GOLD,"left");
   state.filters = eqOutline();
   state._visible = false;

   b.icon._x = EQ_ICON_X;
   b.icon._y = EQ_ICON_Y;
   b.icon._xscale = b.icon._yscale = EQ_ICON_SCALE;
   // Rounds off the thumbnail's corners (and crops it to the socket when it grows on hover).
   var mask = b.createEmptyMovieClip("sanctuaryIconMask",b.getNextHighestDepth());
   mask.beginFill(0,100);
   eqRoundRect(mask,13,13,76,76,12);
   mask.endFill();
   b.icon.setMask(mask);

   b.label._x = EQ_LABEL_X;
   b.label._y = EQ_LABEL_Y;
   b.label._width = 114;
   b.label._height = 52;
   b.label.filters = [new flash.filters.DropShadowFilter(2,90,0x0A1030,0.9,3,3,1,1)];
}

function eqPatchCards()
{
   var proto = EquipmentItemButton.prototype;
   EquipmentItemButton.iconOrigY = EQ_ICON_Y;
   EquipmentItemButton.labelOrigY = EQ_LABEL_Y;

   // The first thing a card does with an item, so the label has its new size before the name is fitted to it.
   var onNewItem = proto.onNewItem;
   proto.onNewItem = function()
   {
      eqStyleCard(this);
      onNewItem.call(this);
   };

   var select = proto.select;
   proto.select = function(value)
   {
      select.call(this,value);
      this.sanctuaryEquipped = value;
      if(this.sanctuaryCard)
      {
         eqDrawCard(this,false,false);
      }
   };

   proto.updateState = function(mode)
   {
      var over = mode == 2 || mode == 3;
      var down = mode == 3 ? EquipmentItemButton.pressDisplacement : 0;
      eqDrawCard(this,over,mode == 3);
      if(over)
      {
         gs.TweenLite.to(this.icon,0.6,{_xscale:EQ_ICON_HOVER,_yscale:EQ_ICON_HOVER,ease:mx.transitions.easing.Elastic.easeOut});
      }
      else
      {
         gs.TweenLite.to(this.icon,0.15,{_xscale:EQ_ICON_SCALE,_yscale:EQ_ICON_SCALE});
      }
      this.icon._y = EQ_ICON_Y + down;
      this.label._y = EQ_LABEL_Y + down;
      this.properties._y = EquipmentItemButton.propOrigY + down;
   };
}

// --- rows (list view) ------------------------------------------------------------------

function eqDrawRow(row, over)
{
   var r = row.sanctuaryRow;
   var w = row.hitArea._width - 4;
   r.clear();
   if(over)
   {
      eqGradientRect(r,0,1,w,32,12,0x4069DA,0x1E3486,100,0xFFE08A,100,1.5);
   }
   else if(row.selected)
   {
      eqGradientRect(r,0,1,w,32,12,0x2B4FA8,0x17286A,100,EQ_GOLD,100,2);
   }
   else
   {
      r.lineStyle();
      r.beginFill(0x0A1440,row.index % 2 == 0 ? 18 : 34);
      eqRoundRect(r,0,1,w,32,12);
      r.endFill();
   }
}

function eqPatchRows()
{
   var proto = EquipmentItemList.prototype;
   var onNewItem = proto.onNewItem;
   proto.onNewItem = function()
   {
      if(!this.sanctuaryRow)
      {
         this.shade._alpha = this.highlight._alpha = this.hitArea._alpha = 0;
         this.sanctuaryRow = this.createEmptyMovieClip("sanctuaryRow",-16384);
         this.label.filters = [new flash.filters.DropShadowFilter(1,90,0x0A1030,0.9,2,2,1,1)];
      }
      onNewItem.call(this);
   };
   var updateState = proto.updateState;
   proto.updateState = function(mode)
   {
      updateState.call(this,mode);
      if(this.sanctuaryRow)
      {
         eqDrawRow(this,mode == 2 || mode == 3);
      }
   };
}

// --- Lua and stock hooks ---------------------------------------------------------------

var eqSetIsEquipScreen = setIsEquipScreen_lua;
setIsEquipScreen_lua = function(n)
{
   eqSetIsEquipScreen(n);
   eqDrawFrame();
   eqUpdateHeaderIcon();
};

var eqSetMode = setMode;
setMode = function(mode)
{
   eqSetMode(mode);
   // Members-only no longer exists, and its column icon would sit on the header's slot icon.
   TitleBar.membersOnly._visible = false;
   eqDrawToggle();
};

var eqOnCarouselItemSelected = onCarouselItemSelected;
onCarouselItemSelected = function(target)
{
   eqOnCarouselItemSelected(target);
   eqUpdateHeaderIcon();
};

// Equipping something changes the slot's icon.
var eqSetCategoryDataForEquip = setCategoryDataForEquip_lua;
setCategoryDataForEquip_lua = function(message)
{
   eqSetCategoryDataForEquip(message);
   eqUpdateHeaderIcon();
};

eqPatchWheels();
eqPatchCards();
eqPatchRows();
m_catCarousel._x = EQ_SLOT_WHEEL_X;
eqStyleWheel(m_jobCarousel);
eqStyleWheel(m_catCarousel);
eqBuildFrame();
Mouse.addListener(eqWheelListener);
// ===== End Sanctuary Equipment =====

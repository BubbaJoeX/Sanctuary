// ===== Sanctuary Friends Dock =====
// Appended to social's main timeline (frame 2, where its stock code lives) by build_client_mods.py.
// Redraws the friends dock (wndSocial) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy
// glass panel whose header bar holds the open pane's icon in a frosted socket, its title in gold and an online
// count pill; glass friend rows with a presence dot (green online, amber in a game or on another server, grey
// offline) and the status in soft blue; a rounded add-by-name field with a gold add button; and the pane buttons
// (friends, acquaintances, guild, ignored, settings) as frosted round sockets with a gold hover, the open pane's
// in gold. The flip side (a player's card, the settings) and the hover card beside the list match.
// Everything is drawn under or over the stock clips, which keep their roles (hit areas, Lua callbacks, tweens).
// Unnamed stock shapes can't be hidden from here, so where one sits under live text (the add field's plate, the
// settings card, the radio rings, the row highlight) a clip is swapped in just above it and covers it.
// RESKINS (hud/reskin.py) still repaints the stock art that stays visible: the sockets' navy rims and the
// minimized button.

// The panel's geometry, in panel_mc's coordinates (the stock frame spans x -103..103 and y 0..height).
var FD_LEFT = -106;
var FD_RIGHT = 106;
var FD_TOP = -8;
var FD_BOTTOM_PAD = 4;
var FD_RADIUS = 22;
var FD_HEADER_Y = -1;
var FD_HEADER_H = 54;
// The list starts higher than the stock 80 and is a little wider than the stock 176.
var FD_LIST_Y = 62;
var FD_LIST_W = 180;
var FD_ROW_H = 28;
// The flip side sits centered under the front (stock -97).
var FD_FLIP_X = -101;
// The pane buttons, centered along the bottom.
var FD_DOCK_STEP = 41;

var FD_GOLD = 0xFFC531;
var FD_GOLD_LIGHT = 0xFFE08A;
var FD_GOLD_DARK = 0xF5A623;
var FD_RIM = 0x9DB8FF;
var FD_SOFT = 0xC9D6FF;
var FD_STATUS = 0x9FB8F2;
var FD_INK = 0x3A2400;
var FD_RECESS = 0x0A1440;
var FD_GREEN = 0x5BE37D;
var FD_AMBER = 0xFFC531;
var FD_GREY = 0x8391B8;
var FD_RED = 0xFF7070;

// What each pane's add field does, shown in it while it's empty.
var FD_HINTS = {friendsPanel:"Add a friend by name",ignoredPanel:"Ignore someone by name",guildPanel:"Invite to your guild by name"};
// Titles until Lua sends its own (setCount).
var FD_TITLES = {friendsPanel:"My Friends",aquaintancePanel:"Acquaintances",ignoredPanel:"Ignored",guildPanel:"Guild"};
// Shown in the list while a pane has nobody in it.
var FD_EMPTY = {friendsPanel:"No friends yet.\nAdd one by name below!",aquaintancePanel:"Nobody met yet.",ignoredPanel:"You're not ignoring anyone.",guildPanel:"No guild members yet."};

var fdFront;
var fdBack;
var fdCover;
var fdRowWidthNow = 0;

// --- drawing helpers -------------------------------------------------------------------

function fdRoundRect(mc, x, y, w, h, r)
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

// Square top corners, rounded bottom ones.
function fdRoundRectBottom(mc, x, y, w, h, r)
{
   mc.moveTo(x,y);
   mc.lineTo(x + w,y);
   mc.lineTo(x + w,y + h - r);
   mc.curveTo(x + w,y + h,x + w - r,y + h);
   mc.lineTo(x + r,y + h);
   mc.curveTo(x,y + h,x,y + h - r);
   mc.lineTo(x,y);
}

function fdGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   fdRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function fdCircle(mc, x, y, r)
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

function fdText(mc, name, txt, x, y, w, h, size, color, align)
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

// Shrinks a one-line field's text until it fits its width.
function fdFitText(t, size, minSize)
{
   var f = new TextFormat();
   f.size = size;
   t.setTextFormat(f);
   while(t.textWidth > t._width - 4 && size > minSize)
   {
      size--;
      f.size = size;
      t.setTextFormat(f);
   }
}

function fdSparkle(mc, x, y, r, alpha)
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

function fdOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// A light frosted disc: the stock icons are made for light plates.
function fdFrosted(g, x, y, r, over)
{
   g.beginGradientFill("radial",over ? [0xFFFFFF,0xEEF4FF,0xC2D4FA] : [0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.3,y:y - r * 1.5,w:r * 2.6,h:r * 2.6,r:0});
   fdCircle(g,x,y,r);
   g.endFill();
}

function fdGoldDisc(g, x, y, r)
{
   g.beginGradientFill("radial",[0xFFF3C4,FD_GOLD_LIGHT,FD_GOLD_DARK],[100,100,100],[0,110,255],{matrixType:"box",x:x - r * 1.3,y:y - r * 1.5,w:r * 2.6,h:r * 2.6,r:0});
   fdCircle(g,x,y,r);
   g.endFill();
}

function fdPresenceDot(g, x, y, color, glow)
{
   g.lineStyle();
   if(glow)
   {
      g.beginGradientFill("radial",[color,color],[55,0],[0,255],{matrixType:"box",x:x - 9,y:y - 9,w:18,h:18,r:0});
      fdCircle(g,x,y,9);
      g.endFill();
   }
   g.lineStyle(1.5,0x0A1030,70);
   g.beginGradientFill("radial",[0xFFFFFF,color,color],[100,100,100],[0,90,255],{matrixType:"box",x:x - 6,y:y - 7,w:11,h:11,r:0});
   fdCircle(g,x,y,4.5);
   g.endFill();
}

// A clip just above an unnamed timeline shape (timeline depth shapeDepth) and below the clip's other children:
// made at the bottom, then swapped into the shape's place (the shape goes to the bottom). If the swap doesn't
// take, the clip stays under the shape and the stock art shows instead.
function fdAboveShape(parent, name, shapeDepth)
{
   var mc = parent.createEmptyMovieClip(name,-16384);
   mc.swapDepths(shapeDepth - 16384);
   return mc;
}

// Dot color and status for a friend, guild member, acquaintance or ignored player.
function fdPresence(model)
{
   if(model.getLastOnline() == -1)
   {
      return [FD_RED,"Ignored"];
   }
   if(!model.getIsOnline())
   {
      return [FD_GREY,"Offline"];
   }
   if(model.getInEncounter())
   {
      return [FD_AMBER,"In a game"];
   }
   if(!model.getIsLocal())
   {
      return [FD_AMBER,"On another server"];
   }
   return [FD_GREEN,"Online"];
}

// --- the frame ---------------------------------------------------------------------------

function fdFrameBox(h)
{
   return {matrixType:"box",x:FD_LEFT,y:FD_TOP,w:FD_RIGHT - FD_LEFT,h:h + FD_BOTTOM_PAD - FD_TOP,r:Math.PI / 2};
}

function fdPaintFrame(g, h)
{
   g.lineStyle(2.5,FD_RIM,80);
   g.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],fdFrameBox(h));
   fdRoundRect(g,FD_LEFT,FD_TOP,FD_RIGHT - FD_LEFT,h + FD_BOTTOM_PAD - FD_TOP,FD_RADIUS);
   g.endFill();
   // The soft light from the top left; it fades out above the add field, so the cover there needn't repeat it.
   g.lineStyle();
   g.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:-190,y:-150,w:300,h:260,r:0});
   fdRoundRect(g,FD_LEFT + 2,FD_TOP + 2,FD_RIGHT - FD_LEFT - 4,h + FD_BOTTOM_PAD - FD_TOP - 4,FD_RADIUS - 2);
   g.endFill();
}

// The header bar: navy glass with a gold edge underneath and a few sparkles (fixed seed, same every time).
function fdPaintHeader(g, seed, height)
{
   fdGradientRect(g,-98,FD_HEADER_Y,196,height,16,FD_RECESS,FD_RECESS,45,FD_RIM,30,1);
   g.lineStyle();
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[14,0],[0,255],{matrixType:"box",x:0,y:FD_HEADER_Y,w:10,h:height / 2,r:Math.PI / 2});
   fdRoundRect(g,-95,FD_HEADER_Y + 2,190,height / 2,13);
   g.endFill();
   g.beginGradientFill("linear",[FD_GOLD,FD_GOLD,FD_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:-80,y:0,w:160,h:2,r:0});
   fdRoundRect(g,-80,FD_HEADER_Y + height - 2,160,2,1);
   g.endFill();
   var s = seed;
   var i;
   for(i = 0; i < 7; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = -40 + s % 90;
      s = (s * 1103515245 + 12345) % 2147483648;
      fdSparkle(g,x,FD_HEADER_Y + 5 + s % (height - 10),1 + s % 2,15 + s % 30);
   }
}

// A round socket in the header (the pane icon, the player's portrait).
function fdPaintSocket(g, x, y, r)
{
   g.lineStyle();
   g.beginFill(0x050A24,35);
   fdCircle(g,x,y + 2,r + 2);
   g.endFill();
   g.lineStyle(2,FD_GOLD_LIGHT,90);
   fdFrosted(g,x,y,r,false);
   g.lineStyle(1,0xFFF3C4,50);
   fdCircle(g,x,y,r + 3);
}

// --- the front: header, list, add field and pane buttons -----------------------------------

function fdInputShown()
{
   return panel_mc.frontSide_mc.bottomWithTextfield._visible;
}

// ExpandablePanel.setHeight sizes the list for a top at 80; the list starts at FD_LIST_Y instead.
function fdListHeight(h)
{
   return h - (fdInputShown() ? 165 : 130) + (80 - FD_LIST_Y);
}

function fdPaintFrontBody(g, h)
{
   var bottom = FD_LIST_Y + fdListHeight(h);
   fdGradientRect(g,-95,FD_LIST_Y - 4,190,bottom - FD_LIST_Y + 8,12,FD_RECESS,FD_RECESS,40,FD_RIM,22,1);
   fdGradientRect(g,-102,h - 42,204,39,19.5,FD_RECESS,FD_RECESS,45,FD_RIM,22,1);
}

function fdDrawFront()
{
   var h = panel_mc.getHeight();
   var g = fdFront.bg;
   g.clear();
   fdPaintFrame(g,h);
   fdPaintHeader(g,2024,FD_HEADER_H);
   fdPaintSocket(g,-73,FD_HEADER_Y + FD_HEADER_H / 2,19);
   fdPaintFrontBody(g,h);
   fdFront.empty_txt._y = FD_LIST_Y + 14;
   fdDrawCover();
   fdUpdateEmpty();
}

// The add field's stock plate (an unnamed shape under the text field) is covered by a copy of the frame with
// the new field on it. It keeps within the plate's bounds: ExpandablePanel.setHeight places the plate by its height.
function fdDrawCover()
{
   if(fdCover == undefined)
   {
      return;
   }
   var bar = panel_mc.frontSide_mc.bottomWithTextfield;
   var h = panel_mc.getHeight();
   // Drawn in the panel's coordinates.
   fdCover._x = 103;
   fdCover._y = - bar._y;
   var top = bar._y;
   var g = fdCover.bg;
   g.clear();
   g.lineStyle();
   g.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],fdFrameBox(h));
   fdRoundRectBottom(g,-103,top,206,h - top,19);
   g.endFill();

   // The bottom of the list's recess, then the pane buttons' strip, as under the plate.
   var bottom = FD_LIST_Y + fdListHeight(h) + 4;
   if(bottom > top + 12)
   {
      g.beginFill(FD_RECESS,40);
      fdRoundRectBottom(g,-95,top,190,bottom - top,12);
      g.endFill();
      g.lineStyle(1,FD_RIM,22);
      g.moveTo(-95,top + 1);
      g.lineTo(-95,bottom - 12);
      g.curveTo(-95,bottom,-83,bottom);
      g.lineTo(83,bottom);
      g.curveTo(95,bottom,95,bottom - 12);
      g.lineTo(95,top + 1);
   }
   fdGradientRect(g,-102,h - 42,204,39,19.5,FD_RECESS,FD_RECESS,45,FD_RIM,22,1);

   // The field: a rounded recess, rimmed in gold while it has the focus.
   var focus = fdCover.focused;
   var f = fdCover.field;
   f.clear();
   fdGradientRect(f,-95,top + 17,152,32,16,0x060D33,0x0F1B4D,75,focus ? FD_GOLD_LIGHT : FD_RIM,focus ? 100 : 40,focus ? 2 : 1.5);
   f.lineStyle();
   f.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[0,10],[0,255],{matrixType:"box",x:0,y:top + 33,w:10,h:16,r:Math.PI / 2});
   fdRoundRect(f,-92,top + 33,146,14,7);
   f.endFill();
   f.filters = focus ? [new flash.filters.GlowFilter(FD_GOLD,0.4,8,8,2,2)] : [];
   fdCover.hint_txt._y = top + 24;
}

function fdUpdateHint()
{
   if(fdCover == undefined)
   {
      return;
   }
   var field = panel_mc.frontSide_mc.bottomWithTextfield.textEntry_txt;
   var hint = FD_HINTS[panel_mc.getCurrentPane()];
   fdCover.hint_txt.text = hint == undefined ? "" : hint;
   fdCover.hint_txt._visible = field.text.length == 0 && !fdCover.focused;
}

function fdBuildInput()
{
   var bar = panel_mc.frontSide_mc.bottomWithTextfield;
   fdCover = fdAboveShape(bar,"sanctuaryCover",1);
   fdCover.bg = fdCover.createEmptyMovieClip("bg",1);
   fdCover.field = fdCover.createEmptyMovieClip("field",2);
   fdText(fdCover,"hint_txt","",-84,0,128,20,12,0x8FA6DC,"left");
   fdCover.focused = false;

   var field = bar.textEntry_txt;
   field.textColor = 0xFFFFFF;
   var setFocus = field.onSetFocus;
   field.onSetFocus = function(old)
   {
      setFocus.apply(this,arguments);
      fdCover.focused = true;
      fdDrawCover();
      fdUpdateHint();
   };
   field.onKillFocus = function(next)
   {
      fdCover.focused = false;
      fdDrawCover();
      fdUpdateHint();
   };
   var typing = new Object();
   typing.onChanged = function()
   {
      fdUpdateHint();
   };
   field.addListener(typing);

   // Sending clears the field from code, which doesn't fire onChanged.
   var sendCommand = bar.sendCommand;
   bar.sendCommand = function()
   {
      var result = sendCommand.apply(this,arguments);
      fdUpdateHint();
      return result;
   };

   // The add button, a gold disc at the field's end.
   bar.button_mc._x = 165;
   bar.button_mc._y = 19;
   fdSkinSquare(bar.button_mc,true);
}

// The header's title and online count pill (the stock title is "My Friends(3/12)" in one field).
function fdDrawHeaderText()
{
   var pane = panel_mc.getCurrentPane();
   var title = panel_mc.m_countOfText;
   if(title == undefined || title.length == 0)
   {
      title = FD_TITLES[pane];
   }
   var t = fdFront.title_txt;
   t.text = title == undefined ? "" : title;
   fdFitText(t,16,10);

   var model = panel_mc.m_listModel;
   var total = model.getItemCount();
   var online = model.getOnlineCount();
   var pill = fdFront.pill;
   var label;
   var dot = true;
   if(pane == ExpandablePanel.IGNORED_PANEL)
   {
      label = total + " ignored";
      dot = false;
   }
   else if(pane == ExpandablePanel.GUILD_PANEL && !_root.getPlayerIsInGuild())
   {
      label = "";
   }
   else
   {
      label = online + " / " + total + " online";
   }
   pill._visible = label.length > 0;
   pill.label_txt.text = label;
   pill.label_txt._x = dot ? 16 : 6;
   var w = pill.label_txt.textWidth + (dot ? 26 : 16);
   var g = pill.bg;
   g.clear();
   fdGradientRect(g,0,0,w,17,8.5,FD_RECESS,FD_RECESS,60,FD_RIM,35,1);
   if(dot)
   {
      fdPresenceDot(g,9,8.5,online > 0 ? FD_GREEN : FD_GREY,online > 0);
   }
}

function fdUpdateEmpty()
{
   var pane = panel_mc.getCurrentPane();
   var t = fdFront.empty_txt;
   var guildSignup = panel_mc.m_guildPage._visible;
   t.text = FD_EMPTY[pane] == undefined ? "" : FD_EMPTY[pane];
   t._visible = panel_mc.m_listModel.getItemCount() == 0 && !guildSignup;
}

function fdBuildFront()
{
   var front = panel_mc.frontSide_mc;
   front.background_mc._alpha = 0;
   fdFront = front.createEmptyMovieClip("sanctuaryFront",-16384);
   // Drawn in the panel's coordinates.
   fdFront._x = 103;
   fdFront.bg = fdFront.createEmptyMovieClip("bg",1);
   fdFront.bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   var title = fdText(fdFront,"title_txt","",-48,FD_HEADER_Y + 4,98,24,16,FD_GOLD_LIGHT,"left");
   title.filters = fdOutline();
   var pill = fdFront.pill = fdFront.createEmptyMovieClip("pill",fdFront.getNextHighestDepth());
   pill._x = -48;
   pill._y = FD_HEADER_Y + 29;
   pill.bg = pill.createEmptyMovieClip("bg",1);
   fdText(pill,"label_txt","",16,0,120,18,11,FD_SOFT,"left");
   var empty = fdText(fdFront,"empty_txt","",-88,FD_LIST_Y + 14,176,40,12,FD_SOFT);
   empty.multiline = true;
   empty.wordWrap = true;
   empty.filters = fdOutline();

   // The stock header: its plate is the expand rollover area, and the title is drawn anew above.
   panel_mc.expand_btn._alpha = 0;
   panel_mc.panel_title_txt._alpha = 0;
   panel_mc.headerIcon_mc._xscale = panel_mc.headerIcon_mc._yscale = 76;
   panel_mc.headerIcon_mc._x = -90;
   panel_mc.headerIcon_mc._y = FD_HEADER_Y + FD_HEADER_H / 2 - 17;

   // The collapse button, a smaller socket at the header's right.
   var centerer = panel_mc.centerer_mc;
   centerer._x = 76;
   centerer._y = FD_HEADER_Y + FD_HEADER_H / 2;
   centerer.minimize_btn._xscale = centerer.minimize_btn._yscale = 86;
   centerer.minimize_btn._x = -22 * 0.86;
   centerer.minimize_btn._y = -22.5 * 0.86;
   fdSkinCircle(centerer.minimize_btn,24);

   var tabs = [panel_mc.m_friends_btn,panel_mc.m_aquaintances_btn,panel_mc.m_guild_btn,panel_mc.m_ignored_btn,panel_mc.m_settings_btn];
   var i;
   for(i = 0; i < tabs.length; i++)
   {
      fdSkinCircle(tabs[i],26);
   }
   fdBuildInput();
}

// The pane buttons, evenly spaced and centered (the stock row hangs off both edges).
function fdLayoutDock()
{
   var buttons = [panel_mc.m_friends_btn,panel_mc.m_aquaintances_btn];
   if(panel_mc.m_guild_btn._visible)
   {
      buttons.push(panel_mc.m_guild_btn);
   }
   buttons.push(panel_mc.m_ignored_btn,panel_mc.m_settings_btn);
   var i;
   for(i = 0; i < buttons.length; i++)
   {
      // frontSide_mc sits at x -103; the buttons' centers are at x 22.
      buttons[i]._x = (i - (buttons.length - 1) / 2) * FD_DOCK_STEP - 22 + 103;
   }
}

// The open pane's button is gold.
function fdSyncTabs()
{
   var pane = panel_mc.getCurrentPane();
   var tabs = [[panel_mc.m_friends_btn,ExpandablePanel.FRIENDS_PANEL],[panel_mc.m_aquaintances_btn,ExpandablePanel.AQUAINTANCE_PANEL],[panel_mc.m_guild_btn,ExpandablePanel.GUILD_PANEL],[panel_mc.m_ignored_btn,ExpandablePanel.IGNORED_PANEL]];
   var i;
   for(i = 0; i < tabs.length; i++)
   {
      tabs[i][0].fdSelected = tabs[i][1] == pane;
      fdDrawButton(tabs[i][0]);
   }
   fdUpdateHint();
   fdDrawHeaderText();
   fdUpdateEmpty();
}

// --- buttons -----------------------------------------------------------------------------

// Scales a button's icons to fit a box and centers them on its socket. FRIconButton moves the icons by
// m_iconUpY and m_iconDownY, so those follow.
function fdFitIcon(btn)
{
   var icons = [btn.m_icon_up,btn.m_icon_over];
   var i;
   for(i = 0; i < icons.length; i++)
   {
      var icon = icons[i];
      if(icon == undefined)
      {
         continue;
      }
      icon._xscale = icon._yscale = 100;
      var b = icon.getBounds(icon);
      var size = Math.max(b.xMax - b.xMin,b.yMax - b.yMin);
      var scale = size > btn.fdIconBox ? btn.fdIconBox / size : 1;
      icon._xscale = icon._yscale = scale * 100;
      icon._x = btn.fdCx - (b.xMin + b.xMax) / 2 * scale;
      icon._y = btn.fdCy - (b.yMin + b.yMax) / 2 * scale;
      btn.m_iconUpY = icon._y;
      btn.m_iconDownY = icon._y + 1;
   }
}

// The round buttons (circleButton): a frosted disc inside the stock navy rim. The skin goes at the free timeline
// depth 3, above the rim (depth 1) and under the (now invisible) state plates and the icons.
function fdSkinCircle(btn, iconBox)
{
   btn.up_mc._alpha = btn.over_mc._alpha = btn.down_mc._alpha = 0;
   btn.fdSkin = btn.createEmptyMovieClip("sanctuarySkin",3 - 16384);
   btn.fdCx = 22;
   btn.fdCy = 22.5;
   btn.fdRadius = 16.5;
   btn.fdIconBox = iconBox;
   btn.fdSkin._x = btn.fdCx;
   btn.fdSkin._y = btn.fdCy;
   btn.fdState = "up";
   fdFitIcon(btn);
   fdDrawButton(btn);
}

// The square buttons (squareButton: the add field's button, a player's actions): a frosted rounded socket, or
// a gold disc for the add button.
function fdSkinSquare(btn, gold)
{
   btn.up_mc._alpha = btn.over_mc._alpha = btn.down_mc._alpha = 0;
   btn.fdSkin = btn.createEmptyMovieClip("sanctuarySkin",-16384);
   btn.fdSquare = !gold;
   btn.fdGold = gold;
   btn.fdCx = 15;
   btn.fdCy = 14;
   btn.fdRadius = gold ? 15 : 13;
   btn.fdIconBox = gold ? 20 : 21;
   btn.fdSkin._x = btn.fdCx;
   btn.fdSkin._y = btn.fdCy;
   btn.fdState = "up";
   fdFitIcon(btn);
   fdDrawButton(btn);
}

function fdDrawButton(btn)
{
   var s = btn.fdSkin;
   if(s == undefined)
   {
      return;
   }
   var over = btn.fdState == "over" || btn.fdState == "down";
   var r = btn.fdRadius;
   var gold = btn.fdGold || btn.fdSelected;
   s.clear();
   if(btn.fdSquare)
   {
      s.lineStyle();
      s.beginFill(0x050A24,35);
      fdRoundRect(s,- r,- r + 2,r * 2,r * 2,8);
      s.endFill();
      s.lineStyle(over ? 2 : 1.5,over ? FD_GOLD_LIGHT : FD_RIM,over ? 100 : 70);
      s.beginGradientFill("radial",over ? [0xFFFFFF,0xEEF4FF,0xC2D4FA] : [0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:- r * 1.3,y:- r * 1.5,w:r * 2.6,h:r * 2.6,r:0});
      fdRoundRect(s,- r,- r,r * 2,r * 2,8);
      s.endFill();
   }
   else
   {
      if(gold)
      {
         s.lineStyle(1.5,0xFFF3C4,100);
         fdGoldDisc(s,0,0,r);
      }
      else
      {
         s.lineStyle(over ? 2 : 1.5,over ? FD_GOLD_LIGHT : 0xFFFFFF,over ? 100 : 60);
         fdFrosted(s,0,0,r,over);
      }
   }
   // Glassy highlight across the top.
   s.lineStyle();
   s.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[gold ? 45 : 60,0],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r,r:Math.PI / 2});
   fdRoundRect(s,- r * 0.7,- r + 2,r * 1.4,r * 0.75,r * 0.37);
   s.endFill();

   if(over)
   {
      s.filters = [new flash.filters.GlowFilter(FD_GOLD,0.85,12,12,2,2)];
   }
   else if(btn.fdSelected)
   {
      s.filters = [new flash.filters.GlowFilter(FD_GOLD,0.5,10,10,2,2)];
   }
   else
   {
      s.filters = [];
   }
   s._y = btn.fdCy + (btn.fdState == "down" ? 1 : 0);
   if(over != btn.fdWasOver)
   {
      btn.fdWasOver = over;
      if(over)
      {
         gs.TweenLite.to(s,0.6,{_xscale:110,_yscale:110,ease:mx.transitions.easing.Elastic.easeOut});
      }
      else
      {
         gs.TweenLite.to(s,0.2,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
      }
   }
}

// --- rows --------------------------------------------------------------------------------

function fdRowWidth()
{
   var list = panel_mc.m_listView;
   var bar = list.m_scrollbar;
   return FD_LIST_W - (bar != undefined && bar._visible ? bar._width + 3 : 0);
}

function fdIsSelected(view)
{
   return view.m_itemModel != undefined && view.m_itemModel == panel_mc.m_currentCharacter;
}

// A row's art: its glass bar underneath, its hover look in the stock highlight (whose _alpha the stock code
// turns on over the row and blinks when a friend comes online), and the dot and status between the two.
function fdStyleRow(view)
{
   view.fdRow = view.createEmptyMovieClip("sanctuaryRow",-16384);
   // Above the stock highlight (depth 1) and under the rank icon (3) and the name (5).
   view.fdInfo = view.createEmptyMovieClip("sanctuaryInfo",2 - 16384);
   fdText(view.fdInfo,"status_txt","",24,14,140,16,10,FD_STATUS,"left");
   view.fdHover = fdAboveShape(view.hilite_mc,"sanctuaryHover",1);
   var f = view.name_txt.getNewTextFormat();
   f.size = 13;
   view.name_txt.setNewTextFormat(f);
   view.name_txt._x = 24;
   view.name_txt._y = 0;
   view.name_txt.filters = fdOutline();
}

function fdDrawRow(view)
{
   var w = fdRowWidth();
   var model = view.m_itemModel;
   var presence = fdPresence(model);
   var online = model.getIsOnline();
   var selected = fdIsSelected(view);

   var g = view.fdRow;
   g.clear();
   if(selected)
   {
      fdGradientRect(g,0,1,w,FD_ROW_H - 2,9,0x34509F,0x1C2A6C,100,FD_GOLD,90,1.5);
      g.lineStyle();
      g.beginGradientFill("radial",[FD_GOLD,FD_GOLD],[30,0],[0,255],{matrixType:"box",x:-30,y:-20,w:120,h:68,r:0});
      fdRoundRect(g,1,2,w - 2,FD_ROW_H - 4,8);
      g.endFill();
   }
   else
   {
      fdGradientRect(g,0,1,w,FD_ROW_H - 2,9,0x284A9E,0x162660,online ? 75 : 40,FD_RIM,online ? 28 : 16,1);
   }
   g.lineStyle();
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[online ? 12 : 6,0],[0,255],{matrixType:"box",x:0,y:2,w:10,h:12,r:Math.PI / 2});
   fdRoundRect(g,3,2,w - 6,11,6);
   g.endFill();

   // The hover look, in the stock highlight's coordinates: the highlight is moved and stretched so its own bar
   // stays inside this one's rounded corners, and the hover clip undoes the stretch.
   var hilite = view.hilite_mc;
   var sx = (w - 10) / 160;
   hilite._x = 5;
   hilite._y = 4;
   hilite._xscale = sx * 100;
   hilite._yscale = 100;
   var hv = view.fdHover;
   hv._xscale = 100 / sx;
   hv._x = -5 / sx;
   hv._y = -4;
   hv.clear();
   fdGradientRect(hv,0,1,w,FD_ROW_H - 2,9,0x3F66D6,0x1D307A,100,FD_GOLD_LIGHT,100,1.5);
   hv.lineStyle();
   hv.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[20,0],[0,255],{matrixType:"box",x:0,y:2,w:10,h:12,r:Math.PI / 2});
   fdRoundRect(hv,3,2,w - 6,11,6);
   hv.endFill();
   hv.filters = [new flash.filters.GlowFilter(FD_GOLD,0.5,10,10,2,2)];

   var info = view.fdInfo;
   info.clear();
   fdPresenceDot(info,13,FD_ROW_H / 2,presence[0],online);
   var rank = model.getGuildRank() != undefined;
   info.status_txt.text = presence[1];
   info.status_txt._width = w - (rank ? 48 : 30);
   info.status_txt.textColor = online ? FD_STATUS : 0x7F8DB8;

   // Guild ranks show at the row's end; everyone else's bullet goes (the dot says it).
   view.m_bullet._alpha = rank ? 100 : 0;
   view.m_bullet._x = w - 19;
   view.m_bullet._y = 7;
}

function fdRefreshRows()
{
   var list = panel_mc.m_listView;
   var w = fdRowWidth();
   if(w != fdRowWidthNow)
   {
      // The names are fitted to their fields when a model is set; reloadModel sets them all again.
      fdRowWidthNow = w;
      list.reloadModel();
      return;
   }
   var i;
   for(i = 0; i < list.m_items.length; i++)
   {
      var view = list.m_items[i];
      if(view.fdRow != undefined && view.m_itemModel != undefined)
      {
         fdDrawRow(view);
      }
   }
}

// The list's scrollbar (the stock FRScrollBarV2 library): thinner, on a navy channel with a gold thumb.
function fdStyleScrollbar(bar)
{
   if(bar == undefined)
   {
      return;
   }
   if(!bar.fdStyled)
   {
      bar.fdStyled = true;
      bar._xscale = 60;
      bar.background_mc._alpha = 0;
      new Color(bar.channel_mc).setTransform({ra:0,rb:10,ga:0,gb:20,ba:0,bb:64,aa:60,ab:0});
      new Color(bar.thumb_mc).setTransform({ra:40,rb:150,ga:40,gb:116,ba:40,bb:18,aa:100,ab:0});
      bar.upArrow_btn._alpha = bar.downArrow_btn._alpha = 70;
      bar.setItemHeight(FD_ROW_H);
   }
   bar._x = panel_mc.m_listView.mask_mc._width - bar._width;
}

// --- the flip side: a player's card and the settings ---------------------------------------

function fdDrawBack()
{
   var h = panel_mc.getHeight();
   var g = fdBack.bg;
   g.clear();
   fdPaintFrame(g,h);
   fdPaintHeader(g,77,FD_HEADER_H);
   fdDrawSettings();
}

function fdBuildBack()
{
   var flip = panel_mc.flipSide_mc;
   flip._x = FD_FLIP_X;
   flip.background_mc._alpha = 0;
   fdBack = flip.createEmptyMovieClip("sanctuaryBack",-16384);
   fdBack._x = - FD_FLIP_X;
   fdBack.bg = fdBack.createEmptyMovieClip("bg",1);
   fdBack.bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   // The back button, at the right end of the bottom.
   panel_mc.m_back_btn._x = 82 - 22 - FD_FLIP_X;
   fdSkinCircle(panel_mc.m_back_btn,26);
   fdBuildCharacterPage();
   fdBuildSettings();
}

// The player's card: the portrait in a round gold-rimmed socket in the header with the name beside it, the
// actions as frosted sockets, and the job and the activity (with the presence dot) on navy glass.
function fdBuildCharacterPage()
{
   var page = panel_mc.flipSide_mc.characterPage_mc;
   page.pageBorder_mc._alpha = 0;
   page.activityBorder_mc._alpha = 0;
   var under = page.fdUnder = page.createEmptyMovieClip("sanctuaryCard",-16384);
   under._x = - FD_FLIP_X;
   under.bg = under.createEmptyMovieClip("bg",1);

   // The page's coordinates are the panel's plus 101.
   var cx = -68 - FD_FLIP_X;
   var cy = FD_HEADER_Y + FD_HEADER_H / 2;
   page.portrait_mc._xscale = page.portrait_mc._yscale = 72;
   page.portrait_mc._x = cx - 23;
   page.portrait_mc._y = cy - 23;
   var mask = page.createEmptyMovieClip("sanctuaryPortraitMask",page.getNextHighestDepth());
   mask.beginFill(0,100);
   fdCircle(mask,cx,cy,21);
   mask.endFill();
   page.portrait_mc.setMask(mask);

   page.characterName_txt._x = -38 - FD_FLIP_X;
   page.characterName_txt._y = FD_HEADER_Y + 16;
   page.characterName_txt._width = 128;
   page.characterName_txt.filters = fdOutline();
   page.jobTitle_txt.filters = page.jobLevel_txt.filters = page.description_txt.filters = fdOutline();
   fdSkinSquare(page.addFriend_btn,false);

   var init = page.init;
   page.init = function(character)
   {
      var result = init.apply(this,arguments);
      fdDrawCharacterPage();
      return result;
   };
}

function fdDrawCharacterPage()
{
   var page = panel_mc.flipSide_mc.characterPage_mc;
   var model = page.m_character;
   var under = page.fdUnder;
   var g = under.bg;
   g.clear();
   if(model == undefined)
   {
      return;
   }
   var presence = fdPresence(model);
   fdPaintSocket(g,-68,FD_HEADER_Y + FD_HEADER_H / 2,22);

   // The name, left-aligned beside the portrait (the stock field centers it).
   var f = new TextFormat();
   f.align = "left";
   f.color = 0xFFFFFF;
   page.characterName_txt.setTextFormat(f);

   // The job card and the activity pill, where the stock code put its borders.
   var x = page.pageBorder_mc._x + FD_FLIP_X;
   fdGradientRect(g,x,page.pageBorder_mc._y,162,page.pageBorder_mc._height,14,FD_RECESS,FD_RECESS,40,FD_RIM,25,1);
   if(page.jobImages_mc._alpha > 0)
   {
      g.lineStyle();
      g.beginGradientFill("radial",[0xFFFFFF,0xFFFFFF],[30,0],[0,255],{matrixType:"box",x:x + 6,y:page.pageBorder_mc._y + 2,w:150,h:90,r:0});
      fdRoundRect(g,x + 4,page.pageBorder_mc._y + 4,154,80,10);
      g.endFill();
   }
   var ax = page.activityBorder_mc._x + FD_FLIP_X;
   var ay = page.activityBorder_mc._y;
   fdGradientRect(g,ax,ay,138,37,12,FD_RECESS,FD_RECESS,60,FD_RIM,30,1);
   fdPresenceDot(g,ax + 14,ay + 18.5,presence[0],model.getIsOnline());
   page.description_txt.textColor = FD_SOFT;
}

// The settings: the stock card (an unnamed shape at depth 4, with the speech bubbles) is covered by a copy of the
// frame with a header and two navy cards for the size and show options. The radios get navy sockets.
function fdBuildSettings()
{
   var settings = panel_mc.flipSide_mc.settingsPage_mc;
   // One pixel down, so the speech bubbles at the card's top stay inside the frame.
   settings._y = 1;
   var cover = settings.fdCover = fdAboveShape(settings,"sanctuarySettings",4);
   cover._x = 103;
   cover._y = -1;
   cover.bg = cover.createEmptyMovieClip("bg",1);
   var icon = cover.attachMovie("settings_icon_up","icon",2);
   icon._x = -14;
   icon._y = FD_HEADER_Y + 8;
   var radios = ["group1_r1","group1_r2","group1_r3","group1_r4","group2_r1","group2_r2"];
   var i;
   for(i = 0; i < radios.length; i++)
   {
      var radio = settings[radios[i]];
      radio.fill_mc._alpha = 0;
      radio.fdRing = fdAboveShape(radio,"sanctuaryRing",2);
      fdDrawRadio(radio);
   }
}

function fdDrawSettings()
{
   var cover = panel_mc.flipSide_mc.settingsPage_mc.fdCover;
   if(cover == undefined)
   {
      return;
   }
   var h = panel_mc.getHeight();
   var g = cover.bg;
   g.clear();
   fdPaintFrame(g,h);
   // Tall enough for the stock "Settings" title under the wrench.
   fdPaintHeader(g,303,74);
   g.lineStyle(2,FD_GOLD_LIGHT,90);
   fdFrosted(g,0,FD_HEADER_Y + 22,17,false);
   fdGradientRect(g,-90,78,180,128,14,FD_RECESS,FD_RECESS,40,FD_RIM,25,1);
   fdGradientRect(g,-90,209,180,82,14,FD_RECESS,FD_RECESS,40,FD_RIM,25,1);
}

function fdDrawRadio(radio)
{
   var g = radio.fdRing;
   if(g == undefined)
   {
      return;
   }
   g.clear();
   g.lineStyle(1.5,FD_RIM,70);
   g.beginGradientFill("radial",[0x1D307A,0x0A1440],[100,100],[0,255],{matrixType:"box",x:-1,y:-1,w:21,h:21,r:0});
   fdCircle(g,9.5,9.5,10.5);
   g.endFill();
   if(radio.fill_mc._visible)
   {
      g.lineStyle(1,0xFFF3C4,100);
      fdGoldDisc(g,9.5,9.5,6);
      g.filters = [new flash.filters.GlowFilter(FD_GOLD,0.6,8,8,2,2)];
   }
   else
   {
      g.filters = [];
   }
}

// --- the hover card beside the list --------------------------------------------------------

// The card's tip is at the flyout's origin, which is (224, 57) in largeFlyout_mc.
function fdBuildFlyout()
{
   var card = listFlyout_mc.largeFlyout_mc;
   card.background_mc._alpha = 0;
   // The stock name plate is an unnamed clip at timeline depth 6.
   card.getInstanceAtDepth(6 - 16384)._alpha = 0;
   var under = card.fdUnder = card.createEmptyMovieClip("sanctuaryCard",-16384);
   var g = under.createEmptyMovieClip("bg",1);
   g.filters = [new flash.filters.DropShadowFilter(5,90,0x050A24,0.6,14,14,1,2)];
   fdGradientRect(g,6,10,206,130,18,0x2E4FB0,0x141F55,100,FD_RIM,80,2.5);
   // The pointer at the panel, joined to the card without a seam.
   g.lineStyle(2.5,FD_RIM,80);
   g.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:6,y:10,w:206,h:130,r:Math.PI / 2});
   g.moveTo(211,45);
   g.lineTo(224,57);
   g.lineTo(211,69);
   g.endFill();
   g.lineStyle();
   g.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:6,y:10,w:206,h:130,r:Math.PI / 2});
   g.moveTo(208,44);
   g.lineTo(212,47);
   g.lineTo(212,67);
   g.lineTo(208,70);
   g.lineTo(208,44);
   g.endFill();
   g.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[30,0],[0,255],{matrixType:"box",x:-60,y:-60,w:220,h:180,r:0});
   fdRoundRect(g,8,12,202,126,16);
   g.endFill();
   // The name strip, and the portrait's socket poking out of the top left corner like the stock one.
   fdGradientRect(g,46,16,160,42,12,FD_RECESS,FD_RECESS,45,FD_RIM,30,1);
   fdPaintSocket(g,12,16,30);
   var mask = card.createEmptyMovieClip("sanctuaryPortraitMask",card.getNextHighestDepth());
   mask.beginFill(0,100);
   fdCircle(mask,12,16,29);
   mask.endFill();
   card.portrait_mc.setMask(mask);

   under.dot = under.createEmptyMovieClip("dot",2);
   fdText(under,"status_txt","",70,15,130,16,10,FD_STATUS,"left");
   card.characterName_txt._x = 70;
   card.characterName_txt._width = 132;
   card.characterName_txt.filters = card.title_txt.filters = fdOutline();

   var setCharacter = listFlyout_mc.setCharacter;
   listFlyout_mc.setCharacter = function(character)
   {
      var result = setCharacter.apply(this,arguments);
      fdDrawFlyout();
      return result;
   };
}

function fdDrawFlyout()
{
   var card = listFlyout_mc.largeFlyout_mc;
   var model = listFlyout_mc.m_character;
   if(model == undefined)
   {
      return;
   }
   var presence = fdPresence(model);
   var d = card.fdUnder.dot;
   d.clear();
   fdPresenceDot(d,60,41,presence[0],model.getIsOnline());
   // Guild members show their rank's title there instead.
   card.fdUnder.status_txt._visible = !card.guildIcon._visible;
   card.fdUnder.status_txt.text = presence[1];
   card.title_txt.textColor = FD_GOLD_LIGHT;
}

// --- stock classes -------------------------------------------------------------------------

function fdWrapButtonState(name, state)
{
   var stock = FRIconButton.prototype[name];
   FRIconButton.prototype[name] = function()
   {
      var result = stock.apply(this,arguments);
      // The stock code ignores over and down on disabled buttons.
      if(this.fdSkin != undefined && (state == "up" || this.m_isEnabled))
      {
         this.fdState = state;
         fdDrawButton(this);
      }
      return result;
   };
}

function fdPatchClasses()
{
   fdWrapButtonState("setStateUp","up");
   fdWrapButtonState("setStateOver","over");
   fdWrapButtonState("setStateDown","down");

   // A new icon (the add button's per pane, a player's add/remove friend) is fitted to its socket.
   var setIcon = FRIconButton.prototype.setIcon;
   FRIconButton.prototype.setIcon = function()
   {
      var result = setIcon.apply(this,arguments);
      if(this.fdSkin != undefined)
      {
         fdFitIcon(this);
      }
      return result;
   };

   // A player's actions (on the hover card and the flip side) are made one by one; each gets a socket.
   var makeButton = ButtonModule.prototype.makeButton;
   ButtonModule.prototype.makeButton = function()
   {
      var result = makeButton.apply(this,arguments);
      var btn = this.m_buttonDeleteArray[this.m_buttonDeleteArray.length - 1];
      if(btn != undefined && btn.fdSkin == undefined)
      {
         fdSkinSquare(btn,false);
      }
      return result;
   };

   // Names are white (the stock orange marked players on another server; their dot is amber now).
   FRCharacterModel.prototype.getNameFormat = function()
   {
      var f = new TextFormat();
      f.color = 0xFFFFFF;
      return f;
   };

   var proto = CharacterItemView.prototype;
   var setModel = proto.setModel;
   proto.setModel = function(itemModel)
   {
      if(this.fdRow == undefined)
      {
         fdStyleRow(this);
      }
      return setModel.apply(this,arguments);
   };
   var reDraw = proto.reDraw;
   proto.reDraw = function()
   {
      // Fitted (and shortened with "...") to the width it has here.
      var rank = this.m_itemModel.getGuildRank() != undefined;
      this.name_txt._width = fdRowWidth() - (rank ? 48 : 30);
      var result = reDraw.apply(this,arguments);
      // Offline rows are dimmed by their colors instead of the stock half alpha.
      this._alpha = 100;
      this.name_txt.textColor = this.m_itemModel.getIsOnline() ? 0xFFFFFF : 0xB4C3E8;
      if(this.fdRow != undefined)
      {
         fdDrawRow(this);
      }
      return result;
   };

   var setFill = FRRadioButton.prototype.setFill;
   FRRadioButton.prototype.setFill = function(f)
   {
      var result = setFill.apply(this,arguments);
      fdDrawRadio(this);
      return result;
   };

   // The guild signup's Create Guild button, a gold pill (its plates are unnamed shapes under the label).
   var showGoodMsg = GuildPage.prototype.showGoodMsg;
   GuildPage.prototype.showGoodMsg = function()
   {
      var result = showGoodMsg.apply(this,arguments);
      if(this.button_mc != undefined && !this.button_mc.fdStyled)
      {
         this.button_mc.fdStyled = true;
         var states = [this.button_mc.up_mc,this.button_mc.over_mc,this.button_mc.down_mc];
         var i;
         for(i = 0; i < states.length; i++)
         {
            var pill = fdAboveShape(states[i],"sanctuaryPill",1);
            var top = i == 1 ? 0xFFF0B8 : FD_GOLD_LIGHT;
            fdGradientRect(pill,0,i == 2 ? 1 : 0,128,34,10,top,FD_GOLD_DARK,100,0xFFF3C4,100,1.5);
            if(i == 1)
            {
               pill.filters = [new flash.filters.GlowFilter(FD_GOLD,0.7,12,12,2,2)];
            }
            states[i].textField_txt.textColor = FD_INK;
         }
         this.body_txt.filters = fdOutline();
      }
      return result;
   };
}

// --- the panel ---------------------------------------------------------------------------

// Everything that follows the panel's height, after ExpandablePanel.setHeight.
function fdLayout()
{
   var h = panel_mc.getHeight();
   var list = panel_mc.m_listView;
   list._y = FD_LIST_Y;
   list.setHeight(fdListHeight(h));
   fdEnsureRows();
   list.setScrollbar();
   fdLayoutDock();
   fdDrawFront();
   fdDrawBack();
}

// CharacterList only makes row clips as items arrive; a taller list needs a few more to fill it.
function fdEnsureRows()
{
   var list = panel_mc.m_listView;
   var items = list.m_model.getItems();
   var added = false;
   while((list.m_items.length - 2) * FD_ROW_H < list.mask_mc._height && list.m_items.length < items.length)
   {
      list.onAddItem(items[0]);
      added = true;
   }
   if(added)
   {
      list.reloadModel();
   }
}

function fdWrapPanel()
{
   var setHeight = panel_mc.setHeight;
   panel_mc.setHeight = function(height)
   {
      var result = setHeight.apply(this,arguments);
      fdLayout();
      return result;
   };

   var setCount = panel_mc.setCount;
   panel_mc.setCount = function(str)
   {
      var result = setCount.apply(this,arguments);
      fdDrawHeaderText();
      fdUpdateEmpty();
      return result;
   };

   var panes = ["gotoFriends","gotoAquaintances","gotoIgnored","gotoGuild"];
   var i;
   for(i = 0; i < panes.length; i++)
   {
      fdWrapPane(panes[i]);
   }

   var showGuildButton = panel_mc.showGuildButton;
   panel_mc.showGuildButton = function()
   {
      var result = showGuildButton.apply(this,arguments);
      fdLayoutDock();
      return result;
   };

   var showGuildSignup = panel_mc.showGuildSignup;
   panel_mc.showGuildSignup = function(canCreate)
   {
      var result = showGuildSignup.apply(this,arguments);
      fdUpdateEmpty();
      return result;
   };

   var list = panel_mc.m_listView;
   var setScrollbar = list.setScrollbar;
   list.setScrollbar = function()
   {
      var result = setScrollbar.apply(this,arguments);
      fdStyleScrollbar(this.m_scrollbar);
      fdRefreshRows();
      fdUpdateEmpty();
      return result;
   };
}

function fdWrapPane(name)
{
   var stock = panel_mc[name];
   panel_mc[name] = function()
   {
      var result = stock.apply(this,arguments);
      fdSyncTabs();
      return result;
   };
}

function fdInit()
{
   CharacterList.ITEM_HEIGHT = FD_ROW_H;
   fdPatchClasses();
   panel_mc.m_listView.setSize(FD_LIST_W,panel_mc.m_listView.getHeight());
   fdBuildFront();
   fdBuildBack();
   fdBuildFlyout();
   fdWrapPanel();

   // Rows Lua may already have sent, at the new row height.
   var list = panel_mc.m_listView;
   var i;
   for(i = 0; i < list.m_items.length; i++)
   {
      if(list.m_items[i].fdRow == undefined)
      {
         fdStyleRow(list.m_items[i]);
      }
   }
   list.rePosition();
   fdLayout();
   list.reloadModel();
   fdSyncTabs();
   if(!panel_mc.getIsOnFront())
   {
      fdDrawCharacterPage();
   }
}

fdInit();
// ===== End Sanctuary Friends Dock =====

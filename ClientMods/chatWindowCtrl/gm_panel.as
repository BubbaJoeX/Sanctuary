// ===== Sanctuary GM panel =====
// Appended to chatWindowCtrl's main timeline by build_client_mods.py.
//
// Referees get a Referee tab on the left edge of the screen (the server sends a "referee" line when they enter
// the world); it pops the panel in and out. "!gm" toggles it too.
// The server answers with hidden chat lines "[[SANCTUARY_GM]]|kind|field|..." (GmChatCommand)
// that are swallowed here and rendered. Every button sends a "!" command through chat.
// The look matches the home screen (ClientMods/welcome/home.as): navy glass with gold for what's selected.

var gmToken = "[[SANCTUARY_GM]]";

// install_client_mods.py enlarges the chat window to about the size of the screen (the client clips
// each movie to its window, and its Lua bottom-aligns the chat window). Drawing 1:1 anchored
// bottom-left keeps chat where it always was, and the rest of the window is room for the panel.
Stage.scaleMode = "noScale";
Stage.align = "BL";

// Height of the chat movie's own stage; with bottom-left alignment the visible area spans
// y = GM_CHAT_STAGE_H - Stage.height (top) .. GM_CHAT_STAGE_H (bottom).
var GM_CHAT_STAGE_H = 345;
var GM_MIN_SCALE = 0.6;

function gmTop()
{
   return Math.min(0, GM_CHAT_STAGE_H - Stage.height);
}

function gmBottom()
{
   return Math.max(GM_CHAT_STAGE_H, Stage.height + gmTop());
}

function gmRight()
{
   return Stage.width;
}

// Base layout; the panel scales from here (resize grip, maximize). Tab groups run along the header, the
// group's tabs in a row under them, and every tab draws into the content area below.
var GM_W = 940;
var GM_H = 600;
var GM_CONTENT_X = 22;
var GM_CONTENT_Y = 112;
var GM_CONTENT_W = GM_W - GM_CONTENT_X - 22;
var GM_CONTENT_H = GM_H - GM_CONTENT_Y - 40;
var GM_COLUMNS = 4;
var GM_CELL_W = 221;
var GM_CELL_H = 52;

// Shell palette (the home screen's). The names are from the old gold-rimmed look: "gold" is now the rims,
// and gold proper (GM_TEXT_GOLD, GM_ACCENT) marks headings and what's selected.
var GM_GOLD = 0x8FB0F0;
var GM_GOLD_DARK = 0x4D6CC4;
var GM_BLUE_TOP = 0x3A5FC2;
var GM_BLUE_BOTTOM = 0x1A2C6E;
var GM_BLUE_INNER = 0x0A1440;
var GM_BLUE_CELL = 0x284A9E;
var GM_BLUE_ACTIVE = 0x5A86EC;
var GM_TEXT = 0xFFFFFF;
var GM_TEXT_DIM = 0xC9D6FF;
var GM_TEXT_GOLD = 0xFFD54A;
var GM_ACCENT = 0xFFC531;
var GM_ACCENT_DARK = 0xC7780F;
var GM_FRAME_TOP = 0x2E4FB0;
var GM_FRAME_BOTTOM = 0x141F55;
var GM_RIM = 0x9DB8FF;

var gmTab = "players";
var gmMaximized = false;
var gmScale = 1;
var gmResults = [];
var gmList = { kind: "", search: "", page: 1, pages: 1, total: 0 };
// Tab groups along the header, each with its tabs: live work first (people, events, getting around), then
// lookups, then authoring, then admin tools.
var gmTabGroups = [
   ["Live", ["players", "events", "world"]],
   ["Lookup", ["items", "npcs", "houses"]],
   ["Create", ["edit", "script", "conv", "quests", "tiles"]],
   ["Admin", ["itemgen", "codes", "server"]]
];
// Items tab categories: [label, filter the server knows ("" = all)].
var gmItemCategories = [
   ["All", ""], ["Gear", "gear"], ["Items", "items"], ["Pets", "pets"], ["Mounts", "mounts"], ["Housing", "housing"],
   ["Styles", "styles"], ["Racing", "racing"], ["Recipes", "recipes"], ["Custom", "custom"], ["Other", "other"]
];
var gmItemCategory = "";
var gmIsReferee = false;
var gmTabs = [
   ["players", "Players"],
   ["events", "Events"],
   ["world", "World"],
   ["items", "Items"],
   ["npcs", "NPCs"],
   ["houses", "Houses"],
   ["edit", "Edit"],
   ["script", "Script"],
   ["conv", "Dialogue"],
   ["quests", "Quests"],
   ["itemgen", "Item Gen"],
   ["codes", "Codes"],
   ["tiles", "Tiles"],
   ["server", "Server"]
];
// Role the server opened the panel with ("Mod" or "Admin").
var gmRole = "";
// Entity selected for editing (god mode click), as sent by the server: ["npc"|"prop"|"none", fields...].
var gmEdit = ["none"];
// Emotes for the editor: [[animationId, label], ...]; the server may replace it with an "anims" line.
var gmAnims = [];

// --- drawing -----------------------------------------------------------------

function gmFormat(size, color, bold, align)
{
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.bold = bold == true;
   f.align = align == undefined ? "left" : align;
   return f;
}

function gmText(mc, name, txt, x, y, w, h, size, color, bold, align)
{
   mc.createTextField(name, mc.getNextHighestDepth(), x, y, w, h);
   var t = mc[name];
   t.embedFonts = true;
   t.selectable = false;
   t.wordWrap = true;
   t.multiline = true;
   t.setNewTextFormat(gmFormat(size, color, bold, align));
   t.text = txt;
   return t;
}

function gmRoundRect(mc, x, y, w, h, r)
{
   mc.moveTo(x + r, y);
   mc.lineTo(x + w - r, y);
   mc.curveTo(x + w, y, x + w, y + r);
   mc.lineTo(x + w, y + h - r);
   mc.curveTo(x + w, y + h, x + w - r, y + h);
   mc.lineTo(x + r, y + h);
   mc.curveTo(x, y + h, x, y + h - r);
   mc.lineTo(x, y + r);
   mc.curveTo(x, y, x + r, y);
}

// Gold-rimmed blue panel like the hub windows.
function gmPanelShape(mc, w, h, r, top, bottom, rim, rimWidth)
{
   mc.clear();
   mc.lineStyle(rimWidth, rim, 100);
   var matrix = { matrixType: "box", x: 0, y: 0, w: w, h: h, r: Math.PI / 2 };
   mc.beginGradientFill("linear", [top, bottom], [100, 100], [0, 255], matrix);
   gmRoundRect(mc, 0, 0, w, h, r);
   mc.endFill();
}

function gmButton(parent, name, label, x, y, w, h, onClick, color)
{
   var b = parent.createEmptyMovieClip(name, parent.getNextHighestDepth());
   b._x = x;
   b._y = y;
   b.w = w;
   b.h = h;
   b.base = color == undefined ? GM_BLUE_TOP : color;
   b.bg = b.createEmptyMovieClip("bg_mc", 1);
   gmPanelShape(b.bg, w, h, Math.min(8, h / 2), b.base, GM_BLUE_BOTTOM, GM_GOLD, 2);
   var size = h >= 22 ? 12 : 10;
   // Single line, centered both ways.
   var label_txt = gmText(b, "label_txt", label, 2, Math.round((h - size) / 2) - 3, w - 4, size + 6, size, GM_TEXT, true, "center");
   label_txt.wordWrap = false;
   label_txt.multiline = false;
   b.onRelease = onClick;
   b.onRollOver = function()
   {
      gmPanelShape(this.bg, this.w, this.h, Math.min(8, this.h / 2), 0x6A97FF, this.base, 0xFFF4C2, 2);
      fscommand("FR_event", "PlaySound(DockOver)");
   };
   b.onRollOut = b.onReleaseOutside = function()
   {
      gmPanelShape(this.bg, this.w, this.h, Math.min(8, this.h / 2), this.base, GM_BLUE_BOTTOM, GM_GOLD, 2);
   };
   return b;
}

function gmUnescape(s)
{
   s = frHelperClass.stringReplace(String(s), "&lt;", "<");
   s = frHelperClass.stringReplace(s, "&gt;", ">");
   return frHelperClass.stringReplace(s, "&amp;", "&");
}

// Item and house icons are image sets; slot 6 is the icon (as the hub's item slots use).
function gmIcon(parent, name, imageSetId, x, y, size)
{
   var frame = parent.createEmptyMovieClip(name, parent.getNextHighestDepth());
   frame._x = x;
   frame._y = y;
   frame.lineStyle(1, GM_GOLD_DARK, 100);
   frame.beginFill(GM_BLUE_INNER, 100);
   gmRoundRect(frame, 0, 0, size, size, 6);
   frame.endFill();
   var holder = frame.createEmptyMovieClip("holder", 1);
   holder._x = 2;
   holder._y = 2;
   // The client's loader (AutoMovieClipEntry) sizes the loaded image to the holder's original
   // bounds, so an empty holder would shrink every icon to nothing. Give it the icon's size.
   holder.beginFill(0x000000, 0);
   gmRoundRect(holder, 0, 0, size - 4, size - 4, 0);
   holder.endFill();
   if(Number(imageSetId) > 0)
   {
      _root.frSetIconByImageSetSlotNoTint(holder, imageSetId, 6);
   }
   return frame;
}

// --- commands ------------------------------------------------------------------

function gmSend(cmd)
{
   if(!_root.frRunningInGame())
   {
      return;
   }
   fscommand("FR_event", "PlaySound(DockSelect)");
   fscommand("FR_event", "sendChatMessage(  \"" + frHelperClass.stringReplace(cmd, "\"", "\"\"") + "\")");
}

// Puts a command in the chat box so the referee can type the rest and press Enter.
function gmPrompt(prefix)
{
   fscommand("FR_event", "PlaySound(DockSelect)");
   onSendEntryBoxOpen();
   entryBox_mc.setText(prefix);
}

// --- panel -----------------------------------------------------------------------

function gmBuild()
{
   var root_mc = _root.createEmptyMovieClip("gm_mc", 16000);
   root_mc._x = 8;
   root_mc._y = GM_CHAT_STAGE_H - GM_H - 8;
   root_mc._visible = false;

   var frame = root_mc.createEmptyMovieClip("frame_mc", 1);
   gmPanelShape(frame, GM_W, GM_H, 24, GM_FRAME_TOP, GM_FRAME_BOTTOM, GM_RIM, 3);
   // Soft light from the top left, and a darker header band behind the tabs.
   frame.lineStyle();
   frame.beginGradientFill("radial", [0x8FB4FF, 0x8FB4FF], [30, 0], [0, 255], { matrixType: "box", x: -200, y: -320, w: 900, h: 700, r: 0 });
   gmRoundRect(frame, 2, 2, GM_W - 4, GM_H - 4, 22);
   frame.endFill();
   frame.beginFill(0x0A1440, 35);
   gmRoundRect(frame, 4, 4, GM_W - 8, GM_CONTENT_Y - 18, 20);
   frame.endFill();
   frame.filters = [new flash.filters.DropShadowFilter(6, 90, 0x050A24, 0.6, 18, 18, 1, 2)];
   frame.onPress = function()
   {
      // Keep the panel inside the window while dragging; anything outside it is clipped.
      var p = _root.gm_mc;
      var w = GM_W * p._xscale / 100;
      var h = GM_H * p._yscale / 100;
      p.startDrag(false, 0, gmTop(), Math.max(0, gmRight() - w), Math.max(gmTop(), gmBottom() - h));
   };
   frame.onRelease = frame.onReleaseOutside = function()
   {
      _root.gm_mc.stopDrag();
   };

   // Title: the Referee Tools tree (the dock's old button, purple) and the name.
   var title = root_mc.createEmptyMovieClip("title_mc", 2);
   var tree = title.attachMovie("sanctuaryIconRefereeTree", "tree_mc", 1);
   tree._x = 16;
   tree._y = 6;
   tree._width = tree._height = 42;
   var title_txt = gmText(title, "title_txt", "Referee Tools", 64, 10, 200, 30, 22, GM_TEXT, true);
   title_txt.wordWrap = false;
   title_txt.filters = [new flash.filters.DropShadowFilter(2, 90, 0x0A1030, 0.9, 3, 3, 1, 1)];
   gmText(title, "role_txt", "", 65, 36, 200, 18, 11, GM_TEXT_DIM, false).wordWrap = false;

   // The tab strip is redrawn at depth 3, so it must exist before the buttons take the next depths.
   root_mc.createEmptyMovieClip("tabs_mc", 3);

   gmButton(root_mc, "max_mc", "[ ]", GM_W - 76, 12, 30, 28, function()
   {
      gmCycleSize();
   });
   gmButton(root_mc, "close_mc", "X", GM_W - 42, 12, 30, 28, function()
   {
      gmPopIn(false);
   }, 0xC0392B);

   var inner = root_mc.createEmptyMovieClip("inner_mc", 50);
   inner._x = GM_CONTENT_X - 10;
   inner._y = GM_CONTENT_Y - 10;
   inner.lineStyle(1, GM_RIM, 35);
   inner.beginFill(GM_BLUE_INNER, 55);
   gmRoundRect(inner, 0, 0, GM_CONTENT_W + 20, GM_CONTENT_H + 16, 14);
   inner.endFill();

   gmText(root_mc, "status_txt", "", GM_CONTENT_X, GM_H - 28, GM_CONTENT_W - 20, 18, 11, GM_TEXT_DIM, false);

   root_mc.createEmptyMovieClip("content_mc", 60);
   gmAddResizeGrip(root_mc);
   gmClearContent();
   gmDrawTabs();

   var stageListener = new Object();
   stageListener.onResize = function()
   {
      gmFit();
      gmPlaceEdgeTab();
   };
   Stage.addListener(stageListener);
}

// --- tabs ---------------------------------------------------------------------------

function gmTabLabel(tabId)
{
   var i;
   for(i = 0; i < gmTabs.length; i++)
   {
      if(gmTabs[i][0] == tabId)
      {
         return gmTabs[i][1];
      }
   }
   return tabId;
}

function gmTabGroup(tabId)
{
   var g;
   var t;
   for(g = 0; g < gmTabGroups.length; g++)
   {
      for(t = 0; t < gmTabGroups[g][1].length; t++)
      {
         if(gmTabGroups[g][1][t] == tabId)
         {
            return g;
         }
      }
   }
   return 0;
}

// A tab pill; selected ones are gold.
function gmTabButton(parent, name, label, x, y, w, h, selected, size, onClick)
{
   var b = parent.createEmptyMovieClip(name, parent.getNextHighestDepth());
   b._x = x;
   b._y = y;
   b.w = w;
   b.h = h;
   b.selected = selected;
   b.bg = b.createEmptyMovieClip("bg_mc", 1);
   b.draw = function(over)
   {
      if(this.selected)
      {
         gmPanelShape(this.bg, this.w, this.h, this.h / 2, 0xFFE066, GM_ACCENT_DARK, 0xFFF4C2, 2);
      }
      else
      {
         gmPanelShape(this.bg, this.w, this.h, this.h / 2, over ? 0x5A86EC : 0x2B4AA6, over ? 0x2B4AA6 : 0x172A6A, over ? 0xFFF4C2 : GM_RIM, 1);
      }
   };
   b.draw(false);
   var t = gmText(b, "label_txt", label, 2, Math.round((h - size) / 2) - 4, w - 4, size + 8, size, selected ? 0x4A2500 : GM_TEXT, true, "center");
   t.wordWrap = false;
   b.onRelease = onClick;
   b.onRollOver = function()
   {
      this.draw(true);
      fscommand("FR_event", "PlaySound(DockOver)");
   };
   b.onRollOut = b.onReleaseOutside = function()
   {
      this.draw(false);
   };
   return b;
}

// Groups along the header, the current group's tabs underneath.
function gmDrawTabs()
{
   var root_mc = _root.gm_mc;
   root_mc.tabs_mc.removeMovieClip();
   var tabs = root_mc.createEmptyMovieClip("tabs_mc", 3);
   var group = gmTabGroup(gmTab);
   var g;
   for(g = 0; g < gmTabGroups.length; g++)
   {
      var gb = gmTabButton(tabs, "group" + g + "_mc", gmTabGroups[g][0], 290 + g * 132, 12, 124, 32, g == group, 14, function()
      {
         if(gmTabGroup(gmTab) != this.group)
         {
            gmShowTab(gmTabGroups[this.group][1][0]);
         }
      });
      gb.group = g;
   }
   var ids = gmTabGroups[group][1];
   var t;
   for(t = 0; t < ids.length; t++)
   {
      var tb = gmTabButton(tabs, "tab" + t + "_mc", gmTabLabel(ids[t]), GM_CONTENT_X + t * 132, 62, 124, 28, ids[t] == gmTab, 12, function()
      {
         // Players again from a player's card goes back to the list.
         if(this.tabId == "players" && gmTab == "players")
         {
            gmPlayer = undefined;
         }
         gmShowTab(this.tabId);
      });
      tb.tabId = ids[t];
   }
}

// --- pop-in -------------------------------------------------------------------------
// The panel slides in from the left edge, from the Referee tab there.

function gmPopIn(show)
{
   var p = _root.gm_mc;
   var offscreen = - GM_W * p._xscale / 100 - 20;
   if(show)
   {
      if(p._visible && p.onEnterFrame == undefined)
      {
         return;
      }
      if(!p._visible)
      {
         p._visible = true;
         gmFit();
         gmShowTab(gmTab);
         // Back where it was (beside the edge tab the first time; maximized, where gmFit centered it).
         p.homeX = gmMaximized ? p._x : (p.homeX == undefined ? 56 : p.homeX);
         p._x = offscreen;
      }
      gmSlide(p, p.homeX, true);
   }
   else if(p._visible)
   {
      if(p.onEnterFrame == undefined)
      {
         p.homeX = p._x;
      }
      gmSlide(p, offscreen, false);
   }
}

function gmSlide(p, toX, staysVisible)
{
   p.onEnterFrame = function()
   {
      this._x += (toX - this._x) * 0.3;
      if(Math.abs(toX - this._x) < 1)
      {
         this._x = toX;
         delete this.onEnterFrame;
         this._visible = staysVisible;
      }
   };
}

// The Referee tab on the left edge of the screen, for referees only.
function gmShowEdgeTab()
{
   gmIsReferee = true;
   if(_root.gmtab_mc != undefined)
   {
      return;
   }
   var tab = _root.createEmptyMovieClip("gmtab_mc", 15990);
   var w = 46;
   var h = 168;
   tab.bg = tab.createEmptyMovieClip("bg_mc", 1);
   tab.draw = function(over)
   {
      this.bg.clear();
      this.bg.lineStyle(2, over ? 0xFFF4C2 : 0xD9CCFF, over ? 100 : 70);
      this.bg.beginGradientFill("linear", [over ? 0x8C6BFF : 0x6A4AD8, over ? 0x5A3CC4 : 0x3A2690], [100, 100], [0, 255], { matrixType: "box", x: 0, y: 0, w: w, h: h, r: 0 });
      // Rounded on the right only; the left side runs off the screen edge.
      this.bg.moveTo(-20, 0);
      this.bg.lineTo(w - 16, 0);
      this.bg.curveTo(w, 0, w, 16);
      this.bg.lineTo(w, h - 16);
      this.bg.curveTo(w, h, w - 16, h);
      this.bg.lineTo(-20, h);
      this.bg.lineTo(-20, 0);
      this.bg.endFill();
   };
   tab.draw(false);
   tab.filters = [new flash.filters.DropShadowFilter(3, 0, 0x050A24, 0.6, 10, 10, 1, 2)];
   var tree = tab.attachMovie("sanctuaryIconRefereeTree", "tree_mc", 2);
   tree._x = 5;
   tree._y = 8;
   tree._width = tree._height = 36;
   // Vertical label, reading bottom to top.
   var label = tab.createEmptyMovieClip("label_mc", 3);
   gmText(label, "txt", "REFEREE", 0, 0, 110, 24, 15, GM_TEXT, true, "center").wordWrap = false;
   label._rotation = -90;
   label._x = 12;
   label._y = h - 8;
   tab.onRollOver = function()
   {
      this.draw(true);
      fscommand("FR_event", "PlaySound(DockOver)");
   };
   tab.onRollOut = tab.onReleaseOutside = function()
   {
      this.draw(false);
   };
   tab.onRelease = function()
   {
      fscommand("FR_event", "PlaySound(DockSelect)");
      gmPopIn(!_root.gm_mc._visible || _root.gm_mc._x < 0);
   };
   gmPlaceEdgeTab();
}

function gmPlaceEdgeTab()
{
   var tab = _root.gmtab_mc;
   if(tab == undefined)
   {
      return;
   }
   tab._x = 0;
   tab._y = Math.round(gmTop() + (gmBottom() - gmTop()) * 0.3);
}

// --- copying item ids -----------------------------------------------------------------
// Middle-clicking an item cell copies its id (Scaleform passes the mouse button to onMouseDown; 3 is the middle).

_global.gfxExtensions = true;
var gmMouseListener = new Object();
gmMouseListener.onMouseDown = function(button)
{
   if(button != 3 || !_root.gm_mc._visible || gmTab != "items")
   {
      return;
   }
   var content = _root.gm_mc.content_mc;
   var i;
   for(i = 0; i < gmResults.length; i++)
   {
      var cell = content["cell" + i + "_mc"];
      if(cell != undefined && cell.hitTest(_root._xmouse, _root._ymouse, false))
      {
         var id = String(gmResults[i][0]);
         System.setClipboard(id);
         gmStatus("Copied item id " + id + " (" + gmResults[i][4] + ")");
         cell._alpha = 40;
         cell.onEnterFrame = function()
         {
            this._alpha += 10;
            if(this._alpha >= 100)
            {
               this._alpha = 100;
               delete this.onEnterFrame;
            }
         };
         fscommand("FR_event", "PlaySound(DockSelect)");
         return;
      }
   }
};
Mouse.addListener(gmMouseListener);

// Keeps the whole panel on screen at any window size; maximized fills the space available.
// Largest scale for the panel. The enlarged chat window can run a little past the screen edge,
// so keep a margin and stay at 90% of the window.
var GM_MAX_FILL = 0.9;

function gmMaxScale()
{
   return GM_MAX_FILL * Math.min((gmRight() - 40) / GM_W, (gmBottom() - gmTop() - 80) / GM_H);
}

// Keeps the panel on screen at its current size; maximized fills the visible area.
function gmFit()
{
   var root_mc = _root.gm_mc;
   var scale = gmMaximized ? gmMaxScale() : Math.max(GM_MIN_SCALE, Math.min(gmScale, gmMaxScale()));
   if(scale <= 0)
   {
      return;
   }
   root_mc._xscale = root_mc._yscale = scale * 100;
   var w = GM_W * scale;
   var h = GM_H * scale;
   if(gmMaximized)
   {
      root_mc._x = (gmRight() - w) / 2;
      root_mc._y = gmTop() + (gmBottom() - gmTop() - h) / 2;
      return;
   }
   root_mc._x = Math.max(0, Math.min(root_mc._x, gmRight() - w));
   root_mc._y = Math.max(gmTop() + 14, Math.min(root_mc._y, gmBottom() - h));
}

// Sizes the [ ] button steps through: normal (the grip's last size), small, then filling the window.
var GM_SMALL_SCALE = 0.72;
var GM_SIZE_NAMES = ["normal", "small", "fill the window"];
var gmSizeMode = 0;
var gmNormalScale = 1;

function gmCycleSize()
{
   gmSizeMode = (gmSizeMode + 1) % 3;
   gmMaximized = gmSizeMode == 2;
   gmScale = gmSizeMode == 1 ? Math.min(GM_SMALL_SCALE, gmNormalScale) : gmNormalScale;
   gmFit();
   gmStatus("Panel size: " + GM_SIZE_NAMES[gmSizeMode] + ". [ ] steps through the sizes; drag the bottom-right corner for any size.");
}

// Bottom-right grip: drag to scale the whole panel (every tab keeps its layout).
function gmAddResizeGrip(root_mc)
{
   var grip = root_mc.createEmptyMovieClip("grip_mc", 70);
   grip._x = GM_W - 22;
   grip._y = GM_H - 22;
   grip.beginFill(0x000000, 0);
   gmRoundRect(grip, 0, 0, 20, 20, 0);
   grip.endFill();
   grip.lineStyle(2, GM_GOLD, 100);
   var i;
   for(i = 0; i < 3; i++)
   {
      grip.moveTo(18, 6 + i * 5);
      grip.lineTo(6 + i * 5, 18);
   }
   grip.onPress = function()
   {
      gmMaximized = false;
      this.onMouseMove = function()
      {
         var p = _root.gm_mc;
         var scale = Math.max((_root._xmouse - p._x) / GM_W, (_root._ymouse - p._y) / GM_H);
         gmScale = Math.max(GM_MIN_SCALE, Math.min(scale, gmMaxScale()));
         p._xscale = p._yscale = gmScale * 100;
         updateAfterEvent();
      };
   };
   grip.onRelease = grip.onReleaseOutside = function()
   {
      delete this.onMouseMove;
      gmNormalScale = gmScale;
      gmSizeMode = 0;
      gmFit();
   };
}

function gmStatus(s)
{
   _root.gm_mc.status_txt.text = s;
}

function gmClearContent()
{
   var root_mc = _root.gm_mc;
   root_mc.content_mc.removeMovieClip();
   var content = root_mc.createEmptyMovieClip("content_mc", 60);
   content._x = GM_CONTENT_X;
   content._y = GM_CONTENT_Y;
   return content;
}

function gmShowTab(tabId)
{
   gmTab = tabId;
   gmDrawTabs();
   var content = gmClearContent();
   if(tabId == "world")
   {
      gmRenderWorld(content);
      return;
   }
   if(tabId == "events")
   {
      gmRenderEvents(content);
      return;
   }
   if(tabId == "server")
   {
      gmRenderServer(content);
      return;
   }
   if(tabId == "players" && gmPlayer != undefined)
   {
      gmRenderPlayerCard(content);
      return;
   }
   if(tabId == "edit")
   {
      gmRenderEdit(content);
      return;
   }
   if(tabId == "script")
   {
      gmRenderScript(content);
      return;
   }
   if(tabId == "conv")
   {
      gmRenderConv(content);
      return;
   }
   if(tabId == "quests")
   {
      gmRenderQuests(content);
      return;
   }
   if(tabId == "itemgen")
   {
      gmRenderItemGen(content);
      return;
   }
   if(tabId == "codes")
   {
      gmRenderCodes(content);
      return;
   }
   if(tabId == "tiles")
   {
      gmRenderTiles(content);
      return;
   }
   gmToolbar(content, tabId);
   if(gmList.kind == tabId)
   {
      gmRenderGrid(content);
   }
   else
   {
      gmSend(gmListCommand(tabId, 1, ""));
   }
}

// Search box, show-all and paging bar on every lookup tab.
function gmToolbar(content, kind)
{
   gmSearchBox(content, kind, 0, 0, 320, 26);
   var search = gmButton(content, "search_mc", "Search", 328, 0, 80, 26, function()
   {
      gmSearch(this.kind);
   });
   search.kind = kind;
   var all = gmButton(content, "all_mc", "Show all", 414, 0, 88, 26, function()
   {
      _root.gm_mc.content_mc.search_txt.text = "";
      gmSend(gmListCommand(this.kind, 1, ""));
   });
   all.kind = kind;
   // Paging at the right end of the bar.
   gmButton(content, "prev_mc", "<", GM_CONTENT_W - 190, 0, 34, 26, function()
   {
      gmPage(kind, -1);
   });
   gmText(content, "page_txt", "", GM_CONTENT_W - 152, 5, 114, 20, 12, GM_TEXT, true, "center");
   gmButton(content, "next_mc", ">", GM_CONTENT_W - 34, 0, 34, 26, function()
   {
      gmPage(kind, 1);
   });
   if(kind == "items")
   {
      // Category chips; middle-click a cell to copy its id.
      var c;
      var x = 0;
      for(c = 0; c < gmItemCategories.length; c++)
      {
         var w = 22 + gmItemCategories[c][0].length * 7;
         var chip = gmTabButton(content, "cat" + c + "_mc", gmItemCategories[c][0], x, 32, w, 24, gmItemCategories[c][1] == gmItemCategory, 11, function()
         {
            gmItemCategory = this.category;
            gmSend(gmListCommand("items", 1, gmFieldText(_root.gm_mc.content_mc.search_txt)));
         });
         chip.category = gmItemCategories[c][1];
         x += w + 6;
      }
      gmText(content, "copy_txt", "Middle-click an item to copy its id", x + 6, 36, GM_CONTENT_W - x - 6, 18, 10, GM_TEXT_DIM, false, "right").wordWrap = false;
   }
   if(kind == "houses")
   {
      gmButtonRow(content, GM_CONTENT_H - 26, [
         ["My houses", "!house list"],
         ["Enter", "!house enter"],
         ["Leave", "!house leave"],
         ["Furniture", "!furniture"],
         ["Anywhere", "!house unbound"]
      ]);
   }
   if(kind == "players")
   {
      // Offline players can't be inspected; these take a typed name.
      gmPromptRow(content, GM_CONTENT_H - 26, [
         ["Ban by name", "!mod ban "],
         ["Unban by name", "!mod unban "],
         ["Unmute by name", "!mod unmute "],
         ["Promote", "!admin promote "],
         ["Demote", "!admin demote "]
      ]);
   }
}

// Like gmButtonRow, but each button puts its command in the chat box to finish typing.
function gmPromptRow(content, y, buttons)
{
   var width = Math.floor((GM_COLUMNS * (GM_CELL_W + 2)) / buttons.length) - 4;
   var i;
   for(i = 0; i < buttons.length; i++)
   {
      var b = gmButton(content, "prow" + y + "_" + i + "_mc", buttons[i][0], i * (width + 4), y, width, 22, function()
      {
         gmPrompt(this.prefix);
      });
      b.prefix = buttons[i][1];
   }
}

// Button that needs a second click within three seconds; for kicks, bans and other things hard to undo.
function gmConfirmButton(parent, name, label, x, y, w, h, onConfirm)
{
   var b = gmButton(parent, name, label, x, y, w, h, function()
   {
      if(!this.armed)
      {
         this.armed = true;
         this.label_txt.text = "Sure?";
         var self = this;
         this.timer = setTimeout(function()
         {
            self.armed = false;
            self.label_txt.text = self.label;
         }, 3000);
         return;
      }
      clearTimeout(this.timer);
      this.armed = false;
      this.label_txt.text = this.label;
      this.onConfirm();
   }, 0xC0392B);
   b.label = label;
   b.onConfirm = onConfirm;
   return b;
}

// Single-line text input. Typing needs the chat window to own the keyboard, which Lua grants
// through showTextEntry (it focuses the chat entry box, so focus is taken back right after).
// Enter calls onSubmit(text).
function gmInput(parent, name, x, y, w, h, value, hint, onSubmit)
{
   var box = parent.createEmptyMovieClip(name + "_box", parent.getNextHighestDepth());
   box._x = x;
   box._y = y;
   box.lineStyle(2, GM_GOLD, 100);
   box.beginFill(0x0B1E3A, 100);
   gmRoundRect(box, 0, 0, w, h, 8);
   box.endFill();

   parent.createTextField(name, parent.getNextHighestDepth(), x + 6, y + 3, w - 12, h - 4);
   var t = parent[name];
   t.embedFonts = true;
   t.type = "input";
   t.selectable = true;
   t.wordWrap = false;
   t.multiline = false;
   t.maxChars = 64;
   t.setNewTextFormat(gmFormat(12, GM_TEXT, false));
   t.text = value == undefined ? "" : String(value);
   t.onSubmit = onSubmit;

   parent.createTextField(name + "_hint", parent.getNextHighestDepth(), x + 6, y + 4, w - 12, h - 4);
   var hintText = parent[name + "_hint"];
   hintText.embedFonts = true;
   hintText.selectable = false;
   hintText.setNewTextFormat(gmFormat(11, GM_TEXT_DIM, false));
   hintText.text = hint == undefined ? "" : hint;
   hintText._visible = t.text == "";
   t.hint = hintText;

   t.onChanged = function()
   {
      this.hint._visible = this.text == "";
   };
   t.onSetFocus = function()
   {
      this.hint._visible = false;
   };
   t.onKillFocus = function()
   {
      this.hint._visible = this.text == "";
   };

   box.field = t;
   box.onPress = function()
   {
      gmFocusInput(this.field);
   };
   return t;
}

function gmFocusInput(t)
{
   fscommand("FR_event", "showTextEntry()");
   setTimeout(function()
   {
      Selection.setFocus(t);
      Selection.setSelection(t.text.length, t.text.length);
   }, 50);
}

// Field text made safe for a chat command and the server's '|'-separated replies.
function gmFieldText(t)
{
   return t == undefined ? "" : frHelperClass.stringReplace(String(t.text), "|", " ");
}

// Runs the focused input's onSubmit on Enter instead of sending chat.
var gmKeyListener = new Object();
gmKeyListener.onKeyDown = function()
{
   if((!_root.gm_mc._visible && _root.gmredeem_mc == undefined) || Key.getCode() != Key.ENTER)
   {
      return;
   }
   var t = eval(Selection.getFocus());
   if(t.onSubmit != undefined)
   {
      t.onSubmit(gmFieldText(t));
      fscommand("FR_event", "killFocus()");
   }
};
Key.addListener(gmKeyListener);

// Search box for the current tab's lookup.
function gmSearchBox(content, kind, x, y, w, h)
{
   var t = gmInput(content, "search_txt", x, y, w, h, gmList.kind == kind ? gmList.search : "", "Search by name or id...", function(text)
   {
      gmSend(gmListCommand(this.kind, 1, text));
   });
   t.kind = kind;
}

function gmSearch(kind)
{
   gmSend(gmListCommand(kind, 1, gmFieldText(_root.gm_mc.content_mc.search_txt)));
   fscommand("FR_event", "killFocus()");
}

function gmButtonRow(content, y, buttons)
{
   var width = Math.floor((GM_COLUMNS * (GM_CELL_W + 2)) / buttons.length) - 4;
   var i;
   for(i = 0; i < buttons.length; i++)
   {
      var b = gmButton(content, "row" + y + "_" + i + "_mc", buttons[i][0], i * (width + 4), y, width, 22, function()
      {
         gmSend(this.cmd);
      });
      b.cmd = buttons[i][1];
   }
}

// "!gm <kind> <page> [search]"; items also carry the category as "@<category>" before the search.
function gmListCommand(kind, page, search)
{
   var category = kind == "items" && gmItemCategory != "" ? "@" + gmItemCategory + " " : "";
   return "!gm " + kind + " " + page + " " + category + search;
}

function gmPage(kind, delta)
{
   if(gmList.kind != kind)
   {
      return;
   }
   var page = gmList.page + delta;
   if(page < 1 || page > gmList.pages)
   {
      return;
   }
   gmSend(gmListCommand(kind, page, gmList.search));
}

function gmRenderGrid(content)
{
   var kind = gmList.kind;
   content.page_txt.text = "Page " + gmList.page + " / " + gmList.pages;
   gmStatus(gmList.total + (gmList.search == "" ? " total" : " matching \"" + gmList.search + "\""));
   var i;
   for(i = 0; i < gmResults.length; i++)
   {
      var r = gmResults[i];
      var cell = content.createEmptyMovieClip("cell" + i + "_mc", 100 + i);
      cell._x = (i % GM_COLUMNS) * (GM_CELL_W + 4);
      // Items have a row of category chips over the grid.
      cell._y = (kind == "items" ? 64 : 34) + Math.floor(i / GM_COLUMNS) * (GM_CELL_H + 4);
      gmPanelShape(cell, GM_CELL_W, GM_CELL_H, 10, GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
      if(kind == "items")
      {
         gmItemCell(cell, r);
      }
      else if(kind == "npcs")
      {
         gmNpcCell(cell, r);
      }
      else if(kind == "houses")
      {
         gmHouseCell(cell, r);
      }
      else if(kind == "players")
      {
         gmPlayerCell(cell, r);
      }
   }
   gmStatus(gmList.total + " result(s)");
}

function gmSmall(cell, name, label, x, w, cmd, prompt)
{
   var b = gmButton(cell, name, label, x, GM_CELL_H - 20, w, 17, function()
   {
      if(this.prompt)
      {
         gmPrompt(this.cmd);
      }
      else
      {
         gmSend(this.cmd);
      }
   });
   b.cmd = cmd;
   b.prompt = prompt == true;
   return b;
}

// Name + id line next to an optional icon.
function gmCellText(cell, title, subtitle, left)
{
   var name_txt = gmText(cell, "name_txt", title, left, 1, GM_CELL_W - left - 4, 16, 10, GM_TEXT, true);
   name_txt.wordWrap = false;
   gmText(cell, "id_txt", subtitle, left, 14, GM_CELL_W - left - 4, 14, 9, GM_TEXT_DIM, false).wordWrap = false;
}

// item: [id, imageSetId, tint, type, name]
function gmItemCell(cell, r)
{
   gmIcon(cell, "icon_mc", r[1], 3, 3, 44);
   gmCellText(cell, r[4], "#" + r[0], 50);
   gmSmall(cell, "give1_mc", "+1", 50, 30, "!give " + r[0] + " 1");
   gmSmall(cell, "give999_mc", "+999", 83, 38, "!give " + r[0] + " 999");
   gmSmall(cell, "place_mc", "Place", 124, 38, "!place mode " + r[0]);
}

// npc: [id, modelId, name]
function gmNpcCell(cell, r)
{
   gmCellText(cell, r[2], "#" + r[0] + "   model " + r[1], 6);
   gmSmall(cell, "spawn_mc", "Spawn here", 6, 84, "!npc spawn " + r[0]);
   gmSmall(cell, "despawn_mc", "Despawn", 94, 66, "!npc despawn");
}

// house: [id, imageSetId, name]
function gmHouseCell(cell, r)
{
   gmIcon(cell, "icon_mc", r[1], 3, 3, 44);
   gmCellText(cell, r[2], "#" + r[0], 50);
   gmSmall(cell, "grant_mc", "Grant to me", 50, 112, "!house grant " + r[0]);
}

// player: [guid, name, zoneName]
function gmPlayerCell(cell, r)
{
   gmCellText(cell, r[1], r[2], 6);
   gmSmall(cell, "inspect_mc", "Inspect", 6, 62, "!gm player " + r[1]);
   gmSmall(cell, "goto_mc", "Go to", 71, 52, "!gm goto " + r[1]);
   gmSmall(cell, "summon_mc", "Summon", 126, 70, "!gm summon " + r[1]);
}

// --- shared card layout ------------------------------------------------------------
// The live tabs (Players card, Events, World, Server) are stacks of titled cards with button rows.

// Card with a gold heading; returns the card clip. Content goes from y = 26 inside it.
function gmCard(content, name, title, x, y, w, h)
{
   var card = content.createEmptyMovieClip(name, content.getNextHighestDepth());
   card._x = x;
   card._y = y;
   gmPanelShape(card, w, h, 8, GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
   gmText(card, "h_txt", title, 10, 4, w - 20, 18, 12, GM_TEXT_GOLD, true).wordWrap = false;
   return card;
}

// Evenly spaced buttons across a card row: [label, command] or [label, command, color].
// A command of the form "?..." asks for a second click first (see gmConfirmButton).
function gmCardRow(card, y, w, buttons)
{
   var bw = Math.floor((w - 20 - (buttons.length - 1) * 6) / buttons.length);
   var i;
   for(i = 0; i < buttons.length; i++)
   {
      var cmd = buttons[i][1];
      var b;
      if(cmd.charAt(0) == "?")
      {
         b = gmConfirmButton(card, "r" + y + "_" + i + "_mc", buttons[i][0], 10 + i * (bw + 6), y, bw, 22, function()
         {
            gmSendThen(this.cmd);
         });
         b.cmd = cmd.substr(1);
         continue;
      }
      b = gmButton(card, "r" + y + "_" + i + "_mc", buttons[i][0], 10 + i * (bw + 6), y, bw, 22, function()
      {
         gmSendThen(this.cmd);
      }, buttons[i][2]);
      b.cmd = cmd;
   }
}

// Sends a command, then whatever the current view needs to show its effect (the player card re-inspects).
function gmSendThen(cmd)
{
   if(gmTab == "players" && gmPlayer != undefined)
   {
      gmQueueSend(cmd);
      gmQueueSend("!gm player " + gmPlayer.name);
      return;
   }
   gmSend(cmd);
}

// --- players: inspect card -----------------------------------------------------------
// pinfo|guid|name|zone|x|y|z|role|coins|member|mutedUntil|frozen|godMode|snooped|packetLog

var gmPlayer = undefined;

function gmHandlePlayerInfo(parts)
{
   gmPlayer = { guid: parts[2], name: parts[3], zone: parts[4], x: parts[5], y: parts[6], z: parts[7], role: parts[8], coins: parts[9],
      member: parts[10] == "1", muted: parts[11], frozen: parts[12] == "1", godMode: parts[13] == "1", snooped: parts[14] == "1", packetLog: parts[15] == "1" };
   if(!_root.gm_mc._visible)
   {
      _root.gm_mc._visible = true;
      gmFit();
   }
   gmShowTab("players");
}

function gmRenderPlayerCard(content)
{
   var p = gmPlayer;
   var w = GM_CONTENT_W;
   gmText(content, "name_txt", p.name + (p.role != "Player" ? "  (" + p.role + ")" : ""), 0, 0, w - 200, 22, 15, GM_TEXT_GOLD, true).wordWrap = false;
   gmText(content, "where_txt", p.zone + "  at " + p.x + ", " + p.y + ", " + p.z + "   coins " + p.coins + (p.member ? "   member" : ""), 0, 22, w - 140, 18, 11, GM_TEXT_DIM, false).wordWrap = false;
   gmButton(content, "refresh_mc", "Refresh", w - 136, 0, 66, 22, function()
   {
      gmSend("!gm player " + gmPlayer.name);
   });
   gmButton(content, "list_mc", "All players", w - 66, 0, 66, 22, function()
   {
      gmPlayer = undefined;
      gmSend("!gm players 1");
      gmShowTab("players");
   });

   // State chips: what's on for this player right now.
   var chips = [];
   if(p.muted != "")
   {
      chips.push("MUTED until " + p.muted);
   }
   if(p.frozen)
   {
      chips.push("FROZEN");
   }
   if(p.snooped)
   {
      chips.push("SNOOPED by you");
   }
   if(p.packetLog)
   {
      chips.push("PACKET LOG on");
   }
   if(p.godMode)
   {
      chips.push("god mode");
   }
   gmText(content, "chips_txt", chips.length == 0 ? "No restrictions." : chips.join("    "), 0, 40, w, 18, 11, chips.length == 0 ? 0x7CFC9A : 0xFFB0A0, true).wordWrap = false;

   var n = p.name;
   var col = Math.floor((w - 10) / 2);

   var move = gmCard(content, "move_mc", "Move", 0, 62, col, 58);
   gmCardRow(move, 28, col, [
      ["Go to", "!gm goto " + n],
      ["Summon", "!gm summon " + n],
      [p.frozen ? "Unfreeze" : "Freeze", "!freeze " + n, p.frozen ? GM_BLUE_ACTIVE : undefined],
      ["Back", "!gm back"]
   ]);

   var mod = gmCard(content, "mod_mc", "Chat", col + 10, 62, col, 58);
   gmCardRow(mod, 28, col, [
      ["Mute 15m", "!mod mute " + n + " 15"],
      ["Mute 1h", "!mod mute " + n + " 60"],
      ["Mute 1d", "!mod mute " + n + " 1440"],
      ["Unmute", "!mod unmute " + n],
      [p.snooped ? "Unsnoop" : "Snoop", "!snoop " + n, p.snooped ? GM_BLUE_ACTIVE : undefined]
   ]);

   var discipline = gmCard(content, "ban_mc", "Remove from the game", 0, 128, col, 58);
   gmCardRow(discipline, 28, col, [
      ["Kick", "?!kick " + n],
      ["Ban 1d", "?!mod ban " + n + " 1440"],
      ["Ban 7d", "?!mod ban " + n + " 10080"],
      ["Ban", "?!mod ban " + n]
   ]);

   var help = gmCard(content, "help_mc", "Help out", col + 10, 128, col, 58);
   gmCardRow(help, 28, col, [
      ["Revive", "!kill revive " + n],
      ["Coins +100", "!coins 100 " + n],
      ["+1000", "!coins 1000 " + n],
      [p.member ? "Member off" : "Member on", "!flags member " + (p.member ? "off " : "on ") + n]
   ]);

   var look = gmCard(content, "look_mc", "Look up", 0, 194, col, 58);
   gmCardRow(look, 28, col, [
      ["Quests", "!quest list " + n],
      ["Pets", "!pet list " + n],
      ["Collect.", "!collection status " + n],
      ["Flags", "!flags " + n]
   ]);

   var debug = gmCard(content, "debug_mc", "Debug", col + 10, 194, col, 58);
   gmCardRow(debug, 28, col, [
      [p.packetLog ? "Packet log off" : "Packet log on", "!packetlog " + (p.packetLog ? "off " : "on ") + n],
      ["Tutorials", "!tutorial seen " + n],
      ["Kill", "?!kill " + n]
   ]);

   // Give an item (event prizes): id, quantity, tint 0.
   var give = gmCard(content, "give_mc", "Give an item (find ids in the Items tab)", 0, 260, w, 58);
   gmInput(give, "item_txt", 10, 28, 110, 22, "", "item id");
   gmInput(give, "qty_txt", 126, 28, 50, 22, "", "qty");
   gmButton(give, "give_mc", "Give to " + n, 182, 28, 160, 22, function()
   {
      var c = _root.gm_mc.content_mc.give_mc;
      var item = gmFieldText(c.item_txt);
      var qty = gmFieldText(c.qty_txt);
      if(item == "")
      {
         gmStatus("Type an item id first.");
         return;
      }
      gmSendThen("!give " + item + " " + (qty == "" ? "1" : qty) + " 0 " + gmPlayer.name);
      fscommand("FR_event", "killFocus()");
   }, 0x2E7D5B);

   gmStatus("Inspecting " + n + ". Red buttons ask for a second click.");
}

// --- events tab -------------------------------------------------------------------

// World events from "!gm events": [[key, on], ...].
var gmEvents = [];
var gmEventsAsked = false;

function gmHandleEvents(parts)
{
   gmEvents = [];
   var i;
   for(i = 2; i < parts.length; i++)
   {
      if(String(parts[i]) == "")
      {
         continue;
      }
      var e = String(parts[i]).split(":");
      gmEvents.push([e[0], e[1] == "1"]);
   }
   if(_root.gm_mc._visible && gmTab == "events")
   {
      gmShowTab("events");
   }
}

// Message box text, or a status hint when it's empty.
function gmEventMessage()
{
   var text = gmFieldText(_root.gm_mc.content_mc.say_mc.msg_txt);
   if(text == "")
   {
      gmStatus("Type the message first.");
   }
   return text;
}

function gmRenderEvents(content)
{
   if(!gmEventsAsked)
   {
      gmEventsAsked = true;
      gmSend("!gm events");
   }
   var w = GM_CONTENT_W;
   var col = Math.floor((w - 10) / 2);

   // Announcements: one message box, several ways to send it.
   var say = gmCard(content, "say_mc", "Announce", 0, 0, w, 86);
   gmInput(say, "msg_txt", 10, 28, w - 20, 22, "", "Message to players...");
   var sends = [
      ["Chat, everyone", ""],
      ["Banner, everyone", "banner "],
      ["Chat, this zone", "zone "],
      ["Banner, this zone", "zone banner "],
      ["Set as MOTD", "motd"]
   ];
   var bw = Math.floor((w - 20 - 4 * 6) / 5);
   var i;
   for(i = 0; i < sends.length; i++)
   {
      var b = gmButton(say, "send" + i + "_mc", sends[i][0], 10 + i * (bw + 6), 56, bw, 22, function()
      {
         var text = gmEventMessage();
         if(text == "")
         {
            return;
         }
         gmSend(this.flags == "motd" ? "!motd " + text : "!broadcast " + this.flags + text);
         fscommand("FR_event", "killFocus()");
      }, i == 4 ? undefined : 0x2E7D5B);
      b.flags = sends[i][1];
   }

   // Countdown on everyone's screen in this zone; the message box (if any) shows at zero.
   var count = gmCard(content, "count_mc", "Countdown (this zone; the message shows at zero)", 0, 94, w, 58);
   var steps = ["3", "5", "10", "30", "60", "300"];
   for(i = 0; i < steps.length; i++)
   {
      var c = gmButton(count, "c" + i + "_mc", steps[i] == "300" ? "5 min" : steps[i] + "s", 10 + i * 78, 28, 72, 22, function()
      {
         var text = gmFieldText(_root.gm_mc.content_mc.say_mc.msg_txt);
         gmSend("!countdown " + this.seconds + (text == "" ? "" : " " + text));
      });
      c.seconds = steps[i];
   }
   gmButton(count, "cancel_mc", "Cancel", w - 90, 28, 80, 22, function()
   {
      gmSend("!countdown cancel");
   }, 0xC0392B);

   // Crowd control for starts and finishes.
   var crowd = gmCard(content, "crowd_mc", "Crowd (this zone, referees excluded)", 0, 160, col, 86);
   gmCardRow(crowd, 28, col, [
      ["Gather all", "!gm gather"],
      ["Gather 30m", "!gm gather 30"],
      ["Gather 100m", "!gm gather 100"]
   ]);
   gmCardRow(crowd, 56, col, [
      ["Freeze all", "!freeze zone on"],
      ["Unfreeze all", "!freeze zone off", 0x2E7D5B]
   ]);

   // World and server state (admin).
   var server = gmCard(content, "state_mc", "World state (admin)", col + 10, 160, col, 86);
   gmCardRow(server, 28, col, [
      ["Lock", "!lock on"],
      ["Lock + kick", "?!lock on kick"],
      ["Unlock", "!lock off", 0x2E7D5B]
   ]);
   gmCardRow(server, 56, col, [
      ["Show MOTD", "!motd"],
      ["Clear MOTD", "!motd clear"],
      ["Day speed x1", "!timescale 1"],
      ["x10", "!timescale 10"]
   ]);

   // GM events: NPCs flagged with an event key spawn only while it's on.
   var ev = gmCard(content, "ev_mc", "World events (NPCs tagged in Edit > Event spawn only while on)", 0, 254, w, GM_CONTENT_H - 254);
   gmInput(ev, "key_txt", 10, 28, 150, 22, "", "new event key");
   gmButton(ev, "evon_mc", "Turn on", 166, 28, 70, 22, function()
   {
      var key = gmFieldText(_root.gm_mc.content_mc.ev_mc.key_txt);
      if(key != "")
      {
         gmQueueSend("!event on " + key);
         gmQueueSend("!gm events");
      }
   }, 0x2E7D5B);
   gmButton(ev, "evrefresh_mc", "Refresh", 242, 28, 70, 22, function()
   {
      gmSend("!gm events");
   });
   if(gmEvents.length == 0)
   {
      gmText(ev, "evnone_txt", "No events yet.", 324, 31, 300, 18, 11, GM_TEXT_DIM, false);
   }
   var perRow = 4;
   var ew = Math.floor((w - 20 - (perRow - 1) * 6) / perRow);
   for(i = 0; i < gmEvents.length && i < 12; i++)
   {
      var on = gmEvents[i][1];
      var t = gmButton(ev, "ev" + i + "_mc", gmEvents[i][0] + (on ? "  ON" : "  off"), 10 + (i % perRow) * (ew + 6), 56 + Math.floor(i / perRow) * 26, ew, 22, function()
      {
         gmQueueSend("!event " + (this.on ? "off " : "on ") + this.key);
         gmQueueSend("!gm events");
      }, on ? 0x2E7D5B : undefined);
      t.key = gmEvents[i][0];
      t.on = on;
   }
   gmStatus("Events: announce, count down, gather and switch world events.");
}

// --- world tab -------------------------------------------------------------------

// Bookmarks from "!gm marks": [[name, zoneId, x, y, z], ...].
var gmBookmarks = [];
var gmBookmarksAsked = false;
var gmBookmarkPage = 0;
var GM_BOOKMARKS_PER_PAGE = 8;

function gmHandleBookmarks(parts)
{
   gmBookmarks = [];
   var i;
   for(i = 2; i < parts.length; i++)
   {
      if(String(parts[i]) != "")
      {
         gmBookmarks.push(String(parts[i]).split(":"));
      }
   }
   if(_root.gm_mc._visible && gmTab == "world")
   {
      gmShowTab("world");
   }
}

function gmRenderWorld(content)
{
   if(!gmBookmarksAsked)
   {
      gmBookmarksAsked = true;
      gmSend("!gm marks");
   }
   var w = GM_CONTENT_W;
   var col = Math.floor((w - 10) / 2);

   var move = gmCard(content, "move_mc", "Getting around", 0, 0, w, 58);
   gmCardRow(move, 28, w, [
      ["God on", "!godmode on", 0x2E7D5B],
      ["God off", "!godmode off"],
      ["Fly", "!fly"],
      ["Back", "!gm back"],
      ["Where am I", "!gm pos"],
      ["Area here", "!area here"],
      ["Zone areas", "!area list"],
      ["Zones", "!zone ls"]
   ]);

   // Shared bookmarks: event stages, meeting points.
   var marks = gmCard(content, "marks_mc", "Bookmarks (shared by every referee)", 0, 66, w, 150);
   gmInput(marks, "mark_txt", 10, 28, 160, 22, "", "name for this spot");
   gmButton(marks, "mark_mc", "Mark here", 176, 28, 80, 22, function()
   {
      var name = gmFieldText(_root.gm_mc.content_mc.marks_mc.mark_txt);
      if(name == "")
      {
         gmStatus("Type a name for this spot first.");
         return;
      }
      gmSend("!gm mark " + name);
      fscommand("FR_event", "killFocus()");
   }, 0x2E7D5B);
   var pages = Math.max(1, Math.ceil(gmBookmarks.length / GM_BOOKMARKS_PER_PAGE));
   gmBookmarkPage = Math.max(0, Math.min(gmBookmarkPage, pages - 1));
   if(pages > 1)
   {
      gmButton(marks, "mprev_mc", "<", w - 128, 28, 26, 22, function()
      {
         gmBookmarkPage--;
         gmShowTab("world");
      });
      gmText(marks, "mpage_txt", (gmBookmarkPage + 1) + " / " + pages, w - 100, 31, 62, 18, 11, GM_TEXT, true, "center");
      gmButton(marks, "mnext_mc", ">", w - 36, 28, 26, 22, function()
      {
         gmBookmarkPage++;
         gmShowTab("world");
      });
   }
   if(gmBookmarks.length == 0)
   {
      gmText(marks, "mnone_txt", "No bookmarks yet. Stand somewhere, name it and press Mark here.", 10, 60, w - 20, 18, 11, GM_TEXT_DIM, false);
   }
   var mw = Math.floor((w - 20 - 6) / 2);
   var i;
   for(i = 0; i < GM_BOOKMARKS_PER_PAGE && gmBookmarkPage * GM_BOOKMARKS_PER_PAGE + i < gmBookmarks.length; i++)
   {
      var m = gmBookmarks[gmBookmarkPage * GM_BOOKMARKS_PER_PAGE + i];
      var mx = 10 + (i % 2) * (mw + 6);
      var my = 58 + Math.floor(i / 2) * 22;
      var go = gmButton(marks, "go" + i + "_mc", m[0] + "   (zone " + m[1] + ")", mx, my, mw - 26, 20, function()
      {
         gmSend("!gm go " + this.markName);
      });
      go.markName = m[0];
      var del = gmConfirmButton(marks, "del" + i + "_mc", "x", mx + mw - 22, my, 22, 20, function()
      {
         gmSend("!gm unmark " + this.markName);
      });
      del.markName = m[0];
   }

   var props = gmCard(content, "props_mc", "World props", 0, 224, col, 58);
   gmCardRow(props, 28, col, [
      ["Nearby", "!place list"],
      ["Remove nearest", "?!place remove"],
      ["Stop placing", "!place off"]
   ]);

   var mine = gmCard(content, "mine_mc", "My character", col + 10, 224, col, 58);
   gmCardRow(mine, 28, col, [
      ["My quests", "!quest list"],
      ["My pets", "!pet list"],
      ["Collections", "!collection status"],
      ["Furniture", "!furniture"]
   ]);

   // Anything that needs typing goes through the chat box.
   var prompts = gmCard(content, "prompts_mc", "Type the rest in chat", 0, 290, w, GM_CONTENT_H - 290);
   var list = [
      ["Teleport x y z", "!teleport "],
      ["Go to area", "!area goto "],
      ["Mount model", "!mount "],
      ["Find quest", "!quest find "],
      ["Start quest", "!quest start "],
      ["Skip goal", "!quest skip "],
      ["Finish quest", "!quest finish "],
      ["Reset quest", "!quest reset "],
      ["Find pet", "!pet find "],
      ["Give pet", "!pet give "],
      ["Grant collect.", "!collection grant "],
      ["Reset collect.", "!collection reset "]
   ];
   var pw = Math.floor((w - 20 - 5 * 6) / 6);
   for(i = 0; i < list.length; i++)
   {
      var b = gmButton(prompts, "p" + i + "_mc", list[i][0], 10 + (i % 6) * (pw + 6), 28 + Math.floor(i / 6) * 26, pw, 22, function()
      {
         gmPrompt(this.prefix);
      });
      b.prefix = list[i][1];
   }
   gmStatus("World tools");
}

// --- server tab ------------------------------------------------------------------
// Developer and admin tools: running zones, data reloads, and quick changes to yourself for testing.

function gmRenderServer(content)
{
   var w = GM_CONTENT_W;
   var col = Math.floor((w - 10) / 2);

   var info = gmCard(content, "info_mc", "Server", 0, 0, w, 58);
   gmCardRow(info, 28, w, [
      ["Zones", "!zone ls"],
      ["Out of bounds", "!oob"],
      ["Packet logs", "!packetlog list"],
      ["Snooping", "!snoop list"],
      ["Stop snooping", "!snoop off"],
      ["Commands", "!help"]
   ]);

   var reload = gmCard(content, "reload_mc", "Reload data from disk (admin)", 0, 66, w, 58);
   gmCardRow(reload, 28, w, [
      ["What can reload", "!reload list"],
      ["Reload all", "?!reload all"],
      ["Scripts", "!script reload"],
      ["Activities", "!activities"]
   ]);

   var speed = gmCard(content, "speed_mc", "My speed and jump (reset on relog)", 0, 132, col, 86);
   gmCardRow(speed, 28, col, [
      ["Walk 8", "!stat MaxMovementSpeed 8"],
      ["16", "!stat MaxMovementSpeed 16"],
      ["32", "!stat MaxMovementSpeed 32"],
      ["64", "!stat MaxMovementSpeed 64"]
   ]);
   gmCardRow(speed, 56, col, [
      ["Jump normal", "!stat JumpHeight 0"],
      ["Jump high", "!stat JumpHeight 20"],
      ["Fly on", "!fly on"],
      ["Fly off", "!fly off"]
   ]);

   var size = gmCard(content, "size_mc", "My size", col + 10, 132, col, 86);
   gmCardRow(size, 28, col, [
      ["x0.5", "!scale 0.5"],
      ["Normal", "!scale reset", 0x2E7D5B],
      ["x2", "!scale 2"],
      ["x4", "!scale 4"]
   ]);
   gmCardRow(size, 56, col, [
      ["Revive me", "!kill revive"],
      ["Dismount", "!mount 0"],
      ["Stat list", "!stat list"]
   ]);

   var time = gmCard(content, "time_mc", "Day / night speed (everyone)", 0, 226, w, 58);
   gmCardRow(time, 28, w, [
      ["Normal", "!timescale 1", 0x2E7D5B],
      ["x10", "!timescale 10"],
      ["x60", "!timescale 60"],
      ["x500", "!timescale 500"],
      ["Show", "!timescale"]
   ]);

   var test = gmCard(content, "test_mc", "Testing", 0, 292, w, 58);
   gmCardRow(test, 28, w, [
      ["Reward tables", "!reward list"],
      ["Abilities", "!ability list"],
      ["Clear ability", "!ability clear"],
      ["Profiles", "!profile list"],
      ["Titles", "!title list"]
   ]);
   gmStatus("Server and testing tools" + (gmRole == "Admin" ? "" : ". Some need admin."));
}

// --- editor --------------------------------------------------------------------

// Label, input and "Set" button that sends "<command> <text>" for the selected entity.
function gmEditField(content, name, label, x, y, labelW, inputW, value, command)
{
   gmText(content, name + "_label", label, x, y + 4, labelW, 18, 11, GM_TEXT_DIM, true);
   var t = gmInput(content, name, x + labelW, y, inputW, 22, value, "", function(text)
   {
      gmSend(this.command + " " + text);
   });
   t.command = command;
   var setButton = gmButton(content, name + "_set", "Set", x + labelW + inputW + 3, y, 36, 22, function()
   {
      gmSend(this.field.command + " " + gmFieldText(this.field));
   });
   setButton.field = t;
   return t;
}

// Row of option buttons, the current one highlighted.
function gmEditChoices(content, name, label, x, y, labelW, buttonW, choices, current)
{
   gmText(content, name + "_label", label, x, y + 4, labelW, 18, 11, GM_TEXT_DIM, true);
   var i;
   for(i = 0; i < choices.length; i++)
   {
      var active = String(choices[i][2]) == String(current);
      var b = gmButton(content, name + i + "_mc", choices[i][0], x + labelW + i * (buttonW + 4), y, buttonW, 22, function()
      {
         gmSend(this.cmd);
      }, active ? GM_BLUE_ACTIVE : undefined);
      b.cmd = choices[i][1];
   }
}

function gmEditButtons(content, y, buttons)
{
   var i;
   for(i = 0; i < buttons.length; i++)
   {
      var b = gmButton(content, "editbtn" + y + "_" + i + "_mc", buttons[i][0], i * 84, y, 80, 22, function()
      {
         gmSend(this.cmd);
      }, buttons[i][2]);
      b.cmd = buttons[i][1];
   }
}

function gmRenderEdit(content)
{
   var kind = gmEdit[0];
   if((kind == "npc" || kind == "prop") && gmFields != undefined && gmFields.guid == gmEdit[1])
   {
      gmRenderFields(content);
   }
   else if(kind == "npc")
   {
      gmRenderEditNpc(content, gmEdit);
   }
   else if(kind == "prop")
   {
      gmRenderEditProp(content, gmEdit);
   }
   else
   {
      gmText(content, "none_txt", "Nothing selected. Turn on god mode, then click an NPC or world prop to edit it here.", 0, 0, 500, 40, 12, GM_TEXT_DIM, false);
      gmButtonRow(content, 48, [
         ["God mode on", "!godmode on"],
         ["God mode off", "!godmode off"]
      ]);
      // House furniture: rotates the piece last placed or moved (in edit mode).
      gmText(content, "rotate_txt", "Rotate my last placed / moved furniture", 0, 84, 500, 18, 12, GM_TEXT_GOLD, true);
      gmButtonRow(content, 104, [
         ["Yaw -45", "!house rotate yaw -45"],
         ["Yaw +45", "!house rotate yaw 45"],
         ["Pitch -45", "!house rotate pitch -45"],
         ["Pitch +45", "!house rotate pitch 45"],
         ["Roll -45", "!house rotate roll -45"],
         ["Roll +45", "!house rotate roll 45"]
      ]);
      gmButtonRow(content, 130, [
         ["Yaw -15", "!house rotate yaw -15"],
         ["Yaw +15", "!house rotate yaw 15"],
         ["Pitch -15", "!house rotate pitch -15"],
         ["Pitch +15", "!house rotate pitch 15"],
         ["Roll -15", "!house rotate roll -15"],
         ["Roll +15", "!house rotate roll 15"]
      ]);
      gmStatus("Editor");
   }
}

// edit|npc|guid|name|nameId|model|scale|animation|disposition|nameplate|behavior|speed|event|x|y|z
function gmRenderEditNpc(content, e)
{
   var title = (e[2] == "" ? "Unnamed NPC" : e[2]) + (Number(e[3]) > 0 ? "  (name #" + e[3] + ")" : "");
   gmText(content, "title_txt", title, 0, 0, 330, 20, 13, GM_TEXT_GOLD, true);
   gmText(content, "where_txt", "at " + e[12] + ", " + e[13] + ", " + e[14], 330, 2, 174, 18, 11, GM_TEXT_DIM, false, "right");

   gmEditField(content, "name_txt", "Name", 0, 22, 44, 166, e[2], "!npc name");
   gmButton(content, "clearname_mc", "Clear", 252, 22, 44, 22, function()
   {
      gmSend("!npc name clear");
   });
   gmEditField(content, "event_txt", "Event", 304, 22, 44, 116, e[11], "!npc event");

   gmEditField(content, "model_txt", "Model", 0, 50, 44, 70, e[4], "!npc model");
   gmEditField(content, "scale_txt", "Scale", 160, 50, 40, 50, e[5], "!npc scale");
   gmEditField(content, "speed_txt", "Speed", 296, 50, 44, 50, e[10], "!npc speed");

   gmEditChoices(content, "disp", "Attitude", 0, 78, 52, 60, [
      ["Hostile", "!npc disposition hostile", 0],
      ["Neutral", "!npc disposition neutral", 1],
      ["Ally", "!npc disposition ally", 2]
   ], e[7]);
   gmEditChoices(content, "behave", "Behavior", 254, 78, 56, 60, [
      ["Stay", "!npc behavior stay", "Stay"],
      ["Follow", "!npc behavior follow", "Follow"],
      ["Wander", "!npc behavior wander", "Wander"]
   ], e[9]);

   gmEditChoices(content, "plate", "Plate", 0, 106, 36, 40, [
      ["On", "!npc nameplate on", 1],
      ["Off", "!npc nameplate off", 0]
   ], e[8]);

   gmEditButtons(content, 134, [
      ["Move here", "!npc move"],
      ["Face me", "!npc face"],
      ["Go home", "!npc home"],
      ["Use", "!npc use"],
      ["Despawn", "!npc despawn", 0xC0392B]
   ]);

   var anim = gmInput(content, "anim_txt", 162, 106, 44, 22, Number(e[6]) > 0 ? e[6] : "", "id", function(text)
   {
      gmSend("!npc anim " + text);
   });
   gmText(content, "anim_label", "Anim", 128, 110, 34, 18, 11, GM_TEXT_DIM, true);
   var playButton = gmButton(content, "play_mc", "Play", 209, 106, 38, 22, function()
   {
      gmSend("!npc anim " + gmFieldText(this.field));
   });
   playButton.field = anim;
   var loopButton = gmButton(content, "loop_mc", "Loop", 250, 106, 38, 22, function()
   {
      gmSend("!npc anim " + gmFieldText(this.field) + " loop");
   });
   loopButton.field = anim;
   gmEditField(content, "say_txt", "Say", 296, 106, 28, 140, "", "!npc say");

   // Emotes from the server's "anims" list (4 rows of 6). Looping ones set the NPC's saved idle.
   var i;
   for(i = 0; i < gmAnims.length && i < 24; i++)
   {
      var b = gmButton(content, "emote" + i + "_mc", gmAnims[i][1], (i % 6) * 84, 164 + Math.floor(i / 6) * 21, 80, 19, function()
      {
         gmSend("!npc anim " + this.animId + (this.loop ? " loop" : ""));
      }, gmAnims[i][2] ? 0x2E7D5B : undefined);
      b.animId = gmAnims[i][0];
      b.loop = gmAnims[i][2];
   }
   gmStatus("Editing NPC " + e[1]);
}

// edit|prop|guid|fixtureId|itemId|itemName|scale|tint|x|y|z
function gmRenderEditProp(content, e)
{
   gmText(content, "title_txt", e[4] + "  (item #" + e[3] + ", prop #" + e[2] + ")", 0, 0, 504, 20, 13, GM_TEXT_GOLD, true);
   gmText(content, "where_txt", "at " + e[7] + ", " + e[8] + ", " + e[9], 0, 22, 504, 18, 11, GM_TEXT_DIM, false);

   gmEditField(content, "scale_txt", "Scale", 0, 54, 44, 60, e[5], "!prop scale");
   gmEditField(content, "tint_txt", "Tint", 160, 54, 36, 60, e[6], "!prop tint");

   gmEditButtons(content, 84, [
      ["Move", "!prop move"],
      ["Info", "!prop info"],
      ["Remove", "!prop remove", 0xC0392B]
   ]);
   gmStatus("Editing prop " + e[1]);
}

// --- editor form ---------------------------------------------------------------------
// The server describes every editable field of the selected NPC or prop ("fields" lines, see
// RefereeFieldsHelper); this draws them as grouped cards in two columns, scrollable, with actions,
// emotes and the overhead icon picker below.

// { kind, guid, title, subtitle, list: [{ group, label, type, value, command, options }] }
var gmFields = undefined;
var gmFieldsLoading = undefined;
var gmFieldsScroll = 0;
// Overhead icon catalog: [{ type, icon, state, count, names }], filled by "!npc icon list".
var gmIcons = [];
var gmIconsLoading = [];
var gmIconPicker = false;
var gmIconPage = 0;
var gmIconReplace = false;

var GM_FIELD_ROW = 28;
var GM_FIELD_COL_W = (GM_CONTENT_W - 12) / 2;
var GM_FIELD_LABEL_W = 108;
var GM_ICON_COLUMNS = 3;
var GM_ICON_ROWS = 6;

function gmHandleFields(parts)
{
   if(parts[2] == "begin")
   {
      gmFieldsLoading = { kind: parts[3], guid: parts[4], title: parts[5], subtitle: parts[6], list: [] };
      return;
   }
   if(parts[2] == "f" && gmFieldsLoading != undefined)
   {
      gmFieldsLoading.list.push({ group: parts[3], label: parts[4], type: parts[5], value: parts[6], command: parts[7], options: parts[8] });
      return;
   }
   if(parts[2] == "end" && gmFieldsLoading != undefined)
   {
      if(gmFields == undefined || gmFields.guid != gmFieldsLoading.guid)
      {
         gmFieldsScroll = 0;
         gmIconPicker = false;
      }
      gmFields = gmFieldsLoading;
      gmFieldsLoading = undefined;
      if(_root.gm_mc._visible && gmTab == "edit")
      {
         gmShowTab("edit");
      }
   }
}

function gmHandleIcons(parts)
{
   if(parts[2] == "begin")
   {
      gmIconsLoading = [];
      return;
   }
   if(parts[2] == "add")
   {
      var i;
      for(i = 3; i < parts.length; i++)
      {
         // type:icon:state:count:example names
         var e = parts[i].split(":");
         gmIconsLoading.push({ type: e[0], icon: e[1], state: e[2], count: e[3], names: e.slice(4).join(":") });
      }
      return;
   }
   if(parts[2] == "end")
   {
      gmIcons = gmIconsLoading;
      if(_root.gm_mc._visible && gmTab == "edit")
      {
         gmShowTab("edit");
      }
   }
}

// Runs a field's command with the typed text; empty text sends the field's "cleared" value, if any.
function gmFieldSend(f, text)
{
   if(text == "")
   {
      if(f.options == undefined || f.options == "")
      {
         return;
      }
      text = f.options;
   }
   gmSend(frHelperClass.stringReplace(f.command, "{v}", text));
   fscommand("FR_event", "killFocus()");
}

function gmFieldRow(card, f, x, y, w)
{
   gmText(card, "l" + y + "_txt", f.label, x, y + 4, GM_FIELD_LABEL_W, 18, 11, GM_TEXT_DIM, true).wordWrap = false;
   var cx = x + GM_FIELD_LABEL_W;
   var cw = w - GM_FIELD_LABEL_W;
   if(f.type == "text" || f.type == "num")
   {
      var inputW = cw - 46;
      var t = gmInput(card, "v" + y + "_txt", cx, y, inputW, 22, f.value, f.options != "" && f.options != undefined ? "empty = " + f.options : "", function(text)
      {
         gmFieldSend(this.field, text);
      });
      t.field = f;
      var setButton = gmButton(card, "s" + y + "_mc", "Set", cx + inputW + 4, y, 40, 22, function()
      {
         gmFieldSend(this.input.field, gmFieldText(this.input));
      });
      setButton.input = t;
      return;
   }
   if(f.type == "toggle")
   {
      var choices = [["On", "on", "1"], ["Off", "off", "0"]];
      var i;
      for(i = 0; i < choices.length; i++)
      {
         var b = gmButton(card, "t" + y + "_" + i + "_mc", choices[i][0], cx + i * 58, y, 54, 22, function()
         {
            gmSend(frHelperClass.stringReplace(this.field.command, "{v}", this.arg));
         }, String(f.value) == choices[i][2] ? GM_BLUE_ACTIVE : undefined);
         b.field = f;
         b.arg = choices[i][1];
      }
      return;
   }
   if(f.type == "choice")
   {
      // Label=current value=argument;...
      var options = String(f.options).split(";");
      var bw = Math.min(70, Math.floor((cw - 4 * (options.length - 1)) / options.length));
      var j;
      for(j = 0; j < options.length; j++)
      {
         var o = options[j].split("=");
         var c = gmButton(card, "c" + y + "_" + j + "_mc", o[0], cx + j * (bw + 4), y, bw, 22, function()
         {
            gmSend(frHelperClass.stringReplace(this.field.command, "{v}", this.arg));
         }, String(f.value) == o[1] ? GM_BLUE_ACTIVE : undefined);
         c.field = f;
         c.arg = o[2];
      }
      return;
   }
   if(f.type == "steps")
   {
      // One button per option, each sending the command with that option.
      var steps = String(f.options).split(";");
      var sw = Math.min(56, Math.floor((cw - 4 * (steps.length - 1)) / steps.length));
      var k;
      for(k = 0; k < steps.length; k++)
      {
         var stepLabel = steps[k] == "reset" ? "Level" : (Number(steps[k]) > 0 ? "+" + steps[k] : steps[k]);
         var st = gmButton(card, "st" + y + "_" + k + "_mc", stepLabel, cx + k * (sw + 4), y, steps[k] == "reset" ? 80 : sw, 22, function()
         {
            gmSend(frHelperClass.stringReplace(this.field.command, "{v}", this.arg));
         });
         st.field = f;
         st.arg = steps[k];
      }
      return;
   }
   if(f.type == "icons")
   {
      var current = f.value == "" ? "none (definition default)" : frHelperClass.stringReplace(String(f.value), ";", ",  ");
      gmText(card, "i" + y + "_txt", current, cx, y + 4, cw, 18, 11, GM_TEXT, false).wordWrap = false;
      gmButton(card, "ipick" + y + "_mc", "Pick icon...", cx, y + GM_FIELD_ROW, 96, 22, function()
      {
         gmIconPicker = true;
         if(gmIcons.length == 0)
         {
            gmSend("!npc icon list");
         }
         gmShowTab("edit");
      }, 0x2E7D5B);
      gmButton(card, "iclear" + y + "_mc", "Clear icons", cx + 100, y + GM_FIELD_ROW, 90, 22, function()
      {
         gmSend("!npc icon clear");
      }, 0xC0392B);
      return;
   }
   gmText(card, "v" + y + "_txt", String(f.value), cx, y + 4, cw, 18, 11, GM_TEXT, false);
}

function gmFieldRows(f)
{
   return f.type == "icons" ? 2 : 1;
}

function gmRenderFields(content)
{
   if(gmIconPicker)
   {
      gmRenderIconPicker(content);
      return;
   }
   gmText(content, "title_txt", (gmFields.kind == "prop" ? "Prop: " : "NPC: ") + gmFields.title, 0, 0, GM_CONTENT_W - 120, 20, 14, GM_TEXT_GOLD, true).wordWrap = false;
   gmText(content, "sub_txt", gmFields.subtitle, 0, 20, GM_CONTENT_W - 120, 18, 11, GM_TEXT_DIM, false).wordWrap = false;

   var top = 44;
   var height = GM_CONTENT_H - top;
   var form = content.createEmptyMovieClip("form_mc", content.getNextHighestDepth());
   form._y = top;
   var mask = content.createEmptyMovieClip("mask_mc", content.getNextHighestDepth());
   mask.beginFill(0x000000, 100);
   gmRoundRect(mask, 0, top, GM_CONTENT_W, height, 0);
   mask.endFill();
   form.setMask(mask);

   // Group the fields, keeping their order.
   var groups = [];
   var byName = {};
   var i;
   for(i = 0; i < gmFields.list.length; i++)
   {
      var f = gmFields.list[i];
      if(byName[f.group] == undefined)
      {
         byName[f.group] = { name: f.group, fields: [] };
         groups.push(byName[f.group]);
      }
      byName[f.group].fields.push(f);
   }

   // Field cards fill the shorter of two columns; actions and emotes run full width below.
   var colY = [0, 0];
   var actions = [];
   var g;
   for(g = 0; g < groups.length; g++)
   {
      var group = groups[g];
      if(group.name == "Actions")
      {
         actions = group.fields;
         continue;
      }
      var rows = 0;
      for(i = 0; i < group.fields.length; i++)
      {
         rows += gmFieldRows(group.fields[i]);
      }
      var col = colY[0] <= colY[1] ? 0 : 1;
      var card = form.createEmptyMovieClip("card" + g + "_mc", form.getNextHighestDepth());
      card._x = col * (GM_FIELD_COL_W + 12);
      card._y = colY[col];
      var cardH = 30 + rows * GM_FIELD_ROW;
      gmPanelShape(card, GM_FIELD_COL_W, cardH, 8, GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
      gmText(card, "h_txt", group.name, 10, 5, GM_FIELD_COL_W - 20, 18, 12, GM_TEXT_GOLD, true);
      var y = 28;
      for(i = 0; i < group.fields.length; i++)
      {
         gmFieldRow(card, group.fields[i], 10, y, GM_FIELD_COL_W - 20);
         y += gmFieldRows(group.fields[i]) * GM_FIELD_ROW;
      }
      colY[col] += cardH + 10;
   }

   var bottom = Math.max(colY[0], colY[1]);
   bottom = gmRenderFieldActions(form, actions, bottom);
   bottom = gmRenderFieldEmotes(form, bottom);

   // Scrolling: arrows, and the mouse wheel over the panel.
   var maxScroll = Math.max(0, bottom - height);
   gmFieldsScroll = Math.max(0, Math.min(gmFieldsScroll, maxScroll));
   form._y = top - gmFieldsScroll;
   if(maxScroll > 0)
   {
      gmButton(content, "up_mc", "Up", GM_CONTENT_W - 110, 6, 52, 24, function()
      {
         gmScrollFields(-140);
      });
      gmButton(content, "down_mc", "Down", GM_CONTENT_W - 54, 6, 52, 24, function()
      {
         gmScrollFields(140);
      });
   }
   content.maxScroll = maxScroll;
   gmStatus("Editing " + gmFields.kind + " " + gmFields.guid + (maxScroll > 0 ? "   (scroll for more)" : ""));
}

function gmScrollFields(delta)
{
   var content = _root.gm_mc.content_mc;
   if(content.form_mc == undefined)
   {
      return;
   }
   gmFieldsScroll = Math.max(0, Math.min(gmFieldsScroll + delta, content.maxScroll));
   content.form_mc._y = 44 - gmFieldsScroll;
}

var gmWheelListener = new Object();
gmWheelListener.onMouseWheel = function(delta)
{
   if(_root.gm_mc._visible && gmTab == "edit" && _root.gm_mc.hitTest(_root._xmouse, _root._ymouse, false))
   {
      gmScrollFields(-delta * 30);
   }
};
Mouse.addListener(gmWheelListener);

function gmRenderFieldActions(form, actions, y)
{
   if(actions.length == 0)
   {
      return y;
   }
   var card = form.createEmptyMovieClip("actions_mc", form.getNextHighestDepth());
   card._y = y;
   var buttons = [];
   var texts = [];
   var i;
   for(i = 0; i < actions.length; i++)
   {
      if(actions[i].type == "action")
      {
         buttons.push(actions[i]);
      }
      else
      {
         texts.push(actions[i]);
      }
   }
   var cardH = 64 + texts.length * GM_FIELD_ROW;
   gmPanelShape(card, GM_CONTENT_W, cardH, 8, GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
   gmText(card, "h_txt", "Actions", 10, 5, 200, 18, 12, GM_TEXT_GOLD, true);
   var bw = 100;
   for(i = 0; i < buttons.length; i++)
   {
      var danger = buttons[i].label == "Despawn" || buttons[i].label == "Remove";
      var b = gmButton(card, "a" + i + "_mc", buttons[i].label, 10 + i * (bw + 6), 28, bw, 24, function()
      {
         gmSend(this.command);
      }, danger ? 0xC0392B : undefined);
      b.command = buttons[i].command;
   }
   for(i = 0; i < texts.length; i++)
   {
      gmFieldRow(card, texts[i], 10, 60 + i * GM_FIELD_ROW, GM_CONTENT_W - 20);
   }
   return y + cardH + 10;
}

// Emote buttons from the server's "anims" list; looping ones (green) become the saved idle.
function gmRenderFieldEmotes(form, y)
{
   if(gmAnims.length == 0)
   {
      return y;
   }
   var perRow = 6;
   var rows = Math.ceil(gmAnims.length / perRow);
   var card = form.createEmptyMovieClip("emotes_mc", form.getNextHighestDepth());
   card._y = y;
   var cardH = 32 + rows * 26;
   gmPanelShape(card, GM_CONTENT_W, cardH, 8, GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
   gmText(card, "h_txt", "Emotes (green loop as the idle)", 10, 5, 300, 18, 12, GM_TEXT_GOLD, true);
   var bw = Math.floor((GM_CONTENT_W - 20 - (perRow - 1) * 6) / perRow);
   var i;
   for(i = 0; i < gmAnims.length; i++)
   {
      var b = gmButton(card, "e" + i + "_mc", gmAnims[i][1], 10 + (i % perRow) * (bw + 6), 28 + Math.floor(i / perRow) * 26, bw, 22, function()
      {
         gmSend("!npc anim " + this.animId + (this.loop ? " loop" : ""));
      }, gmAnims[i][2] ? 0x2E7D5B : undefined);
      b.animId = gmAnims[i][0];
      b.loop = gmAnims[i][2];
   }
   return y + cardH + 10;
}

// Overhead icons the game's NPCs use, labelled with NPCs that show them; or type one in.
function gmRenderIconPicker(content)
{
   gmText(content, "title_txt", "Overhead icon for " + gmFields.title, 0, 0, 360, 20, 14, GM_TEXT_GOLD, true).wordWrap = false;
   gmButton(content, "back_mc", "Back", GM_CONTENT_W - 60, 0, 60, 24, function()
   {
      gmIconPicker = false;
      gmShowTab("edit");
   });
   gmButton(content, "mode_mc", gmIconReplace ? "Mode: replace" : "Mode: add", GM_CONTENT_W - 180, 0, 116, 24, function()
   {
      gmIconReplace = !gmIconReplace;
      gmShowTab("edit");
   });

   // Manual entry.
   gmText(content, "manual_txt", "Type / icon / state", 0, 32, 130, 18, 11, GM_TEXT_DIM, true);
   gmInput(content, "itype_txt", 130, 28, 50, 22, "", "type");
   gmInput(content, "iicon_txt", 184, 28, 60, 22, "", "icon");
   gmInput(content, "istate_txt", 248, 28, 50, 22, "", "state");
   gmButton(content, "iadd_mc", "Apply", 302, 28, 60, 22, function()
   {
      var c = _root.gm_mc.content_mc;
      var state = gmFieldText(c.istate_txt);
      gmIconApply(gmFieldText(c.itype_txt), gmFieldText(c.iicon_txt), state == "" ? "0" : state);
   }, 0x2E7D5B);

   if(gmIcons.length == 0)
   {
      gmText(content, "loading_txt", "Loading the icon list...", 0, 64, 400, 18, 12, GM_TEXT_DIM, false);
      return;
   }
   var perPage = GM_ICON_COLUMNS * GM_ICON_ROWS;
   var pages = Math.ceil(gmIcons.length / perPage);
   gmIconPage = Math.max(0, Math.min(gmIconPage, pages - 1));
   gmButton(content, "iprev_mc", "<", 380, 28, 28, 22, function()
   {
      gmIconPage--;
      gmShowTab("edit");
   });
   gmText(content, "ipage_txt", "Page " + (gmIconPage + 1) + " / " + pages, 410, 31, 96, 18, 11, GM_TEXT, true, "center");
   gmButton(content, "inext_mc", ">", 508, 28, 28, 22, function()
   {
      gmIconPage++;
      gmShowTab("edit");
   });

   var cellW = Math.floor((GM_CONTENT_W - (GM_ICON_COLUMNS - 1) * 6) / GM_ICON_COLUMNS);
   var i;
   for(i = 0; i < perPage && gmIconPage * perPage + i < gmIcons.length; i++)
   {
      var e = gmIcons[gmIconPage * perPage + i];
      var cell = content.createEmptyMovieClip("icon" + i + "_mc", content.getNextHighestDepth());
      cell._x = (i % GM_ICON_COLUMNS) * (cellW + 6);
      cell._y = 60 + Math.floor(i / GM_ICON_COLUMNS) * 56;
      gmPanelShape(cell, cellW, 52, 8, GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
      gmText(cell, "a_txt", "Type " + e.type + "  icon " + e.icon + "  state " + e.state + "   (" + e.count + ")", 6, 2, cellW - 12, 16, 11, GM_TEXT, true).wordWrap = false;
      gmText(cell, "b_txt", e.names == "" ? "(unnamed NPCs)" : e.names, 6, 18, cellW - 12, 32, 10, GM_TEXT_DIM, false);
      cell.entry = e;
      cell.w = cellW;
      cell.onRelease = function()
      {
         gmIconApply(this.entry.type, this.entry.icon, this.entry.state);
      };
      cell.onRollOver = function()
      {
         gmPanelShape(this, this.w, 52, 8, GM_BLUE_ACTIVE, GM_BLUE_BOTTOM, GM_GOLD, 1);
      };
      cell.onRollOut = function()
      {
         gmPanelShape(this, this.w, 52, 8, GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
      };
   }
   gmStatus(gmIcons.length + " icons used by the game's NPCs; the count is how many NPCs show it");
}

function gmIconApply(type, icon, state)
{
   if(type == "" || icon == "")
   {
      return;
   }
   gmSend("!npc icon " + (gmIconReplace ? "set " : "add ") + type + " " + icon + " " + state);
   gmIconPicker = false;
}

// --- text escaping (scripts, conversations, dialog) -----------------------------
// Free text travels as: '\' -> "\\", newline -> "\n", '|' -> "\p", tab -> "\t", and the spaces
// chat would trim or collapse (leading, trailing, or next to another space) -> "\s".

function gmEscapeText(s)
{
   var out = "";
   var i;
   for(i = 0; i < s.length; i++)
   {
      var c = s.charAt(i);
      if(c == "\\")
      {
         out += "\\\\";
      }
      else if(c == "\r" || c == "\n")
      {
         // Flash text fields use \r; treat \r\n as one line break.
         if(c == "\r" && s.charAt(i + 1) == "\n")
         {
            i++;
         }
         out += "\\n";
      }
      else if(c == "|")
      {
         out += "\\p";
      }
      else if(c == "\t")
      {
         out += "\\t";
      }
      else if(c == " " && (i == 0 || i == s.length - 1 || s.charAt(i - 1) == " " || s.charAt(i + 1) == " "))
      {
         // Chat trims leading/trailing spaces and collapses runs of them, so those travel as \s.
         out += "\\s";
      }
      else
      {
         out += c;
      }
   }
   return out;
}

function gmUnescapeText(s)
{
   var out = "";
   var i;
   for(i = 0; i < s.length; i++)
   {
      var c = s.charAt(i);
      if(c == "\\" && i + 1 < s.length)
      {
         var n = s.charAt(i + 1);
         i++;
         if(n == "n")
         {
            out += "\r";
         }
         else if(n == "p")
         {
            out += "|";
         }
         else if(n == "s")
         {
            out += " ";
         }
         else if(n == "t")
         {
            out += "\t";
         }
         else
         {
            out += n;
         }
      }
      else
      {
         out += c;
      }
   }
   return out;
}

// Sends commands one at a time so the chat channel isn't flooded.
var gmSendQueue = [];
var gmSendTimer = undefined;
function gmQueueSend(cmd)
{
   gmSendQueue.push(cmd);
   if(gmSendTimer == undefined)
   {
      gmSendTimer = setInterval(function()
      {
         if(gmSendQueue.length == 0)
         {
            clearInterval(gmSendTimer);
            gmSendTimer = undefined;
            return;
         }
         gmSend(String(gmSendQueue.shift()));
      }, 150);
   }
}

// --- script tab ------------------------------------------------------------------
// Left: the shared script library (templates first). Right: the editor for the selected
// NPC/prop's script, or a library entry. Templates take parameters through a small form.

// Selected entity kind for "!npc script" / "!prop script": the editor's selection.
function gmScriptKind()
{
   return gmEdit[0] == "prop" ? "prop" : "npc";
}

// guid is the entity guid, or "lib:<name>" when the editor holds a library entry.
var gmScript = { kind: "", guid: "", source: "", loading: "", status: "" };

// Library entries: { name, template, description, params: [[key, type, default], ...] }.
var gmScriptLib = [];
var gmScriptLibLoading = [];
var gmScriptLibSel = "";
var gmScriptLibAsked = false;
var gmScriptForm = false;
var GM_LIB_ROWS = 12;
var gmScriptLibFilter = "";
var gmScriptLibScroll = 0;
var gmScriptHelpShown = true;
// Unsaved editor text, kept across redraws (server replies redraw the tab): key is the entity guid or "lib:<name>".
var gmScriptDraft = { key: "", text: "" };
var GM_SCRIPT_MAX = 16000;

var GM_SCRIPT_HELP = "HOOKS (define any of them)\r" +
   "onSpawn()  when saved or loaded\r" +
   "onSecond()  every second\r" +
   "onInteract(player)  clicked\r" +
   "onProximity(player)  walks up (8 m)\r" +
   "onHear(player, message)  says something nearby (10 m)\r" +
   "onChoice(player, hook)  dialogue answer with a hook\r" +
   "\rNPC (the global npc)\r" +
   "npc.guid  npc.name  npc.position  npc.zone  npc.isMoving\r" +
   "npc:say(text)\r" +
   "npc:sayLocalized(stringId)\r" +
   "npc:face(player)\r" +
   "npc:moveTo(x, y, z [, direct])\r" +
   "npc:setSpeed(speed)  npc:setHeading(degrees)\r" +
   "npc:emote(animId [, loop])  npc:playAnimation(animId)\r" +
   "npc:setBaseAnimation(animId)\r" +
   "npc:playEffect(effectId)\r" +
   "npc:addEffectTag(effectId)  npc:removeEffectTag(tagId)\r" +
   "npc:setCollidable(true|false)\r" +
   "npc:getNearbyPlayers(radius)\r" +
   "npc:getPlayersOnTop(radius, minHeight, maxHeight)\r" +
   "npc:startConversation(player [, nodeId])\r" +
   "\rPLAYER\r" +
   "player.name  player.guid  player.position\r" +
   "player:message(text)\r" +
   "player:giveItem(itemId [, qty])\r" +
   "player:acceptQuest(questId)\r" +
   "player:hasCompletedQuest(questId)\r" +
   "\rZONE (npc.zone)\r" +
   "zone.id  zone.name\r" +
   "zone:broadcast(text)\r" +
   "zone:broadcastNear(text, x, z, radius)\r" +
   "zone:spawnNpc(npcId, x, y, z, heading)\r" +
   "zone:setClientAreaEnabled(areaId, on)\r" +
   "zone:isSeasonActive(name)\r" +
   "\rLUA\r" +
   "math string table pairs ipairs next select type tostring tonumber pcall error assert unpack print\r" +
   "\r16 KB per script, 100 ms per hook.";

// Hook skeletons the Insert buttons add: [name, code].
var GM_SCRIPT_SNIPPETS = [
   ["onSpawn", "function onSpawn()\r    npc:say(\"I'm awake!\")\rend"],
   ["onSecond", "local clock = 0\r\rfunction onSecond()\r    clock = clock + 1\rend"],
   ["onInteract", "function onInteract(player)\r    npc:face(player)\r    npc:say(\"Hello, \" .. player.name .. \"!\")\rend"],
   ["onProximity", "function onProximity(player)\r    npc:face(player)\r    npc:say(\"Welcome, \" .. player.name .. \"!\")\rend"],
   ["onHear", "function onHear(player, message)\r    if string.find(string.lower(message), \"hello\", 1, true) then\r        npc:face(player)\r        npc:say(\"Hello to you too, \" .. player.name .. \"!\")\r    end\rend"],
   ["onChoice", "function onChoice(player, hook)\r    if hook == \"thanks\" then\r        player:message(\"The NPC smiles at you.\")\r    end\rend"]
];

// Every space escaped, for values passed as key=value arguments.
function gmEscapeArg(s)
{
   return frHelperClass.stringReplace(gmEscapeText(String(s)), " ", "\\s");
}

function gmScriptLibEntry(name)
{
   var i;
   for(i = 0; i < gmScriptLib.length; i++)
   {
      if(gmScriptLib[i].name == name)
      {
         return gmScriptLib[i];
      }
   }
   return undefined;
}

// Library entries matching the filter box.
function gmScriptLibShown()
{
   var shown = [];
   var filter = gmScriptLibFilter.toLowerCase();
   var i;
   for(i = 0; i < gmScriptLib.length; i++)
   {
      var e = gmScriptLib[i];
      if(filter == "" || e.name.toLowerCase().indexOf(filter) != -1 || String(e.description).toLowerCase().indexOf(filter) != -1)
      {
         shown.push(e);
      }
   }
   return shown;
}

// What the editor is editing: a library entry, or the selected NPC/prop.
function gmScriptKey()
{
   return gmScript.guid.substr(0, 4) == "lib:" ? gmScript.guid : String(gmEdit[1]);
}

// Multi-line input in a rimmed box; clicking the box focuses it.
function gmTextArea(parent, name, x, y, w, h, value, maxChars, size)
{
   var box = parent.createEmptyMovieClip(name + "_box", parent.getNextHighestDepth());
   box._x = x;
   box._y = y;
   box.lineStyle(2, GM_GOLD, 100);
   box.beginFill(0x0B1E3A, 100);
   gmRoundRect(box, 0, 0, w, h, 8);
   box.endFill();
   parent.createTextField(name, parent.getNextHighestDepth(), x + 6, y + 4, w - 12, h - 8);
   var t = parent[name];
   t.embedFonts = true;
   t.type = "input";
   t.selectable = true;
   t.multiline = true;
   t.wordWrap = true;
   t.maxChars = maxChars;
   t.setNewTextFormat(gmFormat(size == undefined ? 12 : size, GM_TEXT, false));
   t.text = value == undefined ? "" : String(value);
   box.field = t;
   box.onPress = function()
   {
      gmFocusInput(this.field);
   };
   return t;
}

function gmRenderScript(content)
{
   if(!gmScriptLibAsked)
   {
      gmScriptLibAsked = true;
      gmSend("!scriptlib list");
   }

   gmRenderScriptLibrary(content);

   var x = 236;
   var hasEntity = gmEdit[0] == "npc" || gmEdit[0] == "prop";
   var kind = gmScriptKind();
   var isLib = gmScript.guid.substr(0, 4) == "lib:";
   var title = isLib ? "Library script: " + gmScript.guid.substr(4)
      : hasEntity ? "Script for " + kind + " " + (gmEdit[2] != "" && kind == "npc" ? gmEdit[2] + " (" + gmEdit[1] + ")" : gmEdit[1])
         + (gmScript.guid == gmEdit[1] ? "" : "  - press Load to fetch it")
      : "No NPC or prop selected (god mode, then click one). You can still write and save library scripts.";
   gmText(content, "title_txt", title, x, 0, 660, 20, 13, GM_TEXT_GOLD, true).wordWrap = false;

   if(gmScriptForm)
   {
      gmRenderScriptForm(content, x);
   }
   else
   {
      gmRenderScriptEditor(content, x, hasEntity, kind, isLib);
   }
   gmStatus("Script editor: write Lua hooks for the selected NPC or prop, or save reusable scripts to the library.");
}

function gmRenderScriptLibrary(content)
{
   gmText(content, "lib_label", "Library", 0, 0, 120, 20, 13, GM_TEXT_GOLD, true);
   gmButton(content, "libreload_mc", "Refresh", 150, 0, 70, 20, function()
   {
      gmSend("!scriptlib list");
   });
   gmInput(content, "libfilter_txt", 0, 24, 220, 22, gmScriptLibFilter, "Filter, then Enter...", function(text)
   {
      gmScriptLibFilter = text;
      gmScriptLibScroll = 0;
      gmShowTab("script");
   });

   var shown = gmScriptLibShown();
   gmScriptLibScroll = Math.max(0, Math.min(gmScriptLibScroll, shown.length - GM_LIB_ROWS));
   var i;
   for(i = 0; i < GM_LIB_ROWS && gmScriptLibScroll + i < shown.length; i++)
   {
      var entry = shown[gmScriptLibScroll + i];
      var selected = entry.name == gmScriptLibSel;
      var row = content.createEmptyMovieClip("lib" + i + "_mc", content.getNextHighestDepth());
      row._y = 52 + i * 24;
      gmPanelShape(row, 220, 21, 6, selected ? GM_BLUE_ACTIVE : GM_BLUE_CELL, GM_BLUE_BOTTOM, selected ? GM_ACCENT : GM_GOLD_DARK, 1);
      gmText(row, "label_txt", entry.name, 6, 2, 160, 18, 11, GM_TEXT, selected).wordWrap = false;
      gmText(row, "kind_txt", entry.template ? "template" : "saved", 150, 3, 64, 16, 9, entry.template ? GM_TEXT_GOLD : GM_TEXT_DIM, false, "right").wordWrap = false;
      row.entryName = entry.name;
      row.onRelease = function()
      {
         gmScriptLibSel = this.entryName;
         gmScriptForm = false;
         gmShowTab("script");
      };
   }
   if(shown.length == 0)
   {
      gmText(content, "libempty_txt", gmScriptLib.length == 0 ? "No library entries yet." : "Nothing matches the filter.", 0, 56, 220, 40, 11, GM_TEXT_DIM, false);
   }
   if(shown.length > GM_LIB_ROWS)
   {
      gmButton(content, "libup_mc", "^", 0, 342, 30, 20, function()
      {
         gmScriptLibScroll -= GM_LIB_ROWS - 1;
         gmShowTab("script");
      });
      gmButton(content, "libdown_mc", "v", 34, 342, 30, 20, function()
      {
         gmScriptLibScroll += GM_LIB_ROWS - 1;
         gmShowTab("script");
      });
      gmText(content, "libpage_txt", (gmScriptLibScroll + 1) + "-" + Math.min(shown.length, gmScriptLibScroll + GM_LIB_ROWS) + " of " + shown.length,
         70, 344, 150, 18, 10, GM_TEXT_DIM, false).wordWrap = false;
   }

   var sel = gmScriptLibEntry(gmScriptLibSel);
   if(sel == undefined)
   {
      gmText(content, "libhint_txt", "Pick a script to read about it, use it on the selected NPC or open it in the editor.", 0, 368, 220, 48, 10, GM_TEXT_DIM, false);
      return;
   }
   gmText(content, "libdesc_txt", sel.description + (sel.params.length > 0 ? "  (" + sel.params.length + " setting" + (sel.params.length == 1 ? "" : "s") + ")" : ""),
      0, 366, 220, 50, 10, GM_TEXT_DIM, false);
   gmButton(content, "libuse_mc", "Use on " + gmScriptKind(), 0, 420, 100, 22, function()
   {
      var e = gmScriptLibEntry(gmScriptLibSel);
      if(gmEdit[0] != "npc" && gmEdit[0] != "prop")
      {
         gmStatus("Select an NPC or prop first (god mode, then click it).");
         return;
      }
      if(e.params.length > 0)
      {
         gmScriptForm = true;
         gmShowTab("script");
         return;
      }
      gmSend("!" + gmScriptKind() + " script use " + e.name);
   }, 0x2E7D5B);
   gmButton(content, "libopen_mc", "Open", 104, 420, 56, 22, function()
   {
      gmSend("!scriptlib show " + gmScriptLibSel);
   });
   if(!sel.template)
   {
      gmConfirmButton(content, "libdel_mc", "Delete", 164, 420, 56, 22, function()
      {
         gmSend("!scriptlib delete " + gmScriptLibSel);
      });
   }
}

function gmRenderScriptEditor(content, x, hasEntity, kind, isLib)
{
   var w = gmScriptHelpShown ? 414 : 660;
   var h = 318;
   var key = gmScriptKey();
   var text = gmScriptDraft.key == key ? gmScriptDraft.text : (isLib || gmScript.guid == gmEdit[1]) ? gmScript.source : "";

   var area = content.createEmptyMovieClip("area_mc", content.getNextHighestDepth());
   area._x = x;
   area._y = 24;
   area.lineStyle(2, GM_GOLD, 100);
   area.beginFill(0x0B1E3A, 100);
   gmRoundRect(area, 0, 0, w, h, 8);
   area.endFill();
   // Line number gutter.
   area.lineStyle();
   area.beginFill(0x13284D, 100);
   gmRoundRect(area, 2, 2, 34, h - 4, 6);
   area.endFill();

   content.createTextField("gutter_txt", content.getNextHighestDepth(), x + 2, 28, 30, h - 8);
   var gutter = content.gutter_txt;
   gutter.embedFonts = true;
   gutter.selectable = false;
   gutter.multiline = true;
   gutter.wordWrap = false;
   gutter.setNewTextFormat(gmFormat(11, GM_TEXT_DIM, false, "right"));

   content.createTextField("source_txt", content.getNextHighestDepth(), x + 40, 28, w - 46, h - 8);
   var t = content.source_txt;
   t.embedFonts = true;
   t.type = "input";
   t.selectable = true;
   t.multiline = true;
   t.wordWrap = false;
   t.maxChars = GM_SCRIPT_MAX;
   t.setNewTextFormat(gmFormat(11, GM_TEXT, false));
   t.text = text;
   t.gutter = gutter;
   t.onChanged = function()
   {
      gmScriptDraft = { key: gmScriptKey(), text: String(this.text) };
      gmScriptGutter(this);
   };
   t.onScroller = function()
   {
      this.gutter.scroll = this.scroll;
   };
   area.field = t;
   area.onPress = function()
   {
      gmFocusInput(this.field);
   };

   if(gmScriptHelpShown)
   {
      var help = content.createEmptyMovieClip("help_mc", content.getNextHighestDepth());
      help._x = x + w + 10;
      help._y = 24;
      help.lineStyle(1, GM_RIM, 60);
      help.beginFill(0x0A1440, 85);
      gmRoundRect(help, 0, 0, 660 - w - 10, h, 8);
      help.endFill();
      var ht = gmText(content, "help_txt", GM_SCRIPT_HELP, x + w + 18, 28, 660 - w - 26, h - 8, 10, GM_TEXT, false);
      ht.wordWrap = true;
      ht.mouseWheelEnabled = true;
   }

   // Row 1: the selected NPC/prop's script, the counter and the help toggle.
   var y = 348;
   if(hasEntity)
   {
      gmButton(content, "load_mc", "Load", x, y, 60, 22, function()
      {
         gmScriptDraft = { key: "", text: "" };
         gmSend("!" + gmScriptKind() + " script show");
      });
      gmButton(content, "save_mc", "Save to " + kind, x + 64, y, 96, 22, function()
      {
         gmSaveScript("!" + gmScriptKind() + " script", "commit", gmEdit[1]);
      }, 0x2E7D5B);
      gmButton(content, "test_mc", "Test", x + 164, y, 56, 22, function()
      {
         gmSend("!" + gmScriptKind() + " script test");
      });
      gmConfirmButton(content, "clear_mc", "Remove", x + 224, y, 72, 22, function()
      {
         gmSend("!" + gmScriptKind() + " script clear");
      });
   }
   gmButton(content, "revert_mc", "Revert", x + 300, y, 64, 22, function()
   {
      gmScriptDraft = { key: "", text: "" };
      gmShowTab("script");
      gmStatus("Edits dropped; showing the last loaded or saved text.");
   });
   gmText(content, "count_txt", "", x + 370, y + 3, 196, 18, 10, GM_TEXT_DIM, false, "right").wordWrap = false;
   gmButton(content, "help_btn", gmScriptHelpShown ? "Hide API" : "Show API", x + 572, y, 88, 22, function()
   {
      gmScriptHelpShown = !gmScriptHelpShown;
      gmShowTab("script");
   });

   // Row 2: hook skeletons.
   y = 376;
   gmText(content, "insert_label", "Insert", x, y + 3, 50, 18, 11, GM_TEXT_DIM, true);
   var i;
   for(i = 0; i < GM_SCRIPT_SNIPPETS.length; i++)
   {
      var b = gmButton(content, "snip" + i + "_mc", GM_SCRIPT_SNIPPETS[i][0], x + 52 + i * 101, y, 97, 22, function()
      {
         gmScriptInsert(this.index);
      });
      b.index = i;
   }

   // Row 3: save the text to the library, and the server's answer.
   y = 404;
   gmInput(content, "libname_txt", x, y, 170, 22, isLib ? gmScript.guid.substr(4) : "", "library name");
   gmButton(content, "libsave_mc", "Save to library", x + 174, y, 120, 22, function()
   {
      var name = gmFieldText(_root.gm_mc.content_mc.libname_txt);
      if(name == "")
      {
         gmStatus("Type a library name first.");
         return;
      }
      gmSaveScript("!scriptlib", "commit " + name, "lib:" + name);
   });
   var result = gmText(content, "result_txt", gmScript.status, x + 302, y - 2, 358, 40, 11,
      gmScript.status.substr(0, 6) == "Error:" ? 0xFF8A80 : GM_TEXT_GOLD, false);
   result.wordWrap = true;

   gmScriptGutter(t);
}

// Line numbers beside the source, and the size counter.
function gmScriptGutter(t)
{
   var source = frHelperClass.stringReplace(String(t.text), "\n", "\r");
   var lines = source.split("\r").length;
   var numbers = "";
   var i;
   for(i = 1; i <= lines; i++)
   {
      numbers += (i > 1 ? "\r" : "") + i;
   }
   t.gutter.text = numbers;
   t.gutter.scroll = t.scroll;
   var count = _root.gm_mc.content_mc.count_txt;
   if(count != undefined)
   {
      count.text = lines + " line" + (lines == 1 ? "" : "s") + "   " + source.length + " / " + GM_SCRIPT_MAX;
   }
}

// Adds a hook skeleton to the end of the source, unless the script already defines that hook.
function gmScriptInsert(index)
{
   var t = _root.gm_mc.content_mc.source_txt;
   var name = GM_SCRIPT_SNIPPETS[index][0];
   var source = String(t.text);
   if(source.indexOf("function " + name + "(") != -1)
   {
      gmStatus("The script already has " + name + ".");
      return;
   }
   t.text = source + (source == "" ? "" : "\r\r") + GM_SCRIPT_SNIPPETS[index][1];
   gmScriptDraft = { key: gmScriptKey(), text: String(t.text) };
   gmScriptGutter(t);
   t.scroll = t.maxscroll;
   t.gutter.scroll = t.scroll;
   gmStatus("Added " + name + ". Edit it, then save.");
}

// Settings form for a template: one input per parameter, then apply to the selected NPC/prop.
function gmRenderScriptForm(content, x)
{
   var e = gmScriptLibEntry(gmScriptLibSel);
   gmText(content, "form_label", "Use \"" + e.name + "\" on the selected " + gmScriptKind(), x, 28, 660, 20, 13, GM_TEXT, true).wordWrap = false;
   gmText(content, "form_desc", e.description, x, 50, 660, 36, 11, GM_TEXT_DIM, false);
   var i;
   for(i = 0; i < e.params.length && i < 10; i++)
   {
      var p = e.params[i];
      var col = i % 2;
      var px = x + col * 334;
      var py = 94 + Math.floor(i / 2) * 32;
      gmText(content, "param" + i + "_label", p[0], px, py + 4, 120, 18, 11, GM_TEXT_DIM, true).wordWrap = false;
      gmText(content, "param" + i + "_type", p[1], px, py + 18, 120, 14, 9, GM_TEXT_DIM, false).wordWrap = false;
      var field = gmInput(content, "param" + i + "_txt", px + 124, py, 196, 24, p[2], "");
      field.maxChars = 200;
   }
   gmButton(content, "apply_mc", "Apply", x, 270, 90, 24, function()
   {
      var e2 = gmScriptLibEntry(gmScriptLibSel);
      var cmd = "!" + gmScriptKind() + " script use " + e2.name;
      var j;
      for(j = 0; j < e2.params.length && j < 10; j++)
      {
         cmd += " " + e2.params[j][0] + "=" + gmEscapeArg(_root.gm_mc.content_mc["param" + j + "_txt"].text);
      }
      gmSend(cmd);
      gmScriptForm = false;
   }, 0x2E7D5B);
   gmButton(content, "cancel_mc", "Back", x + 94, 270, 70, 24, function()
   {
      gmScriptForm = false;
      gmShowTab("script");
   });
}

// Sends the editor text in escaped chunks: "<prefix> append <chunk>" ..., then "<prefix> <commit>".
function gmSaveScript(prefix, commit, target)
{
   var t = _root.gm_mc.content_mc.source_txt;
   if(t == undefined)
   {
      return;
   }
   var source = String(t.text);
   gmScript.source = source;
   gmScript.guid = target;
   gmScriptDraft = { key: "", text: "" };
   // Escape per chunk so no escape sequence is split; each stays well under the chat line limit.
   var i;
   for(i = 0; i < source.length; i += 80)
   {
      gmQueueSend(prefix + " append " + gmEscapeText(source.substr(i, 80)));
   }
   gmQueueSend(prefix + " " + commit);
   gmScript.status = "Saving...";
   _root.gm_mc.content_mc.result_txt.text = gmScript.status;
   fscommand("FR_event", "killFocus()");
}

// scriptlib|begin, scriptlib|entry|<name>|<template>|<description>|<key:type:default;...>, scriptlib|end
function gmHandleScriptLib(parts)
{
   var step = parts[2];
   if(step == "begin")
   {
      gmScriptLibLoading = [];
      return;
   }
   if(step == "entry")
   {
      // One field per parameter: <key>:<type>:<escaped default> (the default may contain ':').
      var params = [];
      var i;
      for(i = 6; i < parts.length; i++)
      {
         if(String(parts[i]) == "")
         {
            continue;
         }
         var bits = String(parts[i]).split(":");
         params.push([bits[0], bits[1], gmUnescapeText(bits.slice(2).join(":"))]);
      }
      gmScriptLibLoading.push({ name: parts[3], template: parts[4] == "1", description: gmUnescapeText(parts[5]), params: params });
      return;
   }
   if(step == "end")
   {
      gmScriptLib = gmScriptLibLoading;
      if(_root.gm_mc._visible && gmTab == "script")
      {
         gmShowTab("script");
      }
   }
}

// --- conversation tab ------------------------------------------------------------
// Click-built: NPC lines are cards; answers lead to a new line, the end, or an existing line.
// Node ids are picked here (highest + 1), so nothing has to be typed but the text.

var gmConv = { guid: "", start: 1, nodes: [], choices: [] };
var gmConvSel = 0;
var gmConvScroll = 0;
var gmConvLinkFrom = undefined;
var GM_CONV_CARDS = 12;
var GM_CONV_MAX_ANSWERS = 8;
// Server index of the answer being edited on the selected line, or -1.
var gmConvAnswerSel = -1;

function gmOneLine(s, max)
{
   var r = frHelperClass.stringReplace(String(s), "\r", " / ");
   return r.length > max ? r.substr(0, max - 2) + "..." : r;
}

function gmConvNode(id)
{
   var i;
   for(i = 0; i < gmConv.nodes.length; i++)
   {
      if(String(gmConv.nodes[i].id) == String(id))
      {
         return gmConv.nodes[i];
      }
   }
   return undefined;
}

function gmConvAnswers(id)
{
   var answers = [];
   var i;
   for(i = 0; i < gmConv.choices.length; i++)
   {
      if(String(gmConv.choices[i].node) == String(id))
      {
         answers.push(gmConv.choices[i]);
      }
   }
   return answers;
}

function gmConvAnswer(id, index)
{
   var answers = gmConvAnswers(id);
   var i;
   for(i = 0; i < answers.length; i++)
   {
      if(String(answers[i].index) == String(index))
      {
         return answers[i];
      }
   }
   return undefined;
}

function gmConvNextId()
{
   var next = 1;
   var i;
   for(i = 0; i < gmConv.nodes.length; i++)
   {
      next = Math.max(next, Number(gmConv.nodes[i].id) + 1);
   }
   return next;
}

function gmConvTarget(next)
{
   if(String(next) == "0")
   {
      return "ends";
   }
   var node = gmConvNode(next);
   return node == undefined ? "missing line " + next : "#" + next + " \"" + gmOneLine(node.text, 20) + "\"";
}

// Lines a player can reach from the first one; the rest are drawn dimmed.
function gmConvReachable()
{
   var seen = {};
   var queue = [String(gmConv.start)];
   while(queue.length > 0)
   {
      var id = String(queue.shift());
      if(seen[id] || gmConvNode(id) == undefined)
      {
         continue;
      }
      seen[id] = true;
      var answers = gmConvAnswers(id);
      var i;
      for(i = 0; i < answers.length; i++)
      {
         if(String(answers[i].next) != "0")
         {
            queue.push(String(answers[i].next));
         }
      }
   }
   return seen;
}

// Lines with an answer leading to the given line.
function gmConvIncoming(id)
{
   var from = [];
   var i;
   for(i = 0; i < gmConv.choices.length; i++)
   {
      var c = gmConv.choices[i];
      if(String(c.next) == String(id) && String(c.node) != String(id))
      {
         var label = "#" + c.node;
         var j;
         var known = false;
         for(j = 0; j < from.length; j++)
         {
            known = known || from[j] == label;
         }
         if(!known)
         {
            from.push(label);
         }
      }
   }
   return from;
}

function gmRenderConv(content)
{
   if(gmEdit[0] != "npc")
   {
      gmText(content, "none_txt", "Select an NPC first (god mode, then click it). Its dialogue is built here: the lines it says, and the answers players can pick.",
         0, 0, 700, 40, 13, GM_TEXT_DIM, false);
      gmStatus("Dialogue");
      return;
   }

   var loaded = gmConv.guid == gmEdit[1];
   if(!loaded)
   {
      gmText(content, "load_txt", "Loading dialogue...", 0, 0, 300, 20, 13, GM_TEXT_DIM, false);
      gmSend("!conv show");
      return;
   }

   if(gmConvNode(gmConvSel) == undefined)
   {
      gmConvSel = gmConvNode(gmConv.start) != undefined ? gmConv.start : (gmConv.nodes.length > 0 ? gmConv.nodes[0].id : 0);
      gmConvAnswerSel = -1;
   }
   if(gmConvAnswer(gmConvSel, gmConvAnswerSel) == undefined)
   {
      gmConvAnswerSel = -1;
   }

   gmText(content, "title_txt", (gmEdit[2] == "" ? "NPC " + gmEdit[1] : gmEdit[2]) + " - dialogue", 0, 2, 360, 22, 14, GM_TEXT_GOLD, true).wordWrap = false;
   gmButton(content, "newline_mc", "+ New line", 896 - 444, 0, 96, 24, function()
   {
      var id = gmConvNextId();
      gmConvSel = id;
      gmConvAnswerSel = -1;
      gmSend("!conv node " + id + " " + gmEscapeText("(new line)"));
   }, 0x2E7D5B);
   gmButton(content, "test_mc", "Test", 896 - 344, 0, 64, 24, function()
   {
      gmSend("!conv test");
   });
   gmButton(content, "reload_mc", "Reload", 896 - 276, 0, 70, 24, function()
   {
      gmSend("!conv show");
   });
   gmButton(content, "copy_mc", "Copy from NPC...", 896 - 202, 0, 118, 24, function()
   {
      gmPrompt("!conv copy ");
      gmStatus("Type the guid of an NPC in this zone whose dialogue to copy, then Enter. This replaces the current dialogue.");
   });
   gmConfirmButton(content, "clear_mc", "Clear all", 896 - 80, 0, 80, 24, function()
   {
      gmSend("!conv clear");
   });

   gmRenderConvCards(content);
   gmRenderConvEditor(content);

   if(gmConvLinkFrom != undefined)
   {
      gmStatus("Click the line this answer should lead to (or pick another tab to cancel).");
   }
   else
   {
      gmStatus(gmConv.nodes.length + " line(s). The starred line is what players hear first; dimmed lines can't be reached from it.");
   }
}

// Left column: one card per NPC line.
function gmRenderConvCards(content)
{
   var reachable = gmConvReachable();
   var targets = {};
   var answers = gmConvAnswers(gmConvSel);
   var i;
   for(i = 0; i < answers.length; i++)
   {
      targets[String(answers[i].next)] = true;
   }

   var list = content.createEmptyMovieClip("cards_mc", content.getNextHighestDepth());
   list._y = 32;
   gmConvScroll = Math.max(0, Math.min(gmConvScroll, gmConv.nodes.length - GM_CONV_CARDS));
   for(i = 0; i < GM_CONV_CARDS && gmConvScroll + i < gmConv.nodes.length; i++)
   {
      var node = gmConv.nodes[gmConvScroll + i];
      var id = String(node.id);
      var selected = id == String(gmConvSel);
      var card = list.createEmptyMovieClip("card" + i, 10 + i);
      card._y = i * 30;
      var rim = selected ? GM_ACCENT : targets[id] ? 0x7CFC9A : GM_GOLD_DARK;
      gmPanelShape(card, 270, 27, 7, selected ? GM_BLUE_ACTIVE : GM_BLUE_CELL, GM_BLUE_BOTTOM, rim, selected || targets[id] ? 2 : 1);
      var star = id == String(gmConv.start) ? "* " : "";
      gmText(card, "id_txt", star + "#" + id, 6, 5, 44, 18, 10, id == String(gmConv.start) ? GM_TEXT_GOLD : GM_TEXT_DIM, true).wordWrap = false;
      gmText(card, "label_txt", gmOneLine(node.text, 30), 50, 4, 176, 18, 11, GM_TEXT, selected).wordWrap = false;
      var count = gmConvAnswers(id).length;
      gmText(card, "count_txt", count == 0 ? "ends" : count + " ans", 226, 5, 40, 18, 9, GM_TEXT_DIM, false, "right").wordWrap = false;
      card._alpha = reachable[id] ? 100 : 55;
      card.nodeId = node.id;
      card.onRelease = function()
      {
         if(gmConvLinkFrom != undefined)
         {
            var link = gmConvLinkFrom;
            gmConvLinkFrom = undefined;
            if(link.index != undefined)
            {
               gmSend("!conv choice next " + link.node + " " + link.index + " " + this.nodeId);
            }
            else
            {
               gmSend("!conv choice add " + link.node + " " + this.nodeId + " " + gmEscapeText(link.text));
            }
            return;
         }
         gmConvSel = this.nodeId;
         gmConvAnswerSel = -1;
         gmShowTab("conv");
      };
   }
   if(gmConv.nodes.length > GM_CONV_CARDS)
   {
      gmButton(content, "up_mc", "^", 0, 32 + GM_CONV_CARDS * 30 + 4, 30, 20, function()
      {
         gmConvScroll -= GM_CONV_CARDS - 1;
         gmShowTab("conv");
      });
      gmButton(content, "down_mc", "v", 34, 32 + GM_CONV_CARDS * 30 + 4, 30, 20, function()
      {
         gmConvScroll += GM_CONV_CARDS - 1;
         gmShowTab("conv");
      });
   }
   if(gmConv.nodes.length == 0)
   {
      gmText(content, "empty_txt", "No lines yet. Press + New line to write what the NPC says first.", 0, 36, 270, 40, 11, GM_TEXT_DIM, false);
   }
   gmText(content, "legend_txt", "* first line   green rim: where the selected line's answers lead", 0, 424, 280, 18, 9, GM_TEXT_DIM, false).wordWrap = false;
}

// Right side: the selected line's text, its answers, and the selected answer's details.
function gmRenderConvEditor(content)
{
   var node = gmConvNode(gmConvSel);
   if(node == undefined)
   {
      return;
   }
   var x = 286;
   var w = 610;
   var isStart = String(node.id) == String(gmConv.start);

   gmText(content, "line_label", "Line #" + node.id + (isStart ? "  - players hear this first" : ""), x, 32, 400, 18, 12, isStart ? GM_TEXT_GOLD : GM_TEXT, true).wordWrap = false;
   var incoming = gmConvIncoming(node.id);
   gmText(content, "incoming_txt", incoming.length == 0 ? (isStart ? "" : "No answer leads here yet.") : "Answers from " + incoming.join(", ") + " lead here.",
      x + 400, 34, 210, 18, 10, GM_TEXT_DIM, false, "right").wordWrap = false;

   gmTextArea(content, "says_txt", x, 52, w, 76, node.text, 400, 12);

   gmButton(content, "savetext_mc", "Save text", x, 134, 90, 22, function()
   {
      gmSend("!conv node " + gmConvSel + " " + gmEscapeText(String(_root.gm_mc.content_mc.says_txt.text)));
   }, 0x2E7D5B);
   if(!isStart)
   {
      gmButton(content, "start_mc", "Make first", x + 94, 134, 90, 22, function()
      {
         gmSend("!conv start " + gmConvSel);
      });
   }
   gmConfirmButton(content, "delline_mc", "Delete line", x + 188, 134, 96, 22, function()
   {
      gmSend("!conv node del " + gmConvSel);
   });

   // Answers, one row each; click a row to edit it below.
   var answers = gmConvAnswers(gmConvSel);
   gmText(content, "answers_label", "Player answers (" + answers.length + "/" + GM_CONV_MAX_ANSWERS + ")", x, 166, 300, 18, 12, GM_TEXT_GOLD, true).wordWrap = false;
   var i;
   for(i = 0; i < answers.length && i < GM_CONV_MAX_ANSWERS; i++)
   {
      var a = answers[i];
      var y = 186 + i * 26;
      var selected = String(a.index) == String(gmConvAnswerSel);
      var row = content.createEmptyMovieClip("answer" + i + "_mc", content.getNextHighestDepth());
      row._x = x;
      row._y = y;
      gmPanelShape(row, w - 72, 23, 6, selected ? GM_BLUE_ACTIVE : GM_BLUE_CELL, GM_BLUE_BOTTOM, selected ? GM_ACCENT : GM_GOLD_DARK, 1);
      gmText(row, "text_txt", (i + 1) + ". \"" + gmOneLine(a.text, 34) + "\"", 6, 3, 250, 18, 11, GM_TEXT, selected).wordWrap = false;
      gmText(row, "next_txt", "-> " + gmConvTarget(a.next), 258, 4, 176, 18, 10, String(a.next) == "0" ? GM_TEXT_DIM : 0x9FF5B5, false).wordWrap = false;
      var extra = (Number(a.give) > 0 ? "item " + a.give + (Number(a.qty) > 1 ? " x" + a.qty : "") : "") + (a.hook != "" ? (Number(a.give) > 0 ? ", " : "") + "hook " + a.hook : "");
      gmText(row, "extra_txt", extra, 436, 4, 98, 18, 9, GM_TEXT_GOLD, false, "right").wordWrap = false;
      row.index = a.index;
      row.onRelease = function()
      {
         gmConvAnswerSel = String(gmConvAnswerSel) == String(this.index) ? -1 : this.index;
         gmShowTab("conv");
      };
      var up = gmButton(content, "aup" + i + "_mc", "^", x + w - 68, y, 20, 23, function()
      {
         gmConvAnswerSel = -1;
         gmSend("!conv choice up " + gmConvSel + " " + this.index);
      });
      up.index = a.index;
      var down = gmButton(content, "adn" + i + "_mc", "v", x + w - 46, y, 20, 23, function()
      {
         gmConvAnswerSel = -1;
         gmSend("!conv choice down " + gmConvSel + " " + this.index);
      });
      down.index = a.index;
      var del = gmButton(content, "adel" + i + "_mc", "x", x + w - 24, y, 24, 23, function()
      {
         gmConvAnswerSel = -1;
         gmSend("!conv choice del " + gmConvSel + " " + this.index);
      }, 0xC0392B);
      del.index = a.index;
   }

   // New answer: type it, then pick where it leads.
   if(answers.length < GM_CONV_MAX_ANSWERS)
   {
      var ny = 186 + answers.length * 26 + 2;
      var field = gmInput(content, "answer_txt", x, ny, 300, 22, "", "New answer the player can pick...");
      field.maxChars = 400;
      gmButton(content, "tonew_mc", "+ to a new line", x + 304, ny, 108, 22, function()
      {
         var text = String(_root.gm_mc.content_mc.answer_txt.text);
         var id = gmConvNextId();
         gmQueueSend("!conv node " + id + " " + gmEscapeText("(new line)"));
         gmQueueSend("!conv choice add " + gmConvSel + " " + id + " " + gmEscapeText(text == "" ? "..." : text));
         gmConvSel = id;
         gmConvAnswerSel = -1;
      }, 0x2E7D5B);
      gmButton(content, "toend_mc", "+ that ends", x + 416, ny, 90, 22, function()
      {
         var text = String(_root.gm_mc.content_mc.answer_txt.text);
         gmSend("!conv choice add " + gmConvSel + " 0 " + gmEscapeText(text == "" ? "Goodbye" : text));
      });
      gmButton(content, "tolink_mc", "+ to a line...", x + 510, ny, 100, 22, function()
      {
         var text = String(_root.gm_mc.content_mc.answer_txt.text);
         gmConvLinkFrom = { node: gmConvSel, text: text == "" ? "..." : text };
         gmStatus("Click the line this answer should lead to.");
      });
   }

   gmRenderConvAnswerEditor(content, x, w);
}

// The selected answer: its text, where it leads, the item it gives and the script hook it calls.
function gmRenderConvAnswerEditor(content, x, w)
{
   var a = gmConvAnswer(gmConvSel, gmConvAnswerSel);
   if(a == undefined)
   {
      gmText(content, "ahint_txt", "Click an answer to change its text, where it leads, the item it gives or the script hook it runs (onChoice).",
         x, 400, w, 40, 10, GM_TEXT_DIM, false);
      return;
   }
   var y = 398;
   var text = gmInput(content, "atext_txt", x, y, 280, 22, a.text, "Answer text", function(value)
   {
      gmSend("!conv choice text " + gmConvSel + " " + gmConvAnswerSel + " " + gmEscapeText(value));
   });
   text.maxChars = 400;
   gmButton(content, "atext_mc", "Save", x + 284, y, 50, 22, function()
   {
      gmSend("!conv choice text " + gmConvSel + " " + gmConvAnswerSel + " " + gmEscapeText(String(_root.gm_mc.content_mc.atext_txt.text)));
   }, 0x2E7D5B);
   gmText(content, "alead_label", "Leads to", x + 342, y + 3, 60, 18, 10, GM_TEXT_DIM, true).wordWrap = false;
   gmButton(content, "anew_mc", "new line", x + 400, y, 70, 22, function()
   {
      var id = gmConvNextId();
      gmQueueSend("!conv node " + id + " " + gmEscapeText("(new line)"));
      gmQueueSend("!conv choice next " + gmConvSel + " " + gmConvAnswerSel + " " + id);
      gmConvSel = id;
      gmConvAnswerSel = -1;
   });
   gmButton(content, "aend_mc", "end", x + 474, y, 48, 22, function()
   {
      gmSend("!conv choice next " + gmConvSel + " " + gmConvAnswerSel + " 0");
   });
   gmButton(content, "apick_mc", "pick...", x + 526, y, 84, 22, function()
   {
      gmConvLinkFrom = { node: gmConvSel, index: gmConvAnswerSel };
      gmStatus("Click the line this answer should lead to.");
   });

   y = 424;
   gmText(content, "agive_label", "Gives item", x, y + 3, 70, 18, 10, GM_TEXT_DIM, true).wordWrap = false;
   gmInput(content, "agive_txt", x + 72, y, 76, 22, Number(a.give) > 0 ? a.give : "", "item id");
   gmInput(content, "aqty_txt", x + 152, y, 40, 22, Number(a.give) > 0 ? a.qty : "", "qty");
   gmButton(content, "agive_mc", "Set", x + 196, y, 40, 22, function()
   {
      var c = _root.gm_mc.content_mc;
      var item = gmFieldText(c.agive_txt);
      var qty = gmFieldText(c.aqty_txt);
      gmSend("!conv choice give " + gmConvSel + " " + gmConvAnswerSel + " " + (item == "" ? "0" : item) + " " + (qty == "" ? "1" : qty));
   });
   gmButton(content, "anogive_mc", "None", x + 240, y, 46, 22, function()
   {
      gmSend("!conv choice give " + gmConvSel + " " + gmConvAnswerSel + " 0");
   });
   gmText(content, "ahook_label", "Script hook", x + 300, y + 3, 76, 18, 10, GM_TEXT_DIM, true).wordWrap = false;
   gmInput(content, "ahook_txt", x + 378, y, 130, 22, a.hook, "onChoice hook");
   gmButton(content, "ahook_mc", "Set", x + 512, y, 40, 22, function()
   {
      var hook = frHelperClass.stringReplace(gmFieldText(_root.gm_mc.content_mc.ahook_txt), " ", "_");
      gmSend("!conv choice hook " + gmConvSel + " " + gmConvAnswerSel + " " + (hook == "" ? "none" : hook));
   });
   gmButton(content, "anohook_mc", "None", x + 556, y, 54, 22, function()
   {
      gmSend("!conv choice hook " + gmConvSel + " " + gmConvAnswerSel + " none");
   });
}

// --- quests tab ------------------------------------------------------------------
// Custom quests (ids 900000+) built on the quest system. The quest window shows generic
// locale text; the custom title, description and goal text reach the player as chat.

var gmQuestList = [];
var gmQuestListLoading = [];
var gmQuestListAsked = false;
var gmQuest = undefined;
var gmQuestLoading = undefined;
var gmQuestSel = 0;
var gmQuestNodes = [];
var gmQuestScroll = 0;
var GM_QUEST_ROWS = 14;
var GM_QUEST_MAX_GOALS = 8;

// The selected NPC's definition id (the edit line's 17th field), or "" when no NPC is selected.
function gmSelectedNpcDefinition()
{
   return gmEdit[0] == "npc" && gmEdit[16] != undefined && String(gmEdit[16]) != "0" ? String(gmEdit[16]) : "";
}

function gmRenderQuests(content)
{
   if(!gmQuestListAsked)
   {
      gmQuestListAsked = true;
      gmSend("!questedit list");
      gmSend("!questedit nodes");
   }

   // Left: quest list and "new".
   gmText(content, "quests_label", "Custom quests", 0, 0, 160, 20, 13, GM_TEXT_GOLD, true);
   gmButton(content, "qreload_mc", "Refresh", 180, 0, 70, 20, function()
   {
      gmSend("!questedit list");
   });
   gmQuestScroll = Math.max(0, Math.min(gmQuestScroll, gmQuestList.length - GM_QUEST_ROWS));
   var i;
   for(i = 0; i < GM_QUEST_ROWS && gmQuestScroll + i < gmQuestList.length; i++)
   {
      var q = gmQuestList[gmQuestScroll + i];
      var selected = String(q.id) == String(gmQuestSel);
      var row = content.createEmptyMovieClip("q" + i + "_mc", content.getNextHighestDepth());
      row._y = 26 + i * 25;
      gmPanelShape(row, 250, 22, 6, selected ? GM_BLUE_ACTIVE : GM_BLUE_CELL, GM_BLUE_BOTTOM, selected ? GM_ACCENT : GM_GOLD_DARK, 1);
      gmText(row, "label_txt", gmOneLine(q.title, 28), 6, 2, 170, 18, 11, GM_TEXT, selected).wordWrap = false;
      gmText(row, "state_txt", (q.published ? "live" : "draft") + "  " + q.goals + "g", 174, 4, 70, 16, 9, q.published ? 0x7CFC9A : GM_TEXT_GOLD, false, "right").wordWrap = false;
      row.questId = q.id;
      row.onRelease = function()
      {
         gmQuestSel = this.questId;
         gmSend("!questedit show " + this.questId);
      };
   }
   if(gmQuestList.length == 0)
   {
      gmText(content, "qempty_txt", "No custom quests yet. Name one below and press + New.", 0, 30, 250, 40, 11, GM_TEXT_DIM, false);
   }
   if(gmQuestList.length > GM_QUEST_ROWS)
   {
      gmButton(content, "qup_mc", "^", 0, 380, 30, 20, function()
      {
         gmQuestScroll -= GM_QUEST_ROWS - 1;
         gmShowTab("quests");
      });
      gmButton(content, "qdown_mc", "v", 34, 380, 30, 20, function()
      {
         gmQuestScroll += GM_QUEST_ROWS - 1;
         gmShowTab("quests");
      });
      gmText(content, "qpage_txt", (gmQuestScroll + 1) + "-" + Math.min(gmQuestList.length, gmQuestScroll + GM_QUEST_ROWS) + " of " + gmQuestList.length,
         70, 382, 180, 18, 10, GM_TEXT_DIM, false).wordWrap = false;
   }
   var title = gmInput(content, "qnew_txt", 0, 420, 172, 22, "", "New quest title", function(text)
   {
      gmSend("!questedit new " + gmEscapeText(text == "" ? "New quest" : text));
   });
   title.maxChars = 100;
   gmButton(content, "qnew_mc", "+ New", 176, 420, 74, 22, function()
   {
      var text = String(_root.gm_mc.content_mc.qnew_txt.text);
      gmSend("!questedit new " + gmEscapeText(text == "" ? "New quest" : text));
   }, 0x2E7D5B);

   if(gmQuest == undefined || String(gmQuest.id) != String(gmQuestSel))
   {
      gmText(content, "qhint_txt", "Pick a quest on the left, or create one.\r\r" +
         "A quest has a giver and a turn-in NPC, goals in order (talk to an NPC, collect from nodes, reach a spot) and rewards. " +
         "Publish it when it's ready; drafts stay hidden from players.\r\r" +
         "The quest window shows generic text; the title, description and goal text reach the player as chat.",
         266, 26, 600, 140, 12, GM_TEXT_DIM, false);
      gmStatus("Quest authoring");
      return;
   }
   gmRenderQuestEditor(content, 266);
}

// Field with a Set button (Enter works too) that sends "!questedit set <id> <what> <value>".
function gmQuestField(content, name, label, x, y, labelW, inputW, value, what, isText, maxChars)
{
   gmText(content, name + "_label", label, x, y + 4, labelW, 18, 11, GM_TEXT_DIM, true).wordWrap = false;
   var t = gmInput(content, name, x + labelW, y, inputW, 22, value, "", function(text)
   {
      gmQuestSet(this.what, this.isText, text);
   });
   t.what = what;
   t.isText = isText;
   t.maxChars = maxChars == undefined ? 12 : maxChars;
   var b = gmButton(content, name + "_set", "Set", x + labelW + inputW + 4, y, 40, 22, function()
   {
      gmQuestSet(this.field.what, this.field.isText, String(this.field.text));
   });
   b.field = t;
   return t;
}

function gmQuestSet(what, isText, text)
{
   gmSend("!questedit set " + gmQuest.id + " " + what + " " + (isText ? gmEscapeText(text) : (text == "" ? "0" : text)));
}

// "Use selected NPC" for the giver and turn-in fields.
function gmQuestUseSelected(content, name, x, y, what)
{
   var b = gmButton(content, name, "Selected NPC", x, y, 100, 22, function()
   {
      var id = gmSelectedNpcDefinition();
      if(id == "")
      {
         gmStatus("Select an NPC first (god mode, then click it).");
         return;
      }
      gmQuestSet(this.what, false, id);
   });
   b.what = what;
}

function gmQuestGoalText(g)
{
   if(g.type == "talk")
   {
      return "Talk to NPC " + g.a;
   }
   if(g.type == "collect")
   {
      return "Collect " + g.b + " from " + g.a;
   }
   if(g.type == "reach")
   {
      return "Reach " + Math.round(Number(g.b)) + ", " + Math.round(Number(g.d)) + " within " + Math.round(Number(g.e)) + " m";
   }
   return g.type;
}

function gmRenderQuestEditor(content, x)
{
   var q = gmQuest;
   var w = 630;
   gmText(content, "qtitle_txt", "Quest #" + q.id + (q.published ? "  - published, players can take it" : "  - draft, hidden from players"),
      x, 0, 380, 20, 13, q.published ? 0x7CFC9A : GM_TEXT_GOLD, true).wordWrap = false;
   gmButton(content, "qpub_mc", q.published ? "Unpublish" : "Publish", x + w - 260, 0, 96, 22, function()
   {
      gmSend("!questedit " + (gmQuest.published ? "unpublish " : "publish ") + gmQuest.id);
   }, q.published ? undefined : 0x2E7D5B);
   gmButton(content, "qshow_mc", "Reload", x + w - 160, 0, 74, 22, function()
   {
      gmSend("!questedit show " + gmQuest.id);
   });
   gmConfirmButton(content, "qdel_mc", "Delete", x + w - 82, 0, 82, 22, function()
   {
      gmSend("!questedit delete " + gmQuest.id);
   });

   gmQuestField(content, "qt", "Title", x, 28, 70, 512, q.title, "title", true, 100);

   gmText(content, "qd_label", "Description", x, 58, 70, 30, 11, GM_TEXT_DIM, true);
   gmTextArea(content, "qd_txt", x + 70, 54, 512, 52, q.desc, 400, 11);
   gmButton(content, "qd_set", "Set", x + 586, 54, 40, 52, function()
   {
      gmQuestSet("desc", true, String(_root.gm_mc.content_mc.qd_txt.text));
   });

   gmQuestField(content, "qg", "Giver", x, 114, 70, 70, q.giver == "0" ? "" : q.giver, "giver", false);
   gmQuestUseSelected(content, "qgsel_mc", x + 188, 114, "giver");
   gmQuestField(content, "qi", "Turn-in", x + 318, 114, 70, 70, q.turnin == "0" ? "" : q.turnin, "turnin", false);
   gmQuestUseSelected(content, "qisel_mc", x + 506, 114, "turnin");

   gmQuestField(content, "qc", "Coins", x, 142, 70, 70, q.coins == "0" ? "" : q.coins, "coins", false);
   gmQuestField(content, "qp", "After quest", x + 200, 142, 80, 70, q.prereq == "0" ? "" : q.prereq, "prereq", false);
   gmQuestField(content, "qn", "Then quest", x + 412, 142, 80, 70, q.next == "0" ? "" : q.next, "next", false);

   // Goals, in order.
   gmText(content, "goals_label", "Goals in order (" + q.goals.length + "/" + GM_QUEST_MAX_GOALS + ")", x, 174, 300, 18, 12, GM_TEXT_GOLD, true).wordWrap = false;
   gmText(content, "goals_hint", "goal text is what the player is told (Enter or Set)", x + 300, 176, 330, 18, 10, GM_TEXT_DIM, false, "right").wordWrap = false;
   var i;
   for(i = 0; i < q.goals.length && i < GM_QUEST_MAX_GOALS; i++)
   {
      var g = q.goals[i];
      var y = 194 + i * 22;
      gmText(content, "goal" + i + "_txt", (Number(g.index) + 1) + ". " + gmQuestGoalText(g), x, y + 3, 220, 18, 11, GM_TEXT, false).wordWrap = false;
      var goalText = gmInput(content, "gtext" + i + "_txt", x + 222, y, 270, 20, g.text, "goal text", function(text)
      {
         gmSend("!questedit goal text " + gmQuest.id + " " + this.index + " " + gmEscapeText(text));
      });
      goalText.index = g.index;
      goalText.maxChars = 200;
      var setButton = gmButton(content, "gset" + i + "_mc", "Set", x + 496, y, 40, 20, function()
      {
         gmSend("!questedit goal text " + gmQuest.id + " " + this.field.index + " " + gmEscapeText(String(this.field.text)));
      });
      setButton.field = goalText;
      var up = gmButton(content, "gup" + i + "_mc", "^", x + 540, y, 26, 20, function()
      {
         gmSend("!questedit goal up " + gmQuest.id + " " + this.index);
      });
      up.index = g.index;
      var down = gmButton(content, "gdn" + i + "_mc", "v", x + 570, y, 26, 20, function()
      {
         gmSend("!questedit goal down " + gmQuest.id + " " + this.index);
      });
      down.index = g.index;
      var del = gmButton(content, "gdel" + i + "_mc", "x", x + 600, y, 30, 20, function()
      {
         gmSend("!questedit goal del " + gmQuest.id + " " + this.index);
      }, 0xC0392B);
      del.index = g.index;
   }
   if(q.goals.length == 0)
   {
      gmText(content, "gempty_txt", "No goals yet. Add some below.", x, 196, 300, 18, 11, GM_TEXT_DIM, false);
   }

   // Add a goal: one value box, four ways.
   var gy = 194 + Math.max(1, q.goals.length) * 22 + 4;
   if(q.goals.length < GM_QUEST_MAX_GOALS)
   {
      gmInput(content, "gval_txt", x, gy, 130, 22, "", "NPC def id / node type");
      gmInput(content, "gcount_txt", x + 134, gy, 44, 22, "", "count");
      gmButton(content, "gtalk_mc", "+ Talk to", x + 182, gy, 80, 22, function()
      {
         gmSend("!questedit goal add " + gmQuest.id + " talk " + gmFieldText(_root.gm_mc.content_mc.gval_txt));
      });
      gmButton(content, "gtalksel_mc", "+ Talk to selected", x + 266, gy, 128, 22, function()
      {
         var id = gmSelectedNpcDefinition();
         if(id == "")
         {
            gmStatus("Select an NPC first (god mode, then click it).");
            return;
         }
         gmSend("!questedit goal add " + gmQuest.id + " talk " + id);
      });
      gmButton(content, "gcollect_mc", "+ Collect", x + 398, gy, 80, 22, function()
      {
         var c = _root.gm_mc.content_mc;
         var count = gmFieldText(c.gcount_txt);
         gmSend("!questedit goal add " + gmQuest.id + " collect " + gmFieldText(c.gval_txt) + " " + (count == "" ? "1" : count));
      });
      gmButton(content, "greach_mc", "+ Reach where I stand", x + 482, gy, 148, 22, function()
      {
         gmSend("!questedit goal add " + gmQuest.id + " reach");
      });
   }

   // Rewards: add on the right, click one to remove it.
   var ry = 398;
   gmText(content, "rewards_label", "Rewards (" + q.rewards.length + "/8)  click one to remove it", x, ry + 3, 380, 18, 12, GM_TEXT_GOLD, true).wordWrap = false;
   if(q.rewards.length < 8)
   {
      gmInput(content, "ritem_txt", x + w - 214, ry, 90, 22, "", "item id");
      gmInput(content, "rqty_txt", x + w - 120, ry, 44, 22, "", "qty");
      gmButton(content, "radd_mc", "+ Add", x + w - 72, ry, 72, 22, function()
      {
         var c = _root.gm_mc.content_mc;
         var qty = gmFieldText(c.rqty_txt);
         gmSend("!questedit reward add " + gmQuest.id + " " + gmFieldText(c.ritem_txt) + " " + (qty == "" ? "1" : qty));
      }, 0x2E7D5B);
   }
   for(i = 0; i < q.rewards.length; i++)
   {
      var label = "item " + q.rewards[i][0] + (Number(q.rewards[i][1]) > 1 ? " x" + q.rewards[i][1] : "");
      var chip = gmButton(content, "reward" + i + "_mc", label, x + i * 78, 426, 74, 20, function()
      {
         gmSend("!questedit reward del " + gmQuest.id + " " + this.itemId);
      });
      chip.itemId = q.rewards[i][0];
   }
   if(q.rewards.length == 0)
   {
      gmText(content, "rnone_txt", "No item rewards yet" + (q.coins == "0" ? "." : " (coins: " + q.coins + ")."), x, 428, 300, 18, 10, GM_TEXT_DIM, false).wordWrap = false;
   }

   gmStatus(gmQuestNodes.length > 0 ? "Node types for Collect: " + gmOneLine(gmQuestNodes.join(", "), 110) : "Quest editor");
}

function gmHandleQuestData(kind, parts)
{
   if(kind == "questlist")
   {
      if(parts[2] == "begin")
      {
         gmQuestListLoading = [];
      }
      else if(parts[2] == "entry")
      {
         gmQuestListLoading.push({ id: parts[3], title: gmUnescapeText(parts[4]), published: parts[5] == "1", goals: Number(parts[6]) });
      }
      else if(parts[2] == "end")
      {
         gmQuestList = gmQuestListLoading;
         if(_root.gm_mc._visible && gmTab == "quests")
         {
            gmShowTab("quests");
         }
      }
      return;
   }
   if(kind == "questedit")
   {
      if(parts[2] == "begin")
      {
         gmQuestLoading = { id: parts[3], title: gmUnescapeText(parts[4]), desc: gmUnescapeText(parts[5]), giver: parts[6], turnin: parts[7],
            coins: parts[8], prereq: parts[9], next: parts[10], published: parts[11] == "1", titleId: parts[12], descId: parts[13], goals: [], rewards: [] };
      }
      else if(parts[2] == "end" && gmQuestLoading != undefined)
      {
         gmQuest = gmQuestLoading;
         gmQuestSel = gmQuest.id;
         gmQuestLoading = undefined;
         if(_root.gm_mc._visible && gmTab == "quests")
         {
            gmShowTab("quests");
         }
         // Titles and published state show in the list too.
         gmSend("!questedit list");
      }
      return;
   }
   if(kind == "questgoal" && gmQuestLoading != undefined)
   {
      gmQuestLoading.goals.push({ index: parts[2], type: parts[3], a: parts[4], b: parts[5], c: parts[6], d: parts[7], e: parts[8], text: gmUnescapeText(parts[9]) });
      return;
   }
   if(kind == "questreward" && gmQuestLoading != undefined)
   {
      gmQuestLoading.rewards.push([parts[2], parts[3]]);
      return;
   }
   if(kind == "questnodes")
   {
      gmQuestNodes = parts.slice(2);
   }
}

// --- item generation (Admin) ----------------------------------------------------
// Custom items are copies of a regular item with fields overridden (!itemgen). The server sends the list as
// itemgen|begin, itemgen|entry|customId|itemId|label|baseItemId|iconImageSetId, itemgen|end, and the editor as
// itemedit|begin|customId|itemId|label|baseItemId, one itemfield|field|value|overridden per field, itemedit|end.

var gmGenList = [];
var gmGenListLoading = [];
var gmGenListAsked = false;
var gmGen = undefined;
var gmGenLoading = undefined;
var gmGenSel = 0;
var gmGenScroll = 0;
var GM_GEN_ROWS = 12;
// Fields that are on/off toggles rather than typed values.
var GM_GEN_FLAGS = { members: true, notrade: true, nosale: true };
// Field labels, in the server's field order.
var GM_GEN_LABELS = { name: "Name id", desc: "Desc id", model: "Model", texture: "Texture", tint: "Tint",
   icon: "Icon set", icontint: "Icon tint", rarity: "Rarity", cost: "Cost", stack: "Stack", effect: "Effect",
   members: "Members", notrade: "No trade", nosale: "No sale", label: "Label" };

function gmRenderItemGen(content)
{
   if(!gmGenListAsked)
   {
      gmGenListAsked = true;
      gmSend("!itemgen list");
   }

   // Left: search, the list and "new from base item".
   gmText(content, "gen_label", "Custom items", 0, 0, 100, 18, 12, GM_TEXT_GOLD, true);
   gmButton(content, "genreload_mc", "Refresh", 120, 0, 54, 20, function()
   {
      var search = gmFieldText(_root.gm_mc.content_mc.gensearch_txt);
      gmSend("!itemgen list" + (search == "" ? "" : " " + search));
   });
   gmInput(content, "gensearch_txt", 0, 22, 174, 20, "", "Search label or id", function(text)
   {
      gmSend("!itemgen list" + (text == "" ? "" : " " + text));
   });
   gmGenScroll = Math.max(0, Math.min(gmGenScroll, gmGenList.length - GM_GEN_ROWS));
   var i;
   for(i = 0; i < GM_GEN_ROWS && gmGenScroll + i < gmGenList.length; i++)
   {
      var g = gmGenList[gmGenScroll + i];
      var selected = String(g.id) == String(gmGenSel);
      var row = content.createEmptyMovieClip("gen" + i + "_mc", content.getNextHighestDepth());
      row._y = 46 + i * 22;
      gmPanelShape(row, 174, 20, 6, selected ? GM_BLUE_ACTIVE : GM_BLUE_CELL, GM_BLUE_BOTTOM, selected ? GM_GOLD : GM_GOLD_DARK, 1);
      gmText(row, "label_txt", g.itemId + "  " + gmOneLine(g.label == "" ? "(base " + g.base + ")" : g.label, 20), 6, 2, 164, 18, 10, GM_TEXT, selected).wordWrap = false;
      row.genId = g.id;
      row.onRelease = function()
      {
         gmGenSel = this.genId;
         gmSend("!itemgen show " + this.genId);
      };
   }
   if(gmGenList.length > GM_GEN_ROWS)
   {
      gmButton(content, "gendown_mc", "more...", 0, 46 + GM_GEN_ROWS * 22, 174, 18, function()
      {
         gmGenScroll = gmGenScroll + GM_GEN_ROWS >= gmGenList.length ? 0 : gmGenScroll + GM_GEN_ROWS;
         gmShowTab("itemgen");
      });
   }
   gmInput(content, "genbase_txt", 0, 344, 56, 20, "", "base id");
   gmInput(content, "genlabel_txt", 60, 344, 62, 20, "", "label");
   gmButton(content, "gennew_mc", "+ New", 126, 344, 48, 20, function()
   {
      var c = _root.gm_mc.content_mc;
      var base = gmFieldText(c.genbase_txt);
      if(base == "")
      {
         gmStatus("Enter the id of the item to copy.");
         return;
      }
      var label = String(c.genlabel_txt.text);
      gmSend("!itemgen new " + base + (label == "" ? "" : " " + gmEscapeText(label)));
   }, 0x2E7D5B);

   if(gmGen == undefined || String(gmGen.id) != String(gmGenSel))
   {
      gmText(content, "genhint_txt", "Pick a custom item on the left, or copy an existing item: enter its id (and a label for yourself) and press + New.\r\rCustom items get item id 9000000 + their number and go live for everyone online. Names and descriptions are client string ids.", 184, 26, 420, 120, 11, GM_TEXT_DIM, false);
      gmStatus("Item generation");
      return;
   }
   gmRenderItemGenEditor(content, 184);
}

function gmGenSet(field, text)
{
   gmSend("!itemgen set " + gmGen.id + " " + field + " " + (text == "" ? "clear" : gmEscapeText(text)));
}

function gmRenderItemGenEditor(content, x)
{
   var g = gmGen;
   gmIcon(content, "genicon_mc", g.iconSet, x, 0, 44);
   gmText(content, "gentitle_txt", "Item " + g.itemId + "  (copy of " + g.base + ")", x + 50, 0, 250, 18, 12, GM_TEXT_GOLD, true).wordWrap = false;
   gmText(content, "gensub_txt", "Gold labels are overridden; Clear reverts to the base item.", x + 50, 20, 250, 18, 10, GM_TEXT_DIM, false).wordWrap = false;
   gmInput(content, "genqty_txt", x + 306, 0, 30, 20, "1", "");
   gmButton(content, "gengive_mc", "Give me", x + 340, 0, 58, 20, function()
   {
      var qty = gmFieldText(_root.gm_mc.content_mc.genqty_txt);
      gmSend("!itemgen give " + gmGen.id + " " + (qty == "" ? "1" : qty));
   }, 0x2E7D5B);
   gmButton(content, "gendel_mc", "Delete", x + 340, 24, 58, 20, function()
   {
      gmSend("!itemgen delete " + gmGen.id);
   }, 0xC0392B);

   // Two columns of fields.
   var colW = 214;
   var i;
   for(i = 0; i < g.fields.length; i++)
   {
      var f = g.fields[i];
      var col = i < 8 ? 0 : 1;
      var fx = x + col * colW;
      var fy = 52 + (col == 0 ? i : i - 8) * 24;
      gmText(content, "gf" + i + "_label", GM_GEN_LABELS[f.name] == undefined ? f.name : GM_GEN_LABELS[f.name], fx, fy + 3, 62, 18, 10, f.over ? GM_TEXT_GOLD : GM_TEXT_DIM, true).wordWrap = false;
      if(GM_GEN_FLAGS[f.name])
      {
         var isOn = f.value == "1" || f.value == "on" || f.value == "True" || f.value == "true";
         var flag = gmButton(content, "gf" + i + "_mc", isOn ? "On" : "Off", fx + 62, fy, 50, 20, function()
         {
            gmGenSet(this.field, this.isOn ? "off" : "on");
         }, isOn ? 0x2E7D5B : undefined);
         flag.field = f.name;
         flag.isOn = isOn;
      }
      else
      {
         var t = gmInput(content, "gf" + i + "_txt", fx + 62, fy, 100, 20, f.value, "", function(text)
         {
            gmGenSet(this.field, text);
         });
         t.field = f.name;
         var setBtn = gmButton(content, "gf" + i + "_set", "Set", fx + 164, fy, 28, 20, function()
         {
            gmGenSet(this.input.field, String(this.input.text));
         });
         setBtn.input = t;
      }
      if(f.over)
      {
         var clear = gmButton(content, "gf" + i + "_clr", "x", fx + (GM_GEN_FLAGS[f.name] ? 114 : 194), fy, 18, 20, function()
         {
            gmSend("!itemgen set " + gmGen.id + " " + this.field + " clear");
         }, 0xC0392B);
         clear.field = f.name;
      }
   }
   gmStatus("Editing custom item " + g.id + (g.label == "" ? "" : ": " + gmOneLine(g.label, 40)));
}

function gmHandleItemGenData(kind, parts)
{
   if(kind == "itemgen")
   {
      if(parts[2] == "begin")
      {
         gmGenListLoading = [];
      }
      else if(parts[2] == "entry")
      {
         gmGenListLoading.push({ id: parts[3], itemId: parts[4], label: gmUnescapeText(parts[5]), base: parts[6], iconSet: parts[7] });
      }
      else if(parts[2] == "end")
      {
         gmGenList = gmGenListLoading;
         if(_root.gm_mc._visible && gmTab == "itemgen")
         {
            gmShowTab("itemgen");
         }
      }
      return;
   }
   if(kind == "itemedit")
   {
      if(parts[2] == "begin")
      {
         gmGenLoading = { id: parts[3], itemId: parts[4], label: gmUnescapeText(parts[5]), base: parts[6], iconSet: 0, fields: [] };
      }
      else if(parts[2] == "end" && gmGenLoading != undefined)
      {
         gmGen = gmGenLoading;
         gmGenSel = gmGen.id;
         gmGenLoading = undefined;
         if(_root.gm_mc._visible && gmTab == "itemgen")
         {
            gmShowTab("itemgen");
         }
         // Labels and icons show in the list too.
         gmSend("!itemgen list");
      }
      return;
   }
   if(kind == "itemfield" && gmGenLoading != undefined)
   {
      var value = gmUnescapeText(parts[3]);
      gmGenLoading.fields.push({ name: parts[2], value: value, over: parts[4] == "1" });
      if(parts[2] == "icon")
      {
         gmGenLoading.iconSet = value;
      }
   }
}

// --- redeem codes (Admin) -----------------------------------------------------------
// !code: codes|begin, codes|entry|code|enabled|uses|maxUses|expires|note, codes|end, and the editor as
// codeedit|begin|code|coins|maxUses|uses|perCharacter|starts|expires|members|enabled|note, codeitem|itemId|qty,
// codeedit|end.

var gmCodeList = [];
var gmCodeListLoading = [];
var gmCodeListAsked = false;
var gmCode = undefined;
var gmCodeLoading = undefined;
var gmCodeSel = "";
var gmCodeScroll = 0;
var GM_CODE_ROWS = 12;

function gmRenderCodes(content)
{
   if(!gmCodeListAsked)
   {
      gmCodeListAsked = true;
      gmSend("!code list");
   }

   gmText(content, "codes_label", "Redeem codes", 0, 0, 100, 18, 12, GM_TEXT_GOLD, true);
   gmButton(content, "codereload_mc", "Refresh", 120, 0, 54, 20, function()
   {
      gmSend("!code list");
   });
   gmCodeScroll = Math.max(0, Math.min(gmCodeScroll, gmCodeList.length - GM_CODE_ROWS));
   var i;
   for(i = 0; i < GM_CODE_ROWS && gmCodeScroll + i < gmCodeList.length; i++)
   {
      var c = gmCodeList[gmCodeScroll + i];
      var selected = c.code == gmCodeSel;
      var row = content.createEmptyMovieClip("code" + i + "_mc", content.getNextHighestDepth());
      row._y = 24 + i * 22;
      gmPanelShape(row, 174, 20, 6, selected ? GM_BLUE_ACTIVE : GM_BLUE_CELL, GM_BLUE_BOTTOM, selected ? GM_GOLD : GM_GOLD_DARK, 1);
      var uses = c.uses + "/" + (c.maxUses == "0" ? "inf" : c.maxUses);
      gmText(row, "label_txt", (c.enabled ? "" : "(off) ") + gmOneLine(c.code, 16) + "  " + uses, 6, 2, 164, 18, 10, c.enabled ? GM_TEXT : GM_TEXT_DIM, selected).wordWrap = false;
      row.code = c.code;
      row.onRelease = function()
      {
         gmCodeSel = this.code;
         gmSend("!code show " + this.code);
      };
   }
   if(gmCodeList.length > GM_CODE_ROWS)
   {
      gmButton(content, "codedown_mc", "more...", 0, 24 + GM_CODE_ROWS * 22, 174, 18, function()
      {
         gmCodeScroll = gmCodeScroll + GM_CODE_ROWS >= gmCodeList.length ? 0 : gmCodeScroll + GM_CODE_ROWS;
         gmShowTab("codes");
      });
   }
   gmInput(content, "codenew_txt", 0, 344, 92, 20, "", "CODE (or blank)");
   gmButton(content, "codenew_mc", "+ New", 96, 344, 40, 20, function()
   {
      var code = gmFieldText(_root.gm_mc.content_mc.codenew_txt);
      gmSend("!code new" + (code == "" ? "" : " " + code));
   }, 0x2E7D5B);
   gmButton(content, "coderandom_mc", "Rnd", 140, 344, 34, 20, function()
   {
      gmSend("!code new random");
   }, 0x2E7D5B);

   if(gmCode == undefined || gmCode.code != gmCodeSel)
   {
      gmText(content, "codehint_txt", "Pick a code on the left, or create one (blank or Rnd makes a random FR-XXXX-XXXX code).\r\rPlayers redeem codes with !redeem <code>, or the Redeem Code button on the dock.", 184, 26, 420, 120, 11, GM_TEXT_DIM, false);
      gmStatus("Redeem codes");
      return;
   }
   gmRenderCodeEditor(content, 184);
}

function gmCodeSetValue(field, text)
{
   gmSend("!code set " + gmCode.code + " " + field + " " + (field == "note" ? gmEscapeText(text) : (text == "" ? "0" : text)));
}

// Field with a Set button that sends "!code set <code> <field> <value>".
function gmCodeField(content, name, label, x, y, labelW, inputW, value, field, hint)
{
   gmText(content, name + "_label", label, x, y + 3, labelW, 18, 10, GM_TEXT_DIM, true).wordWrap = false;
   var t = gmInput(content, name, x + labelW, y, inputW, 20, value, hint, function(text)
   {
      gmCodeSetValue(this.field, text);
   });
   t.field = field;
   var b = gmButton(content, name + "_set", "Set", x + labelW + inputW + 2, y, 28, 20, function()
   {
      gmCodeSetValue(this.input.field, String(this.input.text));
   });
   b.input = t;
}

function gmCodeToggle(content, name, label, x, y, isOn, field)
{
   var b = gmButton(content, name, label + (isOn ? ": on" : ": off"), x, y, 104, 20, function()
   {
      gmSend("!code set " + gmCode.code + " " + this.field + " " + (this.isOn ? "off" : "on"));
   }, isOn ? 0x2E7D5B : undefined);
   b.field = field;
   b.isOn = isOn;
}

function gmRenderCodeEditor(content, x)
{
   var c = gmCode;
   gmText(content, "codetitle_txt", c.code, x, 0, 220, 20, 14, GM_TEXT_GOLD, true).wordWrap = false;
   gmText(content, "codeuses_txt", "Redeemed " + c.uses + (c.maxUses == "0" ? " times (no limit)" : " of " + c.maxUses), x, 20, 220, 18, 10, GM_TEXT_DIM, false).wordWrap = false;
   gmButton(content, "codedel_mc", "Delete", x + 370, 0, 58, 20, function()
   {
      gmSend("!code delete " + gmCode.code);
   }, 0xC0392B);

   gmCodeField(content, "cc", "Coins", x, 44, 74, 70, c.coins, "coins", "0");
   gmCodeField(content, "cu", "Max uses", x, 68, 74, 70, c.maxUses, "uses", "0 = no limit");
   gmCodeField(content, "cp", "Per player", x, 92, 74, 70, c.perCharacter, "percharacter", "0 = no limit");
   gmCodeField(content, "cs", "Starts", x + 218, 44, 50, 92, c.starts, "starts", "never");
   gmCodeField(content, "ce", "Expires", x + 218, 68, 50, 92, c.expires, "expires", "never");
   gmText(content, "cdates_txt", "yyyy-MM-dd (UTC), +days or never", x + 218, 94, 210, 18, 10, GM_TEXT_DIM, false).wordWrap = false;
   gmCodeToggle(content, "cen_mc", "Enabled", x, 118, c.enabled, "enabled");
   gmCodeToggle(content, "cmem_mc", "Members", x + 108, 118, c.members, "members");
   gmCodeField(content, "cn", "Note", x, 144, 74, 324, c.note, "note", "");

   // Items, up to 8.
   gmText(content, "citems_label", "Items (" + c.items.length + "/8)", x, 172, 120, 18, 10, GM_TEXT_DIM, true);
   var i;
   for(i = 0; i < c.items.length; i++)
   {
      var it = c.items[i];
      var iy = 190 + (i % 4) * 22;
      var ix = x + Math.floor(i / 4) * 214;
      gmText(content, "ci" + i + "_txt", "Item " + it.itemId + "  x" + it.qty, ix, iy + 2, 160, 18, 10, GM_TEXT, false).wordWrap = false;
      var del = gmButton(content, "ci" + i + "_del", "x", ix + 164, iy, 18, 18, function()
      {
         gmSend("!code item del " + gmCode.code + " " + this.itemId);
      }, 0xC0392B);
      del.itemId = it.itemId;
   }
   gmInput(content, "ciitem_txt", x, 284, 80, 20, "", "item id");
   gmInput(content, "ciqty_txt", x + 84, 284, 36, 20, "", "qty");
   gmButton(content, "ciadd_mc", "+ Add item", x + 124, 284, 76, 20, function()
   {
      var cm = _root.gm_mc.content_mc;
      var item = gmFieldText(cm.ciitem_txt);
      var qty = gmFieldText(cm.ciqty_txt);
      if(item == "")
      {
         gmStatus("Enter an item id (custom items are 9000000 and up).");
         return;
      }
      gmSend("!code item add " + gmCode.code + " " + item + " " + (qty == "" ? "1" : qty));
   }, 0x2E7D5B);
   gmStatus("Editing code " + c.code);
}

function gmHandleCodeData(kind, parts)
{
   if(kind == "codes")
   {
      if(parts[2] == "begin")
      {
         gmCodeListLoading = [];
      }
      else if(parts[2] == "entry")
      {
         gmCodeListLoading.push({ code: parts[3], enabled: parts[4] == "1", uses: parts[5], maxUses: parts[6], expires: parts[7], note: gmUnescapeText(parts[8]) });
      }
      else if(parts[2] == "end")
      {
         gmCodeList = gmCodeListLoading;
         // A deleted code drops out of the editor.
         var found = false;
         var i;
         for(i = 0; i < gmCodeList.length; i++)
         {
            found = found || gmCodeList[i].code == gmCodeSel;
         }
         if(!found)
         {
            gmCodeSel = "";
         }
         if(_root.gm_mc._visible && gmTab == "codes")
         {
            gmShowTab("codes");
         }
      }
      return;
   }
   if(kind == "codeedit")
   {
      if(parts[2] == "begin")
      {
         gmCodeLoading = { code: parts[3], coins: parts[4], maxUses: parts[5], uses: parts[6], perCharacter: parts[7], starts: parts[8], expires: parts[9],
            members: parts[10] == "1", enabled: parts[11] == "1", note: gmUnescapeText(parts[12]), items: [] };
      }
      else if(parts[2] == "end" && gmCodeLoading != undefined)
      {
         gmCode = gmCodeLoading;
         gmCodeSel = gmCode.code;
         gmCodeLoading = undefined;
         if(_root.gm_mc._visible && gmTab == "codes")
         {
            gmShowTab("codes");
         }
         gmSend("!code list");
      }
      return;
   }
   if(kind == "codeitem" && gmCodeLoading != undefined)
   {
      gmCodeLoading.items.push({ itemId: parts[2], qty: parts[3] });
   }
}

// --- redeem box (every player) ------------------------------------------------------
// "!redeem" with no code answers redeem|open; this small box then sends "!redeem <code>". The result comes
// back as a system chat line.

function gmShowRedeem()
{
   _root.gmredeem_mc.removeMovieClip();
   var box = _root.createEmptyMovieClip("gmredeem_mc", 16002);
   box._x = 8;
   box._y = GM_CHAT_STAGE_H - 130;
   gmPanelShape(box, 300, 96, 12, GM_BLUE_TOP, GM_BLUE_BOTTOM, GM_GOLD, 3);
   gmText(box, "title_txt", "Redeem a code", 12, 8, 220, 20, 13, GM_TEXT_GOLD, true);
   gmButton(box, "close_mc", "X", 270, 8, 22, 20, function()
   {
      _root.gmredeem_mc.removeMovieClip();
   }, 0xC0392B);
   var t = gmInput(box, "code_txt", 12, 36, 196, 24, "", "Enter your code", function(text)
   {
      gmRedeemSubmit(text);
   });
   t.maxChars = 32;
   gmButton(box, "go_mc", "Redeem", 214, 36, 74, 24, function()
   {
      gmRedeemSubmit(gmFieldText(_root.gmredeem_mc.code_txt));
   }, 0x2E7D5B);
   gmText(box, "hint_txt", "Codes from events and the referees. The result shows in chat.", 12, 66, 280, 20, 10, GM_TEXT_DIM, false).wordWrap = false;
   gmFocusInput(t);
}

function gmRedeemSubmit(text)
{
   var code = frHelperClass.stringReplace(String(text), " ", "");
   if(code == "")
   {
      return;
   }
   gmSend("!redeem " + code);
   _root.gmredeem_mc.removeMovieClip();
}

// --- tile editor (Mod+) ---------------------------------------------------------------
// Edits a selection of placed props and saved world NPCs (!tile). After every !tile command the server
// sends tilesel|begin|count|cx|cy|cz, tilesel|entry|guid|prop/npc|definitionId|label|x|y|z|heading per item,
// tilesel|end and tileundo|depth|label; kit lists come as tilekits|begin, tilekits|entry|name|count|note,
// tilekits|end.

var gmTileSel = { count: 0, cx: 0, cy: 0, cz: 0, items: [] };
var gmTileSelLoading = undefined;
var gmTileUndo = { depth: 0, label: "" };
var gmTileKits = [];
var gmTileKitsLoading = [];
var gmTileAsked = false;
var gmTileScroll = 0;
var gmTileKitScroll = 0;
var GM_TILE_ROWS = 11;
var GM_TILE_KIT_ROWS = 5;

function gmTileRound(n)
{
   return Math.round(Number(n) * 10) / 10;
}

function gmRenderTiles(content)
{
   if(!gmTileAsked)
   {
      gmTileAsked = true;
      gmSend("!tile list");
      gmSend("!tile kit list");
   }

   // Select row.
   gmText(content, "tile_label", "Tile editor", 0, 2, 80, 18, 12, GM_TEXT_GOLD, true);
   gmInput(content, "tileradius_txt", 84, 0, 40, 22, "10", "r", function(text)
   {
      gmSend("!tile select radius " + (text == "" ? "10" : text));
   });
   gmButton(content, "tilesel_mc", "Select radius", 128, 0, 92, 22, function()
   {
      var r = gmFieldText(_root.gm_mc.content_mc.tileradius_txt);
      gmSend("!tile select radius " + (r == "" ? "10" : r));
   });
   gmButton(content, "tileadd_mc", "+ Clicked", 224, 0, 74, 22, function()
   {
      gmSend("!tile select add");
   }, 0x2E7D5B);
   gmButton(content, "tilerem_mc", "- Clicked", 302, 0, 74, 22, function()
   {
      gmSend("!tile select remove");
   });
   gmButton(content, "tileclear_mc", "Clear", 380, 0, 52, 22, function()
   {
      gmSend("!tile select clear");
   });
   gmButton(content, "tilelist_mc", "Refresh", 436, 0, 60, 22, function()
   {
      gmSend("!tile list");
      gmSend("!tile kit list");
   });

   gmRenderTileSelection(content, 0, 30);
   gmRenderTileTools(content, 318, 30);
   gmStatus(gmTileSel.count > 0 ? gmTileSel.count + " selected. Moves, turns and copies are saved as you go; Undo keeps 20 steps." : "Select props and NPCs by radius, or click one in god mode and press + Clicked.");
}

// Left: the selection, nearest first, each with a remove button.
function gmRenderTileSelection(content, x, y)
{
   var s = gmTileSel;
   gmText(content, "tilecount_txt", s.count + " selected" + (s.count > 0 ? "   centre " + gmTileRound(s.cx) + ", " + gmTileRound(s.cy) + ", " + gmTileRound(s.cz) : ""), x, y, 310, 18, 10, GM_TEXT_DIM, true).wordWrap = false;
   gmTileScroll = Math.max(0, Math.min(gmTileScroll, s.items.length - GM_TILE_ROWS));
   var i;
   for(i = 0; i < GM_TILE_ROWS && gmTileScroll + i < s.items.length; i++)
   {
      var it = s.items[gmTileScroll + i];
      var row = content.createEmptyMovieClip("tile" + i + "_mc", content.getNextHighestDepth());
      row._x = x;
      row._y = y + 20 + i * 24;
      gmPanelShape(row, 288, 22, 6, it.kind == "npc" ? 0x5B3A8C : GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
      var name = it.label == "" ? it.kind + " " + it.def : it.label;
      gmText(row, "label_txt", gmOneLine(name, 22), 6, 3, 150, 18, 10, GM_TEXT, true).wordWrap = false;
      gmText(row, "pos_txt", gmTileRound(it.x) + ", " + gmTileRound(it.y) + ", " + gmTileRound(it.z) + "  " + Math.round(Number(it.heading)) + " deg", 152, 4, 132, 18, 9, GM_TEXT_DIM, false, "right").wordWrap = false;
      var rem = gmButton(content, "tilerm" + i + "_mc", "x", x + 292, y + 20 + i * 24, 18, 22, function()
      {
         gmSend("!tile select remove " + this.guid);
      }, 0xC0392B);
      rem.guid = it.guid;
   }
   if(s.items.length > GM_TILE_ROWS)
   {
      gmButton(content, "tilemore_mc", "more... (" + (gmTileScroll + 1) + "-" + Math.min(s.items.length, gmTileScroll + GM_TILE_ROWS) + " of " + s.items.length + ")", x, y + 20 + GM_TILE_ROWS * 24, 310, 18, function()
      {
         gmTileScroll = gmTileScroll + GM_TILE_ROWS >= gmTileSel.items.length ? 0 : gmTileScroll + GM_TILE_ROWS;
         gmShowTab("tiles");
      });
   }
}

// Right: nudge, turn, actions, undo and kits.
function gmRenderTileTools(content, x, y)
{
   gmText(content, "tilemove_label", "Move", x, y, 60, 18, 10, GM_TEXT_DIM, true);
   var axes = ["X", "Y", "Z"];
   var steps = [-5, -1, 1, 5];
   var a;
   var k;
   for(a = 0; a < 3; a++)
   {
      gmText(content, "tileaxis" + a + "_txt", axes[a], x, y + 22 + a * 24, 16, 18, 11, GM_TEXT_GOLD, true);
      for(k = 0; k < steps.length; k++)
      {
         var b = gmButton(content, "tilemv" + a + "_" + k + "_mc", (steps[k] > 0 ? "+" : "") + steps[k], x + 18 + k * 46, y + 20 + a * 24, 42, 22, function()
         {
            var d = [0, 0, 0];
            d[this.axis] = this.step;
            gmSend("!tile move " + d[0] + " " + d[1] + " " + d[2]);
         });
         b.axis = a;
         b.step = steps[k];
      }
   }

   var ry = y + 96;
   gmText(content, "tilerot_label", "Turn", x, ry + 3, 40, 18, 10, GM_TEXT_DIM, true);
   var turns = [-90, -15, 15, 90];
   for(k = 0; k < turns.length; k++)
   {
      var t = gmButton(content, "tilerot" + k + "_mc", (turns[k] > 0 ? "+" : "") + turns[k], x + 40 + k * 56, ry, 52, 22, function()
      {
         gmSend("!tile rotate " + this.degrees);
      });
      t.degrees = turns[k];
   }

   var ay = ry + 28;
   gmButton(content, "tilehere_mc", "Move here", x, ay, 72, 22, function()
   {
      gmSend("!tile here");
   });
   gmButton(content, "tilecopy_mc", "Copy", x + 76, ay, 52, 22, function()
   {
      gmSend("!tile copy");
   }, 0x2E7D5B);
   gmButton(content, "tiledel_mc", "Delete", x + 132, ay, 60, 22, function()
   {
      gmSend("!tile delete");
   }, 0xC0392B);
   var undo = gmButton(content, "tileundo_mc", gmTileUndo.depth > 0 ? "Undo " + gmOneLine(gmTileUndo.label, 14) : "Undo", x + 196, ay, 104, 22, function()
   {
      gmSend("!tile undo");
   }, gmTileUndo.depth > 0 ? 0x9C6B12 : undefined);

   // Kits.
   var ky = ay + 32;
   gmText(content, "tilekits_label", "Kits", x, ky, 60, 18, 10, GM_TEXT_DIM, true);
   gmTileKitScroll = Math.max(0, Math.min(gmTileKitScroll, gmTileKits.length - GM_TILE_KIT_ROWS));
   var i;
   for(i = 0; i < GM_TILE_KIT_ROWS && gmTileKitScroll + i < gmTileKits.length; i++)
   {
      var kit = gmTileKits[gmTileKitScroll + i];
      var row = content.createEmptyMovieClip("tilekit" + i + "_mc", content.getNextHighestDepth());
      row._x = x;
      row._y = ky + 18 + i * 24;
      gmPanelShape(row, 194, 22, 6, GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
      gmText(row, "label_txt", gmOneLine(kit.name, 16) + "  (" + kit.count + ")" + (kit.note == "" ? "" : "  " + gmOneLine(kit.note, 12)), 6, 3, 184, 18, 10, GM_TEXT, false).wordWrap = false;
      var place = gmButton(content, "tilekitp" + i + "_mc", "Place", x + 198, ky + 18 + i * 24, 50, 22, function()
      {
         gmSend("!tile kit place " + this.kit);
      }, 0x2E7D5B);
      place.kit = kit.name;
      var kdel = gmButton(content, "tilekitd" + i + "_mc", "x", x + 252, ky + 18 + i * 24, 20, 22, function()
      {
         gmSend("!tile kit delete " + this.kit);
      }, 0xC0392B);
      kdel.kit = kit.name;
   }
   if(gmTileKits.length > GM_TILE_KIT_ROWS)
   {
      gmButton(content, "tilekitmore_mc", "more...", x + 276, ky + 18, 24, 22, function()
      {
         gmTileKitScroll = gmTileKitScroll + GM_TILE_KIT_ROWS >= gmTileKits.length ? 0 : gmTileKitScroll + GM_TILE_KIT_ROWS;
         gmShowTab("tiles");
      });
   }
   if(gmTileKits.length == 0)
   {
      gmText(content, "tilenokits_txt", "No kits yet. Save the selection as one below; kits place at your position and facing.", x, ky + 18, 300, 36, 10, GM_TEXT_DIM, false);
   }

   var sy = ky + 18 + GM_TILE_KIT_ROWS * 24 + 4;
   gmInput(content, "tilekitname_txt", x, sy, 96, 22, "", "kit name");
   gmInput(content, "tilekitnote_txt", x + 100, sy, 140, 22, "", "note");
   gmButton(content, "tilekitsave_mc", "Save kit", x + 244, sy, 56, 22, function()
   {
      var c = _root.gm_mc.content_mc;
      var name = gmFieldText(c.tilekitname_txt);
      if(name == "")
      {
         gmStatus("Name the kit first (letters, digits, _ and -).");
         return;
      }
      var note = String(c.tilekitnote_txt.text);
      gmSend("!tile kit save " + name + (note == "" ? "" : " " + gmEscapeText(note)));
   }, 0x2E7D5B);
}

function gmHandleTileData(kind, parts)
{
   if(kind == "tilesel")
   {
      if(parts[2] == "begin")
      {
         gmTileSelLoading = { count: Number(parts[3]), cx: parts[4], cy: parts[5], cz: parts[6], items: [] };
      }
      else if(parts[2] == "entry" && gmTileSelLoading != undefined)
      {
         gmTileSelLoading.items.push({ guid: parts[3], kind: parts[4], def: parts[5], label: gmUnescapeText(parts[6]),
            x: parts[7], y: parts[8], z: parts[9], heading: parts[10] });
      }
      else if(parts[2] == "end" && gmTileSelLoading != undefined)
      {
         gmTileSel = gmTileSelLoading;
         gmTileSelLoading = undefined;
      }
      return;
   }
   if(kind == "tileundo")
   {
      // The last line after every !tile command, so the tab redraws once per command.
      gmTileUndo = { depth: Number(parts[2]), label: gmUnescapeText(parts[3]) };
      if(_root.gm_mc._visible && gmTab == "tiles")
      {
         gmShowTab("tiles");
      }
      return;
   }
   if(kind == "tilekits")
   {
      if(parts[2] == "begin")
      {
         gmTileKitsLoading = [];
      }
      else if(parts[2] == "entry")
      {
         gmTileKitsLoading.push({ name: parts[3], count: parts[4], note: gmUnescapeText(parts[5]) });
      }
      else if(parts[2] == "end")
      {
         gmTileKits = gmTileKitsLoading;
         if(_root.gm_mc._visible && gmTab == "tiles")
         {
            gmShowTab("tiles");
         }
      }
   }
}

// --- decorate bar (every player) -----------------------------------------------
// While a player decorates their house, the server sends decorate|on and, for the piece last placed,
// moved or turned with the client's own wheel, decorate|sel|name|yaw|pitch|roll. The client's wheel only
// turns in fixed yaw steps; these buttons add yaw, pitch and roll in any step.

var gmDecorate = { on: false, name: "", yaw: 0, pitch: 0, roll: 0, x: undefined, y: undefined };
var GM_DECORATE_W = 300;

function gmHandleDecorate(parts)
{
   if(parts[2] == "on" || parts[2] == "off")
   {
      gmDecorate.on = parts[2] == "on";
   }
   else if(parts[2] == "sel")
   {
      gmDecorate.name = parts[3];
      gmDecorate.yaw = parts[4];
      gmDecorate.pitch = parts[5];
      gmDecorate.roll = parts[6];
   }
   gmShowDecorate();
}

function gmShowDecorate()
{
   var old = _root.gmdecorate_mc;
   if(old != undefined)
   {
      gmDecorate.x = old._x;
      gmDecorate.y = old._y;
      old.removeMovieClip();
   }
   if(!gmDecorate.on)
   {
      return;
   }
   var d = _root.createEmptyMovieClip("gmdecorate_mc", 16002);
   var w = GM_DECORATE_W;
   var h = 176;
   var frame = d.createEmptyMovieClip("frame_mc", 1);
   gmPanelShape(frame, w, h, 12, GM_BLUE_TOP, GM_BLUE_BOTTOM, GM_GOLD, 3);
   frame.onPress = function()
   {
      this._parent.startDrag(false, 0, gmTop(), Math.max(0, gmRight() - w), Math.max(gmTop(), gmBottom() - h));
   };
   frame.onRelease = frame.onReleaseOutside = function()
   {
      this._parent.stopDrag();
   };

   var title = gmDecorate.name == "" ? "Place, move or turn a piece to rotate it" : "Rotate: " + gmDecorate.name;
   gmText(d, "title_txt", title, 12, 8, w - 24, 18, 12, GM_TEXT_GOLD, true).wordWrap = false;
   if(gmDecorate.name != "")
   {
      gmText(d, "angles_txt", "Yaw " + gmDecorate.yaw + "   Pitch " + gmDecorate.pitch + "   Roll " + gmDecorate.roll, 12, 26, w - 24, 18, 11, GM_TEXT_DIM, false);
   }

   var axes = [["Yaw", "yaw"], ["Pitch", "pitch"], ["Roll", "roll"]];
   var steps = ["-45", "-15", "+15", "+45"];
   var i;
   var j;
   for(i = 0; i < axes.length; i++)
   {
      var y = 48 + i * 28;
      gmText(d, "axis" + i + "_txt", axes[i][0], 12, y + 4, 50, 18, 11, GM_TEXT, true);
      for(j = 0; j < steps.length; j++)
      {
         var b = gmButton(d, "step" + i + "_" + j + "_mc", steps[j], 62 + j * 58, y, 54, 24, function()
         {
            gmSend("!house rotate " + this.axis + " " + this.degrees);
         });
         b.axis = axes[i][1];
         b.degrees = Number(steps[j]);
      }
   }
   gmButton(d, "flip_mc", "Flip", 62, 136, 112, 26, function()
   {
      gmSend("!house rotate roll 180");
   });
   gmButton(d, "level_mc", "Level", 178, 136, 110, 26, function()
   {
      gmSend("!house rotate level");
   }, 0x2E7D5B);

   d._x = gmDecorate.x != undefined ? gmDecorate.x : Math.max(0, gmScreenRight() - w - 40);
   d._y = gmDecorate.y != undefined ? gmDecorate.y : Math.max(gmTop(), gmBottom() - h - 240);
}

// --- player dialog (every player) ------------------------------------------------
// GM conversations open the client's own NPC dialog for its camera zoom (its pane stays hidden, see
// ClientMods/dialog); this draws the text where that pane sits: a speech bubble on the right of the
// screen with the answers as large buttons below it.

var GM_DIALOG_W = 420;
var GM_DIALOG_BUBBLE = 0xFFF8E7;
var GM_DIALOG_TEXT = 0x3A2400;

function gmShowDialog(parts)
{
   // dialog|npcGuid|name|nodeId|text|idx:choice|...
   _root.gmdialog_mc.removeMovieClip();
   var d = _root.createEmptyMovieClip("gmdialog_mc", 16001);
   var w = GM_DIALOG_W;
   var guid = parts[2];
   var node = parts[4];

   var bubble = d.createEmptyMovieClip("bubble_mc", 1);
   var name = gmUnescapeText(parts[3]);
   gmText(bubble, "name_txt", name == "" ? "..." : name, 20, 14, w - 70, 22, 16, GM_DIALOG_TEXT, true);
   var body = gmText(bubble, "body_txt", gmUnescapeText(parts[5]), 20, 42, w - 40, 20, 14, GM_DIALOG_TEXT, false);
   body.autoSize = "left";
   var bubbleH = 42 + Math.max(24, body.textHeight + 8) + 18;

   // Rounded cream bubble with a tail pointing left, toward the NPC.
   bubble.lineStyle(3, GM_GOLD_DARK, 100);
   bubble.beginFill(GM_DIALOG_BUBBLE, 100);
   gmRoundRect(bubble, 0, 0, w, bubbleH, 18);
   bubble.endFill();
   bubble.lineStyle(3, GM_GOLD_DARK, 100);
   bubble.beginFill(GM_DIALOG_BUBBLE, 100);
   bubble.moveTo(2, bubbleH - 46);
   bubble.lineTo(-22, bubbleH - 22);
   bubble.lineTo(2, bubbleH - 26);
   bubble.endFill();
   // Text fields sit above the drawing.
   bubble.name_txt.swapDepths(bubble.getNextHighestDepth());
   bubble.body_txt.swapDepths(bubble.getNextHighestDepth());

   gmButton(d, "close_mc", "X", w - 38, 10, 26, 24, function()
   {
      gmSend("!talk end");
   }, 0xC0392B);

   var choices = parts.slice(6);
   if(choices.length == 0 || (choices.length == 1 && choices[0] == ""))
   {
      choices = ["end:Goodbye"];
   }
   var y = bubbleH + 12;
   var i;
   for(i = 0; i < choices.length; i++)
   {
      var colon = choices[i].indexOf(":");
      var index = choices[i].substr(0, colon);
      var b = gmButton(d, "choice" + i + "_mc", gmUnescapeText(choices[i].substr(colon + 1)), 30, y, w - 30, 36, function()
      {
         gmSend(this.index == "end" ? "!talk end" : "!talk " + this.guid + " " + this.node + " " + this.index);
      }, index == "end" ? 0x2E7D5B : undefined);
      b.index = index;
      b.guid = guid;
      b.node = node;
      y += 42;
   }
   var h = y;

   d._x = Math.max(0, gmScreenRight() - w - 120);
   d._y = gmTop() + Math.max(20, (gmBottom() - gmTop() - h) / 2);
}

// Right edge of what is actually on screen. The chat window is a fixed 2000 wide, so it usually runs past
// the edge of the game; the screen width, when the player reports one, is the better bound.
function gmScreenRight()
{
   var screen = Number(System.capabilities.screenResolutionX);
   return screen > 0 && screen < gmRight() ? screen : gmRight() * 0.9;
}

function gmHandleScript(parts)
{
   var step = parts[2];
   if(step == "begin")
   {
      gmScript.kind = parts[3];
      gmScript.pending = parts[3] == "lib" ? "lib:" + parts[4] : parts[4];
      gmScript.loading = "";
      return;
   }
   if(step == "chunk")
   {
      gmScript.loading += parts[3];
      return;
   }
   if(step == "end")
   {
      gmScript.guid = gmScript.pending;
      gmScript.source = gmUnescapeText(gmScript.loading);
      gmScript.status = "Loaded.";
      gmScriptDraft = { key: "", text: "" };
      if(_root.gm_mc._visible && gmTab == "script")
      {
         gmShowTab("script");
      }
      return;
   }
   if(step == "ok" || step == "error")
   {
      gmScript.status = (step == "error" ? "Error: " : "") + gmUnescapeText(parts[3]);
      var result = _root.gm_mc.content_mc.result_txt;
      if(result != undefined)
      {
         result.text = gmScript.status;
      }
   }
}

// Re-renders the Talk tab after a tree update, keeping the selected line.
function gmConvRefresh()
{
   gmShowTab("conv");
}

// --- server data ---------------------------------------------------------------

function gmHandleData(text)
{
   var parts = gmUnescape(text.substr(text.indexOf(gmToken))).split("|");
   var kind = parts[1];
   var root_mc = _root.gm_mc;

   if(kind == "open" || kind == "referee")
   {
      // "referee" comes when a referee enters the world: just the edge tab. "open" (!gm) also toggles the panel.
      gmRole = parts[2];
      root_mc.title_mc.role_txt.text = gmRole == "Admin" ? "Admin" : "Moderator";
      gmShowEdgeTab();
      if(kind == "open")
      {
         gmPopIn(!root_mc._visible || root_mc._x < 0);
      }
      return;
   }
   if(kind == "items" || kind == "npcs" || kind == "houses" || kind == "players")
   {
      gmList = { kind: kind, search: parts[2], page: Number(parts[3]), pages: Number(parts[4]), total: Number(parts[5]) };
      gmResults = [];
      return;
   }
   if(kind == "pinfo")
   {
      gmHandlePlayerInfo(parts);
      return;
   }
   if(kind == "events")
   {
      gmHandleEvents(parts);
      return;
   }
   if(kind == "bookmarks")
   {
      gmHandleBookmarks(parts);
      return;
   }
   if(kind == "item" || kind == "npc" || kind == "house" || kind == "player")
   {
      gmResults.push(parts.slice(2));
      return;
   }
   if(kind == "end")
   {
      if(root_mc._visible && gmTab == gmList.kind)
      {
         gmShowTab(gmTab);
      }
      return;
   }
   if(kind == "edit")
   {
      // A selection opens the panel on the editor (no toggling, unlike "open").
      gmEdit = parts.slice(2);
      if(gmEdit[0] != "none" && !root_mc._visible)
      {
         gmTab = "edit";
         gmPopIn(true);
      }
      if(gmEdit[0] != "none" || gmTab == "edit")
      {
         gmShowTab("edit");
      }
      return;
   }
   if(kind == "script")
   {
      gmHandleScript(parts);
      return;
   }
   if(kind == "questlist" || kind == "questedit" || kind == "questgoal" || kind == "questreward" || kind == "questnodes")
   {
      gmHandleQuestData(kind, parts);
      return;
   }
   if(kind == "itemgen" || kind == "itemedit" || kind == "itemfield")
   {
      gmHandleItemGenData(kind, parts);
      return;
   }
   if(kind == "codes" || kind == "codeedit" || kind == "codeitem")
   {
      gmHandleCodeData(kind, parts);
      return;
   }
   if(kind == "tilesel" || kind == "tileundo" || kind == "tilekits")
   {
      gmHandleTileData(kind, parts);
      return;
   }
   if(kind == "redeem")
   {
      if(parts[2] == "open")
      {
         gmShowRedeem();
      }
      return;
   }
   if(kind == "scriptlib")
   {
      gmHandleScriptLib(parts);
      return;
   }
   if(kind == "conv")
   {
      gmConv = { guid: parts[2], start: parts[3], nodes: [], choices: [] };
      gmConvLinkFrom = undefined;
      return;
   }
   if(kind == "convnode")
   {
      gmConv.nodes.push({ id: parts[2], text: gmUnescapeText(parts[3]) });
      return;
   }
   if(kind == "convchoice")
   {
      gmConv.choices.push({ node: parts[2], index: parts[3], next: parts[4], give: parts[5], qty: parts[6], hook: parts[7], text: gmUnescapeText(parts[8]) });
      return;
   }
   if(kind == "convend")
   {
      if(root_mc._visible && gmTab == "conv")
      {
         gmConvRefresh();
      }
      return;
   }
   if(kind == "dialog")
   {
      if(parts[2] == "close")
      {
         _root.gmdialog_mc.removeMovieClip();
      }
      else
      {
         gmShowDialog(parts);
      }
      return;
   }
   if(kind == "decorate")
   {
      gmHandleDecorate(parts);
      return;
   }
   if(kind == "fields")
   {
      gmHandleFields(parts);
      return;
   }
   if(kind == "icons")
   {
      gmHandleIcons(parts);
      return;
   }
   if(kind == "anims")
   {
      gmAnims = [];
      var a;
      for(a = 2; a < parts.length; a++)
      {
         // <id>:<label>:<loop 1/0>
         var anim = parts[a].split(":");
         gmAnims.push([anim[0], anim[1], anim[2] == "1"]);
      }
      if(root_mc._visible && gmTab == "edit")
      {
         gmShowTab("edit");
      }
      return;
   }
   if(kind == "pos")
   {
      gmStatus(parts[2] + "  (" + parts[3] + ", " + parts[4] + ", " + parts[5] + ")");
   }
}

// Swallow GM data lines instead of printing them. The server sends them as debug/system chat,
// which can arrive through either entry point.
var gmOriginalAddSystemMessage = addSystemMessage;
addSystemMessage = function(guid, charName, bubText, txtColor, bubColor, messageCount)
{
   if(String(bubText).indexOf(gmToken) != -1)
   {
      gmHandleData(String(bubText));
      return undefined;
   }
   // Item gen and code failures always show in the panel's status bar; other replies only while it's open.
   var reply = String(bubText);
   if(reply.indexOf("Item gen failed: ") == 0 || reply.indexOf("Code failed: ") == 0)
   {
      gmStatus(reply);
   }
   else
   {
      gmEchoReply(reply);
   }
   return gmOriginalAddSystemMessage.apply(_root, arguments);
};

// Server replies (a command's result, usage or error) also show on the panel's status line, so the
// referee sees what a button did without looking at chat. They still go to chat as well.
function gmEchoReply(text)
{
   if(!_root.gm_mc._visible)
   {
      return;
   }
   var plain = "";
   var inTag = false;
   var i;
   for(i = 0; i < text.length; i++)
   {
      var c = text.charAt(i);
      if(c == "<")
      {
         inTag = true;
      }
      else if(c == ">")
      {
         inTag = false;
      }
      else if(!inTag)
      {
         plain += c == "\n" || c == "\r" ? "  " : c;
      }
   }
   plain = gmUnescape(plain);
   gmStatus(plain.length > 140 ? plain.substr(0, 137) + "..." : plain);
}

var gmOriginalAddChatMessege = addChatMessege;
addChatMessege = function(guid, charName, bubText, txtSize, txtColor, bubColor, channelName, isWhispNum, isNPC, isRef, areaName, messageCount)
{
   if(String(bubText).indexOf(gmToken) != -1)
   {
      gmHandleData(String(bubText));
      return undefined;
   }
   return gmOriginalAddChatMessege.apply(_root, arguments);
};

gmBuild();
// ===== End Sanctuary GM panel =====

// ===== Sanctuary chat style =====
// Appended to chatWindowCtrl's main timeline by build_client_mods.py, after gm_panel.as.
//
// The chat box in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): chat sits on a soft navy glass
// panel docked to the left screen edge, which fades in on hover, on a new line and while typing, and fades out
// again when idle. Lines are outlined so they read over the world, names get a bright colour of their own (gold
// for you, referees and NPCs), and channel colours are softened for navy. The channel tabs are navy pills with
// the selected one in gold, the input is a recessed field with a gold send button, and the scrollbar and the
// resize grip are slim and gold. Everything is drawn under or inside the stock clips, which keep their roles
// (hit areas, Lua callbacks, the referee panel's hidden lines), so stock behaviour is unchanged.
//
// The stock hover fade never worked in the enlarged window (install_client_mods.py makes it about the size of the
// screen): Lua reports the mouse relative to the window's top left, while chat is drawn 1:1 at its bottom left.
// OnMousePosition now moves the mouse into stage coordinates, so hovering chat shows its tabs, input, scrollbar
// and buttons again and the mouse wheel scrolls it.

// Height of the chat movie's own stage (see gm_panel.as: chat is drawn 1:1 anchored bottom-left).
var CS_STAGE_H = 345;
var CS_TEXT_SIZE = 13;
// How long a new line keeps the glass up (ms), and how long it lingers once idle (s).
var CS_MESSAGE_HOLD = 5000;
var CS_LINGER = 1.2;
var CS_IDLE_ALPHA = 0;
var CS_RADIUS = 16;

var CS_GOLD = 0xFFC531;
var CS_GOLD_LIGHT = 0xFFE08A;
var CS_GOLD_DARK = 0xF5A623;
var CS_GOLD_RIM = 0xFFF3C4;
var CS_INK = 0x3A2400;
var CS_RIM = 0x9DB8FF;
var CS_SOFT = 0xC9D6FF;
var CS_TEXT = 0xF2F5FF;
var CS_REF_TEXT = 0x9CDBFF;
var CS_NPC_NAME = 0xFFB98A;
var CS_SYSTEM_TEXT = 0xFF9A9A;
var CS_OUTLINE = 0x050A24;

// Speaker names: picked from the name, so everyone keeps their colour (gold is kept for you and referees).
var CS_NAME_COLORS = [0x8CD3FF, 0xFF9ED8, 0xB9A6FF, 0x7CF2C2, 0xC8F27C, 0x6FE7F0, 0xFF8F9E, 0xA8C4FF];

// Stock channel and system colours -> the same hue, lighter, so it reads on navy.
var CS_SOFT_COLORS = {};
CS_SOFT_COLORS[0x00FFCC] = 0x7AF5DA; // group
CS_SOFT_COLORS[0x00CC00] = 0x8CF58C; // guild
CS_SOFT_COLORS[0xCCCC99] = 0xEDE3A8; // area, trade, members
CS_SOFT_COLORS[0x99CC99] = 0xB8EFA4; // looking for group
CS_SOFT_COLORS[0xFF3300] = 0xFF9466; // shouts
CS_SOFT_COLORS[0xFF00FF] = 0xFF9BF2; // whispers
CS_SOFT_COLORS[0xFF0000] = CS_SYSTEM_TEXT; // system lines
CS_SOFT_COLORS[0xFFFF00] = CS_GOLD_LIGHT;
CS_SOFT_COLORS[0x000000] = CS_TEXT;

// Fade state: "in" (glass up) or "out", what keeps it up, and the timer that checks while it's up.
var csState = "out";
var csHover = false;
var csTyping = false;
var csMessageUntil = 0;
var csTimer;

// --- drawing helpers -------------------------------------------------------------------

function csTop()
{
   return Math.min(0, CS_STAGE_H - Stage.height);
}

function csRoundRect(mc, x, y, w, h, r)
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

// Rounded on the right only; the left side runs off the screen edge (the chat window sits at x 0).
function csDockRect(mc, x, y, w, h, r)
{
   mc.moveTo(x, y);
   mc.lineTo(x + w - r, y);
   mc.curveTo(x + w, y, x + w, y + r);
   mc.lineTo(x + w, y + h - r);
   mc.curveTo(x + w, y + h, x + w - r, y + h);
   mc.lineTo(x, y + h);
   mc.lineTo(x, y);
}

function csCircle(mc, x, y, r)
{
   var k = Math.tan(Math.PI / 8);
   var s = Math.sin(Math.PI / 4);
   mc.moveTo(x + r, y);
   mc.curveTo(x + r, y + k * r, x + s * r, y + s * r);
   mc.curveTo(x + k * r, y + r, x, y + r);
   mc.curveTo(x - k * r, y + r, x - s * r, y + s * r);
   mc.curveTo(x - r, y + k * r, x - r, y);
   mc.curveTo(x - r, y - k * r, x - s * r, y - s * r);
   mc.curveTo(x - k * r, y - r, x, y - r);
   mc.curveTo(x + k * r, y - r, x + s * r, y - s * r);
   mc.curveTo(x + r, y - k * r, x + r, y);
}

function csGradient(mc, x, y, w, h, top, bottom, topAlpha, bottomAlpha)
{
   mc.beginGradientFill("linear", [top, bottom], [topAlpha, bottomAlpha], [0, 255], { matrixType: "box", x: x, y: y, w: w, h: h, r: Math.PI / 2 });
}

// The dark outline every light text gets over the world and the glass.
function csOutline()
{
   return new flash.filters.GlowFilter(CS_OUTLINE, 0.85, 3, 3, 3, 2);
}

function csChatShown()
{
   return _root.m_textView != undefined && _root.m_textView._visible;
}

// --- the glass ---------------------------------------------------------------------------

function csBack()
{
   if(_root.csback_mc == undefined)
   {
      // Below every stock clip and the referee panel (gm_mc 16000, its edge tab 15990).
      var back = _root.createEmptyMovieClip("csback_mc", -16384);
      back._alpha = CS_IDLE_ALPHA;
      back._visible = false;
   }
   return _root.csback_mc;
}

// The channel tabs' row (stage coordinates), or undefined before the first tab exists.
function csTabsBounds()
{
   var list = _root.m_filterBar_mc.m_filterButtonArray;
   var top;
   var bottom;
   var i;
   for(i = 0; i < list.length; i++)
   {
      var b = list[i];
      if(b != undefined && b.up_mc != undefined)
      {
         var r = b.up_mc.getBounds(_root);
         top = top == undefined ? r.yMin : Math.min(top, r.yMin);
         bottom = bottom == undefined ? r.yMax : Math.max(bottom, r.yMax);
      }
   }
   return top == undefined ? undefined : { yMin: top, yMax: bottom };
}

// Follows the stock backing (m_bg_mc, which onListResize sizes to the text box): from just under the tabs to
// just under the input, and a little wider than the text.
function csLayout()
{
   var bg = _root.m_bg_mc;
   if(bg == undefined)
   {
      return;
   }
   var tabs = csTabsBounds();
   var top = tabs == undefined ? _root.m_bg_top_mc._y + 6 : tabs.yMax + 3;
   var right = Math.round(bg._x + bg._width + 4);
   var entry = _root.entryBox_mc;
   var bottom = Math.min(CS_STAGE_H - 1, entry._y + entry._height + 3);
   var h = bottom - top;
   if(h < 40 || right < 80)
   {
      return;
   }

   var back = csBack();
   back.clear();
   // Navy glass, a touch denser at the top, with a pale rim.
   back.lineStyle(1.5, CS_RIM, 40);
   csGradient(back, 0, top, right, h, 0x22398C, 0x0A1440, 60, 50);
   csDockRect(back, -20, top, right + 20, h, CS_RADIUS);
   back.endFill();
   // Soft light from the top left.
   back.lineStyle();
   back.beginGradientFill("radial", [0x8FB4FF, 0x8FB4FF], [22, 0], [0, 255], { matrixType: "box", x: -180, y: top - 170, w: 560, h: 340, r: 0 });
   csDockRect(back, -20, top + 1, right + 19, h - 2, CS_RADIUS - 1);
   back.endFill();
   // A gold hairline under the top rim, fading out to the right.
   back.beginGradientFill("linear", [CS_GOLD, CS_GOLD], [70, 0], [0, 255], { matrixType: "box", x: 0, y: top, w: right * 0.75, h: 2, r: 0 });
   csRoundRect(back, 0, top + 2, right * 0.75, 1.5, 0);
   back.endFill();
}

function csFadeTo(alpha, delay, duration)
{
   var back = csBack();
   if(alpha > 0)
   {
      back._visible = csChatShown();
   }
   gs.TweenLite.to(back, duration, { _alpha: alpha, delay: delay, ease: mx.transitions.easing.Strong.easeOut, overwrite: true, onComplete: alpha > 0 ? undefined : csBackFaded });
}

// Hidden once faded, so the glass never takes the mouse while it can't be seen.
function csBackFaded()
{
   if(csState == "out" && CS_IDLE_ALPHA == 0)
   {
      _root.csback_mc._visible = false;
   }
}

// Typing: the input has the focus, and Lua opened it for chat (Enter in game) or it has text in it.
function csIsTyping()
{
   var e = _root.entryBox_mc;
   if(!e._visible || (!csTyping && !(e.entry_txt.length > 0)))
   {
      return false;
   }
   var focus = Selection.getFocus();
   return focus == null || focus == undefined ? false : eval(focus) == e.entry_txt;
}

// Glass up while the mouse is over chat, while typing, and for a few seconds after a new line.
function csUpdateFade()
{
   var awake = csChatShown() && (csHover || csIsTyping() || getTimer() < csMessageUntil);
   if(awake)
   {
      if(csState != "in")
      {
         csState = "in";
         csFadeTo(100, 0, 0.3);
      }
      if(csTimer == undefined)
      {
         csTimer = setInterval(csUpdateFade, 500);
      }
   }
   else
   {
      if(csTimer != undefined)
      {
         clearInterval(csTimer);
         csTimer = undefined;
      }
      if(csState != "out")
      {
         csState = "out";
         csFadeTo(CS_IDLE_ALPHA, csChatShown() ? CS_LINGER : 0, 0.9);
      }
   }
   if(!csChatShown())
   {
      csBack()._visible = false;
   }
}

// While typing, the input and the channel tabs stay up even with the mouse elsewhere.
function csKeepChromeUp()
{
   _root.entryBox_mc._alpha = 100;
   if(_root.m_filterBar_mc != undefined)
   {
      gs.TweenLite.to(_root.m_filterBar_mc, 0.1, { _alpha: 100, ease: mx.transitions.easing.Strong.easeOut, overwrite: true });
   }
}

// --- chat lines ------------------------------------------------------------------------

function csHex(c)
{
   var s = Number(c).toString(16);
   while(s.length < 6)
   {
      s = "0" + s;
   }
   return "#" + s;
}

function csFont(c, s)
{
   return "<font color='" + csHex(c) + "'>" + s + "</font>";
}

function csSoften(c)
{
   var soft = CS_SOFT_COLORS[Number(c)];
   return soft == undefined ? c : soft;
}

function csNameColor(item)
{
   var name = String(item.getName());
   if(item.getIsRef())
   {
      return CS_GOLD;
   }
   if(item.getIsNPC())
   {
      return CS_NPC_NAME;
   }
   if(name == ChatManager.Instance().getMyCharacterName())
   {
      return CS_GOLD_LIGHT;
   }
   var hash = 0;
   var i;
   for(i = 0; i < name.length; i++)
   {
      hash = (hash * 31 + name.charCodeAt(i)) % 65521;
   }
   return CS_NAME_COLORS[hash % CS_NAME_COLORS.length];
}

// The stock line (TextChat.getStringFromItem) with new colours: the speaker's name in its own colour, then the
// text in white, the channel's colour or referee blue. The name link (onNameClicked) and the text are unchanged.
function csLine(item)
{
   var name = item.getName();
   var over = item.getOverrideColor();
   var hasOver = over > -1;
   var line;
   if(name == undefined || name == null || name == "")
   {
      line = csFont(hasOver ? csSoften(over) : CS_TEXT, item.getChatText());
   }
   else
   {
      var label = item.getGroup() == _root.WORLDSHOUT_CHANNEL ? ChatManager.Instance().getDecoratedName(item) : name + ": ";
      var head = csFont(csNameColor(item), label);
      if(!item.getIsNPC() && !item.getIsRef())
      {
         head = "<a href='asfunction:_root.onNameClicked," + item.getGuid() + "::" + name + "::" + item.getIsFriend() + "'>" + head + "</a>";
      }
      var textColor = item.getIsRef() ? CS_REF_TEXT : (hasOver ? csSoften(over) : CS_TEXT);
      line = head + csFont(textColor, item.getChatText());
   }
   return "<font size='" + CS_TEXT_SIZE + "'>" + line + "</font>\n";
}

TextChat.prototype.getStringFromItem = function(item)
{
   return csLine(item);
};

// New lines (only the ones the selected tab shows) bring the glass up for a moment.
var csStockAddItem = TextChat.prototype.onAddItem;
TextChat.prototype.onAddItem = function(item)
{
   var result = csStockAddItem.apply(this, arguments);
   if(ChatManager.Instance().getFilteredIn(item))
   {
      csMessageUntil = getTimer() + CS_MESSAGE_HOLD;
      csUpdateFade();
   }
   return result;
};

// The scrollbar is an imported movie (FRScrollBarV2.swf); it calls init once it has loaded.
var csStockTextInit = TextChat.prototype.init;
TextChat.prototype.init = function()
{
   var result = csStockTextInit.apply(this, arguments);
   csStyleScrollbar(this.m_scrollbar);
   return result;
};

// --- the text box --------------------------------------------------------------------------

function csStyleTextView()
{
   var v = _root.m_textView;
   if(v == undefined || v.csStyled)
   {
      return;
   }
   v.csStyled = true;
   var t = v.textArea_txt;
   var f = new TextFormat();
   f.size = CS_TEXT_SIZE;
   f.color = CS_TEXT;
   f.leading = 2;
   t.setNewTextFormat(f);
   t.filters = [csOutline()];
   csStyleGrip(v.dragable_mc);
   csStyleScrollbar(v.m_scrollbar);
   // Lines already in the box are redrawn in the new colours.
   if(v.m_model != undefined)
   {
      v.reset();
   }
}

// The resize corner: gold grip dots in place of the stock icon (the stock button stays as the hit area).
function csStyleGrip(d)
{
   if(d == undefined || d.csskin_mc != undefined)
   {
      return;
   }
   d.resize_btn._alpha = 0;
   var g = d.createEmptyMovieClip("csskin_mc", -16384);
   var rows = [[8, 12, 16], [12, 16], [16]];
   var i;
   var j;
   for(i = 0; i < rows.length; i++)
   {
      for(j = 0; j < rows[i].length; j++)
      {
         g.beginFill(CS_GOLD_LIGHT, 85);
         csCircle(g, rows[i][j], 3 + i * 4, 1.4);
         g.endFill();
      }
   }
   g.filters = [csOutline()];
}

// A slim gold thumb on a dark track; the stock parts stay as (invisible) hit areas.
function csStyleScrollbar(sb)
{
   if(sb == undefined || sb.thumb_mc == undefined || sb.csStyled)
   {
      return;
   }
   sb.csStyled = true;
   sb.createEmptyMovieClip("csskin_mc", -16384);
   var proto = FRScrollBarV2.prototype;
   sb.moveThumb = function()
   {
      var result = proto.moveThumb.apply(this, arguments);
      csDrawScrollbar(this);
      return result;
   };
   sb.setHeight = function()
   {
      var result = proto.setHeight.apply(this, arguments);
      csDrawScrollbar(this);
      return result;
   };
   sb.setContentHeightForText = function()
   {
      var result = proto.setContentHeightForText.apply(this, arguments);
      csDrawScrollbar(this);
      return result;
   };
   csDrawScrollbar(sb);
}

function csDrawScrollbar(sb)
{
   var skin = sb.csskin_mc;
   var bg = sb.background_mc;
   if(skin == undefined || bg == undefined)
   {
      return;
   }
   bg._alpha = sb.channel_mc._alpha = sb.thumb_mc._alpha = sb.upArrow_btn._alpha = sb.downArrow_btn._alpha = 0;
   var cx = bg._x + bg._width / 2;
   var top = sb.channel_mc._y;
   var h = sb.channel_mc._height;
   skin.clear();
   if(h < 8)
   {
      return;
   }
   skin.beginFill(0x0A1440, 60);
   csRoundRect(skin, cx - 2, top, 4, h, 2);
   skin.endFill();
   var th = sb.thumb_mc._height;
   if(th < h - 1)
   {
      skin.lineStyle(1, CS_GOLD_RIM, 70);
      csGradient(skin, cx - 3, sb.thumb_mc._y, 6, th, CS_GOLD_LIGHT, CS_GOLD_DARK, 100, 100);
      csRoundRect(skin, cx - 3, sb.thumb_mc._y, 6, th, 3);
      skin.endFill();
      skin.lineStyle();
   }
   // Small chevrons where the step arrows are.
   var up = sb.upArrow_btn._y + sb.upArrow_btn._height / 2;
   var down = sb.downArrow_btn._y + sb.downArrow_btn._height / 2;
   skin.beginFill(CS_GOLD_LIGHT, 80);
   skin.moveTo(cx - 4, up + 2);
   skin.lineTo(cx, up - 2);
   skin.lineTo(cx + 4, up + 2);
   skin.lineTo(cx - 4, up + 2);
   skin.endFill();
   skin.beginFill(CS_GOLD_LIGHT, 80);
   skin.moveTo(cx - 4, down - 2);
   skin.lineTo(cx, down + 2);
   skin.lineTo(cx + 4, down - 2);
   skin.lineTo(cx - 4, down - 2);
   skin.endFill();
}

// --- channel tabs ------------------------------------------------------------------------

// Navy pill; lighter with a gold rim on hover; gold with dark text when selected. Drawn inside the tab's
// stock art (up_mc stays as the invisible hit area), so the tab keeps its width.
function csSkinTab(btn)
{
   if(btn.up_mc == undefined)
   {
      return;
   }
   btn.up_mc._alpha = 0;
   btn.over_mc._visible = false;
   btn.sel_mc._visible = false;
   var skin = btn.csskin_mc;
   if(skin == undefined)
   {
      skin = btn.createEmptyMovieClip("csskin_mc", -16384);
   }
   skin.clear();
   var r = btn.up_mc.getBounds(btn);
   var x = r.xMin + 2;
   var y = r.yMin + 2;
   var w = r.xMax - r.xMin - 4;
   var h = r.yMax - r.yMin - 3;
   if(w < 6 || h < 6)
   {
      return;
   }
   var state = btn.m_selected ? 2 : (btn.csHover ? 1 : 0);
   if(state == 2)
   {
      skin.lineStyle(1.5, CS_GOLD_RIM, 100);
      csGradient(skin, x, y, w, h, CS_GOLD_LIGHT, CS_GOLD_DARK, 100, 100);
   }
   else if(state == 1)
   {
      skin.lineStyle(1.5, CS_GOLD_LIGHT, 90);
      csGradient(skin, x, y, w, h, 0x3F66D6, 0x1D307A, 92, 92);
   }
   else
   {
      skin.lineStyle(1, CS_RIM, 35);
      csGradient(skin, x, y, w, h, 0x284A9E, 0x162660, 78, 78);
   }
   csRoundRect(skin, x, y, w, h, h / 2);
   skin.endFill();
   skin.filters = state == 2 ? [new flash.filters.GlowFilter(CS_GOLD, 0.45, 8, 8, 1, 2)] : [];
   var t = btn.filterName_txt;
   t.textColor = state == 2 ? CS_INK : (state == 1 ? 0xFFFFFF : CS_SOFT);
   t.filters = state == 2 ? [] : [csOutline()];
}

function csWrapTab(name, hover)
{
   var stock = FilterButton.prototype[name];
   FilterButton.prototype[name] = function()
   {
      if(hover != undefined)
      {
         this.csHover = hover;
      }
      var result = stock.apply(this, arguments);
      csSkinTab(this);
      return result;
   };
}

csWrapTab("setStateUp", false);
csWrapTab("setStateOver", true);
csWrapTab("setStateSelected");

// render resizes the tab and the bar lays the tabs out from their widths, so the old pill goes first.
var csStockTabRender = FilterButton.prototype.render;
FilterButton.prototype.render = function()
{
   this.csskin_mc.clear();
   var result = csStockTabRender.apply(this, arguments);
   csSkinTab(this);
   return result;
};

function csSkinTabs()
{
   var list = _root.m_filterBar_mc.m_filterButtonArray;
   var i;
   for(i = 0; i < list.length; i++)
   {
      if(list[i] != undefined)
      {
         csSkinTab(list[i]);
      }
   }
}

// --- the input ---------------------------------------------------------------------------

// A recessed field (gold rim while typing) and a gold round send button, inside the stock box's own bounds.
function csDrawEntry()
{
   var e = _root.entryBox_mc;
   var skin = e.csskin_mc;
   if(skin == undefined)
   {
      return;
   }
   var w = e.textBG_mc._width;
   skin.clear();
   if(w < 40)
   {
      return;
   }
   var typing = csIsTyping();
   skin.lineStyle(1.5, typing ? CS_GOLD : CS_RIM, typing ? 85 : 40);
   csGradient(skin, 1, 3, w - 2, 32, 0x060D2E, 0x0F1C52, 82, 82);
   csRoundRect(skin, 1, 3, w - 2, 32, 16);
   skin.endFill();
   skin.filters = typing ? [new flash.filters.GlowFilter(CS_GOLD, 0.35, 10, 10, 1, 2)] : [];
}

function csDrawSend(btn, state)
{
   var skin = btn.csskin_mc;
   if(skin == undefined)
   {
      return;
   }
   btn.up_mc._alpha = btn.over_mc._alpha = btn.down_mc._alpha = 0;
   var cx = 11;
   var cy = state == 2 ? 15 : 14;
   skin.clear();
   skin.lineStyle(1.5, CS_GOLD_RIM, 100);
   csGradient(skin, cx - 11, cy - 11, 22, 22, state == 1 ? 0xFFF0B8 : CS_GOLD_LIGHT, state == 2 ? 0xD98A10 : CS_GOLD_DARK, 100, 100);
   csCircle(skin, cx, cy, 11);
   skin.endFill();
   // A paper-plane arrow.
   skin.lineStyle();
   skin.beginFill(CS_INK, 100);
   skin.moveTo(cx - 5, cy - 5);
   skin.lineTo(cx + 6, cy);
   skin.lineTo(cx - 5, cy + 5);
   skin.lineTo(cx - 2, cy);
   skin.lineTo(cx - 5, cy - 5);
   skin.endFill();
   skin.filters = state == 1 ? [new flash.filters.GlowFilter(CS_GOLD, 0.7, 10, 10, 1, 2)] : [];
}

function csStyleEntry()
{
   var e = _root.entryBox_mc;
   if(e == undefined || e.csskin_mc != undefined)
   {
      return;
   }
   e.textBG_mc._alpha = 0;
   e.createEmptyMovieClip("csskin_mc", -16384);
   e.setWidth = function()
   {
      var result = TextEntryBox.prototype.setWidth.apply(this, arguments);
      csDrawEntry();
      return result;
   };
   var t = e.entry_txt;
   var stockFocus = t.onSetFocus;
   t.onSetFocus = function()
   {
      var result = stockFocus.apply(this, arguments);
      csSetTyping();
      return result;
   };
   t.onKillFocus = function()
   {
      csSetTyping(false);
   };

   var s = e.sendChatButton;
   s.createEmptyMovieClip("csskin_mc", -16384);
   var proto = FRIconButton.prototype;
   s.setStateUp = function()
   {
      var result = proto.setStateUp.apply(this, arguments);
      csDrawSend(this, 0);
      return result;
   };
   s.setStateOver = function()
   {
      var result = proto.setStateOver.apply(this, arguments);
      csDrawSend(this, 1);
      return result;
   };
   s.setStateDown = function()
   {
      var result = proto.setStateDown.apply(this, arguments);
      csDrawSend(this, 2);
      return result;
   };
   csDrawSend(s, 0);
   csDrawEntry();
}

// true / false when typing starts or ends, nothing to just refresh (focus or text changed).
function csSetTyping(typing)
{
   if(typing != undefined)
   {
      csTyping = typing;
   }
   csDrawEntry();
   if(csIsTyping())
   {
      csKeepChromeUp();
   }
   csUpdateFade();
}

// --- stock hooks -------------------------------------------------------------------------
// Lua and the stock clips call these on _root by name, so each is wrapped in place, keeping its arguments
// and return value.

// Lua passes the mouse relative to the window's top left (ChatLog:GetMousePosition); chat is drawn bottom-left
// aligned in the enlarged window, so the point moves into stage coordinates. Over the open referee panel,
// chat counts as not hovered (so the wheel and the fade stay with the panel).
var csStockOnMousePosition = _root.OnMousePosition;
_root.OnMousePosition = function(x, y)
{
   var sx = Number(x);
   var sy = Number(y) + csTop();
   if(_root.gm_mc._visible && _root.gm_mc.hitTest(sx, sy, true))
   {
      sx = -10000;
      sy = -10000;
   }
   var result = csStockOnMousePosition.call(_root, sx, sy);
   if(csIsTyping())
   {
      csKeepChromeUp();
   }
   return result;
};

// fadeTabs(false) is "the mouse is over chat" (OnMousePosition, the tabs, the resize grip, Lua's OnEnter).
var csStockFadeTabs = fadeTabs;
fadeTabs = function(b)
{
   var result = csStockFadeTabs.apply(_root, arguments);
   csHover = !b;
   if(b && csIsTyping())
   {
      csKeepChromeUp();
   }
   csUpdateFade();
   return result;
};

var csStockShowSimpleList = showSimpleList;
showSimpleList = function()
{
   var result = csStockShowSimpleList.apply(_root, arguments);
   csStyleTextView();
   csLayout();
   return result;
};

var csStockOnListResize = onListResize;
onListResize = function(dataObj)
{
   var result = csStockOnListResize.apply(_root, arguments);
   csLayout();
   return result;
};

var csStockAddFilter = addFilter;
addFilter = function(guid, name, isWhisper, place)
{
   var result = csStockAddFilter.apply(_root, arguments);
   csSkinTabs();
   csLayout();
   return result;
};

// The stock backing is now invisible (it stays as the hover area); the glass hides with the log.
var csStockSetShowChatLog = SetShowChatLog;
SetShowChatLog = function(b)
{
   var result = csStockSetShowChatLog.apply(_root, arguments);
   _root.m_bg_mc._visible = _root.m_bg_top_mc._visible = boolinate(b);
   csLayout();
   csUpdateFade();
   return result;
};

// Typing starts with SetTextEntry / SetFocusinSWF (Enter in game) and ends with KillCursor / fadeEntryBox
// (Lua's FocusKilled) or Escape / Enter on an empty line.
var csStockSetTextEntry = SetTextEntry;
SetTextEntry = function(isFocused, startText, isFromGame)
{
   var result = csStockSetTextEntry.apply(_root, arguments);
   csSetTyping(isFocused == undefined || Number(isFocused) != 0);
   return result;
};

// SetFocusinSWF(true) also comes without the player typing (showing the log), so it only ends typing.
var csStockSetFocusinSWF = SetFocusinSWF;
SetFocusinSWF = function(b)
{
   var result = csStockSetFocusinSWF.apply(_root, arguments);
   csSetTyping(boolinate(b) ? undefined : false);
   return result;
};

// The input broadcasts its key presses (TextEntryBox.onKeyUp), so typed text counts as soon as it's there.
var csStockOnKeyUp = onKeyUp;
onKeyUp = function(str)
{
   var result = csStockOnKeyUp.apply(_root, arguments);
   csSetTyping();
   return result;
};

function csWrapTypingEnd(name)
{
   var stock = _root[name];
   _root[name] = function()
   {
      var result = stock.apply(_root, arguments);
      csSetTyping(false);
      return result;
   };
}

csWrapTypingEnd("KillCursor");
csWrapTypingEnd("fadeEntryBox");
csWrapTypingEnd("onSendKillFocus");
csWrapTypingEnd("onSendKillFocusEnterPressed");

// --- start ---------------------------------------------------------------------------------

// The stock backing stays as the hover area but no longer shows or fades (the glass replaces it).
function csRetireStockBacking()
{
   var list = _root.m_fadingTabsArray;
   var i;
   for(i = list.length - 1; i >= 0; i--)
   {
      if(list[i] == _root.m_bg_mc || list[i] == _root.m_bg_top_mc)
      {
         list.splice(i, 1);
      }
   }
   gs.TweenLite.killTweensOf(_root.m_bg_mc);
   gs.TweenLite.killTweensOf(_root.m_bg_top_mc);
   _root.m_bg_mc._alpha = 0;
   _root.m_bg_top_mc._alpha = 0;
}

// Lua may already have set chat up during the stock onLoad, so style whatever exists now; the hooks above
// catch anything created later.
csRetireStockBacking();
csBack();
csStyleEntry();
csStyleTextView();
csSkinTabs();
csLayout();
csUpdateFade();
// ===== End Sanctuary chat style =====

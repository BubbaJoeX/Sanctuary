// ===== Sanctuary System Menu =====
// Appended to options' main timeline by build_client_mods.py.
// Redraws the system menu (the top bar's gear or Esc, wndOptions) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): the squirrel peeks over the top of a navy glass panel, the Free Realms
// logo sits in its header, and each entry Lua adds (ExitPanel.lua, the HelpCreditsElements table) is a full-width
// glass row with its icon in a frosted socket and a line about what it does. Exit Game is a red pill and the close
// button the gold circle. Ask for Help is gone: its row is never built, so the others close up. The stock rows,
// Exit Game button, its Yes/No confirmation and the back tab stay as invisible hit areas under the new art and keep
// sending their stock events; the close button is our own and calls the stock returnToGame(). The stock medallion
// is dropped at build time (REMOVED_DEPTHS); the squirrel and the row icons are client textures (TEXTURE_SYMBOLS).

var SM_PANEL_X = 14;
var SM_PANEL_W = 372;
var SM_PANEL_TOP = 116;
// The rows: the first one's top (Options._y) and one row's height (the stock optionHeight was 33).
var SM_ROWS_Y = 222;
var SM_ROW_H = 54;
// The row cards and the Exit Game pill: left edge on the stage, width, and the card's top and height in its row.
var SM_CARD_X = 34;
var SM_CARD_W = 332;
var SM_CARD_TOP = 3;
var SM_CARD_H = 46;
// The Exit Game pill's top and height, from buttonholder's _y.
var SM_EXIT_TOP = -15;
var SM_EXIT_H = 48;
var SM_CLOSE_X = 362;
var SM_CLOSE_Y = 140;
var SM_GOLD = 0xFFC531;
var SM_RIM = 0x9DB8FF;
var SM_SOFT = 0xC9D6FF;
var SM_INK = 0x3A2400;

// Option id (ExitPanel.lua) -> [icon symbol, what it does]. Labels stay Lua's (localized).
var SM_ENTRIES = {
   options:["sanctuarySystemGear","Sound, graphics and controls"],
   submitBugReport:["sanctuarySystemBug","Tell us what went wrong"],
   teleportToSafety:["sanctuarySystemHouse","Stuck? Hop back to safe ground"],
   credits:["sanctuarySystemScroll","The folks who made Free Realms"]
};

var smFrame;
var smRows = [];
var smBottom;
var smBackOver = false;
var smBackKey = "";

// --- drawing helpers -------------------------------------------------------------------

function smRoundRect(mc, x, y, w, h, r)
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

function smGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   smRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

// The soft white shine across the top of a card.
function smGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   smRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function smCircle(mc, x, y, r)
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

// Frosted sockets behind item art: the client's icons are made for light plates.
function smSocketCircle(mc, x, y, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.3,y:y - r * 1.5,w:r * 2.6,h:r * 2.6,r:0});
   smCircle(mc,x,y,r);
   mc.endFill();
}

function smSparkle(mc, x, y, r, color, alpha)
{
   mc.lineStyle();
   mc.beginFill(color,alpha);
   mc.moveTo(x,y - r * 2);
   mc.curveTo(x,y,x + r * 2,y);
   mc.curveTo(x,y,x,y + r * 2);
   mc.curveTo(x,y,x - r * 2,y);
   mc.curveTo(x,y,x,y - r * 2);
   mc.endFill();
}

function smStar(mc, x, y, r, color, alpha)
{
   var i;
   mc.lineStyle();
   mc.beginFill(color,alpha);
   for(i = 0; i <= 10; i++)
   {
      var a = - Math.PI / 2 + i * Math.PI / 5;
      var d = i % 2 == 0 ? r : r * 0.45;
      if(i == 0)
      {
         mc.moveTo(x + Math.cos(a) * d,y + Math.sin(a) * d);
      }
      else
      {
         mc.lineTo(x + Math.cos(a) * d,y + Math.sin(a) * d);
      }
   }
   mc.endFill();
}

// A power symbol: a ring open at the top and a stroke down into it.
function smPowerGlyph(mc, x, y, r, color)
{
   var i;
   mc.lineStyle(3,color,100);
   for(i = 0; i <= 16; i++)
   {
      var a = (-55 + i * 290 / 16) * Math.PI / 180;
      if(i == 0)
      {
         mc.moveTo(x + Math.cos(a) * r,y + Math.sin(a) * r);
      }
      else
      {
         mc.lineTo(x + Math.cos(a) * r,y + Math.sin(a) * r);
      }
   }
   mc.moveTo(x,y - r - 2);
   mc.lineTo(x,y - 1);
}

// A chevron pointing right (or left), centered on x,y.
function smChevron(mc, x, y, s, color, alpha, thickness, left)
{
   var d = left ? -1 : 1;
   mc.lineStyle(thickness,color,alpha);
   mc.moveTo(x - d * s / 2,y - s);
   mc.lineTo(x + d * s / 2,y);
   mc.lineTo(x - d * s / 2,y + s);
}

function smText(mc, name, txt, x, y, w, h, size, color, align)
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

function smOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function smShadow()
{
   return [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
}

// Penner's elastic ease (the movie only has Strong compiled in).
function smElasticOut(t, b, c, d)
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

function smBuild()
{
   // The stock panel pieces and inner plate go; Exit Game (and the plate behind it) stays as an invisible hit
   // area, and the beige X is replaced by our close button. The back tab's art goes too (BackButton only toggles
   // its parts' _visible, so their _alpha sticks); the tab itself still takes the click.
   bgTop._visible = false;
   bgMid._visible = false;
   bgBot._visible = false;
   content._visible = false;
   buttonholder._alpha = 0;
   btnX._visible = false;
   var parts = ["topup","topdown","tophover","middleup","middledown","middlehover","bottomup","bottomdown","bottomhover"];
   var i;
   for(i = 0; i < parts.length; i++)
   {
      btnBack[parts[i]]._alpha = 0;
   }

   // Bigger rows, under a header that holds the logo. The mask is a little taller than the rows so a lifted
   // row's glow isn't cut off, and wide enough for the cards (a submenu still slides out of it).
   optionHeight = SM_ROW_H;
   scrollerBaseY = SM_ROWS_Y;
   Options._y = SM_ROWS_Y;
   menuMask._y = SM_ROWS_Y - 4;
   menuMask._width = SM_CARD_W + 20;
   var mb = menuMask.getBounds(_root);
   menuMask._x += SM_CARD_X - 10 - mb.xMin;
   buttonholder._x = 0;
   btnBack._x = SM_PANEL_X + 6;
   btnBack._y = menuMask._y;

   // The Exit Game button's hit area becomes the pill's rectangle (its hit shape spans -2..327 x 0..72).
   var exit = buttonholder.exit;
   exit._xscale = SM_CARD_W / 329 * 100;
   exit._yscale = SM_EXIT_H / 72 * 100;
   exit._x = SM_CARD_X + 2 * exit._xscale / 100;
   exit._y = SM_EXIT_TOP;

   var f = smFrame = _root.createEmptyMovieClip("sanctuarySystemMenu",-16384);

   // A soft glow behind the squirrel, with a few sparkles.
   var halo = f.createEmptyMovieClip("halo",1);
   halo.beginGradientFill("radial",[0xFFFFFF,0x8FB4FF,0x8FB4FF],[40,18,0],[0,110,255],{matrixType:"box",x:100,y:-30,w:200,h:200,r:0});
   smRoundRect(halo,100,-30,200,200,0);
   halo.endFill();
   var s = 3131;
   for(i = 0; i < 9; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var hx = 112 + s % 176;
      s = (s * 1103515245 + 12345) % 2147483648;
      smSparkle(halo,hx,14 + s % 88,1 + s % 3,i % 3 == 0 ? 0xFFE08A : 0xFFFFFF,35 + s % 45);
   }

   // The squirrel peeks over the panel's top edge (the panel hides where his texture is cut off) and pops up
   // when the menu opens.
   var sq = f.attachMovie("sanctuarySystemSquirrel","squirrel",2);
   sq._width = 124;
   sq._height = 132;
   sq._x = 138;
   sq._y = 74;
   gs.TweenLite.to(sq,0.9,{_y:10,delay:0.12,ease:smElasticOut});

   f.bg = f.createEmptyMovieClip("bg",3);
   f.bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   smBottom = SM_PANEL_TOP + 150;
   smDrawPanel();

   smBuildHeader();
   smBuildExit();
   f.createEmptyMovieClip("back",6);
   smBuildClose();
   f.onEnterFrame = smUpdate;
}

function smDrawPanel()
{
   var bg = smFrame.bg;
   var h = Math.round(smBottom - SM_PANEL_TOP);
   bg.clear();
   smGradientRect(bg,SM_PANEL_X,SM_PANEL_TOP,SM_PANEL_W,h,30,0x2E4FB0,0x141F55,100,SM_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-140,y:SM_PANEL_TOP - 220,w:560,h:460,r:0});
   smRoundRect(bg,SM_PANEL_X + 2,SM_PANEL_TOP + 2,SM_PANEL_W - 4,h - 4,28);
   bg.endFill();
}

// The header band under the logo: sparkles, a line under the logo and a gold divider above the rows.
function smBuildHeader()
{
   var h = smFrame.createEmptyMovieClip("header",4);
   h.beginFill(0x0A1440,30);
   smRoundRect(h,SM_PANEL_X + 4,SM_PANEL_TOP + 4,SM_PANEL_W - 8,88,26);
   h.endFill();
   var s = 6161;
   var i;
   for(i = 0; i < 18; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 34 + s % (SM_PANEL_W - 40);
      s = (s * 1103515245 + 12345) % 2147483648;
      smSparkle(h,x,SM_PANEL_TOP + 12 + s % 70,1 + s % 3,0xFFFFFF,25 + s % 50);
   }
   h.lineStyle();
   h.beginGradientFill("linear",[SM_GOLD,SM_GOLD,SM_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:70,y:0,w:260,h:2,r:0});
   smRoundRect(h,70,SM_ROWS_Y - 16,260,2,1);
   h.endFill();
   smText(h,"caption_txt","Taking a break?",0,SM_PANEL_TOP + 63,400,22,14,SM_SOFT);
}

// --- the logo (setLocale_lua attaches it) -----------------------------------------------

// A little smaller, so the close button fits beside it in the header.
function smPlaceLogo()
{
   var logo = _root.bannerMC;
   logo._xscale = logo._yscale = 80;
   logo._x = 200;
   logo._y = SM_PANEL_TOP + 34;
   logo.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.6,8,8,1,2)];
}

var smSetLocale = setLocale_lua;
setLocale_lua = function(locale)
{
   var result = smSetLocale.apply(_root,arguments);
   smPlaceLogo();
   return result;
};

// --- rows ------------------------------------------------------------------------------

function smDrawCard(row, over)
{
   var art = row.smArt;
   var x = row.smX;
   var g = art.card;
   g.clear();
   smGradientRect(g,x,SM_CARD_TOP,SM_CARD_W,SM_CARD_H,18,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : SM_RIM,over ? 100 : 45,over ? 2 : 1.5);
   smGloss(g,x + 10,SM_CARD_TOP + 2,SM_CARD_W - 20,SM_CARD_H * 0.45,14,over ? 26 : 16);
   g.filters = over ? [new flash.filters.GlowFilter(SM_GOLD,0.75,10,10,2,2)] : smShadow();

   var cx = x + 31;
   var cy = SM_CARD_TOP + SM_CARD_H / 2;
   var sk = art.socket;
   sk.clear();
   smSocketCircle(sk,cx,cy,18,over ? 0xFFE08A : 0xFFFFFF,over ? 100 : 70,over ? 2 : 1.5);

   var ch = art.chevron;
   ch.clear();
   smChevron(ch,0,0,6,over ? 0xFFE08A : SM_SOFT,over ? 100 : 60,3,false);
}

function smHoverRow(row, over)
{
   smDrawCard(row,over);
   var art = row.smArt;
   // The row itself runs its entrance tween, so the lift moves its art and label instead.
   gs.TweenLite.to(art,0.3,{_y:over ? -2 : 0,ease:mx.transitions.easing.Strong.easeOut});
   gs.TweenLite.to(row.OptName,0.3,{_y:row.smLabelY - (over ? 2 : 0),ease:mx.transitions.easing.Strong.easeOut});
   gs.TweenLite.to(art.icon,0.8,{_rotation:over ? -10 : 0,_xscale:over ? 116 : 100,_yscale:over ? 116 : 100,ease:smElasticOut});
   gs.TweenLite.to(art.chevron,0.3,{_x:row.smChevronX + (over ? 4 : 0),ease:mx.transitions.easing.Strong.easeOut});
}

// Each root entry (a "treebutton": a click sends its id) becomes a glass row. The stock row's bg is its hit area;
// it's stretched over the card and stays invisible (the stock rollover shows it, so it's hidden again after).
function smStyleRow(row, id)
{
   if(row == undefined)
   {
      return;
   }
   var x = row.smX = SM_CARD_X - scrollerBaseX;
   var entry = SM_ENTRIES[id];
   row.div._visible = false;
   row.bullet._visible = false;
   row.Slider._visible = false;

   var bg = row.bg;
   var b = bg.getBounds(bg);
   if(b.xMax > b.xMin && b.yMax > b.yMin)
   {
      bg._xscale = SM_CARD_W / (b.xMax - b.xMin) * 100;
      bg._yscale = SM_CARD_H / (b.yMax - b.yMin) * 100;
      bg._x = x - b.xMin * bg._xscale / 100;
      bg._y = SM_CARD_TOP - b.yMin * bg._yscale / 100;
   }
   bg._alpha = 0;
   bg.smRow = row;
   bg.onRollOver = bg.onDragOver = function()
   {
      rollOverOption.call(this);
      this._alpha = 0;
      smHoverRow(this.smRow,true);
   };
   bg.onRollOut = bg.onDragOut = function()
   {
      rollOutOption.call(this);
      smHoverRow(this.smRow,false);
   };

   // Below the row's own clips: a single slot, so everything of ours lives in one clip.
   var art = row.smArt = row.createEmptyMovieClip("smArt",-16384);
   art.card = art.createEmptyMovieClip("card",1);
   art.socket = art.createEmptyMovieClip("socket",2);
   var icon = art.icon = art.createEmptyMovieClip("icon",3);
   icon._x = x + 31;
   icon._y = SM_CARD_TOP + SM_CARD_H / 2;
   if(entry != undefined)
   {
      var pic = icon.attachMovie(entry[0],"pic",1);
      pic._width = pic._height = 28;
      pic._x = pic._y = -14;
   }
   else
   {
      smStar(icon,0,0,12,0xF5A623,100);
   }
   var ch = art.chevron = art.createEmptyMovieClip("chevron",4);
   row.smChevronX = ch._x = x + SM_CARD_W - 20;
   ch._y = SM_CARD_TOP + SM_CARD_H / 2;

   // Lua's label, white with the dark outline, and a line about the entry under it.
   var label = row.OptName;
   var fmt = new TextFormat();
   fmt.color = 0xFFFFFF;
   fmt.size = 18;
   fmt.align = "left";
   label.setTextFormat(fmt);
   label.setNewTextFormat(fmt);
   label.textColor = 0xFFFFFF;
   label._x = x + 58;
   label._width = SM_CARD_W - 58 - 34;
   label.filters = smOutline();
   if(entry != undefined)
   {
      row.smLabelY = label._y = SM_CARD_TOP + 2;
      smText(art,"blurb_txt",entry[1],x + 60,SM_CARD_TOP + 23,SM_CARD_W - 60 - 34,20,12,SM_SOFT,"left");
   }
   else
   {
      row.smLabelY = label._y = SM_CARD_TOP + (SM_CARD_H - label._height) / 2;
   }

   smDrawCard(row,false);

   // In one after another as Lua adds them.
   row._alpha = 0;
   row._x = -14;
   gs.TweenLite.to(row,0.45,{_alpha:100,_x:0,delay:0.1 + smRows.length * 0.06,ease:mx.transitions.easing.Strong.easeOut});
   smRows.push(row);
}

var smBuildOptionTree = buildOptionTree;
buildOptionTree = function(objName, sName, buttonOnly)
{
   // Ask for Help is gone: never built, so the rows under it move up and the panel is one row shorter.
   if(objName == "addCSTicket")
   {
      return undefined;
   }
   var result = smBuildOptionTree.apply(_root,arguments);
   smStyleRow(Options[objName],objName);
   return result;
};

// --- Exit Game -------------------------------------------------------------------------

function smBuildExit()
{
   var p = smFrame.createEmptyMovieClip("exitPill",5);
   p._x = SM_CARD_X + SM_CARD_W / 2;
   p._alpha = 0;
   p.bg = p.createEmptyMovieClip("bg",1);
   var glyph = p.createEmptyMovieClip("glyph",2);
   var label = smText(p,"label_txt","Exit Game",0,- SM_EXIT_H / 2 + 10,200,30,20,0xFFFFFF,"left");
   label.filters = [new flash.filters.GlowFilter(0x5A0A14,0.9,3,3,4,2)];
   // The glyph and label centered together.
   var start = - (18 + 10 + label.textWidth) / 2;
   smPowerGlyph(glyph,start + 9,1,8,0xFFFFFF);
   glyph.filters = [new flash.filters.GlowFilter(0x5A0A14,0.9,3,3,4,2)];
   label._x = start + 28 - 2;
   smDrawExit(false);

   var exit = buttonholder.exit;
   var rollOver = exit.onRollOver;
   exit.onRollOver = function()
   {
      rollOver.apply(this,arguments);
      smHoverExit(true);
   };
   // selectOption calls this when it opens the confirmation.
   exit.onRollOut = exit.onDragOut = function()
   {
      smHoverExit(false);
   };
   var release = exit.onRelease;
   exit.onRelease = function()
   {
      var result = release.apply(this,arguments);
      smStyleVerify();
      return result;
   };
}

function smDrawExit(over)
{
   var p = smFrame.exitPill;
   var g = p.bg;
   var w = SM_CARD_W;
   var h = SM_EXIT_H;
   g.clear();
   g.lineStyle(2,over ? 0xFFE3DC : 0xFFB8AC,100);
   g.beginGradientFill("linear",over ? [0xFF7A62,0xD01F36] : [0xF2563F,0xA8122B],[100,100],[0,255],{matrixType:"box",x:- w / 2,y:- h / 2,w:w,h:h,r:Math.PI / 2});
   smRoundRect(g,- w / 2,- h / 2,w,h,h / 2);
   g.endFill();
   smGloss(g,- w / 2 + 14,- h / 2 + 3,w - 28,h * 0.42,h * 0.2,over ? 50 : 40);
   p.filters = over ? [new flash.filters.GlowFilter(0xFF5A40,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x2A0610,0.6,8,8,1,2)];
}

function smHoverExit(over)
{
   smDrawExit(over);
   gs.TweenLite.to(smFrame.exitPill,over ? 0.7 : 0.25,{_xscale:over ? 104 : 100,_yscale:over ? 104 : 100,ease:over ? smElasticOut : mx.transitions.easing.Strong.easeOut});
}

// The "Are you sure?" panel selectOption slides in (mcExitVerify, attached fresh each time): its Yes and No
// buttons become invisible hit areas over a red and a glass pill. Its question is white already.
function smStyleVerify()
{
   var v = Options.exitverify;
   if(v == undefined || v.smStyled)
   {
      return;
   }
   v.smStyled = true;
   var art = v.createEmptyMovieClip("smArt",-16384);
   smVerifyButton(v.yes,art,"yes",51,"Yes",true);
   smVerifyButton(v.no,art,"no",161,"No",false);
}

// The stock button's hit shape spans 8..68 x 0..56; it's stretched over a 90x40 pill at x.
function smVerifyButton(b, art, name, x, label, danger)
{
   b._alpha = 0;
   b._xscale = 150;
   b._yscale = 40 / 56 * 100;
   b._x = x - 12;
   b._y = 70;
   var p = art.createEmptyMovieClip(name,art.getNextHighestDepth());
   p._x = x + 45;
   p._y = 90;
   p.danger = danger;
   p.bg = p.createEmptyMovieClip("bg",1);
   var t = smText(p,"label_txt",label,-45,-13,90,28,18,0xFFFFFF);
   t.filters = danger ? [new flash.filters.GlowFilter(0x5A0A14,0.9,3,3,4,2)] : smOutline();
   smDrawVerifyButton(p,false);

   var rollOver = b.onRollOver;
   b.onRollOver = function()
   {
      rollOver.apply(this,arguments);
      smHoverVerify(p,true);
   };
   b.onRollOut = b.onDragOut = function()
   {
      smHoverVerify(p,false);
   };
}

function smDrawVerifyButton(p, over)
{
   var g = p.bg;
   g.clear();
   if(p.danger)
   {
      g.lineStyle(2,over ? 0xFFE3DC : 0xFFB8AC,100);
      g.beginGradientFill("linear",over ? [0xFF7A62,0xD01F36] : [0xF2563F,0xA8122B],[100,100],[0,255],{matrixType:"box",x:-45,y:-20,w:90,h:40,r:Math.PI / 2});
   }
   else
   {
      g.lineStyle(2,over ? 0xFFE08A : SM_RIM,over ? 100 : 60);
      g.beginGradientFill("linear",over ? [0x3F66D6,0x1D307A] : [0x284A9E,0x162660],[100,100],[0,255],{matrixType:"box",x:-45,y:-20,w:90,h:40,r:Math.PI / 2});
   }
   smRoundRect(g,-45,-20,90,40,20);
   g.endFill();
   smGloss(g,-37,-18,74,16,8,over ? 45 : 32);
   var glow = p.danger ? 0xFF5A40 : SM_GOLD;
   p.filters = over ? [new flash.filters.GlowFilter(glow,0.8,14,14,2,2)] : smShadow();
}

function smHoverVerify(p, over)
{
   smDrawVerifyButton(p,over);
   gs.TweenLite.to(p,over ? 0.7 : 0.25,{_xscale:over ? 108 : 100,_yscale:over ? 108 : 100,ease:over ? smElasticOut : mx.transitions.easing.Strong.easeOut});
}

// --- back tab (shown while the confirmation is up) -------------------------------------

var smBackRollOver = btnBack.onRollOver;
btnBack.onRollOver = btnBack.onDragOver = function()
{
   smBackRollOver.apply(this,arguments);
   smBackOver = true;
};
var smBackRollOut = btnBack.onRollOut;
btnBack.onRollOut = btnBack.onDragOut = function()
{
   smBackRollOut.apply(this,arguments);
   smBackOver = false;
};

function smDrawBack(b)
{
   var g = smFrame.back;
   var x = b.xMin + 3;
   var w = Math.max(16,b.xMax - b.xMin - 6);
   var y = b.yMin + 3;
   var h = Math.max(30,b.yMax - b.yMin - 6);
   g.clear();
   smGradientRect(g,x,y,w,h,w / 2,smBackOver ? 0x3F66D6 : 0x284A9E,smBackOver ? 0x1D307A : 0x162660,100,smBackOver ? 0xFFE08A : SM_RIM,smBackOver ? 100 : 45,smBackOver ? 2 : 1.5);
   smGloss(g,x + 3,y + 3,w - 6,h * 0.3,w / 2 - 3,20);
   var cx = x + w / 2;
   var cy = y + h / 2;
   smChevron(g,cx - 3,cy,6,smBackOver ? 0xFFE08A : SM_GOLD,100,3,true);
   smChevron(g,cx + 4,cy,6,smBackOver ? 0xFFE08A : SM_GOLD,60,3,true);
   g.filters = smBackOver ? [new flash.filters.GlowFilter(SM_GOLD,0.7,12,12,2,2)] : smShadow();
}

// --- close button ----------------------------------------------------------------------

function smDrawClose(over)
{
   var c = smFrame.close;
   c.clear();
   c.lineStyle(1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-17,y:-17,w:34,h:34,r:Math.PI / 2});
   smCircle(c,0,0,17);
   c.endFill();
   smGloss(c,-11,-14,22,11,5,50);
   c.lineStyle(4,SM_INK,100);
   c.moveTo(-6,-6);
   c.lineTo(6,6);
   c.moveTo(6,-6);
   c.lineTo(-6,6);
   c.filters = over ? [new flash.filters.GlowFilter(SM_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

// Our own button: the stock X is hidden (it moves with the panel's bottom), and this does what it did.
function smBuildClose()
{
   var c = smFrame.createEmptyMovieClip("close",7);
   c._x = SM_CLOSE_X;
   c._y = SM_CLOSE_Y;
   smDrawClose(false);
   c.onRollOver = function()
   {
      fscommand("FR_event","PlaySound(DockOver)");
      smHoverClose(true);
   };
   c.onRollOut = c.onDragOut = c.onReleaseOutside = function()
   {
      smHoverClose(false);
   };
   c.onRelease = function()
   {
      fscommand("FR_event","PlaySound(DockSelect)");
      _root.returnToGame();
   };
}

function smHoverClose(over)
{
   smDrawClose(over);
   gs.TweenLite.to(smFrame.close,over ? 0.7 : 0.25,{_xscale:over ? 115 : 100,_yscale:over ? 115 : 100,_rotation:over ? 90 : 0,ease:over ? smElasticOut : mx.transitions.easing.Strong.easeOut});
}

// --- following the stock layout ---------------------------------------------------------

// Stock code tweens the Exit Game button, the mask and the back tab as rows are added and the confirmation
// slides in or out; the panel, the pill and the tab follow them each frame.
function smUpdate()
{
   var target;
   if(buttonholder._visible)
   {
      target = buttonholder._y + SM_EXIT_TOP + SM_EXIT_H + 20;
   }
   else
   {
      target = menuMask._y + menuMask._height + 20;
   }
   var d = target - smBottom;
   if(Math.abs(d) > 0.5)
   {
      smBottom += d * 0.35;
      smDrawPanel();
   }
   else if(d != 0)
   {
      smBottom = target;
      smDrawPanel();
   }

   var pill = smFrame.exitPill;
   pill._y = buttonholder._y + SM_EXIT_TOP + SM_EXIT_H / 2;
   var a = buttonholder._visible ? 100 : 0;
   pill._alpha += (a - pill._alpha) * 0.35;
   pill._visible = pill._alpha > 1;

   var back = smFrame.back;
   if(btnBack._visible && btnBack._alpha > 1)
   {
      var b = btnBack.getBounds(_root);
      var key = Math.round(b.yMin) + "," + Math.round(b.yMax) + "," + smBackOver;
      if(key != smBackKey)
      {
         smBackKey = key;
         smDrawBack(b);
      }
      back._alpha = btnBack._alpha;
      back._visible = true;
   }
   else
   {
      back._visible = false;
   }
}

smBuild();
// ===== End Sanctuary System Menu =====

// ===== Sanctuary Tile Browser =====
// Appended to Browser_V2's main timeline by build_client_mods.py.
// Redraws the tile browser (Games, Pets, Rides, Houses, and the farming browser's seeds and tools, which use the
// same movie) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass frame with the title
// and the mode's own icon (the movie's mc_icon* symbols) in the header, a gold close button, a gold pager and a
// gold Main Menu pill. Tiles become glass cards: game screenshots fill the top of the card, item icons sit a
// little bigger in a frosted socket, and the name moves to a strip under the art. Hovered cards lift with a gold
// glow, the selected one (the summoned pet, say) has a gold rim and check. The stock badges keep their roles
// (hit areas, tooltips, Lua events); their art is hidden. Station Cash and membership no longer exist, so the
// store tiles at the end of Pets, Rides and Houses are left out and nothing is greyed out as members only.

var TB_W = 1024;
var TB_H = 768;
var TB_GOLD = 0xFFC531;
var TB_RIM = 0x9DB8FF;
var TB_SOFT = 0xC9D6FF;
// The grid's recess, and where the pager and Main Menu pill sit under it.
var TB_GRID_Y = 84;
var TB_GRID_H = 544;
var TB_PAGER_Y = 638;
var TB_MENU_Y = 708;
var TB_CLOSE_X = 972;
var TB_CLOSE_Y = 48;

// The card inside each badge, in badge coordinates (the view draws badges at about 80%), and its center,
// which the hover scales about.
var TB_CARD_X = 6;
var TB_CARD_Y = 3;
var TB_CARD_W = 210;
var TB_CARD_H = 154;
var TB_CX = 111;
var TB_CY = 80;
// The name strip at the bottom of the card.
var TB_STRIP_Y = 126;
// The frosted socket, and the icon in it: stock icons are 64px, drawn at 100px by the stock badges.
var TB_SOCKET_X = 53;
var TB_SOCKET_Y = 7;
var TB_SOCKET = 116;
var TB_ICON_X = 55;
var TB_ICON_Y = 9;
var TB_ICON_SCALE = 175;
// Game screenshots (the stock 180x135 holder) fill the top of the card, cropped top and bottom.
var TB_PHOTO_X = 8;
var TB_PHOTO_Y = 5;
var TB_PHOTO_W = 206;
var TB_PHOTO_H = 121;

// Badge children that stay visible; everything else in a badge is stock art and is hidden.
var TB_KEEP = {icon:true,holder_mc:true,mysteryChestIcon_mc:true,canUpgrade_mc:true,isUpgraded_mc:true,lock_mc:true,levelBadge_mc:true};
// BrowserV2.modes -> the movie's own icon for the header.
var TB_MODE_ICONS = [undefined,"mc_iconPets","mc_iconGames","mc_iconMounts","mc_iconHouses","mc_iconSeeds","mc_iconTools"];

var tbFrame;

// --- drawing helpers -------------------------------------------------------------------

function tbRoundRect(mc, x, y, w, h, r)
{
   tbCornerRect(mc,x,y,w,h,r,r);
}

// A rectangle with its top corners rounded by rt and its bottom ones by rb.
function tbCornerRect(mc, x, y, w, h, rt, rb)
{
   mc.moveTo(x + rt,y);
   mc.lineTo(x + w - rt,y);
   mc.curveTo(x + w,y,x + w,y + rt);
   mc.lineTo(x + w,y + h - rb);
   mc.curveTo(x + w,y + h,x + w - rb,y + h);
   mc.lineTo(x + rb,y + h);
   mc.curveTo(x,y + h,x,y + h - rb);
   mc.lineTo(x,y + rt);
   mc.curveTo(x,y,x + rt,y);
}

function tbGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   tbRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function tbCircle(mc, x, y, r)
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

// A gold disc (close button, pager arrows, the selected card's check).
function tbGoldDisc(mc, x, y, r, over)
{
   mc.lineStyle(1.5,0xFFF3C4,100);
   mc.beginGradientFill("linear",over ? [0xFFF0B8,0xFFC531] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x - r,y:y - r,w:r * 2,h:r * 2,r:Math.PI / 2});
   tbCircle(mc,x,y,r);
   mc.endFill();
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:x - r,y:y - r,w:r * 2,h:r,r:Math.PI / 2});
   tbRoundRect(mc,x - r * 0.65,y - r + 3,r * 1.3,r * 0.7,r * 0.35);
   mc.endFill();
}

function tbText(mc, name, txt, x, y, w, h, size, color, align)
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
   t.baseSize = size;
   return t;
}

// Sets a field's text on one line, shrinking it to fit and cutting it short past the smallest size.
function tbSetFitted(t, txt, color)
{
   var f = t.getTextFormat();
   f.size = t.baseSize;
   f.color = color;
   t.setNewTextFormat(f);
   t.text = txt;
   while(t.textWidth > t._width - 8 && f.size > 12)
   {
      f.size -= 1;
      t.setTextFormat(f);
   }
   while(t.textWidth > t._width - 8 && txt.length > 3)
   {
      txt = txt.substr(0,txt.length - 1);
      t.text = txt + "...";
      t.setTextFormat(f);
   }
}

// Restyles a stock text field in place.
function tbRestyle(t, size, color, align)
{
   var f = t.getTextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = align;
   t.setNewTextFormat(f);
   t.setTextFormat(f);
   t.filters = tbOutline();
}

function tbSparkle(mc, x, y, r, alpha)
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

function tbOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// --- frame -----------------------------------------------------------------------------

// Drawn inside the browser clip, under its children, so it plays the stock open and close tweens with it.
function tbBuildFrame()
{
   browser.panel_mc._visible = false;
   tbFrame = browser.createEmptyMovieClip("sanctuaryFrame",-16384);
   tbFrame._x = - TB_W / 2;
   tbFrame._y = - TB_H / 2;

   var bg = tbFrame.createEmptyMovieClip("bg",1);
   tbGradientRect(bg,12,12,TB_W - 24,TB_H - 24,30,0x2E4FB0,0x141F55,100,TB_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-150,y:-300,w:900,h:700,r:0});
   tbRoundRect(bg,14,14,TB_W - 28,TB_H - 28,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   tbRoundRect(bg,16,16,TB_W - 32,TB_GRID_Y - 22,26);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   // Sparkles in the header (fixed seed, same every time).
   var s = 2468;
   var i;
   for(i = 0; i < 26; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 40 + s % (TB_W - 80);
      s = (s * 1103515245 + 12345) % 2147483648;
      tbSparkle(bg,x,24 + s % 44,1 + s % 3,25 + s % 50);
   }

   // A gold underline under the title.
   bg.beginGradientFill("linear",[TB_GOLD,TB_GOLD,TB_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:TB_W / 2 - 170,y:0,w:340,h:2,r:0});
   tbRoundRect(bg,TB_W / 2 - 170,70,340,2,1);
   bg.endFill();

   // The recess the tiles sit in.
   tbGradientRect(bg,44,TB_GRID_Y,TB_W - 88,TB_GRID_H,26,0x0A1440,0x0A1440,35,TB_RIM,22,1);

   tbFrame.header = tbFrame.createEmptyMovieClip("header",2);
   tbFrame.header._visible = false;
}

// The title in gold, centered in the header, with the mode's icon in a gold-rimmed disc beside it.
function tbStyleTitle()
{
   var t = browser.title;
   t._x = -300;
   t._y = 28 - TB_H / 2;
   t._width = 600;
   t._height = 40;
   tbRestyle(t,28,0xFFE08A,"center");
   // frSetTextPreserveWidth keeps each field's first size and bounds; it should keep these.
   delete TFDim[String(t)];
}

function tbUpdateHeader()
{
   var h = tbFrame.header;
   var symbol = TB_MODE_ICONS[browser.getMode()];
   if(symbol == undefined || browser.title.text.length == 0)
   {
      h._visible = false;
      return;
   }
   if(h.symbol != symbol)
   {
      h.symbol = symbol;
      h.clear();
      h.lineStyle(2,TB_GOLD,100);
      h.beginGradientFill("radial",[0x3F66D6,0x162660],[100,100],[0,255],{matrixType:"box",x:-30,y:-34,w:60,h:60,r:0});
      tbCircle(h,0,0,25);
      h.endFill();
      var icon = h.attachMovie(symbol,"icon",1);
      icon._xscale = icon._yscale = 70;
      icon._x = icon._y = -22.4;
      h.filters = [new flash.filters.GlowFilter(TB_GOLD,0.45,10,10,2,2)];
   }
   h._x = TB_W / 2 - browser.title.textWidth / 2 - 42;
   h._y = TB_CLOSE_Y - 2;
   h._visible = true;
}

// --- close button and Main Menu pill ---------------------------------------------------

// Both stand in for the stock buttons (now hidden) and do what they did.
function tbBuildButtons()
{
   exitButton_mc._visible = false;
   mainMenuButton._visible = false;

   var close = _root.createEmptyMovieClip("sanctuaryClose",_root.getNextHighestDepth());
   close._x = TB_CLOSE_X;
   close._y = TB_CLOSE_Y;
   close.art = close.createEmptyMovieClip("art",1);
   tbDrawClose(close,false);
   close.onRollOver = function()
   {
      tbDrawClose(this,true);
      fscommand("FR_event","PlaySound(DockOver)");
      gs.TweenLite.to(this,0.6,{_xscale:115,_yscale:115,ease:mx.transitions.easing.Elastic.easeOut});
   };
   close.onRollOut = close.onReleaseOutside = function()
   {
      tbDrawClose(this,false);
      gs.TweenLite.to(this,0.3,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
   };
   close.onRelease = function()
   {
      fscommand("FR_event","PlaySound(DockSelect)");
      exitButton_mc.onPress();
   };

   var menu = _root.createEmptyMovieClip("sanctuaryMainMenu",_root.getNextHighestDepth());
   menu._x = TB_W / 2;
   menu._y = TB_MENU_Y;
   menu.art = menu.createEmptyMovieClip("art",1);
   var label = tbText(menu,"label_txt","Main Menu",-90,-13,180,28,18,0x3A2400);
   label.filters = [new flash.filters.GlowFilter(0xFFF3C4,0.6,2,2,2,2)];
   tbDrawMenuPill(menu,false);
   menu.onRollOver = function()
   {
      tbDrawMenuPill(this,true);
      _onmainMenuButtonOver();
      gs.TweenLite.to(this,0.5,{_xscale:106,_yscale:106,ease:mx.transitions.easing.Elastic.easeOut});
   };
   menu.onRollOut = menu.onReleaseOutside = function()
   {
      tbDrawMenuPill(this,false);
      gs.TweenLite.to(this,0.3,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
   };
   menu.onPress = function()
   {
      gs.TweenLite.to(this,0.08,{_xscale:98,_yscale:98});
   };
   menu.onRelease = function()
   {
      gs.TweenLite.to(this,0.3,{_xscale:106,_yscale:106,ease:mx.transitions.easing.Strong.easeOut});
      _onmainMenuButtonClicked();
   };
}

function tbDrawClose(close, over)
{
   var a = close.art;
   a.clear();
   tbGoldDisc(a,0,0,17,over);
   a.lineStyle(3.5,0x3A2400,100);
   a.moveTo(-6,-6);
   a.lineTo(6,6);
   a.moveTo(6,-6);
   a.lineTo(-6,6);
   close.filters = over ? [new flash.filters.GlowFilter(TB_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.6,6,6,1,2)];
}

function tbDrawMenuPill(menu, over)
{
   var a = menu.art;
   a.clear();
   a.lineStyle(1.5,0xFFF3C4,100);
   a.beginGradientFill("linear",over ? [0xFFF0B8,0xFFC531] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-90,y:-20,w:180,h:40,r:Math.PI / 2});
   tbRoundRect(a,-90,-20,180,40,20);
   a.endFill();
   a.lineStyle();
   a.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[50,0],[0,255],{matrixType:"box",x:0,y:-18,w:10,h:18,r:Math.PI / 2});
   tbRoundRect(a,-82,-17,164,16,8);
   a.endFill();
   menu.filters = over ? [new flash.filters.GlowFilter(TB_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.6,8,8,1,2)];
}

// --- pager -----------------------------------------------------------------------------

// The stock PageController keeps its buttons (now invisible hit areas: previous on the left half, next on the
// right) and its "1 / 3" label; a navy capsule with gold arrow discs covers its art.
function tbBuildPager()
{
   var p = browser.pages;
   p._xscale = p._yscale = 80;
   p._x = -71;
   p._y = TB_PAGER_Y - TB_H / 2;
   p.previous._alpha = p.next._alpha = p.labelBackground._alpha = 0;
   // Above the stock capsule (a plain shape at depth 1) and the previous button, under the label.
   p.sanctuaryPager = p.createEmptyMovieClip("sanctuaryPager",3);
   p.sanctuaryPager.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,6,6,1,2)];

   p.label._x = 44;
   p.label._y = 9;
   p.label._width = 90;
   p.label._height = 28;
   tbRestyle(p.label,18,0xFFFFFF,"center");
   delete TFDim[String(p.label)];

   var arrows = new Object();
   arrows.state = function(type, data)
   {
      var pages = data.target._parent;
      if(type == "rollover" || type == "dragover" || type == "press")
      {
         pages.tbOver = data.target._name;
      }
      else if(type == "rollout" || type == "dragout" || type == "releaseoutside")
      {
         pages.tbOver = undefined;
      }
      tbDrawPager(pages);
   };
   p.previous.addListener(arrows);
   p.next.addListener(arrows);
   p.setText();
}

function tbDrawPager(p)
{
   var g = p.sanctuaryPager;
   if(g == undefined)
   {
      return;
   }
   g.clear();
   g.lineStyle(1.5,TB_RIM,45);
   g.beginGradientFill("linear",[0x1D307A,0x111C4F],[100,100],[0,255],{matrixType:"box",x:0,y:-1,w:180,h:46,r:Math.PI / 2});
   tbRoundRect(g,-2,-1,182,46,23);
   g.endFill();
   tbDrawArrow(g,22,p.value > p.min,p.tbOver == "previous",-1);
   tbDrawArrow(g,156,p.value < p.max,p.tbOver == "next",1);
}

function tbDrawArrow(g, x, enabled, over, dir)
{
   var y = 22;
   if(enabled)
   {
      tbGoldDisc(g,x,y,over ? 18 : 17,over);
      g.lineStyle(3.5,0x3A2400,100);
   }
   else
   {
      g.lineStyle(1.5,TB_RIM,40);
      g.beginFill(0x284A9E,60);
      tbCircle(g,x,y,17);
      g.endFill();
      g.lineStyle(3.5,TB_RIM,45);
   }
   g.moveTo(x - 3 * dir,y - 7);
   g.lineTo(x + 4 * dir,y);
   g.lineTo(x - 3 * dir,y + 7);
}

// --- cards -----------------------------------------------------------------------------

// Hides a badge's stock art: its frames (some unnamed, so they're found by depth as well as by name), the
// state plates, the selection glint and the members-only icons. _alpha, not _visible: the stock code toggles
// some of these, and the states clip is the hit area.
function tbHideStockArt(b)
{
   var d;
   for(d = -16383; d < -16284; d++)
   {
      var mc = b.getInstanceAtDepth(d);
      if(typeof mc == "movieclip" && !TB_KEEP[mc._name])
      {
         mc._alpha = 0;
      }
   }
   var name;
   for(name in b)
   {
      if(typeof b[name] == "movieclip" && b[name]._parent == b && !TB_KEEP[name])
      {
         b[name]._alpha = 0;
      }
   }
}

function tbStyleBadge(b, photo)
{
   if(b.sanctuaryCard)
   {
      return;
   }
   b.tbPhoto = photo;
   tbHideStockArt(b);
   b.label._visible = false;

   var card = b.sanctuaryCard = b.createEmptyMovieClip("sanctuaryCard",-16384);
   card.bg = card.createEmptyMovieClip("bg",1);
   card.name_txt = tbText(card,"name_txt","",TB_CARD_X + 6,TB_STRIP_Y + 2,TB_CARD_W - 12,28,18,0xFFFFFF);
   card.name_txt.filters = tbOutline();

   // Rounds off the art's corners (the screenshot is cropped to the top of the card).
   var mask = b.createEmptyMovieClip("sanctuaryArtMask",b.getNextHighestDepth());
   mask.beginFill(0,100);
   if(photo)
   {
      tbCornerRect(mask,TB_PHOTO_X,TB_PHOTO_Y,TB_PHOTO_W,TB_PHOTO_H,16,0);
      var scale = TB_PHOTO_W / 180;
      b.holder_mc._xscale = b.holder_mc._yscale = scale * 100;
      b.holder_mc._x = TB_PHOTO_X;
      b.holder_mc._y = TB_PHOTO_Y + (TB_PHOTO_H - 135 * scale) / 2;
      b.holder_mc.setMask(mask);
      b.mysteryChestIcon_mc._x = TB_PHOTO_X + TB_PHOTO_W - 46;
      b.mysteryChestIcon_mc._y = TB_PHOTO_Y + 4;
   }
   else
   {
      tbRoundRect(mask,TB_ICON_X,TB_ICON_Y,TB_SOCKET - 4,TB_SOCKET - 4,13);
      b.icon._x = TB_ICON_X;
      b.icon._y = TB_ICON_Y;
      b.icon._xscale = b.icon._yscale = TB_ICON_SCALE;
      b.icon.setMask(mask);
      // The press nudge (_correctPosition) tweens the icon back to where it started.
      b.origValues.icon = {x:TB_ICON_X,y:TB_ICON_Y};
      // Mount upgrade wings and the farming lock, on the socket's corners.
      b.canUpgrade_mc._x = b.isUpgraded_mc._x = TB_SOCKET_X - 18;
      b.canUpgrade_mc._y = b.isUpgraded_mc._y = TB_SOCKET_Y + TB_SOCKET - 46;
      b.lock_mc._x = TB_SOCKET_X + TB_SOCKET - 30;
      b.lock_mc._y = TB_SOCKET_Y + TB_SOCKET - 34;
      // Seed and tool counts, bottom left on the socket.
      var counts = [b.seedCount_txt,b.count_txt];
      var i;
      for(i = 0; i < counts.length; i++)
      {
         if(counts[i] != undefined)
         {
            counts[i]._x = TB_SOCKET_X + 6;
            counts[i]._y = TB_SOCKET_Y + TB_SOCKET - 30;
            counts[i]._width = 60;
            counts[i]._height = 26;
            tbRestyle(counts[i],18,0xFFFFFF,"left");
         }
      }
   }

   // The selected card's check, over the art.
   var chip = b.sanctuaryChip = b.createEmptyMovieClip("sanctuaryChip",b.getNextHighestDepth());
   tbGoldDisc(chip,TB_CARD_X + 15,TB_CARD_Y + 15,13,false);
   chip.lineStyle(3,0x3A2400,100);
   chip.moveTo(TB_CARD_X + 9,TB_CARD_Y + 15);
   chip.lineTo(TB_CARD_X + 13,TB_CARD_Y + 20);
   chip.lineTo(TB_CARD_X + 21,TB_CARD_Y + 10);
   chip.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];

   b.addListener(tbBadgeListener);
   tbDrawCard(b,false);
}

function tbDrawCard(b, over)
{
   var card = b.sanctuaryCard;
   if(card == undefined)
   {
      return;
   }
   var bg = card.bg;
   var sel = b.tbSelected == true;
   var gold = over || sel;
   var x = TB_CARD_X;
   var y = TB_CARD_Y;
   var w = TB_CARD_W;
   var h = TB_CARD_H;
   bg.clear();
   tbGradientRect(bg,x,y,w,h,18,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : (sel ? TB_GOLD : TB_RIM),gold ? 100 : 45,sel ? 3 : 2);
   // Glassy sheen across the top.
   bg.lineStyle();
   bg.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[over ? 22 : 14,0],[0,255],{matrixType:"box",x:0,y:y + 3,w:10,h:60,r:Math.PI / 2});
   tbRoundRect(bg,x + 3,y + 3,w - 6,60,15);
   bg.endFill();

   // The name strip.
   bg.beginFill(0x0A1440,over ? 30 : 45);
   tbCornerRect(bg,x + 2,TB_STRIP_Y,w - 4,y + h - TB_STRIP_Y - 2,0,16);
   bg.endFill();
   bg.lineStyle(1,gold ? 0xFFE08A : TB_RIM,gold ? 70 : 25);
   bg.moveTo(x + 12,TB_STRIP_Y);
   bg.lineTo(x + w - 12,TB_STRIP_Y);

   if(b.tbPhoto)
   {
      // Behind the screenshot while it loads.
      bg.lineStyle();
      bg.beginFill(0x0A1440,60);
      tbCornerRect(bg,TB_PHOTO_X,TB_PHOTO_Y,TB_PHOTO_W,TB_PHOTO_H,16,0);
      bg.endFill();
   }
   else
   {
      // Frosted white, so the stock icons (made for light plates) read well.
      bg.lineStyle(2,gold ? 0xFFE08A : 0xFFFFFF,gold ? 100 : 85);
      bg.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:TB_SOCKET_X - 14,y:TB_SOCKET_Y - 20,w:TB_SOCKET + 28,h:TB_SOCKET + 28,r:0});
      tbRoundRect(bg,TB_SOCKET_X,TB_SOCKET_Y,TB_SOCKET,TB_SOCKET,16);
      bg.endFill();
   }

   if(over)
   {
      bg.filters = [new flash.filters.GlowFilter(TB_GOLD,0.6,16,16,2,2)];
   }
   else if(sel)
   {
      bg.filters = [new flash.filters.GlowFilter(TB_GOLD,0.4,10,10,2,2)];
   }
   else
   {
      bg.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,8,8,1,2)];
   }
   b.sanctuaryChip._visible = sel;
   tbSetFitted(card.name_txt,card.name_txt.fullText == undefined ? "" : card.name_txt.fullText,sel ? 0xFFE08A : 0xFFFFFF);
}

// Lifts the badge, scaling it about the card's center. Its resting place is where the stock entrance tween
// (Badge.show) puts it, so taking over that tween is harmless.
function tbLift(b, scale, lift, time)
{
   if(b.tbHomeX == undefined)
   {
      b.tbHomeX = b._x;
   }
   var k = 1 - scale / 100;
   gs.TweenLite.to(b,time,{_xscale:scale,_yscale:scale,_x:b.tbHomeX + TB_CX * k,_y:TB_CY * k - lift,ease:mx.transitions.easing.Strong.easeOut});
}

var tbBadgeListener = new Object();
tbBadgeListener.state = function(type, data)
{
   var b = data.target;
   if(b.sanctuaryCard == undefined)
   {
      return;
   }
   switch(type)
   {
      case "rollover":
      case "dragover":
         tbDrawCard(b,true);
         tbLift(b,104,5,0.3);
         break;
      case "rollout":
      case "dragout":
      case "releaseoutside":
         tbDrawCard(b,false);
         tbLift(b,100,0,0.3);
         break;
      case "press":
         tbLift(b,101,2,0.08);
         break;
      case "release":
         tbLift(b,104,5,0.25);
   }
};

function tbPatchBadges()
{
   // [class, art is a screenshot]
   var badges = [[fr.badges.GameBadge,true],[fr.badges.PetBadge,false],[fr.badges.MountBadge,false],[fr.badges.EmptyBadge,false],[fr.badges.SeedsBadge,false],[fr.badges.FarmToolBadge,false],[fr.badges.InteractionBadge,false]];
   var i;
   for(i = 0; i < badges.length; i++)
   {
      tbWrapSetItem(badges[i][0].prototype,badges[i][1]);
   }
   var bases = [fr.core.Badge.prototype,fr.core.Badge2.prototype];
   for(i = 0; i < bases.length; i++)
   {
      tbWrapBase(bases[i]);
   }
}

function tbWrapSetItem(proto, photo)
{
   var setItem = proto.setItem;
   proto.setItem = function(obj)
   {
      // Before the stock code, so the counts are fitted to their new bounds.
      tbStyleBadge(this,photo);
      setItem.call(this,obj);
      // Membership no longer exists; the members-only icon also opened the membership page.
      this.membersOnlyIcon._visible = this.membersOnly_mc._visible = this.membership_mc._visible = false;
      this.sanctuaryCard.name_txt.fullText = obj.name == undefined ? "" : String(obj.name);
      tbDrawCard(this,false);
   };
}

function tbWrapBase(proto)
{
   var show = proto.show;
   proto.show = function(width, column, row, direction)
   {
      this.tbHomeX = width * column;
      show.call(this,width,column,row,direction);
   };
   // The selected badge is deactivated (the summoned pet, the current mount).
   var activate = proto.activate;
   proto.activate = function()
   {
      activate.call(this);
      this.tbSelected = false;
      tbDrawCard(this,false);
   };
   var deactivate = proto.deactivate;
   proto.deactivate = function()
   {
      deactivate.call(this);
      this.tbSelected = true;
      tbDrawCard(this,false);
   };
}

// --- Station Cash and membership -------------------------------------------------------

// Pets, Rides and Houses end with a store tile (ID -1) that opened the Station Cash marketplace. The farming
// browser's -1 tiles stay: they're its own seed and tool flows.
var tbAddItem = BrowserV2.prototype.addItem;
BrowserV2.prototype.addItem = function(item)
{
   var mode = this.getMode();
   if(Number(item.ID) == -1 && mode >= 1 && mode <= 4)
   {
      return;
   }
   tbAddItem.call(this,item);
};

var tbSetMode = BrowserV2.prototype.setMode;
BrowserV2.prototype.setMode = function(value)
{
   tbSetMode.call(this,value);
   tbUpdateHeader();
};

var tbSetText = BrowserV2.prototype.setText;
BrowserV2.prototype.setText = function(text, subTextString)
{
   tbSetText.call(this,text,subTextString);
   tbUpdateHeader();
};

var tbPagerSetText = fr.display.PageController.prototype.setText;
fr.display.PageController.prototype.setText = function()
{
   tbPagerSetText.call(this);
   tbDrawPager(this);
};

// Everything members-only is everyone's: nothing is greyed out or sent to the membership page.
_global.isMember = true;
setMemberStatus = function(value)
{
   _global.isMember = true;
};

// The store banners' handler: only the farming one (the coin store's farming page) is left.
onClick = function(btn)
{
   if(banner.getMode() == 5 || banner.getMode() == 6)
   {
      fscommand("FR_event","gotoMarketplaceBanner(farming)");
   }
};

tbPatchBadges();
tbBuildFrame();
tbStyleTitle();
tbBuildPager();
tbBuildButtons();
// Centers the grid in its recess.
browser.view._x = -458.7;
// ===== End Sanctuary Tile Browser =====

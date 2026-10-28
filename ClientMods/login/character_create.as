// ===== Sanctuary Character Create =====
// Appended to charCreate's main timeline by build_client_mods.py.
// Restyles the character creator (the customizing step between choosing a type and a name) in the Main Menu's
// style (ClientMods/MyStuffBrowser/main_menu.as) without moving anything: the stock blue bezel around the 3D view
// (a plain shape, dropped at build time with REMOVED_DEPTHS) becomes a navy glass bezel with a gold inner rim and a
// hanging "Create Your Hero" title tab, the eight category buttons become glass cards (the category in gold over
// the current choice, its icon in a frosted socket; the open one rimmed in gold with a pointer toward its fan), the
// fanned option tiles become frosted sockets with a gold rim on the chosen one and slightly bigger art, the fan
// arrows turn gold, and Back / Randomize / Next become a glass pill, a gold-rimmed glass pill and a gold pill.
// Everything stock keeps its role: the hidden plates and buttons are the hit areas, and every click still goes
// through the stock handlers (categoryButtonClick, itemSelect, randomize, backButtonClick, nextButtonClick).
// The 3D view in the middle is left clear. Nothing here needs the world to be loaded.

var CC_W = 1024;
var CC_H = 768;
// The window onto the 3D character, inside the bezel.
var CC_VIEW_X = 22;
var CC_VIEW_Y = 20;
var CC_VIEW_W = 980;
var CC_VIEW_H = 632;
// A category card, in catButton coordinates (the stock plate is at 3,14 and its icon 48px at 4,20).
var CC_CARD_X = -2;
var CC_CARD_Y = -8;
var CC_CARD_W = 180;
var CC_CARD_H = 84;
// The fan's option tiles: the stock icon (48px at -373,-24) is shown at 112% about the plate's center.
var CC_TILE_X = -349;
var CC_TILE_Y = -1;
var CC_TILE_ICON = 112;
var CC_GOLD = 0xFFC531;
var CC_RIM = 0x9DB8FF;
var CC_SOFT = 0xC9D6FF;
var CC_INK = 0x3A2400;

var ccFrame;

// --- drawing helpers -------------------------------------------------------------------

function ccRoundRect(mc, x, y, w, h, r)
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

function ccGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   ccRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function ccGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   ccRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function ccCircle(mc, x, y, r)
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

// Frosted sockets behind item art: the stock icons are made for light plates.
function ccSocket(mc, x, y, w, h, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - w * 0.18,y:y - h * 0.24,w:w * 1.36,h:h * 1.36,r:0});
   ccRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function ccSparkle(mc, x, y, r, alpha)
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

function ccText(mc, name, txt, x, y, w, h, size, color, align)
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

// Sets a field's text, shrinking its font until it fits on one line.
function ccSetFitted(t, txt)
{
   var f = t.getTextFormat();
   f.size = t.baseSize;
   t.setNewTextFormat(f);
   t.text = txt == undefined ? "" : txt;
   while(t.textWidth > t._width - 6 && f.size > 10)
   {
      f.size -= 1;
      t.setTextFormat(f);
   }
}

function ccSetColor(t, color)
{
   var f = t.getTextFormat();
   f.color = color;
   t.setNewTextFormat(f);
   t.setTextFormat(f);
}

function ccOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// The bezel: the whole stage less a rounded window, as one contour (outer clockwise, window counter-clockwise),
// so the window is a hole whatever fill rule the renderer uses.
function ccBezel(mc, x, y, w, h, r)
{
   mc.moveTo(0,0);
   mc.lineTo(CC_W,0);
   mc.lineTo(CC_W,CC_H);
   mc.lineTo(0,CC_H);
   mc.lineTo(0,0);
   mc.lineTo(x,y + r);
   mc.lineTo(x,y + h - r);
   mc.curveTo(x,y + h,x + r,y + h);
   mc.lineTo(x + w - r,y + h);
   mc.curveTo(x + w,y + h,x + w,y + h - r);
   mc.lineTo(x + w,y + r);
   mc.curveTo(x + w,y,x + w - r,y);
   mc.lineTo(x + r,y);
   mc.curveTo(x,y,x,y + r);
   mc.lineTo(0,0);
}

// Gold, keeping the art's shading (multiplies plus offsets per channel; Color can't mix channels).
function ccTintGold(mc)
{
   new Color(mc).setTransform({ra:30,rb:200,ga:40,gb:120,ba:10,bb:20,aa:100,ab:0});
}

// --- frame -----------------------------------------------------------------------------

function ccBuildFrame()
{
   var f = ccFrame = _root.createEmptyMovieClip("sanctuaryCharacterCreate",-16384);
   var bg = f.createEmptyMovieClip("bg",1);
   bg.lineStyle();
   bg.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:0,y:0,w:CC_W,h:CC_H,r:Math.PI / 2});
   ccBezel(bg,CC_VIEW_X,CC_VIEW_Y,CC_VIEW_W,CC_VIEW_H,34);
   bg.endFill();
   // The rims: pale blue round the outside, gold (over a dark line) round the window.
   bg.lineStyle(3,CC_RIM,80);
   ccRoundRect(bg,2,2,CC_W - 4,CC_H - 4,22);
   bg.lineStyle(4,0x050A24,50);
   ccRoundRect(bg,CC_VIEW_X - 1,CC_VIEW_Y - 1,CC_VIEW_W + 2,CC_VIEW_H + 2,35);
   bg.lineStyle(2,CC_GOLD,85);
   ccRoundRect(bg,CC_VIEW_X - 2,CC_VIEW_Y - 2,CC_VIEW_W + 4,CC_VIEW_H + 4,36);

   // The dock under the window: a soft highlight, sparkles and the tray the buttons sit in.
   var dockY = CC_VIEW_Y + CC_VIEW_H + 4;
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[30,0],[0,255],{matrixType:"box",x:CC_W / 2 - 420,y:dockY - 60,w:840,h:220,r:0});
   ccRoundRect(bg,8,dockY,CC_W - 16,CC_H - dockY - 8,20);
   bg.endFill();
   var s = 3131;
   var i;
   for(i = 0; i < 22; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 30 + s % (CC_W - 60);
      s = (s * 1103515245 + 12345) % 2147483648;
      if(x > 250 && x < 774)
      {
         continue;
      }
      ccSparkle(bg,x,dockY + 16 + s % 80,1 + s % 3,25 + s % 50);
   }
   bg.lineStyle(1,CC_RIM,30);
   bg.beginFill(0x0A1440,45);
   ccRoundRect(bg,250,dockY + 14,524,80,40);
   bg.endFill();

   // The title tab hanging into the top of the window.
   var tab = f.createEmptyMovieClip("tab",2);
   ccGradientRect(tab,CC_W / 2 - 170,-24,340,74,26,0x2E4FB0,0x1A2A70,100,CC_RIM,80,2);
   tab.lineStyle();
   tab.beginGradientFill("linear",[CC_GOLD,CC_GOLD,CC_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:CC_W / 2 - 120,y:0,w:240,h:2,r:0});
   ccRoundRect(tab,CC_W / 2 - 120,42,240,2,1);
   tab.endFill();
   for(i = 0; i < 8; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var tx = CC_W / 2 - 150 + s % 300;
      s = (s * 1103515245 + 12345) % 2147483648;
      ccSparkle(tab,tx,6 + s % 32,1 + s % 2,25 + s % 45);
   }
   tab.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,10,10,1,2)];
   var title = ccText(tab,"title_txt","Create Your Hero",CC_W / 2 - 170,2,340,40,26,0xFFE08A);
   title.filters = ccOutline();
}

// --- category cards --------------------------------------------------------------------

function ccDrawCat(btn)
{
   var c = btn.sanctuaryCard;
   var sel = btn.m_isSelected;
   var over = btn.sanctuaryOver;
   var x = CC_CARD_X;
   var y = CC_CARD_Y;
   var w = CC_CARD_W;
   var h = CC_CARD_H;
   var g = c.bg;
   g.clear();
   if(sel)
   {
      ccGradientRect(g,x,y,w,h,20,over ? 0x4A72E0 : 0x3F66D6,0x1D307A,100,CC_GOLD,100,3);
      g.lineStyle();
      g.beginGradientFill("radial",[CC_GOLD,CC_GOLD],[36,0],[0,255],{matrixType:"box",x:x - 50,y:y - 40,w:170,h:h + 80,r:0});
      ccRoundRect(g,x + 2,y + 2,w - 4,h - 4,18);
      g.endFill();
      // A gold pointer toward the open fan, on the side facing the middle.
      var px = btn.getIsLeftSide() ? x + w + 1 : x - 1;
      var dir = btn.getIsLeftSide() ? 1 : -1;
      g.lineStyle(1,0xFFF3C4,80);
      g.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:px - 10,y:y + h / 2 - 10,w:20,h:20,r:Math.PI / 2});
      g.moveTo(px,y + h / 2 - 10);
      g.lineTo(px + dir * 11,y + h / 2);
      g.lineTo(px,y + h / 2 + 10);
      g.lineTo(px,y + h / 2 - 10);
      g.endFill();
   }
   else
   {
      ccGradientRect(g,x,y,w,h,20,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : CC_RIM,over ? 100 : 45,over ? 2 : 1.5);
   }
   ccGloss(g,x + 4,y + 2,w - 8,h * 0.4,16,16);
   // The icon's socket (the stock icon is 48px at 4,20, and nudges down 2px on hover).
   ccSocket(g,1,17,54,54,13,sel ? CC_GOLD : 0xFFFFFF,sel ? 100 : 85,sel ? 3 : 2);

   ccSetColor(c.title_txt,sel ? 0xFFE08A : CC_SOFT);
   if(sel)
   {
      c.filters = [new flash.filters.GlowFilter(CC_GOLD,over ? 0.7 : 0.5,16,16,2,2)];
   }
   else if(over)
   {
      c.filters = [new flash.filters.GlowFilter(CC_GOLD,0.5,12,12,2,2)];
   }
   else
   {
      c.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
   }
}

// The card goes over the stock plates (now invisible hit areas) and the frosted shelf behind them, and under the
// icons, which are moved up above it. A drawn clip with no mouse handlers doesn't take clicks.
function ccStyleCat(btn)
{
   btn.up_mc._alpha = btn.over_mc._alpha = btn.down_mc._alpha = btn.sel_mc._alpha = 0;
   var c = btn.sanctuaryCard = btn.createEmptyMovieClip("sanctuaryCard",btn.getNextHighestDepth());
   c.bg = c.createEmptyMovieClip("bg",1);
   var title = ccText(c,"title_txt",btn.catTitle_txt.text,CC_CARD_X + 10,CC_CARD_Y + 3,CC_CARD_W - 20,22,14,CC_SOFT,"left");
   title.filters = ccOutline();
   var name = ccText(c,"name_txt",btn.up_mc.textField_txt.text,62,30,CC_CARD_W - 72,40,16,0xFFFFFF,"left");
   name.multiline = true;
   name.wordWrap = true;
   name.filters = ccOutline();
   btn.catTitle_txt._visible = false;
   btn.m_icon_up.swapDepths(btn.getNextHighestDepth());
   btn.m_icon_over.swapDepths(btn.getNextHighestDepth());
   ccDrawCat(btn);
}

// The current choice's name, up to two lines, shrunk until it fits.
function ccSetCatName(btn, str)
{
   var t = btn.sanctuaryCard.name_txt;
   var f = t.getTextFormat();
   f.size = 16;
   t.setNewTextFormat(f);
   t.text = str == undefined ? "" : str;
   while(t.textHeight > 38 && f.size > 10)
   {
      f.size -= 1;
      t.setTextFormat(f);
   }
   t._y = 30 + Math.max(0,(38 - t.textHeight) / 2) - 4;
}

function ccWrapCatState(name, over)
{
   var stock = FRIconButton.prototype[name];
   CharCreateCatButton.prototype[name] = function()
   {
      stock.apply(this,arguments);
      if(this.sanctuaryCard && this.sanctuaryOver != over)
      {
         this.sanctuaryOver = over;
         ccDrawCat(this);
      }
   };
}

function ccPatchCats()
{
   var proto = CharCreateCatButton.prototype;
   ccWrapCatState("setStateUp",false);
   ccWrapCatState("setStateOver",true);
   ccWrapCatState("setStateDown",true);

   var setSelected = proto.setSelected;
   proto.setSelected = function(bool)
   {
      setSelected.call(this,bool);
      if(this.sanctuaryCard)
      {
         ccDrawCat(this);
      }
   };
   var setCategoryTitle = proto.setCategoryTitle;
   proto.setCategoryTitle = function(str)
   {
      setCategoryTitle.call(this,str);
      if(this.sanctuaryCard)
      {
         ccSetFitted(this.sanctuaryCard.title_txt,str);
         ccSetColor(this.sanctuaryCard.title_txt,this.m_isSelected ? 0xFFE08A : CC_SOFT);
      }
   };
   // setItem (and setTint) name the current choice through this.
   var setTextToFieldWidth = proto.setTextToFieldWidth;
   proto.setTextToFieldWidth = function(str)
   {
      setTextToFieldWidth.call(this,str);
      if(this.sanctuaryCard)
      {
         ccSetCatName(this,str);
      }
   };

   var i;
   for(i = 0; i < catButtonArray.length; i++)
   {
      ccStyleCat(catButtonArray[i]);
   }
   // The stock starburst behind the open category; its card glows gold instead.
   catButtonGlow_mc._visible = false;
}

// --- the fans' option tiles ------------------------------------------------------------

function ccDrawTile(item)
{
   var s = item.sanctuarySocket;
   var sel = item.sel_mc._visible;
   var over = item.sanctuaryOver;
   s.clear();
   s.lineStyle();
   s.beginFill(0x050A24,30);
   ccRoundRect(s,-31,-28,62,62,16);
   s.endFill();
   ccSocket(s,-31,-31,62,62,16,sel ? CC_GOLD : (over ? 0xFFE08A : 0xFFFFFF),sel || over ? 100 : 85,sel ? 3.5 : 2);
   ccGloss(s,-26,-29,52,22,10,55);
   s._xscale = s._yscale = item.sanctuaryDown ? 95 : 100;
   if(sel)
   {
      s.filters = [new flash.filters.GlowFilter(CC_GOLD,0.8,16,16,2,2)];
   }
   else if(over)
   {
      s.filters = [new flash.filters.GlowFilter(0xFFE08A,0.6,12,12,2,2)];
   }
   else
   {
      s.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,6,6,1,2)];
   }
}

function ccStyleTile(item)
{
   item.up_mc._alpha = item.over_mc._alpha = item.down_mc._alpha = item.sel_mc._alpha = 0;
   var s = item.sanctuarySocket = item.createEmptyMovieClip("sanctuarySocket",-16384);
   s._x = CC_TILE_X;
   s._y = CC_TILE_Y;
   // A little bigger, about the plate's center. Set before the art loads: the loader sizes it to the holder.
   var size = 48 * CC_TILE_ICON / 100;
   item.m_icon_up._xscale = item.m_icon_up._yscale = item.m_icon_over._xscale = item.m_icon_over._yscale = CC_TILE_ICON;
   item.m_icon_up._x = item.m_icon_over._x = CC_TILE_X - size / 2;
   item.m_icon_up._y = item.m_icon_over._y = CC_TILE_Y - size / 2;
}

function ccWrapTileState(name, over, down)
{
   var stock = FRIconButton.prototype[name];
   VertListItem.prototype[name] = function()
   {
      stock.apply(this,arguments);
      if(this.sanctuarySocket)
      {
         this.sanctuaryOver = over;
         this.sanctuaryDown = down;
         ccDrawTile(this);
      }
   };
}

function ccPatchTiles()
{
   var proto = VertListItem.prototype;
   var setItem = proto.setItem;
   proto.setItem = function(itemData)
   {
      if(!this.sanctuarySocket)
      {
         ccStyleTile(this);
      }
      setItem.call(this,itemData);
      // setIconByIndex puts the art back at the stock height; keep it centered on the socket.
      this.setIconY(CC_TILE_Y - 48 * CC_TILE_ICON / 200,0);
      ccDrawTile(this);
   };
   var setSelected = proto.setSelected;
   proto.setSelected = function(bool)
   {
      setSelected.call(this,bool);
      if(this.sanctuarySocket)
      {
         ccDrawTile(this);
      }
   };
   ccWrapTileState("setStateUp",false,false);
   ccWrapTileState("setStateOver",true,false);
   ccWrapTileState("setStateDown",true,true);
}

// The fans' scroll arrows, gold.
function ccStyleArrows()
{
   var lists = [m_leftList,m_rightList];
   var i;
   for(i = 0; i < lists.length; i++)
   {
      ccTintGold(lists[i].topArrow_mc);
      ccTintGold(lists[i].bottomArrow_mc);
   }
}

// --- Back / Randomize / Next -----------------------------------------------------------

// Fits a stock button (an invisible hit area from now on) over the pill drawn for it.
function ccCoverButton(btn, x, y, w, h)
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

// kind: "gold" (Next), "glass" (Back) or "rimmed" (Randomize, glass with a gold rim).
function ccDrawPill(pill, over, down)
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
      var rim = pill.kind == "rimmed" ? CC_GOLD : (over ? 0xFFE08A : CC_RIM);
      g.lineStyle(pill.kind == "rimmed" ? 2 : 1.5,rim,pill.kind == "rimmed" || over ? 100 : 60);
      g.beginGradientFill("linear",over ? [0x3F66D6,0x1D307A] : [0x284A9E,0x162660],[100,100],[0,255],{matrixType:"box",x:- w / 2,y:- h / 2,w:w,h:h,r:Math.PI / 2});
   }
   ccRoundRect(g,- w / 2,- h / 2,w,h,h / 2);
   g.endFill();
   ccGloss(g,- w / 2 + 8,- h / 2 + 2,w - 16,h * 0.42,h * 0.2,pill.kind == "gold" ? 55 : 25);
   pill.body._y = down ? 2 : 0;
   if(over)
   {
      pill.filters = [new flash.filters.GlowFilter(CC_GOLD,0.8,16,16,2,2)];
   }
   else
   {
      pill.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
   }
}

function ccMakePill(btn, name, kind, label, glyph)
{
   var b = btn.getBounds(_root);
   var cx = b.xMax > b.xMin ? Math.round((b.xMin + b.xMax) / 2) : 512;
   var cy = b.yMax > b.yMin ? Math.round((b.yMin + b.yMax) / 2) : 704;
   var w = 144;
   var h = 48;
   var pill = ccFrame.createEmptyMovieClip(name,ccFrame.getNextHighestDepth());
   pill._x = cx;
   pill._y = cy;
   pill.kind = kind;
   pill.w = w;
   pill.h = h;
   pill.btn = btn;
   pill.bg = pill.createEmptyMovieClip("bg",1);
   pill.body = pill.createEmptyMovieClip("body",2);
   var color = kind == "gold" ? CC_INK : 0xFFFFFF;
   // The label leaves room for the glyph on the side it points to.
   var shift = glyph == "left" ? 10 : (glyph == "right" ? -10 : 10);
   var t = ccText(pill.body,"label_txt",label,- w / 2 + shift,-14,w,30,19,color);
   if(kind != "gold")
   {
      t.filters = ccOutline();
   }
   var gl = pill.body.createEmptyMovieClip("glyph",pill.body.getNextHighestDepth());
   var gc = kind == "gold" ? CC_INK : 0xFFE08A;
   gl.lineStyle(3.5,gc,100);
   if(glyph == "left")
   {
      gl._x = shift - t.textWidth / 2 - 14;
      gl.moveTo(5,-7);
      gl.lineTo(-2,0);
      gl.lineTo(5,7);
   }
   else if(glyph == "right")
   {
      gl._x = shift + t.textWidth / 2 + 14;
      gl.moveTo(-5,-7);
      gl.lineTo(2,0);
      gl.lineTo(-5,7);
   }
   else
   {
      // A die showing five.
      gl._x = shift - t.textWidth / 2 - 16;
      gl.lineStyle(2,gc,100);
      gl.beginFill(gc,20);
      ccRoundRect(gl,-8,-8,16,16,4);
      gl.endFill();
      gl.lineStyle();
      gl.beginFill(gc,100);
      ccCircle(gl,-3.5,-3.5,1.6);
      ccCircle(gl,3.5,-3.5,1.6);
      ccCircle(gl,0,0,1.6);
      ccCircle(gl,-3.5,3.5,1.6);
      ccCircle(gl,3.5,3.5,1.6);
      gl.endFill();
   }
   ccCoverButton(btn,cx - w / 2,cy - h / 2,w,h);
   ccDrawPill(pill,false,false);

   // Dragging off a pressed button only resets its art (no hover-out event), so the pill hears it here.
   btn.onDragOut = function()
   {
      FRIconButton.prototype.onDragOut.call(this);
      ccHoverPill(this,false);
   };
   return pill;
}

function ccBuildButtons()
{
   ccFrame.back = ccMakePill(leftButton_mc,"back","glass","Back","left");
   ccFrame.random = ccMakePill(middleButton_mc,"random","rimmed","Randomize","die");
   ccFrame.next = ccMakePill(rightButton_mc,"next","gold","Next","right");
}

function ccPillFor(btn)
{
   if(btn == leftButton_mc)
   {
      return ccFrame.back;
   }
   if(btn == middleButton_mc)
   {
      return ccFrame.random;
   }
   if(btn == rightButton_mc)
   {
      return ccFrame.next;
   }
   return undefined;
}

function ccHoverPill(btn, over)
{
   var pill = ccPillFor(btn);
   if(pill == undefined)
   {
      return;
   }
   ccDrawPill(pill,over,false);
   gs.TweenLite.to(pill,over ? 0.7 : 0.25,{_xscale:over ? 106 : 100,_yscale:over ? 106 : 100,ease:over ? mx.transitions.easing.Elastic.easeOut : mx.transitions.easing.Strong.easeOut});
}

// The buttons (and the category cards, which ccPillFor ignores) broadcast these to _root, their listener.
function onHoverOver(btn)
{
   ccHoverPill(btn,true);
}

function onHoverOut(btn)
{
   ccHoverPill(btn,false);
}

function onButtonPress(btn)
{
   var pill = ccPillFor(btn);
   if(pill != undefined)
   {
      ccDrawPill(pill,true,true);
   }
}

function onButtonRelease(btn)
{
   var pill = ccPillFor(btn);
   if(pill != undefined)
   {
      ccDrawPill(pill,btn.hitTest(_root._xmouse,_root._ymouse,true),false);
   }
}

// --- build -----------------------------------------------------------------------------

ccBuildFrame();
ccPatchCats();
ccPatchTiles();
ccStyleArrows();
ccBuildButtons();
// ===== End Sanctuary Character Create =====

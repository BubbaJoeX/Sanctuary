// ===== Sanctuary Jobs =====
// Appended to FRJobBrowser's main timeline by build_client_mods.py.
// Redraws the Jobs window (the Main Menu's Jobs tile, GotoMyJobs) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass frame with a gold title, close button and Main Menu pill,
// and the jobs as glass cards in a recessed panel. Each card keeps the job's own crest and icon (the stock image set
// art, a little smaller and with a soft glow), with the name under it and a gold level badge beside a gold XP bar.
// Hovered cards lift with a gold glow and the icon pops; the current job (Lua's SelectItem) is rimmed in gold with a
// Current tab, and the header shows its icon, name and level. The line under the panel describes the hovered job.
// The stock frame is a plain shape inside the window, which ActionScript can't hide: a navy cover sits on it and a
// mask trims its vines. The stock renderers, buttons and scrollbar keep their roles (clicks, SelectJob, Main Menu,
// wheel and drag scrolling) with their art hidden. Membership no longer exists, so the Members Only badge is gone.

var JB_W = 1024;
var JB_H = 768;
var JB_HEADER_H = 88;
// The recessed panel the cards sit in, and where the stock list goes so the grid and scrollbar center in it
// (the ViewStack's container is counter-scaled to 100%; the list sits 5px into the jobs view).
var JB_PANEL_X = 44;
var JB_PANEL_Y = 112;
var JB_PANEL_W = 936;
var JB_PANEL_H = 500;
var JB_VIEW_X = 55;
var JB_VIEW_Y = 121;
// A card inside its renderer (212x150 unscaled, in a 215x160 cell), its crest and job icon, name and XP row.
var JB_CARD_X = 4;
var JB_CARD_Y = 2;
var JB_CARD_W = 204;
var JB_CARD_H = 146;
var JB_CREST_CX = 106;
var JB_CREST_CY = 46;
// The stock crest box is 172x86 at (21,6) with the 64px icon 2px left of its center: 85% here.
var JB_CREST_SCALE = 85;
var JB_CREST_W = 146.2;
var JB_CREST_H = 73.1;
var JB_ICON_CX = 104.3;
var JB_ICON_SIZE = 54.4;
var JB_ICON_HOVER = 61;
var JB_NAME_Y = 85;
var JB_BAR_X = 50;
var JB_BAR_Y = 120;
var JB_BAR_W = 146;
var JB_BAR_H = 14;
var JB_BADGE_X = 29;
var JB_BADGE_Y = 127;
var JB_LIFT = 4;
var JB_INFO_Y = 618;
var JB_CLOSE_X = 968;
var JB_CLOSE_Y = 60;
var JB_MENU_Y = 700;
var JB_GOLD = 0xFFC531;
var JB_RIM = 0x9DB8FF;
var JB_SOFT = 0xC9D6FF;
var JB_INK = 0x3A2400;

var jbWin;
var jbCover;
var jbActiveId;
var jbInfoOwner;

// --- drawing helpers -------------------------------------------------------------------

function jbRoundRect(mc, x, y, w, h, r)
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

function jbGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   jbRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function jbCircle(mc, x, y, r)
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

// The frosted socket behind stock art (the icons are made for light plates).
function jbSocket(mc, x, y, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.25,y:y - r * 1.4,w:r * 2.5,h:r * 2.5,r:0});
   jbCircle(mc,x,y,r);
   mc.endFill();
}

function jbGoldFill(mc, x, y, w, h, over)
{
   mc.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
}

// A white shine across the top of a gold shape.
function jbGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   jbRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function jbText(mc, name, txt, x, y, w, h, size, color, align)
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
function jbSetFitted(t, txt, minSize)
{
   var f = t.getTextFormat();
   f.size = t.baseSize;
   t.setNewTextFormat(f);
   t.text = txt;
   while(t.textWidth > t._width - 6 && f.size > minSize)
   {
      f.size -= 1;
      t.setTextFormat(f);
   }
   while(t.textWidth > t._width - 6 && txt.length > 3)
   {
      txt = txt.substr(0,txt.length - 1);
      t.text = txt + "...";
      t.setTextFormat(f);
   }
}

function jbSparkle(mc, x, y, r, alpha)
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

function jbOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// An image holder the client's loader can fill (it sizes images to the holder's first bounds), centered.
function jbImageHolder(parent, size)
{
   var holder = parent.createEmptyMovieClip("holder",1);
   holder.beginFill(0,0);
   jbRoundRect(holder,0,0,size,size,0);
   holder.endFill();
   holder._x = holder._y = - size / 2;
   return holder;
}

function jbStrong()
{
   return mx.transitions.easing.Strong.easeOut;
}

function jbElastic()
{
   return mx.transitions.easing.Elastic.easeOut;
}

// A gfx Button's look while the mouse is on it (Button.stateMap: release plays on into over).
function jbIsOver(button)
{
   return button.state == "over" || button.state == "down" || button.state == "release";
}

// Moves a stock clip so its bounds center on (x, y).
function jbCenterOn(mc, x, y)
{
   var b = mc.getBounds(mc._parent);
   mc._x += x - (b.xMin + b.xMax) / 2;
   mc._y += y - (b.yMin + b.yMax) / 2;
}

function jbNumber(value)
{
   var n = Number(value);
   return isNaN(n) ? 0 : n;
}

// --- frame -----------------------------------------------------------------------------

function jbDrawFrame(bg)
{
   // Opaque under the rim, so none of the stock frame shows through it.
   bg.lineStyle();
   bg.beginFill(0x141F55,100);
   jbRoundRect(bg,10.5,10.5,JB_W - 21,JB_H - 21,31.5);
   bg.endFill();
   jbGradientRect(bg,12,12,JB_W - 24,JB_H - 24,30,0x2E4FB0,0x141F55,100,JB_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-150,y:-300,w:900,h:700,r:0});
   jbRoundRect(bg,14,14,JB_W - 28,JB_H - 28,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   jbRoundRect(bg,16,16,JB_W - 32,JB_HEADER_H,26);
   bg.endFill();

   // Sparkles in the header (fixed seed, same every time).
   var s = 7373;
   var i;
   for(i = 0; i < 30; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 40 + s % (JB_W - 80);
      s = (s * 1103515245 + 12345) % 2147483648;
      jbSparkle(bg,x,24 + s % 70,1 + s % 3,25 + s % 50);
   }

   // A gold underline under the title, and the recess the cards sit in.
   bg.beginGradientFill("linear",[JB_GOLD,JB_GOLD,JB_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:JB_W / 2 - 170,y:0,w:340,h:2,r:0});
   jbRoundRect(bg,JB_W / 2 - 170,88,340,2,1);
   bg.endFill();
   jbGradientRect(bg,JB_PANEL_X,JB_PANEL_Y,JB_PANEL_W,JB_PANEL_H,24,0x0A1440,0x0A1440,38,JB_RIM,22,1);
}

// The window's drop shadow, under it on the root (the cover inside the window is masked to the frame).
function jbBuildShadow()
{
   var sh = _root.createEmptyMovieClip("sanctuaryJobsShadow",-16384);
   sh._x = jbWin._x;
   sh._y = jbWin._y;
   sh.beginFill(0x141F55,100);
   jbRoundRect(sh,12,12,JB_W - 24,JB_H - 24,30);
   sh.endFill();
   sh.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   // Trims the stock frame's vines and corners outside the navy frame.
   var mask = _root.createEmptyMovieClip("sanctuaryJobsMask",_root.getNextHighestDepth());
   mask._x = jbWin._x;
   mask._y = jbWin._y;
   mask.beginFill(0,100);
   jbRoundRect(mask,10.5,10.5,JB_W - 21,JB_H - 21,31.5);
   mask.endFill();
   jbWin.setMask(mask);
}

// The title, and under it the current job (icon in a frosted socket, name and level) once Lua names it.
function jbBuildTitle()
{
   var title = jbText(jbCover,"title_txt","Jobs",0,22,JB_W,40,28,0xFFE08A);
   title.filters = jbOutline();
   jbCover.subtitle_txt = jbText(jbCover,"subtitle_txt","Pick a job to switch to it",0,58,JB_W,22,14,JB_SOFT);

   var icon = jbCover.jobIcon = jbCover.createEmptyMovieClip("jobIcon",jbCover.getNextHighestDepth());
   icon._x = Math.round((JB_W - title.textWidth) / 2) - 34;
   icon._y = 42;
   jbSocket(icon,0,0,22,0xFFE08A,100,2);
   var art = icon.createEmptyMovieClip("art",icon.getNextHighestDepth());
   jbImageHolder(art,40);
   var mask = icon.createEmptyMovieClip("artMask",icon.getNextHighestDepth());
   mask.beginFill(0,100);
   jbCircle(mask,0,0,20);
   mask.endFill();
   art.setMask(mask);
   icon.filters = [new flash.filters.GlowFilter(JB_GOLD,0.45,10,10,2,2)];
   icon._visible = false;
}

// The line under the panel: the hovered job's name and level in gold over its description.
function jbBuildInfo()
{
   jbCover.infoName_txt = jbText(jbCover,"infoName_txt","",JB_PANEL_X + 40,JB_INFO_Y,JB_PANEL_W - 80,24,17,0xFFE08A);
   jbCover.infoName_txt.filters = jbOutline();
   jbCover.info_txt = jbText(jbCover,"info_txt","",JB_PANEL_X + 40,JB_INFO_Y + 23,JB_PANEL_W - 80,22,14,JB_SOFT);
   jbShowInfo(undefined);
}

function jbShowInfo(r)
{
   jbInfoOwner = r;
   var d = r.data;
   var name = jbCover.infoName_txt;
   var info = jbCover.info_txt;
   if(d == undefined)
   {
      name._visible = false;
      info._y = JB_INFO_Y + 12;
      jbSetFitted(info,"Click a job to switch to it.",11);
      return;
   }
   var rank = jbNumber(d.Rank);
   name._visible = true;
   jbSetFitted(name,String(d.label) + (rank > 0 ? "   Level " + rank : ""),12);
   info._y = JB_INFO_Y + 23;
   var text = d.Description == undefined ? "" : String(d.Description);
   // Lua sends localized text; an untranslated key or nothing gets a plain line instead.
   if(text.length == 0 || text == "nil" || text.substr(0,3) == "S##")
   {
      text = jbIsActive(d) ? "This is your current job." : "Click to switch to this job.";
   }
   jbSetFitted(info,text,11);
}

// --- shell buttons ---------------------------------------------------------------------

// The gold close button and Main Menu pill, under the stock buttons (invisible hit areas, centered on them).
function jbBuildButtons()
{
   var close = jbCover.close = jbCover.createEmptyMovieClip("close",jbCover.getNextHighestDepth());
   close._x = JB_CLOSE_X;
   close._y = JB_CLOSE_Y;
   close.draw = function(over)
   {
      this.clear();
      this.lineStyle(2,0xFFF3C4,100);
      jbGoldFill(this,-17,-17,34,34,over);
      jbCircle(this,0,0,17);
      this.endFill();
      jbGloss(this,-11,-14,22,11,5,50);
      this.lineStyle(4,JB_INK,100);
      this.moveTo(-6,-6);
      this.lineTo(6,6);
      this.moveTo(6,-6);
      this.lineTo(-6,6);
      this.filters = over ? [new flash.filters.GlowFilter(JB_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,6,6,1,2)];
   };
   close.draw(false);

   var pill = jbCover.menuPill = jbCover.createEmptyMovieClip("menuPill",jbCover.getNextHighestDepth());
   pill._x = JB_W / 2;
   pill._y = JB_MENU_Y;
   pill.bg = pill.createEmptyMovieClip("bg",1);
   pill.draw = function(over)
   {
      var g = this.bg;
      g.clear();
      g.lineStyle(2,0xFFF3C4,100);
      jbGoldFill(g,-92,-21,184,42,over);
      jbRoundRect(g,-92,-21,184,42,21);
      g.endFill();
      g.lineStyle();
      g.beginFill(0xFFFFFF,over ? 40 : 30);
      jbRoundRect(g,-82,-17,164,12,6);
      g.endFill();
      g.filters = over ? [new flash.filters.GlowFilter(JB_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,8,8,1,2)];
   };
   pill.draw(false);
   jbText(pill,"label_txt","Main Menu",-92,-12,184,26,18,JB_INK);

   var win = jbWin;
   win.exitButton._alpha = 0;
   win.mainMenuButton._alpha = 0;
   jbCenterOn(win.exitButton,JB_CLOSE_X,JB_CLOSE_Y);
   jbCenterOn(win.mainMenuButton,JB_W / 2,JB_MENU_Y);
}

function jbSyncButton(mine, over)
{
   if(over == mine.over)
   {
      return;
   }
   mine.over = over;
   mine.draw(over);
   if(mine == jbCover.close)
   {
      gs.TweenLite.to(mine,over ? 0.7 : 0.25,{_rotation:over ? 90 : 0,_xscale:over ? 112 : 100,_yscale:over ? 112 : 100,ease:over ? jbElastic() : jbStrong()});
   }
   else
   {
      gs.TweenLite.to(mine,over ? 0.6 : 0.25,{_xscale:over ? 106 : 100,_yscale:over ? 106 : 100,ease:over ? jbElastic() : jbStrong()});
   }
}

// --- job cards -------------------------------------------------------------------------

function jbIsActive(d)
{
   return d != undefined && jbActiveId != undefined && Number(d.Id) == jbActiveId;
}

// Hides a renderer's stock art and sets up its card, once, before its first setData: the loader sizes each image
// to the bounds its holder had at its first load (frGetMovieDimensions caches them), so the crest and icon boxes
// are set here and the stock reloads (every state change reloads the crest) keep them.
function jbStyleRenderer(r)
{
   if(r.jbCard != undefined)
   {
      return;
   }
   // _alpha, not _visible: the plate is most of the renderer's hit area.
   r.background._alpha = 0;
   r.progressBar._visible = false;
   r.txtName._visible = false;
   r.membersOnlyIcon._visible = false;
   r.jbOver = r.jbDown = false;

   var crest = r.iconBackground;
   crest._xscale = crest._yscale = JB_CREST_SCALE;
   crest._x = JB_CREST_CX - JB_CREST_W / 2;
   crest._y = JB_CREST_CY - JB_CREST_H / 2;
   var icon = r.holder;
   icon._width = icon._height = JB_ICON_SIZE;
   icon._x = JB_ICON_CX - JB_ICON_SIZE / 2;
   icon._y = JB_CREST_CY - JB_ICON_SIZE / 2;

   // Under all of the renderer's own children.
   var card = r.jbCard = r.createEmptyMovieClip("sanctuaryCard",-16384);
   card.bg = card.createEmptyMovieClip("bg",1);
   card.name_txt = jbText(card,"name_txt","",JB_CARD_X + 8,JB_NAME_Y,JB_CARD_W - 16,26,17,0xFFFFFF);
   card.name_txt.filters = jbOutline();
   card.level_txt = jbText(card,"level_txt","",JB_BADGE_X - 16,JB_BADGE_Y - 10,32,20,14,JB_INK);
   card.level_txt.filters = [new flash.filters.GlowFilter(0xFFF3C4,0.6,2,2,2,2)];
   card.xp_txt = jbText(card,"xp_txt","",JB_BAR_X,JB_BAR_Y - 2,JB_BAR_W,18,11,0xFFFFFF);
   card.xp_txt.filters = jbOutline();

   // The current job's tab, over the top of the crest.
   var tag = r.jbTag = r.createEmptyMovieClip("sanctuaryTag",r.getNextHighestDepth());
   tag.lineStyle(1.5,0xFFF3C4,100);
   jbGoldFill(tag,JB_CREST_CX - 40,-8,80,20,false);
   jbRoundRect(tag,JB_CREST_CX - 40,-8,80,20,10);
   tag.endFill();
   jbGloss(tag,JB_CREST_CX - 34,-7,68,8,4,45);
   jbText(tag,"label_txt","CURRENT",JB_CREST_CX - 40,-7,80,18,11,JB_INK);
   tag.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];
   tag._visible = false;
}

// The card's text and art for its data; fades in the first time it shows something.
function jbUpdateCard(r)
{
   var d = r.data;
   if(d == undefined || r.jbCard == undefined)
   {
      return;
   }
   var card = r.jbCard;
   var rank = jbNumber(d.Rank);
   r.jbRank = rank;
   r.jbPercent = rank >= 20 ? 100 : Math.max(0,Math.min(100,jbNumber(d.RankPercent)));
   r.jbActive = jbIsActive(d);
   jbSetFitted(card.name_txt,String(d.label),11);
   card.level_txt.text = String(rank);
   card.xp_txt.text = rank >= 20 ? "MAX" : Math.round(r.jbPercent) + "%";
   jbDrawCard(r);
   if(r.jbShown != true)
   {
      r.jbShown = true;
      r._alpha = 0;
      gs.TweenLite.to(r,0.35,{_alpha:100,delay:jbNumber(r.index) * 0.03,ease:jbStrong()});
   }
   if(jbInfoOwner == r)
   {
      jbShowInfo(r);
   }
}

function jbDrawCard(r)
{
   var card = r.jbCard;
   if(card == undefined)
   {
      return;
   }
   var g = card.bg;
   var x = JB_CARD_X;
   var y = JB_CARD_Y;
   var w = JB_CARD_W;
   var h = JB_CARD_H;
   var over = r.jbOver == true;
   var active = r.jbActive == true;
   g.clear();

   // The renderer's hit area reaches under the card's lifted spot, so a lifted card doesn't lose the mouse.
   g.lineStyle();
   g.beginFill(0,0);
   jbRoundRect(g,0,0,212,150 + JB_LIFT,0);
   g.endFill();

   if(over)
   {
      jbGradientRect(g,x,y,w,h,18,0x3F66D6,0x1D307A,100,active ? JB_GOLD : 0xFFE08A,100,active ? 3 : 2);
   }
   else
   {
      jbGradientRect(g,x,y,w,h,18,0x284A9E,0x162660,100,active ? JB_GOLD : JB_RIM,active ? 100 : 40,active ? 3 : 1.5);
   }
   g.lineStyle();
   if(active)
   {
      // A gold wash from the top.
      g.beginGradientFill("linear",[JB_GOLD,JB_GOLD],[over ? 40 : 30,0],[0,255],{matrixType:"box",x:0,y:y,w:10,h:90,r:Math.PI / 2});
      jbRoundRect(g,x + 2,y + 2,w - 4,86,16);
      g.endFill();
   }
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[over ? 20 : 14,0],[0,255],{matrixType:"box",x:0,y:y + 3,w:10,h:44,r:Math.PI / 2});
   jbRoundRect(g,x + 3,y + 3,w - 6,44,15);
   g.endFill();

   // A soft halo behind the crest, gold for the current job.
   var halo = active ? JB_GOLD : 0x8FB4FF;
   g.beginGradientFill("radial",[halo,halo],[over ? 65 : 45,0],[0,255],{matrixType:"box",x:JB_CREST_CX - 70,y:JB_CREST_CY - 52,w:140,h:104,r:0});
   jbCircle(g,JB_CREST_CX,JB_CREST_CY,62);
   g.endFill();

   // The XP row: a recessed strip with the level badge and the gold bar.
   g.beginFill(0x0A1440,over ? 30 : 40);
   jbRoundRect(g,x + 6,JB_BAR_Y - 9,w - 12,32,14);
   g.endFill();
   g.lineStyle(1,JB_RIM,30);
   g.beginFill(0x0A1440,70);
   jbRoundRect(g,JB_BAR_X,JB_BAR_Y,JB_BAR_W,JB_BAR_H,JB_BAR_H / 2);
   g.endFill();
   if(r.jbPercent > 0)
   {
      var fw = Math.max(JB_BAR_H - 2,Math.round((JB_BAR_W - 2) * r.jbPercent / 100));
      g.lineStyle();
      jbGoldFill(g,JB_BAR_X,JB_BAR_Y,JB_BAR_W,JB_BAR_H,false);
      jbRoundRect(g,JB_BAR_X + 1,JB_BAR_Y + 1,fw,JB_BAR_H - 2,(JB_BAR_H - 2) / 2);
      g.endFill();
      if(fw > 12)
      {
         g.beginFill(0xFFFFFF,40);
         jbRoundRect(g,JB_BAR_X + 5,JB_BAR_Y + 3,fw - 8,3,1.5);
         g.endFill();
      }
   }
   g.lineStyle(1.5,0xFFF3C4,100);
   jbGoldFill(g,JB_BADGE_X - 15,JB_BADGE_Y - 15,30,30,over);
   jbCircle(g,JB_BADGE_X,JB_BADGE_Y,15);
   g.endFill();
   jbGloss(g,JB_BADGE_X - 10,JB_BADGE_Y - 13,20,10,5,50);

   if(over)
   {
      g.filters = [new flash.filters.GlowFilter(JB_GOLD,0.6,16,16,2,2)];
   }
   else if(active)
   {
      g.filters = [new flash.filters.GlowFilter(JB_GOLD,0.4,12,12,2,2)];
   }
   else
   {
      g.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,8,8,1,2)];
   }
   card.name_txt.textColor = active ? 0xFFE08A : 0xFFFFFF;
   r.jbTag._visible = active;
   jbGlowCrest(r);
}

// The loader tells each image's frLoadListener when it's in (AutoMovieClipEntry.Load), so the plate goes
// invisible again and the crest gets its glow back after every stock reload, whatever loading does to them.
var jbPlateListener = new Object();
jbPlateListener.onLoadInit = function(target)
{
   target._alpha = 0;
};
var jbCrestListener = new Object();
jbCrestListener.onLoadInit = function(target)
{
   jbGlowCrest(target._parent);
};

// Set before every stock call that reloads them (a load may drop the holder's own properties).
function jbWatchLoads(r)
{
   r.background.frLoadListener = jbPlateListener;
   r.iconBackground.frLoadListener = jbCrestListener;
}

// The crest's soft glow; set again after each stock reload of it.
function jbGlowCrest(r)
{
   var over = r.jbOver == true;
   r.iconBackground.filters = [new flash.filters.GlowFilter(r.jbActive ? JB_GOLD : 0xBFD4FF,over ? 0.8 : 0.5,over ? 16 : 12,over ? 16 : 12,1.5,2)];
   // The plate is reloaded with the crest; it stays an invisible hit area.
   r.background._alpha = 0;
}

// Hover: the card lifts and glows and the job icon pops; a press settles it.
function jbStateChanged(r)
{
   var over = jbIsOver(r);
   var down = r.state == "down";
   if(over == r.jbOver && down == r.jbDown)
   {
      jbGlowCrest(r);
      return;
   }
   r.jbOver = over;
   r.jbDown = down;
   jbDrawCard(r);
   if(r.jbHomeY == undefined)
   {
      r.jbHomeY = r._y;
   }
   // A new tween on the renderer ends its fade-in (TweenLite overwrites), so this one finishes it.
   gs.TweenLite.to(r,0.25,{_y:r.jbHomeY - (down ? 1 : (over ? JB_LIFT : 0)),_alpha:100,ease:jbStrong()});
   var size = over && !down ? JB_ICON_HOVER : JB_ICON_SIZE;
   gs.TweenLite.to(r.holder,over ? 0.7 : 0.3,{_x:JB_ICON_CX - size / 2,_y:JB_CREST_CY - size / 2,_width:size,_height:size,ease:over ? jbElastic() : jbStrong()});
   if(over)
   {
      jbShowInfo(r);
   }
   else if(jbInfoOwner == r)
   {
      jbShowInfo(undefined);
   }
}

function jbList()
{
   return jbWin.view.currentView.jobList;
}

function jbRedrawCards()
{
   var renderers = jbList().renderers;
   var i;
   for(i = 0; i < renderers.length; i++)
   {
      var r = renderers[i];
      if(r.jbCard != undefined && r.data != undefined)
      {
         r.jbActive = jbIsActive(r.data);
         jbDrawCard(r);
      }
   }
}

// Lua names the current job (SelectItem(activeProfileID)) after sending the list.
function jbSetActive(id)
{
   jbActiveId = Number(id);
   var jobs = dto.JobData.GetInstance().JobListData;
   var d;
   var i;
   for(i = 0; i < jobs.length; i++)
   {
      if(Number(jobs[i].Id) == jbActiveId)
      {
         d = jobs[i];
      }
   }
   var icon = jbCover.jobIcon;
   if(d == undefined)
   {
      jbCover.subtitle_txt.text = "Pick a job to switch to it";
      icon._visible = false;
   }
   else
   {
      var rank = jbNumber(d.Rank);
      jbCover.subtitle_txt.text = "Current job: " + d.label + (rank > 0 ? "  (Level " + rank + ")" : "");
      _root.frSetIconByIndex(icon.art.holder,d.IconId);
      icon._visible = true;
   }
   jbRedrawCards();
}

function jbPatchRenderers()
{
   var proto = controls.JobItemRenderer.prototype;
   var setData = proto.setData;
   proto.setData = function(data)
   {
      jbStyleRenderer(this);
      jbWatchLoads(this);
      var result = setData.apply(this,arguments);
      jbUpdateCard(this);
      return result;
   };
   // Button.draw stretches the renderer to its 215x160 cell; the card is drawn at its own size.
   var draw = proto.draw;
   proto.draw = function()
   {
      var result = draw.apply(this,arguments);
      this._xscale = this._yscale = 100;
      return result;
   };
   var stateChange = proto.updateAfterStateChange;
   // Every state reloads the crest and plate, so the boxes have to be set before the first one as well.
   proto.updateAfterStateChange = function()
   {
      jbStyleRenderer(this);
      jbWatchLoads(this);
      var result = stateChange.apply(this,arguments);
      jbStateChanged(this);
      return result;
   };
   // Membership no longer exists (the badge also showed a Members Only tooltip).
   proto._setMembersBadge = function()
   {
      this.membersOnlyIcon._visible = false;
   };

   // Where each renderer rests, for the hover lift.
   var tiles = gfx.controls.TileList.prototype;
   var drawLayout = tiles.drawLayout;
   tiles.drawLayout = function()
   {
      var result = drawLayout.apply(this,arguments);
      var i;
      for(i = 0; i < this.renderers.length; i++)
      {
         var r = this.renderers[i];
         r.jbHomeY = r._y;
         if(r.jbOver && !r.jbDown)
         {
            r._y -= JB_LIFT;
         }
      }
      return result;
   };
}

function jbPatchSelectItem()
{
   // configUI hands Lua a delegate to _selectItem: patched here if it hasn't run yet, wrapped below if it has.
   var proto = views.FRJobBrowser.prototype;
   var selectItem = proto._selectItem;
   proto._selectItem = function(id)
   {
      var result = selectItem.apply(this,arguments);
      jbSetActive(id);
      return result;
   };
   if(typeof _root.SelectItem == "function")
   {
      var stock = _root.SelectItem;
      _root.SelectItem = function(id)
      {
         var result = stock.apply(this,arguments);
         jbSetActive(id);
         return result;
      };
   }
}

// --- scrollbar -------------------------------------------------------------------------

// The stock scrollbar (attached past 12 jobs) keeps its arrows, track and thumb as invisible hit areas; a slim
// navy track with a gold thumb is drawn under them, inside its 20px width (the list subtracts that width).
function jbSyncScrollBar()
{
   var sb = jbList().scrollBar;
   if(sb == undefined || sb == null || sb._parent == undefined)
   {
      return;
   }
   var art = sb.jbArt;
   if(art == undefined)
   {
      sb.track._alpha = sb.upArrow._alpha = sb.downArrow._alpha = sb.thumb._alpha = 0;
      art = sb.jbArt = sb.createEmptyMovieClip("sanctuaryScroll",-16384);
   }
   var up = sb.upArrow.getBounds(sb);
   var down = sb.downArrow.getBounds(sb);
   var t = sb.thumb.getBounds(sb);
   var over = jbIsOver(sb.thumb);
   var key = Math.round(up.yMax) + "," + Math.round(down.yMin) + "," + Math.round(down.yMax) + "," + Math.round(t.yMin) + "," + Math.round(t.yMax) + "," + sb.thumb._visible + "," + over;
   if(key == art.key)
   {
      return;
   }
   art.key = key;
   var cx = 10;
   art.clear();
   art.lineStyle(1,JB_RIM,30);
   art.beginFill(0x0A1440,55);
   jbRoundRect(art,cx - 5,up.yMax + 2,10,down.yMin - up.yMax - 4,5);
   art.endFill();
   // Chevrons where the stock arrows are.
   var top = (up.yMin + up.yMax) / 2;
   var bottom = (down.yMin + down.yMax) / 2;
   art.lineStyle(3,JB_SOFT,80);
   art.moveTo(cx - 6,top + 3);
   art.lineTo(cx,top - 3);
   art.lineTo(cx + 6,top + 3);
   art.moveTo(cx - 6,bottom - 3);
   art.lineTo(cx,bottom + 3);
   art.lineTo(cx + 6,bottom - 3);
   if(sb.thumb._visible && t.yMax > t.yMin)
   {
      art.lineStyle(1,0xFFF3C4,over ? 100 : 80);
      jbGoldFill(art,cx - 5,t.yMin,10,t.yMax - t.yMin,over);
      jbRoundRect(art,cx - 5,t.yMin + 1,10,Math.max(10,t.yMax - t.yMin - 2),5);
      art.endFill();
   }
   art.filters = over ? [new flash.filters.GlowFilter(JB_GOLD,0.5,8,8,2,2)] : [];
}

function jbSync()
{
   jbSyncScrollBar();
   jbSyncButton(jbCover.close,jbIsOver(jbWin.exitButton));
   jbSyncButton(jbCover.menuPill,jbIsOver(jbWin.mainMenuButton));
}

// --- build -----------------------------------------------------------------------------

// The window is the root's unnamed clip (timeline depth 1).
function jbFindWindow()
{
   var win = _root.getInstanceAtDepth(-16383);
   if(win.view != undefined && win.mainMenuButton != undefined)
   {
      return win;
   }
   var name;
   for(name in _root)
   {
      if(typeof _root[name] == "movieclip" && _root[name].view != undefined && _root[name].mainMenuButton != undefined)
      {
         return _root[name];
      }
   }
   return undefined;
}

function jbBuild()
{
   jbWin = jbFindWindow();
   if(jbWin == undefined)
   {
      return;
   }
   var win = jbWin;
   win.title_txt._visible = false;
   win.view._x = JB_VIEW_X;
   win.view._y = JB_VIEW_Y;

   // Above the stock frame (a plain shape at timeline depth 1); the cards and the stock buttons go back above it.
   jbCover = win.createEmptyMovieClip("sanctuaryJobs",10);
   jbDrawFrame(jbCover.createEmptyMovieClip("bg",1));
   jbBuildTitle();
   jbBuildInfo();
   jbBuildButtons();
   win.view.swapDepths(20);
   win.exitButton.swapDepths(21);
   win.mainMenuButton.swapDepths(22);
   jbBuildShadow();
   jbCover.onEnterFrame = jbSync;
}

jbPatchRenderers();
jbPatchSelectItem();
jbBuild();
// ===== End Sanctuary Jobs =====

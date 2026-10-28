// ===== Sanctuary Achievements =====
// Appended to achievements' main timeline by build_client_mods.py.
// Redraws the Achievements window in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass
// frame with a gold title, close button and Main Menu pill, a category sidebar whose rows carry their icons in
// frosted sockets (the selected one rimmed in gold), and a header strip with the selected category and a gold
// Total Points pill. The summary page shows the recent achievements as glass cards (or an empty trophy case),
// an overall progress bar and a row per category (gold bars; a click opens the category). Category pages list
// the achievements as glass cards with their icon and reward in frosted sockets and a gold progress bar; earned
// ones are rimmed in gold. Everything is drawn under or over the stock clips, which keep their roles (hit
// areas, Lua callbacks, the tree's scrolling). Labels are plain text; the trophy cups are the movie's own.

var ACH_W = 1024;
var ACH_H = 768;
// The sidebar, the header strip and the content panel to its right.
var ACH_SIDE_X = 34;
var ACH_SIDE_W = 232;
var ACH_TREE_Y = 166;
var ACH_PANEL_X = 280;
var ACH_PANEL_W = 710;
var ACH_HEADER_Y = 104;
var ACH_HEADER_H = 48;
var ACH_PANEL_Y = 160;
var ACH_PANEL_BOTTOM = 666;
// Summary page: recent cards, the overall bar and the category rows (PanelRow puts its second item at 350).
var ACH_CONTENT_X = 295;
var ACH_RECENT_Y = 194;
var ACH_RECENT_W = 158;
var ACH_RECENT_H = 160;
var ACH_RECENT_GAP = 16;
var ACH_OVERALL_Y = 370;
var ACH_ROWS_Y = 474;
var ACH_ROW_W = 330;
// Category pages: the achievement cards (FRAchievementEntry, 652 wide) and their scrollbar.
var ACH_LIST_X = 292;
var ACH_ENTRY_W = 652;
var ACH_GOLD = 0xFFC531;
var ACH_RIM = 0x9DB8FF;
var ACH_SOFT = 0xC9D6FF;
var ACH_INK = 0x3A2400;

var achFrame;
var achSummary = false;

// --- drawing helpers -------------------------------------------------------------------

function achRoundRect(mc, x, y, w, h, r)
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

function achGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   achRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

// A recess: flat navy glass with a faint rim.
function achPanel(mc, x, y, w, h, r, alpha, rimAlpha)
{
   mc.lineStyle(1,ACH_RIM,rimAlpha);
   mc.beginFill(0x0A1440,alpha);
   achRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

// The soft white shine across the top of a card.
function achGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   achRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function achCircle(mc, x, y, r)
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
function achSocket(mc, x, y, w, h, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - w * 0.18,y:y - h * 0.24,w:w * 1.36,h:h * 1.36,r:0});
   achRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function achSocketCircle(mc, x, y, r, rim, rimAlpha, rimWidth)
{
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r * 1.3,y:y - r * 1.5,w:r * 2.6,h:r * 2.6,r:0});
   achCircle(mc,x,y,r);
   mc.endFill();
}

// A gold progress bar in a navy track; f is 0..1.
function achBar(mc, x, y, w, h, f)
{
   if(isNaN(f) || f < 0)
   {
      f = 0;
   }
   else if(f > 1)
   {
      f = 1;
   }
   mc.lineStyle(1,ACH_RIM,25);
   mc.beginFill(0x0A1440,65);
   achRoundRect(mc,x,y,w,h,h / 2);
   mc.endFill();
   if(f <= 0)
   {
      return;
   }
   var fw = Math.max(h,w * f);
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:fw,h:h,r:Math.PI / 2});
   achRoundRect(mc,x,y,fw,h,h / 2);
   mc.endFill();
   if(fw > h)
   {
      mc.beginFill(0xFFFFFF,40);
      achRoundRect(mc,x + h / 2,y + 1,fw - h,Math.max(1,h * 0.3),h * 0.15);
      mc.endFill();
   }
}

function achSparkle(mc, x, y, r, alpha)
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

function achStar(mc, x, y, r, color, alpha)
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

// A trophy cup's outline, centered on x,y (the empty trophy case).
function achTrophyOutline(mc, x, y)
{
   mc.moveTo(x - 30,y - 38);
   mc.lineTo(x + 30,y - 38);
   mc.curveTo(x + 30,y + 4,x,y + 10);
   mc.curveTo(x - 30,y + 4,x - 30,y - 38);
   mc.moveTo(x - 30,y - 30);
   mc.curveTo(x - 48,y - 30,x - 45,y - 14);
   mc.curveTo(x - 42,y - 2,x - 24,y);
   mc.moveTo(x + 30,y - 30);
   mc.curveTo(x + 48,y - 30,x + 45,y - 14);
   mc.curveTo(x + 42,y - 2,x + 24,y);
   mc.moveTo(x - 6,y + 10);
   mc.lineTo(x - 6,y + 24);
   mc.moveTo(x + 6,y + 10);
   mc.lineTo(x + 6,y + 24);
   achRoundRect(mc,x - 22,y + 24,44,12,5);
}

function achText(mc, name, txt, x, y, w, h, size, color, align)
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
function achSetFitted(t, txt)
{
   var f = t.getTextFormat();
   f.size = t.baseSize;
   t.setNewTextFormat(f);
   t.text = txt;
   while(t.textWidth > t._width - 6 && f.size > 10)
   {
      f.size -= 1;
      t.setTextFormat(f);
   }
}

function achOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function achShadow()
{
   return [new flash.filters.DropShadowFilter(4,90,0x050A24,0.5,10,10,1,2)];
}

// An image holder the client's loader can fill, centered on cx,cy so it can grow about its middle.
// The mask (when given) rounds off the art's corners and crops it to its socket.
function achIconClip(parent, name, depth, cx, cy, size, mx, my, mw, mh, mr)
{
   var icon = parent.createEmptyMovieClip(name,depth);
   icon._x = cx;
   icon._y = cy;
   var holder = icon.createEmptyMovieClip("holder",1);
   holder.beginFill(0,0);
   achRoundRect(holder,0,0,size,size,0);
   holder.endFill();
   holder._x = holder._y = - size / 2;
   if(mw != undefined)
   {
      var mask = parent.createEmptyMovieClip(name + "Mask",depth + 1);
      mask.beginFill(0,100);
      achRoundRect(mask,mx,my,mw,mh,mr);
      mask.endFill();
      icon.setMask(mask);
   }
   return icon;
}

// Thousands separators: 1128 -> "1,128".
function achNumber(n)
{
   n = Math.floor(Number(n));
   if(isNaN(n))
   {
      return "0";
   }
   var s = String(Math.abs(n));
   var out = "";
   while(s.length > 3)
   {
      out = "," + s.substr(s.length - 3) + out;
      s = s.substr(0,s.length - 3);
   }
   return (n < 0 ? "-" : "") + s + out;
}

// Penner's elastic ease (the movie only has None and Strong compiled in).
function achElasticOut(t, b, c, d)
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

function achTint(mc, ra, rb, ga, gb, ba, bb)
{
   new Color(mc).setTransform({ra:ra,rb:rb,ga:ga,gb:gb,ba:ba,bb:bb,aa:100,ab:0});
}

// --- frame -----------------------------------------------------------------------------

function achBuildFrame()
{
   var f = achFrame = _root.createEmptyMovieClip("sanctuaryAchievements",-16384);
   var bg = f.createEmptyMovieClip("bg",1);
   achGradientRect(bg,12,12,ACH_W - 24,ACH_H - 24,30,0x2E4FB0,0x141F55,100,ACH_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-150,y:-300,w:900,h:700,r:0});
   achRoundRect(bg,14,14,ACH_W - 28,ACH_H - 28,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   achRoundRect(bg,16,16,ACH_W - 32,76,26);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   // Sparkles in the header (fixed seed, same every time).
   var s = 5151;
   var i;
   for(i = 0; i < 30; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 40 + s % (ACH_W - 80);
      s = (s * 1103515245 + 12345) % 2147483648;
      achSparkle(bg,x,24 + s % 62,1 + s % 3,25 + s % 50);
   }

   // The sidebar, the header strip and the content panel.
   var p = f.createEmptyMovieClip("panels",2);
   achPanel(p,ACH_SIDE_X,ACH_HEADER_Y,ACH_SIDE_W,ACH_PANEL_BOTTOM - ACH_HEADER_Y,24,40,25);
   achPanel(p,ACH_PANEL_X,ACH_HEADER_Y,ACH_PANEL_W,ACH_HEADER_H,22,50,30);
   achPanel(p,ACH_PANEL_X,ACH_PANEL_Y,ACH_PANEL_W,ACH_PANEL_BOTTOM - ACH_PANEL_Y,24,30,20);
   var heading = achText(f,"categories_txt","Categories",ACH_SIDE_X,112,ACH_SIDE_W,26,17,0xFFFFFF);
   heading.filters = achOutline();
   p.lineStyle();
   p.beginGradientFill("linear",[ACH_GOLD,ACH_GOLD,ACH_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:ACH_SIDE_X + 30,y:0,w:ACH_SIDE_W - 60,h:2,r:0});
   achRoundRect(p,ACH_SIDE_X + 30,142,ACH_SIDE_W - 60,2,1);
   p.endFill();

   var title = achText(f,"title_txt","Achievements",0,18,ACH_W,40,28,0xFFE08A);
   title.filters = achOutline();
   achText(f,"subtitle_txt","Your trophy case",0,54,ACH_W,24,14,ACH_SOFT);

   achBuildHeader();
   achBuildSummary();
   achBuildClose();
   achBuildMainMenu();
}

// --- header strip: the selected category and the Total Points pill ----------------------

function achBuildHeader()
{
   var h = achFrame.createEmptyMovieClip("header",achFrame.getNextHighestDepth());
   h.path_txt = achText(h,"path_txt","",ACH_PANEL_X + 20,ACH_HEADER_Y + 13,300,24,16,ACH_SOFT,"left");
   h.name_txt = achText(h,"name_txt","",ACH_PANEL_X + 20,ACH_HEADER_Y + 9,400,32,22,0xFFFFFF,"left");
   h.name_txt.filters = achOutline();
   h.caption_txt = achText(h,"caption_txt","",ACH_PANEL_X + 20,ACH_HEADER_Y + 16,260,22,13,ACH_SOFT,"left");

   var pill = h.points = h.createEmptyMovieClip("points",h.getNextHighestDepth());
   pill._y = ACH_HEADER_Y + 4;
   pill.bg = pill.createEmptyMovieClip("bg",1);
   var cup = pill.attachMovie("badge_bg3","cup",2);
   cup._xscale = cup._yscale = 30;
   cup._x = 21 - cup._width / 2;
   cup._y = 20 - cup._height / 2;
   pill.value_txt = achText(pill,"value_txt","0",46,6,200,30,21,ACH_INK,"left");
   pill.unit_txt = achText(pill,"unit_txt","TOTAL POINTS",0,15,120,18,11,0x6A4A08,"left");
   pill.filters = [new flash.filters.GlowFilter(ACH_GOLD,0.4,12,12,2,2)];
   achSetPoints(0);
}

function achSetPoints(points)
{
   var pill = achFrame.header.points;
   pill.value_txt.text = achNumber(points);
   pill.unit_txt._x = pill.value_txt._x + pill.value_txt.textWidth + 8;
   var w = Math.round(pill.unit_txt._x + pill.unit_txt.textWidth + 18);
   pill._x = ACH_PANEL_X + ACH_PANEL_W - 10 - w;
   var g = pill.bg;
   g.clear();
   g.lineStyle(1.5,0xFFF3C4,100);
   g.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:0,y:0,w:w,h:40,r:Math.PI / 2});
   achRoundRect(g,0,0,w,40,20);
   g.endFill();
   achGloss(g,6,2,w - 12,16,8,45);
   // The cup's socket.
   g.lineStyle(1.5,0xFFF3C4,90);
   g.beginGradientFill("radial",[0x2B4FA8,0x111E52],[100,100],[0,255],{matrixType:"box",x:3,y:2,w:36,h:36,r:0});
   achCircle(g,21,20,16);
   g.endFill();
}

// The selected category ("Jobs / Medic") and, on a category page, how many of its achievements are done.
function achRefreshHeader()
{
   var h = achFrame.header;
   var cat = m_tree_mc.selectedCategory;
   var name = cat.data.catText;
   var parentName = cat.parent.data.catText;
   if(name == undefined)
   {
      name = "Achievements";
   }
   var x = ACH_PANEL_X + 20;
   if(parentName != undefined)
   {
      h.path_txt.text = parentName + "  /";
      h.path_txt._visible = true;
      x += h.path_txt.textWidth + 10;
   }
   else
   {
      h.path_txt._visible = false;
   }
   h.name_txt._x = x;
   h.name_txt._width = Math.max(80,h.points._x - 160 - x);
   achSetFitted(h.name_txt,name);

   var caption = "";
   if(!achSummary)
   {
      var n = 0;
      var done = 0;
      var i;
      for(i = 0; i < qArray.length; i++)
      {
         if(qArray[i]._name != undefined)
         {
            n++;
            if(qArray[i].questProgress >= 1)
            {
               done++;
            }
         }
      }
      if(n > 0)
      {
         caption = done + " of " + n + " earned";
      }
   }
   h.caption_txt.text = caption;
   h.caption_txt._x = x + h.name_txt.textWidth + 14;
}

// --- shell buttons ---------------------------------------------------------------------

function achDrawClose(over)
{
   var c = achFrame.close;
   c.clear();
   c.lineStyle(1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-17,y:-17,w:34,h:34,r:Math.PI / 2});
   achCircle(c,0,0,17);
   c.endFill();
   achGloss(c,-11,-14,22,11,5,50);
   c.lineStyle(4,ACH_INK,100);
   c.moveTo(-6,-6);
   c.lineTo(6,6);
   c.moveTo(6,-6);
   c.lineTo(-6,6);
   c.filters = over ? [new flash.filters.GlowFilter(ACH_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

// A gold close button under the stock one, which keeps its click (an invisible clip still takes the mouse).
function achBuildClose()
{
   exit_mc1._alpha = 0;
   var b = exit_mc1.getBounds(_root);
   var c = achFrame.createEmptyMovieClip("close",achFrame.getNextHighestDepth());
   c._x = b.xMax > b.xMin ? (b.xMin + b.xMax) / 2 : 967;
   c._y = b.yMax > b.yMin ? (b.yMin + b.yMax) / 2 : 44;
   achDrawClose(false);

   var rollOver = exit_mc1.onRollOver;
   exit_mc1.onRollOver = function()
   {
      rollOver.call(this);
      achHoverClose(true);
   };
   var rollOut = exit_mc1.onRollOut;
   exit_mc1.onRollOut = function()
   {
      rollOut.call(this);
      achHoverClose(false);
   };
   var dragOut = exit_mc1.onDragOut;
   exit_mc1.onDragOut = function()
   {
      dragOut.call(this);
      achHoverClose(false);
   };
}

function achHoverClose(over)
{
   achDrawClose(over);
   gs.TweenLite.to(achFrame.close,over ? 0.7 : 0.25,{_xscale:over ? 115 : 100,_yscale:over ? 115 : 100,_rotation:over ? 90 : 0,ease:over ? achElasticOut : mx.transitions.easing.Strong.easeOut});
}

function achDrawMainMenu(over)
{
   var m = achFrame.mainMenu.bg;
   m.clear();
   m.lineStyle(1.5,0xFFF3C4,100);
   m.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-90,y:-20,w:180,h:40,r:Math.PI / 2});
   achRoundRect(m,-90,-20,180,40,20);
   m.endFill();
   achGloss(m,-82,-18,164,16,8,50);
   achFrame.mainMenu.filters = over ? [new flash.filters.GlowFilter(ACH_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
}

// A gold pill under the stock Main Menu button, which keeps sending HideToMainMenu().
function achBuildMainMenu()
{
   mainMenuButton._alpha = 0;
   var b = mainMenuButton.getBounds(_root);
   var m = achFrame.createEmptyMovieClip("mainMenu",achFrame.getNextHighestDepth());
   m._x = b.xMax > b.xMin ? Math.round((b.xMin + b.xMax) / 2) : 512;
   m._y = b.yMax > b.yMin ? Math.round((b.yMin + b.yMax) / 2) : 705;
   m.bg = m.createEmptyMovieClip("bg",1);
   achText(m,"label_txt","Main Menu",-90,-13,180,28,18,ACH_INK);
   achDrawMainMenu(false);
}

function achHoverMainMenu(over)
{
   achDrawMainMenu(over);
   gs.TweenLite.to(achFrame.mainMenu,over ? 0.7 : 0.25,{_xscale:over ? 106 : 100,_yscale:over ? 106 : 100,ease:over ? achElasticOut : mx.transitions.easing.Strong.easeOut});
}

// --- sidebar: the category tree --------------------------------------------------------

function achIsRoot(item)
{
   return Number(item.data.parentCatId) == 0;
}

function achDrawNav(item)
{
   var g = item.achBg;
   if(g == undefined)
   {
      return;
   }
   var root = achIsRoot(item);
   // The rows sit inside the tree's mask; children are indented by the tree.
   var w = 197 - item._x;
   g.clear();
   if(item.boolIsSelected)
   {
      achGradientRect(g,0,1,w,32,14,0x3352AE,0x1B2E74,100,ACH_GOLD,100,2);
      g.lineStyle();
      g.beginGradientFill("linear",[ACH_GOLD,ACH_GOLD],[40,0],[0,255],{matrixType:"box",x:0,y:0,w:w,h:32,r:0});
      achRoundRect(g,1,2,w - 2,30,13);
      g.endFill();
      g.filters = [new flash.filters.GlowFilter(ACH_GOLD,0.45,10,10,2,2)];
   }
   else if(item.achOver)
   {
      achGradientRect(g,0,1,w,32,14,0x3F66D6,0x1D307A,100,0xFFE08A,90,1.5);
      g.filters = [];
   }
   else if(root)
   {
      achGradientRect(g,0,1,w,32,14,0x284A9E,0x162660,100,ACH_RIM,35,1);
      g.filters = [];
   }
   else
   {
      g.lineStyle();
      g.beginFill(0x0A1440,35);
      achRoundRect(g,0,1,w,32,14);
      g.endFill();
      g.filters = [];
   }
   if(root || item.boolIsSelected || item.achOver)
   {
      achGloss(g,3,3,w - 6,13,10,14);
   }
   achSocketCircle(g,item.icon._x,17,root ? 14 : 12,item.boolIsSelected ? ACH_GOLD : 0xFFFFFF,item.boolIsSelected ? 100 : 80,1.5);
}

function achStyleNav(item)
{
   if(item.achBg == undefined)
   {
      item.achBg = item.createEmptyMovieClip("achBg",-16384);
      item.highlight._alpha = 0;
      // The stock divider line under each row is an unnamed clip.
      var k;
      for(k in item)
      {
         var o = item[k];
         if(typeof o == "movieclip" && o._parent == item && o != item.achBg && o != item.highlight && o != item.icon && o != item.mcExpandArrow)
         {
            o._alpha = 0;
         }
      }
      item.txtLabel.filters = achOutline();
   }
   var root = achIsRoot(item);
   item.achIconScale = root ? 44 : 36;
   item.icon._x = root ? 18 : 16;
   item.icon._y = 17;
   item.icon._xscale = item.icon._yscale = item.achIconScale;
   item.txtLabel._x = root ? 38 : 34;
   if(!item.boolIsSelected)
   {
      item.txtLabel.textColor = root ? 0xFFFFFF : ACH_SOFT;
   }
   achDrawNav(item);
}

// The tree's scroll strips stay (hovering them scrolls); they're invisible, with a chevron when there's more.
function achDrawNavChrome(tree)
{
   var c = tree.achChrome;
   if(c == undefined)
   {
      c = tree.achChrome = tree.createEmptyMovieClip("achChrome",-16384);
   }
   c.clear();
   c.lineStyle(3,ACH_SOFT,80);
   if(tree.mcNavTop.mcArrowTop._visible)
   {
      c.moveTo(91,0);
      c.lineTo(101,-8);
      c.lineTo(111,0);
   }
   if(tree.mcNavBottom.mcArrowBottom._visible)
   {
      c.moveTo(92,449);
      c.lineTo(102,457);
      c.lineTo(112,449);
   }
}

function achPatchTree()
{
   var tree = fr.display.TreeNavigation.prototype;
   var setElementData = tree._setElementData;
   tree._setElementData = function(item, data)
   {
      setElementData.call(this,item,data);
      achStyleNav(item);
   };
   // The selected root's bars: the top one is our gold row, the one behind its children a faint recess.
   var updateSelectedBar = tree._updateSelectedBar;
   tree._updateSelectedBar = function()
   {
      updateSelectedBar.call(this);
      this.mcSelectedBarTop._alpha = 0;
      this.mcSelectedBarBottom._alpha = 35;
   };
   var onScrollbarUpdate = tree.onScrollbarUpdate;
   tree.onScrollbarUpdate = function(obj)
   {
      onScrollbarUpdate.call(this,obj);
      achDrawNavChrome(this);
   };

   // NavigationElement copies its roll handlers in its constructor, so these are patched before any row exists.
   var nav = fr.display.NavigationElement.prototype;
   var stockSelect = nav.select;
   nav.select = function()
   {
      stockSelect.call(this);
      this.txtLabel.textColor = 0xFFE08A;
      achDrawNav(this);
   };
   var stockUnselect = nav.unselect;
   nav.unselect = function()
   {
      stockUnselect.call(this);
      this.txtLabel.textColor = achIsRoot(this) ? 0xFFFFFF : ACH_SOFT;
      achDrawNav(this);
   };
   var rollOver = nav._handleRollOver;
   nav._handleRollOver = function()
   {
      rollOver.call(this);
      this.achOver = true;
      achDrawNav(this);
      gs.TweenLite.to(this.icon,0.7,{_xscale:this.achIconScale * 1.2,_yscale:this.achIconScale * 1.2,ease:achElasticOut});
   };
   var rollOut = nav._handleRollOut;
   nav._handleRollOut = function()
   {
      rollOut.call(this);
      this.achOver = false;
      achDrawNav(this);
      gs.TweenLite.to(this.icon,0.25,{_xscale:this.achIconScale,_yscale:this.achIconScale,ease:mx.transitions.easing.Strong.easeOut});
   };
}

// --- achievement cards (category pages) and recent cards (summary) ---------------------
// Both are FRAchievementEntry ("Entry" and "RecentEntry"). Their stock plates go invisible but keep the mouse;
// the icon and reward are loaded again into our own holders, in frosted sockets, since the stock ones sit on
// dark plates that can't be hidden on their own.

function achStyleEntry(e)
{
   if(e.achLayer != undefined)
   {
      return;
   }
   var recent = e.achRecent = e.description_txt == undefined;
   var L = e.achLayer = e.createEmptyMovieClip("achLayer",-16384);
   L.card = L.createEmptyMovieClip("card",1);
   e.bg._alpha = e.doneBg._alpha = 0;
   if(recent)
   {
      var cx = ACH_RECENT_W / 2;
      L.icon = achIconClip(L,"icon",2,cx,58,76,cx - 40,18,80,80,18);
      L.top = L.createEmptyMovieClip("top",4);
      L.cat_txt = achText(L,"cat_txt","",6,131,ACH_RECENT_W - 12,20,12,ACH_SOFT);
      // The plate is the card's hit area; a little taller, so the card can lift on hover without losing the mouse.
      e.bg._width = ACH_RECENT_W;
      e.bg._height = ACH_RECENT_H + 8;
      e.title_txt._x = 6;
      e.title_txt._width = ACH_RECENT_W - 12;
      e.title_txt._y = 108;
   }
   else
   {
      e.achSize = {h:FRAchievementEntry.ITEM_HEIGHT};
      L.icon = achIconClip(L,"icon",2,53,53,70,16,16,74,74,14);
      L.reward = achIconClip(L,"reward",4,496,60,50,469,33,54,54,11);
      L.top = L.createEmptyMovieClip("top",6);
      L.pct_txt = achText(L,"pct_txt","",444,81,60,20,12,ACH_SOFT,"left");
      e.prog._alpha = 0;
      e.title_txt._x = 108;
      e.description_txt._x = 108;
      e.description_txt._width = 336;
      e.description_txt.textColor = ACH_SOFT;
      e.reward_txt.textColor = 0xFFE08A;
      e.reward_txt.filters = achOutline();
      e.trophyName_txt.textColor = 0xFFE08A;
      e.trophyName_txt.filters = achOutline();
   }
   e.title_txt.textColor = 0xFFFFFF;
   e.title_txt.filters = achOutline();
}

// After the stock loadGraphics: our icons, the points over the trophy, and the card itself.
function achAfterLoad(e)
{
   var L = e.achLayer;
   if(Number(e.iconID) > 0)
   {
      _root.frSetIconByImageSetSlot(L.icon.holder,e.iconID,7);
   }
   if(L.reward != undefined && Number(e.rewardSetID) > 0)
   {
      _root.frSetIconByImageSetSlot(L.reward.holder,e.rewardSetID,7);
   }
   // The points on the trophy: one outlined number instead of a black copy behind a white one.
   e.points_txt_2._visible = false;
   e.points_txt_1.filters = achOutline();
   if(e.achRecent)
   {
      // Recent cards have no trophy to put points on.
      e.points_txt_1._visible = false;
      var cat = m_tree_mc.hashTree[String(e.m_categoryID)];
      L.cat_txt.text = cat.catText != undefined ? cat.catText : "";
   }
   achSyncEntry(e);
}

// Follows the stock state: doneBg shows when earned, the badge and reward are dimmed while in progress.
function achSyncEntry(e)
{
   var L = e.achLayer;
   if(L == undefined)
   {
      return;
   }
   e.achDone = e.doneBg._visible == true;
   e.badge._alpha = e.reward_mc._alpha = 0;
   L.icon._alpha = e.achDone ? 100 : 55;
   if(L.reward != undefined)
   {
      L.reward._alpha = e.achDone ? 100 : 55;
      L.reward._visible = Number(e.rewardSetID) > 0;
   }
   e.achPct = Math.floor(e.questProgress * 100 + 0.5);
   if(isNaN(e.achPct))
   {
      e.achPct = 0;
   }
   achDrawEntry(e);
}

function achDrawEntry(e)
{
   if(e.achRecent)
   {
      achDrawRecent(e);
      return;
   }
   var L = e.achLayer;
   var c = L.card;
   var w = ACH_ENTRY_W;
   var h = e.achSize.h;
   var done = e.achDone;
   var over = e.achOver;
   c.clear();
   if(over)
   {
      achGradientRect(c,0,0,w,h,18,0x3F66D6,0x1D307A,100,done ? ACH_GOLD : 0xFFE08A,100,done ? 2.5 : 2);
   }
   else if(done)
   {
      achGradientRect(c,0,0,w,h,18,0x2E52B0,0x18296C,100,ACH_GOLD,100,2.5);
   }
   else
   {
      achGradientRect(c,0,0,w,h,18,0x284A9E,0x162660,100,ACH_RIM,40,1.5);
   }
   achGloss(c,4,3,w - 8,40,15,16);

   // A faint divider before the reward and trophy, and a glow behind the cup.
   c.lineStyle();
   c.beginGradientFill("linear",[ACH_RIM,ACH_RIM,ACH_RIM],[0,40,0],[0,128,255],{matrixType:"box",x:0,y:10,w:2,h:86,r:Math.PI / 2});
   achRoundRect(c,452,10,1,86,0);
   c.endFill();
   var glow = done ? ACH_GOLD : 0x8FB4FF;
   c.beginGradientFill("radial",[glow,glow],[done ? 45 : 22,0],[0,255],{matrixType:"box",x:546,y:2,w:102,h:102,r:0});
   achCircle(c,597,53,51);
   c.endFill();

   achSocket(c,12,12,82,82,18,done ? ACH_GOLD : 0xFFFFFF,done ? 100 : 85,done ? 2.5 : 2);
   if(L.reward._visible)
   {
      achSocket(c,466,30,60,60,14,0xFFFFFF,85,2);
   }
   achBar(c,108,88,330,8,e.achPct / 100);
   L.pct_txt.text = e.achPct + "%";
   L.pct_txt.textColor = done ? 0xFFE08A : ACH_SOFT;

   // Earned: a gold check on the icon socket's corner.
   var t = L.top;
   t.clear();
   if(done)
   {
      t.lineStyle(1.5,0xFFF3C4,100);
      t.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:78,y:6,w:22,h:22,r:Math.PI / 2});
      achCircle(t,89,17,11);
      t.endFill();
      t.lineStyle(3,ACH_INK,100);
      t.moveTo(84,17);
      t.lineTo(88,21);
      t.lineTo(95,13);
   }

   if(over)
   {
      c.filters = [new flash.filters.GlowFilter(ACH_GOLD,0.55,14,14,2,2)];
   }
   else if(done)
   {
      c.filters = [new flash.filters.GlowFilter(ACH_GOLD,0.35,10,10,2,2)];
   }
   else
   {
      c.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
   }
}

// Recent achievements are all earned: gold rims, the icon big in its socket, title and category under it.
function achDrawRecent(e)
{
   var L = e.achLayer;
   var c = L.card;
   var w = ACH_RECENT_W;
   var h = ACH_RECENT_H;
   var over = e.achOver;
   c.clear();
   achGradientRect(c,0,0,w,h,20,over ? 0x3F66D6 : 0x2B4FA8,over ? 0x1D307A : 0x17286A,100,over ? 0xFFE08A : ACH_GOLD,100,over ? 2.5 : 2);
   c.lineStyle();
   c.beginGradientFill("radial",[ACH_GOLD,ACH_GOLD],[over ? 45 : 28,0],[0,255],{matrixType:"box",x:w / 2 - 80,y:-50,w:160,h:180,r:0});
   achRoundRect(c,3,3,w - 6,h - 6,18);
   c.endFill();
   achGloss(c,4,3,w - 8,36,16,14);
   achSocket(c,w / 2 - 44,14,88,88,22,ACH_GOLD,100,2.5);
   c.filters = over ? [new flash.filters.GlowFilter(ACH_GOLD,0.75,18,18,2,2)] : [new flash.filters.GlowFilter(ACH_GOLD,0.3,10,10,2,2)];
   L.top.clear();
   achStar(L.top,w - 18,18,8,0xFFE08A,over ? 100 : 75);
}

function achHoverEntry(e, over)
{
   if(e.achLayer == undefined || e.achOver == over)
   {
      return;
   }
   e.achOver = over;
   achDrawEntry(e);
   var s = over ? 116 : 100;
   gs.TweenLite.to(e.achLayer.icon,over ? 0.7 : 0.25,{_xscale:s,_yscale:s,ease:over ? achElasticOut : mx.transitions.easing.Strong.easeOut});
   if(e.achRecent)
   {
      // Finishes the entrance fade too, in case the mouse got there first.
      gs.TweenLite.to(e,0.3,{_y:e.achBaseY - (over ? 6 : 0),_alpha:100,ease:mx.transitions.easing.Strong.easeOut});
   }
}

function achStyleObjective(o, f)
{
   o.prog._alpha = 0;
   o.title.textColor = 0xFFFFFF;
   o.title.filters = achOutline();
   o.description.textColor = ACH_SOFT;
   var g = o.createEmptyMovieClip("achBar",-16384);
   achBar(g,5,39,72,8,f);
}

function achPatchEntries()
{
   var proto = FRAchievementEntry.prototype;
   var loadGraphics = proto.loadGraphics;
   proto.loadGraphics = function()
   {
      achStyleEntry(this);
      loadGraphics.call(this);
      achAfterLoad(this);
   };
   var updateProgress = proto.updateProgress;
   proto.updateProgress = function(n)
   {
      updateProgress.call(this,n);
      achSyncEntry(this);
   };
   var setComplete = proto.setComplete;
   proto.setComplete = function()
   {
      setComplete.call(this);
      achSyncEntry(this);
   };

   // The roll handlers are copied onto the plate, texts, badge and expand button in the constructor. The stock
   // ones fade the plate in and out; it stays invisible and the card lights up instead.
   var rollover = proto.rollover;
   proto.rollover = function()
   {
      rollover.call(this);
      this._parent.bg._alpha = 0;
      if(this._parent.m_objectiveCount != 0)
      {
         achHoverEntry(this._parent,true);
      }
   };
   var rollout = proto.rollout;
   proto.rollout = function()
   {
      rollout.call(this);
      this._parent.bg._alpha = 0;
      achHoverEntry(this._parent,false);
   };
   var rewardRollOver = proto.rewardRollOver;
   proto.rewardRollOver = function()
   {
      rewardRollOver.call(this);
      this._parent.bg._alpha = 0;
      achHoverEntry(this._parent,true);
   };
   var rewardRollOut = proto.rewardRollOut;
   proto.rewardRollOut = function()
   {
      rewardRollOut.call(this);
      this._parent.bg._alpha = 0;
      achHoverEntry(this._parent,false);
   };

   // Expanding (objectives) tweens the stock plates' height; the card follows.
   var scaleHeight = proto.scaleHeight;
   proto.scaleHeight = function(scaleswitch, newheight)
   {
      scaleHeight.call(this,scaleswitch,newheight);
      if(this.achSize != undefined)
      {
         gs.TweenLite.to(this.achSize,0.2,{h:newheight,ease:mx.transitions.easing.None.easeNone,onUpdate:achDrawEntry,onUpdateParams:[this]});
      }
   };
   var addObjective = proto.addObjective;
   proto.addObjective = function(obj)
   {
      addObjective.call(this,obj);
      achStyleObjective(this.ObjArray[this.ObjArray.length - 1],obj.progress);
   };
}

// --- summary page ----------------------------------------------------------------------

function achBuildSummary()
{
   var s = achFrame.createEmptyMovieClip("summary",achFrame.getNextHighestDepth());
   var t = achText(s,"recent_txt","Recent Achievements",ACH_CONTENT_X + 4,ACH_PANEL_Y + 6,400,26,17,0xFFFFFF,"left");
   t.filters = achOutline();
   t = achText(s,"rows_txt","By Category",ACH_CONTENT_X + 4,ACH_ROWS_Y - 28,400,26,17,0xFFFFFF,"left");
   t.filters = achOutline();

   // The empty trophy case, until the first achievement is earned.
   var e = s.createEmptyMovieClip("empty",s.getNextHighestDepth());
   achPanel(e,ACH_CONTENT_X,ACH_RECENT_Y,ACH_PANEL_X + ACH_PANEL_W - 15 - ACH_CONTENT_X,ACH_RECENT_H,20,25,25);
   var cx = ACH_PANEL_X + ACH_PANEL_W / 2;
   e.lineStyle();
   e.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[22,0],[0,255],{matrixType:"box",x:cx - 80,y:ACH_RECENT_Y - 10,w:160,h:140,r:0});
   achCircle(e,cx,ACH_RECENT_Y + 60,70);
   e.endFill();
   e.lineStyle(3,ACH_RIM,45);
   achTrophyOutline(e,cx,ACH_RECENT_Y + 64);
   achStar(e,cx,ACH_RECENT_Y + 46,9,ACH_RIM,35);
   achSparkle(e,cx - 62,ACH_RECENT_Y + 30,2,45);
   achSparkle(e,cx + 66,ACH_RECENT_Y + 48,2,35);
   achSparkle(e,cx + 50,ACH_RECENT_Y + 18,1,45);
   achText(e,"hint_txt","Earn achievements to see them here",ACH_PANEL_X,ACH_RECENT_Y + 112,ACH_PANEL_W,24,16,ACH_SOFT);
}

// The overall bar: drawn over the stock bar and its plate (an unnamed shape), so it brings its own texts.
function achDrawOverall(o, c, t)
{
   if(o._name == undefined)
   {
      return;
   }
   var v = o.achCover;
   if(v == undefined)
   {
      v = o.achCover = o.createEmptyMovieClip("achCover",50);
      v.bg = v.createEmptyMovieClip("bg",1);
      var cup = v.attachMovie("badge_bg3","cup",2);
      cup._xscale = cup._yscale = 38;
      cup._x = 32 - cup._width / 2;
      cup._y = 31 - cup._height / 2;
      v.label_txt = achText(v,"label_txt","Achievements Completed",66,6,420,26,17,0xFFFFFF,"left");
      v.label_txt.filters = achOutline();
      v.pct_txt = achText(v,"pct_txt","0%",536,0,136,40,30,0xFFE08A,"right");
      v.pct_txt.filters = achOutline();
      v.count_txt = achText(v,"count_txt","",530,38,142,20,13,ACH_SOFT,"right");
      v.filters = achShadow();
   }
   if(isNaN(c))
   {
      c = 0;
   }
   if(isNaN(t))
   {
      t = 0;
   }
   var f = t > 0 ? Math.min(1,c / t) : 0;
   v.pct_txt.text = Math.floor(f * 100 + 0.5) + "%";
   v.count_txt.text = achNumber(c) + " of " + achNumber(t);
   var g = v.bg;
   g.clear();
   // A little bigger than the stock plate, so its rounded corners cover the plate's.
   achGradientRect(g,-2,-2,684,68,14,0x284A9E,0x162660,100,ACH_RIM,45,1.5);
   achGloss(g,2,1,676,24,12,16);
   achSocketCircle(g,32,31,24,ACH_GOLD,100,2);
   achBar(g,66,36,460,16,f);
}

// The root category with this name (the summary rows are the tree's roots), for its icon and id.
function achRootCategory(label)
{
   var tree = m_tree_mc.linearizedTree;
   var i;
   for(i = 0; i < tree.length; i++)
   {
      if(Number(tree[i].parentCatId) == 0 && tree[i].catText == label)
      {
         return tree[i];
      }
   }
   return undefined;
}

function achDrawRowBg(r, over)
{
   var g = r.bg;
   g.clear();
   // From -1: covers the stock plate (an unnamed shape) corners and all.
   achGradientRect(g,-1,-1,ACH_ROW_W + 2,38,8,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : ACH_RIM,over ? 100 : 40,over ? 2 : 1);
   achGloss(g,2,1,ACH_ROW_W - 4,14,6,14);
   achSocketCircle(g,19,18,14,0xFFFFFF,85,1.5);
   achBar(g,40,25,ACH_ROW_W - 48,6,r.f);
   r.filters = over ? [new flash.filters.GlowFilter(ACH_GOLD,0.5,12,12,2,2)] : [];
}

// A category row (PanelListItem) on the summary page: icon, name, count and a gold bar; a click opens it.
function achDrawRow(item)
{
   var r = item.achRow;
   if(r == undefined)
   {
      // PanelRow puts the items at 0 and 350; two pixels in, so the row's rim clears the list's mask.
      item._x += 2;
      r = item.achRow = item.createEmptyMovieClip("achRow",100);
      r.bg = r.createEmptyMovieClip("bg",1);
      r.star = r.createEmptyMovieClip("star",2);
      achStar(r.star,19,18,9,ACH_GOLD,100);
      r.icon = achIconClip(r,"icon",3,19,18,26);
      r.label_txt = achText(r,"label_txt","",40,0,190,24,15,0xFFFFFF,"left");
      r.label_txt.filters = achOutline();
      r.count_txt = achText(r,"count_txt","",226,1,ACH_ROW_W - 230,22,13,ACH_SOFT,"right");
      r.onRollOver = function()
      {
         fscommand("FR_event","PlaySound(DockOver)");
         achDrawRowBg(this,true);
         gs.TweenLite.to(this.icon,0.7,{_xscale:120,_yscale:120,ease:achElasticOut});
      };
      r.onRollOut = r.onReleaseOutside = function()
      {
         achDrawRowBg(this,false);
         gs.TweenLite.to(this.icon,0.25,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
      };
      // The same way a recent card opens its category (NavigateToObjective).
      r.onRelease = function()
      {
         m_tree_mc.expandAndSelectCategory(String(this.catId));
      };
   }
   var d = item.m_data;
   var c = Number(d.questsCompleted);
   var t = Number(d.totalQuests);
   r.f = Number(d.progress);
   if(isNaN(r.f))
   {
      r.f = t > 0 ? c / t : 0;
   }
   achSetFitted(r.label_txt,d.label);
   r.count_txt.text = achNumber(c) + " / " + achNumber(t);
   var cat = achRootCategory(d.label);
   r.catId = cat.catId;
   r.enabled = cat != undefined;
   if(Number(cat.catIcon) > 0)
   {
      _root.frSetIconByIndex(r.icon.holder,cat.catIcon);
      r.icon._visible = true;
      r.star._visible = false;
   }
   else
   {
      r.icon._visible = false;
      r.star._visible = true;
   }
   achDrawRowBg(r,false);
}

function achPatchSummary()
{
   var drawItem = PanelListItem.prototype.drawItem;
   PanelListItem.prototype.drawItem = function()
   {
      drawItem.call(this);
      achDrawRow(this);
   };
   // The list's mask a little wider, for the second column's rim.
   var stockInit = VertList.prototype.init;
   VertList.prototype.init = function(w, h)
   {
      stockInit.call(this,w,h);
      this.m_mask._width = w + 12;
   };
   var populate = OverallCompleted.prototype.populate;
   OverallCompleted.prototype.populate = function(percent, questsCompleted, totalQuests, str)
   {
      populate.call(this,percent,questsCompleted,totalQuests,str);
      achDrawOverall(this,Number(questsCompleted),Number(totalQuests));
   };
}

// Recent cards in a row, in index order (whatever Lua's first index is).
function achLayoutRecent()
{
   var k = 0;
   var i;
   for(i = 0; i < riArray.length; i++)
   {
      var e = riArray[i];
      if(e._name != undefined)
      {
         e._x = ACH_CONTENT_X + k * (ACH_RECENT_W + ACH_RECENT_GAP);
         e.achBaseY = ACH_RECENT_Y;
         k++;
      }
   }
   return k;
}

function achLayout()
{
   var s = achFrame.summary;
   s._visible = achSummary;
   s.empty._visible = achSummary && achLayoutRecent() == 0;
   // Labels the stock code shows again with each page, and the stock header bar (a loaded image).
   browser.achievementTxt_0._visible = browser.achievementTxt_1._visible = browser.achievementTxt_2._visible = false;
   browser.recentAchievements_txt._visible = false;
   browser.sortingbar_background_mc._visible = false;
   achRefreshHeader();
}

// --- stock clips -----------------------------------------------------------------------

function achBuild()
{
   achBuildFrame();

   browser.frame_mc._alpha = 0;
   browser.recentAchievement_BG._alpha = 0;
   browser.sortingbar_background_mc._visible = false;
   browser.JobSort._visible = false;
   browser.totalPointsLabel_txt._visible = browser.totalPointsValue_txt._visible = false;
   JobSort._visible = false;
   pageTurner_mc._visible = false;

   // The tree: a little higher under the Categories heading, with invisible scroll strips.
   m_tree_mc._y = ACH_TREE_Y;
   m_tree_mc.mcNavTop._alpha = m_tree_mc.mcNavBottom._alpha = 0;

   // Category pages: the list inside the content panel, its mask the panel's height, the scrollbar beside it.
   qList._x = ACH_LIST_X;
   browser.qMask._x = ACH_LIST_X - 8;
   browser.qMask._y = ACH_PANEL_Y + 6;
   browser.qMask._width = ACH_ENTRY_W + 16;
   browser.qMask._height = ACH_PANEL_BOTTOM - ACH_PANEL_Y - 12;
   scrollbar._x = ACH_PANEL_X + ACH_PANEL_W - 38;
   scrollbar._y = ACH_PANEL_Y + 8;
   achTint(scrollbar.background_mc,25,5,25,15,30,45);
   achTint(scrollbar.channel_mc,25,5,25,15,30,45);
   achTint(scrollbar.thumb_mc,100,80,75,30,20,0);
   achLayout();
}

// --- Lua -------------------------------------------------------------------------------

var achSetSummaryPage = SetSummaryPage;
SetSummaryPage = function(n)
{
   achSetSummaryPage(n);
   achSummary = n != 0;
   if(achSummary)
   {
      overall_complete._x = ACH_CONTENT_X + 2;
      overall_complete._y = ACH_OVERALL_Y + 2;
      m_list._x = ACH_CONTENT_X;
      m_list._y = ACH_ROWS_Y + 1;
      achDrawOverall(overall_complete,0,0);
   }
   achLayout();
};

var achZapItems = zapItems;
zapItems = function()
{
   achZapItems();
   achLayout();
};

var achAddRecentItems = AddRecentItems_lua;
AddRecentItems_lua = function(index, sTitle, icon, cat, objID)
{
   achAddRecentItems(index,sTitle,icon,cat,objID);
   var e = riArray[index];
   achLayout();
   // Pops in from just below, one after another.
   e._y = ACH_RECENT_Y + 14;
   e._alpha = 0;
   gs.TweenLite.to(e,0.5,{_y:ACH_RECENT_Y,_alpha:100,delay:0.05 + achLayoutRecent() * 0.06,ease:mx.transitions.easing.Strong.easeOut});
};

var achAddItem = AddItem;
AddItem = function()
{
   achAddItem.apply(_root,arguments);
   achRefreshHeader();
};

var achSetTotalPoints = SetTotalPoints;
SetTotalPoints = function(points)
{
   achSetTotalPoints(points);
   achSetPoints(points);
   achRefreshHeader();
};

var achSetCategoryTree = SetCategoryTree;
SetCategoryTree = function(str)
{
   achSetCategoryTree(str);
   achRefreshHeader();
};

var achOnNavigationSelectItem = onNavigationSelectItem;
onNavigationSelectItem = function(selectedCategory)
{
   achOnNavigationSelectItem(selectedCategory);
   achRefreshHeader();
};

// The Main Menu button's roll events call these by name.
var achMainMenuOver = _onmainMenuButtonOver;
_onmainMenuButtonOver = function()
{
   achMainMenuOver();
   achHoverMainMenu(true);
};
var achMainMenuOut = _onmainMenuButtonOut;
_onmainMenuButtonOut = function()
{
   achMainMenuOut();
   achHoverMainMenu(false);
};

achPatchTree();
achPatchEntries();
achPatchSummary();
achBuild();
// ===== End Sanctuary Achievements =====

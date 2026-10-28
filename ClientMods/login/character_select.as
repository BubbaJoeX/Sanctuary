// ===== Sanctuary Character Select =====
// Appended to characterSelectV2's main timeline by build_client_mods.py.
// Redraws the character select panel (the list beside the character lineup) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass frame with a gold "Choose Your Hero" title and a hero
// count, the characters as glass cards with their initial in a frosted medallion (the chosen one rimmed in gold
// with a gold check, "Ready to play!"), a gold-rimmed "Create a New Hero" card, and the buttons as a gold Play
// pill, a rose Delete pill and a gold Create pill. The list scrolls with the mouse wheel or its gold thumb.
// The stock list stays hidden but keeps its role (Lua fills it with addCharacter, its selection drives
// setCharacterId/setButtonState); a card click does what the stock row does (SelectCharacter). The stock
// buttons stay as invisible hit areas over the pills, so add/delete/go send their stock events. The stock
// panel is a plain shape, dropped at build time (REMOVED_DEPTHS). Nothing here needs the world to be loaded.

var CS_W = 425;
var CS_H = 525;
// The list's recess, the cards inside it, and the buttons under it.
var CS_LIST_X = 22;
var CS_LIST_Y = 100;
var CS_LIST_W = 381;
var CS_LIST_H = 332;
var CS_PAD = 10;
var CS_ROW_H = 62;
var CS_ROW_GAP = 8;
var CS_BUTTON_Y = 448;
var CS_GOLD = 0xFFC531;
var CS_RIM = 0x9DB8FF;
var CS_SOFT = 0xC9D6FF;
var CS_INK = 0x3A2400;

var csFrame;
var csRows = [];
var csSelected = 0;
var csScroll = 0;

// --- drawing helpers -------------------------------------------------------------------

function csRoundRect(mc, x, y, w, h, r)
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

function csGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   csRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

// A recess: flat navy glass with a faint rim.
function csPanel(mc, x, y, w, h, r, alpha, rimAlpha)
{
   mc.lineStyle(1,CS_RIM,rimAlpha);
   mc.beginFill(0x0A1440,alpha);
   csRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

// The soft white shine across the top of a card or pill.
function csGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   csRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function csCircle(mc, x, y, r)
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

function csSparkle(mc, x, y, r, alpha)
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

function csText(mc, name, txt, x, y, w, h, size, color, align)
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
function csSetFitted(t, txt)
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

function csSetColor(t, color)
{
   var f = t.getTextFormat();
   f.color = color;
   t.setNewTextFormat(f);
   t.setTextFormat(f);
}

function csOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Penner's elastic ease (the movie only has Strong compiled in).
function csElasticOut(t, b, c, d)
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

function csBuildFrame()
{
   var f = csFrame = _root.createEmptyMovieClip("sanctuaryCharacterSelect",-16384);
   var bg = f.createEmptyMovieClip("bg",1);
   csGradientRect(bg,8,8,CS_W - 16,CS_H - 16,30,0x2E4FB0,0x141F55,100,CS_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-160,y:-220,w:520,h:460,r:0});
   csRoundRect(bg,10,10,CS_W - 20,CS_H - 20,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   csRoundRect(bg,12,12,CS_W - 24,78,26);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   // Sparkles in the header (fixed seed, same every time).
   var s = 2626;
   var i;
   for(i = 0; i < 16; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 30 + s % (CS_W - 60);
      s = (s * 1103515245 + 12345) % 2147483648;
      csSparkle(bg,x,20 + s % 62,1 + s % 3,25 + s % 50);
   }
   // A gold rule under the header.
   bg.beginGradientFill("linear",[CS_GOLD,CS_GOLD,CS_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:60,y:0,w:CS_W - 120,h:2,r:0});
   csRoundRect(bg,60,89,CS_W - 120,2,1);
   bg.endFill();

   var title = csText(f,"title_txt","Choose Your Hero",0,16,CS_W,40,28,0xFFE08A);
   title.filters = csOutline();
   f.subtitle = csText(f,"subtitle_txt","",0,54,CS_W,24,14,CS_SOFT);

   // The list: a recess, the cards (masked to it) and a slim scrollbar.
   var p = f.createEmptyMovieClip("panel",2);
   csPanel(p,CS_LIST_X,CS_LIST_Y,CS_LIST_W,CS_LIST_H,22,40,25);

   var list = f.list = f.createEmptyMovieClip("list",3);
   list._x = CS_LIST_X + CS_PAD;
   list._y = CS_LIST_Y + CS_PAD;
   list.rows = list.createEmptyMovieClip("rows",1);
   var mask = list.createEmptyMovieClip("mask",2);
   mask.beginFill(0,100);
   // A little wider than the cards, so their hover lift and glow aren't clipped at the sides.
   csRoundRect(mask,-CS_PAD + 2,-CS_PAD + 4,CS_LIST_W - 4,CS_LIST_H - 8,18);
   mask.endFill();
   list.rows.setMask(mask);

   csBuildScrollbar();
   csBuildButtons();
}

// --- buttons ---------------------------------------------------------------------------

// Fits a stock button (an invisible hit area from now on) over the pill drawn for it.
function csCoverButton(btn, x, y, w, h)
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

// kind: "gold" (Play, Create) or "rose" (Delete). The pill's clip is centered so the hover grows it about its middle.
function csDrawPill(pill, over, down)
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
      g.lineStyle(1.5,over ? 0xFFC9C9 : 0xFF9A9A,over ? 100 : 70);
      g.beginGradientFill("linear",over ? [0xE0604A,0xA8302A] : [0x5A2A5E,0x2A1648],[100,100],[0,255],{matrixType:"box",x:- w / 2,y:- h / 2,w:w,h:h,r:Math.PI / 2});
   }
   csRoundRect(g,- w / 2,- h / 2,w,h,h / 2);
   g.endFill();
   csGloss(g,- w / 2 + 8,- h / 2 + 2,w - 16,h * 0.42,h * 0.2,pill.kind == "gold" ? 55 : 30);
   pill.body._y = down ? 2 : 0;
   if(over)
   {
      pill.filters = [new flash.filters.GlowFilter(pill.kind == "gold" ? CS_GOLD : 0xFF7A6A,0.8,16,16,2,2)];
   }
   else
   {
      pill.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
   }
}

function csMakePill(name, kind, label, glyph, x, y, w, h, size)
{
   var pill = csFrame.createEmptyMovieClip(name,csFrame.getNextHighestDepth());
   pill._x = x + w / 2;
   pill._y = y + h / 2;
   pill.kind = kind;
   pill.w = w;
   pill.h = h;
   pill.bg = pill.createEmptyMovieClip("bg",1);
   pill.body = pill.createEmptyMovieClip("body",2);
   var color = kind == "gold" ? CS_INK : 0xFFE6E6;
   var t = csText(pill.body,"label_txt",label,- w / 2 + (glyph != "" ? 14 : 0),- size * 0.72,w,size + 12,size,color);
   if(kind != "gold")
   {
      t.filters = csOutline();
   }

   // The glyph left of the label: a play triangle, a plus or a bin.
   var gl = pill.body.createEmptyMovieClip("glyph",pill.body.getNextHighestDepth());
   // The label sits 14px right of center to make room, so its first letter is at 14 - textWidth / 2.
   gl._x = - t.textWidth / 2;
   if(glyph == "play")
   {
      gl.lineStyle();
      gl.beginFill(CS_INK,100);
      gl.moveTo(-6,-9);
      gl.lineTo(9,0);
      gl.lineTo(-6,9);
      gl.lineTo(-6,-9);
      gl.endFill();
   }
   else if(glyph == "plus")
   {
      gl.lineStyle();
      gl.beginFill(CS_INK,100);
      csRoundRect(gl,-8,-2.5,16,5,2);
      csRoundRect(gl,-2.5,-8,5,16,2);
      gl.endFill();
   }
   else if(glyph == "bin")
   {
      gl.lineStyle(2,color,100);
      gl.moveTo(-7,-5);
      gl.lineTo(7,-5);
      gl.moveTo(-3,-8);
      gl.lineTo(3,-8);
      gl.beginFill(color,25);
      gl.moveTo(-5,-3);
      gl.lineTo(5,-3);
      gl.lineTo(4,8);
      gl.lineTo(-4,8);
      gl.lineTo(-5,-3);
      gl.endFill();
   }
   csDrawPill(pill,false,false);
   return pill;
}

function csBuildButtons()
{
   // Play and Delete share the row; Create (only with the "Create a New Hero" card chosen) takes the middle.
   csFrame.del = csMakePill("del","rose","Delete","bin",CS_LIST_X + 6,CS_BUTTON_Y + 4,124,44,17);
   csFrame.play = csMakePill("play","gold","Play!","play",CS_LIST_X + 142,CS_BUTTON_Y,233,52,24);
   csFrame.create = csMakePill("create","gold","Create Hero","plus",(CS_W - 250) / 2,CS_BUTTON_Y,250,52,22);
   csFrame.del.btn = deleteButton;
   csFrame.play.btn = goButton;
   csFrame.create.btn = addButton;
   csCoverButton(deleteButton,CS_LIST_X + 6,CS_BUTTON_Y + 4,124,44);
   csCoverButton(goButton,CS_LIST_X + 142,CS_BUTTON_Y,233,52);
   csCoverButton(addButton,(CS_W - 250) / 2,CS_BUTTON_Y,250,52);
   csSyncButtons();

   // Dragging off a pressed button only resets its art (no hover-out event), so the pill hears it here.
   var buttons = [deleteButton,goButton,addButton];
   var i;
   for(i = 0; i < buttons.length; i++)
   {
      buttons[i].onDragOut = function()
      {
         FRIconButton.prototype.onDragOut.call(this);
         csHoverPill(this,false);
      };
   }
}

function csPillFor(btn)
{
   if(btn == goButton)
   {
      return csFrame.play;
   }
   if(btn == deleteButton)
   {
      return csFrame.del;
   }
   if(btn == addButton)
   {
      return csFrame.create;
   }
   return undefined;
}

function csHoverPill(btn, over, down)
{
   var pill = csPillFor(btn);
   if(pill == undefined)
   {
      return;
   }
   csDrawPill(pill,over,down);
   var scale = over ? (pill.kind == "gold" ? 106 : 104) : 100;
   gs.TweenLite.to(pill,over ? 0.7 : 0.25,{_xscale:scale,_yscale:scale,ease:over ? csElasticOut : mx.transitions.easing.Strong.easeOut});
}

// The pills follow the stock buttons, which setButtonState shows and hides ("add" or "character").
function csSyncButtons()
{
   csFrame.play._visible = goButton._visible;
   csFrame.del._visible = deleteButton._visible;
   csFrame.create._visible = addButton._visible && !goButton._visible;
}

// The stock buttons broadcast these to _root, their listener.
function onHoverOver(btn)
{
   csHoverPill(btn,true,false);
}

function onHoverOut(btn)
{
   csHoverPill(btn,false,false);
}

function onButtonPress(btn)
{
   var pill = csPillFor(btn);
   if(pill != undefined)
   {
      csDrawPill(pill,true,true);
   }
}

function onButtonRelease(btn)
{
   var pill = csPillFor(btn);
   if(pill != undefined)
   {
      csDrawPill(pill,btn.hitTest(_root._xmouse,_root._ymouse,true),false);
   }
}

// --- cards -----------------------------------------------------------------------------

function csRowW()
{
   return CS_LIST_W - 2 * CS_PAD - (csMaxScroll() > 0 ? 14 : 0);
}

function csDrawRow(row)
{
   var w = csRowW();
   var h = CS_ROW_H;
   var d = characterData[row.index];
   var add = d.state == "add";
   var sel = row.index == csSelected;
   var over = row.over;
   var g = row.body.bg;
   g.clear();
   if(add)
   {
      // The "new hero" card: a lighter glass slot with a gold rim, waiting to be filled.
      g.lineStyle(over || sel ? 2.5 : 1.5,sel ? CS_GOLD : 0xFFE08A,over || sel ? 100 : 55);
      g.beginGradientFill("linear",over ? [0x3F66D6,0x1D307A] : [0x1A2E78,0x0E1A4E],[over ? 100 : 70,over ? 100 : 70],[0,255],{matrixType:"box",x:0,y:0,w:w,h:h,r:Math.PI / 2});
      csRoundRect(g,0,0,w,h,18);
      g.endFill();
   }
   else if(sel)
   {
      csGradientRect(g,0,0,w,h,18,over ? 0x4A72E0 : 0x3F66D6,0x1D307A,100,CS_GOLD,100,3);
      // A gold warmth from the left, behind the medallion.
      g.lineStyle();
      g.beginGradientFill("radial",[CS_GOLD,CS_GOLD],[38,0],[0,255],{matrixType:"box",x:-60,y:-60,w:200,h:h + 120,r:0});
      csRoundRect(g,2,2,w - 4,h - 4,16);
      g.endFill();
   }
   else
   {
      csGradientRect(g,0,0,w,h,18,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : CS_RIM,over ? 100 : 40,over ? 2 : 1.5);
   }
   csGloss(g,4,2,w - 8,h * 0.45,14,add ? 10 : 16);

   // The medallion: the hero's initial (or a plus) on frosted glass.
   var m = row.body.medal;
   m.bg.clear();
   if(add)
   {
      m.bg.lineStyle(2,0xFFF3C4,100);
      m.bg.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-21,y:-21,w:42,h:42,r:Math.PI / 2});
      csCircle(m.bg,0,0,21);
      m.bg.endFill();
      m.bg.lineStyle();
      m.bg.beginFill(CS_INK,100);
      csRoundRect(m.bg,-10,-3,20,6,3);
      csRoundRect(m.bg,-3,-10,6,20,3);
      m.bg.endFill();
   }
   else
   {
      m.bg.lineStyle(sel ? 3 : 2,sel ? CS_GOLD : 0xFFFFFF,sel ? 100 : 85);
      m.bg.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:-28,y:-32,w:56,h:56,r:0});
      csCircle(m.bg,0,0,21);
      m.bg.endFill();
   }
   csGloss(m.bg,-14,-19,28,13,6,add ? 45 : 60);

   // The chosen card's gold check; the rest get a soft chevron.
   var c = row.body.mark;
   c.clear();
   c._x = w - 26;
   if(sel)
   {
      c.lineStyle(1.5,0xFFF3C4,100);
      c.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-13,y:-13,w:26,h:26,r:Math.PI / 2});
      csCircle(c,0,0,13);
      c.endFill();
      c.lineStyle(3.5,CS_INK,100);
      c.moveTo(-6,0);
      c.lineTo(-2,5);
      c.lineTo(6,-5);
   }
   else
   {
      c.lineStyle(3,over ? 0xFFE08A : CS_SOFT,over ? 100 : 45);
      c.moveTo(-3,-7);
      c.lineTo(4,0);
      c.lineTo(-3,7);
   }

   row.body.name_txt._width = w - 120;
   row.body.sub_txt._width = w - 120;
   if(add)
   {
      row.body.sub_txt.text = "Start a brand-new adventure";
      csSetColor(row.body.sub_txt,CS_SOFT);
   }
   else if(sel)
   {
      row.body.sub_txt.text = "Ready to play!";
      csSetColor(row.body.sub_txt,0xFFE08A);
   }
   else
   {
      row.body.sub_txt.text = "Click to choose";
      csSetColor(row.body.sub_txt,CS_SOFT);
   }

   if(sel)
   {
      row.body.filters = [new flash.filters.GlowFilter(CS_GOLD,over ? 0.6 : 0.4,14,14,2,2)];
   }
   else if(over)
   {
      row.body.filters = [new flash.filters.GlowFilter(CS_GOLD,0.5,12,12,2,2)];
   }
   else
   {
      row.body.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
   }
}

function csMakeRow(index)
{
   var d = characterData[index];
   var add = d.state == "add";
   var rows = csFrame.list.rows;
   var row = rows.createEmptyMovieClip("row" + index,rows.getNextHighestDepth());
   row.index = index;
   row._y = index * (CS_ROW_H + CS_ROW_GAP);
   row.body = row.createEmptyMovieClip("body",1);
   row.body.bg = row.body.createEmptyMovieClip("bg",1);

   var m = row.body.medal = row.body.createEmptyMovieClip("medal",2);
   m._x = 34;
   m._y = CS_ROW_H / 2;
   m.bg = m.createEmptyMovieClip("bg",1);
   if(!add)
   {
      var initial = String(d.name).length > 0 ? String(d.name).charAt(0).toUpperCase() : "?";
      csText(m,"initial_txt",initial,-21,-15,42,30,22,0x1D307A);
   }
   row.body.mark = row.body.createEmptyMovieClip("mark",3);
   row.body.mark._y = CS_ROW_H / 2;

   var name = csText(row.body,"name_txt","",66,8,200,28,20,add ? 0xFFE08A : 0xFFFFFF,"left");
   name.filters = csOutline();
   // The stock row shows the character's id; the card shows the name.
   csSetFitted(name,add ? "Create a New Hero" : (String(d.name).length > 0 ? String(d.name) : String(d.id)));
   var sub = csText(row.body,"sub_txt","",66,34,200,20,13,CS_SOFT,"left");
   sub.filters = [new flash.filters.DropShadowFilter(1,90,0x0A1030,0.9,2,2,1,1)];

   row.onRollOver = function()
   {
      this.over = true;
      csDrawRow(this);
      fscommand("FR_event","PlaySound(DockOver)");
      gs.TweenLite.to(this.body,0.3,{_x:5,ease:mx.transitions.easing.Strong.easeOut});
      gs.TweenLite.to(this.body.medal,0.8,{_xscale:112,_yscale:112,_rotation:this.index == csSelected ? 0 : -6,ease:csElasticOut});
   };
   row.onRollOut = row.onReleaseOutside = function()
   {
      this.over = false;
      csDrawRow(this);
      gs.TweenLite.to(this.body,0.3,{_x:0,ease:mx.transitions.easing.Strong.easeOut});
      gs.TweenLite.to(this.body.medal,0.8,{_xscale:100,_yscale:100,_rotation:0,ease:csElasticOut});
   };
   row.onRelease = function()
   {
      fscommand("FR_event","PlaySound(DockSelect)");
      csSelect(this.index,true);
   };

   csDrawRow(row);
   // Slides in from the right, one after another as Lua adds them.
   row.body._alpha = 0;
   row.body._x = 30;
   gs.TweenLite.to(row.body,0.45,{_alpha:100,_x:0,delay:0.05 + index * 0.06,ease:mx.transitions.easing.Strong.easeOut});
   csRows.push(row);
}

function csRedrawRows()
{
   var i;
   for(i = 0; i < csRows.length; i++)
   {
      csDrawRow(csRows[i]);
   }
}

// What a click on a stock row does (CharacterItemRenderer._handleMouseClick), with the stock list's selection
// kept in step so a redraw of it picks the same character.
function csSelect(index, send)
{
   var d = characterData[index];
   if(d == undefined)
   {
      return;
   }
   csSelected = index;
   characterList.selectedIndex = index;
   setCharacterId(d.id);
   setButtonState(d.state);
   if(send)
   {
      fscommand("FR_event","SelectCharacter(" + d.id + ")");
   }
   csRedrawRows();
}

function csUpdateSubtitle()
{
   var n = 0;
   var i;
   for(i = 0; i < characterData.length; i++)
   {
      if(characterData[i].state == "character")
      {
         n++;
      }
   }
   csFrame.subtitle.text = n == 0 ? "Make your very first hero" : (n == 1 ? "1 hero ready for adventure" : n + " heroes ready for adventure");
}

// --- scrolling -------------------------------------------------------------------------

function csContentH()
{
   return characterData.length * (CS_ROW_H + CS_ROW_GAP) - CS_ROW_GAP;
}

function csMaxScroll()
{
   return Math.max(0,csContentH() - (CS_LIST_H - 2 * CS_PAD));
}

function csBuildScrollbar()
{
   var sb = csFrame.scrollbar = csFrame.createEmptyMovieClip("scrollbar",csFrame.getNextHighestDepth());
   sb._x = CS_LIST_X + CS_LIST_W - 14;
   sb._y = CS_LIST_Y + 14;
   sb.trackH = CS_LIST_H - 28;
   sb.track = sb.createEmptyMovieClip("track",1);
   sb.thumb = sb.createEmptyMovieClip("thumb",2);
   sb.thumb.onPress = function()
   {
      this.grab = this._ymouse;
      this.dragging = true;
   };
   sb.thumb.onRelease = sb.thumb.onReleaseOutside = function()
   {
      this.dragging = false;
   };
   sb.thumb.onMouseMove = function()
   {
      if(!this.dragging)
      {
         return;
      }
      var sb = this._parent;
      var room = sb.trackH - this.h;
      if(room <= 0)
      {
         return;
      }
      var y = Math.max(0,Math.min(room,sb._ymouse - this.grab));
      csScrollTo(y / room * csMaxScroll(),false);
   };
   sb._visible = false;
}

function csDrawScrollbar()
{
   var sb = csFrame.scrollbar;
   var max = csMaxScroll();
   sb._visible = max > 0;
   if(max <= 0)
   {
      return;
   }
   var view = CS_LIST_H - 2 * CS_PAD;
   var h = Math.max(36,sb.trackH * view / csContentH());
   sb.thumb.h = h;
   sb.track.clear();
   sb.track.lineStyle();
   sb.track.beginFill(0x0A1440,60);
   csRoundRect(sb.track,0,0,6,sb.trackH,3);
   sb.track.endFill();
   sb.thumb.clear();
   sb.thumb.lineStyle(1,0xFFF3C4,90);
   sb.thumb.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-1,y:0,w:8,h:h,r:0});
   csRoundRect(sb.thumb,-1,0,8,h,4);
   sb.thumb.endFill();
   sb.thumb._y = (sb.trackH - h) * csScroll / max;
}

function csScrollTo(y, tween)
{
   var max = csMaxScroll();
   csScroll = Math.max(0,Math.min(max,y));
   var rows = csFrame.list.rows;
   if(tween)
   {
      gs.TweenLite.to(rows,0.35,{_y:- csScroll,ease:mx.transitions.easing.Strong.easeOut,onUpdate:csClipRows});
   }
   else
   {
      rows._y = - csScroll;
   }
   csClipRows();
   csDrawScrollbar();
}

// Cards scrolled right out of the recess are hidden, so they can't take clicks outside it.
function csClipRows()
{
   var view = CS_LIST_H - 2 * CS_PAD;
   var top = - csFrame.list.rows._y;
   var i;
   for(i = 0; i < csRows.length; i++)
   {
      var r = csRows[i];
      r._visible = r._y + CS_ROW_H > top - 4 && r._y < top + view + 4;
   }
}

var csWheelListener = new Object();
csWheelListener.onMouseWheel = function(delta)
{
   if(csMaxScroll() <= 0 || !csFrame.panel.hitTest(_root._xmouse,_root._ymouse,true))
   {
      return;
   }
   csScrollTo(csScroll + (delta > 0 ? -1 : 1) * (CS_ROW_H + CS_ROW_GAP),true);
};

// --- Lua and stock hooks ---------------------------------------------------------------

// Lua's Populate adds each character, then the "add" entry. The stock list selects the first after each one.
var csAddCharacter = addCharacter;
addCharacter = function(index, id, name, state)
{
   csAddCharacter(index,id,name,state);
   csMakeRow(characterData.length - 1);
   csUpdateSubtitle();
   // What the stock list's first row does once it's drawn selected (it doesn't send SelectCharacter).
   csSelected = 0;
   setCharacterId(characterData[0].id);
   setButtonState(characterData[0].state);
   csRedrawRows();
   csScrollTo(0,false);
};

var csSetButtonState = setButtonState;
setButtonState = function(s)
{
   csSetButtonState(s);
   csSyncButtons();
};

// --- stock clips -----------------------------------------------------------------------

function csBuild()
{
   csBuildFrame();
   csUpdateSubtitle();
   characterList._visible = false;
   // The renderer the movie keeps on its stage (off to the right) for its class.
   var n;
   for(n in _root)
   {
      if(_root[n] instanceof controls.CharacterItemRenderer && _root[n]._parent == _root)
      {
         _root[n]._visible = false;
      }
   }
   Mouse.addListener(csWheelListener);
}

csBuild();
// ===== End Sanctuary Character Select =====

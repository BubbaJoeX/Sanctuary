// ===== Sanctuary Character Name =====
// Appended to characterNameScreen's main timeline by build_client_mods.py.
// Restyles the name picker (the last step of making a character: three wheels of first names, last-name starts
// and last-name ends) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass frame with
// a gold "Name Your Hero" title, the chosen name in gold on a glass name plate, the wheels as navy glass columns
// with a gold band across the chosen row (soft blue names fading toward the edges), gold arrow circles that pop
// on hover, and Back / Randomize / Next as a glass pill, a gold-rimmed glass pill and a gold pill. In the custom
// name only mode (setCustomNameOnly_lua: no wheels, the name typed in) the frame shrinks around the plate and
// buttons the same way the stock panel does. The stock clips keep their roles: the arrows' onPress, the buttons
// (invisible hit areas over the pills, still sending backButtonClick/nextButtonClick) and the name field.
// Nothing here needs the world to be loaded.

var CN_W = 600;
var CN_H = 525;
var CN_PLATE_Y = 94;
var CN_PLATE_H = 56;
var CN_GOLD = 0xFFC531;
var CN_RIM = 0x9DB8FF;
var CN_SOFT = 0xC9D6FF;
var CN_INK = 0x3A2400;

var cnFrame;
// Custom names only (no wheels): the wheels show until Lua says otherwise (the stock flag starts false).
var cnCustomOnly = false;

// --- drawing helpers -------------------------------------------------------------------

function cnRoundRect(mc, x, y, w, h, r)
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

function cnGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   cnRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function cnGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   cnRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function cnCircle(mc, x, y, r)
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

function cnSparkle(mc, x, y, r, alpha)
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

function cnText(mc, name, txt, x, y, w, h, size, color, align)
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

// Recolors a stock field, now and for any text set later.
function cnRecolor(t, color, size)
{
   var f = t.getTextFormat();
   f.color = color;
   if(size != undefined)
   {
      f.size = size;
   }
   t.setTextFormat(f);
   t.setNewTextFormat(f);
}

function cnOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Penner's elastic ease (the movie only has Strong compiled in).
function cnElasticOut(t, b, c, d)
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

// The panel, sized for the wheels (or, with a custom name only, just the plate and buttons).
function cnDrawFrame()
{
   var h = cnCustomOnly ? 250 : CN_H - 16;
   var bg = cnFrame.bg;
   bg.clear();
   cnGradientRect(bg,8,8,CN_W - 16,h,30,0x2E4FB0,0x141F55,100,CN_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-180,y:-240,w:700,h:500,r:0});
   cnRoundRect(bg,10,10,CN_W - 20,h - 4,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   cnRoundRect(bg,12,12,CN_W - 24,72,26);
   bg.endFill();
   var s = 2828;
   var i;
   for(i = 0; i < 20; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 30 + s % (CN_W - 60);
      s = (s * 1103515245 + 12345) % 2147483648;
      cnSparkle(bg,x,20 + s % 56,1 + s % 3,25 + s % 50);
   }

   // The name plate, with a little sparkle at each end.
   cnGradientRect(bg,56,CN_PLATE_Y,CN_W - 112,CN_PLATE_H,CN_PLATE_H / 2,0x0A1440,0x14246A,70,CN_GOLD,80,2);
   cnGloss(bg,64,CN_PLATE_Y + 2,CN_W - 128,CN_PLATE_H * 0.4,14,14);
   cnSparkle(bg,78,CN_PLATE_Y + CN_PLATE_H / 2,4,80);
   cnSparkle(bg,CN_W - 78,CN_PLATE_Y + CN_PLATE_H / 2,4,80);

   cnFrame.wheels._visible = !cnCustomOnly;
   cnFrame.subtitle_txt.text = cnCustomOnly ? "Type the name your hero will go by" : "Spin the wheels, or roll the dice for a surprise";
}

// The wheels: a glass column under each, a gold band across the chosen row, and gold arrow circles.
function cnDrawWheels()
{
   var w = cnFrame.wheels;
   w.clear();
   var i;
   for(i = 1; i <= 3; i++)
   {
      var col = _root["nameColumn" + i];
      var x = col._x;
      var y = col._y;
      cnGradientRect(w,x - 6,y - 6,133,149,18,0x0A1440,0x14246A,60,CN_RIM,35,1.5);
      // The chosen row (slot2, at 56 in the column).
      w.lineStyle(2,CN_GOLD,90);
      w.beginGradientFill("linear",[0x3F66D6,0x1D307A],[100,100],[0,255],{matrixType:"box",x:x,y:y + 52,w:121,h:34,r:Math.PI / 2});
      cnRoundRect(w,x - 2,y + 52,125,34,12);
      w.endFill();
      cnGloss(w,x + 2,y + 54,117,13,6,22);
   }
}

function cnDrawArrow(a, over)
{
   var c = a.sanctuaryCircle;
   c.clear();
   c.lineStyle(1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-17,y:-17,w:34,h:34,r:Math.PI / 2});
   cnCircle(c,0,0,17);
   c.endFill();
   cnGloss(c,-11,-14,22,11,5,50);
   c.lineStyle(4,CN_INK,100);
   var up = a.down ? -1 : 1;
   c.moveTo(-7,3 * up);
   c.lineTo(0,-4 * up);
   c.lineTo(7,3 * up);
   c.filters = over ? [new flash.filters.GlowFilter(CN_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

// The stock arrows stay as the hit areas (their onPress spins the wheel); the circle sits under each.
function cnStyleArrow(a, down)
{
   var b = a.getBounds(_root);
   var c = cnFrame.wheels.createEmptyMovieClip(a._name + "Circle",cnFrame.wheels.getNextHighestDepth());
   c._x = (b.xMin + b.xMax) / 2;
   c._y = (b.yMin + b.yMax) / 2;
   a.sanctuaryCircle = c;
   a.down = down;
   a._alpha = 0;
   cnDrawArrow(a,false);
   a.onRollOver = function()
   {
      cnDrawArrow(this,true);
      gs.TweenLite.to(this.sanctuaryCircle,0.6,{_xscale:118,_yscale:118,ease:cnElasticOut});
   };
   a.onRollOut = a.onReleaseOutside = a.onDragOut = function()
   {
      cnDrawArrow(this,false);
      gs.TweenLite.to(this.sanctuaryCircle,0.25,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Strong.easeOut});
   };
}

function cnStyleColumn(col)
{
   var i;
   for(i = 0; i <= 4; i++)
   {
      var t = col["slot" + i];
      if(i == 2)
      {
         cnRecolor(t,0xFFE08A,18);
         t.filters = cnOutline();
      }
      else
      {
         cnRecolor(t,CN_SOFT);
         t._alpha = i == 1 || i == 3 ? 80 : 45;
      }
   }
}

function cnBuildFrame()
{
   var f = cnFrame = _root.createEmptyMovieClip("sanctuaryCharacterName",-16384);
   f.bg = f.createEmptyMovieClip("bg",1);
   f.bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   f.wheels = f.createEmptyMovieClip("wheels",2);
   var title = cnText(f,"title_txt","Name Your Hero",0,14,CN_W,40,28,0xFFE08A);
   title.filters = cnOutline();
   cnText(f,"subtitle_txt","",0,50,CN_W,24,14,CN_SOFT);

   background._visible = false;
   nameWheelBGClip._visible = false;
   neonBGClip._visible = false;
   // The stars over the name (its last frame shows the name, so it keeps playing, unseen).
   nameBarBG._alpha = 0;
   cnRecolor(nameField,0xFFE08A,26);
   nameField.filters = cnOutline();
   nameField._y = CN_PLATE_Y + (CN_PLATE_H - nameField._height) / 2 + 2;

   var i;
   for(i = 1; i <= 3; i++)
   {
      cnStyleColumn(_root["nameColumn" + i]);
      var box = _root["letterBox" + i];
      cnRecolor(box.txtLetter,0xFFE08A);
      box.txtLetter.filters = [new flash.filters.GlowFilter(0x0A1030,0.9,6,6,4,2)];
   }
   cnDrawWheels();
   cnStyleArrow(firstNameUpArrow,false);
   cnStyleArrow(lastNamePreUpArrow,false);
   cnStyleArrow(lastNamePostUpArrow,false);
   cnStyleArrow(firstNameDownArrow,true);
   cnStyleArrow(lastNamePreDownArrow,true);
   cnStyleArrow(lastNamePostDownArrow,true);
   cnDrawFrame();
}

// --- Back / Randomize / Next -----------------------------------------------------------

// Fits a stock button (an invisible hit area from now on) over the pill drawn for it.
function cnCoverButton(btn, x, y, w, h)
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
function cnDrawPill(pill, over, down)
{
   var w = pill.w;
   var h = pill.h;
   var g = pill.bg;
   var on = pill.btn.isEnabled();
   g.clear();
   if(pill.kind == "gold")
   {
      g.lineStyle(1.5,0xFFF3C4,100);
      g.beginGradientFill("linear",over && on ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- w / 2,y:- h / 2,w:w,h:h,r:Math.PI / 2});
   }
   else
   {
      var rim = pill.kind == "rimmed" ? CN_GOLD : (over ? 0xFFE08A : CN_RIM);
      g.lineStyle(pill.kind == "rimmed" ? 2 : 1.5,rim,pill.kind == "rimmed" || over ? 100 : 60);
      g.beginGradientFill("linear",over ? [0x3F66D6,0x1D307A] : [0x284A9E,0x162660],[100,100],[0,255],{matrixType:"box",x:- w / 2,y:- h / 2,w:w,h:h,r:Math.PI / 2});
   }
   cnRoundRect(g,- w / 2,- h / 2,w,h,h / 2);
   g.endFill();
   cnGloss(g,- w / 2 + 8,- h / 2 + 2,w - 16,h * 0.42,h * 0.2,pill.kind == "gold" ? 55 : 25);
   pill.body._y = down ? 2 : 0;
   // Next waits for a long enough custom name (enable(false) dims the stock button).
   pill._alpha = on ? 100 : 40;
   if(over && on)
   {
      pill.filters = [new flash.filters.GlowFilter(CN_GOLD,0.8,16,16,2,2)];
   }
   else
   {
      pill.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
   }
}

function cnMakePill(btn, name, kind, label, glyph)
{
   var w = 144;
   var h = 48;
   var pill = cnFrame.createEmptyMovieClip(name,cnFrame.getNextHighestDepth());
   pill.kind = kind;
   pill.w = w;
   pill.h = h;
   pill.btn = btn;
   btn.sanctuaryPill = pill;
   pill.bg = pill.createEmptyMovieClip("bg",1);
   pill.body = pill.createEmptyMovieClip("body",2);
   var color = kind == "gold" ? CN_INK : 0xFFFFFF;
   var shift = glyph == "right" ? -10 : 10;
   var t = cnText(pill.body,"label_txt",label,- w / 2 + shift,-14,w,30,19,color);
   if(kind != "gold")
   {
      t.filters = cnOutline();
   }
   var gl = pill.body.createEmptyMovieClip("glyph",pill.body.getNextHighestDepth());
   var gc = kind == "gold" ? CN_INK : 0xFFE08A;
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
      cnRoundRect(gl,-8,-8,16,16,4);
      gl.endFill();
      gl.lineStyle();
      gl.beginFill(gc,100);
      cnCircle(gl,-3.5,-3.5,1.6);
      cnCircle(gl,3.5,-3.5,1.6);
      cnCircle(gl,0,0,1.6);
      cnCircle(gl,-3.5,3.5,1.6);
      cnCircle(gl,3.5,3.5,1.6);
      gl.endFill();
   }
   cnPlacePill(pill);

   // Dragging off a pressed button only resets its art (no hover-out event), so the pill hears it here.
   btn.onDragOut = function()
   {
      FRIconButton.prototype.onDragOut.call(this);
      cnHoverPill(this,false);
   };
   return pill;
}

// Centers the pill on its stock button (which custom-name mode moves) and fits the button over it.
function cnPlacePill(pill)
{
   var btn = pill.btn;
   btn._xscale = btn._yscale = 100;
   var b = btn.getBounds(_root);
   pill._x = b.xMax > b.xMin ? Math.round((b.xMin + b.xMax) / 2) : CN_W / 2;
   pill._y = b.yMax > b.yMin ? Math.round((b.yMin + b.yMax) / 2) : 440;
   cnCoverButton(btn,pill._x - pill.w / 2,pill._y - pill.h / 2,pill.w,pill.h);
   pill._visible = btn._visible;
   cnDrawPill(pill,false,false);
}

function cnBuildButtons()
{
   cnFrame.back = cnMakePill(backButton_mc,"back","glass","Back","left");
   cnFrame.random = cnMakePill(randomButton_mc,"random","rimmed","Randomize","die");
   cnFrame.next = cnMakePill(nextButton_mc,"next","gold","Next","right");
}

function cnHoverPill(btn, over)
{
   var pill = btn.sanctuaryPill;
   if(pill == undefined)
   {
      return;
   }
   cnDrawPill(pill,over,false);
   var scale = over && btn.isEnabled() ? 106 : 100;
   gs.TweenLite.to(pill,over ? 0.7 : 0.25,{_xscale:scale,_yscale:scale,ease:over ? cnElasticOut : mx.transitions.easing.Strong.easeOut});
}

// The buttons broadcast these to _root, their listener.
function onHoverOver(btn)
{
   cnHoverPill(btn,true);
}

function onHoverOut(btn)
{
   cnHoverPill(btn,false);
}

function onButtonPress(btn)
{
   if(btn.sanctuaryPill != undefined)
   {
      cnDrawPill(btn.sanctuaryPill,true,true);
   }
}

function onButtonRelease(btn)
{
   if(btn.sanctuaryPill != undefined)
   {
      cnDrawPill(btn.sanctuaryPill,btn.hitTest(_root._xmouse,_root._ymouse,true),false);
   }
}

// enable() fades the stock button to 50%; it stays an invisible hit area and its pill dims instead.
var cnEnable = FRIconButton.prototype.enable;
FRIconButton.prototype.enable = function(is)
{
   cnEnable.call(this,is);
   if(this.sanctuaryPill != undefined)
   {
      this._alpha = 0;
      cnDrawPill(this.sanctuaryPill,false,false);
   }
};

// --- Lua -------------------------------------------------------------------------------

// Custom names only: the stock code hides the wheels and moves the buttons up; the frame and pills follow.
var cnSetCustomNameOnly = setCustomNameOnly_lua;
setCustomNameOnly_lua = function(isCustomNum)
{
   cnSetCustomNameOnly(isCustomNum);
   cnCustomOnly = !m_isNotCustomOnly;
   cnDrawFrame();
   cnPlacePill(cnFrame.back);
   cnPlacePill(cnFrame.random);
   cnPlacePill(cnFrame.next);
   cnRecolor(nameField,0xFFE08A,26);
   // The name is typed here, so it shows from the start.
   if(cnCustomOnly)
   {
      nameField._alpha = 100;
   }
};

cnBuildFrame();
cnBuildButtons();
// ===== End Sanctuary Character Name =====

// ===== Sanctuary Name Change =====
// Appended to CharacterNameChange's main timeline by build_client_mods.py.
// Redraws the character rename dialog (wndCharacterNameChange: pick a first and last name, Check it, then Submit,
// or wait while a rename is pending) in the profile card's style (ClientMods/aboutme/profile_card.as): a navy glass
// card with sparkles and Lua's title in gold, a gold close circle, the name fields as recessed navy wells with white
// text (gold rimmed while typing), Check / Submit / OK as gold pills and Cancel as a glass pill, Lua's status line
// in gold on a recessed pill and the naming rules in soft blue in their own well. Renames are free, and this
// dialog never had a price. The stock components keep every role: the buttons stay where they were as invisible
// hit areas (their states, labels and disabled flags drive the pills), and the inputs keep their text, focus and IME.

var NC_W = 536;
var NC_H = 374;
var NC_GOLD = 0xFFC531;
var NC_RIM = 0x9DB8FF;
var NC_SOFT = 0xC9D6FF;
var NC_INK = 0x3A2400;

var ncRoot;

// --- drawing helpers -------------------------------------------------------------------

function ncRoundRect(mc, x, y, w, h, r)
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

function ncGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   ncRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function ncCircle(mc, x, y, r)
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

function ncText(mc, name, txt, x, y, w, h, size, color, align)
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
function ncSetFitted(t, txt)
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

function ncSparkle(mc, x, y, r, alpha)
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

function ncOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function ncShadow()
{
   return [new flash.filters.DropShadowFilter(1,90,0x0A1030,0.9,2,2,1,1)];
}

function ncGoldFill(mc, x, y, w, h, over)
{
   mc.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
}

// Penner's elastic ease out; this movie only carries mx.transitions.easing.Strong.
function ncElasticOut(t, b, c, d)
{
   if(t == 0)
   {
      return b;
   }
   t /= d;
   if(t == 1)
   {
      return b + c;
   }
   var p = d * 0.3;
   return c * Math.pow(2,-10 * t) * Math.sin((t * d - p / 4) * (2 * Math.PI) / p) + c + b;
}

// A gfx Button's look while the mouse is on it.
function ncIsOver(button)
{
   return button.state == "over" || button.state == "down" || button.state == "release";
}

// Recolours a stock field only when it has drifted (the components rewrite their text when they redraw).
function ncColor(t, color)
{
   if(t != undefined && t.textColor != color)
   {
      t.textColor = color;
   }
}

// --- frame -----------------------------------------------------------------------------

function ncDrawFrame(bg)
{
   ncGradientRect(bg,4,4,NC_W - 8,NC_H - 8,30,0x2E4FB0,0x141F55,100,NC_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:-120,y:-240,w:600,h:480,r:0});
   ncRoundRect(bg,6,6,NC_W - 12,NC_H - 12,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   ncRoundRect(bg,16,16,NC_W - 32,50,24);
   bg.endFill();

   // Sparkles in the header (fixed seed, same every time).
   var s = 2718;
   var i;
   for(i = 0; i < 16; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 30 + s % (NC_W - 110);
      s = (s * 1103515245 + 12345) % 2147483648;
      ncSparkle(bg,x,22 + s % 38,1 + s % 2,20 + s % 45);
   }

   // The content bay and a gold rule under the header.
   ncGradientRect(bg,16,78,NC_W - 32,NC_H - 94,22,0x0A1440,0x0A1440,38,NC_RIM,22,1);
   bg.lineStyle();
   bg.beginGradientFill("linear",[NC_GOLD,NC_GOLD,NC_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:40,y:0,w:NC_W - 80,h:2,r:0});
   ncRoundRect(bg,40,70,NC_W - 80,2,1);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
}

// The well behind a block of stock text (the status line and the naming rules), sized to its field.
function ncDrawWell(mc, field, show, alpha)
{
   mc.clear();
   if(!show || field == undefined || !field._visible)
   {
      return;
   }
   var b = field.getBounds(ncRoot);
   ncGradientRect(mc,b.xMin - 6,b.yMin - 4,b.xMax - b.xMin + 12,b.yMax - b.yMin + 8,14,0x0A1440,0x0A1440,alpha,NC_RIM,25,1);
}

// --- buttons ---------------------------------------------------------------------------

// A pill under a stock button (invisible above it, still taking the mouse): gold, or glass for Cancel.
function ncMakePill(name, gold)
{
   var p = ncRoot.pills.createEmptyMovieClip(name,ncRoot.pills.getNextHighestDepth());
   p.bg = p.createEmptyMovieClip("bg",1);
   p.gold = gold;
   p.label_txt = ncText(p,"label_txt","",0,0,10,24,17,gold ? NC_INK : 0xFFFFFF);
   if(!gold)
   {
      p.label_txt.filters = ncShadow();
   }
   p._visible = false;
   return p;
}

function ncDrawPill(p, w, h, over)
{
   var g = p.bg;
   var r = h / 2;
   g.clear();
   if(p.gold)
   {
      g.lineStyle(2,0xFFF3C4,100);
      ncGoldFill(g,-w / 2,-r,w,h,over);
   }
   else if(over)
   {
      g.lineStyle(2,0xFFE08A,100);
      g.beginGradientFill("linear",[0x3F66D6,0x1D307A],[100,100],[0,255],{matrixType:"box",x:-w / 2,y:-r,w:w,h:h,r:Math.PI / 2});
   }
   else
   {
      g.lineStyle(1.5,NC_RIM,60);
      g.beginGradientFill("linear",[0x284A9E,0x162660],[100,100],[0,255],{matrixType:"box",x:-w / 2,y:-r,w:w,h:h,r:Math.PI / 2});
   }
   ncRoundRect(g,-w / 2,-r,w,h,r);
   g.endFill();
   g.lineStyle();
   g.beginFill(0xFFFFFF,over ? 40 : 28);
   ncRoundRect(g,-w / 2 + 10,-r + 4,w - 20,h * 0.28,h * 0.14);
   g.endFill();
   if(over)
   {
      g.filters = [new flash.filters.GlowFilter(NC_GOLD,0.8,16,16,2,2)];
   }
   else
   {
      g.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,8,8,1,2)];
   }
   p.label_txt._x = -w / 2;
   p.label_txt._width = w;
   p.label_txt._y = -13;
}

// Follows a stock button: where it is, whether it shows, its label, hover and disabled state.
function ncSyncPill(p, btn)
{
   if(btn == undefined || btn._parent == undefined || !btn._visible)
   {
      p._visible = false;
      p.key = undefined;
      return;
   }
   btn._alpha = 0;
   p._visible = true;
   var b = btn.getBounds(ncRoot);
   var w = Math.round(b.xMax - b.xMin);
   var h = Math.min(40,Math.round(b.yMax - b.yMin));
   var over = ncIsOver(btn) && !btn.disabled;
   var label = btn.label == undefined ? "" : String(btn.label);
   var key = Math.round(b.xMin) + "," + Math.round(b.yMin) + "," + w + "," + h + "," + over + "," + btn.disabled + "," + label;
   if(key == p.key)
   {
      return;
   }
   p.key = key;
   p._x = Math.round((b.xMin + b.xMax) / 2);
   p._y = Math.round((b.yMin + b.yMax) / 2);
   ncDrawPill(p,w,h,over);
   ncSetFitted(p.label_txt,label);
   p._alpha = btn.disabled ? 45 : 100;
   if(over != p.over)
   {
      p.over = over;
      gs.TweenLite.to(p,0.3,{_xscale:over ? 106 : 100,_yscale:over ? 106 : 100,ease:mx.transitions.easing.Strong.easeOut});
   }
}

function ncBuildClose()
{
   var close = ncRoot.close = ncRoot.createEmptyMovieClip("close",ncRoot.getNextHighestDepth());
   close.draw = function(over)
   {
      this.clear();
      this.lineStyle(2,0xFFF3C4,100);
      ncGoldFill(this,-17,-17,34,34,over);
      ncCircle(this,0,0,17);
      this.endFill();
      this.lineStyle(4,NC_INK,100);
      this.moveTo(-6,-6);
      this.lineTo(6,6);
      this.moveTo(6,-6);
      this.lineTo(-6,6);
      this.filters = over ? [new flash.filters.GlowFilter(NC_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,6,6,1,2)];
   };
   close.draw(false);
   close._visible = false;
}

function ncSyncClose(btn)
{
   var close = ncRoot.close;
   if(btn == undefined || !btn._visible)
   {
      close._visible = false;
      return;
   }
   btn._alpha = 0;
   var b = btn.getBounds(ncRoot);
   close._x = Math.round((b.xMin + b.xMax) / 2);
   close._y = Math.round((b.yMin + b.yMax) / 2);
   close._visible = true;
   var over = ncIsOver(btn);
   if(over != close.over)
   {
      close.over = over;
      close.draw(over);
      gs.TweenLite.to(close,0.6,{_rotation:over ? 90 : 0,_xscale:over ? 112 : 100,_yscale:over ? 112 : 100,ease:ncElasticOut});
   }
}

// --- name fields -----------------------------------------------------------------------

// A name field: its white stock box (a sprite at its first depth) hidden, a recessed navy well under the text.
function ncSyncInput(input)
{
   if(input == undefined || input._parent == undefined)
   {
      return;
   }
   var box = input.getInstanceAtDepth(-16383);
   if(input.sanctuaryWell == undefined)
   {
      var b = box.getBounds(input);
      input.sanctuaryBox = {x:b.xMin,y:b.yMin,w:b.xMax - b.xMin,h:b.yMax - b.yMin};
      input.sanctuaryWell = input.createEmptyMovieClip("sanctuaryWell",-16384);
      input.textField.filters = ncShadow();
   }
   // The box is re-placed when the field changes state (default, focused, disabled).
   box._alpha = 0;
   ncColor(input.textField,0xFFFFFF);
   var focused = input.focused == true;
   var key = focused + "," + input.disabled;
   if(key == input.sanctuaryKey)
   {
      return;
   }
   input.sanctuaryKey = key;
   var r = input.sanctuaryBox;
   var g = input.sanctuaryWell;
   g.clear();
   g.lineStyle(focused ? 2 : 1.5,focused ? NC_GOLD : NC_RIM,focused ? 100 : 45);
   g.beginGradientFill("linear",[0x081030,0x14235E],[85,85],[0,255],{matrixType:"box",x:r.x,y:r.y,w:r.w,h:r.h,r:Math.PI / 2});
   ncRoundRect(g,r.x,r.y,r.w,r.h,Math.min(12,r.h / 2));
   g.endFill();
   g.filters = focused ? [new flash.filters.GlowFilter(NC_GOLD,0.45,10,10,2,2)] : [];
   input._alpha = input.disabled ? 55 : 100;
}

// --- sync ------------------------------------------------------------------------------

function ncSync()
{
   var view = _root.view;
   // The stock backdrop: a sprite at the view's first depth.
   view.getInstanceAtDepth(-16383)._alpha = 0;

   // Lua's title, in gold over the header.
   var title = view.lblWindowTitle;
   if(title != undefined && title._visible)
   {
      title._alpha = 0;
      if(ncRoot.title_txt.text != title.text)
      {
         ncSetFitted(ncRoot.title_txt,title.text);
      }
      ncRoot.title_txt._visible = true;
   }
   else
   {
      // The pending page has no title of its own.
      ncRoot.title_txt._visible = false;
   }

   var labels = [view.lblFirstName,view.lblLastName];
   var i;
   for(i = 0; i < labels.length; i++)
   {
      if(labels[i] != undefined)
      {
         ncColor(labels[i].textField,NC_SOFT);
         labels[i].textField.filters = ncShadow();
      }
   }
   ncSyncInput(view.tfFirstName);
   ncSyncInput(view.tfLastName);

   // Lua's status line ("checking", "in use", "available"...), gold on a recessed pill.
   var status = view.lblStatusString;
   var statusText = status != undefined && status._visible ? String(status.text) : "";
   if(status != undefined)
   {
      status._alpha = 0;
   }
   if(ncRoot.status_txt.text != statusText)
   {
      ncRoot.status_txt._y = status._y + (status._height - 22) / 2;
      ncRoot.status_txt._x = status._x;
      ncRoot.status_txt._width = status._width;
      ncSetFitted(ncRoot.status_txt,statusText);
      var well = ncRoot.statusWell;
      well.clear();
      if(statusText.length > 0)
      {
         var t = ncRoot.status_txt;
         var w = Math.min(t._width,t.textWidth + 36);
         ncGradientRect(well,t._x + (t._width - w) / 2,t._y - 3,w,28,14,0x0A1440,0x0A1440,60,NC_GOLD,60,1);
      }
   }

   // The naming rules, and the pending page's message (both read-only text areas with black stock text).
   var rules = view.tfDisclaimer;
   if(rules != undefined)
   {
      // A little lower than stock, clear of the Cancel pill.
      rules._y = 208;
      ncColor(rules.textField,NC_SOFT);
      rules.textField.filters = ncShadow();
   }
   if(rules != ncRoot.rules)
   {
      ncRoot.rules = rules;
      ncDrawWell(ncRoot.rulesWell,rules,rules != undefined,45);
   }
   var pending = view.tfNameSubmittedPending;
   if(pending != undefined)
   {
      ncColor(pending.textField,0xFFE08A);
      pending.textField.filters = ncOutline();
   }

   ncSyncPill(ncRoot.check,view.btnCheck);
   ncSyncPill(ncRoot.submit,view.btnSubmit);
   ncSyncPill(ncRoot.cancel,view.btnCancel);
   ncSyncPill(ncRoot.ok,view.btnOk);
   ncSyncClose(view.btnClose);
}

function ncBuild()
{
   var view = _root.view;
   // Has no mouse handlers, so the mouse goes through it to the stock clips over it.
   ncRoot = view.createEmptyMovieClip("sanctuaryNameChange",-16384);
   ncDrawFrame(ncRoot.createEmptyMovieClip("bg",1));
   ncRoot.statusWell = ncRoot.createEmptyMovieClip("statusWell",2);
   ncRoot.rulesWell = ncRoot.createEmptyMovieClip("rulesWell",3);
   ncRoot.pills = ncRoot.createEmptyMovieClip("pills",4);
   ncRoot.title_txt = ncText(ncRoot,"title_txt","",60,22,NC_W - 120,40,26,0xFFE08A);
   ncRoot.title_txt.filters = ncOutline();
   ncRoot.status_txt = ncText(ncRoot,"status_txt","",0,0,10,22,14,0xFFE08A);
   ncRoot.status_txt.filters = ncOutline();
   ncRoot.check = ncMakePill("check",true);
   ncRoot.submit = ncMakePill("submit",true);
   ncRoot.cancel = ncMakePill("cancel",false);
   ncRoot.ok = ncMakePill("ok",true);
   ncBuildClose();
   ncRoot.onEnterFrame = ncSync;
   ncSync();
}

ncBuild();
// ===== End Sanctuary Name Change =====

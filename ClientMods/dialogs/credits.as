// ===== Sanctuary credits =====
// Appended to credits' main timeline by build_client_mods.py.
// Redraws the scrolling credits (wndCredits, the system menu's Credits row) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass card with sparkles and the title in gold, the names rolling
// up through a recess (gold department headers and white names, outlined), and a gold close circle that closes it
// the way the stock button did (CloseWindow()). The stock frame, with its logo, goes invisible; the stock close
// button is hidden.

var CR_W = 400;
var CR_H = 560;
var CR_GOLD = 0xFFC531;
var CR_RIM = 0x9DB8FF;
var CR_SOFT = 0xC9D6FF;
var CR_INK = 0x3A2400;

var crFrame;

// --- drawing helpers -------------------------------------------------------------------

function crRoundRect(mc, x, y, w, h, r)
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

function crGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   crRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function crGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   crRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function crCircle(mc, x, y, r)
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

function crSparkle(mc, x, y, r, alpha)
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

function crText(mc, name, txt, x, y, w, h, size, color)
{
   mc.createTextField(name,mc.getNextHighestDepth(),x,y,w,h);
   var t = mc[name];
   t.embedFonts = true;
   t.selectable = false;
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = "center";
   t.setNewTextFormat(f);
   t.text = txt;
   return t;
}

function crOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function crStrongOut(t, b, c, d)
{
   t = t / d - 1;
   return c * (t * t * t * t * t + 1) + b;
}

// --- card ------------------------------------------------------------------------------

function crDrawClose(over)
{
   var c = crFrame.close.face;
   c.clear();
   c.lineStyle(1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-17,y:-17,w:34,h:34,r:Math.PI / 2});
   crCircle(c,0,0,17);
   c.endFill();
   crGloss(c,-11,-14,22,11,5,50);
   c.lineStyle(4,CR_INK,100);
   c.moveTo(-6,-6);
   c.lineTo(6,6);
   c.moveTo(6,-6);
   c.lineTo(-6,6);
   crFrame.close.filters = over ? [new flash.filters.GlowFilter(CR_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

function crBuild()
{
   background._alpha = 0;
   closeButton._visible = false;
   var f = crFrame = _root.createEmptyMovieClip("sanctuaryCredits",-16384);
   var bg = f.createEmptyMovieClip("bg",1);
   crGradientRect(bg,8,8,CR_W - 16,CR_H - 16,30,0x2E4FB0,0x141F55,100,CR_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-150,y:-260,w:560,h:560,r:0});
   crRoundRect(bg,10,10,CR_W - 20,CR_H - 20,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   crRoundRect(bg,12,12,CR_W - 24,82,26);
   bg.endFill();
   var s = 9090;
   var i;
   for(i = 0; i < 20; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 30 + s % (CR_W - 60);
      s = (s * 1103515245 + 12345) % 2147483648;
      crSparkle(bg,x,20 + s % 64,1 + s % 3,25 + s % 50);
   }
   // The names roll up through a recess, fading at its top and bottom edges.
   bg.lineStyle(1,CR_RIM,25);
   bg.beginFill(0x0A1440,40);
   crRoundRect(bg,22,106,CR_W - 44,CR_H - 128,22);
   bg.endFill();
   bg.lineStyle();
   bg.beginGradientFill("linear",[CR_GOLD,CR_GOLD,CR_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:40,y:0,w:CR_W - 80,h:2,r:0});
   crRoundRect(bg,40,99,CR_W - 80,2,1);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   var title = crText(f,"title_txt","Credits",0,20,CR_W,40,28,0xFFE08A);
   title.filters = crOutline();
   crText(f,"subtitle_txt","The team behind Free Realms",0,56,CR_W,24,14,CR_SOFT);
   content.filters = crOutline();

   // The gold close circle; the stock close button is hidden and this one sends the same event.
   var c = f.close = _root.createEmptyMovieClip("sanctuaryCreditsClose",_root.getNextHighestDepth());
   c._x = CR_W - 46;
   c._y = 46;
   c.face = c.createEmptyMovieClip("face",1);
   crDrawClose(false);
   c.onRollOver = function()
   {
      fscommand("FR_event","PlaySound(DockOver)");
      crDrawClose(true);
      gs.TweenLite.to(this,0.4,{_xscale:112,_yscale:112,_rotation:90,ease:crStrongOut});
   };
   c.onRollOut = c.onReleaseOutside = function()
   {
      crDrawClose(false);
      gs.TweenLite.to(this,0.25,{_xscale:100,_yscale:100,_rotation:0,ease:crStrongOut});
   };
   c.onRelease = function()
   {
      fscommand("FR_event","PlaySound(DockSelect)");
      CloseWindow();
   };
}

crBuild();
// ===== End Sanctuary credits =====

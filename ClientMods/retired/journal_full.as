// ===== Sanctuary journal full =====
// Appended to upgrade_dialog's main timeline by build_client_mods.py.
// The Upgrade dialog (wndUpgradeDialog) only ever opens when your Hero's Journal is full (Lua's
// NotifyNonmemberQuestLimitReached), to sell a membership for more quest slots. Membership no longer exists, but
// the news is still useful, so it's a compact card in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as)
// that says the journal is full, with a gold OK pill that closes it the way the stock close button did
// (CloseUpgradeDialog()). Lua's text (MoreQuestsBeMember) and the Upgrade badge are covered: the card is drawn over
// the whole stock dialog, opaque.

var UD_X = 8;
var UD_Y = 8;
var UD_W = 424;
var UD_H = 268;
var UD_GOLD = 0xFFC531;
var UD_RIM = 0x9DB8FF;
var UD_SOFT = 0xC9D6FF;
var UD_INK = 0x3A2400;

var udCard;

// --- drawing helpers -------------------------------------------------------------------

function udRoundRect(mc, x, y, w, h, r)
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

function udGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   udRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function udGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   udRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function udCircle(mc, x, y, r)
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

function udSparkle(mc, x, y, r, alpha)
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

function udText(mc, name, txt, x, y, w, h, size, color)
{
   mc.createTextField(name,mc.getNextHighestDepth(),x,y,w,h);
   var t = mc[name];
   t.embedFonts = true;
   t.selectable = false;
   t.multiline = true;
   t.wordWrap = true;
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = "center";
   t.setNewTextFormat(f);
   t.text = txt;
   return t;
}

function udOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// --- card ------------------------------------------------------------------------------

function udDrawOk(over)
{
   var g = udCard.ok.bg;
   g.clear();
   g.lineStyle(1.5,0xFFF3C4,100);
   g.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-80,y:-20,w:160,h:40,r:Math.PI / 2});
   udRoundRect(g,-80,-20,160,40,20);
   g.endFill();
   udGloss(g,-72,-18,144,16,8,50);
   udCard.ok.filters = over ? [new flash.filters.GlowFilter(UD_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
}

function udBuild()
{
   closeBtn._visible = false;
   member_mc._visible = false;

   udCard = _root.createEmptyMovieClip("sanctuaryJournalFull",_root.getNextHighestDepth());
   var bg = udCard.createEmptyMovieClip("bg",1);
   udGradientRect(bg,UD_X,UD_Y,UD_W,UD_H,28,0x2E4FB0,0x141F55,100,UD_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-120,y:-220,w:520,h:440,r:0});
   udRoundRect(bg,UD_X + 2,UD_Y + 2,UD_W - 4,UD_H - 4,26);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   udRoundRect(bg,UD_X + 4,UD_Y + 4,UD_W - 8,68,24);
   bg.endFill();
   var s = 2727;
   var i;
   for(i = 0; i < 20; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = UD_X + 24 + s % (UD_W - 48);
      s = (s * 1103515245 + 12345) % 2147483648;
      udSparkle(bg,x,UD_Y + 10 + s % 54,1 + s % 3,20 + s % 50);
   }
   bg.beginGradientFill("linear",[UD_GOLD,UD_GOLD,UD_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:UD_X + 40,y:0,w:UD_W - 80,h:2,r:0});
   udRoundRect(bg,UD_X + 40,UD_Y + 76,UD_W - 80,2,1);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   // A full journal: three gold quest pages in a frosted socket.
   var icon = udCard.createEmptyMovieClip("icon",2);
   icon._x = UD_X + 56;
   icon._y = UD_Y + 40;
   icon.lineStyle(2,0xFFE08A,100);
   icon.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:-34,y:-38,w:68,h:68,r:0});
   udCircle(icon,0,0,26);
   icon.endFill();
   var k;
   for(k = 0; k < 3; k++)
   {
      udGradientRect(icon,-14 + k * 4,-15 + k * 3,20,26,4,0xFFE08A,0xF5A623,100,0xB07A10,100,1);
   }
   icon.lineStyle(2,UD_INK,80);
   icon.moveTo(-2,2);
   icon.lineTo(10,2);
   icon.moveTo(-2,8);
   icon.lineTo(10,8);
   icon.filters = [new flash.filters.GlowFilter(UD_GOLD,0.45,12,12,2,2)];

   var title = udText(udCard,"title_txt","Your Journal Is Full",UD_X + 90,UD_Y + 22,UD_W - 180,40,26,0xFFE08A);
   title.filters = udOutline();
   var body = udText(udCard,"body_txt","You're on as many quests as your Hero's Journal can hold. Finish one, or drop one from the journal, to take on a new quest.",UD_X + 36,UD_Y + 98,UD_W - 72,90,17,0xFFFFFF);
   body.filters = udOutline();

   // OK closes it the way the stock close button did.
   var ok = udCard.ok = udCard.createEmptyMovieClip("ok",udCard.getNextHighestDepth());
   ok._x = UD_X + UD_W / 2;
   ok._y = UD_Y + UD_H - 44;
   ok.bg = ok.createEmptyMovieClip("bg",1);
   udText(ok,"label_txt","OK",-80,-13,160,28,19,UD_INK);
   udDrawOk(false);
   ok.onRollOver = function()
   {
      fscommand("FR_event","PlaySound(DockOver)");
      udDrawOk(true);
   };
   ok.onRollOut = ok.onReleaseOutside = function()
   {
      udDrawOk(false);
   };
   ok.onRelease = function()
   {
      fscommand("FR_event","PlaySound(DockSelect)");
      fscommand("FR_event","CloseUpgradeDialog()");
   };
}

udBuild();
// ===== End Sanctuary journal full =====

// ===== Sanctuary messages =====
// Appended to Communicator's main timeline by build_client_mods.py.
// Redraws the Communicator (wndCommunicator: friend requests, invites and system messages, each with Accept/Deny or
// a close button) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass frame with
// sparkles and a gold "Messages" title, the list in a recess, and white outlined text. The message rows, their
// portrait wells and the message count pill are repainted navy glass at build time and the close button gold
// (RESKINS), since their art lives on multi-frame timelines; the stock frame goes invisible.

var CM_W = 800;
var CM_H = 600;
var CM_GOLD = 0xFFC531;
var CM_RIM = 0x9DB8FF;
var CM_SOFT = 0xC9D6FF;

var cmView = mcCommunicatorView;

// --- drawing helpers -------------------------------------------------------------------

function cmRoundRect(mc, x, y, w, h, r)
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

function cmGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   cmRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function cmSparkle(mc, x, y, r, alpha)
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

function cmText(mc, name, txt, x, y, w, h, size, color)
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

function cmOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// --- frame -----------------------------------------------------------------------------

function cmBuild()
{
   cmView.bg._alpha = 0;
   // Under everything in the view (the stock frame is at depth 1), in the view's coordinates.
   var f = cmView.createEmptyMovieClip("sanctuaryFrame",-16384);
   var bg = f.createEmptyMovieClip("bg",1);
   cmGradientRect(bg,8,6,CM_W - 18,CM_H - 14,30,0x2E4FB0,0x141F55,100,CM_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-150,y:-300,w:900,h:700,r:0});
   cmRoundRect(bg,10,8,CM_W - 22,CM_H - 18,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   cmRoundRect(bg,12,10,CM_W - 26,70,26);
   bg.endFill();
   var s = 7272;
   var i;
   for(i = 0; i < 30; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 40 + s % (CM_W - 80);
      s = (s * 1103515245 + 12345) % 2147483648;
      cmSparkle(bg,x,18 + s % 56,1 + s % 3,25 + s % 50);
   }
   // The list's recess, around the list and its scrollbar.
   var lb = cmView.mcList.getBounds(cmView);
   var sb = cmView.mcScrollbar.getBounds(cmView);
   var rx = lb.xMax > lb.xMin ? Math.round(lb.xMin) - 12 : 32;
   var ry = lb.yMax > lb.yMin ? Math.round(lb.yMin) - 12 : 132;
   var rr = sb.xMax > sb.xMin ? Math.round(sb.xMax) + 10 : CM_W - 30;
   bg.lineStyle(1,CM_RIM,25);
   bg.beginFill(0x0A1440,40);
   cmRoundRect(bg,rx,ry,rr - rx,CM_H - 26 - ry,22);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   var title = cmText(f,"title_txt","Messages",0,18,CM_W,40,28,0xFFE08A);
   title.filters = cmOutline();
   cmText(f,"subtitle_txt","Requests, invites and news",0,52,CM_W,22,14,CM_SOFT);
   cmView.mcMessageCount.txtCount.filters = cmOutline();
}

// Message rows' text, outlined each time a row draws its message (its frames hold different text fields).
var cmProto = _global.CommunicatorListItemRenderer.prototype;
var cmDraw = cmProto.draw;
cmProto.draw = function()
{
   var r = cmDraw.apply(this,arguments);
   this.txtMsg.filters = cmOutline();
   return r;
};

cmBuild();
// ===== End Sanctuary messages =====

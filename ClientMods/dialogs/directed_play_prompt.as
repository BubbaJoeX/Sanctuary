// ===== Sanctuary guide prompt =====
// Appended to DirectedPlayPrompt's main timeline by build_client_mods.py.
// Redraws the guide's speech bubble (wndDirectedPlayPrompt: the new-player tips that drop in beside the guide's
// portrait) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass bubble with a tail toward
// the portrait and a gold hairline glint, and the tip in white outlined text. Lua's text is html whose default colour
// is a dark green made for the stock pale bubble; that colour becomes white and any highlight colours stay. The stock
// bubble goes invisible and the new one follows its size.

var DP_GOLD = 0xFFC531;
var DP_RIM = 0x9DB8FF;

var dpBubble;

// --- drawing helpers -------------------------------------------------------------------

function dpRoundRect(mc, x, y, w, h, r)
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

function dpGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   dpRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function dpGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   dpRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

// --- bubble ----------------------------------------------------------------------------

function dpDraw()
{
   var b = dialogBG_mc.getBounds(_root);
   var g = dpBubble;
   g.clear();
   if(!(b.xMax > b.xMin))
   {
      return;
   }
   var x = Math.round(b.xMin) + 2;
   var y = Math.round(b.yMin) + 2;
   var w = Math.round(b.xMax - b.xMin) - 14;
   var h = Math.max(40,Math.round(b.yMax - b.yMin) - 4);
   // The tail points at the portrait on the right, level with the bubble's first line.
   var ty = y + Math.min(26,h / 2);
   g.lineStyle(2.5,DP_RIM,80);
   g.beginFill(0x24419A,100);
   g.moveTo(x + w - 4,ty - 12);
   g.lineTo(x + w + 16,ty);
   g.lineTo(x + w - 4,ty + 12);
   g.endFill();
   dpGradientRect(g,x,y,w,h,Math.min(20,h / 2),0x2E4FB0,0x141F55,100,DP_RIM,80,2.5);
   dpGloss(g,x + 8,y + 2,w - 16,Math.min(18,h / 2),9,20);
   g.beginGradientFill("linear",[DP_GOLD,DP_GOLD,DP_GOLD],[0,70,0],[0,128,255],{matrixType:"box",x:x + 24,y:0,w:w - 48,h:2,r:0});
   dpRoundRect(g,x + 24,y + h - 6,w - 48,2,1);
   g.endFill();
   g.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.6,12,12,1,2)];
}

// The tip's default colour (#003300, for the pale stock bubble) becomes white; other colours stay.
function dpLighten()
{
   var h = dialog_txt.htmlText;
   var light = h.split("#003300").join("#FFFFFF");
   if(light != h)
   {
      dialog_txt.htmlText = light;
   }
   var f = dialog_txt.getNewTextFormat();
   f.color = 0xFFFFFF;
   dialog_txt.setNewTextFormat(f);
   dialog_txt.filters = [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

dialogBG_mc._alpha = 0;
dpBubble = _root.createEmptyMovieClip("sanctuaryBubble",-16384);
dpLighten();
dpDraw();

var dpSetDialog = setDialog_lua;
setDialog_lua = function(str)
{
   var r = dpSetDialog.apply(_root,arguments);
   dpLighten();
   dpDraw();
   return r;
};
// ===== End Sanctuary guide prompt =====

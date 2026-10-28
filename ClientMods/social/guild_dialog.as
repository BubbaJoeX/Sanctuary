// ===== Sanctuary Guild Dialog =====
// Appended to guildDialog's main timeline by build_client_mods.py.
// Redraws the guild dialog (wndGuildDialog: "Create a Guild!" / rename your guild, which sends the player on to
// pick the name) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass card with the
// guild crest in a gold-rimmed frosted medallion on its top edge, the title in gold, Lua's message in a recessed
// panel, a gold "Choose a Name" pill and a gold close circle in the corner. The two stock buttons (FRIconButton)
// keep their roles and callbacks: their art goes invisible, shrunk to the new shapes so those are their hit areas,
// and each state change redraws the new look. Lua's title and message are recoloured after every SetMode.

var GLD_W = 426;
// The card, under the stock backdrop's spot (y 80..420).
var GLD_X = 12;
var GLD_Y = 86;
var GLD_CW = 402;
var GLD_CH = 328;
var GLD_CREST_Y = GLD_Y;
var GLD_TITLE_Y = 128;
var GLD_BODY = [36,180,354,118];
var GLD_PILL_Y = 352;
var GLD_PILL_W = 210;
var GLD_PILL_H = 46;
var GLD_CLOSE_X = 384;
var GLD_CLOSE_Y = 116;
var GLD_GOLD = 0xFFC531;
var GLD_GOLD_LIGHT = 0xFFE08A;
var GLD_GOLD_DARK = 0xF5A623;
var GLD_RIM = 0x9DB8FF;
var GLD_SOFT = 0xC9D6FF;
var GLD_INK = 0x3A2400;

var gldFrame;

// --- drawing helpers -------------------------------------------------------------------

function gldRoundRect(mc, x, y, w, h, r)
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

function gldGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   gldRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function gldCircle(mc, x, y, r)
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

function gldText(mc, name, txt, x, y, w, h, size, color)
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

function gldSparkle(mc, x, y, r, alpha)
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

function gldOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function gldGoldFill(mc, x, y, w, h, over)
{
   mc.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB43C] : [GLD_GOLD_LIGHT,GLD_GOLD_DARK],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
}

// Puts a stock field in a box and shrinks its font from size until the text fits, in color.
function gldFit(t, x, y, w, h, size, color)
{
   t._x = x;
   t._y = y;
   t._width = w;
   t._height = h;
   var f = t.getTextFormat();
   f.size = size;
   f.color = color;
   f.align = "center";
   t.setTextFormat(f);
   while((t.textHeight > h - 4 || (!t.wordWrap && t.textWidth > w - 6)) && f.size > 10)
   {
      f.size -= 1;
      t.setTextFormat(f);
   }
}

// --- the card --------------------------------------------------------------------------

function gldDrawFrame(bg)
{
   gldGradientRect(bg,GLD_X,GLD_Y,GLD_CW,GLD_CH,30,0x2E4FB0,0x141F55,100,GLD_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:GLD_X - 120,y:GLD_Y - 200,w:520,h:420,r:0});
   gldRoundRect(bg,GLD_X + 2,GLD_Y + 2,GLD_CW - 4,GLD_CH - 4,28);
   bg.endFill();
   var s = 7310;
   var i;
   for(i = 0; i < 18; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = GLD_X + 24 + s % (GLD_CW - 48);
      s = (s * 1103515245 + 12345) % 2147483648;
      // Not over the medallion.
      if(Math.abs(x - GLD_W / 2) > 50)
      {
         gldSparkle(bg,x,GLD_Y + 10 + s % 50,1 + s % 2,25 + s % 45);
      }
   }
   // A gold rule under the title, and the message's recess.
   bg.beginGradientFill("linear",[GLD_GOLD,GLD_GOLD,GLD_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:GLD_X + 40,y:0,w:GLD_CW - 80,h:2,r:0});
   gldRoundRect(bg,GLD_X + 40,GLD_TITLE_Y + 38,GLD_CW - 80,2,1);
   bg.endFill();
   gldGradientRect(bg,GLD_BODY[0],GLD_BODY[1],GLD_BODY[2],GLD_BODY[3],18,0x0A1440,0x0A1440,40,GLD_RIM,22,1);
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
}

// The crest (the stock createGuild_icon) in a frosted medallion on the card's top edge.
function gldBuildCrest()
{
   var c = gldFrame.createEmptyMovieClip("crest",gldFrame.getNextHighestDepth());
   c._x = GLD_W / 2;
   c._y = GLD_CREST_Y;
   c.lineStyle();
   c.beginGradientFill("radial",[GLD_GOLD,GLD_GOLD],[45,0],[0,255],{matrixType:"box",x:-56,y:-56,w:112,h:112,r:0});
   gldCircle(c,0,0,56);
   c.endFill();
   c.lineStyle(3,GLD_GOLD,100);
   c.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:-44,y:-50,w:88,h:88,r:0});
   gldCircle(c,0,0,34);
   c.endFill();
   c.lineStyle(1,0xFFF3C4,80);
   gldCircle(c,0,0,38);
   c.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,8,8,1,2)];
   var icon = c.attachMovie("createGuild_icon","icon",1);
   icon._xscale = icon._yscale = 140;
   var b = icon.getBounds(c);
   icon._x -= (b.xMin + b.xMax) / 2;
   icon._y -= (b.yMin + b.yMax) / 2;
}

// --- buttons ---------------------------------------------------------------------------

// The stock buttons' art goes invisible (still their hit area, sized to the new shape); state changes redraw.
function gldHideStock(btn, x, y, w, h)
{
   var parts = [btn.up_mc,btn.over_mc,btn.down_mc];
   var i;
   for(i = 0; i < parts.length; i++)
   {
      parts[i]._alpha = 0;
      parts[i]._x = x;
      parts[i]._y = y;
      parts[i]._width = w;
      parts[i]._height = h;
   }
   // The close button's X icons: invisible and shrunk, so they don't add to its hit area.
   btn.m_icon_up._alpha = btn.m_icon_over._alpha = 0;
   btn.m_icon_up._xscale = btn.m_icon_up._yscale = btn.m_icon_over._xscale = btn.m_icon_over._yscale = 1;
}

function gldDrawPill(btn, state)
{
   var p = btn.gldSkin;
   var w = GLD_PILL_W;
   var h = GLD_PILL_H;
   var over = state != "up";
   var dy = state == "down" ? 2 : 0;
   p.bg.clear();
   p.bg.lineStyle(2,0xFFF3C4,100);
   gldGoldFill(p.bg,- w / 2,- h / 2 + dy,w,h,over);
   gldRoundRect(p.bg,- w / 2,- h / 2 + dy,w,h,h / 2);
   p.bg.endFill();
   p.bg.lineStyle();
   p.bg.beginFill(0xFFFFFF,over ? 40 : 30);
   gldRoundRect(p.bg,- w / 2 + 10,- h / 2 + 4 + dy,w - 20,13,6.5);
   p.bg.endFill();
   p.bg.filters = over ? [new flash.filters.GlowFilter(GLD_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,8,8,1,2)];
   p.label_txt._y = -12 + dy;
   if(over != p.over)
   {
      p.over = over;
      gs.TweenLite.to(p,0.3,{_xscale:over ? 106 : 100,_yscale:over ? 106 : 100,ease:mx.transitions.easing.Strong.easeOut});
   }
}

function gldDrawClose(btn, state)
{
   var c = btn.gldSkin;
   var over = state != "up";
   c.clear();
   c.lineStyle(2,0xFFF3C4,100);
   gldGoldFill(c,-17,-17,34,34,over);
   gldCircle(c,0,0,17);
   c.endFill();
   c.lineStyle(4,GLD_INK,100);
   c.moveTo(-6,-6);
   c.lineTo(6,6);
   c.moveTo(6,-6);
   c.lineTo(-6,6);
   c.filters = over ? [new flash.filters.GlowFilter(GLD_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,6,6,1,2)];
   if(over != c.over)
   {
      c.over = over;
      gs.TweenLite.to(c,0.6,{_rotation:over ? 90 : 0,_xscale:over ? 112 : 100,_yscale:over ? 112 : 100,ease:mx.transitions.easing.Elastic.easeOut});
   }
}

function gldBuildButtons()
{
   // The name button: a gold pill centered under the message, its own label (the stock one is a string key).
   var b = customNameBtn;
   b._x = GLD_W / 2;
   b._y = GLD_PILL_Y;
   gldHideStock(b,- GLD_PILL_W / 2,- GLD_PILL_H / 2,GLD_PILL_W,GLD_PILL_H);
   var p = b.gldSkin = b.createEmptyMovieClip("sanctuarySkin",-16384);
   p.bg = p.createEmptyMovieClip("bg",1);
   gldText(p,"label_txt","Choose a Name",- GLD_PILL_W / 2,-12,GLD_PILL_W,28,19,GLD_INK);
   b.gldDraw = gldDrawPill;

   // The close button: a gold circle in the card's corner (the stock one sat outside it, bottom right).
   var c = closebtn;
   c._x = GLD_CLOSE_X;
   c._y = GLD_CLOSE_Y;
   gldHideStock(c,-17,-17,34,34);
   c.gldSkin = c.createEmptyMovieClip("sanctuarySkin",-16384);
   c.gldDraw = gldDrawClose;

   b.gldDraw(b,"up");
   c.gldDraw(c,"up");
}

function gldPatchButtons()
{
   var states = [["setStateUp","up"],["setStateOver","over"],["setStateDown","down"]];
   var i;
   for(i = 0; i < states.length; i++)
   {
      gldWrapState(states[i][0],states[i][1]);
   }
}

function gldWrapState(name, state)
{
   var stock = FRIconButton.prototype[name];
   FRIconButton.prototype[name] = function()
   {
      var r = stock.apply(this,arguments);
      if(this.gldDraw != undefined)
      {
         this.m_icon_up._alpha = this.m_icon_over._alpha = 0;
         // A disabled button doesn't change state on hover.
         if(state == "up" || this.m_isEnabled)
         {
            this.gldDraw(this,state);
         }
      }
      return r;
   };
}

// --- text ------------------------------------------------------------------------------

// Lua's title and message, in the new boxes and colours (frSetTextPreserveWidth puts them back in the stock ones).
function gldStyleText()
{
   gldFit(header_txt,GLD_X + 60,GLD_TITLE_Y,GLD_CW - 120,34,26,GLD_GOLD_LIGHT);
   header_txt.filters = gldOutline();
   body_txt.multiline = body_txt.wordWrap = true;
   gldFit(body_txt,GLD_BODY[0] + 14,GLD_BODY[1] + 10,GLD_BODY[2] - 28,GLD_BODY[3] - 20,16,0xFFFFFF);
   // One or two lines sit in the middle of the recess.
   body_txt._y += Math.max(0,(body_txt._height - 4 - body_txt.textHeight) / 2);
   body_txt.filters = [new flash.filters.DropShadowFilter(1,90,0x0A1030,0.9,2,2,1,1)];
}

var gldSetMode = SetMode;
SetMode = function(mode, title, message)
{
   var r = gldSetMode.apply(_root,arguments);
   gldStyleText();
   return r;
};
var gldSetHeaderText = SetHeaderText;
SetHeaderText = function(str)
{
   var r = gldSetHeaderText.apply(_root,arguments);
   gldStyleText();
   return r;
};
var gldSetBodyText = SetBodyText;
SetBodyText = function(str)
{
   var r = gldSetBodyText.apply(_root,arguments);
   gldStyleText();
   return r;
};
var gldSetEnv = setEnv_lua;
setEnv_lua = function(n)
{
   var r = gldSetEnv.apply(_root,arguments);
   gldStyleText();
   return r;
};

function gldBuild()
{
   // Under every timeline child; the stock backdrop goes invisible (it takes no clicks: its onPress is cleared).
   gldFrame = _root.createEmptyMovieClip("sanctuaryGuild",-16384);
   gldDrawFrame(gldFrame.createEmptyMovieClip("bg",1));
   gldBuildCrest();
   bg_mc._alpha = 0;
   gldPatchButtons();
   gldBuildButtons();
   gldStyleText();
}

gldBuild();
// ===== End Sanctuary Guild Dialog =====

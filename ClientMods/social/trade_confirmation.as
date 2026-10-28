// ===== Sanctuary Trade Confirmation =====
// Appended to tradeConfirmation's main timeline by build_client_mods.py.
// Redraws the last step of a trade (both players locked their offers; Lua shows this before the swap) in the Main
// Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass card with a gold "!" medallion beside the
// warning, the body text in a recessed panel, the status line in a gold-rimmed pill, Accept as a gold pill and
// Cancel as a glass pill. The stock buttons (FRIconButton) stay where they are as invisible hit areas; their
// states drive the pills under them, so clicks, the accept lock-out (enable) and the Lua events are stock.

var TCF_W = 340;
var TCF_H = 492;
var TCF_GOLD = 0xFFC531;
var TCF_RIM = 0x9DB8FF;
var TCF_SOFT = 0xC9D6FF;
var TCF_INK = 0x3A2400;

var tcfRoot;

// --- drawing helpers -------------------------------------------------------------------

function tcfRoundRect(mc, x, y, w, h, r)
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

function tcfGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   tcfRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function tcfCircle(mc, x, y, r)
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

function tcfText(mc, name, txt, x, y, w, h, size, color)
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

function tcfSparkle(mc, x, y, r, alpha)
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

function tcfOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function tcfShadow()
{
   return [new flash.filters.DropShadowFilter(1,90,0x0A1030,0.9,2,2,1,1)];
}

// Recolours a stock field (its text and every later setTextFormat keep it).
function tcfColor(t, color)
{
   var f = new TextFormat();
   f.color = color;
   t.setNewTextFormat(f);
   t.setTextFormat(f);
}

// --- the card --------------------------------------------------------------------------

function tcfDrawCard(bg)
{
   tcfGradientRect(bg,4,4,TCF_W - 8,TCF_H - 8,28,0x2E4FB0,0x141F55,100,TCF_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:-120,y:-200,w:420,h:440,r:0});
   tcfRoundRect(bg,6,6,TCF_W - 12,TCF_H - 12,26);
   bg.endFill();
   // The warning's band, with sparkles (fixed seed, same every time).
   bg.beginFill(0x0A1440,30);
   tcfRoundRect(bg,14,14,TCF_W - 28,62,24);
   bg.endFill();
   var s = 3131;
   var i;
   for(i = 0; i < 12; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 24 + s % (TCF_W - 48);
      s = (s * 1103515245 + 12345) % 2147483648;
      tcfSparkle(bg,x,20 + s % 50,1 + s % 2,20 + s % 40);
   }
   // The body panel behind the message.
   tcfGradientRect(bg,16,88,TCF_W - 32,212,20,0x0A1440,0x0A1440,38,TCF_RIM,22,1);
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   // The "!" medallion.
   var m = tcfRoot.createEmptyMovieClip("medal",2);
   m._x = 38;
   m._y = 45;
   m.lineStyle(2,0xFFF3C4,100);
   m.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-17,y:-17,w:34,h:34,r:Math.PI / 2});
   tcfCircle(m,0,0,17);
   m.endFill();
   m.lineStyle();
   m.beginFill(TCF_INK,100);
   tcfRoundRect(m,-2.5,-10,5,13,2.5);
   tcfCircle(m,0,7,2.8);
   m.endFill();
   m.filters = [new flash.filters.GlowFilter(TCF_GOLD,0.6,12,12,2,2)];
}

// A pill under a stock button: gold for Accept, glass for Cancel; lighter while hovered, dimmed while disabled.
function tcfDrawPill(btn)
{
   var p = btn.sanctuaryPill;
   var b = p.box;
   var over = btn.over_mc._visible || btn.down_mc._visible;
   var down = btn.down_mc._visible;
   var y = b.y + (down ? 2 : 0);
   var h = b.h - 2;
   p.clear();
   if(p.gold)
   {
      p.lineStyle(2,0xFFF3C4,100);
      p.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:b.x,y:y,w:b.w,h:h,r:Math.PI / 2});
   }
   else
   {
      p.lineStyle(over ? 2 : 1.5,over ? 0xFFE08A : TCF_RIM,over ? 100 : 50);
      p.beginGradientFill("linear",over ? [0x3F66D6,0x1D307A] : [0x284A9E,0x162660],[100,100],[0,255],{matrixType:"box",x:b.x,y:y,w:b.w,h:h,r:Math.PI / 2});
   }
   tcfRoundRect(p,b.x,y,b.w,h,h / 2);
   p.endFill();
   p.lineStyle();
   p.beginFill(0xFFFFFF,over ? 34 : 24);
   tcfRoundRect(p,b.x + 12,y + 4,b.w - 24,h * 0.3,h * 0.15);
   p.endFill();
   if(over && btn.m_isEnabled)
   {
      p.filters = [new flash.filters.GlowFilter(TCF_GOLD,0.7,16,16,2,2)];
   }
   else
   {
      p.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,8,8,1,2)];
   }
   // FRIconButton.enable(false) sets the stock button to 50%; the pill follows.
   p._alpha = p.label._alpha = btn.m_isEnabled ? 100 : 45;
   p.label._y = y + (h - 30) / 2;
}

function tcfBuildPill(btn, gold, depth)
{
   var b = btn.getBounds(_root);
   var p = tcfRoot.createEmptyMovieClip(btn._name + "Pill",depth);
   p.box = {x:b.xMin + 3,y:b.yMin + 3,w:b.xMax - b.xMin - 6,h:b.yMax - b.yMin - 6};
   p.gold = gold;
   // Its label is the stock one's text (FRIconButton.setText), which is hidden with the stock art.
   p.label = tcfText(tcfRoot,btn._name + "Label",String(btn.up_mc.textField_txt.text),p.box.x,p.box.y,p.box.w,30,20,gold ? TCF_INK : 0xFFFFFF);
   if(!gold)
   {
      p.label.filters = tcfOutline();
   }
   btn.sanctuaryPill = p;
   btn.up_mc._alpha = btn.over_mc._alpha = btn.down_mc._alpha = 0;
   tcfDrawPill(btn);
}

function tcfPatchButtons()
{
   var proto = FRIconButton.prototype;
   var states = ["setStateUp","setStateOver","setStateDown"];
   var i;
   for(i = 0; i < states.length; i++)
   {
      tcfWrapState(proto,states[i]);
   }
   var setText = proto.setText;
   proto.setText = function(str)
   {
      var r = setText.apply(this,arguments);
      if(this.sanctuaryPill)
      {
         this.sanctuaryPill.label.text = String(this.up_mc.textField_txt.text);
      }
      return r;
   };
}

function tcfWrapState(proto, name)
{
   var stock = proto[name];
   proto[name] = function()
   {
      var r = stock.apply(this,arguments);
      if(this.sanctuaryPill)
      {
         tcfDrawPill(this);
      }
      return r;
   };
}

// --- text ------------------------------------------------------------------------------

function tcfStyleText()
{
   // The warning in warm gold beside the medallion, the body in white, the status in gold in its pill.
   tcfColor(warning_txt,0xFFB347);
   warning_txt.filters = tcfOutline();
   tcfColor(message_txt,0xFFFFFF);
   message_txt.filters = tcfShadow();
   tcfColor(status_txt,0xFFE08A);
   status_txt.filters = tcfShadow();

   var b = status_txt.getBounds(_root);
   var s = tcfRoot.createEmptyMovieClip("statusPill",3);
   s.lineStyle(1,TCF_GOLD,55);
   s.beginFill(0x0A1440,55);
   tcfRoundRect(s,20,b.yMin - 3,TCF_W - 40,b.yMax - b.yMin + 6,(b.yMax - b.yMin + 6) / 2);
   s.endFill();
}

// The status line changes as the trade moves on (stock: waiting, then review); its colour is kept, as stock
// fits it with setTextFormat.
var tcfEnableAccept = enableAcceptButton;
enableAcceptButton = function()
{
   var r = tcfEnableAccept.apply(_root,arguments);
   tcfColor(status_txt,0xFFE08A);
   return r;
};
var tcfStartTimer = startTradeTimerMessage;
startTradeTimerMessage = function()
{
   var r = tcfStartTimer.apply(_root,arguments);
   tcfColor(status_txt,0xFFE08A);
   return r;
};
var tcfOnClick = onClick;
onClick = function(btn)
{
   var r = tcfOnClick.apply(_root,arguments);
   tcfColor(status_txt,0xFFE08A);
   return r;
};

function tcfBuild()
{
   messageWindow._alpha = 0;
   tcfRoot = _root.createEmptyMovieClip("sanctuaryTradeConfirmation",-16384);
   tcfDrawCard(tcfRoot.createEmptyMovieClip("bg",1));
   tcfStyleText();
   tcfPatchButtons();
   tcfBuildPill(accept_mc,true,10);
   tcfBuildPill(cancel_mc,false,12);
}

tcfBuild();
// ===== End Sanctuary Trade Confirmation =====

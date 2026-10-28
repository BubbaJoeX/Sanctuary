// ===== Sanctuary Tutorial Dialog =====
// Appended to tutorial_dialog's main timeline by build_client_mods.py.
// The tutorial tip window (TutorialDialog.lua: the pages of hints new players get) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass panel that grows with its text like the stock one, a few
// sparkles and a gold rule along the top, outlined white text, gold page arrows, and the close button as the gold
// circle with a dark X (dimmed while Lua keeps it disabled). The stock background and button art stay as the
// invisible hit areas (the background swallows clicks; the button still sends Close).

var TD_RIM = 0x9DB8FF;
var TD_GOLD = 0xFFC531;
var TD_INK = 0x3A2400;
// The close button is drawn at 70% (its stock scale), so its circle is this big in its own coordinates.
var TD_CLOSE_R = 24;

// --- drawing helpers -------------------------------------------------------------------

function tdRoundRect(mc, x, y, w, h, r)
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

function tdCircle(mc, x, y, r)
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

function tdSparkle(mc, x, y, r, alpha)
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

function tdOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Gold, keeping the art's shading (multiplies plus offsets per channel; Color can't mix channels).
function tdTintGold(mc)
{
   new Color(mc).setTransform({ra:30,rb:200,ga:40,gb:120,ba:10,bb:20,aa:100,ab:0});
}

// --- panel -----------------------------------------------------------------------------

// Follows the stock background, which draw() stretches to the content (it ends in a notch for the button).
function tdDrawPanel(dlg)
{
   var p = dlg.sanctuaryPanel;
   var w = dlg.m_Background._width;
   var h = dlg.m_Background._height - 12;
   p.clear();
   p.lineStyle(3,TD_RIM,80);
   p.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:0,y:0,w:w,h:h,r:Math.PI / 2});
   tdRoundRect(p,2,2,w - 4,h,26);
   p.endFill();
   p.lineStyle();
   p.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-120,y:-160,w:420,h:320,r:0});
   tdRoundRect(p,4,4,w - 8,h - 4,24);
   p.endFill();
   p.beginGradientFill("linear",[TD_GOLD,TD_GOLD,TD_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:40,y:0,w:w - 80,h:2,r:0});
   tdRoundRect(p,40,10,w - 80,2,1);
   p.endFill();
   var s = 3636;
   var i;
   for(i = 0; i < 10; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 24 + s % (w - 48);
      s = (s * 1103515245 + 12345) % 2147483648;
      if(dlg.m_PageController._visible && x > dlg.m_PageController._x - 10 && x < dlg.m_PageController._x + dlg.m_PageController._width + 10)
      {
         continue;
      }
      tdSparkle(p,x,16 + s % 14,1 + s % 2,25 + s % 45);
   }
}

function tdDrawClose(btn, over)
{
   var c = btn.sanctuaryClose;
   var r = TD_CLOSE_R;
   c.clear();
   c.lineStyle(2,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   tdCircle(c,0,0,r);
   c.endFill();
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[50,0],[0,255],{matrixType:"box",x:- r * 0.65,y:- r * 0.85,w:r * 1.3,h:r * 0.65,r:Math.PI / 2});
   tdRoundRect(c,- r * 0.65,- r * 0.85,r * 1.3,r * 0.65,r * 0.3);
   c.endFill();
   c.lineStyle(5.5,TD_INK,100);
   c.moveTo(-8.5,-8.5);
   c.lineTo(8.5,8.5);
   c.moveTo(8.5,-8.5);
   c.lineTo(-8.5,8.5);
   c._xscale = c._yscale = over ? 112 : 100;
   c.filters = over ? [new flash.filters.GlowFilter(TD_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

function tdStyle(dlg)
{
   if(dlg.sanctuaryPanel)
   {
      return;
   }
   dlg.m_Background._alpha = 0;
   dlg.sanctuaryPanel = dlg.createEmptyMovieClip("sanctuaryPanel",-16384);
   dlg.sanctuaryPanel.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   // The close button's art (loaded into these) goes; the circle is drawn inside it, so it moves and dims with it.
   var b = dlg.m_CloseButton;
   b.UP._alpha = b.HOVER._alpha = b.DOWN._alpha = b.ICON_UP._alpha = b.ICON_HOVER._alpha = 0;
   var box = b.getBounds(b);
   b.sanctuaryClose = b.createEmptyMovieClip("sanctuaryClose",-16384);
   b.sanctuaryClose._x = box.xMax > box.xMin ? (box.xMin + box.xMax) / 2 : 32;
   b.sanctuaryClose._y = box.yMax > box.yMin ? (box.yMin + box.yMax) / 2 : 32;
   tdDrawClose(b,false);

   tdTintGold(dlg.m_PageController.previous);
   tdTintGold(dlg.m_PageController.next);
   dlg.m_PageController.label.filters = tdOutline();
   dlg.m_Content.m_Message1.filters = tdOutline();
   dlg.m_Content.m_Message2.filters = tdOutline();
}

// --- stock hooks -----------------------------------------------------------------------

var tdDraw = TutorialDialog.prototype.draw;
TutorialDialog.prototype.draw = function()
{
   tdDraw.apply(this,arguments);
   tdStyle(this);
   tdDrawPanel(this);
};

var tdUpdateState = fr.core.FRButton.prototype.updateState;
fr.core.FRButton.prototype.updateState = function(mode)
{
   tdUpdateState.apply(this,arguments);
   if(this.sanctuaryClose)
   {
      tdDrawClose(this,mode == 2 || mode == 3);
   }
};

// The multi-icon rows' text, outlined like the rest.
var tdSetText = MultiIconObject.prototype.setText;
MultiIconObject.prototype.setText = function(value)
{
   tdSetText.apply(this,arguments);
   this.tfLabel.filters = tdOutline();
};

tdStyle(window);
tdDrawPanel(window);
// ===== End Sanctuary Tutorial Dialog =====

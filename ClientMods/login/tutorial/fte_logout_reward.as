// ===== Sanctuary FTE Logout Reward =====
// Appended to FTELogoutReward's main timeline by build_client_mods.py.
// The "come back tomorrow for a reward" card (LoyalityReward / FTELogoutReward.lua, shown on logging out early on)
// in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass panel the size of the stock one
// with a header band, sparkles and the title in gold, the reward's picture in a frosted socket with a gold rim, the
// text outlined, and OK as a gold pill. The squirrel medallion stays, peeking over the corner. The stock OK button
// stays as the invisible hit area over the pill (it still sends exit()); the membership button the movie carries
// is never placed, and nothing here offers membership.

var LR_RIM = 0x9DB8FF;
var LR_GOLD = 0xFFC531;
var LR_INK = 0x3A2400;

// --- drawing helpers -------------------------------------------------------------------

function lrRoundRect(mc, x, y, w, h, r)
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

function lrSparkle(mc, x, y, r, alpha)
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

function lrText(mc, name, txt, x, y, w, h, size, color)
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

function lrOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function lrRecolor(t, color)
{
   var f = new TextFormat();
   f.color = color;
   t.setTextFormat(f);
   t.setNewTextFormat(f);
   t.filters = lrOutline();
}

// Penner's elastic ease (the movie only has Strong compiled in).
function lrElasticOut(t, b, c, d)
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

// --- panel -----------------------------------------------------------------------------

var lrFrame;

function lrBuild()
{
   var b = background_mc.getBounds(_root);
   background_mc._alpha = 0;
   var f = lrFrame = _root.createEmptyMovieClip("sanctuaryLogoutReward",-16384);
   var p = f.createEmptyMovieClip("bg",1);
   var x = b.xMin;
   var y = b.yMin;
   var pw = b.xMax - b.xMin;
   var ph = b.yMax - b.yMin;
   p.lineStyle(3,LR_RIM,80);
   p.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:pw,h:ph,r:Math.PI / 2});
   lrRoundRect(p,x + 4,y + 4,pw - 8,ph - 8,30);
   p.endFill();
   p.lineStyle();
   p.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:x - 100,y:y - 220,w:520,h:460,r:0});
   lrRoundRect(p,x + 6,y + 6,pw - 12,ph - 12,28);
   p.endFill();
   p.beginFill(0x0A1440,30);
   lrRoundRect(p,x + 8,y + 8,pw - 16,58,26);
   p.endFill();
   p.beginGradientFill("linear",[LR_GOLD,LR_GOLD,LR_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:x + 110,y:0,w:pw - 160,h:2,r:0});
   lrRoundRect(p,x + 110,y + 68,pw - 160,2,1);
   p.endFill();
   var s = 3939;
   var i;
   for(i = 0; i < 14; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + 110 + s % (pw - 140);
      s = (s * 1103515245 + 12345) % 2147483648;
      lrSparkle(p,sx,y + 14 + s % 46,1 + s % 3,20 + s % 45);
   }

   // The reward's picture on a frosted plate.
   var ib = imageLoader.getBounds(_root);
   if(ib.xMax > ib.xMin)
   {
      p.lineStyle(2.5,LR_GOLD,90);
      p.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:ib.xMin - 40,y:ib.yMin - 50,w:ib.xMax - ib.xMin + 80,h:ib.yMax - ib.yMin + 80,r:0});
      lrRoundRect(p,ib.xMin - 6,ib.yMin - 6,ib.xMax - ib.xMin + 12,ib.yMax - ib.yMin + 12,16);
      p.endFill();
      // Rounds off the picture's corners.
      var mask = f.createEmptyMovieClip("imageMask",2);
      mask.beginFill(0,100);
      lrRoundRect(mask,ib.xMin - 2,ib.yMin - 2,ib.xMax - ib.xMin + 4,ib.yMax - ib.yMin + 4,12);
      mask.endFill();
      imageLoader.setMask(mask);
   }
   p.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   lrRecolor(label_txt,0xFFE08A);
   lrRecolor(description_txt,0xFFFFFF);
   lrBuildOk();
}

// --- OK --------------------------------------------------------------------------------

function lrDrawOk(over, down)
{
   var m = lrFrame.ok;
   var g = m.bg;
   g.clear();
   g.lineStyle(1.5,0xFFF3C4,100);
   g.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-75,y:-23,w:150,h:46,r:Math.PI / 2});
   lrRoundRect(g,-75,-23,150,46,23);
   g.endFill();
   g.lineStyle();
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:-67,y:-21,w:134,h:19,r:Math.PI / 2});
   lrRoundRect(g,-67,-21,134,19,9);
   g.endFill();
   m.label_txt._y = down ? -12 : -14;
   m.filters = over ? [new flash.filters.GlowFilter(LR_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
}

function lrBuildOk()
{
   var b = exitButton_mc.getBounds(_root);
   var m = lrFrame.ok = lrFrame.createEmptyMovieClip("ok",lrFrame.getNextHighestDepth());
   m._x = b.xMax > b.xMin ? Math.round((b.xMin + b.xMax) / 2) : 275;
   m._y = b.yMax > b.yMin ? Math.round((b.yMin + b.yMax) / 2) : 330;
   m.bg = m.createEmptyMovieClip("bg",1);
   lrText(m,"label_txt","OK",-75,-14,150,30,20,LR_INK);
   lrDrawOk(false,false);
   // The stock button, an invisible hit area over the pill.
   exitButton_mc._xscale = exitButton_mc._yscale = 100;
   var lb = exitButton_mc.getBounds(exitButton_mc);
   if(lb.xMax > lb.xMin)
   {
      exitButton_mc._xscale = 150 / (lb.xMax - lb.xMin) * 100;
      exitButton_mc._yscale = 46 / (lb.yMax - lb.yMin) * 100;
      exitButton_mc._x = m._x - 75 - lb.xMin * exitButton_mc._xscale / 100;
      exitButton_mc._y = m._y - 23 - lb.yMin * exitButton_mc._yscale / 100;
   }
   exitButton_mc._alpha = 0;
   exitButton_mc.onDragOut = function()
   {
      FRIconButton.prototype.onDragOut.call(this);
      lrHoverOk(false);
   };
}

function lrHoverOk(over)
{
   lrDrawOk(over,false);
   gs.TweenLite.to(lrFrame.ok,over ? 0.7 : 0.25,{_xscale:over ? 106 : 100,_yscale:over ? 106 : 100,ease:over ? lrElasticOut : mx.transitions.easing.Strong.easeOut});
}

// The button broadcasts these to _root, its listener.
function onHoverOver(btn)
{
   lrHoverOk(true);
}

function onHoverOut(btn)
{
   lrHoverOk(false);
}

function onButtonPress(btn)
{
   lrDrawOk(true,true);
}

function onButtonRelease(btn)
{
   lrDrawOk(btn.hitTest(_root._xmouse,_root._ymouse,true),false);
}

// --- Lua -------------------------------------------------------------------------------

var lrPopulate = popluateDatafrom_lua;
popluateDatafrom_lua = function(imageId, description, titleString)
{
   lrPopulate(imageId,description,titleString);
   lrRecolor(label_txt,0xFFE08A);
   lrRecolor(description_txt,0xFFFFFF);
};

lrBuild();
// ===== End Sanctuary FTE Logout Reward =====

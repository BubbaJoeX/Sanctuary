// ===== Sanctuary VIP window =====
// Appended to VipMessageWindow's main timeline by build_client_mods.py.
// Redraws the V.I.P. Program window (wndVipMessageWindow) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass frame with sparkles and the title in gold, your VIP level,
// star and progress bar on a glass pill under it, the How it Works and What You Get sections as recesses with gold
// headers over a gold hairline, white outlined text, and a gold close circle. Membership no longer exists, so the
// widget never swaps its level for the members-only button. The frame and section plates go invisible; the title's
// star plate and a divider are plain shapes, dropped at build time (REMOVED_DEPTHS).

var VP_W = 800;
var VP_H = 600;
var VP_GOLD = 0xFFC531;
var VP_RIM = 0x9DB8FF;
var VP_SOFT = 0xC9D6FF;
var VP_INK = 0x3A2400;

var vpFrame;

// --- drawing helpers -------------------------------------------------------------------

function vpRoundRect(mc, x, y, w, h, r)
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

function vpGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   vpRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function vpGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   vpRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function vpCircle(mc, x, y, r)
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

function vpSparkle(mc, x, y, r, alpha)
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

function vpOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Recolours a stock field (all of its text and what it's given next) and outlines it.
function vpLightText(t, color)
{
   if(t == undefined)
   {
      return;
   }
   t.textColor = color;
   var f = t.getNewTextFormat();
   f.color = color;
   t.setNewTextFormat(f);
   t.filters = vpOutline();
}

function vpStrongOut(t, b, c, d)
{
   t = t / d - 1;
   return c * (t * t * t * t * t + 1) + b;
}

// --- frame -----------------------------------------------------------------------------

function vpDrawClose(over)
{
   var c = vpFrame.close;
   c.clear();
   c.lineStyle(1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-17,y:-17,w:34,h:34,r:Math.PI / 2});
   vpCircle(c,0,0,17);
   c.endFill();
   vpGloss(c,-11,-14,22,11,5,50);
   c.lineStyle(4,VP_INK,100);
   c.moveTo(-6,-6);
   c.lineTo(6,6);
   c.moveTo(6,-6);
   c.lineTo(-6,6);
   c.filters = over ? [new flash.filters.GlowFilter(VP_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
   gs.TweenLite.to(c,over ? 0.4 : 0.25,{_xscale:over ? 112 : 100,_yscale:over ? 112 : 100,_rotation:over ? 90 : 0,ease:vpStrongOut});
}

function vpBuild()
{
   background_mc._alpha = 0;
   var f = vpFrame = _root.createEmptyMovieClip("sanctuaryVip",-16384);
   var bg = f.createEmptyMovieClip("bg",1);
   vpGradientRect(bg,10,10,VP_W - 20,VP_H - 20,30,0x2E4FB0,0x141F55,100,VP_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-150,y:-300,w:800,h:640,r:0});
   vpRoundRect(bg,12,12,VP_W - 24,VP_H - 24,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   vpRoundRect(bg,14,14,VP_W - 28,110,26);
   bg.endFill();
   var s = 8181;
   var i;
   for(i = 0; i < 30; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 40 + s % (VP_W - 80);
      s = (s * 1103515245 + 12345) % 2147483648;
      vpSparkle(bg,x,22 + s % 40,1 + s % 3,25 + s % 50);
   }
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   // The level row: a glass pill behind the VIP widget.
   var vb = vip_mc.getBounds(_root);
   var row = f.createEmptyMovieClip("row",2);
   if(vb.xMax > vb.xMin)
   {
      vpGradientRect(row,vb.xMin - 18,vb.yMin - 6,vb.xMax - vb.xMin + 36,vb.yMax - vb.yMin + 12,(vb.yMax - vb.yMin + 12) / 2,0x284A9E,0x162660,100,0xFFE08A,80,1.5);
      vpGloss(row,vb.xMin - 8,vb.yMin - 5,vb.xMax - vb.xMin + 16,(vb.yMax - vb.yMin) / 2,8,22);
      row.filters = [new flash.filters.GlowFilter(VP_GOLD,0.35,12,12,2,2)];
   }

   // The two sections: recesses where the stock plates (unnamed, depths 7 and 9) were, a gold hairline under each
   // header.
   var p = f.createEmptyMovieClip("sections",3);
   var depths = [7,9];
   var headers = [how_it_works_txt,what_you_get_txt];
   for(i = 0; i < depths.length; i++)
   {
      var plate = _root.getInstanceAtDepth(depths[i] - 16384);
      var pb = plate.getBounds(_root);
      plate._alpha = 0;
      if(!(pb.xMax > pb.xMin))
      {
         continue;
      }
      p.lineStyle(1,VP_RIM,28);
      p.beginFill(0x0A1440,40);
      vpRoundRect(p,pb.xMin,pb.yMin,pb.xMax - pb.xMin,pb.yMax - pb.yMin,22);
      p.endFill();
      vpGloss(p,pb.xMin + 10,pb.yMin + 1,pb.xMax - pb.xMin - 20,24,12,10);
      var hy = Math.round(headers[i]._y + 30);
      p.lineStyle();
      p.beginGradientFill("linear",[VP_GOLD,VP_GOLD,VP_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:pb.xMin + 14,y:0,w:pb.xMax - pb.xMin - 28,h:2,r:0});
      vpRoundRect(p,pb.xMin + 14,hy,pb.xMax - pb.xMin - 28,2,1);
      p.endFill();
   }

   vpLightText(title_txt,0xFFE08A);
   vpLightText(how_it_works_txt,0xFFE08A);
   vpLightText(what_you_get_txt,0xFFE08A);
   vpLightText(how_it_works_body_txt,0xFFFFFF);
   vpLightText(what_you_get_body_txt,0xFFFFFF);
   vpLightText(vip_mc.vip_level_txt,0xFFFFFF);

   // The close button: its state art hidden (still the hit area) over a gold circle.
   var exit = exitButton_mc;
   exit.up_mc._alpha = exit.over_mc._alpha = exit.down_mc._alpha = 0;
   var eb = exit.getBounds(_root);
   f.close = f.createEmptyMovieClip("close",4);
   f.close._x = eb.xMax > eb.xMin ? Math.round((eb.xMin + eb.xMax) / 2) : VP_W - 70;
   f.close._y = eb.yMax > eb.yMin ? Math.round((eb.yMin + eb.yMax) / 2) : 70;
   vpDrawClose(false);
   var lo = new Object();
   lo.onHoverOver = function(target)
   {
      vpDrawClose(true);
   };
   lo.onHoverOut = function(target)
   {
      vpDrawClose(false);
   };
   exit.addListener(lo);
}

// --- Lua -------------------------------------------------------------------------------

// Membership no longer exists: the widget always shows the level, star and bar, never the members-only button.
var vpLoadData = loadData_lua;
loadData_lua = function(title, howItWorksMessage, whatYouGetMessage, howItWorksHeader, whatYouGetHeader, isMember, vipRank, vipProgress, buttonText)
{
   var r = vpLoadData.apply(_root,arguments);
   vip_mc.showButton(false);
   vpLightText(how_it_works_body_txt,0xFFFFFF);
   vpLightText(what_you_get_body_txt,0xFFFFFF);
   return r;
};

vpBuild();
// ===== End Sanctuary VIP window =====

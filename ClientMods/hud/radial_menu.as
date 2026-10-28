// ===== Sanctuary Radial Menu =====
// Appended to radial_menu's main timeline by build_client_mods.py.
// The radial menu that opens on clicking an NPC, player, pet or prop (wndRadialMenu) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as). Its ring (a gold ring in a soft blue glow, like the minimap's) and the
// labels' plates (navy) are repainted at build time (RESKINS); this script fills the ring with a soft navy glass
// disc, puts each choice's icon in a frosted socket with a gold rim, outlines the labels, and lights the hovered
// label's plate gold instead of the stock blue.
// setButton is wrapped (styling after the stock layout); its hover, click and tween code is untouched.

var RM_GOLD_LIGHT = 0xFFE08A;

function rmCircle(mc, x, y, r)
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

// A frosted disc with a gold rim under the icon's art (the holder is a 50px circle centred on Icon's origin).
function rmSocket(icon)
{
   if(icon.sanctuarySocket != undefined)
   {
      return;
   }
   var b = icon.holder.getBounds(icon);
   var cx = (b.xMin + b.xMax) / 2;
   var cy = (b.yMin + b.yMax) / 2;
   var r = Math.max(b.xMax - b.xMin,b.yMax - b.yMin) / 2 + 3;
   var sock = icon.createEmptyMovieClip("sanctuarySocket",-16384);
   icon.sanctuarySocket = sock;
   sock.lineStyle(2,RM_GOLD_LIGHT,100);
   sock.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:cx - r * 1.3,y:cy - r * 1.4,w:r * 2.4,h:r * 2.6,r:0});
   rmCircle(sock,cx,cy,r);
   sock.endFill();
   sock.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

// The repainted ring is only a ring: a soft navy glass disc inside it keeps the icons on a backdrop. It's drawn in
// circle_gradient (under its art), so it opens, bounces and fades with the ring.
function rmBackdrop()
{
   var b = circle_gradient.getBounds(circle_gradient);
   var cx = (b.xMin + b.xMax) / 2;
   var cy = (b.yMin + b.yMax) / 2;
   var r = (b.xMax - b.xMin) / 2 - 8;
   var disc = circle_gradient.createEmptyMovieClip("sanctuaryDisc",-16384);
   disc.beginGradientFill("radial",[0x2E4FB0,0x141F55,0x0A1440],[40,55,20],[0,180,255],{matrixType:"box",x:cx - r,y:cy - r,w:r * 2,h:r * 2,r:0});
   rmCircle(disc,cx,cy,r);
   disc.endFill();
}

rmBackdrop();

var rmSetButton = setButton;
setButton = function(id)
{
   var result = rmSetButton.apply(_root,arguments);
   var ic = _root["icon" + id];
   rmSocket(ic.Icon);
   var label = ic.labelC;
   label.butText.textColor = 0xFFFFFF;
   label.butText.filters = [new flash.filters.GlowFilter(0x0A1030,0.95,3,3,5,2)];
   // The hovered plate: the navy plate lit gold (the stock tints it blue).
   new Color(label.bghl).setTransform({ra:55,rb:150,ga:45,gb:125,ba:15,bb:20,aa:100,ab:0});
   label.bghl.filters = [new flash.filters.GlowFilter(0xFFC531,0.8,10,10,2,2)];
   label.bg.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,5,5,1,2)];
   return result;
};
// ===== End Sanctuary Radial Menu =====

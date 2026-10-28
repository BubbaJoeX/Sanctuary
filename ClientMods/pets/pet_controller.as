// ===== Sanctuary Pet Controller =====
// Appended to petController's main timeline by build_client_mods.py.
// The pet remote (wndPetController, top right while your pet is out) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): the pet's portrait in a frosted socket inside a gold ring, the
// dismiss (X) and tricks (paw) buttons as gold circles that glow on hover, the name tag that slides out on
// hover as a navy glass pill, the pet's requests ("I'm hungry!") in a navy glass thought bubble, and the
// tricks flyout as a glass tray with each trick in a frosted socket. Everything is drawn under the stock clips,
// which stay as invisible hit areas and keep their Lua callbacks.

var PR_GOLD = 0xFFC531;
var PR_RIM = 0x9DB8FF;
var PR_SOFT = 0xC9D6FF;
var PR_INK = 0x3A2400;
// The portrait's center and radius in minpet (the stock frame and icon are 58px around -32, 23).
var PR_FACE_X = -32;
var PR_FACE_Y = 23;
var PR_FACE_R = 28;
// FRCircleButton is 45px square, its plates 36px.
var PR_CHIP_C = 22.5;
var PR_CHIP_R = 19;

// --- drawing helpers -------------------------------------------------------------------

function prRoundRect(mc, x, y, w, h, r)
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

function prGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   prRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function prCircle(mc, x, y, r)
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

function prGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   prRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function prSparkle(mc, x, y, r, alpha)
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

function prOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// A navy glass panel (the card and tile gradient) with a soft highlight.
function prGlass(mc, x, y, w, h, r)
{
   prGradientRect(mc,x,y,w,h,r,0x2E4FB0,0x141F55,100,PR_RIM,80,2.5);
   mc.lineStyle();
   mc.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[30,0],[0,255],{matrixType:"box",x:x - w * 0.3,y:y - h,w:w,h:h * 2.2,r:0});
   prRoundRect(mc,x + 2,y + 2,w - 4,h - 4,Math.max(0,r - 2));
   mc.endFill();
}

function prLightText(t, color)
{
   var f = new TextFormat();
   f.color = color;
   t.setNewTextFormat(f);
   t.setTextFormat(f);
   t.filters = prOutline();
}

// --- gold circle buttons (dismiss and tricks) -------------------------------------------

function prDrawChip(b)
{
   var c = b.sanctuaryChip;
   var down = b.down_mc._visible;
   var over = !down && b.over_mc._visible;
   var x = PR_CHIP_C;
   var y = PR_CHIP_C + (down ? 1 : 0);
   var r = PR_CHIP_R;
   c.clear();
   c.lineStyle(1.5,0xFFF3C4,100);
   var colors = down ? [0xF5B840,0xD98A10] : over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623];
   c.beginGradientFill("linear",colors,[100,100],[0,255],{matrixType:"box",x:x - r,y:y - r,w:r * 2,h:r * 2,r:Math.PI / 2});
   prCircle(c,x,y,r);
   c.endFill();
   prGloss(c,x - r * 0.65,y - r * 0.85,r * 1.3,r * 0.65,r * 0.3,down ? 25 : 55);
   if(b.sanctuaryCross)
   {
      c.lineStyle(4,PR_INK,100);
      c.moveTo(x - 6,y - 6);
      c.lineTo(x + 6,y + 6);
      c.moveTo(x + 6,y - 6);
      c.lineTo(x - 6,y + 6);
   }
   if(over || down)
   {
      c.filters = [new flash.filters.GlowFilter(PR_GOLD,down ? 0.95 : 0.8,14,14,2,2)];
   }
   else
   {
      c.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
   }
}

function prStyleChip(b, cross)
{
   b.holder._alpha = b.up_mc._alpha = b.over_mc._alpha = b.down_mc._alpha = 0;
   b.sanctuaryCross = cross;
   if(cross)
   {
      // The dismiss button's stock X icon; the chip draws its own.
      b.m_icon_up._alpha = b.m_icon_over._alpha = 0;
   }
   b.sanctuaryChip = b.createEmptyMovieClip("sanctuaryChip",-16384);
   // The plates all show until the first state change; start from the up state.
   b.setStateUp();
}

function prWrapChipState(name)
{
   var stock = FRCircleButton.prototype[name];
   FRCircleButton.prototype[name] = function()
   {
      stock.apply(this,arguments);
      if(this.sanctuaryChip)
      {
         prDrawChip(this);
      }
   };
}

function prPatchChips()
{
   var states = ["setStateUp","setStateOver","setStateDown","setToggled","enable"];
   var i;
   for(i = 0; i < states.length; i++)
   {
      prWrapChipState(states[i]);
   }
}

// --- portrait and name tag --------------------------------------------------------------

function prDrawFace(over)
{
   var f = minpet.sanctuaryFace;
   var x = PR_FACE_X;
   var y = PR_FACE_Y;
   var r = PR_FACE_R;
   f.clear();
   // The gold ring, a navy band and the frosted socket the pet's icon sits on.
   f.lineStyle(1.5,0xFFF3C4,100);
   f.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x - r - 7,y:y - r - 7,w:r * 2 + 14,h:r * 2 + 14,r:Math.PI / 2});
   prCircle(f,x,y,r + 7);
   f.endFill();
   f.lineStyle();
   f.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:x - r - 3,y:y - r - 3,w:r * 2 + 6,h:r * 2 + 6,r:Math.PI / 2});
   prCircle(f,x,y,r + 3);
   f.endFill();
   f.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:x - r - 6,y:y - r - 10,w:r * 2 + 8,h:r * 2 + 8,r:0});
   prCircle(f,x,y,r);
   f.endFill();
   prSparkle(f,x + r + 2,y - r + 2,2,over ? 90 : 55);
   prSparkle(f,x - r - 1,y + r - 6,1.3,over ? 70 : 35);
   f.filters = over ? [new flash.filters.GlowFilter(PR_GOLD,0.8,16,16,2,2)] : [new flash.filters.DropShadowFilter(4,90,0x050A24,0.6,10,10,1,2)];
}

// The portrait's gloss, over the icon (it takes no clicks; minpet keeps them).
function prDrawFaceGloss()
{
   var g = minpet.sanctuaryFaceGloss;
   var r = PR_FACE_R;
   g.clear();
   g.lineStyle();
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[40,0],[0,255],{matrixType:"box",x:PR_FACE_X - r,y:PR_FACE_Y - r,w:r * 2,h:r,r:Math.PI / 2});
   prRoundRect(g,PR_FACE_X - r * 0.7,PR_FACE_Y - r + 3,r * 1.4,r * 0.6,r * 0.3);
   g.endFill();
}

// The name tag: a navy glass pill from the stock left cap to behind the portrait. It sits between the stock
// plates (now invisible) and the name, over the unnamed right cap (a shape AS can't hide), which it covers.
function prDrawTag()
{
   var tag = minpet.nametag;
   var t = tag.sanctuaryTag;
   var x = tag.ntleft._x;
   t.clear();
   prGlass(t,x,-24,60 - x,54,27);
   prLightText(tag.petName,0xFFFFFF);
}

function prBuildFace()
{
   // minpet's timeline depths: nametag 1, bg 8, icon 12, dismissHolder_mc 14 (AS depth = depth - 16384).
   minpet.bg._alpha = 0;
   var face = minpet.createEmptyMovieClip("sanctuaryFace",-16374);
   prDrawFace(false);
   // A round mask on the icon, so square art fits the socket.
   var mask = minpet.createEmptyMovieClip("sanctuaryFaceMask",-16375);
   mask.beginFill(0,100);
   prCircle(mask,PR_FACE_X,PR_FACE_Y,PR_FACE_R - 0.5);
   mask.endFill();
   minpet.icon.setMask(mask);
   minpet.createEmptyMovieClip("sanctuaryFaceGloss",-16371);
   prDrawFaceGloss();

   var tag = minpet.nametag;
   tag.ntleft._alpha = tag.ntmid._alpha = 0;
   tag.createEmptyMovieClip("sanctuaryTag",-16379);
   tag.sanctuaryTag.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.6,8,8,1,2)];
   prDrawTag();

   var rollOver = minpet.onRollOver;
   minpet.onRollOver = function()
   {
      rollOver.apply(this,arguments);
      prDrawFace(true);
   };
   var rollOut = minpet.onRollOut;
   minpet.onRollOut = function()
   {
      rollOut.apply(this,arguments);
      prDrawFace(false);
   };
   minpet.onDragOut = minpet.onReleaseOutside = function()
   {
      prDrawFace(false);
   };
}

// --- the pet's requests (thought bubble) ---------------------------------------------------

function prDrawBubble()
{
   var p = petcontentpanel;
   var b = p.sanctuaryBubble;
   var x = p.bubble._x;
   var y = p.bubble._y;
   var w = p.bubble._width;
   var h = p.bubble._height;
   b.clear();
   // The body, and two thought dots rising toward the portrait at the right (where the stock ones are).
   prGlass(b,x,y + 10,w - 30,h - 10,20);
   b.lineStyle(1.5,PR_RIM,80);
   b.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:x + w - 30,y:y + 6,w:16,h:16,r:Math.PI / 2});
   prCircle(b,x + w - 22,y + 14,8);
   b.endFill();
   b.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:x + w - 12,y:y,w:9,h:9,r:Math.PI / 2});
   prCircle(b,x + w - 7.5,y + 4.5,4.5);
   b.endFill();
   // A gold edge along the body's top.
   b.lineStyle();
   b.beginGradientFill("linear",[PR_GOLD,PR_GOLD,PR_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:x + 20,y:0,w:w - 70,h:2,r:0});
   prRoundRect(b,x + 20,y + 11,w - 70,2,1);
   b.endFill();
   prLightText(p.petAction,0xFFFFFF);
}

function prBuildBubble()
{
   var p = petcontentpanel;
   p.bubble._alpha = 0;
   var b = p.createEmptyMovieClip("sanctuaryBubble",-16384);
   b.filters = [new flash.filters.DropShadowFilter(5,90,0x050A24,0.6,14,14,1,2)];
   prDrawBubble();
}

// --- tricks flyout -----------------------------------------------------------------------

function prDrawTrickSocket(btn, over)
{
   var s = btn.sanctuarySocket;
   var r = 23;
   s.clear();
   s.lineStyle();
   s.beginFill(0x050A24,35);
   prCircle(s,0,26,r + 1);
   s.endFill();
   s.lineStyle(2,over ? PR_GOLD : 0xFFFFFF,over ? 100 : 85);
   s.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:- r - 6,y:24 - r - 10,w:r * 2 + 8,h:r * 2 + 8,r:0});
   prCircle(s,0,24,r);
   s.endFill();
   s.filters = over ? [new flash.filters.GlowFilter(PR_GOLD,0.8,14,14,2,2)] : [];
   gs.TweenLite.to(s,0.3,{_y:over ? -3 : 0,ease:mx.transitions.easing.Strong.easeOut});
}

function prStyleTrick(btn)
{
   if(btn.sanctuarySocket)
   {
      return;
   }
   // The beige plate is the trick's hit area; it stays, invisibly.
   btn.background._alpha = 0;
   btn.createEmptyMovieClip("sanctuarySocket",-16384);
   btn.label.filters = prOutline();
   prDrawTrickSocket(btn,false);
   // Buttons made before the prototype was patched bound the stock handlers; bind the patched ones.
   btn.background.onRollOut = btn.background.onDragOut = gfx.utils.Delegate.create(btn,btn.m_OnRollOut);
   btn.background.onRollOver = gfx.utils.Delegate.create(btn,btn.m_OnRollOver);
}

function prDrawFlyout(fly)
{
   var t = fly.sanctuaryTray;
   var b = fly.background.getBounds(fly);
   t.clear();
   if(b.xMax <= b.xMin)
   {
      return;
   }
   prGlass(t,b.xMin,b.yMin,b.xMax - b.xMin,b.yMax - b.yMin,24);
   var s = 5151;
   var i;
   for(i = 0; i < 8; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = b.xMin + 20 + s % Math.max(1,Math.round(b.xMax - b.xMin - 40));
      s = (s * 1103515245 + 12345) % 2147483648;
      prSparkle(t,x,b.yMin + 8 + s % 14,1 + s % 2,15 + s % 30);
   }
   for(i = 0; i < fly.m_TrickButtons.length; i++)
   {
      prStyleTrick(fly.m_TrickButtons[i]);
   }
}

function prPatchTricks()
{
   var proto = pets.display.TrickButton.prototype;
   var rollOver = proto.m_OnRollOver;
   proto.m_OnRollOver = function()
   {
      rollOver.apply(this,arguments);
      if(this.sanctuarySocket)
      {
         prDrawTrickSocket(this,true);
      }
   };
   var rollOut = proto.m_OnRollOut;
   proto.m_OnRollOut = function()
   {
      rollOut.apply(this,arguments);
      if(this.sanctuarySocket)
      {
         prDrawTrickSocket(this,false);
      }
   };

   var fly = pets.display.TrickFlyout.prototype;
   var update = fly.update;
   fly.update = function()
   {
      update.apply(this,arguments);
      if(this.sanctuaryTray)
      {
         prDrawFlyout(this);
      }
   };
}

function prBuildFlyout()
{
   var fly = petbtn.trickBar_mc;
   fly.background._alpha = 0;
   fly.createEmptyMovieClip("sanctuaryTray",-16384);
   fly.sanctuaryTray.filters = [new flash.filters.DropShadowFilter(5,90,0x050A24,0.6,14,14,1,2)];
   prDrawFlyout(fly);
}

// --- Lua and stock hooks ---------------------------------------------------------------

var prSetPetName = SetPetName;
SetPetName = function(sName)
{
   prSetPetName.apply(_root,arguments);
   prDrawTag();
};

// The request bubble grows with its text.
var prActivatePanel = ActivatePanel;
ActivatePanel = function(sBehaviourString, id, displayTime)
{
   prActivatePanel.apply(_root,arguments);
   prDrawBubble();
};

prPatchChips();
prPatchTricks();
prStyleChip(config,false);
prStyleChip(minHolder_mc.minimize_btn,true);
prBuildFace();
prBuildBubble();
prBuildFlyout();
// ===== End Sanctuary Pet Controller =====

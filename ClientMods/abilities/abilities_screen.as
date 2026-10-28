// ===== Sanctuary Abilities =====
// Appended to AbilitiesScreen's main timeline by build_client_mods.py.
// Redraws the character screen's Abilities tab (a combat job's weapon, its two attacks and the job's four traits) in
// the equipment pane's style (ClientMods/equipment/equipment_pane.as): a navy glass frame with a gold title, a
// glass card for the weapon and one for each attack, and a recessed Traits bay with a card per trait. Art sits a
// little bigger in frosted sockets with rounded corners, and the attack cards lift with a gold glow and pop their
// icon on hover (where the stock shows their tooltip). Locked traits are dimmed with a gold lock badge and an
// "Unlocks at level N" pill. The stock clips keep their roles: Lua fills them through the stock callbacks
// (setEquippedWeaponData, SetWeaponAbilities, SetProfileAbilities) and they stay the hit areas for the tooltips.
// The stock plates under the icons are plain shapes, so each socket is drawn over its plate, under the icon.

var AB_W = 761;
var AB_H = 580;
// The cards, in the window's coordinates: the weapon, the attack and the special attack, and the Traits bay.
var AB_TOP_Y = 78;
var AB_TOP_H = 252;
var AB_WEAPON = [24,AB_TOP_Y,190,AB_TOP_H];
var AB_ATTACKS = [[224,AB_TOP_Y,251,AB_TOP_H],[485,AB_TOP_Y,252,AB_TOP_H]];
var AB_BAY = [24,342,713,224];
var AB_TRAIT_X = 36;
var AB_TRAIT_Y = 382;
var AB_TRAIT_W = 166;
var AB_TRAIT_H = 176;
var AB_TRAIT_GAP = 8;
// The weapon's socket (its center), and the weapon icon's scale (stock 112.5) resting and hovered.
var AB_WEAPON_CX = 119;
var AB_WEAPON_CY = 160;
var AB_WEAPON_SCALE = 125;
var AB_WEAPON_HOVER = 142;
// The stock ability plates are 46px; the icons sit at 53px (56px hovered) in a 60px socket.
var AB_PLATE = 46;
var AB_ICON = 53;
var AB_ICON_HOVER = 58;
var AB_SOCKET = 60;
var AB_GOLD = 0xFFC531;
var AB_RIM = 0x9DB8FF;
var AB_SOFT = 0xC9D6FF;

var abFrame;

// --- drawing helpers -------------------------------------------------------------------

function abRoundRect(mc, x, y, w, h, r)
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

function abGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   abRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function abCircle(mc, x, y, r)
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

function abText(mc, name, txt, x, y, w, h, size, color, align)
{
   mc.createTextField(name,mc.getNextHighestDepth(),x,y,w,h);
   var t = mc[name];
   t.embedFonts = true;
   t.selectable = false;
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = align == undefined ? "center" : align;
   t.setNewTextFormat(f);
   t.text = txt;
   return t;
}

function abSparkle(mc, x, y, r, alpha)
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

function abOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function abShadow()
{
   return [new flash.filters.DropShadowFilter(1,90,0x0A1030,0.9,2,2,1,1)];
}

// A gold rule that fades out at both ends.
function abRule(mc, x, y, w)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[AB_GOLD,AB_GOLD,AB_GOLD],[0,85,0],[0,128,255],{matrixType:"box",x:x,y:0,w:w,h:2,r:0});
   abRoundRect(mc,x,y,w,2,1);
   mc.endFill();
}

// The frosted socket behind stock art (the icons are made for light plates), a rounded square around (cx, cy).
function abSocket(mc, cx, cy, size, rim, rimAlpha, rimWidth)
{
   var h = size / 2;
   mc.clear();
   mc.lineStyle();
   mc.beginFill(0x050A24,30);
   abRoundRect(mc,cx - h,cy - h + 3,size,size,size * 0.24);
   mc.endFill();
   mc.lineStyle(rimWidth,rim,rimAlpha);
   mc.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:cx - size * 0.65,y:cy - size * 0.75,w:size * 1.3,h:size * 1.3,r:0});
   abRoundRect(mc,cx - h,cy - h,size,size,size * 0.24);
   mc.endFill();
}

// A rounded mask over a stock icon, so its corners match the socket.
function abMask(parent, name, cx, cy, size)
{
   var m = parent[name];
   if(m == undefined)
   {
      m = parent.createEmptyMovieClip(name,parent.getNextHighestDepth());
   }
   var h = size / 2;
   m.clear();
   m.beginFill(0,100);
   abRoundRect(m,cx - h,cy - h,size,size,size * 0.22);
   m.endFill();
   return m;
}

// Sets a stock field's box and shrinks its font from size until the text fits (the stock fitters size to the
// stock boxes, and frSetTextPreserveWidth puts a field back in its stock box every time it's called).
function abFit(t, x, y, w, h, size, color, align)
{
   t._x = x;
   t._y = y;
   t._width = w;
   t._height = h;
   var f = t.getTextFormat();
   f.size = size;
   f.color = color;
   f.align = align;
   t.setTextFormat(f);
   while((t.textHeight > h - 4 || t.textWidth > w - 4) && f.size > 9)
   {
      f.size -= 1;
      t.setTextFormat(f);
   }
}

// Dark rarity colours would vanish on navy; they're lifted halfway to white.
function abLight(c)
{
   var r = c >> 16 & 0xFF;
   var g = c >> 8 & 0xFF;
   var b = c & 0xFF;
   if(r * 0.3 + g * 0.59 + b * 0.11 >= 150)
   {
      return c;
   }
   return Math.round((r + 255) / 2) << 16 | Math.round((g + 255) / 2) << 8 | Math.round((b + 255) / 2);
}

function abElastic()
{
   return mx.transitions.easing.Elastic.easeOut;
}

function abStrong()
{
   return mx.transitions.easing.Strong.easeOut;
}

// --- frame and cards -------------------------------------------------------------------

function abDrawFrame(bg)
{
   abGradientRect(bg,4,4,AB_W - 8,AB_H - 8,30,0x2E4FB0,0x141F55,100,AB_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[34,0],[0,255],{matrixType:"box",x:-160,y:-320,w:860,h:700,r:0});
   abRoundRect(bg,6,6,AB_W - 12,AB_H - 12,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   abRoundRect(bg,16,16,AB_W - 32,52,24);
   bg.endFill();

   // Sparkles in the header (fixed seed, same every time).
   var s = 4242;
   var i;
   for(i = 0; i < 22; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 34 + s % (AB_W - 68);
      s = (s * 1103515245 + 12345) % 2147483648;
      abSparkle(bg,x,22 + s % 40,1 + s % 2,20 + s % 45);
   }

   // The Traits bay and its rule.
   abGradientRect(bg,AB_BAY[0],AB_BAY[1],AB_BAY[2],AB_BAY[3],24,0x0A1440,0x0A1440,38,AB_RIM,22,1);
   abRule(bg,AB_BAY[0] + 20,AB_BAY[1] + 32,AB_BAY[2] - 40);
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
}

// A glass card: navy (lighter and gold-rimmed while hovered), a sheen across the top and, with a caption,
// a gold rule under its header strip. Locked cards are recessed instead.
function abDrawCard(card)
{
   var g = card.bg;
   var x = card.box[0];
   var y = card.box[1];
   var w = card.box[2];
   var h = card.box[3];
   g.clear();
   if(card.locked)
   {
      abGradientRect(g,x,y,w,h,18,0x0A1440,0x0A1440,45,AB_RIM,25,1);
   }
   else if(card.over)
   {
      abGradientRect(g,x,y,w,h,18,0x3F66D6,0x1D307A,100,0xFFE08A,100,2);
   }
   else
   {
      abGradientRect(g,x,y,w,h,18,0x284A9E,0x162660,100,AB_RIM,40,1.5);
   }
   g.lineStyle();
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[card.locked ? 6 : 16,0],[0,255],{matrixType:"box",x:0,y:y + 3,w:10,h:44,r:Math.PI / 2});
   abRoundRect(g,x + 3,y + 3,w - 6,44,15);
   g.endFill();
   if(card.caption)
   {
      abRule(g,x + 16,y + 32,w - 32);
   }
   if(card.over)
   {
      g.filters = [new flash.filters.GlowFilter(AB_GOLD,0.55,14,14,2,2)];
   }
   else if(card.locked)
   {
      g.filters = [];
   }
   else
   {
      g.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
   }
}

function abMakeCard(name, box, caption)
{
   var card = abFrame.cards.createEmptyMovieClip(name,abFrame.cards.getNextHighestDepth());
   card.bg = card.createEmptyMovieClip("bg",1);
   card.box = box;
   card.caption = caption;
   card.over = false;
   card.locked = false;
   abDrawCard(card);
   return card;
}

function abSetOver(card, over)
{
   if(card.over == over)
   {
      return;
   }
   card.over = over;
   abDrawCard(card);
   gs.TweenLite.to(card,0.25,{_y:over ? -3 : 0,ease:abStrong()});
}

function abBuildFrame(win)
{
   abFrame = win.createEmptyMovieClip("sanctuaryAbilities",-16384);
   abDrawFrame(abFrame.createEmptyMovieClip("bg",1));
   abFrame.cards = abFrame.createEmptyMovieClip("cards",2);
   abFrame.weapon = abMakeCard("weapon",AB_WEAPON,true);
   abFrame.attacks = [abMakeCard("attack0",AB_ATTACKS[0],true),abMakeCard("attack1",AB_ATTACKS[1],true)];
   abFrame.traits = [];
   var i;
   for(i = 0; i < 4; i++)
   {
      abFrame.traits.push(abMakeCard("trait" + i,[AB_TRAIT_X + i * (AB_TRAIT_W + AB_TRAIT_GAP),AB_TRAIT_Y,AB_TRAIT_W,AB_TRAIT_H],false));
   }
   // The "Unlocks at level" pills, drawn under the stock text.
   abFrame.pills = abFrame.createEmptyMovieClip("pills",3);
   var title = abText(abFrame,"title_txt","Abilities",0,19,AB_W,40,28,0xFFE08A);
   title.filters = abOutline();
   var hint = abText(abFrame,"hint_txt","Your weapon decides your attacks",AB_WEAPON[0] + 14,AB_WEAPON[1] + AB_WEAPON[3] - 50,AB_WEAPON[2] - 28,40,12,AB_SOFT);
   hint.multiline = hint.wordWrap = true;
   hint.filters = abShadow();

   // The stock backdrop (a sprite at the window's first depth).
   win.getInstanceAtDepth(-16383)._alpha = 0;
}

// --- the weapon ------------------------------------------------------------------------

// Unnamed stock text (a caption) at a timeline depth, outlined like the rest, if the player lets AS reach it.
function abOutlineAt(mc, depth)
{
   var t = mc.getInstanceAtDepth(depth - 16384);
   if(t != undefined)
   {
      t.filters = abShadow();
   }
}

function abStyleSelector(sel)
{
   // The plate (a 72px shape at the selector's first depth) is centered on (77, 42.4); the socket covers it.
   // Placed every time: the window places its parts itself right after attaching them.
   sel._x = AB_WEAPON_CX - 77;
   sel._y = AB_WEAPON_CY - 42.4;
   if(sel.sanctuary)
   {
      return;
   }
   sel.sanctuary = true;
   sel.m_Icon.swapDepths(1001);
   sel.m_Icon._x = 77;
   sel.m_Icon._y = 42.4;
   sel.m_Icon._xscale = sel.m_Icon._yscale = AB_WEAPON_SCALE;
   abSocket(sel.createEmptyMovieClip("sanctuarySocket",1000),77,42.4,92,0xFFFFFF,90,2);
   // The icon loads into its holder, so the mask on the icon stays.
   sel.m_Icon.setMask(abMask(sel,"sanctuaryIconMask",77,42.4,88));
   sel.m_Label._y = 100;
   sel.m_Label.filters = abOutline();
   abOutlineAt(sel,2);
}

function abWeaponHover(sel, over)
{
   abSetOver(abFrame.weapon,over);
   var s = over ? AB_WEAPON_HOVER : AB_WEAPON_SCALE;
   gs.TweenLite.to(sel.m_Icon,over ? 0.6 : 0.2,{_xscale:s,_yscale:s,ease:over ? abElastic() : abStrong()});
}

// --- the attacks -----------------------------------------------------------------------

// Lays out one attack inside its card. Its origin is the stock plate's top left (a 46px shape at its first depth).
function abLayoutAttack(a, box)
{
   var ox = box[0] + 21;
   var oy = box[1] + 49;
   a._x = ox;
   a._y = oy;
   var c = AB_PLATE / 2;
   if(a.sanctuarySocket == undefined)
   {
      a.m_Icon.swapDepths(1001);
      abSocket(a.createEmptyMovieClip("sanctuarySocket",1000),c,c,AB_SOCKET,0xFFFFFF,90,2);
      abMask(a,"sanctuaryIconMask",c,c,AB_SOCKET - 4);
   }
   // Set again each time: the icon loads into the clip itself, which may drop its mask.
   a.m_Icon.setMask(a.sanctuaryIconMask);
   if(!a.sanctuaryHover)
   {
      abIconSize(a.m_Icon,c,c,AB_ICON,117.5);
   }
   var right = box[0] + box[2] - 12 - ox;
   var left = 12 - 21;
   abFit(a.m_Label,c + AB_SOCKET / 2 + 4,c - 22,right - c - AB_SOCKET / 2 - 4,48,16,0xFFFFFF,"left");
   a.m_Label.filters = abOutline();
   abFit(a.m_Description,left,c + AB_SOCKET / 2 + 10,right - left,60,13,AB_SOFT,"left");
   a.m_Description.filters = abShadow();
   // The effect lines SetWeaponAbilities adds under the description, as gold lines.
   var y = c + AB_SOCKET / 2 + 74;
   var i = 0;
   while(a["effect_" + i] != undefined)
   {
      var e = a["effect_" + i];
      e._x = left;
      e._y = y;
      e._width = right - left;
      var f = new TextFormat();
      f.color = 0xFFE08A;
      f.size = 11;
      e.setTextFormat(f);
      e.filters = abShadow();
      y += e._height;
      i++;
   }
}

// Scales a stock icon (its art fitted to a square of `art` px, top left at its origin) to `size` around (cx, cy).
function abIconSize(icon, cx, cy, size, art)
{
   icon._xscale = icon._yscale = size / art * 100;
   icon._x = cx - size / 2;
   icon._y = cy - size / 2;
}

function abAttackHover(a, over)
{
   var i = a == a._parent.m_Ability2 ? 1 : 0;
   abSetOver(abFrame.attacks[i],over);
   var size = over ? AB_ICON_HOVER : AB_ICON;
   var c = AB_PLATE / 2;
   a.sanctuaryHover = over;
   gs.TweenLite.to(a.m_Icon,over ? 0.6 : 0.2,{_xscale:size / 117.5 * 100,_yscale:size / 117.5 * 100,_x:c - size / 2,_y:c - size / 2,ease:over ? abElastic() : abStrong()});
}

// The stock rollovers show the attack's tooltip; this adds the card's hover after them.
function abWrapRoll(a)
{
   if(a.sanctuaryRoll || a.onRollOver == undefined)
   {
      return;
   }
   a.sanctuaryRoll = true;
   var over = a.onRollOver;
   var out = a.onRollOut;
   a.onRollOver = function()
   {
      var r = over.apply(this,arguments);
      abAttackHover(this,true);
      return r;
   };
   a.onRollOut = function()
   {
      var r = out.apply(this,arguments);
      abAttackHover(this,false);
      return r;
   };
   // The tooltip goes when the mouse leaves while pressed, too.
   a.onDragOut = a.onRollOut;
}

function abStyleAttacks(wa)
{
   wa._x = wa._y = 0;
   if(!wa.sanctuary)
   {
      wa.sanctuary = true;
      // The two column separators and the unused effects label.
      wa.getInstanceAtDepth(13 - 16384)._visible = false;
      wa.getInstanceAtDepth(15 - 16384)._visible = false;
      wa.getInstanceAtDepth(19 - 16384)._visible = false;
   }
   var captions = [wa.m_Attack,wa.m_Special];
   var names = ["Attack","Special Attack"];
   var i;
   for(i = 0; i < 2; i++)
   {
      var box = AB_ATTACKS[i];
      var t = captions[i];
      // The stock captions are string keys this client doesn't have; plain text instead.
      if(t.text != names[i])
      {
         t.text = names[i];
      }
      abFit(t,box[0] + 10,box[1] + 5,box[2] - 20,26,17,0xFFFFFF,"center");
      t.filters = abShadow();
      abLayoutAttack(wa["m_Ability" + (i + 1)],box);
      abWrapRoll(wa["m_Ability" + (i + 1)]);
   }
}

// --- the traits ------------------------------------------------------------------------

function abLayoutTrait(pa, i)
{
   var a = pa["m_Ability" + (i + 1)];
   var box = abFrame.traits[i].box;
   var cx = box[0] + box[2] / 2;
   var cy = box[1] + 38;
   // The trait's plate (a 46px shape at (0, 2) in it) sits on the socket's center.
   a._x = cx - 23 - pa._x;
   a._y = cy - 25 - pa._y;
   if(a.sanctuarySocket == undefined)
   {
      a.m_Icon.swapDepths(1001);
      abSocket(a.createEmptyMovieClip("sanctuarySocket",1000),23,25,AB_SOCKET,0xFFFFFF,90,2);
      abMask(a,"sanctuaryIconMask",23,25,AB_SOCKET - 4);
      a.onRollOver = function()
      {
         abTraitHover(this,true);
      };
      a.onRollOut = a.onDragOut = function()
      {
         abTraitHover(this,false);
      };
      a.useHandCursor = false;
      a.sanctuaryIndex = i;
   }
   a.m_Icon.setMask(a.sanctuaryIconMask);
   if(!a.sanctuaryHover)
   {
      abIconSize(a.m_Icon,23,25,AB_ICON,119);
   }
   var left = box[0] + 8 - pa._x - a._x;
   var top = box[1] - pa._y - a._y;
   abFit(a.m_Label,left,top + 70,box[2] - 16,26,16,0xFFFFFF,"center");
   a.m_Label.filters = abOutline();
   abFit(a.m_Description,left,top + 96,box[2] - 16,54,12,AB_SOFT,"center");
   a.m_Description.filters = abShadow();

   // Locked: a lock badge on the socket's corner and the level it unlocks at in a pill along the bottom.
   var lock = pa["m_Lock_" + i];
   var level = pa["m_RequiredLevel_" + i];
   var card = abFrame.traits[i];
   var locked = lock != undefined;
   if(card.locked != locked)
   {
      card.locked = locked;
      abDrawCard(card);
   }
   var pill = abFrame.pills["pill" + i];
   if(pill == undefined)
   {
      pill = abFrame.pills.createEmptyMovieClip("pill" + i,i + 1);
   }
   pill.clear();
   // Traits Lua didn't send (fewer than four) aren't shown.
   var shown = pa.m_Abilities[i] != undefined;
   card._visible = a._visible = shown;
   if(locked && shown)
   {
      lock._width = lock._height = 28;
      lock._x = cx + 16 - pa._x;
      lock._y = cy - pa._y;
      lock.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.7,4,4,1,2)];
      a._alpha = 60;
      var data = pa.m_Abilities[i];
      var t = level.m_RequiredLevel;
      t.text = "Unlocks at level " + data.RequiredLevel;
      level._x = box[0] + 8 - pa._x;
      level._y = box[1] + box[3] - 26 - pa._y;
      abFit(t,0,0,box[2] - 16,22,13,0xFFE08A,"center");
      t.filters = abShadow();
      var w = Math.min(box[2] - 12,t.textWidth + 28);
      pill.lineStyle(1,AB_GOLD,70);
      pill.beginFill(0x0A1440,70);
      abRoundRect(pill,cx - w / 2,box[1] + box[3] - 27,w,22,11);
      pill.endFill();
   }
}

function abTraitHover(a, over)
{
   var card = abFrame.traits[a.sanctuaryIndex];
   if(!card.locked)
   {
      abSetOver(card,over);
   }
   a.sanctuaryHover = over;
   var size = over ? AB_ICON_HOVER : AB_ICON;
   gs.TweenLite.to(a.m_Icon,over ? 0.6 : 0.2,{_xscale:size / 119 * 100,_yscale:size / 119 * 100,_x:23 - size / 2,_y:25 - size / 2,ease:over ? abElastic() : abStrong()});
}

function abStyleTraits(pa)
{
   // The stock "Traits" caption (centered on 334 in it) sits in the bay's header.
   pa._x = AB_BAY[0] + AB_BAY[2] / 2 - 334;
   pa._y = AB_BAY[1] + 14;
   if(!pa.sanctuary)
   {
      pa.sanctuary = true;
      pa.getInstanceAtDepth(26 - 16384)._visible = false;
      pa.getInstanceAtDepth(28 - 16384)._visible = false;
      pa.getInstanceAtDepth(30 - 16384)._visible = false;
      abOutlineAt(pa,25);
   }
   var i;
   for(i = 0; i < 4; i++)
   {
      abLayoutTrait(pa,i);
   }
}

// --- stock hooks -----------------------------------------------------------------------

// Lays out whatever the window has created so far (it attaches its parts a moment after loading).
function abSync()
{
   var win = _root.window;
   if(win.m_WeaponSelector != undefined)
   {
      abStyleSelector(win.m_WeaponSelector);
   }
   if(win.m_WeaponAbilities != undefined)
   {
      abStyleAttacks(win.m_WeaponAbilities);
   }
   if(win.m_ProfileAbilities != undefined)
   {
      abStyleTraits(win.m_ProfileAbilities);
   }
   return win.m_WeaponSelector != undefined && win.m_WeaponAbilities != undefined && win.m_ProfileAbilities != undefined;
}

function abPatch()
{
   var sel = abilities.display.WeaponSelector.prototype;
   var displayItem = sel.displayItem;
   sel.displayItem = function(item, direction)
   {
      var r = displayItem.apply(this,arguments);
      abStyleSelector(this);
      // The weapon's name keeps its rarity colour, lifted if it's too dark for the navy.
      var f = this.m_Label.getTextFormat();
      if(f.color != undefined)
      {
         var c = abLight(Number(f.color));
         if(c != f.color)
         {
            f.color = c;
            this.m_Label.setTextFormat(f);
         }
      }
      return r;
   };
   // The stock hover pops the icon to 130% and back to 100%; here it's the bigger icon, and the card lifts.
   sel.scaleIcon = function(pct)
   {
      abWeaponHover(this,pct > 100);
   };

   var attacks = abilities.display.WeaponAbilities.prototype;
   var attacksConfig = attacks.configUI;
   attacks.configUI = function()
   {
      var r = attacksConfig.apply(this,arguments);
      abStyleAttacks(this);
      return r;
   };
   var attacksSetup = attacks.setupAbility;
   attacks.setupAbility = function(index)
   {
      var r = attacksSetup.apply(this,arguments);
      abStyleAttacks(this);
      return r;
   };

   var traits = abilities.display.ProfileAbilities.prototype;
   var traitsSetup = traits.setupAbility;
   traits.setupAbility = function(index)
   {
      var r = traitsSetup.apply(this,arguments);
      abStyleTraits(this);
      return r;
   };
}

abPatch();
abBuildFrame(_root.window);
// The window attaches its parts 0.1s after loading; follow until all three are there.
abFrame.onEnterFrame = function()
{
   if(abSync())
   {
      delete this.onEnterFrame;
   }
};
// ===== End Sanctuary Abilities =====

// ===== Sanctuary Pet Name Bar =====
// Appended to PetNameBar's main timeline by build_client_mods.py.
// The pet name bar (top left while you're with your pet) in the Main Menu's style (ClientMods/MyStuffBrowser/
// main_menu.as): a navy glass pill with the name in white, the gender glyph in a frosted medallion with a gold
// rim, and a gold rename chip with a pencil. Renaming opens the stock title and name pickers, here navy glass
// columns with a gold band on the chosen row, gold arrow chips, and a gold accept and a glass cancel chip.
// Everything is drawn under the stock clips, which stay as invisible hit areas (the buttons keep their clicks).
// The edit bar's plate is an unnamed shape inside petNameEdit, so a cover is drawn over it and the pickers are
// lifted above the cover.

var PN_GOLD = 0xFFC531;
var PN_RIM = 0x9DB8FF;
var PN_SOFT = 0xC9D6FF;
var PN_INK = 0x3A2400;
// The pill and the gender medallion, in petNameSet's coordinates (the stock plate is 0..388 x -25.5..25.5).
var PN_PILL_X = 12;
var PN_PILL_W = 376;
var PN_PILL_H = 42;
var PN_MEDAL_X = 31;
var PN_MEDAL_Y = -1;
var PN_MEDAL_R = 25;
// The gender glyph is a 64px symbol; this fits it inside the medallion.
var PN_GLYPH_SCALE = 64;
var PN_CHIP_R = 16;

// --- drawing helpers -------------------------------------------------------------------

function pnRoundRect(mc, x, y, w, h, r)
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

function pnGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   pnRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function pnCircle(mc, x, y, r)
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

function pnGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   pnRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function pnSparkle(mc, x, y, r, alpha)
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

function pnOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// A navy glass pill with a soft top highlight and a few sparkles (fixed seed).
function pnGlassPill(mc, x, y, w, h, alpha)
{
   var r = h / 2;
   pnGradientRect(mc,x,y,w,h,r,0x2E4FB0,0x141F55,alpha,PN_RIM,80,2.5);
   mc.lineStyle();
   mc.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[30,0],[0,255],{matrixType:"box",x:x - 40,y:y - h * 1.5,w:w * 0.6,h:h * 2.6,r:0});
   pnRoundRect(mc,x + 2,y + 2,w - 4,h - 4,r - 2);
   mc.endFill();
   pnGloss(mc,x + r * 0.6,y + 3,w - r * 1.2,h * 0.42,h * 0.2,22);
   var s = 2626;
   var i;
   for(i = 0; i < 6; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + 70 + s % (w - 140);
      s = (s * 1103515245 + 12345) % 2147483648;
      pnSparkle(mc,sx,y + 6 + s % (h - 12),1 + s % 2,15 + s % 30);
   }
}

// A gold chip (circle) at x, y; over is the hover look.
function pnGoldChip(mc, x, y, r, over)
{
   mc.lineStyle(1.5,0xFFF3C4,100);
   mc.beginGradientFill("linear",over ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x - r,y:y - r,w:r * 2,h:r * 2,r:Math.PI / 2});
   pnCircle(mc,x,y,r);
   mc.endFill();
   pnGloss(mc,x - r * 0.65,y - r * 0.82,r * 1.3,r * 0.65,r * 0.3,50);
}

// A navy glass chip (circle), for the cancel button.
function pnGlassChip(mc, x, y, r, over)
{
   mc.lineStyle(1.5,over ? 0xFFE08A : PN_RIM,over ? 100 : 70);
   mc.beginGradientFill("linear",over ? [0x4F78E8,0x22388C] : [0x3A5CC4,0x1A2C70],[100,100],[0,255],{matrixType:"box",x:x - r,y:y - r,w:r * 2,h:r * 2,r:Math.PI / 2});
   pnCircle(mc,x,y,r);
   mc.endFill();
   pnGloss(mc,x - r * 0.65,y - r * 0.82,r * 1.3,r * 0.65,r * 0.3,30);
}

function pnChipShadow(over)
{
   return over ? [new flash.filters.GlowFilter(PN_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

// A pencil, slanted up to the right, centered on x, y.
function pnPencil(mc, x, y, color)
{
   mc.lineStyle();
   mc.beginFill(color,100);
   mc.moveTo(x - 7,y + 7);
   mc.lineTo(x - 5.5,y + 2);
   mc.lineTo(x + 3,y - 6.5);
   mc.lineTo(x + 6.5,y - 3);
   mc.lineTo(x - 2,y + 5.5);
   mc.lineTo(x - 7,y + 7);
   mc.endFill();
   // The eraser end.
   mc.beginFill(color,100);
   mc.moveTo(x + 4.2,y - 7.7);
   mc.lineTo(x + 5.6,y - 9.1);
   mc.curveTo(x + 6.6,y - 10.1,x + 7.6,y - 9.1);
   mc.lineTo(x + 9.1,y - 7.6);
   mc.curveTo(x + 10.1,y - 6.6,x + 9.1,y - 5.6);
   mc.lineTo(x + 7.7,y - 4.2);
   mc.lineTo(x + 4.2,y - 7.7);
   mc.endFill();
}

function pnCheck(mc, x, y, color)
{
   mc.lineStyle(3.5,color,100);
   mc.moveTo(x - 6,y);
   mc.lineTo(x - 1.5,y + 5);
   mc.lineTo(x + 6.5,y - 5);
}

function pnCross(mc, x, y, color)
{
   mc.lineStyle(3.5,color,100);
   mc.moveTo(x - 5,y - 5);
   mc.lineTo(x + 5,y + 5);
   mc.moveTo(x + 5,y - 5);
   mc.lineTo(x - 5,y + 5);
}

// A chevron pointing up (dir -1) or down (dir 1).
function pnChevron(mc, x, y, dir, color)
{
   mc.lineStyle(3,color,100);
   mc.moveTo(x - 6,y - 3 * dir);
   mc.lineTo(x,y + 3 * dir);
   mc.lineTo(x + 6,y - 3 * dir);
}

// Light text with the dark outline, re-applied whenever the stock code sets text.
function pnLightText(t, color)
{
   var f = new TextFormat();
   f.color = color;
   t.setNewTextFormat(f);
   t.setTextFormat(f);
   t.filters = pnOutline();
}

// --- the name bar --------------------------------------------------------------------------

var pnSet;

function pnDrawSet()
{
   var bar = pnSet.sanctuaryBar;
   bar.clear();
   pnGlassPill(bar,PN_PILL_X,- PN_PILL_H / 2,PN_PILL_W,PN_PILL_H,100);
   pnDrawMedal();
   pnDrawEditChip(false);
}

// The gender medallion: a frosted disc in a gold rim, under the stock glyph.
function pnDrawMedal()
{
   var m = pnSet.sanctuaryMedal;
   var r = PN_MEDAL_R;
   m.clear();
   m._visible = pnSet.gender._visible;
   m.lineStyle(3,PN_GOLD,100);
   m.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:- r - 6,y:- r - 10,w:r * 2 + 8,h:r * 2 + 8,r:0});
   pnCircle(m,0,0,r);
   m.endFill();
   m.lineStyle(1,0xFFF3C4,90);
   pnCircle(m,0,0,r + 2.5);
   pnGloss(m,- r * 0.6,- r * 0.85,r * 1.2,r * 0.6,r * 0.3,60);
}

function pnDrawEditChip(over)
{
   var c = pnSet.sanctuaryEdit;
   c.clear();
   pnGoldChip(c,0,0,PN_CHIP_R,over);
   pnPencil(c,0,0,PN_INK);
   c.filters = pnChipShadow(over);
}

function pnHover(button, draw)
{
   button.onRollOver = function()
   {
      draw(true);
   };
   button.onRollOut = button.onDragOut = button.onReleaseOutside = function()
   {
      draw(false);
   };
}

function pnBuildSet()
{
   pnSet = petNameSet;
   pnSet.petNameSetBG._alpha = 0;
   var bar = pnSet.createEmptyMovieClip("sanctuaryBar",-16384);
   bar.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.6,12,12,1,2)];

   // The medallion sits over the pill's left end, under the glyph (gender keeps its depth above both).
   var medal = pnSet.createEmptyMovieClip("sanctuaryMedal",-16383 + 4);
   medal._x = PN_MEDAL_X;
   medal._y = PN_MEDAL_Y;
   medal.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
   var g = pnSet.gender;
   g._xscale = g._yscale = PN_GLYPH_SCALE;
   g._x = PN_MEDAL_X - 32 * PN_GLYPH_SCALE / 100;
   g._y = PN_MEDAL_Y - 32 * PN_GLYPH_SCALE / 100;

   // The rename chip under the stock (now invisible) pencil button, which keeps its click.
   var edit = pnSet.createEmptyMovieClip("sanctuaryEdit",-16383 + 1);
   edit._x = pnSet.editButton._x + 18;
   edit._y = pnSet.editButton._y + 18;
   pnSet.editButton._alpha = 0;
   pnHover(pnSet.editButton,pnDrawEditChip);

   pnLightText(pnSet.petNameSet,0xFFFFFF);
   pnDrawSet();
}

// --- renaming ---------------------------------------------------------------------------

var pnEdit;

function pnDrawCover()
{
   var c = pnEdit.sanctuaryCover;
   c.clear();
   // Opaque, and a pixel past the stock plate (-22..366 x -25.5..25.5) on every side, so none of it shows.
   pnGlassPill(c,-23,-26.5,390,53,100);
   pnDrawAccept(false);
   pnDrawCancel(false);
}

function pnDrawAccept(over)
{
   var c = pnEdit.sanctuaryCover.accept;
   c.clear();
   pnGoldChip(c,0,0,PN_CHIP_R,over);
   pnCheck(c,0,0,PN_INK);
   c.filters = pnChipShadow(over);
}

function pnDrawCancel(over)
{
   var c = pnEdit.sanctuaryCover.cancel;
   c.clear();
   pnGlassChip(c,0,0,PN_CHIP_R,over);
   pnCross(c,0,0,over ? 0xFFE08A : 0xFFFFFF);
   c.filters = over ? [new flash.filters.GlowFilter(PN_GOLD,0.6,12,12,2,2)] : [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

// A picker column: navy glass with a gold band on the middle row (the chosen one) and gold arrow chips.
function pnStylePicker(p)
{
   p.background._alpha = 0;
   p.up._alpha = p.down._alpha = 0;
   var c = p.createEmptyMovieClip("sanctuaryColumn",-16384);
   var w = 134;
   var h = 160;
   pnGradientRect(c,0,0,w,h,20,0x1A2C70,0x0A1440,92,PN_RIM,45,1.5);
   c.lineStyle();
   c.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[22,0],[0,255],{matrixType:"box",x:-40,y:-60,w:200,h:160,r:0});
   pnRoundRect(c,2,2,w - 4,h - 4,18);
   c.endFill();
   // The list shows five 28px rows from y 6; the third is the selection.
   c.lineStyle(1.5,PN_GOLD,80);
   c.beginGradientFill("linear",[PN_GOLD,0xF5A623],[30,18],[0,255],{matrixType:"box",x:4,y:62,w:126,h:28,r:Math.PI / 2});
   pnRoundRect(c,5,62,124,28,12);
   c.endFill();
   c.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.6,12,12,1,2)];

   // The arrows (stock buttons, 36x30, the down one turned over) sit on the top and bottom edges.
   var arrows = p.createEmptyMovieClip("sanctuaryArrows",-16383 + 3);
   pnGoldChip(arrows,67,0,13,false);
   pnChevron(arrows,67,-1,-1,PN_INK);
   pnGoldChip(arrows,67,160,13,false);
   pnChevron(arrows,67,161,1,PN_INK);
   arrows.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,6,6,1,2)];
}

function pnBuildEdit()
{
   pnEdit = petNameEdit;
   var cover = pnEdit.createEmptyMovieClip("sanctuaryCover",1000);
   cover.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.6,12,12,1,2)];
   var accept = cover.createEmptyMovieClip("accept",1);
   accept._x = pnEdit.acceptButton._x + 18;
   accept._y = pnEdit.acceptButton._y + 18;
   var cancel = cover.createEmptyMovieClip("cancel",2);
   cancel._x = pnEdit.cancelButton._x + 18;
   cancel._y = pnEdit.cancelButton._y + 18;
   // The stock buttons stay under the cover as invisible hit areas (a clip without handlers takes no clicks).
   pnEdit.acceptButton._alpha = pnEdit.cancelButton._alpha = 0;
   pnHover(pnEdit.acceptButton,pnDrawAccept);
   pnHover(pnEdit.cancelButton,pnDrawCancel);
   pnDrawCover();

   // The pickers above the cover.
   pnStylePicker(pnEdit.PetNameTitlePicker);
   pnStylePicker(pnEdit.PetNamePicker);
   pnEdit.PetNameTitlePicker.swapDepths(1001);
   pnEdit.PetNamePicker.swapDepths(1002);
   pnLightText(pnEdit.textEntry_txt,0xFFFFFF);
}

// Picker rows are white, the chosen one gold (PetNameItem's own formats); they get the outline too.
function pnPatchItems()
{
   var proto = PetNameItem.prototype;
   var format = proto.format;
   proto.format = function(value)
   {
      format.apply(this,arguments);
      this.label.filters = pnOutline();
   };
}

// --- Lua and stock hooks ---------------------------------------------------------------

var pnSetPetName = setPetName;
setPetName = function(sName)
{
   pnSetPetName.apply(_root,arguments);
   pnLightText(pnSet.petNameSet,0xFFFFFF);
};

var pnSetPetGender = setPetGender;
setPetGender = function(genderId)
{
   pnSetPetGender.apply(_root,arguments);
   pnSet.sanctuaryMedal._visible = pnSet.gender._visible;
};

// Leaving or entering rename mode drops any hover look left on the chips.
var pnToggleNameSelector = toggleNameSelector;
toggleNameSelector = function(value)
{
   pnToggleNameSelector.apply(_root,arguments);
   pnDrawEditChip(false);
   pnDrawAccept(false);
   pnDrawCancel(false);
};

pnPatchItems();
pnBuildSet();
pnBuildEdit();
// ===== End Sanctuary Pet Name Bar =====

// ===== Sanctuary Character Create Type =====
// Appended to characterCreateType's main timeline by build_client_mods.py.
// Redraws the first step of making a character (choose female or male, then human or pixie) in the Main Menu's
// style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass panel with a gold "Create a Hero" title, the four
// choices as glass cards in a 2x2 grid with their symbol in a frosted medallion (the chosen one rimmed in gold
// with a gold check; the kinds stay dimmed until a gender is picked, as in the stock screen), a gold summary pill
// ("Female Pixie"), a gold Let's Go! pill (dimmed until both are picked) and a glass Back pill.
// The stock CLIK buttons are resized over the cards as invisible hit areas, so every click runs the stock handlers
// (typeSelected, SelectedGo, returnToSelect). The stock panel and its two question lines are plain shapes and
// unnamed text, dropped at build time (REMOVED_DEPTHS). Nothing here needs the world to be loaded.

var CT_W = 425;
var CT_H = 525;
var CT_CARD_W = 176;
var CT_CARD_H = 92;
var CT_LEFT_X = 30;
var CT_RIGHT_X = 219;
var CT_GENDER_Y = 116;
var CT_RACE_Y = 248;
var CT_GO_Y = 394;
var CT_BACK_Y = 458;
var CT_GOLD = 0xFFC531;
var CT_RIM = 0x9DB8FF;
var CT_SOFT = 0xC9D6FF;
var CT_INK = 0x3A2400;

var ctFrame;
var ctCards = [];
// What the player has picked (0 = nothing yet); the stock variables start at 2 before any click.
var ctGender = 0;
var ctRace = 0;

// --- drawing helpers -------------------------------------------------------------------

function ctRoundRect(mc, x, y, w, h, r)
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

function ctGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   ctRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function ctGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   ctRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function ctCircle(mc, x, y, r)
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

function ctSparkle(mc, x, y, r, alpha)
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

function ctText(mc, name, txt, x, y, w, h, size, color, align)
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

function ctOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// Penner's elastic ease (the movie only has Strong compiled in).
function ctElasticOut(t, b, c, d)
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

// --- glyphs (drawn about 0,0, about 36px across) ---------------------------------------

function ctGlyph(mc, kind)
{
   if(kind == "female")
   {
      mc.lineStyle(5,0xE5577E,100);
      ctCircle(mc,0,-6,10);
      mc.moveTo(0,4);
      mc.lineTo(0,18);
      mc.moveTo(-7,11);
      mc.lineTo(7,11);
   }
   else if(kind == "male")
   {
      mc.lineStyle(5,0x3F7DE8,100);
      ctCircle(mc,-4,4,10);
      mc.moveTo(3,-3);
      mc.lineTo(13,-13);
      mc.moveTo(4,-14);
      mc.lineTo(14,-14);
      mc.lineTo(14,-4);
   }
   else if(kind == "human")
   {
      // A little adventurer: head and shoulders.
      mc.lineStyle();
      mc.beginFill(0x2E4FB0,100);
      ctCircle(mc,0,-8,8);
      mc.endFill();
      mc.beginFill(0x2E4FB0,100);
      mc.moveTo(-15,16);
      mc.curveTo(-15,2,0,2);
      mc.curveTo(15,2,15,16);
      mc.lineTo(-15,16);
      mc.endFill();
   }
   else if(kind == "pixie")
   {
      // Four wings about a tiny body.
      mc.lineStyle(1.5,0x6A3FD0,100);
      mc.beginFill(0x9B6BFF,70);
      mc.moveTo(0,-2);
      mc.curveTo(-20,-22,-16,-2);
      mc.curveTo(-12,4,0,-2);
      mc.moveTo(0,-2);
      mc.curveTo(20,-22,16,-2);
      mc.curveTo(12,4,0,-2);
      mc.moveTo(0,1);
      mc.curveTo(-16,18,-8,12);
      mc.curveTo(-3,8,0,1);
      mc.moveTo(0,1);
      mc.curveTo(16,18,8,12);
      mc.curveTo(3,8,0,1);
      mc.endFill();
      mc.lineStyle();
      mc.beginFill(0x6A3FD0,100);
      ctRoundRect(mc,-2,-8,4,18,2);
      mc.endFill();
   }
}

// --- frame -----------------------------------------------------------------------------

function ctBuildFrame()
{
   var f = ctFrame = _root.createEmptyMovieClip("sanctuaryCharacterCreateType",-16384);
   var bg = f.createEmptyMovieClip("bg",1);
   ctGradientRect(bg,8,8,CT_W - 16,CT_H - 16,30,0x2E4FB0,0x141F55,100,CT_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-160,y:-220,w:520,h:460,r:0});
   ctRoundRect(bg,10,10,CT_W - 20,CT_H - 20,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   ctRoundRect(bg,12,12,CT_W - 24,74,26);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   var s = 2727;
   var i;
   for(i = 0; i < 16; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 30 + s % (CT_W - 60);
      s = (s * 1103515245 + 12345) % 2147483648;
      ctSparkle(bg,x,20 + s % 58,1 + s % 3,25 + s % 50);
   }

   var title = ctText(f,"title_txt","Create a Hero",0,14,CT_W,40,28,0xFFE08A);
   title.filters = ctOutline();
   ctText(f,"subtitle_txt","Who will you be?",0,50,CT_W,24,14,CT_SOFT);

   // The two questions, each over its pair of cards, with a gold rule.
   var q1 = ctText(f,"gender_txt","I am...",0,CT_GENDER_Y - 26,CT_W,24,16,0xFFFFFF);
   var q2 = ctText(f,"race_txt","And I'm a...",0,CT_RACE_Y - 26,CT_W,24,16,0xFFFFFF);
   q1.filters = q2.filters = ctOutline();
   var rule = f.createEmptyMovieClip("rule",2);
   rule.beginGradientFill("linear",[CT_GOLD,CT_GOLD,CT_GOLD],[0,80,0],[0,128,255],{matrixType:"box",x:70,y:0,w:CT_W - 140,h:2,r:0});
   ctRoundRect(rule,70,CT_RACE_Y - 34,CT_W - 140,2,1);
   ctRoundRect(rule,70,CT_GO_Y - 46,CT_W - 140,2,1);
   rule.endFill();

   // The summary pill: what has been picked so far.
   var sum = f.summary = f.createEmptyMovieClip("summary",f.getNextHighestDepth());
   sum._x = CT_W / 2;
   sum._y = CT_GO_Y - 22;
   sum.bg = sum.createEmptyMovieClip("bg",1);
   sum.label = ctText(sum,"label_txt","",-120,-11,240,24,15,0xFFE08A);
   sum.label.filters = ctOutline();
}

function ctUpdateSummary()
{
   var sum = ctFrame.summary;
   var words = [];
   if(ctGender != 0 && genderText.text.length > 0)
   {
      words.push(genderText.text);
   }
   if(ctRace != 0 && raceText.text.length > 0)
   {
      words.push(raceText.text);
   }
   sum._visible = words.length > 0;
   sum.label.text = words.join(" ");
   var w = Math.max(90,sum.label.textWidth + 40);
   sum.bg.clear();
   sum.bg.lineStyle(1,CT_GOLD,70);
   sum.bg.beginFill(0x0A1440,55);
   ctRoundRect(sum.bg,- w / 2,-13,w,26,13);
   sum.bg.endFill();
}

// --- cards -----------------------------------------------------------------------------

function ctDrawCard(card)
{
   var w = CT_CARD_W;
   var h = CT_CARD_H;
   var sel = card.isChosen();
   var over = card.over && card.btn.enabled;
   var g = card.bg;
   g.clear();
   if(sel)
   {
      ctGradientRect(g,0,0,w,h,22,over ? 0x4A72E0 : 0x3F66D6,0x1D307A,100,CT_GOLD,100,3);
      g.lineStyle();
      g.beginGradientFill("radial",[CT_GOLD,CT_GOLD],[36,0],[0,255],{matrixType:"box",x:-50,y:-40,w:180,h:h + 80,r:0});
      ctRoundRect(g,2,2,w - 4,h - 4,20);
      g.endFill();
   }
   else
   {
      ctGradientRect(g,0,0,w,h,22,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : CT_RIM,over ? 100 : 45,over ? 2 : 1.5);
   }
   ctGloss(g,4,2,w - 8,h * 0.42,18,16);

   var m = card.medal.bg;
   m.clear();
   m.lineStyle(sel ? 3 : 2,sel ? CT_GOLD : 0xFFFFFF,sel ? 100 : 85);
   m.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:-36,y:-40,w:72,h:72,r:0});
   ctCircle(m,0,0,27);
   m.endFill();
   ctGloss(m,-18,-24,36,16,8,60);

   // The gold check on the chosen card.
   var c = card.check;
   c.clear();
   if(sel)
   {
      c.lineStyle(1.5,0xFFF3C4,100);
      c.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:-12,y:-12,w:24,h:24,r:Math.PI / 2});
      ctCircle(c,0,0,12);
      c.endFill();
      c.lineStyle(3.5,CT_INK,100);
      c.moveTo(-5.5,0);
      c.lineTo(-1.5,4.5);
      c.lineTo(5.5,-4.5);
   }

   card._alpha = card.btn.enabled ? 100 : 45;
   if(sel)
   {
      card.filters = [new flash.filters.GlowFilter(CT_GOLD,over ? 0.7 : 0.5,16,16,2,2)];
   }
   else if(over)
   {
      card.filters = [new flash.filters.GlowFilter(CT_GOLD,0.5,12,12,2,2)];
   }
   else
   {
      card.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
   }
}

function ctMakeCard(btn, x, y, label, glyph, chosen)
{
   var card = ctFrame.createEmptyMovieClip(glyph,ctFrame.getNextHighestDepth());
   card._x = x;
   card._y = y;
   card.btn = btn;
   card.isChosen = chosen;
   card.bg = card.createEmptyMovieClip("bg",1);
   var m = card.medal = card.createEmptyMovieClip("medal",2);
   m._x = 44;
   m._y = CT_CARD_H / 2;
   m.bg = m.createEmptyMovieClip("bg",1);
   var gl = m.createEmptyMovieClip("glyph",2);
   ctGlyph(gl,glyph);
   card.check = card.createEmptyMovieClip("check",3);
   card.check._x = CT_CARD_W - 16;
   card.check._y = 16;
   var t = ctText(card,"label_txt",label,78,CT_CARD_H / 2 - 15,CT_CARD_W - 84,30,20,0xFFFFFF,"left");
   t.filters = ctOutline();

   // The stock button, resized over the card (CLIK sizes itself in its next draw).
   btn._x = x;
   btn._y = y;
   btn.setSize(CT_CARD_W,CT_CARD_H);
   btn.card = card;
   ctListen(btn);
   ctDrawCard(card);
   ctCards.push(card);
   return card;
}

function ctHover(card, over)
{
   card.over = over;
   ctDrawCard(card);
   if(over && card.btn.enabled)
   {
      gs.TweenLite.to(card.medal,0.8,{_xscale:112,_yscale:112,_rotation:-6,ease:ctElasticOut});
   }
   else
   {
      gs.TweenLite.to(card.medal,0.4,{_xscale:100,_yscale:100,_rotation:0,ease:mx.transitions.easing.Strong.easeOut});
   }
}

// --- pills -----------------------------------------------------------------------------

function ctDrawPill(pill, over, down)
{
   var w = pill.w;
   var h = pill.h;
   var g = pill.bg;
   var on = pill.btn.enabled;
   g.clear();
   if(pill.kind == "gold")
   {
      g.lineStyle(1.5,0xFFF3C4,100);
      g.beginGradientFill("linear",over && on ? [0xFFF0B0,0xFFB838] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- w / 2,y:- h / 2,w:w,h:h,r:Math.PI / 2});
   }
   else
   {
      g.lineStyle(1.5,over ? 0xFFE08A : CT_RIM,over ? 100 : 60);
      g.beginGradientFill("linear",over ? [0x3F66D6,0x1D307A] : [0x284A9E,0x162660],[100,100],[0,255],{matrixType:"box",x:- w / 2,y:- h / 2,w:w,h:h,r:Math.PI / 2});
   }
   ctRoundRect(g,- w / 2,- h / 2,w,h,h / 2);
   g.endFill();
   ctGloss(g,- w / 2 + 8,- h / 2 + 2,w - 16,h * 0.42,h * 0.2,pill.kind == "gold" ? 55 : 25);
   pill.body._y = down ? 2 : 0;
   // Dimmed until both choices are made (the stock button is disabled until then).
   pill._alpha = on ? 100 : 40;
   if(over && on)
   {
      pill.filters = [new flash.filters.GlowFilter(CT_GOLD,0.8,16,16,2,2)];
   }
   else
   {
      pill.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
   }
}

function ctMakePill(btn, name, kind, label, w, h, y, size)
{
   var pill = ctFrame.createEmptyMovieClip(name,ctFrame.getNextHighestDepth());
   pill._x = CT_W / 2;
   pill._y = y + h / 2;
   pill.kind = kind;
   pill.w = w;
   pill.h = h;
   pill.btn = btn;
   pill.bg = pill.createEmptyMovieClip("bg",1);
   pill.body = pill.createEmptyMovieClip("body",2);
   var t = ctText(pill.body,"label_txt",label,- w / 2,- size * 0.72,w,size + 12,size,kind == "gold" ? CT_INK : 0xFFFFFF);
   if(kind != "gold")
   {
      t.filters = ctOutline();
   }
   btn._x = CT_W / 2 - w / 2;
   btn._y = y;
   btn.setSize(w,h);
   btn.pill = pill;
   ctListen(btn);
   ctDrawPill(pill,false,false);
   return pill;
}

function ctHoverPill(pill, over, down)
{
   ctDrawPill(pill,over,down);
   var scale = over && pill.btn.enabled ? 106 : 100;
   gs.TweenLite.to(pill,over ? 0.7 : 0.25,{_xscale:scale,_yscale:scale,ease:over ? ctElasticOut : mx.transitions.easing.Strong.easeOut});
}

// --- the stock buttons -----------------------------------------------------------------

// Our handlers go on after the stock ones (CLIK calls listeners in order), so a click's stock work is done first.
function ctListen(btn)
{
   btn.addEventListener("rollOver",_root,"ctOnOver");
   btn.addEventListener("rollOut",_root,"ctOnOut");
   btn.addEventListener("dragOut",_root,"ctOnOut");
   btn.addEventListener("releaseOutside",_root,"ctOnOut");
   btn.addEventListener("press",_root,"ctOnPress");
   btn.addEventListener("click",_root,"ctOnClick");
}

function ctOnOver(e)
{
   var b = e.target;
   if(b.card)
   {
      ctHover(b.card,true);
   }
   else if(b.pill)
   {
      ctHoverPill(b.pill,true,false);
   }
}

function ctOnOut(e)
{
   var b = e.target;
   if(b.card)
   {
      ctHover(b.card,false);
   }
   else if(b.pill)
   {
      ctHoverPill(b.pill,false,false);
   }
}

function ctOnPress(e)
{
   if(e.target.pill)
   {
      ctDrawPill(e.target.pill,true,true);
   }
}

function ctOnClick(e)
{
   var b = e.target;
   if(b == femaleBtn || b == maleBtn)
   {
      ctGender = b == femaleBtn ? 2 : 1;
   }
   else if(b == humanBtn || b == pixieBtn)
   {
      ctRace = b == humanBtn ? 1 : 2;
   }
   if(b.pill)
   {
      ctDrawPill(b.pill,true,false);
   }
   ctRefresh();
}

// The stock code fades its buttons in and out with _alpha; ours stay invisible and the cards follow enabled.
function ctRefresh()
{
   var stock = [femaleBtn,maleBtn,humanBtn,pixieBtn,goBtn,returnBtn];
   var i;
   for(i = 0; i < stock.length; i++)
   {
      stock[i]._alpha = 0;
   }
   for(i = 0; i < ctCards.length; i++)
   {
      ctDrawCard(ctCards[i]);
   }
   ctDrawPill(ctFrame.go,ctFrame.go.btn.hitTest(_root._xmouse,_root._ymouse,true),false);
   ctUpdateSummary();
}

function ctBuild()
{
   ctBuildFrame();
   ctMakeCard(femaleBtn,CT_LEFT_X,CT_GENDER_Y,"Female","female",function()
   {
      return ctGender == 2;
   });
   ctMakeCard(maleBtn,CT_RIGHT_X,CT_GENDER_Y,"Male","male",function()
   {
      return ctGender == 1;
   });
   ctMakeCard(humanBtn,CT_LEFT_X,CT_RACE_Y,"Human","human",function()
   {
      return ctRace == 1;
   });
   ctMakeCard(pixieBtn,CT_RIGHT_X,CT_RACE_Y,"Pixie","pixie",function()
   {
      return ctRace == 2;
   });
   ctFrame.go = ctMakePill(goBtn,"go","gold","Let's Go!",230,52,CT_GO_Y,24);
   ctFrame.back = ctMakePill(returnBtn,"back","glass","Back",150,42,CT_BACK_Y,18);
   genderText._visible = raceText._visible = false;
   ctRefresh();
}

ctBuild();
// ===== End Sanctuary Character Create Type =====

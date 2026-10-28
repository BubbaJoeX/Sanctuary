// ===== Sanctuary Lobby Team =====
// Appended to lobbyLeftSoccer's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The soccer
// lobby's team column (wndLobbyLeftSoccer) in the Main Menu's style: a navy glass frame (the stock curved panel and
// the line under the leader are dropped with REMOVED_DEPTHS) with a card under each of the six slots: a glass card
// for a player (the leader's rimmed in gold with a gold star), a dim recess for an open slot and almost nothing for a
// closed one. The portrait sits in a frosted socket. The invite area is a glass panel and its Invite A Friend button
// a gold pill drawn inside the stock one under its label (now dark ink), so it keeps its clicks and disabled state.

var LBS_W = 270;
var LBS_H = 532;
var LBS_CARD_X = 20;
var LBS_CARD_W = 214;

var lbsFrame;
var lbsCards = [];
var lbsInvite;

function lbsBuild()
{
   lbsFrame = mgUnder(_root,"sanctuaryLobbyLeftSoccer");
   var bg = lbsFrame.createEmptyMovieClip("bg",1);
   mgFrame(bg,4,4,LBS_W - 8,LBS_H - 8,28,0,0);
   mgSparkles(bg,6161,34,LBS_H - 26,LBS_W - 68,12,8);

   // The invite area between the leader and the rest of the team.
   var inv = lbsFrame.createEmptyMovieClip("invite",2);
   mgPanel(inv,LBS_CARD_X,103,LBS_CARD_W,66,18,30,20);
   inv.lineStyle();
   inv.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:LBS_CARD_X + 24,y:0,w:LBS_CARD_W - 48,h:2,r:0});
   mgRoundRect(inv,LBS_CARD_X + 24,99,LBS_CARD_W - 48,2,1);
   inv.endFill();

   var i;
   for(i = 0; i < clips.length; i++)
   {
      var c = clips[i];
      var card = lbsFrame.createEmptyMovieClip("card" + i,10 + i);
      card.slot = c;
      card.host = i == 0;
      card.y0 = c._y - 5;
      card.h = 65;
      card.state = "";
      lbsCards.push(card);
      mgLight(c.firstName,0xFFFFFF);
      mgLight(c.lastName,MG_SOFT);
   }

   mgLight(mcInvite.txt,0xFFFFFF);
   mgLight(mcInvite.txt2,MG_SOFT);
   var b = mcInvite.buttonInvite;
   mgHold(b.buttonStates);
   mgLight(b.iconStates.ICON_UP.textbox,MG_INK,false);
   mgLight(b.iconStates.ICON_ROLL.textbox,MG_INK,false);
   lbsInvite = lbsInnerSkin(b,"gold",{x:-80,y:-17,w:160,h:34});

   lbsFrame.onEnterFrame = lbsTick;
   lbsTick();
}

function lbsState(c)
{
   if(c.player != undefined && c.player != null)
   {
      return "full";
   }
   return c.portrait._visible ? "empty" : "off";
}

function lbsTick()
{
   lbsInnerTick(lbsInvite);
   var i;
   for(i = 0; i < lbsCards.length; i++)
   {
      var card = lbsCards[i];
      var state = lbsState(card.slot);
      if(state != card.state)
      {
         card.state = state;
         lbsDrawCard(card);
      }
   }
}

function lbsDrawCard(card)
{
   var g = card;
   var x = LBS_CARD_X;
   var y = card.y0;
   var w = LBS_CARD_W;
   var h = card.h;
   var px = card.slot._x;
   var py = card.slot._y;
   g.clear();
   g.filters = [];
   if(card.state == "full")
   {
      mgGradientRect(g,x,y,w,h,18,0x284A9E,0x162660,100,card.host ? MG_GOLD : MG_RIM,card.host ? 100 : 45,card.host ? 2.5 : 1.5);
      mgGloss(g,x + 8,y + 3,w - 16,h * 0.35,12,22);
      mgSocket(g,px - 3,py - 3,61,61,14,card.host ? MG_GOLD : MG_RIM,card.host ? 100 : 60,1.5);
      if(card.host)
      {
         mgStar(g,x + w - 16,y + 15,8,MG_GOLD_LIGHT,100);
         g.filters = [new flash.filters.GlowFilter(MG_GOLD,0.55,14,14,2,2)];
      }
      else
      {
         g.filters = mgShadow();
      }
   }
   else if(card.state == "empty")
   {
      mgPanel(g,x,y,w,h,18,35,card.host ? 45 : 25);
      mgPanel(g,px - 3,py - 3,61,61,14,40,30);
   }
   else
   {
      mgPanel(g,x,y,w,h,18,14,10);
   }
}

// A gold pill inside a stock button, between its (held invisible) plate at depth 1 and its label at depth 7, so the
// label stays on top and the pill shows, hides and fades with the button. Drawn by mg_theme's mgDrawSkin.
function lbsInnerSkin(button, kind, rect)
{
   var s = button.createEmptyMovieClip("sanctuaryPill",-16380);
   s.stock = button;
   s.kind = kind;
   s.w = rect.w;
   s.h = rect.h;
   s.over = false;
   s.scale = 100;
   s.speed = 0;
   s.body = s.createEmptyMovieClip("body",1);
   s.body._x = rect.x + rect.w / 2;
   s.body._y = rect.y + rect.h / 2;
   s.bg = s.body.createEmptyMovieClip("bg",1);
   mgDrawSkin(s,false);
   return s;
}

function lbsInnerTick(s)
{
   var b = s.stock;
   var over = b._visible && b._parent._visible && b.enabled != false && b._alpha > 70 && b.hitTest(_root._xmouse,_root._ymouse,false);
   if(over != s.over)
   {
      s.over = over;
      mgDrawSkin(s,over);
   }
   var target = over ? 106 : 100;
   s.speed = s.speed * 0.62 + (target - s.scale) * 0.28;
   s.scale += s.speed;
   s.body._xscale = s.body._yscale = s.scale;
}

lbsBuild();
// ===== End Sanctuary Lobby Team =====

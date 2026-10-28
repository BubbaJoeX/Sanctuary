// ===== Sanctuary Lobby Players =====
// Appended to lobbyLeft's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The group lobby's
// player column (wndLobbyLeft) in the Main Menu's style: a navy glass frame (the stock curved panel and its divider
// lines are dropped with REMOVED_DEPTHS) with a card under each of the five slots. A slot with a player is a glass
// card, the host's rimmed in gold with a gold star; a slot still waiting for a player is a dim recess, and a closed
// slot (disableClip) all but fades away. Names are white and "Host" gold, both outlined. The cards follow the stock
// slot state every frame, so addPlayer / removePlayer / hideAllClips need no wrapping.

var LBL_W = 270;
var LBL_H = 532;
var LBL_CARD_X = 18;
var LBL_CARD_W = 232;

var lblFrame;
var lblCards = [];

function lblBuild()
{
   lblFrame = mgUnder(_root,"sanctuaryLobbyLeft");
   var bg = lblFrame.createEmptyMovieClip("bg",1);
   mgFrame(bg,4,4,LBL_W - 8,LBL_H - 8,28,0,0);
   mgSparkles(bg,7373,34,LBL_H - 32,LBL_W - 68,14,8);

   var i;
   for(i = 0; i < clips.length; i++)
   {
      var c = clips[i];
      var card = lblFrame.createEmptyMovieClip("card" + i,10 + i);
      card.slot = c;
      card.host = i == 0;
      // The host's slot holds a bigger portrait; the others sit 90 apart.
      card.y0 = card.host ? c._y - 79 : c._y - 65;
      card.h = card.host ? 110 : 86;
      card.state = "";
      lblCards.push(card);
      mgLight(c.playerName.textbox,0xFFFFFF);
      mgLight(c.playerHost.textbox,MG_GOLD_LIGHT);
   }
   lblFrame.onEnterFrame = lblTick;
   lblTick();
}

// "full" (a player), "empty" (waiting for one) or "off" (closed by disableClip).
function lblState(c)
{
   if(c.player != undefined && c.player != null)
   {
      return "full";
   }
   if(c.emptyPortrait._visible || c.loadingAnim._visible || c.portrait._visible)
   {
      return "empty";
   }
   return "off";
}

function lblTick()
{
   var i;
   for(i = 0; i < lblCards.length; i++)
   {
      var card = lblCards[i];
      var state = lblState(card.slot);
      if(state != card.state)
      {
         card.state = state;
         lblDrawCard(card);
      }
   }
}

function lblDrawCard(card)
{
   var g = card;
   var x = LBL_CARD_X;
   var y = card.y0;
   var w = LBL_CARD_W;
   var h = card.h;
   g.clear();
   g.filters = [];
   if(card.state == "full")
   {
      mgGradientRect(g,x,y,w,h,18,0x284A9E,0x162660,100,card.host ? MG_GOLD : MG_RIM,card.host ? 100 : 45,card.host ? 2.5 : 1.5);
      mgGloss(g,x + 8,y + 3,w - 16,h * 0.35,12,22);
      if(card.host)
      {
         mgStar(g,x + w - 20,y + 18,9,MG_GOLD_LIGHT,100);
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
   }
   else
   {
      mgPanel(g,x,y,w,h,18,14,10);
   }
}

lblBuild();
// ===== End Sanctuary Lobby Players =====

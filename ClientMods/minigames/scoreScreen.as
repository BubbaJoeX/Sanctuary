// ===== Sanctuary Score Screen =====
// Appended to scoreScreen's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The end-of-game
// screen (wndScoreScreen: the win/lose phrase, Total Score, and the Rewards, Stats, Score, Race and Derby pages) in
// the Main Menu's style. The stock blue sunburst frame is dropped (REMOVED_DEPTHS) for the navy glass frame; once the
// phrase has risen to the top, a sparkling header band with a gold rule and a recess for the page fade in under it.
// Total Score is a gold pill, the page title gold, the close button the gold circle, and the page buttons glass
// cards with their page's icon in a frosted socket and a gold arrow disc (gold rim and glow on hover). Rewards are
// glass cards with the item in a frosted socket, lighter with a gold rim on hover. Membership no longer exists: the
// Members Only Bonus panel and its Upgrade button never show (the rewards keep the full grid) and no Members Only
// icon is shown. The win/lose lettering, the numbers and the pages' rows are the movie's own.

var SSC_W = 1024;
var SSC_H = 768;
var SSC_FRAME_X = 44;
var SSC_FRAME_Y = 12;
var SSC_FRAME_W = 936;
var SSC_FRAME_H = 744;
var SSC_PANEL_X = 72;
var SSC_PANEL_W = 880;
var SSC_PANEL_BOTTOM = 726;
// The page buttons: the stock plate (148x36 at 0,0) and the label above it are covered by the card.
var SSC_NAV_Y = -12;
var SSC_NAV_W = 148;
var SSC_NAV_H = 100;
// The reward cards (RewardButton: 219x93, the icon centered at 46,41).
var SSC_CARD_W = 219;
var SSC_CARD_H = 93;
var SSC_ICON_X = 46;
var SSC_ICON_Y = 41;

var sscFrame;
var sscPanelTop = -1;

// --- frame and panels --------------------------------------------------------------------

function sscBuild()
{
   sscFrame = mgUnder(_root,"sanctuaryScoreScreen");
   var bg = sscFrame.createEmptyMovieClip("bg",1);
   mgFrame(bg,SSC_FRAME_X,SSC_FRAME_Y,SSC_FRAME_W,SSC_FRAME_H,36,0,0);
   // The header band and the page recess appear once a page is up (sscPanelTick).
   sscFrame.panels = sscFrame.createEmptyMovieClip("panels",2);
   sscFrame.panels._alpha = 0;
   sscFrame.panels.onEnterFrame = sscPanelTick;

   mgSkin(m_CloseButton,"close","",{x:0,y:0,w:77,h:72});
   sscNavButton(m_PreviousButton,true);
   sscNavButton(m_NextButton,false);
   sscHeader();
}

// Draws the header band above the page and the recess behind it, from the top of the page on show.
function sscDrawPanels(top)
{
   var p = sscFrame.panels;
   p.clear();
   var headerBottom = top - 22;
   p.beginFill(MG_NAVY,30);
   mgRoundRect(p,SSC_FRAME_X + 6,SSC_FRAME_Y + 6,SSC_FRAME_W - 12,headerBottom - SSC_FRAME_Y - 6,30);
   p.endFill();
   mgSparkles(p,6161,SSC_FRAME_X + 40,SSC_FRAME_Y + 14,SSC_FRAME_W - 80,Math.max(10,headerBottom - SSC_FRAME_Y - 40),30);
   p.lineStyle();
   // The gold rule runs between the page buttons.
   p.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:SSC_PANEL_X + 200,y:0,w:SSC_PANEL_W - 400,h:2,r:0});
   mgRoundRect(p,SSC_PANEL_X + 200,headerBottom + 6,SSC_PANEL_W - 400,2,1);
   p.endFill();
   mgPanel(p,SSC_PANEL_X,top - 10,SSC_PANEL_W,SSC_PANEL_BOTTOM - top + 10,24,30,20);
}

// Follows the page on show (the stock code sets each page's top from the header's size), fading the panels in.
function sscPanelTick()
{
   var state = StateContainer["State_" + stateManager.getState()];
   var top = state == undefined ? -1 : Math.round(state._y);
   if(top > 0 && top != sscPanelTop)
   {
      sscPanelTop = top;
      sscDrawPanels(top);
   }
   var want = top > 0 ? 100 : 0;
   if(this._alpha != want)
   {
      this._alpha += want > this._alpha ? Math.min(10,want - this._alpha) : - Math.min(10,this._alpha - want);
   }
}

// --- header: the page title, zone name and Total Score -----------------------------------

function sscHeader()
{
   mgLight(statusHolder.m_PageTitle.label,MG_GOLD_LIGHT);
   mgLight(statusHolder.instanceName.nameText,0xFFFFFF);
   var total = statusHolder.m_TotalScore;
   mgLight(total.title.titleName,MG_GOLD_LIGHT);

   // A gold pill over the green one (an unnamed shape right under the number), with the number in ink on it.
   var sf = total.scoreField;
   var pill = sf.createEmptyMovieClip("sscPill",10);
   mgGradientRect(pill,-80,-23,160,46,23,MG_GOLD_LIGHT,MG_GOLD_DEEP,100,MG_GOLD_RIM,100,2);
   mgGloss(pill,-70,-21,140,18,9,50);
   mgStar(pill,-56,0,11,0xFFF3C4,100);
   mgStar(pill,-56,0,6,MG_GOLD_DEEP,100);
   pill.filters = [new flash.filters.GlowFilter(MG_GOLD,0.45,14,14,2,2)];
   pill.num_txt = mgText(pill,"num_txt","",-44,-16,118,34,24,MG_INK);
   pill.onEnterFrame = function()
   {
      var n = this._parent.num.text;
      if(this.num_txt.text != n)
      {
         this.num_txt.text = n;
      }
   };
}

// --- page buttons ------------------------------------------------------------------------

// A glass card over the stock plate (which the icon is moved above to make room), its arrow a gold disc.
function sscNavButton(b, prev)
{
   if(b == undefined)
   {
      return;
   }
   b.icon.swapDepths(-16381);
   var p = b.createEmptyMovieClip("sscPlate",-16382);
   p.btn = b;
   p.prev = prev;
   p.over = false;
   p.bg = p.createEmptyMovieClip("bg",1);
   p.disc = p.createEmptyMovieClip("disc",2);
   p.disc._x = prev ? 35 : 113;
   p.disc._y = 50;
   mgHold(b.arrow);
   mgLight(b.label,0xFFFFFF);
   sscDrawNav(p,false);
   p.onEnterFrame = sscNavTick;
}

function sscDrawNav(p, over)
{
   var g = p.bg;
   g.clear();
   mgGradientRect(g,0,SSC_NAV_Y,SSC_NAV_W,SSC_NAV_H,24,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? MG_GOLD_LIGHT : MG_RIM,over ? 100 : 60,2);
   mgGloss(g,10,SSC_NAV_Y + 3,SSC_NAV_W - 20,26,12,25);
   // The page's icon sits in a frosted socket.
   var ix = p.btn.icon._x;
   var iy = p.btn.icon._y;
   mgSocketCircle(g,ix,iy,30,over ? MG_GOLD : MG_RIM,over ? 100 : 60,2);
   p.filters = over ? [new flash.filters.GlowFilter(MG_GOLD,0.7,16,16,2,2)] : [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.55,8,8,1,2)];

   var d = p.disc;
   d.clear();
   mgGoldCircle(d,18,over);
   d.lineStyle(4,MG_INK,100);
   var dir = p.prev ? -1 : 1;
   d.moveTo(- dir * 4,-7);
   d.lineTo(dir * 4,0);
   d.lineTo(- dir * 4,7);
   d.filters = [new flash.filters.DropShadowFilter(2,90,MG_SHADOW,0.6,4,4,1,2)];
}

function sscNavTick()
{
   var b = this.btn;
   var over = b._visible && b.hitTest(_root._xmouse,_root._ymouse,true);
   if(over != this.over)
   {
      this.over = over;
      sscDrawNav(this,over);
   }
   var target = over ? 115 : 100;
   this.disc._xscale += (target - this.disc._xscale) * 0.35;
   this.disc._yscale = this.disc._xscale;
}

// --- reward cards ------------------------------------------------------------------------

// Each RewardButton (the rewards grid, the mystery chest) gets a glass card under its icon and name, with the
// item in a frosted socket; its beige plate, icon well and blue rim go invisible. Its name stays white (or the
// item's rarity colour) with an outline.
function sscStyleCard(item)
{
   if(item.sscCard != undefined)
   {
      return;
   }
   item.sscCard = item.createEmptyMovieClip("sscCard",-16384);
   item.background._alpha = 0;
   item.states._alpha = 0;
   item.iconBackground._alpha = 0;
   item.label.filters = mgOutline();
   sscDrawCard(item,false);
}

function sscDrawCard(item, over)
{
   var g = item.sscCard;
   g.clear();
   mgGradientRect(g,2,2,SSC_CARD_W - 4,SSC_CARD_H - 4,18,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? MG_GOLD_LIGHT : MG_RIM,over ? 100 : 45,over ? 2.5 : 1.5);
   mgGloss(g,10,4,SSC_CARD_W - 20,30,12,over ? 30 : 20);
   mgSocket(g,SSC_ICON_X - 36,SSC_ICON_Y - 34,72,72,16,over ? MG_GOLD : MG_RIM,over ? 100 : 50,2);
   g.filters = over ? [new flash.filters.GlowFilter(MG_GOLD,0.6,14,14,2,2)] : [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.5,8,8,1,2)];
}

function sscPatchClasses()
{
   // Rewards: no Members Only Bonus panel (setMOBonus answers RequestMOBonusData), no upgrade, so the grid keeps
   // its full 4 x 2 mode.
   var rp = _global.scorescreen.states.RewardsState.prototype;
   rp.setMOBonus = function(message, isMember)
   {
      this.SetMOBonusEnabled(false);
   };
   rp._upgradeButtonRelease = function()
   {
   };
   // The panel shows until Lua answers; hidden from the start instead.
   var configUI = rp.configUI;
   rp.configUI = function()
   {
      var result = configUI.apply(this,arguments);
      this.SetMOBonusEnabled(false);
      return result;
   };
   // The item list's Members Only icon never shows.
   var lp = _global.scorescreen.item.InventoryItemList.prototype;
   lp.setMembersOnly = function(value)
   {
      this.membersOnlyIcon._visible = false;
   };
   // The reward cards: styled on their first state change (onLoad), redrawn on hover (2) and press (3).
   var bp = _global.scorescreen.item.InventoryItemButton.prototype;
   var updateState = bp.updateState;
   bp.updateState = function(mode)
   {
      var result = updateState.apply(this,arguments);
      sscStyleCard(this);
      var over = mode == 2 || mode == 3;
      if(over != this.sscOver)
      {
         this.sscOver = over;
         sscDrawCard(this,over);
      }
      return result;
   };
   // A reused card can keep the last item's rarity colour; it starts from white before the stock refresh.
   var refresh = bp.refresh;
   bp.refresh = function()
   {
      var f = new TextFormat();
      f.color = 0xFFFFFF;
      this.label.setNewTextFormat(f);
      this.label.setTextFormat(f);
      return refresh.apply(this,arguments);
   };
}

sscPatchClasses();
sscBuild();
// ===== End Sanctuary Score Screen =====

// ===== Sanctuary Minigame Start Screen =====
// Appended to MinigameStartScreen's main timeline (after minigames/mg_theme.as) by build_client_mods.py.
// The start screen a minigame opens with (wndMinigameStartScreen: the game's name, difficulty, description and
// objective, its prizes or high scores, and Go) in the Main Menu's style: the navy glass frame with the name in gold
// over a sparkling header, difficulty on a glass pill, the objective on a glass strip, a gold rule, the prizes and
// high scores in a recess under a gold-ruled title strip (prizes as glass cards with the art in frosted sockets,
// scores as glass rows with the top three ranks in gold, silver and bronze), Go as a big gold pill that still
// pulses, the gold close circle, and a navy Prizes / High Scores toggle. The flyout that slid out with a Station
// Cash item to buy keeps only the game's crest and a gold Switch Jobs pill, and the members-only Upgrade button
// never shows (Station Cash and membership no longer exist). The stock clips keep their roles: their art is made
// invisible and the new art drawn under them, so clicks, tweens and Lua's calls work as before.

var SS_W = 547;
var SS_H = 556;
var SS_HEADER = 112;
// The flyout's panel, behind the crest and Switch Jobs (the ad below them is gone).
var SS_FLY_W = 245;
var SS_FLY_H = 214;
// The list panel's size (its stock backing) and the card a prize sits on.
var SS_LIST_W = 477;
var SS_LIST_H = 256;

var ssFrame;

// --- frame -----------------------------------------------------------------------------

function ssBuild()
{
   var w = window;
   // Above the flyout (depth 1), which slides out from behind the window, and below everything else.
   var f = ssFrame = w.createEmptyMovieClip("sanctuaryFrame",-16382);
   var bg = f.createEmptyMovieClip("bg",1);
   mgFrame(bg,0,0,SS_W,SS_H,30,SS_HEADER,6161);
   mgHold(w.background);

   // A gold rule between the header and the description, where the stock grey line was.
   var rule = f.createEmptyMovieClip("rule",2);
   rule.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:74,y:0,w:392,h:2,r:0});
   mgRoundRect(rule,74,SS_HEADER - 3,392,2,1);
   rule.endFill();
   mgHold(w.getInstanceAtDepth(-16324));

   mgLight(w.m_Title,MG_GOLD_LIGHT);
   mgLight(w.m_Description,0xE4ECFF,false);

   ssBuildDifficulty();
   ssBuildObjective();
   ssBuildPlaceholder();
   ssBuildList();
   ssBuildButtons();
   ssBuildFlyout();
}

function ssBuildDifficulty()
{
   var d = window.m_Difficulty;
   mgHold(d.background);
   var g = mgUnder(d,"sanctuaryPill");
   g.lineStyle(1,MG_RIM,35);
   g.beginFill(MG_NAVY,50);
   mgRoundRect(g,0,2,271,52,18);
   g.endFill();
   mgGloss(g,10,3,251,18,9,14);
   mgLight(d.m_LevelRange,MG_SOFT);
   mgLight(d.m_Bubbles.m_Label,0xFFFFFF);
}

function ssBuildObjective()
{
   var o = window.m_Objective;
   var g = mgUnder(o,"sanctuaryStrip");
   g.lineStyle(1,MG_RIM,30);
   g.beginFill(MG_NAVY,45);
   mgRoundRect(g,-4,-3,452,38,19);
   g.endFill();
   // The objective's state icon in a small frosted socket.
   mgSocketCircle(g,20,17,15,MG_GOLD,80,1.5);
   mgLight(o.m_Label,0xFFFFFF);
}

// The squirrel art shown while there are no prizes or scores: rounded off, with a pale rim that shows with it.
function ssBuildPlaceholder()
{
   var w = window;
   var art = w.m_PlaceHolderArt;
   // The frame clip sits at the window's origin, so the mask can live in it.
   var mask = ssFrame.createEmptyMovieClip("artMask",10);
   mask.beginFill(0,100);
   mgRoundRect(mask,art._x,art._y,art._width,art._height,22);
   mask.endFill();
   art.setMask(mask);
   var rim = mgBelow(w.m_ListPanel,"sanctuaryArtRim");
   rim.lineStyle(2,MG_RIM,45);
   mgRoundRect(rim,art._x,art._y,art._width,art._height,22);
   rim.art = art;
   rim.onEnterFrame = function()
   {
      this._visible = this.art._visible;
   };
}

// --- prizes and high scores ---------------------------------------------------------------

function ssBuildList()
{
   var lp = window.m_ListPanel;
   // The stock sunburst backing (an unnamed clip at depth 1) goes, and an opaque recess covers its spot either way.
   mgHold(lp.getInstanceAtDepth(-16383));
   var g = mgBelow(lp.scrollbar,"sanctuaryPanel");
   mgGradientRect(g,0,0,SS_LIST_W,SS_LIST_H,24,0x1C2F74,0x101A48,100,MG_RIM,35,1.5);
   g.lineStyle();
   g.beginFill(MG_NAVY,55);
   mgRoundRect(g,10,6,SS_LIST_W - 20,36,18);
   g.endFill();
   g.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:40,y:0,w:SS_LIST_W - 80,h:2,r:0});
   mgRoundRect(g,40,44,SS_LIST_W - 80,2,1);
   g.endFill();
   mgHold(lp.m_TitleBar);
   mgLight(lp.m_Title,MG_GOLD_LIGHT);
   mgLight(lp.m_RewardsMessage,MG_SOFT);

   // The Prizes / High Scores toggle: a navy pill under its own (white) label.
   var sw = lp.m_SwitchPanel;
   mgHold(sw.btn);
   mgSkin(sw,"navy","",{x:2,y:3,w:146,h:35},false);

   // Rows and cards are made by the list as it scrolls: style them through their classes.
   var hs = minigame.display.HighScoreItem.prototype;
   var hsUpdate = hs.update;
   hs.update = function()
   {
      var result = hsUpdate.apply(this,arguments);
      ssStyleScoreRow(this);
      return result;
   };
   var ri = minigame.display.RewardItem.prototype;
   var riUpdate = ri.update;
   ri.update = function()
   {
      var result = riUpdate.apply(this,arguments);
      ssStyleRewardCard(this);
      return result;
   };
}

// A high score row: a glass strip, every other one fainter; the top three ranks in gold, silver and bronze.
function ssStyleScoreRow(row)
{
   var g = row.ssBg;
   if(g == undefined)
   {
      g = row.ssBg = row.createEmptyMovieClip("ssBg",-16384);
   }
   var rank = Number(row.getItem()[minigame.display.HighScoreItem.RANK]);
   var odd = Math.round(row._y / 21) % 2 == 1;
   var medal = rank == 1 ? MG_GOLD : (rank == 2 ? 0xDDE5F2 : (rank == 3 ? 0xE9A066 : undefined));
   g.clear();
   if(medal != undefined)
   {
      g.lineStyle(1,medal,70);
      g.beginGradientFill("linear",[0x3F66D6,0x1D307A],[70,70],[0,255],{matrixType:"box",x:0,y:0,w:10,h:20,r:Math.PI / 2});
   }
   else
   {
      g.lineStyle(1,MG_RIM,odd ? 12 : 20);
      g.beginFill(0x284A9E,odd ? 22 : 38);
   }
   mgRoundRect(g,4,1,413,19,9.5);
   g.endFill();
   var color = medal == undefined ? 0xFFFFFF : medal;
   var i;
   for(i = 0; i < 10; i++)
   {
      row.rank["txt" + i].textColor = color;
   }
}

// A prize: a glass card, the art in a frosted socket (in place of the stock plate) and the name outlined.
function ssStyleRewardCard(card)
{
   if(card.ssCard != undefined)
   {
      return;
   }
   card.ssCard = card.createEmptyMovieClip("ssCard",-16384);
   mgGradientRect(card.ssCard,4,46,101,156,18,0x284A9E,0x162660,100,MG_RIM,45,1.5);
   mgGloss(card.ssCard,10,48,89,40,14,22);
   card.ssCard.filters = mgShadow();
   // The icon moves up a depth so the socket fits between it and the stock plate, which it covers.
   card.m_Icon.swapDepths(-16381);
   var s = card.createEmptyMovieClip("ssSocket",-16382);
   mgSocket(s,13,52,90,90,20,MG_GOLD,70,1.5);
   card.m_Name.filters = mgOutline();
}

// --- buttons ------------------------------------------------------------------------------

function ssBuildButtons()
{
   var w = window;
   // Go: a big gold pill under the stock label (dark ink), following the stock button's pulse and fades.
   var go = w.m_ActionButton;
   mgHold(go.UP);
   mgHold(go.HOVER);
   mgHold(go.DOWN);
   mgLight(go.label,MG_INK,false);
   mgSkin(go,"gold","",{x:-10,y:8,w:97,h:56},false);

   var close = w.m_CloseButton;
   mgSkin(close,"close","",{x:0,y:0,w:77,h:72});

   // Membership no longer exists: Go always shows, never the Upgrade button.
   mgHold(w.m_UpgradeButton);
   w.m_UpgradeButton._visible = false;
   var setMembersOnly = w.setMembersOnly;
   w.setMembersOnly = function(value, isMember)
   {
      var result = setMembersOnly.apply(this,arguments);
      this.m_UpgradeButton._visible = false;
      this.m_ActionButton._visible = true;
      return result;
   };
   w.upgradeButtonRelease = function()
   {
   };
}

// --- the flyout -----------------------------------------------------------------------------

function ssBuildFlyout()
{
   var w = window;
   var fl = w.m_Flyout;
   mgHold(fl.background);
   // Its left edge tucks under the frame, so the panel starts at 0 (keeping the flyout's width for its tweens).
   var p = mgUnder(fl,"sanctuaryPanel");
   mgGradientRect(p,0,0,SS_FLY_W,SS_FLY_H,26,0x2E4FB0,0x141F55,100,MG_RIM,70,2);
   mgGloss(p,20,3,SS_FLY_W - 40,60,20,22);
   mgSparkles(p,7171,40,10,SS_FLY_W - 70,40,7);
   // A soft gold halo behind the game's crest.
   p.lineStyle();
   p.beginGradientFill("radial",[MG_GOLD_LIGHT,MG_GOLD_LIGHT],[45,0],[0,255],{matrixType:"box",x:fl.m_icon_up._x - 80,y:fl.m_icon_up._y - 80,w:160,h:160,r:0});
   mgCircle(p,fl.m_icon_up._x,fl.m_icon_up._y,80);
   p.endFill();
   p.filters = [new flash.filters.DropShadowFilter(6,90,MG_SHADOW,0.6,18,18,1,2)];

   // Switch Jobs: a gold pill under its own label (dark ink), dimmed by the stock code when jobs can't change.
   var sj = fl.switchJobsMc;
   mgHold(sj.btn);
   mgLight(sj.label,MG_INK,false);
   sj.enabled = false;
   mgSkin(sj,"gold","",{x:2,y:3,w:146,h:35},false);

   // The Station Cash item to buy, its Buy button and arrows: gone.
   mgHold(fl.m_BuyButton);
   fl.m_BuyButton._visible = false;
   fl.m_Next._visible = false;
   fl.m_Previous._visible = false;
   fl.setItem = function(message)
   {
   };
   fl.gotoMarket = function()
   {
   };
   var setupMarketData = w.setupMarketData;
   w.setupMarketData = function(rowCount)
   {
      return setupMarketData.call(this,0);
   };
}

// Lua turns Switch Jobs on and off; the pill follows (it reads the clip's enabled flag for its hover).
var ssEnableSwitchJobs = enableSwitchJobs;
enableSwitchJobs = function(n)
{
   var result = ssEnableSwitchJobs.apply(_root,arguments);
   window.m_Flyout.switchJobsMc.enabled = n == 1;
   return result;
};

ssBuild();
// ===== End Sanctuary Minigame Start Screen =====

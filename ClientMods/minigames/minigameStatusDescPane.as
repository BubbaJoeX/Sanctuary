// ===== Sanctuary Minigame Description Pane =====
// Appended to minigameStatusDescPane's main timeline (after minigames/mg_theme.as) by build_client_mods.py.
// The top pane of the minigame launch screen (wndMinigameStatusDescPane) in the Main Menu's style. Its three faces
// flip on the stock panel's tween, and each is redrawn inside its stock clip so it flips and hides with it:
// - the game: a navy glass frame with the name in gold on a sparkling recessed strip, the description, the
//   difficulty as a navy glass pill (its pop-up a navy card with the game's difficulty lit in its colour), the
//   results lines as faint rules, a soft gold halo behind the game's art, and High Scores as a gold pill;
// - high scores: a recess with the title over a gold rule, glass score rows (yours in gold) and gold arrow discs;
// - an objective: the title strip, a recess for its text, a coloured status pill (blue under way, green done, navy
//   not started), the star reward as a gold pill, and a Rewards panel with the reward art in frosted sockets.
// The stock panels, strips and buttons are client images loaded at runtime; they're kept invisible (their hit areas
// and Lua roles untouched). Members-only no longer exists: no Members Only badge (the description keeps its full
// width), no members-only objective mark, and the upgrade click does nothing.

// The frame, in the flipping panel's coordinates (the stock backing image's area).
var MSD_FRAME = {x:-380,y:13.6,w:746,h:254};
var MSD_EASY = 0xE9C758;
var MSD_MEDIUM = 0xEF8B41;
var MSD_HARD = 0xD6375B;

var msdTip;

function msdBuild()
{
   var f = mgUnder(panel,"sanctuaryDescPane");
   var bg = f.createEmptyMovieClip("bg",1);
   var r = MSD_FRAME;
   mgFrame(bg,r.x,r.y,r.w,r.h,26,0,8181);
   mgSparkles(bg,8282,r.x + 30,r.y + 6,r.w - 60,54,26);
   // The window ends 2px under the frame, so the shadow stays short.
   bg.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.55,8,8,1,2)];
   mgHold(panel.toppanel_mc);

   msdInfo();
   msdHighScores();
   msdObjective();
}

// A recessed title strip with a gold rule along its foot.
function msdStrip(g, x, y, w, h)
{
   mgPanel(g,x,y,w,h,h / 2,45,30);
   g.lineStyle();
   g.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:x + 30,y:0,w:w - 60,h:2,r:0});
   mgRoundRect(g,x + 30,y + h - 3,w - 60,2,1);
   g.endFill();
}

// A faint rule (the results' divider lines).
function msdRule(g, x, y, w)
{
   g.lineStyle();
   g.beginGradientFill("linear",[MG_RIM,MG_RIM,MG_RIM],[0,45,0],[0,128,255],{matrixType:"box",x:x,y:0,w:w,h:2,r:0});
   mgRoundRect(g,x,y,w,1.5,0.75);
   g.endFill();
}

// A stock button made of up/roll/down images and a label: a gold pill under it, the images invisible, the label dark.
function msdPillButton(b)
{
   if(b == undefined)
   {
      return;
   }
   mgHold(b.up_mc);
   mgHold(b.roll_mc);
   mgHold(b.down_mc);
   mgLight(b.label,MG_INK,false);
   mgSkin(b,"gold","",{x:2,y:1,w:142,h:35},false);
}

// A clip that shows only while another clip does.
function msdFollow(clip, target)
{
   clip.msdTarget = target;
   clip.onEnterFrame = function()
   {
      this._visible = this.msdTarget._visible;
   };
}

// --- the game: name, description, difficulty, results ----------------------------------

function msdInfo()
{
   var info = panel.info;
   var u = mgUnder(info,"msdArt");
   msdStrip(u,4,30,424,41);
   mgHold(info.info_title_bg_mc);
   mgLight(info.infoHeader,MG_GOLD_LIGHT);
   info.start.infoDesc.filters = mgOutline();

   // A soft gold halo behind the game's art.
   var a = info.enemyGraphic;
   var cx = a._x + 107;
   var cy = a._y + 100;
   u.lineStyle();
   u.beginGradientFill("radial",[MG_GOLD_LIGHT,0x8FB4FF,0x8FB4FF],[40,18,0],[0,140,255],{matrixType:"box",x:cx - 140,y:cy - 120,w:280,h:240,r:0});
   mgRoundRect(u,cx - 140,cy - 120,280,240,0);
   u.endFill();

   msdDifficulty(info.difficulty);
   msdPillButton(info.highscoreBtn);
   msdPillButton(info.optionsBtn);
   msdResults(info.finish,["goalsComp","bonusComp","starsEarned","totalTime","won","lost"]);
   msdResults(info.mcEndLevel,["tfScore","tfCoins","tfStars","tfChain","tfItem"]);
   mgHold(info.memberBadge_mc);
}

// The finished and end-of-level summaries: faint rules for the divider images, outlined values.
function msdResults(mc, fields)
{
   if(mc == undefined)
   {
      return;
   }
   var u = mgUnder(mc,"msdArt");
   var i;
   for(i = 1; i <= 4; i++)
   {
      var l = mc["line" + i];
      mgHold(l);
      msdRule(u,l._x,l._y + 3,404);
   }
   for(i = 0; i < fields.length; i++)
   {
      mc[fields[i]].filters = mgOutline();
   }
}

function msdDifficulty(d)
{
   mgHold(d.info_progress_bg_mc);
   var u = mgUnder(d,"msdArt");
   mgGradientRect(u,0,0,215,40,20,0x284A9E,0x162660,100,MG_RIM,60,1.5);
   mgGloss(u,10,2,195,15,7,25);

   // The pop-up: a navy card laid over the stock card's backing (under its rows, texts and gems), with the game's
   // difficulty row lit in its colour; the stock row highlights stay invisible.
   var t = d.mcTooltip;
   mgHold(t.mcEasyBG);
   mgHold(t.mcMediumBG);
   mgHold(t.mcHardBG);
   t.txtEasy.filters = mgOutline();
   t.txtMedium.filters = mgOutline();
   t.txtHard.filters = mgOutline();
   t.txtEasyD.filters = mgOutline();
   t.txtMediumD.filters = mgOutline();
   t.txtHardD.filters = mgOutline();
   msdTip = mgBelow(t.mcMediumBG,"msdTip");
   msdDrawTip();
}

function msdDrawTip()
{
   var g = msdTip;
   if(g == undefined)
   {
      return;
   }
   g.clear();
   mgGradientRect(g,0,-226,248,272,20,0x2E4FB0,0x141F55,100,MG_GOLD,100,2);
   mgGloss(g,10,-224,228,60,16,25);
   var rows = [null,[-207.65,62.35,MSD_EASY],[-133.85,76.85,MSD_MEDIUM],[-46.4,76.85,MSD_HARD]];
   var i;
   for(i = 1; i <= 3; i++)
   {
      var lit = gameDifficulty == i;
      g.lineStyle(lit ? 2 : 1,rows[i][2],lit ? 100 : 35);
      g.beginFill(rows[i][2],lit ? 45 : 8);
      mgRoundRect(g,14.65,rows[i][0],219.15,rows[i][1],14);
      g.endFill();
   }
   g._parent.filters = [new flash.filters.DropShadowFilter(4,90,MG_SHADOW,0.6,12,12,1,2)];
}

// --- high scores -----------------------------------------------------------------------

function msdHighScores()
{
   var h = panel.highscore;
   var u = mgUnder(h,"msdArt");
   mgHold(h.itemHolder);
   mgPanel(u,34,32,518,214,22,40,25);
   u.lineStyle();
   u.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:74,y:0,w:438,h:2,r:0});
   mgRoundRect(u,74,70,438,2,1);
   u.endFill();
   mgLight(h.highscoreHeader,MG_GOLD_LIGHT);
   // The left arrow is the right one mirrored (_xscale -100), and so is its skin.
   mgSkin(h.navArrowLeft,"right","",{x:-20,y:-20,w:40,h:40});
   mgSkin(h.navArrowRight,"right","",{x:-20,y:-20,w:40,h:40});
   mgSkin(h.highscoreBack,"left","",{x:0,y:0,w:39,h:38});
}

// A glass strip behind a score row (rows step 16px down, so it stays inside 4..19); yours is gold.
function msdStyleScore(row, me, i)
{
   if(row == undefined)
   {
      return;
   }
   var b = row.getBounds(row);
   var x = Math.max(-226,b.xMin - 6);
   var w = Math.min(232,b.xMax + 6) - x;
   var g = mgUnder(row,"msdBg");
   if(me)
   {
      g.lineStyle(1,MG_GOLD,90);
      g.beginFill(MG_GOLD,28);
   }
   else
   {
      g.lineStyle();
      g.beginFill(0x284A9E,i % 2 == 0 ? 45 : 22);
   }
   mgRoundRect(g,x,4,w,15,7.5);
   g.endFill();
}

// --- an objective ----------------------------------------------------------------------

function msdObjective()
{
   var o = panel.objDesc;
   var u = mgUnder(o,"msdArt");
   msdStrip(u,4.5,0,590,43);
   mgPanel(u,4,47,404,124,16,25,15);
   mgHold(o.objective_title_bg_mc);
   mgHold(o.membericon);
   mgLight(o.objHeader,MG_GOLD_LIGHT);
   o.objDesc.filters = mgOutline();
   o.objProgText.filters = mgOutline();

   // Rewards: a recessed panel with its heading over a gold rule, and frosted sockets behind the art.
   var rb = o.reward_bg_mc;
   mgHold(rb.holder);
   var ru = mgUnder(rb,"msdArt");
   mgPanel(ru,0,0,185,186,20,45,30);
   ru.lineStyle();
   ru.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:20,y:0,w:145,h:2,r:0});
   mgRoundRect(ru,20,28,145,2,1);
   ru.endFill();
   mgLight(o.rewardsheader,MG_GOLD_LIGHT);
   var i;
   for(i = 1; i <= 4; i++)
   {
      var s = mgUnder(o.reward4["r" + i],"msdSocket");
      mgSocket(s,-3,-3,70,70,14,MG_RIM,90,2);
   }
   var big = mgUnder(o.reward1,"msdSocket");
   mgSocket(big,-12,-12,145,145,26,MG_GOLD,100,3);
   big.filters = [new flash.filters.GlowFilter(MG_GOLD,0.4,14,14,2,2)];
   msdFollow(big,o.reward1.icon);

   // The status pill: its colour follows which stock status bar the flip shows.
   var pb = o.objProgBg;
   mgHold(pb.completed_mc);
   mgHold(pb.notstarted_mc);
   mgHold(pb.progression_mc);
   var p = mgUnder(pb,"msdArt");
   p.pb = pb;
   p.onEnterFrame = msdProgressTick;

   // The star reward as a gold pill with a star in a navy socket.
   var sc = o.starcounter_mc;
   mgHold(sc.holder);
   var su = mgUnder(sc,"msdArt");
   mgGradientRect(su,0,2,118,36,18,MG_GOLD_LIGHT,MG_GOLD_DEEP,100,MG_GOLD_RIM,100,1.5);
   mgGloss(su,8,4,102,14,7,45);
   su.lineStyle(1.5,MG_GOLD_RIM,90);
   su.beginGradientFill("radial",[0x2B4FA8,0x111E52],[100,100],[0,255],{matrixType:"box",x:4,y:4,w:32,h:32,r:0});
   mgCircle(su,20,20,15);
   su.endFill();
   mgStar(su,20,20.5,10,MG_GOLD_LIGHT,100);
   su.filters = [new flash.filters.GlowFilter(MG_GOLD,0.4,10,10,2,2)];
   mgLight(o.objStarReward,MG_INK,false);

   mgSkin(o.objInfoBack,"left","",{x:0,y:0,w:39,h:38});
}

function msdProgressTick()
{
   var pb = this.pb;
   var state = pb.completed_mc._visible ? "done" : (pb.progression_mc._visible ? "going" : "waiting");
   if(state == this.state)
   {
      return;
   }
   this.state = state;
   this.clear();
   if(state == "done")
   {
      mgGradientRect(this,0,0,233,38,19,0x5ED16E,0x23843A,100,0xC8F5C8,100,1.5);
   }
   else if(state == "going")
   {
      mgGradientRect(this,0,0,233,38,19,0x4F7BE8,0x1D307A,100,MG_GOLD_LIGHT,90,1.5);
   }
   else
   {
      mgGradientRect(this,0,0,233,38,19,0x284A9E,0x162660,100,MG_RIM,50,1.5);
   }
   mgGloss(this,10,2,213,14,7,35);
   this.filters = [new flash.filters.DropShadowFilter(2,90,MG_SHADOW,0.5,6,6,1,2)];
}

// --- Lua -------------------------------------------------------------------------------

// No Members Only badge (the 8th argument): the description keeps its full width.
var msdSetInfoData = setMiniGameInfoData;
setMiniGameInfoData = function(id, sGameName, iDifficulty, sDesc, time, stars, iIcon, membersOnly, myMinigameType)
{
   return msdSetInfoData.call(_root,id,sGameName,iDifficulty,sDesc,time,stars,iIcon,0,myMinigameType);
};

// No objective is marked members-only (the last argument).
var msdSetObjectiveData = setMiniGameObjectiveData;
setMiniGameObjectiveData = function(id, sObjName, iIsBonus, sDesc, iRewardStars, sProgress, sStatus, bMembersOnly)
{
   return msdSetObjectiveData.call(_root,id,sObjName,iIsBonus,sDesc,iRewardStars,sProgress,sStatus,false);
};

// The badge's upgrade click (memberClicked) goes nowhere.
upgrade = function()
{
};

// The pop-up's lit row follows the game's difficulty (setMiniGameInfoData and setJobLevel go through here).
var msdEvaluateDifficulty = evaluateEncounterDifficulty;
evaluateEncounterDifficulty = function()
{
   var result = msdEvaluateDifficulty.apply(_root,arguments);
   msdDrawTip();
   return result;
};

var msdAddHighScore = addHighScore;
addHighScore = function(rank, name, guid, score, me, highScoreType)
{
   var n = highScores.length;
   var result = msdAddHighScore.apply(_root,arguments);
   if(highScores.length > n)
   {
      msdStyleScore(highScores[highScores.length - 1],me == 1,n);
   }
   return result;
};

msdBuild();
// ===== End Sanctuary Minigame Description Pane =====

// ===== Sanctuary Minigame End Stage =====
// Appended (after minigames/mg_theme.as) to the empty main timeline script FRAME_SCRIPTS gives MinigameEndStageScreen.
// The between-stages results card (wndMiniGameEndStageScreen, Lua's Show with "name^value^..." pairs) in the Main
// Menu's style: a navy glass frame over the stock 512px panel art (an unnamed shape inside the panel, so the frame is
// drawn on top of it and the Next button is lifted above the frame), the title in gold in its sparkling header, each
// stat as a glass row (name in soft blue, value in white) in a recess, and Next as a gold pill. The title and the
// button's label come from the client's strings through the movie's own LocaleManager, with English fallbacks.
// The panel keeps its behaviour: Show, the pop-in tweens, Next (hide, then CloseWindow) and the click blocker.

var MES_SIZE = 512;
var MES_HEADER = 62;
var MES_ROWS_Y = 84;
var MES_ROWS_H = 320;
// The stats' centre: their name and value fields span -145..151 around it.
var MES_ROW_X = 253;

var mesFrame;
var mesNext;

function mesString(key, fallback)
{
   var s = soe.managers.LocaleManager.getInstance().getStringByName(key);
   if(s == undefined || s.length == 0 || s.substr(0,2) == "##" || s.substr(0,3) == "S##")
   {
      return fallback;
   }
   return s;
}

function mesBuild()
{
   var p = mcSettingsPanel;
   // Above the stock panel art (timeline depth 1) and its title; the Next button goes above it.
   var f = mesFrame = p.createEmptyMovieClip("mesFrame",5);
   p.btnDone.swapDepths(20);
   var bg = f.createEmptyMovieClip("bg",1);
   mgFrame(bg,0,0,MES_SIZE,MES_SIZE,36,MES_HEADER,7373);
   var r = f.createEmptyMovieClip("recess",2);
   mgPanel(r,36,MES_ROWS_Y,MES_SIZE - 72,MES_ROWS_H,22,40,25);
   r.lineStyle();
   r.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:80,y:0,w:MES_SIZE - 160,h:2,r:0});
   mgRoundRect(r,80,MES_HEADER + 6,MES_SIZE - 160,2,1);
   r.endFill();

   // The stock title is held invisible; this one says the same, in gold.
   mgHold(p.tfTitle);
   f.title_txt = mgText(f,"title_txt","",20,14,MES_SIZE - 40,40,28,MG_GOLD_LIGHT);
   f.title_txt.filters = mgOutline();

   // The first stat a little lower than stock (100), so its row clears the recess's top edge.
   p.FirstEntryPosY = 112;

   // Next, centred under the stats.
   p.btnDone._x = (MES_SIZE - 115) / 2;
   mesNext = mgSkin(p.btnDone,"gold","",{x:-12,y:2,w:139,h:50});
   mesNext.label = mgText(mesNext.body,"label_txt","",-69,-14,139,30,20,MG_INK);
   mesLabels();
}

// The strings may only arrive after the first lookup, so they're looked up again on every Show.
function mesLabels()
{
   mesFrame.title_txt.text = mesString("S##EndStageScreen_Title","Stage Complete!");
   mesNext.label.text = mesString("S##ui_mgStage_Next","Next");
}

// A stat: a glass row behind its name and value, centred in the recess.
function mesStyleStat(stat)
{
   if(stat == undefined || stat.mesRow != undefined)
   {
      return;
   }
   stat._x = MES_ROW_X;
   var row = stat.mesRow = mgUnder(stat,"mesRow");
   mgGradientRect(row,-158,-16,322,38,19,0x284A9E,0x162660,100,MG_RIM,45,1.5);
   mgGloss(row,-150,-15,306,15,8,22);
   row.filters = mgShadow();
   mgLight(stat.tfName,MG_SOFT);
   mgLight(stat.tfValue,0xFFFFFF);
}

var mesAddStatEntry = MinigameEndStageScreen.prototype.AddStatEntry;
MinigameEndStageScreen.prototype.AddStatEntry = function(name, value)
{
   var result = mesAddStatEntry.apply(this,arguments);
   mesStyleStat(this.mStats[this.mStats.length - 1]);
   return result;
};

var mesShow = MinigameEndStageScreen.prototype.Show;
MinigameEndStageScreen.prototype.Show = function(unparsedStats)
{
   mesLabels();
   return mesShow.apply(this,arguments);
};

mesBuild();
// ===== End Sanctuary Minigame End Stage =====

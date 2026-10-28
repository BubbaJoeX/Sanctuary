// ===== Sanctuary Fishing Reel Icon =====
// Appended (after minigames/mg_theme.as) to the empty main timeline script FRAME_SCRIPTS gives icon_reel. The reel
// icon fishing shows (wndFishingReel, 64x64) sits on a navy glass medallion with a gold rim, like the HUD's buttons,
// instead of floating bare. The icon itself is untouched.

function rlBuild()
{
   var m = mgUnder(_root,"sanctuaryReel");
   m.lineStyle(2.5,MG_GOLD,100);
   m.beginGradientFill("linear",[0x2E4FB0,0x141F55],[95,95],[0,255],{matrixType:"box",x:2,y:2,w:60,h:60,r:Math.PI / 2});
   mgCircle(m,32,32,30);
   m.endFill();
   mgGloss(m,12,4,40,22,11,30);
}

if(_root.sanctuaryReel == undefined)
{
   rlBuild();
}
// ===== End Sanctuary Fishing Reel Icon =====

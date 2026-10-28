// ===== Sanctuary Fishing Combo Icon =====
// Appended (after minigames/mg_theme.as) to the empty main timeline script FRAME_SCRIPTS gives icon_fish_combo_bbe.
// The combo badge fishing shows (wndFishingCombo, 64x64) is already a round picture with a blue rim; a gold ring is
// drawn over that rim (and a faint shine across the top), so it reads as one of Sanctuary's gold medallions. The
// picture inside is untouched.

function cbBuild()
{
   var m = _root.createEmptyMovieClip("sanctuaryCombo",_root.getNextHighestDepth());
   m.lineStyle(3,MG_GOLD,100);
   mgCircle(m,32,32,30);
   m.lineStyle(1,MG_GOLD_RIM,90);
   mgCircle(m,32,32,28.2);
   m.lineStyle();
   m.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[35,0],[0,255],{matrixType:"box",x:14,y:4,w:36,h:18,r:Math.PI / 2});
   mgRoundRect(m,14,4,36,18,9);
   m.endFill();
}

if(_root.sanctuaryCombo == undefined)
{
   cbBuild();
}
// ===== End Sanctuary Fishing Combo Icon =====

// ===== Sanctuary Pirates Controls =====
// Appended to pir_instructions_bbe's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The
// Pirates controls hint (wndPiratesInstructions: the arrow keys over "Move", Ctrl over "Fire") sits on a navy glass
// card with a gold rim, so its white labels read over the sea. The stock key art and labels are untouched.

function pinBuild()
{
   var g = mgUnder(_root,"sanctuaryControls");
   mgGradientRect(g,3,3,124,164,20,0x2E4FB0,0x141F55,80,MG_GOLD,90,2);
   mgGloss(g,10,5,110,40,14,25);
   g.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.55,8,8,1,2)];
}

pinBuild();
// ===== End Sanctuary Pirates Controls =====

// ===== Sanctuary Fishing Status =====
// Appended to fishingStatus' main timeline (after minigames/mg_theme.as) by build_client_mods.py. The words that pop
// up mid-screen while fishing (wndFishingStatusText: "Hooked!", "Reel it in!" and the combo) in the Sanctuary
// lettering: white with a heavy navy outline and a soft gold halo, over a deep navy shadow, instead of black and
// brown with orange strokes. The pop animation, stars and timing are the stock ones. Every word is set through
// frSetTextPreserveWidth by the frame scripts, so that's where the colours go on. The timeline returns to frame 1
// after each pop, which runs this script again; it only rewraps that function, so running twice is harmless.

function fsStyle(t)
{
   if(t._name == "fishStatusText2")
   {
      // The shadow layer, a few pixels below the words.
      t.textColor = 0x141F55;
      t.filters = [new flash.filters.GlowFilter(MG_SHADOW,0.9,6,6,6,2)];
   }
   else
   {
      t.textColor = 0xFFFFFF;
      t.filters = [new flash.filters.GlowFilter(0x0A1440,1,5,5,12,3),new flash.filters.GlowFilter(MG_GOLD,0.85,14,14,2,2)];
   }
}

var fsSetText = frSetTextPreserveWidth;
frSetTextPreserveWidth = function(t, s)
{
   var result = fsSetText.apply(_root,arguments);
   fsStyle(t);
   return result;
};
// ===== End Sanctuary Fishing Status =====

// ===== Sanctuary Fish Caught =====
// Appended to caughtStatus' main timeline (after minigames/mg_theme.as) by build_client_mods.py. The catch
// announcement (wndFishingCaughtStatusText: the fish's name, size and weight bursting in with its icon) in the
// Sanctuary lettering: the name in gold with a heavy navy outline and a gold halo over a deep navy shadow, size and
// weight in white with the navy outline, instead of cream and brown with orange strokes. The animation, starburst
// and icon are the stock ones. Every line is set through frSetTextPreserveWidth by the frame scripts, so that's
// where the colours go on. The timeline returns to frame 1 after each catch, which runs this script again; it only
// rewraps that function, so running twice is harmless.

function fcStyle(t)
{
   if(t._name == "fishNameText2")
   {
      // The name's shadow layer, a few pixels below it.
      t.textColor = 0x141F55;
      t.filters = [new flash.filters.GlowFilter(MG_SHADOW,0.9,6,6,6,2)];
   }
   else if(t._name == "fishNameText1")
   {
      t.textColor = MG_GOLD_LIGHT;
      t.filters = [new flash.filters.GlowFilter(0x0A1440,1,5,5,12,3),new flash.filters.GlowFilter(MG_GOLD,0.7,16,16,2,2)];
   }
   else
   {
      t.textColor = 0xFFFFFF;
      t.filters = [new flash.filters.GlowFilter(0x0A1440,1,4,4,10,3)];
   }
}

var fcSetText = frSetTextPreserveWidth;
frSetTextPreserveWidth = function(t, s)
{
   var result = fcSetText.apply(_root,arguments);
   fcStyle(t);
   return result;
};
// ===== End Sanctuary Fish Caught =====

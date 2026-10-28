// ===== Sanctuary Pay Attention =====
// Appended to payattention's main timeline by build_client_mods.py.
// The "!" that stretches up and fades over something new (wndPayAttention, 200x600): gold (0xFFE08A, stock orange)
// with a dark navy outline and a soft gold glow, like the Main Menu's accents. The "!" is an unnamed text field in
// mc, found by walking mc's children; the stock payAttention tween is untouched.

var PA_GOLD = 0xFFC531;

function paStyle()
{
   var k;
   for(k in mc)
   {
      if(mc[k] instanceof TextField)
      {
         mc[k].textColor = 0xFFE08A;
         mc[k].filters = [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
      }
   }
   mc.filters = [new flash.filters.GlowFilter(PA_GOLD,0.7,12,12,2,2)];
}

paStyle();
// ===== End Sanctuary Pay Attention =====

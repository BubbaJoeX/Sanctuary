// ===== Sanctuary Derby Crown =====
// Appended to derbyCrown's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The crown over the
// derby leader (wndDerbyCrown) gets a dark edge and a gold glow that slowly breathes, so it reads as a prize over any
// scenery. Lua still turns and scales it (setRotation, setScale).

function dcBuild()
{
   crown.onEnterFrame = function()
   {
      var k = (Math.sin(getTimer() / 300) + 1) / 2;
      this.filters = [new flash.filters.GlowFilter(0x3A2400,0.8,3,3,3,2),new flash.filters.GlowFilter(MG_GOLD,0.45 + 0.4 * k,8 + 8 * k,8 + 8 * k,2,2)];
   };
}

dcBuild();
// ===== End Sanctuary Derby Crown =====

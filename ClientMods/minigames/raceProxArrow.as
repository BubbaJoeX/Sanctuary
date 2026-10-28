// ===== Sanctuary Race Proximity Arrow =====
// Appended to raceProxArrow's and raceProxArrow9's main timelines (after minigames/mg_theme.as) by
// build_client_mods.py. The arrows pointing at racers close behind you (wndRaceProxArrow1..9): the dark purple arrow
// is repainted gold (RESKINS) and gets a soft gold glow and dark edge so it stands out on any track, and the racer's
// name turns from small red text to white with the dark outline. Rotation, scale and portrait are the stock ones.

function raBuild()
{
   proxarrow.filters = [new flash.filters.GlowFilter(0x0A1030,0.8,3,3,3,2),new flash.filters.GlowFilter(MG_GOLD,0.5,8,8,2,2)];
   mgLight(proxName,0xFFFFFF);
}

raBuild();
// ===== End Sanctuary Race Proximity Arrow =====

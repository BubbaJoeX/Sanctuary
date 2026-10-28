// ===== Sanctuary Fishing Target =====
// Appended (after minigames/mg_theme.as) to the empty main timeline script FRAME_SCRIPTS gives fish_moving_target.
// The fishing tutorial's moving target (wndFishingTutorialSplat: green brackets closing on the spot to cast at)
// turns gold: its art is two unnamed instances of one sprite, so the tint goes on the whole movie, mapping the
// greens to Sanctuary's golds (bright green to light gold, dark green edges to amber). The animation is untouched.
// The timeline loops back to frame 1, which only sets the same tint again.

function ftTint()
{
   new Color(_root).setTransform({ra:30,rb:200,ga:70,gb:60,ba:20,bb:0,aa:100,ab:0});
}

ftTint();
// ===== End Sanctuary Fishing Target =====

// ===== Sanctuary no play more nag =====
// Appended to NpxPlayMore's main timeline by build_client_mods.py.
// The "play more" upsell (wndNpxPlayMore: shown after a trial, with a Play More button into the Marketplace) is
// gone: it sold membership content. Lua blocks clicks and hides the HUD while it's open, so it isn't just hidden:
// as soon as it's shown it closes itself the way its No Thanks button does (NoThanks()). Lua loads it on show (and
// unloads it on hide) and fills it then (populate_lua); either closes it, once.

_root._visible = false;

var npClosedAt = -10000;

function npClose()
{
   if(getTimer() - npClosedAt < 1000)
   {
      return;
   }
   npClosedAt = getTimer();
   // After Lua's Show() has finished (it invokes this movie partway through).
   setTimeout(function()
   {
      fscommand("FR_event","NoThanks()");
   },150);
}

var npPopulate = populate_lua;
populate_lua = function(category_id, imageFileName, message, title)
{
   var r = npPopulate.apply(_root,arguments);
   npClose();
   return r;
};

npClose();
// ===== End Sanctuary no play more nag =====

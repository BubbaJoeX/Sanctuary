// ===== Sanctuary no rental nag =====
// Appended to MarketplaceRentalNag's main timeline by build_client_mods.py.
// The "your rental has expired" nag (wndMarketplaceRentalNag, with a button to rent it again in the Marketplace)
// is gone: rentals were Station Cash items, which no longer exist. When Lua fills it (setData, which the panel
// hands to _root on load) it closes itself the way its own X does (Hide()).

_root._visible = false;

// Wrapped before the panel's onLoad makes the _root.setData delegate from it.
var rnSetData = _global.RentalNag.prototype.setData;
_global.RentalNag.prototype.setData = function(s)
{
   var r = rnSetData.apply(this,arguments);
   setTimeout(function()
   {
      fscommand("FR_event","Hide()");
   },150);
   return r;
};
// ===== End Sanctuary no rental nag =====

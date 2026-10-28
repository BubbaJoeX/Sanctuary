// ===== Sanctuary no membership store =====
// Appended to FRMembership's main timeline by build_client_mods.py.
// The membership store (wndFRMembership: buy a membership with Station Cash or a card) is gone: membership and
// Station Cash no longer exist. Its Lua blocks clicks and movement while it's open, so it isn't just hidden: as
// soon as it's shown it closes itself the way its own close button does (CloseWindow()). It's loaded when shown
// (Lua unloads it on hide), and Lua also sets the member flag and fills the price list on show; any of those
// closes it, once.

_root._visible = false;

var fmClosedAt = -10000;

function fmClose()
{
   if(getTimer() - fmClosedAt < 1000)
   {
      return;
   }
   fmClosedAt = getTimer();
   // After Lua's Show() has finished (it invokes this movie partway through).
   setTimeout(function()
   {
      fscommand("FR_event","CloseWindow()");
   },150);
}

// The class's methods, wrapped before configUI makes Lua's delegates from them.
var fmProto = _global.views.FRMembership.prototype;
var fmSetIsMember = fmProto._setIsMember;
fmProto._setIsMember = function()
{
   var r = fmSetIsMember.apply(this,arguments);
   fmClose();
   return r;
};
var fmShowSkus = fmProto._showPopulatedSkus;
fmProto._showPopulatedSkus = function()
{
   var r = fmShowSkus.apply(this,arguments);
   fmClose();
   return r;
};

fmClose();
// ===== End Sanctuary no membership store =====

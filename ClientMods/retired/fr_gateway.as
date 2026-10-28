// ===== Sanctuary no purchase gateway =====
// Appended to FRGateway's main timeline by build_client_mods.py.
// SOE's purchase gateway (wndFRGateway: buy Station Cash or a membership with a card, PayPal, SMS or a game card) is
// gone: Station Cash and membership no longer exist. Its Lua blocks clicks, movement and the keyboard and hides the
// HUD while it's open, so it isn't just hidden: as soon as it's shown it closes itself the way its own close button
// does (CloseWindow()). It's loaded when shown (Lua unloads it on hide), and Lua sets its member flag and its state
// page on show; any of those closes it, once.

_root._visible = false;

var gwClosedAt = -10000;

function gwClose()
{
   if(getTimer() - gwClosedAt < 1000)
   {
      return;
   }
   gwClosedAt = getTimer();
   // After Lua's Show() has finished (it invokes this movie partway through).
   setTimeout(function()
   {
      fscommand("FR_event","CloseWindow()");
   },150);
}

// The view's methods, wrapped before configUI makes Lua's delegates from them.
var gwProto = _global.views.FRGateway.prototype;
var gwSetIsMember = gwProto._setIsMember;
gwProto._setIsMember = function()
{
   var r = gwSetIsMember.apply(this,arguments);
   gwClose();
   return r;
};
var gwSetState = gwProto.setState;
gwProto.setState = function()
{
   var r = gwSetState.apply(this,arguments);
   gwClose();
   return r;
};

gwClose();
// ===== End Sanctuary no purchase gateway =====

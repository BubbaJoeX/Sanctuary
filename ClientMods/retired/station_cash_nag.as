// ===== Sanctuary no Station Cash nag =====
// Appended to SCAvailable's main timeline by build_client_mods.py.
// The login nag about your Station Cash balance and membership (wndSCAvailable, from the nag stack before the
// welcome screen) is gone: Station Cash and membership no longer exist. Lua hides the HUD while it's open and
// shows the next nag when it closes, so it isn't just hidden: as soon as it's shown it closes itself the way its
// own close button does (ExitButtonClicked, which pops the next nag). Lua loads it on show (and unloads it on
// hide) and sets its member flag and balance then; any of those closes it, once.

_root._visible = false;

var scClosedAt = -10000;

function scClose()
{
   if(getTimer() - scClosedAt < 1000)
   {
      return;
   }
   scClosedAt = getTimer();
   // After Lua's Show() has finished (it invokes this movie partway through).
   setTimeout(function()
   {
      fscommand("FR_event","ExitButtonClicked");
   },150);
}

var scSetIsMember = SetIsMember;
SetIsMember = function(is, str)
{
   var r = scSetIsMember.apply(_root,arguments);
   scClose();
   return r;
};

var scSetIsNewbie = SetIsNewbie;
SetIsNewbie = function()
{
   var r = scSetIsNewbie.apply(_root,arguments);
   scClose();
   return r;
};

scClose();
// ===== End Sanctuary no Station Cash nag =====

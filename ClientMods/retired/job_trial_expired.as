// ===== Sanctuary no job trial lock =====
// Appended to JobTrialExpired's main timeline by build_client_mods.py.
// The "your job trial has ended" lock (wndJobTrialExpired: a members-only job's trial, with a membership upgrade
// bundle) is gone: membership no longer exists, so no job is a trial. Lua locks the game dock while it's open, so
// it isn't just hidden: when Lua shows it (JobTrialWindow.show) it closes itself the way its own close button
// does (hide with CLOSE_DEFAULT).

_root._visible = false;

var jtShow = JobTrialWindow.show;
JobTrialWindow.show = function(message)
{
   var r = jtShow.apply(JobTrialWindow,arguments);
   // After Lua's Show() has finished (it invokes this movie partway through).
   setTimeout(function()
   {
      JobTrialWindow.hide(JobTrialWindow.CLOSE_DEFAULT);
   },150);
   return r;
};
// ===== End Sanctuary no job trial lock =====

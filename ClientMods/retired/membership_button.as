// ===== Sanctuary no membership button =====
// Appended to hud_membership_button's main timeline by build_client_mods.py.
// The HUD's Upgrade button (wndUpgradeButton; a click sent onUpgrade() to the membership store) is gone: membership
// no longer exists. The window holds no context, so hiding the movie is enough (a hidden clip takes no clicks).

_root._visible = false;
// ===== End Sanctuary no membership button =====

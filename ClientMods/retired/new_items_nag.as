// ===== Sanctuary no new items nag =====
// Appended to NewItemRem's main timeline by build_client_mods.py.
// The login nag advertising new Marketplace items (wndNewItemRem: "Hot new items", with a members-only badge) is
// gone: the Coin Shop has no new-item ads. Lua hides the HUD while it's open and shows the next nag when it closes,
// so it isn't just hidden: Lua loads it when it's shown (and unloads it on hide), so on load it closes itself the
// way its own close button does (ExitButtonClicked, which pops the next nag).

_root._visible = false;

// After Lua's Show() has finished.
setTimeout(function()
{
   fscommand("FR_event","ExitButtonClicked");
},150);
// ===== End Sanctuary no new items nag =====

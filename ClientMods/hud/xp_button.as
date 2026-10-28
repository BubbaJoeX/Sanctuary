// ===== Sanctuary XP Button =====
// Appended to XpButton's main timeline by build_client_mods.py.
// The stars and coins bank button (wndXpButton), which flips between the two every few seconds, in the Main Menu's
// gold pill style (ClientMods/MyStuffBrowser/main_menu.as): the stock plate is already a gold pill, so its count and
// label are set in dark ink with a light emboss (gold-pill buttons use 0x3A2400 text), the star or coin medallion gets
// a soft gold glow, and hovering lifts the button with a gold glow. The button is attached by Lua's
// loadButtonOnStage_lua, which is wrapped to style it; its FRIconButton/XPButton behaviour is untouched.

var XB_GOLD = 0xFFC531;
var XB_INK = 0x3A2400;

function xbEmboss()
{
   return [new flash.filters.GlowFilter(0xFFF3C4,0.7,3,3,3,2)];
}

function xbStyle(btn)
{
   if(btn == undefined)
   {
      return;
   }
   var states = [btn.up_mc,btn.over_mc,btn.down_mc];
   var i;
   for(i = 0; i < states.length; i++)
   {
      states[i].textField_txt.textColor = XB_INK;
      states[i].textField_txt.filters = xbEmboss();
   }
   btn.stars_label_text.textColor = XB_INK;
   btn.coins_label_text.textColor = XB_INK;
   btn.stars_label_text.filters = xbEmboss();
   btn.coins_label_text.filters = xbEmboss();
   btn.star_mc.filters = btn.coin_mc.filters = [new flash.filters.GlowFilter(0xFFF3C4,0.8,10,10,2,2)];
   btn.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];

   // Hover: a gold glow (the stock roll over still swaps the plate and plays the star or coin spin).
   var over = btn.onRollOver;
   var out = btn.onRollOut;
   btn.onRollOver = function()
   {
      over.apply(this,arguments);
      this.filters = [new flash.filters.GlowFilter(XB_GOLD,0.9,16,16,2,2)];
   };
   btn.onRollOut = function()
   {
      out.apply(this,arguments);
      this.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
   };
   var outside = btn.onReleaseOutside;
   btn.onReleaseOutside = function()
   {
      outside.apply(this,arguments);
      this.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.55,8,8,1,2)];
   };
}

var xbLoadButton = loadButtonOnStage_lua;
loadButtonOnStage_lua = function()
{
   var result = xbLoadButton.apply(_root,arguments);
   xbStyle(xp_button_mc);
   return result;
};
// ===== End Sanctuary XP Button =====

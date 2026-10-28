// ===== Sanctuary Canned Chat =====
// Appended to cannedchat's main timeline by build_client_mods.py.
// The quick chat phrase menu (wndCannedChat). Its category bubbles are repainted at build time (RESKINS): navy glass,
// gold while hovered, with a gold arrow. Phrase bubbles keep the player's chosen chat bubble colour (the menu previews
// it), so only the category labels change here: white with a dark outline, readable on navy and on gold.
// CannedChat.setBubbleColor (a static the bubbles call by name) is wrapped; colour 10 is a category, 11 a hovered one.

var CC_CATEGORY = 10;
var CC_CATEGORY_OVER = 11;

var ccSetBubbleColor = CannedChat.setBubbleColor;
CannedChat.setBubbleColor = function(bubble, color)
{
   var result = ccSetBubbleColor.apply(CannedChat,arguments);
   if(color == CC_CATEGORY || color == CC_CATEGORY_OVER)
   {
      bubble.label_txt.textColor = 0xFFFFFF;
      bubble.label_txt.filters = [new flash.filters.GlowFilter(0x0A1030,0.95,3,3,5,2)];
   }
   else
   {
      bubble.label_txt.filters = [];
   }
   return result;
};
// ===== End Sanctuary Canned Chat =====

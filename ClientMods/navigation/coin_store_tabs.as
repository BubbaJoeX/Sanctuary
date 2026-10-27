// ===== Sanctuary coin store tabs =====
// Appended to navigation's main timeline by build_client_mods.py.
// The Marketplace window's tab bar comes from Lua (marketplace.lua): Marketplace, Coin Shop,
// Membership and Claim. Station Cash and membership no longer exist, so only Coin Shop is added.
// The tabs are told apart by their icons, since this bar is shared with other windows.

// Marketplace (Station Cash store), Membership and Claim tab icons.
var sanctuaryRemovedTabIcons = [798, 703, 28546];

var sanctuaryOriginalAddButton = AddButton;

AddButton = function(parent, iconID, name, id)
{
   var i = 0;
   while(i < sanctuaryRemovedTabIcons.length)
   {
      if(Number(iconID) == sanctuaryRemovedTabIcons[i])
      {
         return undefined;
      }
      i = i + 1;
   }
   sanctuaryOriginalAddButton(parent,iconID,name,id);
   sanctuaryThemeBar();
};

// The bar's backgrounds and button frames take the coin store's dark navy (marketplace/coin_store_only.as);
// the icons on them keep their colours.
var NAVY_TINT = {ra:22, ga:26, ba:34, aa:100, rb:10, gb:22, bb:44, ab:0};

function sanctuaryTint(clip)
{
   if(clip != undefined)
   {
      new Color(clip).setTransform(NAVY_TINT);
   }
}

function sanctuaryThemeBar()
{
   sanctuaryTint(Bar.navButtons.background);
   sanctuaryTint(Bar.barButtons.background);
   var i = 0;
   while(i < Bar.barButtons.buttons.length)
   {
      var button = Bar.barButtons.buttons[i];
      sanctuaryTint(button.UP);
      sanctuaryTint(button.HOVER);
      sanctuaryTint(button.DOWN);
      i = i + 1;
   }
}

var sanctuaryOriginalAddBarButton = AddBarButton;
AddBarButton = function(iconID, graphicID, text, id)
{
   sanctuaryOriginalAddBarButton(iconID,graphicID,text,id);
   sanctuaryThemeBar();
};

var sanctuaryOriginalSetBarButtonGraphic = SetBarButtonGraphic;
SetBarButtonGraphic = function(ID, graphic)
{
   sanctuaryOriginalSetBarButtonGraphic(ID,graphic);
   sanctuaryThemeBar();
};
// ===== End Sanctuary coin store tabs =====

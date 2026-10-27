// ===== Sanctuary Main Menu =====
// Appended to MyStuffBrowser's main timeline by build_client_mods.py.
// Redraws the Main Menu in the home screen's style (ClientMods/welcome/home.as): a new frame, the title in the
// client's Free Realms lettering (sanctuaryMainMenuTitle, TEXT_SYMBOLS) and twelve tiles with their own colors,
// labels and blurbs. Labels are plain text, so they don't depend on the client's string table. The tiles send
// the stock events (onClick below), follow Lua's SetButtonsEnabled, and Equipment and Jobs keep the player's
// portrait and job icon. Shop is the coin store (Station Cash is gone). Icons are client textures embedded
// as sanctuaryMenu* symbols (TEXTURE_SYMBOLS).

var MENU_W = 1024;
var MENU_H = 768;
var MENU_TILE_W = 220;
var MENU_TILE_H = 180;
var MENU_GAP = 18;
var MENU_TOP = 132;

// [label, blurb, icon symbol ("" = portrait or job icon), event, SetButtonsEnabled argument, accent color]
var MENU_TILES = [
   ["Equipment","Outfits and gear","","GotoAboutMe()",0,0xFF8A3D],
   ["Shop","Spend your coins","sanctuaryMenuCoin","GotoMarket()",2,0xFFC531],
   ["Achievements","Your trophy case","sanctuaryMenuTrophy","GotoAchievements()",7,0xF5A623],
   ["Hero's Journal","Quests and stories","sanctuaryMenuJournal","GotoQuests()",9,0xE0604A],
   ["Jobs","Try a new job","","GotoMyJobs()",1,0x2EC4B6],
   ["My Stuff","Everything you own","sanctuaryMenuBag","GotoMyStuff()",6,0xE5577E],
   ["Collections","Things you've found","sanctuaryMenuBook","GotoCollections()",10,0x4D8DF7],
   ["Profile","Your player card","sanctuaryMenuProfile","GotoProfile()",10,0x9B6BFF],
   ["Pets","Your best friends","sanctuaryMenuPaw","GotoMyPets()",4,0x6CCB3C],
   ["Rides","Saddle up","sanctuaryMenuSaddle","GotoMyRides()",11,0xC9884B],
   ["Houses","Home sweet home","sanctuaryMenuHouse","GotoMyHouses()",3,0x9ACD32],
   ["Games","Play and win","sanctuaryMenuPalette","GotoGames()",5,0xF06292]
];

var menuTiles = [];

// --- drawing helpers -----------------------------------------------------------------

function menuRoundRect(mc, x, y, w, h, r)
{
   mc.moveTo(x + r,y);
   mc.lineTo(x + w - r,y);
   mc.curveTo(x + w,y,x + w,y + r);
   mc.lineTo(x + w,y + h - r);
   mc.curveTo(x + w,y + h,x + w - r,y + h);
   mc.lineTo(x + r,y + h);
   mc.curveTo(x,y + h,x,y + h - r);
   mc.lineTo(x,y + r);
   mc.curveTo(x,y,x + r,y);
}

function menuGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
{
   if(rim != undefined)
   {
      mc.lineStyle(rimWidth,rim,rimAlpha);
   }
   else
   {
      mc.lineStyle();
   }
   mc.beginGradientFill("linear",[top,bottom],[alpha,alpha],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   menuRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function menuText(mc, name, txt, x, y, w, h, size, color)
{
   mc.createTextField(name,mc.getNextHighestDepth(),x,y,w,h);
   var t = mc[name];
   t.embedFonts = true;
   t.selectable = false;
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = "center";
   t.setNewTextFormat(f);
   t.text = txt;
   return t;
}

// An image holder the client's loader can fill (it sizes images to the holder's original bounds).
function menuImageHolder(parent, name, size)
{
   var holder = parent.createEmptyMovieClip(name,parent.getNextHighestDepth());
   holder.beginFill(0,0);
   menuRoundRect(holder,0,0,size,size,0);
   holder.endFill();
   return holder;
}

function menuDarker(color, f)
{
   return Math.round((color >> 16 & 0xFF) * f) << 16 | Math.round((color >> 8 & 0xFF) * f) << 8 | Math.round((color & 0xFF) * f);
}

// --- tiles -----------------------------------------------------------------------------

function menuDrawTile(tile, over)
{
   var w = MENU_TILE_W;
   var h = MENU_TILE_H;
   var c = tile.accent;
   var bg = tile.body.bg;
   bg.clear();
   menuGradientRect(bg,0,0,w,h,22,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? c : 0x9DB8FF,over ? 100 : 45,over ? 3 : 2);
   // The tile's color, glowing from the top.
   bg.lineStyle();
   bg.beginGradientFill("radial",[c,c],[over ? 70 : 45,0],[0,255],{matrixType:"box",x:w / 2 - 90,y:-40,w:180,h:180,r:0});
   menuRoundRect(bg,3,3,w - 6,h - 6,20);
   bg.endFill();
   tile.filters = over ? [new flash.filters.GlowFilter(c,0.9,18,18,2,2)] : [new flash.filters.DropShadowFilter(4,90,0x050A24,0.55,10,10,1,2)];
}

function menuMakeTile(i)
{
   var def = MENU_TILES[i];
   var w = MENU_TILE_W;
   var h = MENU_TILE_H;
   var col = i % 4;
   var row = Math.floor(i / 4);
   var menu = _root.sanctuaryMenu;

   // Positioned by its center so the hover and entrance scale from the middle.
   var tile = menu.createEmptyMovieClip("tile" + i,menu.getNextHighestDepth());
   tile.baseX = (MENU_W - (4 * w + 3 * MENU_GAP)) / 2 + col * (w + MENU_GAP) + w / 2;
   tile.baseY = MENU_TOP + row * (h + MENU_GAP) + h / 2;
   tile._x = tile.baseX;
   tile._y = tile.baseY;
   tile.accent = def[5];
   tile.command = def[3];
   tile.enabled = true;
   tile.body = tile.createEmptyMovieClip("body",1);
   tile.body._x = - w / 2;
   tile.body._y = - h / 2;
   tile.body.bg = tile.body.createEmptyMovieClip("bg",1);
   menuDrawTile(tile,false);

   // The icon on a soft halo of the tile's color.
   var halo = tile.body.createEmptyMovieClip("halo",tile.body.getNextHighestDepth());
   halo.beginGradientFill("radial",[0xFFFFFF,def[5],def[5]],[45,30,0],[0,120,255],{matrixType:"box",x:w / 2 - 62,y:6,w:124,h:124,r:0});
   halo.moveTo(w / 2 - 62,6);
   halo.lineTo(w / 2 + 62,6);
   halo.lineTo(w / 2 + 62,130);
   halo.lineTo(w / 2 - 62,130);
   halo.lineTo(w / 2 - 62,6);
   halo.endFill();

   // The icon sits in a clip centered on the halo, so it can wobble about its middle.
   var icon = tile.icon = tile.body.createEmptyMovieClip("icon",tile.body.getNextHighestDepth());
   icon._x = w / 2;
   icon._y = 66;
   if(def[2].length > 0)
   {
      var art = icon.attachMovie(def[2],"art",1);
      art._x = art._y = -46;
      art._width = art._height = 92;
   }
   else
   {
      // Equipment shows the player's portrait and Jobs the current job's icon (SetPortrait, SetJobIcon).
      var frame = icon.createEmptyMovieClip("frame",1);
      if(i == 0)
      {
         menuGradientRect(frame,-46,-46,92,92,18,0xFFFFFF,0xBFD3FF,100,def[5],100,3);
      }
      var holder = menuImageHolder(icon,"holder",i == 0 ? 86 : 92);
      holder._x = holder._y = i == 0 ? -43 : -46;
   }

   var label = menuText(tile.body,"label_txt",def[0],0,h - 56,w,30,20,0xFFFFFF);
   label.filters = [new flash.filters.DropShadowFilter(2,90,0x0A1030,0.9,3,3,1,1)];
   menuText(tile.body,"blurb_txt",def[1],0,h - 28,w,22,13,0xC9D6FF);

   tile.onRollOver = function()
   {
      if(!this.enabled)
      {
         return;
      }
      menuDrawTile(this,true);
      fscommand("FR_event","PlaySound(DockOver)");
      gs.TweenLite.to(this,0.3,{_xscale:105,_yscale:105,_y:this.baseY - 6,ease:mx.transitions.easing.Strong.easeOut});
      gs.TweenLite.to(this.icon,0.8,{_rotation:-8,_xscale:112,_yscale:112,ease:mx.transitions.easing.Elastic.easeOut});
   };
   tile.onRollOut = tile.onReleaseOutside = function()
   {
      menuDrawTile(this,false);
      gs.TweenLite.to(this,0.3,{_xscale:100,_yscale:100,_y:this.baseY,ease:mx.transitions.easing.Strong.easeOut});
      gs.TweenLite.to(this.icon,0.8,{_rotation:0,_xscale:100,_yscale:100,ease:mx.transitions.easing.Elastic.easeOut});
   };
   tile.onRelease = function()
   {
      if(!this.enabled)
      {
         return;
      }
      fscommand("FR_event","PlaySound(DockSelect)");
      fscommand("FR_event",this.command);
   };

   tile._alpha = 0;
   tile._xscale = tile._yscale = 70;
   gs.TweenLite.to(tile,0.6,{_alpha:100,_xscale:100,_yscale:100,delay:0.15 + (row * 4 + col) * 0.04,ease:mx.transitions.easing.Elastic.easeOut});
   menuTiles.push(tile);
}

// --- frame -----------------------------------------------------------------------------

function menuBuild()
{
   // Everything stock goes except the close button.
   var name;
   for(name in _root)
   {
      if(typeof _root[name] == "movieclip" && name != "exit_mc" && _root[name]._parent == _root)
      {
         _root[name]._visible = false;
      }
   }
   title_txt._visible = false;

   var menu = _root.createEmptyMovieClip("sanctuaryMenu",_root.getNextHighestDepth());
   var bg = menu.createEmptyMovieClip("bg",1);
   menuGradientRect(bg,12,12,MENU_W - 24,MENU_H - 24,30,0x2E4FB0,0x141F55,100,0x9DB8FF,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-150,y:-300,w:900,h:700,r:0});
   menuRoundRect(bg,14,14,MENU_W - 28,MENU_H - 28,28);
   bg.endFill();
   bg.beginFill(0x0A1440,30);
   menuRoundRect(bg,16,16,MENU_W - 32,MENU_TOP - 26,26);
   bg.endFill();
   bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];

   // Sparkles in the header (fixed seed, same every time).
   var s = 4242;
   var i;
   for(i = 0; i < 30; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 40 + s % (MENU_W - 80);
      s = (s * 1103515245 + 12345) % 2147483648;
      var y = 24 + s % 80;
      var r = 1 + s % 3;
      bg.beginFill(0xFFFFFF,25 + s % 50);
      bg.moveTo(x,y - r * 2);
      bg.curveTo(x,y,x + r * 2,y);
      bg.curveTo(x,y,x,y + r * 2);
      bg.curveTo(x,y,x - r * 2,y);
      bg.curveTo(x,y,x,y - r * 2);
      bg.endFill();
   }

   var title = menu.attachMovie("sanctuaryMainMenuTitle","title",menu.getNextHighestDepth());
   title._x = Math.round((MENU_W - title._width) / 2);
   title._y = 26;
   menuText(menu,"subtitle_txt","Where to next?",0,title._y + title._height - 2,MENU_W,24,15,0xC9D6FF);

   for(i = 0; i < MENU_TILES.length; i++)
   {
      menuMakeTile(i);
   }

   exit_mc._x = MENU_W - 70;
   exit_mc._y = 22;
   exit_mc.swapDepths(_root.getNextHighestDepth());
}

// --- Lua -------------------------------------------------------------------------------

SetButtonsEnabled = function(n1, n2, n3, n4, n5, n6, n7, n8, n9, n10, n11, n12)
{
   var i;
   for(i = 0; i < menuTiles.length; i++)
   {
      var tile = menuTiles[i];
      tile.enabled = arguments[MENU_TILES[i][4]] == 1;
      // The body, since the entrance tween animates the tile's own alpha.
      tile.body._alpha = tile.enabled ? 100 : 35;
   }
};

SetPortrait = function(value)
{
   _root.frBindPlayerPortrait(String(menuTiles[0].icon.holder),String(value));
};

SetJobIcon = function(iconId)
{
   _root.frSetIconByIndex(menuTiles[4].icon.holder,iconId);
};

// The stock label is a string table lookup; the tile says Equipment.
SetEquipString = function(str)
{
};

menuBuild();
// ===== End Sanctuary Main Menu =====

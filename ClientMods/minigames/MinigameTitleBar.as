// ===== Sanctuary Minigame Title Bar =====
// Appended to MinigameTitleBar's main timeline (after minigames/mg_theme.as) by build_client_mods.py.
// The bar across the top of the screen during a minigame (wndMinigameTitleBar: the game's name, exit, pause and the
// name edit button) in the Main Menu's style: a navy glass pill with a gold rim and a few sparkles, the name in gold
// with the dark outline, and the exit button as a gold tile with the stock X. When exit is clicked once the stock bar
// flips and the exit button stretches across it asking to click again; the new art follows that (the pill flips with
// the stock backing, and the stretched exit button turns into a red pill under the white confirm text). The pause
// button is already gold and stays. The name picker under it (showNamePicker_lua) gets the same navy pill. The stock
// clips keep their roles: their art is held invisible and the new art follows their tweens every frame.

var TB_SPARKLE_SEED = 3131;

// A navy pill under a stock backing that follows its vertical flip (the stock tweens its _y and _yscale); drawn in
// its parent's coordinates across the backing's width, so the ends stay round however the backing is stretched.
function tbPlate(back, name, sparkles)
{
   var p = mgBelow(back,name);
   p.back = back;
   var x = back._x;
   var w = back._width;
   mgGradientRect(p,x,-25,w,50,25,0x2E4FB0,0x141F55,96,MG_GOLD,100,2);
   mgGloss(p,x + 14,-23,w - 28,20,10,30);
   if(sparkles)
   {
      mgSparkles(p,TB_SPARKLE_SEED,x + 30,-20,w - 60,40,8);
   }
   p.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];
   mgHold(back);
   p.onEnterFrame = function()
   {
      this._y = this.back._y;
      this._yscale = this.back._yscale;
      this._visible = this.back._visible;
   };
   p.onEnterFrame();
   return p;
}

// The exit button: a gold tile with the stock X over it, or a red pill once the stock code stretches it across the
// bar to ask for a second click. Redrawn whenever the stock tweens change its size.
function tbBuildExit()
{
   var bar = titleBar_mc;
   var e = bar.m_ExitButton;
   var g = mgBelow(e,"sanctuaryExit");
   g.button = e;
   mgHold(e);
   g.onEnterFrame = function()
   {
      var b = this.button;
      var over = b._visible && b.hitTest(_root._xmouse,_root._ymouse,false);
      var key = b._x + "," + b._y + "," + b._width + "," + b._height + "," + over + "," + b._visible;
      if(key == this.key)
      {
         return;
      }
      this.key = key;
      tbDrawExit(this,b._x,b._y,b._width,b._height,over);
      this._visible = b._visible;
   };
}

function tbDrawExit(g, x, y, w, h, over)
{
   g.clear();
   if(h < 2 || w < 2)
   {
      g.filters = [];
      return;
   }
   var wide = w > 80;
   var r = Math.min(h / 2,wide ? h / 2 : 11);
   if(wide)
   {
      mgGradientRect(g,x,y,w,h,r,over ? 0xFF8A80 : 0xF0605A,over ? 0xD03A3A : 0xB52A2A,100,0xFFD0C8,100,1.5);
   }
   else
   {
      mgGradientRect(g,x,y,w,h,r,over ? 0xFFF0B0 : MG_GOLD_LIGHT,over ? 0xFFB838 : MG_GOLD_DEEP,100,MG_GOLD_RIM,100,1.5);
   }
   mgGloss(g,x + r * 0.6,y + 2,w - r * 1.2,h * 0.4,h * 0.2,50);
   g.filters = over ? [new flash.filters.GlowFilter(wide ? 0xFF6A5A : MG_GOLD,0.8,14,14,2,2)] : [new flash.filters.DropShadowFilter(2,90,MG_SHADOW,0.6,6,6,1,2)];
}

function tbBuild()
{
   var bar = titleBar_mc;
   tbPlate(bar.background,"sanctuaryPlate",true);
   tbBuildExit();
   mgLight(bar.m_Title,MG_GOLD_LIGHT);
   mgLight(bar.m_ExitConfirmText,0xFFFFFF);

   // The name picker's bar.
   tbPlate(nameEdit_mc.background,"sanctuaryPlate",false);
   mgLight(nameEdit_mc.textbox_mc.textEntry_txt,0xFFFFFF,false);
}

tbBuild();
// ===== End Sanctuary Minigame Title Bar =====

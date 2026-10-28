// ===== Sanctuary Lobby Browser =====
// Appended to lobbyTop's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The group lobby's game
// browser (wndLobbyTop: the open games list, Create a New Game's options, Your Game Has Been Created, and Quick Match
// with its Quick Play / Invite Friends cards) in the Main Menu's style. The stock panel (panelBG) stays as the flip
// animation's target but goes invisible; a navy glass frame with a sparkling header redraws itself at its size every
// frame, so it still flips between pages and stretches for Quick Match. Page titles are gold on navy recesses with a
// gold hairline. The games list rows are glass strips (hover lighter with a pale gold rim, the selected game gold
// rimmed and gold tinted, white text) with the game's icon in a frosted socket, and the Members Only badge is gone
// (membership no longer exists). Create a New Game is a gold pill inside the stock button under its own label, Back
// and the Quick Match close button are gold circles, and the Quick Match cards are glass cards with the group icon in
// a frosted socket that rim in gold on hover. Every stock clip keeps its handlers and Lua calls.

var LBT_PANEL_W = 746;
var LBT_PANEL_H = 254;
var LBT_PANEL_Y = 28;
var LBT_CARD_W = 248;
var LBT_CARD_H = 272;
// A games list row (GameItemListRow): its strip and the icon's socket, in the row's own coordinates.
var LBT_ROW_X = -7;
var LBT_ROW_W = 551;
var LBT_ROW_H = 23;
var LBT_ICON_X = 37;
var LBT_ICON_Y = -1;

var lbtFrame;
var lbtFrameW = -1;
var lbtFrameH = -1;
var lbtCreate;
var lbtCards = [];

function lbtBuild()
{
   lbtFrame = mgUnder(_root,"sanctuaryLobbyTop");
   lbtFrame.bg = lbtFrame.createEmptyMovieClip("bg",1);
   mgHold(panelBG);

   // Page titles.
   lbtTitleBar(mode0.titleBar);
   lbtTitleBar(mode1.titleBar);
   lbtTitleBar(mode2.titleBar);
   lbtTitleBar(mode7.titleBar);
   mgLight(myTitle,MG_GOLD_LIGHT);
   mgLight(myTitleCreate,MG_GOLD_LIGHT);
   mgLight(myTitleCreated,MG_GOLD_LIGHT);
   mgLight(mode7.textholderTitle.textbox,MG_GOLD_LIGHT);
   mgLight(myGameDesc,0xFFFFFF);
   mgLight(mode7.gameDesc,0xFFFFFF);
   mgLight(mode0.loadingHolder.textTop.textbox,MG_GOLD_LIGHT);
   mgLight(mode0.loadingHolder.textMiddle.textbox,0xFFFFFF);

   // The Create a New Game options: names white, values and choices outlined.
   var i;
   for(i = 0; i < optionArray.length; i++)
   {
      var o = optionArray[i];
      mgLight(o.textholder.textbox,0xFFFFFF);
      mgLight(o.incrementer.count,MG_GOLD_LIGHT);
      mgLight(o.doubleRadial.textA,0xFFFFFF);
      mgLight(o.doubleRadial.textB,0xFFFFFF);
   }

   // Create a New Game: the pill goes between the button's plate (held invisible) and its label (dark ink).
   mgHold(buttonCreate.buttonStates);
   mgLight(buttonCreate.iconStates.ICON_UP.textbox,MG_INK,false);
   mgLight(buttonCreate.iconStates.ICON_ROLL.textbox,MG_INK,false);
   lbtCreate = lbtInnerSkin(buttonCreate,"gold",{x:-130,y:-18,w:260,h:36},-16380);

   mgSkin(mode1.buttonBack,"left","",{x:-18,y:-18,w:37,h:37});
   mgSkin(mode2.buttonBack,"left","",{x:-18,y:-18,w:37,h:37});
   var b = mode7.mcExitButton.getBounds(mode7.mcExitButton);
   var rect = b.xMax - b.xMin > 4 ? {x:b.xMin,y:b.yMin,w:b.xMax - b.xMin,h:b.yMax - b.yMin} : {x:-34,y:-34,w:68,h:68};
   mgSkin(mode7.mcExitButton,"close","",rect);

   lbtQuickCard(mode7.mcButtonSolo);
   lbtQuickCard(mode7.mcButtonMulti);

   lbtFrame.onEnterFrame = lbtTick;
   lbtTick();
}

// --- the frame, following panelBG's flip ------------------------------------------------

function lbtTick()
{
   var w = Math.round(LBT_PANEL_W * panelBG._xscale / 100);
   var h = Math.round(LBT_PANEL_H * panelBG._yscale / 100);
   if(w != lbtFrameW || h != lbtFrameH)
   {
      lbtFrameW = w;
      lbtFrameH = h;
      var g = lbtFrame.bg;
      g.clear();
      g._visible = w > 12 && h > 12 && panelBG._visible;
      if(g._visible)
      {
         mgFrame(g,panelBG._x - w / 2,LBT_PANEL_Y,w,h,Math.min(30,w / 2),Math.min(44,h - 8),5858);
      }
   }
   lbtInnerTick(lbtCreate);
   var i;
   for(i = 0; i < lbtCards.length; i++)
   {
      lbtCardTick(lbtCards[i]);
   }
}

// A page title's recess: navy glass over the invisible stock bar, with a gold hairline under the title.
function lbtTitleBar(bar)
{
   if(bar == undefined)
   {
      return;
   }
   mgHold(bar);
   var t = mgBelow(bar,"sanctuaryTitleBar");
   t._x = bar._x;
   t._y = bar._y;
   var half = 285.5 * bar._xscale / 100;
   // A little shorter than the stock bar: the games list's first row starts just under it.
   mgGradientRect(t,- half,-21.5,half * 2,34,17,0x13235F,0x0B1745,90,MG_RIM,30,1);
   t.lineStyle();
   t.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:- half + 40,y:0,w:half * 2 - 80,h:2,r:0});
   mgRoundRect(t,- half + 40,10.5,half * 2 - 80,2,1);
   t.endFill();
}

// --- buttons -------------------------------------------------------------------------------

// A pill inside a stock button at depth (between its plate and its label), so the label stays on top and the pill
// shows, hides and fades with the button. Drawn by mg_theme's mgDrawSkin.
function lbtInnerSkin(button, kind, rect, depth)
{
   var s = button.createEmptyMovieClip("sanctuaryPill",depth);
   s.stock = button;
   s.kind = kind;
   s.w = rect.w;
   s.h = rect.h;
   s.over = false;
   s.scale = 100;
   s.speed = 0;
   s.body = s.createEmptyMovieClip("body",1);
   s.body._x = rect.x + rect.w / 2;
   s.body._y = rect.y + rect.h / 2;
   s.bg = s.body.createEmptyMovieClip("bg",1);
   mgDrawSkin(s,false);
   return s;
}

function lbtInnerTick(s)
{
   var b = s.stock;
   // setButtonDisabled clears the handlers; a disabled button doesn't light up.
   var over = b._visible && b._parent._visible && typeof b.onRelease == "function" && b.hitTest(_root._xmouse,_root._ymouse,false);
   if(over != s.over)
   {
      s.over = over;
      mgDrawSkin(s,over);
   }
   var target = over ? 105 : 100;
   s.speed = s.speed * 0.62 + (target - s.scale) * 0.28;
   s.scale += s.speed;
   s.body._xscale = s.body._yscale = s.scale;
}

// --- Quick Match cards -----------------------------------------------------------------

// The stock card is an unnamed shape at the bottom of the button; a glass card goes over it and the text and icons
// are lifted above the card. The stock roll-over brightening and scaling still apply to the whole button.
function lbtQuickCard(btn)
{
   if(btn == undefined)
   {
      return;
   }
   var card = btn.createEmptyMovieClip("sanctuaryCard",1);
   btn.mcText.swapDepths(5);
   btn.mcIconMulti.swapDepths(6);
   btn.mcIconSolo.swapDepths(7);
   card.btn = btn;
   card.over = undefined;
   mgLight(btn.mcText.textbox,MG_GOLD_LIGHT);
   lbtCards.push(card);
   lbtCardTick(card);
}

function lbtCardTick(card)
{
   var b = card.btn;
   var over = b._visible && mode7._visible && b.enabled != false && b.hitTest(_root._xmouse,_root._ymouse,false);
   if(over == card.over)
   {
      return;
   }
   card.over = over;
   var w = LBT_CARD_W;
   var h = LBT_CARD_H;
   card.clear();
   mgGradientRect(card,- w / 2,- h / 2,w,h,26,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? MG_GOLD_LIGHT : MG_RIM,over ? 100 : 50,over ? 3 : 2);
   mgGloss(card,- w / 2 + 10,- h / 2 + 4,w - 20,70,20,25);
   // The group icon's socket.
   mgSocketCircle(card,2,87,40,over ? MG_GOLD : MG_RIM,over ? 100 : 70,2);
   card.filters = over ? mgGoldGlow() : mgShadow();
}

// --- the games list rows (GameItemListRow) -------------------------------------------

function lbtStyleRow(row)
{
   if(row.lbtBg == undefined)
   {
      row.lbtBg = mgUnder(row,"lbtBg");
      row.hitArea._alpha = 0;
      row.shade._alpha = 0;
      row.highlight._alpha = 0;
      row.selectedBG._alpha = 0;
      row.textTitle.filters = mgOutline();
      row.textBody.filters = mgOutline();
   }
   row.membersOnlyIcon._visible = false;
}

function lbtDrawRow(row, over)
{
   lbtStyleRow(row);
   var g = row.lbtBg;
   var x = LBT_ROW_X;
   var y = - LBT_ROW_H / 2;
   var w = LBT_ROW_W;
   var h = LBT_ROW_H;
   var selected = row.selectedBG._visible == true;
   g.clear();
   if(selected)
   {
      mgGradientRect(g,x,y,w,h,h / 2,0x3A5BC4,0x1C2E78,100,MG_GOLD,100,2);
      g.lineStyle();
      g.beginFill(MG_GOLD,22);
      mgRoundRect(g,x + 1,y + 1,w - 2,h - 2,h / 2 - 1);
      g.endFill();
      g.filters = [new flash.filters.GlowFilter(MG_GOLD,0.5,10,10,2,2)];
   }
   else if(over)
   {
      mgGradientRect(g,x,y,w,h,h / 2,0x3F66D6,0x1D307A,100,MG_GOLD_LIGHT,90,1.5);
      g.filters = [];
   }
   else if(row.index % 2 == 1)
   {
      mgGradientRect(g,x,y,w,h,h / 2,0x284A9E,0x162660,75,MG_RIM,25,1);
      g.filters = [];
   }
   else
   {
      mgPanel(g,x,y,w,h,h / 2,35,15);
      g.filters = [];
   }
   mgGloss(g,x + 10,y + 1,w - 20,h * 0.4,4,selected || over ? 25 : 12);
   mgSocketCircle(g,LBT_ICON_X,LBT_ICON_Y,11,selected ? MG_GOLD : MG_RIM,selected ? 100 : 60,1);
}

function lbtPatchRows()
{
   var P = _global.GameItemListRow.prototype;
   var doneLoading = P.doneLoading;
   P.doneLoading = function()
   {
      var result = doneLoading.apply(this,arguments);
      if(this._loaded)
      {
         // The selected game's text was a pale cyan; white reads better on the gold-tinted row.
         this.selectedFormat.color = 0xFFFFFF;
         if(this.selectedBG._visible)
         {
            this.textTitle.setTextFormat(this.selectedFormat);
            this.textBody.setTextFormat(this.selectedFormat);
         }
         lbtDrawRow(this,false);
      }
      return result;
   };
   var updateState = P.updateState;
   P.updateState = function(mode)
   {
      var result = updateState.apply(this,arguments);
      if(this._loaded)
      {
         lbtDrawRow(this,mode == 2 || mode == 3);
      }
      return result;
   };
}

lbtPatchRows();
lbtBuild();
// ===== End Sanctuary Lobby Browser =====

// ===== Sanctuary Lobby Game Card =====
// Appended to lobbyBottom's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The group lobby's
// game card (wndLobbyBottom: the chosen game's title, description, options, the 5-4-3-2-1 countdown and the
// Join / Close / Back buttons) in the Main Menu's style: a navy glass frame (the stock curved panel is dropped with
// REMOVED_DEPTHS) with a sparkling header, the title in gold on a navy recess, the description on a glass recess and
// the game options as a glass list with gold values. Join is a gold pill drawn inside the stock button under its own
// label (dark ink; it turns into a navy pill with white text when Lua makes the button beige), and Close and Back are
// gold circles under the invisible stock buttons. Every stock button keeps its handlers, its disabled alpha and the
// join pulse; a disabled button doesn't light up on hover. The countdown stays big and gold, now outlined.

var LBB_W = 746;
var LBB_H = 312;

var lbbFrame;
var lbbJoin;

function lbbBuild()
{
   lbbFrame = mgUnder(_root,"sanctuaryLobbyBottom");
   var bg = lbbFrame.createEmptyMovieClip("bg",1);
   mgFrame(bg,4,4,LBB_W - 8,LBB_H - 8,30,68,4646);
   // The description's recess.
   var p = lbbFrame.createEmptyMovieClip("panels",2);
   mgPanel(p,24,74,428,222,22,30,20);

   lbbBuildTitle();
   lbbBuildOptions();

   mgLight(descriptionText.textbox,MG_SOFT);
   mgLight(loadingHolder.textTop.textbox,MG_GOLD_LIGHT);
   mgLight(loadingHolder.textMiddle.textbox,0xFFFFFF);
   mgLight(countdown.textbox,MG_GOLD_LIGHT);
   countdown.textbox.filters = [new flash.filters.GlowFilter(0x0A1030,1,6,6,6,2),new flash.filters.GlowFilter(MG_GOLD,0.6,24,24,2,2)];

   // Close and Back: gold circles under the invisible stock buttons (they share one spot).
   mgSkin(buttonClose,"close","",{x:-27,y:-27,w:54,h:54});
   mgSkin(buttonBack,"left","",{x:-27,y:-27,w:54,h:54});

   // Join: the pill goes between the button's plates (held invisible) and its labels.
   mgHold(buttonJoin.buttonStatesGreen);
   mgHold(buttonJoin.buttonStatesBeige);
   var names = ["ICON_UP","ICON_ROLL","ICON_UP_TOP","ICON_UP_BOT","ICON_ROLL_TOP","ICON_ROLL_BOT"];
   var i;
   for(i = 0; i < names.length; i++)
   {
      mgLight(buttonJoin.iconStates[names[i]].textbox,MG_INK,false);
   }
   lbbJoin = lbbInnerSkin(buttonJoin,"gold",{x:-60,y:-29,w:120,h:58},-16374);

   lbbFrame.onEnterFrame = lbbTick;
   lbbTick();
}

// The title bar: a navy recess with a gold hairline over the stock bar, under the title (gold).
function lbbBuildTitle()
{
   var t = titleBar.createEmptyMovieClip("sanctuaryBar",-16382);
   mgGradientRect(t,-286,-22,572,44,22,0x13235F,0x0B1745,100,MG_RIM,35,1);
   t.lineStyle();
   t.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:-240,y:0,w:480,h:2,r:0});
   mgRoundRect(t,-240,19,480,2,1);
   t.endFill();
   mgLight(titleBar.textholder.textbox,MG_GOLD_LIGHT);
}

// The options list: a glass recess over the stock teal panel and its lines (both unnamed), under the rows.
function lbbBuildOptions()
{
   var o = optionsPanel.createEmptyMovieClip("sanctuaryList",-16376);
   mgGradientRect(o,-113,-71,228,142,18,0x13235F,0x0B1745,100,MG_RIM,40,1.5);
   mgGloss(o,-105,-69,212,30,12,12);
   var ys = [-30.6,0.3,30.9];
   var i;
   for(i = 0; i < ys.length; i++)
   {
      o.lineStyle();
      o.beginGradientFill("linear",[MG_RIM,MG_RIM,MG_RIM],[0,35,0],[0,128,255],{matrixType:"box",x:-100,y:0,w:204,h:1,r:0});
      mgRoundRect(o,-100,ys[i],204,1,0.5);
      o.endFill();
   }
   for(i = 0; i < optionArray.length; i++)
   {
      mgLight(optionArray[i].leftItem.textbox,0xFFFFFF);
      mgLight(optionArray[i].rightItem.textbox,MG_GOLD_LIGHT);
   }
}

// A pill inside a stock button at depth (between its plates and its labels), so the labels stay on top and the pill
// shows, hides, fades and pulses with the button. Drawn by mg_theme's mgDrawSkin.
function lbbInnerSkin(button, kind, rect, depth)
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

function lbbTick()
{
   // setButtonDisabled clears a button's handlers; keep it from lighting up while it has none.
   buttonClose.enabled = typeof buttonClose.onRelease == "function";
   buttonBack.enabled = typeof buttonBack.onRelease == "function";
   buttonJoin.enabled = typeof buttonJoin.onRelease == "function";

   // Lua turns Join beige (setJoinButtonBeige) for the quieter states: a navy pill with white text then.
   var s = lbbJoin;
   var kind = buttonJoin.buttonStates == buttonJoin.buttonStatesBeige ? "navy" : "gold";
   if(kind != s.kind)
   {
      s.kind = kind;
      var color = kind == "navy" ? 0xFFFFFF : MG_INK;
      var names = ["ICON_UP","ICON_ROLL","ICON_UP_TOP","ICON_UP_BOT","ICON_ROLL_TOP","ICON_ROLL_BOT"];
      var i;
      for(i = 0; i < names.length; i++)
      {
         var t = buttonJoin.iconStates[names[i]].textbox;
         t.mgColor = color;
         t.filters = kind == "navy" ? mgOutline() : [];
      }
      mgDrawSkin(s,s.over);
   }
   var over = buttonJoin._visible && buttonJoin.enabled && buttonJoin.hitTest(_root._xmouse,_root._ymouse,false);
   if(over != s.over)
   {
      s.over = over;
      mgDrawSkin(s,over);
   }
   var target = over ? 106 : 100;
   s.speed = s.speed * 0.62 + (target - s.scale) * 0.28;
   s.scale += s.speed;
   s.body._xscale = s.body._yscale = s.scale;
}

lbbBuild();
// ===== End Sanctuary Lobby Game Card =====

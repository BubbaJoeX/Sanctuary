// ===== Sanctuary Minigame Detail =====
// Appended to MinigameDetail's main timeline (after minigames/mg_theme.as) by build_client_mods.py.
// The game station's detail window (wndMinigameDetail) in the Main Menu's style: the stock orange-vine frame (one
// plain shape, dropped with REMOVED_DEPTHS) becomes the navy glass frame with a gold "Games" title, the screenshot
// sits in a rounded recess, the game's name is gold over an info panel with the difficulty on a glass pill, Play is
// a gold pill on the screenshot's corner and the X the gold circle. The game cards along the bottom are glass with
// the screenshot thumbnail rounded off, lighter with a gold rim on hover and gold with a glow when selected; the
// scroller's arrows are gold circles and a footer counts the games. Membership no longer exists: no game is members
// only (addHorzItem_lua is told so), so nothing is greyed out or locked for it and the Members badges are gone.
// The stock clips keep their roles (hit areas, the scroller's slide, Lua's events).

var MDT_W = 1024;
var MDT_H = 768;
// The screenshot (holder_mc's 440x330 target at 51,111), the info panel and the card tray.
var MDT_IMAGE_X = 51;
var MDT_IMAGE_Y = 111;
var MDT_IMAGE_W = 436;
var MDT_IMAGE_H = 330;
var MDT_INFO_X = 506;
var MDT_TRAY_Y = 466;
var MDT_TRAY_H = 226;

var mdtFrame;
var mdtCards = [];
var mdtCount = "";

// --- frame -----------------------------------------------------------------------------

function mdtBuild()
{
   var f = mdtFrame = mgUnder(_root,"sanctuaryMinigameDetail");
   var bg = f.createEmptyMovieClip("bg",1);
   mgFrame(bg,12,12,MDT_W - 24,MDT_H - 24,30,80,6161);

   var p = f.createEmptyMovieClip("panels",2);
   // The screenshot's recess, the info panel (with a strip under the game's name) and the card tray.
   mgPanel(p,MDT_IMAGE_X - 8,MDT_IMAGE_Y - 8,MDT_IMAGE_W + 16,MDT_IMAGE_H + 16,22,55,30);
   mgPanel(p,MDT_INFO_X,MDT_IMAGE_Y - 8,976 - MDT_INFO_X,MDT_IMAGE_H + 16,22,35,22);
   mgPanel(p,MDT_INFO_X + 8,MDT_IMAGE_Y,976 - MDT_INFO_X - 16,42,16,40,0);
   mgPanel(p,34,MDT_TRAY_Y,MDT_W - 68,MDT_TRAY_H,26,40,25);
   // A gold rule between the difficulty and the description.
   p.lineStyle();
   p.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:MDT_INFO_X + 30,y:0,w:410,h:2,r:0});
   mgRoundRect(p,MDT_INFO_X + 30,210,410,2,1);
   p.endFill();

   var title = mgText(f,"title_txt","Games",0,20,MDT_W,40,28,MG_GOLD_LIGHT);
   title.filters = mgOutline();
   mgText(f,"subtitle_txt","Pick a game to play",0,56,MDT_W,24,14,MG_SOFT);
   f.count_txt = mgText(f,"count_txt","",0,MDT_TRAY_Y + MDT_TRAY_H + 10,MDT_W,24,14,MG_SOFT);

   // The screenshot is rounded off by its recess.
   var mask = mgUnder(_root,"mdtImageMask");
   mask.beginFill(0,100);
   mgRoundRect(mask,MDT_IMAGE_X,MDT_IMAGE_Y,MDT_IMAGE_W,MDT_IMAGE_H,16);
   mask.endFill();
   holder_mc.setMask(mask);

   mdtBuildInfo();
   mdtBuildButtons();
   mdtBuildCards();

   var w = f.createEmptyMovieClip("watch",3);
   w.onEnterFrame = mdtWatch;
}

// The game's name in gold, the difficulty plate as a glass pill, and the text light on navy.
function mdtBuildInfo()
{
   mgLight(title_txt,MG_GOLD_LIGHT);
   mgLight(description_txt,0xDCE6FF,false);
   mgLight(noPlayMessage_txt,MG_GOLD);
   m_Difficulty.background._alpha = 0;
   var d = mgUnder(m_Difficulty,"mdtPill");
   mgGradientRect(d,0,0,271,55,18,0x284A9E,0x162660,100,MG_RIM,45,1.5);
   mgGloss(d,8,2,255,20,9,25);
   mgLight(m_Difficulty.m_Label,MG_SOFT);
   mgLight(m_Difficulty.m_LevelRange,0xFFFFFF);
   // The Members badge on the screenshot never shows.
   mgHold(memberIcon_mc);
}

function mdtBuildButtons()
{
   // Play: a gold pill on the screenshot's bottom right corner. The stock button sat over the recess's edge, so it
   // moves in a little; and up a depth step, since the depth under it is the title's text field.
   playButton_mc._x = 324;
   playButton_mc._y = 380;
   playButton_mc.swapDepths(20 - 16384);
   var play = mgSkin(playButton_mc,"gold","",{x:-7.5,y:5,w:225,h:73});
   // The skin is drawn at the button's 71% scale, so its label is set large to read at about 20px.
   play.label = mgText(play.body,"label_txt","Play!",-112,-20,225,40,28,MG_INK);
   // A disabled Play is dimmed below the skins' hover threshold, so it doesn't light up.
   var enable = playButton_mc.enable;
   playButton_mc.enable = function(is)
   {
      var result = enable.apply(this,arguments);
      if(is == false)
      {
         this._alpha = 30;
      }
      return result;
   };
   if(!playButton_mc.isEnabled())
   {
      playButton_mc._alpha = 30;
   }

   // The X (77x72, drawn at 57%) and the scroller's arrows; the right one is the left one mirrored.
   mgSkin(exitButton_mc,"close","",{x:8.5,y:6,w:60,h:60});
   // The scroller masks its depths up to 94 to the cards' strip, and the arrows sit outside it: their skins go on
   // top (they have no mouse handlers, so the invisible stock arrows under them still take the clicks).
   mgSkin(items_mc.m_left_mc,"left","",{x:-4.5,y:4.5,w:40,h:40}).swapDepths(1000);
   mgSkin(items_mc.m_right_mc,"left","",{x:-4.5,y:4.5,w:40,h:40}).swapDepths(1001);
}

// --- game cards ------------------------------------------------------------------------

function mdtBuildCards()
{
   var i;
   for(i = 0; i < 5; i++)
   {
      mdtCard(items_mc.m_fiveItems1_mc["item" + i]);
      mdtCard(items_mc.m_fiveItems2_mc["item" + i]);
   }
}

// A HorzItem (card -80..80 x -61..59, thumbnail -64..64 x -48..48, name below) as a glass card.
function mdtCard(item)
{
   if(item == undefined || item.mdtBody != undefined)
   {
      return;
   }
   // The stock card plate (unnamed, depth 1) and its green highlight copy go; the highlight still says "selected".
   item.getInstanceAtDepth(-16383)._visible = false;
   item.hilite_mc._alpha = 0;
   item.memberIcon_mc._visible = false;
   item.mdtBody = mgUnder(item,"mdtBody");
   item.mdtState = -1;
   var mask = item.createEmptyMovieClip("mdtThumbMask",item.getNextHighestDepth());
   mask.beginFill(0,100);
   mgRoundRect(mask,-64,-48,128,96,12);
   mask.endFill();
   item.m_icon.setMask(mask);
   mgLight(item.title_txt,0xFFFFFF);
   mdtCards.push(item);
   mdtDrawCard(item,0);
}

// state: 0 resting, 1 hovered, 2 selected.
function mdtDrawCard(item, state)
{
   var g = item.mdtBody;
   g.clear();
   var gold = state == 2;
   var over = state == 1;
   mgGradientRect(g,-80,-61,160,120,18,gold ? 0x4A62B8 : (over ? 0x3F66D6 : 0x284A9E),gold ? 0x1D2C70 : (over ? 0x1D307A : 0x162660),100,gold ? MG_GOLD : (over ? MG_GOLD_LIGHT : MG_RIM),gold || over ? 100 : 45,gold ? 3 : 2);
   if(gold)
   {
      g.lineStyle();
      g.beginGradientFill("radial",[MG_GOLD,MG_GOLD],[45,0],[0,255],{matrixType:"box",x:-90,y:-90,w:180,h:150,r:0});
      mgRoundRect(g,-77,-58,154,114,16);
      g.endFill();
   }
   mgGloss(g,-72,-59,144,30,12,over || gold ? 30 : 20);
   // The thumbnail's recess and rim.
   g.lineStyle(1.5,gold ? MG_GOLD_RIM : 0xFFFFFF,gold ? 90 : 35);
   g.beginFill(MG_NAVY,80);
   mgRoundRect(g,-65,-49,130,98,13);
   g.endFill();
   g.filters = gold ? [new flash.filters.GlowFilter(MG_GOLD,0.85,16,16,2,2)] : (over ? [new flash.filters.GlowFilter(MG_GOLD_LIGHT,0.5,12,12,2,2)] : [new flash.filters.DropShadowFilter(4,90,MG_SHADOW,0.55,10,10,1,2)]);
}

// Every frame: each card's state, and the footer's count.
function mdtWatch()
{
   var x = _root._xmouse;
   var y = _root._ymouse;
   var i;
   for(i = 0; i < mdtCards.length; i++)
   {
      var item = mdtCards[i];
      if(!item._visible || !item._parent._visible)
      {
         continue;
      }
      var state = 0;
      if(item.hilite_mc._visible)
      {
         state = 2;
      }
      else if(item.enabled != false && item._alpha > 60 && item.hitTest(x,y,false))
      {
         state = 1;
      }
      if(state != item.mdtState)
      {
         item.mdtState = state;
         mdtDrawCard(item,state);
      }
   }
   var n = items_mc.m_itemArray.length;
   var start = items_mc.m_savedPageIndex;
   var count = n > 0 ? "Showing " + (start + 1) + " - " + Math.min(start + 5,n) + " of " + n : "";
   if(count != mdtCount)
   {
      mdtCount = count;
      mdtFrame.count_txt.text = count;
   }
}

// --- Lua -------------------------------------------------------------------------------

// No game is members only any more (memberOnly is the 8th argument).
var mdtAddHorzItem = addHorzItem_lua;
addHorzItem_lua = function(count, total, guid, iconId, name, desc, quantity, memberOnly)
{
   arguments[7] = 0;
   return mdtAddHorzItem.apply(_root,arguments);
};

// Cards are filled through setItemData (every page); make sure each is styled and has no Members badge.
var mdtSetItemData = HorzItem.prototype.setItemData;
HorzItem.prototype.setItemData = function(d)
{
   var result = mdtSetItemData.apply(this,arguments);
   mdtCard(this);
   this.memberIcon_mc._visible = false;
   return result;
};

mdtBuild();
// ===== End Sanctuary Minigame Detail =====

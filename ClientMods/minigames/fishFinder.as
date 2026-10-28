// ===== Sanctuary Fish Finder =====
// Appended to fishFinder's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The fishing hole's
// side panel (wndFishFinder: the fish you can catch there, your lures and the rewards) in the Main Menu's style: a
// navy glass frame that grows and shrinks with the list (it follows the stock background's tweened height), the
// title in gold over a sparkling header with a gold rule, the description in soft blue, and the close button as the
// gold circle. Each row is a glass strip: lighter with a pale rim on hover, gold-tinted with a gold rim and gold
// text for the lure in use. The stock background and row highlight go invisible; their clips keep their roles (the
// height tween, hover and click handlers, Lua's FishClick).

var FF_X = -195;
var FF_Y = -12;
var FF_W = 205;
var FF_HEADER_H = 46;
var FF_ROW_W = 158;

var ffFrame;
var ffHeight = -1;

function ffBuild()
{
   var win = mcBigWindow;
   ffFrame = mgUnder(win,"sanctuaryFishFinder");
   ffFrame.bg = ffFrame.createEmptyMovieClip("bg",1);
   ffFrame.rule = ffFrame.createEmptyMovieClip("rule",2);
   ffFrame.onEnterFrame = ffTick;
   mcBackground._alpha = 0;

   mgLight(win.txtTitle,MG_GOLD_LIGHT);
   mgLight(win.txtDesc,MG_SOFT);
   mgSkin(mcExitButton,"close","");

   // The gold rule under the header (it doesn't change with the height).
   var r = ffFrame.rule;
   r.lineStyle();
   r.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:FF_X + 18,y:0,w:FF_W - 36,h:2,r:0});
   mgRoundRect(r,FF_X + 18,FF_Y + FF_HEADER_H + 2,FF_W - 36,2,1);
   r.endFill();
   ffTick.call(ffFrame);
}

// The frame follows the stock background's height (setBackgroundHeight tweens it as rows come and go).
function ffDrawFrame(h)
{
   var g = ffFrame.bg;
   g.clear();
   mgFrame(g,FF_X,FF_Y,FF_W,h,18,FF_HEADER_H,6161);
   // The list's recess below the header.
   if(h > FF_HEADER_H + 24)
   {
      mgPanel(g,FF_X + 8,FF_Y + FF_HEADER_H + 8,FF_W - 16,h - FF_HEADER_H - 16,12,30,18);
   }
}

function ffTick()
{
   var h = Math.round(mcBackground._height);
   if(h != ffHeight && h > 0)
   {
      ffHeight = h;
      ffDrawFrame(h);
   }
   var name;
   for(name in body)
   {
      var item = body[name];
      if(typeof item == "movieclip" && item.ffRow != undefined)
      {
         ffRowTick(item);
      }
   }
}

// --- rows --------------------------------------------------------------------------------

function ffStyleRows()
{
   var name;
   for(name in body)
   {
      var item = body[name];
      if(typeof item == "movieclip" && item.myType != undefined && item.ffRow == undefined)
      {
         item.ffRow = mgUnder(item,"ffRow");
         item.ffState = -1;
         // The stock highlight's alpha still says when the row is hovered; it's just not drawn.
         item.mcHigh._visible = false;
         item.txt.filters = mgOutline();
         ffRowTick(item);
      }
   }
}

// 0 resting, 1 hovered, 2 the lure in use.
function ffRowTick(item)
{
   var state = 0;
   if(_root.selectedFish[item.myType] == item._name)
   {
      state = 2;
   }
   else if(item.mcHigh._alpha > 0)
   {
      state = 1;
   }
   if(state == item.ffState)
   {
      return;
   }
   item.ffState = state;
   var g = item.ffRow;
   g.clear();
   if(state == 2)
   {
      mgGradientRect(g,0,1,FF_ROW_W,15,7,0x6B5A2A,0x3A3218,95,MG_GOLD,100,1.5);
      g.lineStyle();
      g.beginFill(MG_GOLD,90);
      mgCircle(g,FF_ROW_W - 9,8.5,3);
      g.endFill();
   }
   else if(state == 1)
   {
      mgGradientRect(g,0,1,FF_ROW_W,15,7,0x3F66D6,0x1D307A,95,MG_GOLD_LIGHT,80,1);
   }
   else
   {
      mgGradientRect(g,0,1,FF_ROW_W,15,7,0x284A9E,0x162660,70,MG_RIM,25,1);
   }
   mgGloss(g,4,2,FF_ROW_W - 8,6,3,state == 0 ? 12 : 22);
   item.txt.textColor = state == 2 ? MG_GOLD_LIGHT : 0xFFFFFF;
}

// Lua adds every row through addLineItem; clearItems makes a new list.
var ffAddLineItem = addLineItem;
addLineItem = function(type, myName, myID, desc, quantity, iconID, myGuid, fishCatchable)
{
   var result = ffAddLineItem.apply(_root,arguments);
   ffStyleRows();
   return result;
};

ffBuild();
// ===== End Sanctuary Fish Finder =====

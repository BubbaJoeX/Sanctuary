// ===== Sanctuary coin store only =====
// Appended to marketplace's main timeline by build_client_mods.py.
// Station Cash and membership no longer exist, so the Marketplace window only shows its coin store
// page. Every other page (portal, Station Cash store, membership, claim, add funds) goes to the
// coin store instead.

// MarketplaceEnum.COIN_STORE in the client's marketplace.lua.
var COIN_STORE_MODE = "7";

var sanctuaryOriginalSetMainMode = setMainMode;
var sanctuaryRedirecting = false;

setMainMode = function(s)
{
   if(s == "coinstore")
   {
      sanctuaryRedirecting = false;
      sanctuaryOriginalSetMainMode("coinstore");
      return;
   }
   // Ask Lua for the coin store, so it also sends the coin store's categories and items.
   if(!sanctuaryRedirecting)
   {
      sanctuaryRedirecting = true;
      fscommand("FR_event","setMode(" + COIN_STORE_MODE + ")");
      return;
   }
   // Lua didn't switch pages; show the coin store page anyway.
   sanctuaryRedirecting = false;
   sanctuaryOriginalSetMainMode("coinstore");
};

// Buttons inside the window (add funds, become a member, claim, view all) ask Lua for a page here.
base.FSCommands.fsSetMode = function(mode)
{
   fscommand("FR_event","setMode(" + COIN_STORE_MODE + ")");
};

// Lua's Station Cash and membership purchase flows.
setModeAddSC = function()
{
   setMainMode("coinstore");
};
setModeMembershipSC = function()
{
   setMainMode("coinstore");
};
settoMembershipPreview_lua = function(sku)
{
   setMainMode("coinstore");
};

// The Station Cash wallet sits on top of every page.
_root.wallet._visible = false;

// The coin store header has its own Station Cash wallet (top left). Chatty pops up there instead.
var sanctuaryOriginalGetWalletInfo = WalletInfo.prototype._handleGetWalletInfo;
WalletInfo.prototype._handleGetWalletInfo = function()
{
   sanctuaryOriginalGetWalletInfo.apply(this,arguments);
   this._visible = false;
};
var sanctuaryOriginalSetWalletInfo = WalletInfo.prototype._handleSetWalletInfo;
WalletInfo.prototype._handleSetWalletInfo = function()
{
   sanctuaryOriginalSetWalletInfo.apply(this,arguments);
   this._visible = false;
};
_root.mc_coinstore.mcWalletInfo._visible = false;

// The Chatty toy's 128px icon (Resources/Images/Images.txt; ii:// takes an image id, not an image set).
var CHATTY_IMAGE_ID = 46502;
var CHATTY_IMAGE_SIZE = 128;
var CHATTY_HEIGHT = 110;

// Loads the Chatty icon into a new clip of parent, sized height and anchored at its bottom centre.
// The image loader fits an image to its holder's original size, so the holder starts as an invisible
// square of the icon's size, inside a frame that does the scaling (the loader resets the holder itself).
function sanctuaryMakeChatty(parent, name, height)
{
   var chatty = parent.createEmptyMovieClip(name,parent.getNextHighestDepth());
   var frame = chatty.createEmptyMovieClip("frame",1);
   frame._xscale = frame._yscale = height / CHATTY_IMAGE_SIZE * 100;
   frame._x = - height / 2;
   frame._y = - height;
   var holder = frame.createEmptyMovieClip("holder",1);
   holder.beginFill(0,0);
   holder.moveTo(0,0);
   holder.lineTo(CHATTY_IMAGE_SIZE,0);
   holder.lineTo(CHATTY_IMAGE_SIZE,CHATTY_IMAGE_SIZE);
   holder.lineTo(0,CHATTY_IMAGE_SIZE);
   holder.endFill();
   _root.frSetIconByIndex(holder,CHATTY_IMAGE_ID);
   return chatty;
}

function sanctuaryPopChatty()
{
   var store = _root.mc_coinstore;
   var chatty = store.sanctuaryChatty;
   if(chatty == undefined)
   {
      // Stands on the header's bottom edge where the wallet was, so he pokes up out of the window.
      var spot = store.mcWalletInfo.getBounds(store);
      chatty = sanctuaryMakeChatty(store,"sanctuaryChatty",CHATTY_HEIGHT);
      chatty._x = (spot.xMin + spot.xMax) / 2;
      chatty._y = spot.yMax + 12;
      chatty.onRollOver = function()
      {
         gs.TweenLite.to(this,1,{_rotation:-8,ease:mx.transitions.easing.Elastic.easeOut});
      };
      chatty.onRollOut = function()
      {
         gs.TweenLite.to(this,1,{_rotation:0,ease:mx.transitions.easing.Elastic.easeOut});
      };
   }
   chatty._xscale = chatty._yscale = 10;
   chatty._rotation = 0;
   gs.TweenLite.to(chatty,0.9,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Elastic.easeOut});
}

// The client's item sorts (Resources/ItemSortTypes.txt) in display order, with their English names
// (the combo box shows its text as is, so locale ids don't resolve). The coin store's Lua sends the
// Marketplace's sort options, which are empty without the Station Cash store.
var SANCTUARY_SORT_OPTIONS = [
   {Id:1, Name:"Default"},
   {Id:2, Name:"Alphabetical A - Z"},
   {Id:4, Name:"Alphabetical Z - A"},
   {Id:3, Name:"Price Low to High"},
   {Id:5, Name:"Price High to Low"}
];

// The coin store's Sort By menu read the Station Cash store's sort options ("undefined"); it reads its
// own, falling back to the client's item sorts.
coinstore.CoinStoreManager.prototype.onSetSortOptions = function()
{
   var model = base.CoinStoreModel.getInstance();
   var options = model.dsSortOptions.data;
   if(options == undefined || options.length == 0 || options[0].Name == undefined)
   {
      options = SANCTUARY_SORT_OPTIONS;
   }
   var sortId;
   if(model.dsSortState.data.length > 0)
   {
      sortId = model.dsSortState.data[0].SortId;
   }
   // The combo box's own setup (label field, change listener) runs on a load callback that the coin
   // store's copy can miss; without it every option reads "undefined".
   var combo = this.mcSort.cbSorts;
   combo.labelField = "Name";
   if(!this.mcSort.sanctuaryListening)
   {
      this.mcSort.sanctuaryListening = true;
      var listener = new Object();
      listener.store = this;
      listener.onValueChange = function(data)
      {
         sanctuarySetSort(this.store,Number(data.Id));
      };
      combo.addListener(listener);
   }
   this.mcSort.setData(options,sortId);
};

// The client's coin store sort (CoinStoreScreen.SortItems) doesn't change the order the list gets its
// rows in, so the coin store sorts here: when a sort other than Default is picked, it reads every row of
// the current view from Lua (a chunk a frame, so a 17,000-row view doesn't freeze the client), sorts them
// by name or price, and serves the list's pages from that copy. A new category, search, filter or
// buy/sell view sorts again once Lua has sent its first page.
var SORT_DEFAULT = 1;
var SORT_NAME_ASCENDING = 2;
var SORT_PRICE_ASCENDING = 3;
var SORT_NAME_DESCENDING = 4;
var SORT_PRICE_DESCENDING = 5;
var SORT_CHUNK_ROWS = 400;
var ROW_DELIM = "~~";
var PROPERTY_DELIM = "|";
var KEY_VALUE_DELIM = "%%";

var sanctuarySortId = SORT_DEFAULT;
var sanctuarySortedRows = null;
var sanctuarySortedTrailingDelim = false;
var sanctuaryCapturingRows = false;
var sanctuaryCaptureCancelled = false;
var sanctuaryCaptureEntries = null;
var sanctuaryCaptureOffset = 0;
var sanctuaryCaptureTotal = 0;
var sanctuaryResortPending = false;

var sanctuaryOriginalRequestItemData = base.FSCommands.fsRequestCoinStoreItemData;
var sanctuaryOriginalSetCoinStoreData = setCoinStoreData;

// One value of a row ("key%%value|key%%value|..."), found without splitting the whole row.
function sanctuaryRowValue(row, key)
{
   var marker = key + KEY_VALUE_DELIM;
   var start;
   if(row.indexOf(marker) == 0)
   {
      start = marker.length;
   }
   else
   {
      start = row.indexOf(PROPERTY_DELIM + marker);
      if(start < 0)
      {
         return undefined;
      }
      start += PROPERTY_DELIM.length + marker.length;
   }
   var end = row.indexOf(PROPERTY_DELIM,start);
   return end < 0 ? row.substr(start) : row.substring(start,end);
}

function sanctuaryRowPrice(row)
{
   // Everyone pays the member price; the sell view's rows only have a Price.
   var price = Number(sanctuaryRowValue(row,base.CoinStoreDataSource.KEY_MEMBER_PURCHASE_PRICE));
   if(isNaN(price))
   {
      price = Number(sanctuaryRowValue(row,base.CoinStoreDataSource.KEY_COIN_PURCHASE_PRICE));
   }
   if(isNaN(price))
   {
      price = Number(sanctuaryRowValue(row,base.CoinStoreDataSource.KEY_SELL_PRICE));
   }
   return isNaN(price) ? 0 : price;
}

function sanctuaryNextFrame(callback)
{
   var waiter = _root.createEmptyMovieClip("sanctuaryWaiter" + _root.getNextHighestDepth(),_root.getNextHighestDepth());
   waiter.onEnterFrame = function()
   {
      this.removeMovieClip();
      callback();
   };
}

function sanctuaryRequestAllRows(store)
{
   var total = Number(store.totalItemCount);
   if(isNaN(total) || total <= 0)
   {
      return undefined;
   }
   sanctuarySortedRows = null;
   sanctuaryCapturingRows = true;
   sanctuaryCaptureCancelled = false;
   sanctuaryCaptureEntries = [];
   sanctuaryCaptureOffset = 0;
   sanctuaryCaptureTotal = total;
   sanctuaryOriginalRequestItemData(0,Math.min(SORT_CHUNK_ROWS,total));
}

function sanctuaryCaptureChunk(message)
{
   sanctuarySortedTrailingDelim = message.substr(message.length - ROW_DELIM.length) == ROW_DELIM;
   var rows = message.split(ROW_DELIM);
   var added = 0;
   var i = 0;
   while(i < rows.length)
   {
      var row = rows[i];
      if(row.length > 0)
      {
         var name = sanctuaryRowValue(row,base.CoinStoreDataSource.KEY_NAME);
         sanctuaryCaptureEntries.push({row:row, name:name == undefined ? "" : name.toLowerCase(), price:sanctuaryRowPrice(row)});
         added = added + 1;
      }
      i = i + 1;
   }
   sanctuaryCaptureOffset += SORT_CHUNK_ROWS;
   if(added > 0 && sanctuaryCaptureOffset < sanctuaryCaptureTotal)
   {
      sanctuaryNextFrame(function()
      {
         if(sanctuaryCapturingRows)
         {
            sanctuaryOriginalRequestItemData(sanctuaryCaptureOffset,Math.min(SORT_CHUNK_ROWS,sanctuaryCaptureTotal - sanctuaryCaptureOffset));
         }
      });
      return undefined;
   }
   sanctuaryCapturingRows = false;
   var entries = sanctuaryCaptureEntries;
   sanctuaryCaptureEntries = null;
   if(sanctuarySortId == SORT_NAME_ASCENDING)
   {
      entries.sortOn("name");
   }
   else if(sanctuarySortId == SORT_NAME_DESCENDING)
   {
      entries.sortOn("name",Array.DESCENDING);
   }
   else if(sanctuarySortId == SORT_PRICE_ASCENDING)
   {
      entries.sortOn("price",Array.NUMERIC);
   }
   else
   {
      entries.sortOn("price",Array.NUMERIC | Array.DESCENDING);
   }
   sanctuarySortedRows = [];
   i = 0;
   while(i < entries.length)
   {
      sanctuarySortedRows.push(entries[i].row);
      i = i + 1;
   }
   // Back to the top: the list asks for its first page, served from the sorted rows.
   _root.mc_coinstore.refreshItemDisplay(-1);
}

function sanctuarySetSort(store, sortId)
{
   sanctuarySortId = sortId;
   sanctuarySortedRows = null;
   sanctuaryResortPending = false;
   if(sanctuaryCapturingRows)
   {
      sanctuaryCaptureCancelled = true;
   }
   if(sortId == SORT_DEFAULT)
   {
      store.refreshItemDisplay(-1);
      return undefined;
   }
   if(!sanctuaryCapturingRows)
   {
      sanctuaryRequestAllRows(store);
   }
}

setCoinStoreData = function(message, rowCount)
{
   var store = _root.mc_coinstore;
   if(sanctuaryCapturingRows)
   {
      if(sanctuaryCaptureCancelled)
      {
         // A capture for a view or sort that's since changed: drop it, then start over if still sorting.
         sanctuaryCapturingRows = false;
         sanctuaryCaptureCancelled = false;
         if(sanctuarySortId != SORT_DEFAULT && !sanctuaryResortPending)
         {
            sanctuaryRequestAllRows(store);
         }
         return undefined;
      }
      sanctuaryCaptureChunk(message);
      return undefined;
   }
   sanctuaryOriginalSetCoinStoreData(message,rowCount);
   if(sanctuaryResortPending)
   {
      sanctuaryResortPending = false;
      sanctuaryRequestAllRows(store);
   }
};

base.FSCommands.fsRequestCoinStoreItemData = function(offset, count)
{
   if(sanctuaryCapturingRows && !sanctuaryCaptureCancelled)
   {
      // The list refreshes from the top once the sort's rows are in.
      return undefined;
   }
   if(sanctuarySortedRows == null)
   {
      sanctuaryOriginalRequestItemData(offset,count);
      return undefined;
   }
   var page = sanctuarySortedRows.slice(Number(offset),Number(offset) + Number(count));
   var message = page.join(ROW_DELIM) + (sanctuarySortedTrailingDelim && page.length > 0 ? ROW_DELIM : "");
   var total = sanctuarySortedRows.length;
   // Answer on the next frame, as Lua would, rather than inside the list's own request.
   var waiter = _root.createEmptyMovieClip("sanctuarySortPage",_root.getNextHighestDepth());
   waiter.onEnterFrame = function()
   {
      _root.mc_coinstore.setData(message,total);
      this.removeMovieClip();
   };
};

// Anything that changes which rows are in view drops the sorted copy; Lua's first page then sorts again.
function sanctuaryInvalidateSort(original)
{
   return function()
   {
      sanctuarySortedRows = null;
      // A capture in flight still gets its reply, which is dropped.
      sanctuaryCaptureCancelled = sanctuaryCapturingRows;
      sanctuaryResortPending = sanctuarySortId != SORT_DEFAULT;
      original.apply(base.FSCommands,arguments);
   };
}
base.FSCommands.fsSetCoinStoreCategory = sanctuaryInvalidateSort(base.FSCommands.fsSetCoinStoreCategory);
base.FSCommands.fsSetSearchForCoinStore = sanctuaryInvalidateSort(base.FSCommands.fsSetSearchForCoinStore);
base.FSCommands.fsApplyFiltersForCoinStore = sanctuaryInvalidateSort(base.FSCommands.fsApplyFiltersForCoinStore);
base.FSCommands.fsClearFiltersForCoinStore = sanctuaryInvalidateSort(base.FSCommands.fsClearFiltersForCoinStore);
base.FSCommands.fsSetCoinStoreMode = sanctuaryInvalidateSort(base.FSCommands.fsSetCoinStoreMode);

var sanctuaryOriginalCoinStoreEnter = mainstates.StateCoinStore.prototype.enterState;
mainstates.StateCoinStore.prototype.enterState = function()
{
   sanctuaryOriginalCoinStoreEnter.apply(this,arguments);
   var store = this.manager.mc_coinstore;
   store.mcWalletInfo._visible = false;
   // (Not getSortStateForCoinStore: the client's Lua has no CoinStoreHandler:SetSortState.)
   // The menu opens on Default, so the list does too.
   sanctuarySortId = SORT_DEFAULT;
   sanctuarySortedRows = null;
   sanctuaryCaptureCancelled = sanctuaryCapturingRows;
   sanctuaryResortPending = false;
   store.onSetSortOptions();
   sanctuaryPopChatty();
   sanctuaryModernLayout(store);
   store.itemDisplay.refresh();
};

// Everyone pays what used to be the member price, so the side panel shows that one price: no regular
// price, member savings or members-only badge (the panel's own non-member layout, used for China).
function sanctuaryShowOnePrice(panel)
{
   var widget = panel.mcActionWidget;
   widget.notMemberPrice._visible = false;
   widget.savings._visible = false;
   widget.MOText._visible = false;
   widget.memberPrice._alpha = 100;
   _root.frSetTextPreserveWidthV3(widget.memberPrice.label,"S##marketplace_label_price2",true);
   panel.mcMemberGraphic._visible = false;
   panel.btnMembersOnly._visible = false;
   panel.UpgradeCantBuy._visible = false;
   widget._visible = true;
   sanctuaryModernBuyArea(panel);
}

// With nothing selected the panel works out a price of NaN; it shows dashes like the stock empty panel,
// and no preview button (the magnifier) over the empty item area.
var sanctuaryOriginalSetSelectedUI = coinstore.CoinStoreSidePanel.prototype._setSelectedUI;
coinstore.CoinStoreSidePanel.prototype._setSelectedUI = function()
{
   sanctuaryOriginalSetSelectedUI.apply(this,arguments);
   var nothingSelected = isNaN(Number(this.totalCost));
   if(nothingSelected)
   {
      this.mcActionWidget.memberPrice.value.text = "---";
      this.mcActionWidget.mcItemPriceDisplay.txtPrice.text = "---";
   }
   // The preview state shows its close button instead; leave that alone.
   if(!this.btnClosePreview._visible)
   {
      this.btnShowPreview._visible = !nothingSelected;
   }
};

// No member savings to show; the box stays hidden whatever the panel does with it.
marketplace.MemberSavings.prototype.setValue = function()
{
   this._visible = false;
};
marketplace.MemberSavings.prototype.setIsMember = function()
{
   this._visible = false;
};

// The panel's buy area as one flat card: price, then quantity and total side by side, then a full width
// Buy (or Sell) button; and the coin wallet as a flat pill. Positions are in the action widget's own
// space (248 x 200) and the wallet's (216 x 88).
var COLOR_CARD = 0x1d2b44;
var COLOR_CARD_LINE = 0x2c4670;
var COLOR_TILE = 0x0f1726;
var COLOR_WALLET_LINE = 0xc9a14a;

function sanctuaryModernBuyArea(panel)
{
   var widget = panel.mcActionWidget;
   widget.savings._visible = false;
   if(widget.sanctuaryModern)
   {
      return undefined;
   }
   widget.sanctuaryModern = true;

   // The stock total box (timeline depth 20) and divider (depth 39) are shapes, which can't be hidden, so
   // card-coloured patches just above them (free depths 22 and 40) cover them.
   var card = widget.createEmptyMovieClip("sanctuaryCard",-16384);
   sanctuaryRoundRect(card,-6,24,260,186,12,COLOR_CARD,100,COLOR_CARD_LINE);
   sanctuaryRoundRect(card,118,70,118,40,8,COLOR_TILE,100);
   var boxCover = widget.createEmptyMovieClip("sanctuaryBoxCover",22 - 16384);
   sanctuaryRoundRect(boxCover,134,114,108,68,0,COLOR_CARD,100);
   var lineCover = widget.createEmptyMovieClip("sanctuaryLineCover",40 - 16384);
   sanctuaryRoundRect(lineCover,134,112,108,14,0,COLOR_CARD,100);

   // Quantity ("X" label at depth 41, then the stepper) on the left, the total on the tile to its right.
   var times = widget.getInstanceAtDepth(41 - 16384);
   times._x = 14;
   times._y = 74;
   widget.mcItemIncrementer._x = 42;
   widget.mcItemIncrementer._y = 70;
   widget.mcItemPriceDisplay._x = 120;
   widget.mcItemPriceDisplay._y = 72;

   // Buy / Sell across the card.
   var buttons = [widget.mcBuyButton, widget.mcSellButton];
   var i = 0;
   while(i < buttons.length)
   {
      // The big Buy / Sell art is 200 x 70 at full size (the stock layout shows it at 48%).
      buttons[i]._xscale = buttons[i]._yscale = 100;
      buttons[i]._x = 24;
      buttons[i]._y = 124;
      i = i + 1;
   }

   // Coin wallet: its ornate frame (depth 1) becomes a flat pill with a thin gold edge.
   var wallet = panel.getInstanceAtDepth(108 - 16384);
   if(wallet != undefined)
   {
      wallet.getInstanceAtDepth(1 - 16384)._visible = false;
      var pill = wallet.createEmptyMovieClip("sanctuaryPill",-16384);
      sanctuaryRoundRect(pill,0,8,216,70,35,COLOR_TILE,100,COLOR_WALLET_LINE);
   }
}

var sanctuaryOriginalSetMembershipState = coinstore.CoinStoreSidePanel.prototype._setMembershipState;
coinstore.CoinStoreSidePanel.prototype._setMembershipState = function()
{
   sanctuaryOriginalSetMembershipState.apply(this,arguments);
   sanctuaryShowOnePrice(this);
};

var sanctuaryOriginalCoinStoreSetMode = coinstore.CoinStoreSidePanel.prototype._handleCoinStoreSetMode;
coinstore.CoinStoreSidePanel.prototype._handleCoinStoreSetMode = function(s)
{
   sanctuaryOriginalCoinStoreSetMode.apply(this,arguments);
   if(s == base.CoinStoreModel.MODE_BUY)
   {
      sanctuaryShowOnePrice(this);
   }
};

// ----- Wider, modern coin store -----
// The window is 1300 wide (install_client_mods.py) instead of 1024. The ornate background goes; flat dark
// panels drawn here hold a header (Chatty, coin, title, filters, Buy/Sell), the categories on the left,
// a three-column item grid in the middle and the item panel on the right. Positions are in the coin
// store page's own space; the stock layout is 1024 x 768.
var STORE_WIDTH = 1300;
var COLOR_FRAME = 0x101826;
var COLOR_FRAME_LINE = 0x2f4f7c;
var COLOR_HEADER = 0x182439;
var COLOR_PANEL = 0x172235;
var COLOR_PANEL_LINE = 0x24395a;
var COLOR_TITLE = 0xffffff;
var COLOR_SUBTITLE = 0x9db4d4;
var COIN_IMAGE_ID = 22665;
var HEADER_BOTTOM = 125;

var GRID_X = 298;
var GRID_COLUMNS = 3;
var GRID_COLUMN_WIDTH = 222;
var ITEM_PANEL_X = 1150;

// Keep the stage's top left where the movie put it, now that the window is wider than the movie.
Stage.align = "TL";

function sanctuaryRoundRect(clip, x, y, w, h, r, fill, alpha, line)
{
   if(line != undefined)
   {
      clip.lineStyle(2,line,100,true);
   }
   else
   {
      // (lineStyle(undefined) still draws a black hairline in the client's player.)
      clip.lineStyle(0,fill,0);
   }
   clip.beginFill(fill,alpha);
   clip.moveTo(x + r,y);
   clip.lineTo(x + w - r,y);
   clip.curveTo(x + w,y,x + w,y + r);
   clip.lineTo(x + w,y + h - r);
   clip.curveTo(x + w,y + h,x + w - r,y + h);
   clip.lineTo(x + r,y + h);
   clip.curveTo(x,y + h,x,y + h - r);
   clip.lineTo(x,y + r);
   clip.curveTo(x,y,x + r,y);
   clip.endFill();
}

function sanctuaryText(parent, name, x, y, w, size, color, text)
{
   parent.createTextField(name,parent.getNextHighestDepth(),x,y,w,size + 14);
   var field = parent[name];
   var format = new TextFormat();
   format.font = "$MainFont";
   format.size = size;
   format.color = color;
   field.embedFonts = true;
   field.selectable = false;
   field.setNewTextFormat(format);
   field.text = text;
   return field;
}

// The stock clips this layout replaces: the ornate background, the Station Cash wallet and the stock
// "Earn coins" line (an unnamed text field, so it's found by its text; the header has its own).
function sanctuaryHideStockArt(store)
{
   store.mcBackground._visible = false;
   store.mcWalletInfo._visible = false;
   // The item panel's light blue frame (an unnamed clip at its timeline depth 1); the right panel
   // drawn behind it replaces it.
   store.mcItemPanel.getInstanceAtDepth(1 - 16384)._visible = false;
   // The stock header's text fields ("Coin Shop" under the old coin, "Earn coins by playing minigames!");
   // the new header has its own.
   for(var name in store)
   {
      var field = store[name];
      if(field instanceof TextField && field._y < HEADER_BOTTOM)
      {
         field._visible = false;
      }
   }
}

// Coin amounts: commas up to 999,999, then 1.23M / 4.5B so the wallet box always fits.
function sanctuaryFormatCoins(n)
{
   n = Math.floor(Number(n));
   if(isNaN(n))
   {
      return "0";
   }
   var units = [{size:1000000000, suffix:"B"}, {size:1000000, suffix:"M"}];
   var i = 0;
   while(i < units.length)
   {
      if(n >= units[i].size)
      {
         var value = n / units[i].size;
         var decimals = value < 10 ? 2 : (value < 100 ? 1 : 0);
         var factor = Math.pow(10,decimals);
         return String(Math.floor(value * factor) / factor) + units[i].suffix;
      }
      i = i + 1;
   }
   var digits = String(n);
   var out = "";
   var count = 0;
   var j = digits.length - 1;
   while(j >= 0)
   {
      out = digits.charAt(j) + out;
      count = count + 1;
      if(count % 3 == 0 && j > 0)
      {
         out = "," + out;
      }
      j = j - 1;
   }
   return out;
}
coinstore.CoinWallet.prototype.formatNumber = sanctuaryFormatCoins;

function sanctuaryModernLayout(store)
{
   if(store.sanctuaryModern)
   {
      sanctuaryHideStockArt(store);
      return undefined;
   }
   store.sanctuaryModern = true;
   sanctuaryHideStockArt(store);

   // Background panels, under everything else on the page.
   var bg = store.createEmptyMovieClip("sanctuaryBackground",store.mcBackground.getDepth() - 1);
   sanctuaryRoundRect(bg,8,22,STORE_WIDTH - 16,734,18,COLOR_FRAME,97,COLOR_FRAME_LINE);
   sanctuaryRoundRect(bg,10,24,STORE_WIDTH - 20,100,16,COLOR_HEADER,100);
   sanctuaryRoundRect(bg,18,134,260,612,12,COLOR_PANEL,100,COLOR_PANEL_LINE);
   sanctuaryRoundRect(bg,288,134,712,612,12,COLOR_PANEL,100,COLOR_PANEL_LINE);
   sanctuaryRoundRect(bg,1006,134,STORE_WIDTH - 1020,612,12,COLOR_PANEL,100,COLOR_PANEL_LINE);

   // Header: coin, title and subtitle (Chatty stands at its left).
   var header = store.createEmptyMovieClip("sanctuaryHeader",store.getNextHighestDepth());
   var coin = header.createEmptyMovieClip("coin",1);
   coin._x = 116;
   coin._y = 40;
   var coinHolder = coin.createEmptyMovieClip("holder",1);
   coinHolder.beginFill(0,0);
   coinHolder.moveTo(0,0);
   coinHolder.lineTo(64,0);
   coinHolder.lineTo(64,64);
   coinHolder.lineTo(0,64);
   coinHolder.endFill();
   _root.frSetIconByIndex(coinHolder,COIN_IMAGE_ID);
   sanctuaryText(header,"title",188,34,420,34,COLOR_TITLE,"Coin Shop");
   sanctuaryText(header,"subtitle",190,80,420,15,COLOR_SUBTITLE,"Earn coins by playing minigames!");

   // Header controls: filter status, Buy and Sell on the right.
   store.mcFilterSettings._x += 60;
   store.mcFilterSettings._y += 14;
   store.btnBuyMode._x = 1000;
   store.btnBuyMode._y = 47;
   store.btnSellMode._x = 1140;
   store.btnSellMode._y = 47;

   // Middle: sort bar and view buttons along the top, a three-column grid below.
   var dx = GRID_X + 2 - store.mcSort._x;
   store.mcSort._x += dx;
   store.mcSort._y = 146;
   var viewDx = 985 - 732;
   store.mcViewModeHolder._x += viewDx;
   store.listView_btn._x += viewDx;
   store.buttonsExpandedView_btn._x += viewDx;
   store.mcViewModeHolder._y += -8;
   store.listView_btn._y += -8;
   store.buttonsExpandedView_btn._y += -8;

   // The grid's x comes from these when its scrollbar shows or hides.
   marketplace.MarketplaceManager.ITEMDISPLAY_INIT_X = GRID_X;
   marketplace.MarketplaceManager.ITEMDISPLAY_OFFSET_X = GRID_X + 14;
   store.itemDisplay._x = GRID_X;
   store.itemDisplay._y = 196;
   var modes = store.itemDisplay.itemDisplay.displayModes;
   if(modes == undefined)
   {
      modes = store.itemDisplay.displayModes;
   }
   var i = 0;
   while(i < modes.length)
   {
      if(modes[i].columns > 1)
      {
         modes[i].columns = GRID_COLUMNS;
         modes[i].columnWidth = GRID_COLUMN_WIDTH;
      }
      i = i + 1;
   }
   store.itemDisplay.scrollbar._x = GRID_COLUMNS * GRID_COLUMN_WIDTH + 4;

   // Right: the item panel and the filters panel that slides over it.
   var panelDx = ITEM_PANEL_X - store.mcItemPanel._x;
   store.mcItemPanel._x = ITEM_PANEL_X;
   store.mcSidePanelStates._x += panelDx;
   store.STATE_ITEM_PREVIEW.Xi = ITEM_PANEL_X;

   // Chatty stands at the header's left edge.
   var chatty = store.sanctuaryChatty;
   if(chatty != undefined)
   {
      chatty._x = 60;
      chatty._y = 124;
   }
}

// Showing the page again (after the full-screen preview) shows every clip, the stock art included.
var sanctuaryOriginalShowAll = coinstore.CoinStoreManager.prototype.showAll;
coinstore.CoinStoreManager.prototype.showAll = function(b)
{
   sanctuaryOriginalShowAll.apply(this,arguments);
   if(b && this.sanctuaryModern)
   {
      sanctuaryHideStockArt(this);
   }
};
// ===== End Sanctuary coin store only =====

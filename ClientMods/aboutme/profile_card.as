// ===== Sanctuary Profile Card =====
// Appended to aboutme's main timeline by build_client_mods.py, after no_station_cash.as.
// Redraws the About Me card in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a header with
// the player's portrait, name, title picker and birthday/age/played chips, then a row of stat tiles with big
// numbers (coins, levels, collections, quests, VIP rank) and, when Lua sends any, a Highlights grid of the
// stock stat lines. The stock sentences ("I have 120 coins") come from the string table, so the tiles show
// the number in them under a plain label. Tile icons are client textures embedded as sanctuaryProfile*
// symbols (TEXTURE_SYMBOLS); the stock icons are root shapes, dropped at build time (REMOVED_DEPTHS).

var PC_X = 8;
var PC_Y = 104;
var PC_W = 730;
var PC_TILE_Y = 258;
var PC_TILE_W = 134;
var PC_TILE_H = 130;
var PC_STATS_Y = 404;
var PC_GOLD = 0xFFC531;
var PC_RIM = 0x9DB8FF;
var PC_SOFT = 0xC9D6FF;

// [label, icon symbol ("" = the stock VIP star)]
var PC_TILES = [
   ["Coins","sanctuaryProfileCoin"],
   ["Levels Gained","sanctuaryProfileTrophy"],
   ["Collections","sanctuaryProfileBook"],
   ["Quests","sanctuaryProfileJournal"],
   ["VIP Rank",""]
];

var pc;
var pcTiles = [];
var pcGuid;

// --- drawing helpers -------------------------------------------------------------------

function pcRoundRect(mc, x, y, w, h, r)
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

function pcGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   pcRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function pcText(mc, name, txt, x, y, w, h, size, color, align)
{
   mc.createTextField(name,mc.getNextHighestDepth(),x,y,w,h);
   var t = mc[name];
   t.embedFonts = true;
   t.selectable = false;
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = align == undefined ? "center" : align;
   t.setNewTextFormat(f);
   t.text = txt;
   t.baseSize = size;
   return t;
}

// Sets a field's text, shrinking its font until it fits on one line.
function pcSetFitted(t, txt)
{
   var f = t.getTextFormat();
   f.size = t.baseSize;
   t.setNewTextFormat(f);
   t.text = txt;
   while(t.textWidth > t._width - 6 && f.size > 10)
   {
      f.size -= 1;
      t.setTextFormat(f);
   }
}

function pcOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

// --- data ------------------------------------------------------------------------------

// The first number in a sentence ("I've gained 342 levels" -> "342"), with thousands separators.
function pcNumber(s)
{
   s = String(s);
   var digits = "";
   var i;
   for(i = 0; i < s.length; i++)
   {
      var c = s.charAt(i);
      if(c >= "0" && c <= "9")
      {
         digits += c;
      }
      else if(digits.length > 0 && c != "," && c != ".")
      {
         break;
      }
   }
   if(digits.length == 0)
   {
      return "0";
   }
   var out = "";
   while(digits.length > 3)
   {
      out = "," + digits.substr(digits.length - 3) + out;
      digits = digits.substr(0,digits.length - 3);
   }
   return digits + out;
}

// "Played Time: 6 hours" -> "6 hours"; the chip has its own label.
function pcValue(s)
{
   s = String(s);
   var i = s.indexOf(": ");
   if(i > 0 && i < 24)
   {
      s = s.substr(i + 2);
   }
   while(s.charAt(0) == " ")
   {
      s = s.substr(1);
   }
   return s.length > 0 && s != "undefined" && s != "0" ? s : "-";
}

function pcSetTile(i, value)
{
   pcSetFitted(pcTiles[i].value_txt,value);
}

// --- frame -----------------------------------------------------------------------------

function pcDrawFrame(h)
{
   var bg = pc.bg;
   bg.clear();
   pcGradientRect(bg,PC_X,PC_Y,PC_W,h,30,0x2E4FB0,0x141F55,100,PC_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-160,y:PC_Y - 260,w:820,h:560,r:0});
   pcRoundRect(bg,PC_X + 2,PC_Y + 2,PC_W - 4,h - 4,28);
   bg.endFill();

   // The header card, with a few sparkles (fixed seed, same every time).
   pcGradientRect(bg,20,116,706,128,22,0x0A1440,0x0A1440,32,PC_RIM,20,1);
   var s = 2626;
   var i;
   for(i = 0; i < 16; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = 160 + s % 540;
      s = (s * 1103515245 + 12345) % 2147483648;
      var y = 124 + s % 30;
      var r = 1 + s % 2;
      bg.beginFill(0xFFFFFF,20 + s % 40);
      bg.moveTo(x,y - r * 2);
      bg.curveTo(x,y,x + r * 2,y);
      bg.curveTo(x,y,x,y + r * 2);
      bg.curveTo(x,y,x - r * 2,y);
      bg.curveTo(x,y,x,y - r * 2);
      bg.endFill();
   }
}

function pcMakeChip(i, label)
{
   var w = 182;
   var chip = pc.createEmptyMovieClip("chip" + i,pc.getNextHighestDepth());
   chip._x = 152 + i * (w + 9);
   chip._y = 206;
   pcGradientRect(chip,0,0,w,30,15,0x1B2F78,0x13215A,100,PC_RIM,35,1);
   var l = pcText(chip,"label_txt",label,10,6,70,20,11,PC_GOLD,"left");
   chip.value_txt = pcText(chip,"value_txt","-",l._x + l.textWidth + 10,5,w - l.textWidth - 26,22,14,0xFFFFFF,"left");
   return chip;
}

function pcMakeTile(i)
{
   var def = PC_TILES[i];
   var tile = pc.createEmptyMovieClip("tile" + i,pc.getNextHighestDepth());
   tile._x = 20 + i * (PC_TILE_W + 9);
   tile._y = PC_TILE_Y;
   var w = PC_TILE_W;
   var h = PC_TILE_H;
   pcGradientRect(tile,0,0,w,h,18,0x284A9E,0x162660,100,PC_RIM,45,1.5);
   tile.lineStyle();
   tile.beginGradientFill("radial",[0xFFFFFF,0xFFFFFF],[30,0],[0,255],{matrixType:"box",x:w / 2 - 50,y:-18,w:100,h:100,r:0});
   pcRoundRect(tile,3,3,w - 6,70,16);
   tile.endFill();
   tile.filters = [new flash.filters.DropShadowFilter(4,90,0x050A24,0.5,10,10,1,2)];
   if(def[1].length > 0)
   {
      var art = tile.attachMovie(def[1],"art",tile.getNextHighestDepth());
      art._width = art._height = 58;
      art._x = (w - 58) / 2;
      art._y = 10;
   }
   tile.value_txt = pcText(tile,"value_txt","0",4,70,w - 8,32,24,0xFFFFFF);
   tile.value_txt.filters = pcOutline();
   pcText(tile,"label_txt",def[0],4,102,w - 8,20,13,PC_SOFT);
   pcTiles.push(tile);
}

// The stock VIP star (setVipLevel_Lua attaches vipStar<n> to star_mc), fitted into the VIP tile's icon spot.
function pcPlaceStar()
{
   var tile = pcTiles[4];
   star_mc._xscale = star_mc._yscale = 100;
   var b = star_mc.getBounds(star_mc);
   if(b.xMax <= b.xMin)
   {
      return;
   }
   var scale = 54 / Math.max(b.xMax - b.xMin,b.yMax - b.yMin);
   star_mc._xscale = star_mc._yscale = scale * 100;
   star_mc._x = tile._x + PC_TILE_W / 2 - (b.xMin + b.xMax) / 2 * scale;
   star_mc._y = tile._y + 39 - (b.yMin + b.yMax) / 2 * scale;
   star_mc.swapDepths(_root.getNextHighestDepth());
}

// Highlights: the stock stat lines (stat1-9) that have a label, three to a row on chips; the card grows to fit.
function pcLayoutStats()
{
   var g = pc.stats;
   g.clear();
   var shown = 0;
   var i;
   for(i = 1; i <= 9; i++)
   {
      var stat = _root["stat" + i];
      if(stat.label.text.length > 0)
      {
         var x = 20 + (shown % 3) * 237;
         var y = PC_STATS_Y + 30 + Math.floor(shown / 3) * 46;
         pcGradientRect(g,x,y,228,38,14,0x1B2F78,0x13215A,100,PC_RIM,35,1);
         stat._x = x + 8;
         stat._y = y + 5;
         stat._visible = true;
         stat.label.textColor = 0xFFFFFF;
         stat.statValue.textColor = PC_GOLD;
         shown++;
      }
      else
      {
         stat._visible = false;
      }
   }
   pc.statsTitle._visible = shown > 0;
   pcDrawFrame(shown > 0 ? PC_STATS_Y + 30 + Math.ceil(shown / 3) * 46 + 10 - PC_Y : PC_STATS_Y - PC_Y);
}

function pcBuild()
{
   // Everything stock goes but the title picker, the rename button (an invisible hit area on our chip),
   // the VIP star and the stat lines (placed by pcPlaceStar and pcLayoutStats).
   var keep = {combobox_mc:1,editButton:1,star_mc:1};
   var name;
   for(name in _root)
   {
      var o = _root[name];
      if(keep[name] || name.substr(0,4) == "stat" && name.length == 5)
      {
         continue;
      }
      if(typeof o == "movieclip" && o._parent == _root)
      {
         o._visible = false;
      }
   }
   var fields = [characterName,totalCoins,totalStationCash,level,totalCollections,totalQuests,vip_txt,vip_value_txt,creationDate,playerAge,playedTime,myBirthday,ageTitle_txt,playerGender_txt,genderTitle_txt];
   var f;
   for(f = 0; f < fields.length; f++)
   {
      fields[f]._visible = false;
   }

   pc = _root.createEmptyMovieClip("sanctuaryProfile",-16384);
   pc.bg = pc.createEmptyMovieClip("bg",1);
   pc.bg.filters = [new flash.filters.DropShadowFilter(6,90,0x050A24,0.6,18,18,1,2)];
   pc.stats = pc.createEmptyMovieClip("stats",2);

   // The portrait, on a silhouette until the client's portrait arrives.
   var frame = pc.createEmptyMovieClip("portraitFrame",3);
   pcGradientRect(frame,30,126,108,108,22,0xFFFFFF,0xBFD3FF,100,PC_GOLD,100,3);
   var silhouette = frame.attachMovie("sanctuaryProfileSilhouette","silhouette",1);
   silhouette._width = silhouette._height = 88;
   silhouette._x = 40;
   silhouette._y = 136;
   var holder = pc.portrait = pc.createEmptyMovieClip("portrait",4);
   holder.beginFill(0,0);
   pcRoundRect(holder,0,0,104,104,0);
   holder.endFill();
   holder._x = 32;
   holder._y = 128;
   var mask = pc.createEmptyMovieClip("portraitMask",5);
   mask.beginFill(0,100);
   pcRoundRect(mask,32,128,104,104,20);
   mask.endFill();
   holder.setMask(mask);

   pc.name_txt = pcText(pc,"name_txt","",150,120,500,44,32,0xFFFFFF,"left");
   pc.name_txt.filters = pcOutline();

   pc.chips = [pcMakeChip(0,"BIRTHDAY"),pcMakeChip(1,"AGE"),pcMakeChip(2,"PLAYED")];
   var i;
   for(i = 0; i < PC_TILES.length; i++)
   {
      pcMakeTile(i);
   }
   pc.statsTitle = pcText(pc,"statsTitle","Highlights",24,PC_STATS_Y,300,26,18,0xFFFFFF,"left");
   pc.statsTitle.filters = pcOutline();

   combobox_mc._x = 152;
   combobox_mc._y = 166;
   combobox_mc.swapDepths(_root.getNextHighestDepth());

   // A gold rename chip under the stock button, which keeps its tooltip and click.
   editButton._alpha = 0;
   editButton._x = PC_X + PC_W - 22 - editButton._width;
   editButton._y = 126;
   var b = editButton.getBounds(_root);
   var edit = pc.createEmptyMovieClip("edit",pc.getNextHighestDepth());
   var cx = (b.xMin + b.xMax) / 2;
   var cy = (b.yMin + b.yMax) / 2;
   edit.lineStyle(1,0xFFF3C4,90);
   edit.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:cx - 17,y:cy - 17,w:34,h:34,r:Math.PI / 2});
   pcRoundRect(edit,cx - 17,cy - 17,34,34,17);
   edit.endFill();
   // A pencil.
   edit.lineStyle(4,0x3A2400,100);
   edit.moveTo(cx - 6,cy + 6);
   edit.lineTo(cx + 6,cy - 6);
   edit.lineStyle(2,0x3A2400,100);
   edit.moveTo(cx - 9,cy + 9);
   edit.lineTo(cx - 6,cy + 6);

   pcLayoutStats();
}

// --- Lua -------------------------------------------------------------------------------

var pcSetPlayerData = setPlayerData;
setPlayerData = function(name, guid, numCoins, wallet, jobLevelsGained, collectionsCompleted, questsCompleted, creationDateStr, age, playTime, isMember, gender, race, vipRank)
{
   pcSetPlayerData.apply(_root,arguments);
   pcSetFitted(pc.name_txt,characterName.text);
   pcSetTile(0,pcNumber(numCoins));
   pcSetTile(1,pcNumber(jobLevelsGained));
   pcSetTile(2,pcNumber(collectionsCompleted));
   pcSetTile(3,pcNumber(questsCompleted));
   pcSetFitted(pc.chips[0].value_txt,pcValue(creationDateStr));
   pcSetFitted(pc.chips[1].value_txt,pcValue(age));
   pcSetFitted(pc.chips[2].value_txt,pcValue(playTime));
   if(guid != undefined && String(guid) != pcGuid)
   {
      pcGuid = String(guid);
      _root.frBindPlayerPortrait(String(pc.portrait),pcGuid);
   }
};

var pcSetWalletData = setWalletData;
setWalletData = function(message)
{
   pcSetWalletData(message);
   pcSetTile(0,pcNumber(message));
};

// Picking a title renames the stock name line; the header follows it.
var pcSetCharacterName = SetCharacterName;
SetCharacterName = function(str)
{
   pcSetCharacterName(str);
   pcSetFitted(pc.name_txt,characterName.text);
};

var pcSetVipLevel = setVipLevel_Lua;
setVipLevel_Lua = function(n)
{
   pcSetVipLevel(n);
   pcSetTile(4,pcNumber(n));
   pcPlaceStar();
};

var pcSetStat = setStat_lua;
setStat_lua = function(statId, iconId, label, value)
{
   pcSetStat(statId,iconId,label,value);
   pcLayoutStats();
};

var pcSetData = setData;
setData = function(message, stats)
{
   pcSetData(message,stats);
   pcLayoutStats();
};

pcBuild();
// ===== End Sanctuary Profile Card =====

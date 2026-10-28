// ===== Sanctuary Farming Browser Extras =====
// Appended to Browser_V2's main timeline after browser/tile_browser.as by build_client_mods.py.
// The farming browser's seed and tool cards (wndFarmingBrowser, the same movie as the tile browser) finished in
// the tile browser's style: a seed's count or a tool's charges sit on a gold chip at the bottom left of the art's
// socket (a navy "Out" chip when you have none left), and a seed you can't plant yet or a tool you can't use
// (canUse / active 0) gets a navy veil over its socket and a gold padlock badge, for tools too, whose stock art
// has no lock. The stock count fields stay (invisible) for Lua's text, which the chips mirror; the stock lock
// is hidden for the badge. Everything here is drawn over the cards with no mouse handlers, so the cards keep
// their clicks, hovers and tooltips.

var FE_INK = 0x3A2400;
var FE_SOFT = 0xC9D6FF;

// The chip and padlock badge layer on top of a card (the socket is tile_browser.as's TB_SOCKET_*).
function feLayer(b)
{
   if(b.feLayer == undefined)
   {
      b.feLayer = b.createEmptyMovieClip("sanctuaryFarm",b.getNextHighestDepth());
      b.feLayer.veil = b.feLayer.createEmptyMovieClip("veil",1);
      b.feLayer.chip = b.feLayer.createEmptyMovieClip("chip",2);
      b.feLayer.lock = b.feLayer.createEmptyMovieClip("lock",3);
      b.feLayer.count_txt = tbText(b.feLayer,"count_txt","",0,0,60,24,16,FE_INK);
   }
   return b.feLayer;
}

// A padlock on a gold disc, top right on the socket.
function feDrawLock(mc)
{
   var x = TB_SOCKET_X + TB_SOCKET - 14;
   var y = TB_SOCKET_Y + 14;
   mc.clear();
   tbGoldDisc(mc,x,y,15,false);
   mc.lineStyle(3,FE_INK,100);
   mc.moveTo(x - 5,y - 1);
   mc.lineTo(x - 5,y - 4);
   mc.curveTo(x - 5,y - 10,x,y - 10);
   mc.curveTo(x + 5,y - 10,x + 5,y - 4);
   mc.lineTo(x + 5,y - 1);
   mc.lineStyle();
   mc.beginFill(FE_INK,100);
   tbRoundRect(mc,x - 8,y - 2,16,12,3);
   mc.endFill();
   mc.beginFill(0xFFE08A,100);
   tbCircle(mc,x,y + 3.5,2);
   mc.endFill();
   mc.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];
}

// The count's chip: gold with the number, or navy with "Out" when there are none.
function feDrawChip(layer, text, out)
{
   var c = layer.chip;
   var t = layer.count_txt;
   c.clear();
   if(text == undefined || text == "" || text == " ")
   {
      t.text = "";
      return;
   }
   t.text = out ? "Out" : text;
   var w = Math.max(34,Math.round(t.textWidth) + 20);
   var x = TB_SOCKET_X + 6;
   var y = TB_SOCKET_Y + TB_SOCKET - 30;
   if(out)
   {
      tbGradientRect(c,x,y,w,24,12,0x2B4FA8,0x17286A,100,0xFFE08A,80,1.5);
   }
   else
   {
      c.lineStyle(1.5,0xFFF3C4,100);
      c.beginGradientFill("linear",[0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:24,r:Math.PI / 2});
      tbRoundRect(c,x,y,w,24,12);
      c.endFill();
      c.lineStyle();
      c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:12,r:Math.PI / 2});
      tbRoundRect(c,x + 3,y + 2,w - 6,10,5);
      c.endFill();
   }
   c.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,4,4,1,2)];
   t._x = x;
   t._y = y + 1;
   t._width = w;
   t._height = 22;
   t.textColor = out ? FE_SOFT : FE_INK;
}

// The veil over a locked card's socket.
function feDrawVeil(mc, on)
{
   mc.clear();
   if(on)
   {
      mc.beginFill(0x0A1440,45);
      tbRoundRect(mc,TB_SOCKET_X,TB_SOCKET_Y,TB_SOCKET,TB_SOCKET,16);
      mc.endFill();
   }
}

// count: the stock count field; locked: can't be used yet; out: none left.
function feDecorate(b, obj, count, locked, out)
{
   var layer = feLayer(b);
   var shown = Number(obj.ID) != -1 && count != undefined && (count._visible || out);
   // The stock field keeps Lua's text (and stays a source for this mirror); the chip shows it.
   count._alpha = 0;
   feDrawChip(layer,shown && !locked ? String(count.text) : "",out);
   feDrawVeil(layer.veil,locked);
   layer.lock._visible = locked;
   if(locked)
   {
      feDrawLock(layer.lock);
   }
   b.lock_mc._alpha = 0;
}

function fePatchBadges()
{
   var seeds = fr.badges.SeedsBadge.prototype;
   var setSeed = seeds.setItem;
   seeds.setItem = function(obj)
   {
      setSeed.apply(this,arguments);
      var locked = Number(obj.ID) != -1 && obj.canUse == 0;
      var out = Number(obj.ID) != -1 && !locked && obj.seedCount == 0;
      feDecorate(this,obj,this.seedCount_txt,locked,out);
   };
   var tools = fr.badges.FarmToolBadge.prototype;
   var setTool = tools.setItem;
   tools.setItem = function(obj)
   {
      setTool.apply(this,arguments);
      var locked = Number(obj.ID) != -1 && obj.active == 0;
      var out = Number(obj.ID) != -1 && !locked && obj.usesCharges != 0 && Number(obj.consumibleCount) == 0;
      feDecorate(this,obj,this.count_txt,locked,out);
   };
}

fePatchBadges();
// ===== End Sanctuary Farming Browser Extras =====

// ===== Sanctuary Housing Directory =====
// Appended to HousingBrowser's main timeline by build_client_mods.py.
// Redraws the Housing Directory (wndHousingBrowser: published houses to visit and rate) in the Main Menu's style
// (ClientMods/MyStuffBrowser/main_menu.as): a navy glass frame with "Housing Directory" in gold beside the house and a
// gold close button, a recessed search pill with a gold search button, the Popular / New / Friends / Results tabs as
// navy chips (the open one gold), the selected house's snapshot in a rounded gold-rimmed frame, and the list as glass
// rows (gold rim and tint for the selected one, lighter on hover) with gold stars. Visit and More are gold pills, and
// the selected and featured houses sit on glass cards. The stock clips keep their roles (hit areas, Lua callbacks):
// their art is hidden (alpha 0) and the new look is drawn under them; the crest's glass (a shape) is dropped at build
// time (REMOVED_DEPTHS). Lua's texts arrive in the stock yellow and white, so they're recolored after each update.

var HD_FRAME = [8,58,1004,654];
// The panels: the snapshot's recess, the list's, and the cards under the snapshot (selected, featured).
var HD_SHOT = [49,154,512,384];
var HD_SHOT_PANEL = [37,148,536,398];
var HD_LIST_PANEL = [580,148,406,548];
var HD_SELECTED_CARD = [37,552,230,142];
var HD_FEATURED_CARD = [273,552,300,142];
var HD_GOLD = 0xFFC531;
var HD_RIM = 0x9DB8FF;
var HD_SOFT = 0xC9D6FF;
var HD_INK = 0x3A2400;

// --- drawing helpers -------------------------------------------------------------------

function hdRoundRect(mc, x, y, w, h, r)
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

function hdGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
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
   hdRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function hdCircle(mc, x, y, r)
{
   var k = Math.tan(Math.PI / 8);
   var s = Math.sin(Math.PI / 4);
   mc.moveTo(x + r,y);
   mc.curveTo(x + r,y + k * r,x + s * r,y + s * r);
   mc.curveTo(x + k * r,y + r,x,y + r);
   mc.curveTo(x - k * r,y + r,x - s * r,y + s * r);
   mc.curveTo(x - r,y + k * r,x - r,y);
   mc.curveTo(x - r,y - k * r,x - s * r,y - s * r);
   mc.curveTo(x - k * r,y - r,x,y - r);
   mc.curveTo(x + k * r,y - r,x + s * r,y - s * r);
   mc.curveTo(x + r,y - k * r,x + r,y);
}

// A gold pill with a glassy shine across its top half.
function hdGoldRect(mc, x, y, w, h, r, over)
{
   mc.lineStyle(over ? 2 : 1.5,0xFFF3C4,over ? 100 : 90);
   mc.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   hdRoundRect(mc,x,y,w,h,r);
   mc.endFill();
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h / 2,r:Math.PI / 2});
   hdRoundRect(mc,x + 3,y + 2,w - 6,h / 2,Math.min(r,h / 4));
   mc.endFill();
}

function hdText(mc, name, txt, x, y, w, h, size, color, align)
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
   return t;
}

function hdSparkle(mc, x, y, r, alpha)
{
   mc.lineStyle();
   mc.beginFill(0xFFFFFF,alpha);
   mc.moveTo(x,y - r * 2);
   mc.curveTo(x,y,x + r * 2,y);
   mc.curveTo(x,y,x,y + r * 2);
   mc.curveTo(x,y,x - r * 2,y);
   mc.curveTo(x,y,x,y - r * 2);
   mc.endFill();
}

function hdSparkles(mc, seed, count, x, y, w, h)
{
   var s = seed;
   var i;
   for(i = 0; i < count; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var sx = x + s % w;
      s = (s * 1103515245 + 12345) % 2147483648;
      hdSparkle(mc,sx,y + s % h,1 + s % 3,25 + s % 50);
   }
}

function hdOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.9,3,3,4,2)];
}

function hdShadow(distance, blur, alpha)
{
   return [new flash.filters.DropShadowFilter(distance,90,0x050A24,alpha,blur,blur,1,2)];
}

function hdGlow(color, blur)
{
   return [new flash.filters.GlowFilter(color,0.8,blur,blur,2,2)];
}

function hdGoldRule(mc, x, y, w)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[HD_GOLD,HD_GOLD,HD_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:x,y:y,w:w,h:2,r:0});
   hdRoundRect(mc,x,y,w,2,1);
   mc.endFill();
}

function hdPanel(mc, p)
{
   hdGradientRect(mc,p[0],p[1],p[2],p[3],22,0x0A1440,0x0A1440,40,HD_RIM,25,1);
}

function hdCard(mc, p)
{
   hdGradientRect(mc,p[0],p[1],p[2],p[3],20,0x284A9E,0x162660,100,HD_RIM,45,1.5);
}

function hdColor(t, color)
{
   if(t == undefined)
   {
      return;
   }
   t.textColor = color;
   t.filters = hdOutline();
}

function hdGoldStars(stars)
{
   if(stars.star_tweener_mc != undefined)
   {
      new Color(stars.star_tweener_mc).setRGB(HD_GOLD);
   }
   stars.filters = hdShadow(2,4,0.6);
}

// --- FRIconButton skins ------------------------------------------------------------------
// A skinned button has hdDraw(state); its states call it after the stock code has swapped the art.

var hdStateUp = FRIconButton.prototype.setStateUp;
FRIconButton.prototype.setStateUp = function()
{
   hdStateUp.apply(this,arguments);
   if(this.hdDraw != undefined)
   {
      this.hdDraw("up");
   }
};

var hdStateOver = FRIconButton.prototype.setStateOver;
FRIconButton.prototype.setStateOver = function()
{
   hdStateOver.apply(this,arguments);
   if(this.hdDraw != undefined && this.m_isEnabled)
   {
      this.hdDraw("over");
   }
};

var hdStateDown = FRIconButton.prototype.setStateDown;
FRIconButton.prototype.setStateDown = function()
{
   hdStateDown.apply(this,arguments);
   if(this.hdDraw != undefined && this.m_isEnabled)
   {
      this.hdDraw("down");
   }
};

function hdSkin(btn, draw)
{
   if(btn == undefined || btn.hdSkinMc != undefined)
   {
      return;
   }
   btn.up_mc._alpha = btn.over_mc._alpha = btn.down_mc._alpha = 0;
   btn.hdSkinMc = btn.createEmptyMovieClip("hdSkinMc",-16384);
   btn.hdDraw = draw;
   btn.hdDraw("up");
}

// Visit and More (playButton, 210x83 art drawn scaled): a gold pill with the stock label's text in dark ink.
function hdDrawPlay(state)
{
   var s = this.hdSkinMc;
   s.clear();
   var over = state == "over" || state == "down";
   hdGoldRect(s,8,10,194,62,31,over);
   if(s.label_txt == undefined)
   {
      hdText(s,"label_txt","",8,22,194,44,30,HD_INK);
   }
   var t = s.label_txt;
   var f = t.getTextFormat();
   f.size = 30;
   t.setNewTextFormat(f);
   t.text = this.up_mc.textField_txt.text;
   while(t.textWidth > 176 && f.size > 12)
   {
      f.size--;
      t.setTextFormat(f);
   }
   s._y = state == "down" ? 1.5 : 0;
   this.filters = over ? hdGlow(HD_GOLD,16) : hdShadow(4,10,0.55);
}

// Search: a gold circle with a dark magnifier (32x32 art).
function hdDrawSearch(state)
{
   var s = this.hdSkinMc;
   s.clear();
   var over = state == "over" || state == "down";
   s.lineStyle(over ? 2 : 1.5,0xFFF3C4,100);
   s.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:0,y:0,w:32,h:32,r:Math.PI / 2});
   hdCircle(s,16,16,16);
   s.endFill();
   s.lineStyle(3,HD_INK,100);
   hdCircle(s,14,14,6);
   s.moveTo(18.5,18.5);
   s.lineTo(23,23);
   this.filters = over ? hdGlow(HD_GOLD,12) : hdShadow(3,8,0.5);
}

// --- frame -----------------------------------------------------------------------------

var hdFrame;

function hdDrawFrame()
{
   var bg = hdFrame.bg;
   var f = HD_FRAME;
   bg.clear();
   hdGradientRect(bg,f[0],f[1],f[2],f[3],30,0x2E4FB0,0x141F55,100,HD_RIM,80,3);
   bg.lineStyle();
   bg.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[32,0],[0,255],{matrixType:"box",x:-200,y:-320,w:1000,h:780,r:0});
   hdRoundRect(bg,f[0] + 2,f[1] + 2,f[2] - 4,f[3] - 4,28);
   bg.endFill();
   // The header band, with sparkles and a gold rule under the title.
   bg.beginFill(0x0A1440,30);
   hdRoundRect(bg,f[0] + 4,f[1] + 4,f[2] - 8,44,26);
   bg.endFill();
   hdSparkles(bg,9191,26,120,f[1] + 8,780,34);
   hdGoldRule(bg,412,f[1] + 46,200);

   hdPanel(bg,HD_SHOT_PANEL);
   hdPanel(bg,HD_LIST_PANEL);
   hdCard(bg,HD_SELECTED_CARD);
   hdCard(bg,HD_FEATURED_CARD);

   // The search: a recessed pill behind the stock field (its plate is hidden).
   hdGradientRect(bg,126,111,404,34,17,0x0A1440,0x0A1440,80,HD_RIM,45,1);
}

// The gold round close button, centered on the stock one (an invisible hit area).
function hdDrawClose(state)
{
   var c = hdFrame.close;
   c.clear();
   var over = state == "over" || state == "down";
   var r = 17;
   c.lineStyle(over ? 2 : 1.5,0xFFF3C4,100);
   c.beginGradientFill("linear",over ? [0xFFF0B8,0xFFB43C] : [0xFFE08A,0xF5A623],[100,100],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r * 2,r:Math.PI / 2});
   hdCircle(c,0,0,r);
   c.endFill();
   c.lineStyle();
   c.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[55,0],[0,255],{matrixType:"box",x:- r,y:- r,w:r * 2,h:r,r:Math.PI / 2});
   hdRoundRect(c,-11,-15,22,12,6);
   c.endFill();
   c.lineStyle(4,HD_INK,100);
   c.moveTo(-6,-6);
   c.lineTo(6,6);
   c.moveTo(6,-6);
   c.lineTo(-6,6);
   c.filters = over ? hdGlow(HD_GOLD,14) : hdShadow(3,8,0.5);
   gs.TweenLite.to(c,0.5,{_xscale:over ? 114 : 100,_yscale:over ? 114 : 100,ease:mx.transitions.easing.Elastic.easeOut});
}

function hdBuildFrame()
{
   hdFrame = _root.createEmptyMovieClip("hdFrame",-16384);
   hdFrame.bg = hdFrame.createEmptyMovieClip("bg",1);
   hdFrame.bg.filters = hdShadow(6,18,0.6);
   hdFrame.close = hdFrame.createEmptyMovieClip("close",2);

   // The stock frame and panels (unnamed clips) stay as invisible hit areas; the search field's plate too.
   bg_mc._alpha = 0;
   var depths = [5,7,9,25,127];
   var i;
   for(i = 0; i < depths.length; i++)
   {
      _root.getInstanceAtDepth(depths[i] - 16384)._alpha = 0;
   }

   // The title, with the house (the stock crest's house) beside it.
   var title = hdText(hdFrame,"title_txt","Housing Directory",262,HD_FRAME[1] + 6,500,40,28,0xFFE08A);
   title.filters = hdOutline();
   var house = _root.getInstanceAtDepth(1 - 16384);
   if(house != undefined)
   {
      house._width = 54;
      house._height = 42;
      var b = house.getBounds(_root);
      house._x += Math.round(512 - title.textWidth / 2 - 64 - b.xMin);
      house._y += Math.round(HD_FRAME[1] + 5 - b.yMin);
      house.filters = hdShadow(2,6,0.5);
   }

   // The close button: the stock one is the hit area, moved into the header; the gold circle sits on it.
   exitButton_mc._x = 960;
   exitButton_mc._y = HD_FRAME[1] + 8;
   exitButton_mc.up_mc._alpha = exitButton_mc.over_mc._alpha = exitButton_mc.down_mc._alpha = 0;
   var e = exitButton_mc.getBounds(_root);
   hdFrame.close._x = Math.round((e.xMin + e.xMax) / 2);
   hdFrame.close._y = Math.round((e.yMin + e.yMax) / 2);
   exitButton_mc.hdDraw = hdDrawClose;
   hdDrawClose("up");

   // The snapshot: rounded off by a mask and rimmed in gold (the rim has no mouse handlers, so the snapshot's
   // click-to-visit still works).
   var mask = _root.createEmptyMovieClip("hdShotMask",10);
   mask.beginFill(0x000000,100);
   hdRoundRect(mask,HD_SHOT[0],HD_SHOT[1],HD_SHOT[2],HD_SHOT[3],16);
   mask.endFill();
   holder_mc.setMask(mask);
   var rim = _root.createEmptyMovieClip("hdShotRim",11);
   rim.lineStyle(3,0xFFE08A,100);
   hdRoundRect(rim,HD_SHOT[0],HD_SHOT[1],HD_SHOT[2],HD_SHOT[3],16);
   rim.filters = hdGlow(HD_GOLD,8);

   hdColor(searchTitle_txt,0xFFFFFF);
   hdColor(search_txt,0xFFFFFF);
   var lf = txtTitleLabel.getTextFormat();
   lf.size = 20;
   txtTitleLabel.setTextFormat(lf);
   hdColor(txtTitleLabel,0xFFE08A);

   hdDrawFrame();
}

// --- tabs ------------------------------------------------------------------------------

// A tab: a navy chip, lighter on hover, gold with dark ink when it's the open one. The stock art goes invisible
// after every state change (its up_mc is the tab's hit area, so it stays, at alpha 0).
function hdDrawTab(tab, state)
{
   tab.up_mc._alpha = 0;
   tab.over_mc._visible = tab.sel_mc._visible = false;
   var s = tab.hdSkin;
   s.clear();
   var x = 3;
   var y = -27;
   var w = 84;
   var h = 26;
   if(state == "selected")
   {
      hdGoldRect(s,x,y,w,h,13,false);
      tab.filterName_txt.textColor = HD_INK;
      tab.filterName_txt.filters = [];
      s.filters = hdGlow(HD_GOLD,10);
   }
   else
   {
      var over = state == "over";
      hdGradientRect(s,x,y,w,h,13,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : HD_RIM,over ? 100 : 55,1.5);
      tab.filterName_txt.textColor = 0xFFFFFF;
      tab.filterName_txt.filters = hdOutline();
      s.filters = hdShadow(2,6,0.5);
   }
}

var hdTabSelected = FilterButton.prototype.setStateSelected;
FilterButton.prototype.setStateSelected = function()
{
   hdTabSelected.apply(this,arguments);
   if(this.hdSkin != undefined)
   {
      hdDrawTab(this,"selected");
   }
};

var hdTabUp = FilterButton.prototype.setStateUp;
FilterButton.prototype.setStateUp = function()
{
   hdTabUp.apply(this,arguments);
   if(this.hdSkin != undefined)
   {
      hdDrawTab(this,this.m_selected ? "selected" : "up");
   }
};

var hdTabOver = FilterButton.prototype.setStateOver;
FilterButton.prototype.setStateOver = function()
{
   hdTabOver.apply(this,arguments);
   if(this.hdSkin != undefined)
   {
      hdDrawTab(this,this.m_selected ? "selected" : "over");
   }
};

function hdSkinTab(tab)
{
   tab.hdSkin = tab.createEmptyMovieClip("hdSkin",-16384);
   hdDrawTab(tab,tab.m_selected ? "selected" : "up");
}

// --- list rows -------------------------------------------------------------------------

// A row: glass, lighter on hover, rimmed and tinted gold when selected; the thumbnail gets a rim.
function hdDrawRow(row)
{
   var s = row.hdSkin;
   s.clear();
   var selected = row.hilite_mc._visible;
   var over = row.hdOver;
   if(selected)
   {
      hdGradientRect(s,2,2,372,76,18,0x4A5FB8,0x2A2F7A,100,HD_GOLD,100,2.5);
      s.lineStyle();
      s.beginGradientFill("linear",[0xFFC531,0xFFC531],[30,0],[0,255],{matrixType:"box",x:2,y:2,w:372,h:76,r:0});
      hdRoundRect(s,3,3,370,74,17);
      s.endFill();
   }
   else
   {
      hdGradientRect(s,2,2,372,76,18,over ? 0x3F66D6 : 0x284A9E,over ? 0x1D307A : 0x162660,100,over ? 0xFFE08A : HD_RIM,over ? 100 : 45,over ? 2 : 1.5);
   }
   s.filters = selected ? hdGlow(HD_GOLD,10) : hdShadow(2,6,0.5);
   var rim = row.hdRim;
   rim.clear();
   rim.lineStyle(2,selected || over ? 0xFFE08A : HD_RIM,100);
   hdRoundRect(rim,2,4,98,72,10);
}

function hdColorRow(row)
{
   hdColor(row.title_txt,0xFFE08A);
   hdColor(row.owner_txt,HD_SOFT);
   hdColor(row.votes_txt,HD_SOFT);
}

function hdStyleRow(row)
{
   if(row.hdSkin != undefined)
   {
      return;
   }
   row.hdSkin = row.createEmptyMovieClip("hdSkin",-16384);
   // Over the thumbnail; no mouse handlers, so it doesn't take the row's clicks.
   row.hdRim = row.createEmptyMovieClip("hdRim",30);
   row.bg_mc._alpha = 0;
   row.hilite_mc._alpha = 0;
   // The row's hit area is bg_mc (it selects on press); it lights the row on hover too.
   row.bg_mc.hdRow = row;
   row.bg_mc.onRollOver = function()
   {
      this.hdRow.hdOver = true;
      hdDrawRow(this.hdRow);
      fscommand("FR_event","PlaySound(DockOver)");
   };
   row.bg_mc.onRollOut = row.bg_mc.onReleaseOutside = function()
   {
      this.hdRow.hdOver = false;
      hdDrawRow(this.hdRow);
   };
}

var hdRowSetSelected = VertListItem.prototype.setSelected;
VertListItem.prototype.setSelected = function(s)
{
   hdRowSetSelected.apply(this,arguments);
   hdStyleRow(this);
   hdDrawRow(this);
};

var hdRowDrawItem = VertListItem.prototype.drawItem;
VertListItem.prototype.drawItem = function()
{
   hdRowDrawItem.apply(this,arguments);
   hdStyleRow(this);
   hdColorRow(this);
   hdGoldStars(this.votingStars_mc);
   hdDrawRow(this);
};

// --- selected and featured houses ------------------------------------------------------

var hdOnItemSelect = onItemSelect;
onItemSelect = function(item)
{
   hdOnItemSelect.apply(_root,arguments);
   hdColor(title_txt,0xFFE08A);
   hdColor(owner_txt,HD_SOFT);
   hdColor(votes_txt,HD_SOFT);
};

var hdFeaturedSetData = FeaturedItem.prototype.setData;
FeaturedItem.prototype.setData = function(data)
{
   hdFeaturedSetData.apply(this,arguments);
   hdColor(this.title_txt,0xFFE08A);
   hdColor(this.owner_txt,HD_SOFT);
   hdColor(this.votes_txt,HD_SOFT);
   hdSkin(this.visitButton_mc,hdDrawPlay);
   this.visitButton_mc.hdDraw("up");
};

// --- build -----------------------------------------------------------------------------

function hdBuild()
{
   hdBuildFrame();

   hdSkin(visitButton_mc,hdDrawPlay);
   hdSkin(refreshButton_mc,hdDrawPlay);
   hdSkin(searchButton_mc,hdDrawSearch);
   hdSkin(featuredHouse_mc.visitButton_mc,hdDrawPlay);

   var tabs = [tab1_mc,tab2_mc,tab3_mc,tab4_mc];
   var i;
   for(i = 0; i < tabs.length; i++)
   {
      hdSkinTab(tabs[i]);
   }

   hdGoldStars(votingStars_mc);
   hdGoldStars(featuredHouse_mc.votingStars_mc);
   hdColor(title_txt,0xFFE08A);
   hdColor(owner_txt,HD_SOFT);
   hdColor(votes_txt,HD_SOFT);
   hdColor(featuredHouse_mc.title_txt,0xFFE08A);
   hdColor(featuredHouse_mc.owner_txt,HD_SOFT);
   hdColor(featuredHouse_mc.votes_txt,HD_SOFT);
}

hdBuild();
// ===== End Sanctuary Housing Directory =====

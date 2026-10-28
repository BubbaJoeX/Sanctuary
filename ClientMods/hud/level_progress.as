// ===== Sanctuary Level Progress =====
// Appended to FRLevelProgressBar's main timeline by build_client_mods.py.
// The job level bar (wndLevelProgressPane, 185x41: the job's title, its rank icon and level, and the progress to the
// next level) in the Main Menu's style (ClientMods/MyStuffBrowser/main_menu.as): a navy glass pill with a recessed
// track and a glossy gold fill that glides to each new value (gold with a glow at the top rank), the rank icon in
// a frosted medallion with a gold rim and the title and level in outlined white. The stock frame (an unnamed clip
// in progress_mc) and purple bar are made invisible; setPercent and setLevel are wrapped (the stock mask still
// follows them).

var LP_RIM = 0x9DB8FF;
var LP_GOLD = 0xFFC531;
var LP_GOLD_LIGHT = 0xFFE08A;
// In progress_mc's coordinates: the pill, the track and the stock mask's full width (3.4 .. 179.4).
var LP_W = 185;
var LP_H = 41;
var LP_TRACK_X = 8;
var LP_TRACK_Y = 9;
var LP_TRACK_W = 171;
var LP_TRACK_H = 23;

function lpRoundRect(mc, x, y, w, h, r)
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

function lpCircle(mc, x, y, r)
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

function lpGloss(mc, x, y, w, h, r, alpha)
{
   mc.lineStyle();
   mc.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[alpha,0],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   lpRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function lpOutline()
{
   return [new flash.filters.GlowFilter(0x0A1030,0.95,3,3,5,2)];
}

function lpBuild()
{
   var p = progress_mc;
   p.getInstanceAtDepth(-16383)._alpha = 0;
   p.barHolder_mc._alpha = 0;

   var g = p.createEmptyMovieClip("sanctuaryFrame",-16384);
   g.lineStyle(2,LP_RIM,80);
   g.beginGradientFill("linear",[0x2E4FB0,0x141F55],[96,96],[0,255],{matrixType:"box",x:0,y:0,w:LP_W,h:LP_H,r:Math.PI / 2});
   lpRoundRect(g,1.5,1.5,LP_W - 3,LP_H - 3,(LP_H - 3) / 2);
   g.endFill();
   g.lineStyle();
   g.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[28,0],[0,255],{matrixType:"box",x:-30,y:-40,w:140,h:90,r:0});
   lpRoundRect(g,3,3,LP_W - 6,LP_H - 6,(LP_H - 6) / 2);
   g.endFill();
   g.lineStyle(1,LP_RIM,30);
   g.beginGradientFill("linear",[0x050A24,0x0A1440],[70,50],[0,255],{matrixType:"box",x:LP_TRACK_X,y:LP_TRACK_Y,w:LP_TRACK_W,h:LP_TRACK_H,r:Math.PI / 2});
   lpRoundRect(g,LP_TRACK_X - 1.5,LP_TRACK_Y - 1.5,LP_TRACK_W + 3,LP_TRACK_H + 3,(LP_TRACK_H + 3) / 2);
   g.endFill();

   // The fill, in the free timeline depth between the frame and the (masked) stock bar.
   var fill = p.createEmptyMovieClip("sanctuaryFill",-16382);
   fill.ratio = -1;
   fill.target = Math.max(0,Math.min(1,p.mask_mc._xscale / 100));
   fill.onEnterFrame = lpStep;

   // The rank icon's medallion, under its art in level_mc.
   var lv = p.level_mc;
   var hb = lv.holder.getBounds(lv);
   var cx = (hb.xMin + hb.xMax) / 2;
   var cy = (hb.yMin + hb.yMax) / 2;
   var r = Math.max(hb.xMax - hb.xMin,hb.yMax - hb.yMin) / 2;
   var m = lv.createEmptyMovieClip("sanctuaryMedal",-16384);
   m.lineStyle(2,LP_GOLD_LIGHT,100);
   m.beginGradientFill("radial",[0xFFFFFF,0xDCE7FF,0xA7BFF0],[100,100,100],[0,140,255],{matrixType:"box",x:cx - r * 1.3,y:cy - r * 1.4,w:r * 2.4,h:r * 2.6,r:0});
   lpCircle(m,cx,cy,r);
   m.endFill();
   m.filters = [new flash.filters.DropShadowFilter(2,90,0x050A24,0.6,5,5,1,2)];

   lpStyleText();
}

function lpStyleText()
{
   progress_mc.title_txt.textColor = 0xFFFFFF;
   progress_mc.title_txt.filters = lpOutline();
   progress_mc.level_mc.level_txt.textColor = 0xFFFFFF;
   progress_mc.level_mc.level_txt.filters = [new flash.filters.GlowFilter(0x3A2400,0.95,3,3,6,2)];
}

// Eases the fill toward its target and redraws it.
function lpStep()
{
   var ratio = this.ratio < 0 ? this.target : this.ratio + (this.target - this.ratio) * 0.25;
   if(Math.abs(ratio - this.target) < 0.002)
   {
      ratio = this.target;
   }
   if(ratio == this.ratio && !this.dirty)
   {
      return;
   }
   this.ratio = ratio;
   this.dirty = false;
   this.clear();
   var w = Math.round(LP_TRACK_W * ratio);
   if(w < 3)
   {
      return;
   }
   var h = LP_TRACK_H;
   var r = Math.min(h / 2,w / 2);
   this.lineStyle();
   this.beginGradientFill("linear",[0xFFF3C4,0xFFC531,0xF5A623,0xC97D10],[100,100,100,100],[0,90,170,255],{matrixType:"box",x:LP_TRACK_X,y:LP_TRACK_Y,w:LP_TRACK_W,h:h,r:Math.PI / 2});
   lpRoundRect(this,LP_TRACK_X,LP_TRACK_Y,w,h,r);
   this.endFill();
   if(w > 10)
   {
      lpGloss(this,LP_TRACK_X + 4,LP_TRACK_Y + 2,w - 8,h * 0.42,h * 0.2,60);
   }
   this.filters = this.maxed ? [new flash.filters.GlowFilter(LP_GOLD,0.9,10,10,2,2)] : [];
}

lpBuild();

var lpSetPercent = setPercent;
setPercent = function(percent)
{
   var result = lpSetPercent.apply(_root,arguments);
   if(!isNaN(Number(percent)))
   {
      progress_mc.sanctuaryFill.target = Math.max(0,Math.min(1,Number(percent) / 100));
   }
   return result;
};
var lpSetTitle = setTitle;
setTitle = function()
{
   var result = lpSetTitle.apply(_root,arguments);
   lpStyleText();
   return result;
};
var lpSetLevel = setLevel;
setLevel = function(rank)
{
   var result = lpSetLevel.apply(_root,arguments);
   progress_mc.sanctuaryFill.maxed = rank == 20;
   progress_mc.sanctuaryFill.dirty = true;
   lpStyleText();
   return result;
};
// ===== End Sanctuary Level Progress =====

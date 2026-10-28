// ===== Sanctuary Fishing Distance Meter =====
// Appended to fishingmeters_bbe's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The reel-in
// meter (wndFishingDistanceMeter) as a navy glass capsule with a gold rim and a dark recessed track, the net at the
// end in a frosted medallion with a gold rim, and the stretch between the fish and the net as a glossy gold bar
// (it follows the stock bar, which goes invisible). The fish, hook and escape timer are the stock ones, untouched.
// setDistanceMeter moves everything with gotoAndStop on the main timeline, which runs this frame's script again when
// it lands on frame 1, so everything here is safe to run twice and is made again if a rewind took it away.

var FD_X = 38;
var FD_Y = 52;
var FD_W = 580;
var FD_H = 42;
// The net's medallion (the stock net icon is 64px at 612,43).
var FD_NET_X = 644;
var FD_NET_Y = 75;

function fdEnsure()
{
   if(_root.sanctuaryDistance._parent != undefined)
   {
      return;
   }
   var m = mgUnder(_root,"sanctuaryDistance");
   m.track = m.createEmptyMovieClip("track",1);
   m.fill = m.createEmptyMovieClip("fill",2);
   m.net = m.createEmptyMovieClip("net",3);
   fdDrawTrack(m.track);
   mgSocketCircle(m.net,FD_NET_X,FD_NET_Y,30,MG_GOLD,100,2.5);
   m.net.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];
   m.fill.filters = [new flash.filters.GlowFilter(MG_GOLD,0.45,8,8,2,2)];
   m.onEnterFrame = fdTick;
}

function fdDrawTrack(g)
{
   mgGradientRect(g,FD_X,FD_Y,FD_W,FD_H,FD_H / 2,0x2E4FB0,0x141F55,95,MG_GOLD,100,2.5);
   mgGloss(g,FD_X + 14,FD_Y + 3,FD_W - 28,FD_H * 0.35,8,28);
   // The recess the bar runs in.
   g.lineStyle(1,MG_RIM,30);
   g.beginGradientFill("linear",[0x050A24,0x0A1440],[90,80],[0,255],{matrixType:"box",x:FD_X + 8,y:FD_Y + 9,w:FD_W - 16,h:FD_H - 18,r:Math.PI / 2});
   mgRoundRect(g,FD_X + 8,FD_Y + 9,FD_W - 16,FD_H - 18,(FD_H - 18) / 2);
   g.endFill();
   // Faint quarter marks along the recess.
   var i;
   for(i = 1; i < 4; i++)
   {
      var x = FD_X + 8 + (FD_W - 16) * i / 4;
      g.lineStyle(1,MG_RIM,35);
      g.moveTo(x,FD_Y + 12);
      g.lineTo(x,FD_Y + FD_H - 12);
   }
   g.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];
}

// The gold bar over the stock one's place (it is centred on its x and scaled to its length).
function fdTick()
{
   // The stock tube and bar stay invisible, even if a rewind placed them again.
   topmeter._alpha = 0;
   distanceMeter._alpha = 0;
   var f = this.fill;
   f.clear();
   if(!distanceMeter._visible)
   {
      return;
   }
   var b = distanceMeter.getBounds(_root);
   var x = Math.max(FD_X + 10,b.xMin);
   var right = Math.min(FD_X + FD_W - 10,b.xMax);
   if(right - x < 4)
   {
      return;
   }
   var h = FD_H - 22;
   mgFill(f,x,FD_Y + 11,right - x,h,h / 2,MG_GOLD_LIGHT,MG_GOLD_DEEP);
}

// Lua moves the meter with setDistanceMeter (a gotoAndStop on this timeline).
var fdSetDistanceMeter = setDistanceMeter;
setDistanceMeter = function(distance)
{
   var result = fdSetDistanceMeter.apply(_root,arguments);
   fdEnsure();
   return result;
};

fdEnsure();
// ===== End Sanctuary Fishing Distance Meter =====

// ===== Sanctuary Kick Charge Meter =====
// Appended to scr_charge_meter_bbe's main timeline (after minigames/mg_theme.as) by build_client_mods.py. The soccer
// kick meter (wndSSSChargeMeter): its purple tube (a plain shape, dropped with REMOVED_DEPTHS) becomes a navy glass
// tube with a gold rim and quarter notches, under the stock lightning fill and ball, which are untouched (the unlit
// bolt still shows what's left to charge). A full charge lights the rim with a pulsing gold glow alongside the
// stock burst.

// The tube: a round head around the ball, tapering to a round foot (the stock fill runs from y 205 up to 51).
var CHM_CX = 38;
var CHM_HEAD_R = 22;
var CHM_HEAD_Y = 36;
var CHM_FOOT_R = 14;
var CHM_FOOT_Y = 196;
var CHM_FILL_TOP = 51;
var CHM_FILL_BOTTOM = 205;

var chmTube;
var chmFull = false;

function chmOutline(g)
{
   var x = CHM_CX;
   var hr = CHM_HEAD_R;
   var fr = CHM_FOOT_R;
   g.moveTo(x - hr,CHM_HEAD_Y);
   g.curveTo(x - hr,CHM_HEAD_Y - hr,x,CHM_HEAD_Y - hr);
   g.curveTo(x + hr,CHM_HEAD_Y - hr,x + hr,CHM_HEAD_Y);
   g.lineTo(x + fr,CHM_FOOT_Y);
   g.curveTo(x + fr,CHM_FOOT_Y + fr,x,CHM_FOOT_Y + fr);
   g.curveTo(x - fr,CHM_FOOT_Y + fr,x - fr,CHM_FOOT_Y);
   g.lineTo(x - hr,CHM_HEAD_Y);
}

function chmBuild()
{
   chmTube = mgUnder(_root,"sanctuaryChargeMeter");
   var g = chmTube.createEmptyMovieClip("glass",1);
   g.lineStyle(2.5,MG_GOLD,100);
   g.beginGradientFill("linear",[0x141F55,0x2E4FB0,0x141F55],[95,95,95],[0,110,255],{matrixType:"box",x:CHM_CX - CHM_HEAD_R,y:0,w:CHM_HEAD_R * 2,h:10,r:0});
   chmOutline(g);
   g.endFill();
   // A shine down the left side and a dark recess for the bolt.
   g.lineStyle();
   g.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[35,0],[0,255],{matrixType:"box",x:CHM_CX - CHM_HEAD_R + 3,y:0,w:10,h:10,r:0});
   g.moveTo(CHM_CX - CHM_HEAD_R + 4,CHM_HEAD_Y + 4);
   g.lineTo(CHM_CX - CHM_HEAD_R + 12,CHM_HEAD_Y + 4);
   g.lineTo(CHM_CX - CHM_FOOT_R + 7,CHM_FOOT_Y - 4);
   g.lineTo(CHM_CX - CHM_FOOT_R + 3,CHM_FOOT_Y - 4);
   g.lineTo(CHM_CX - CHM_HEAD_R + 4,CHM_HEAD_Y + 4);
   g.endFill();
   g.beginFill(MG_NAVY,45);
   mgRoundRect(g,CHM_CX - 12,CHM_FILL_TOP - 4,24,CHM_FILL_BOTTOM - CHM_FILL_TOP + 6,10);
   g.endFill();
   // Notches at a quarter, half and three quarters.
   var i;
   for(i = 1; i <= 3; i++)
   {
      var y = CHM_FILL_BOTTOM - (CHM_FILL_BOTTOM - CHM_FILL_TOP) * i / 4;
      var t = (y - CHM_HEAD_Y) / (CHM_FOOT_Y - CHM_HEAD_Y);
      var edge = CHM_CX + CHM_HEAD_R + (CHM_FOOT_R - CHM_HEAD_R) * t;
      g.lineStyle(2,MG_GOLD_LIGHT,i == 2 ? 100 : 70);
      g.moveTo(edge - 1,y);
      g.lineTo(edge - (i == 2 ? 8 : 5),y);
   }
   chmTube.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2)];
   chmTube.onEnterFrame = chmGlow;
}

// A full charge breathes a gold glow around the tube.
function chmGlow()
{
   var shadow = new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.6,8,8,1,2);
   if(!chmFull)
   {
      if(this.glowing)
      {
         this.filters = [shadow];
         this.glowing = false;
      }
      return;
   }
   this.glowing = true;
   var k = (Math.sin(getTimer() / 120) + 1) / 2;
   this.filters = [shadow,new flash.filters.GlowFilter(MG_GOLD,0.5 + 0.4 * k,8 + 12 * k,8 + 12 * k,2,2)];
}

// Lua sends the charge (0..100) with setMeter.
var chmSetMeter = setMeter;
setMeter = function(_percent)
{
   var result = chmSetMeter.apply(_root,arguments);
   if(_percent >= 0 && _percent < 101)
   {
      chmFull = _percent == 100;
   }
   return result;
};

chmBuild();
// ===== End Sanctuary Kick Charge Meter =====

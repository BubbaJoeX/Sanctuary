// ===== Sanctuary Minigame Countdown =====
// Appended (after minigames/mg_theme.as) to the empty main timeline script FRAME_SCRIPTS gives countdown3.
// The "Get Ready ... GO!" countdown (wndCountdown) is one long timeline animation of unnamed clips: the traffic
// light and its lamps stay as they are (red, red, red, green reads at a glance), and the words are restyled every
// time the timeline places them: "Get Ready" in gold and "GO!" in bright green, both with a heavy navy outline and a
// glow, over a navy glass banner with a gold rim that animates with them. The words are the client's own strings.

// Root depths the animation places its clips at (the words are at 1 and 4).
var CD3_DEPTHS = [1,4];

function cd3Build()
{
   if(_root.cd3Watch != undefined)
   {
      return;
   }
   // The lowest free dynamic depth: timeline rewinds don't clear it, and it draws nothing (mgFreeDepth: a bounded
   // search, since the client's getInstanceAtDepth can report every depth taken).
   var d = mgFreeDepth(_root,0,1);
   var w = _root.createEmptyMovieClip("cd3Watch",d != undefined ? d : _root.getNextHighestDepth());
   w.onEnterFrame = cd3Watch;
}

// Every frame: a newly placed word clip gets styled once.
function cd3Watch()
{
   var i;
   for(i = 0; i < CD3_DEPTHS.length; i++)
   {
      var c = _root.getInstanceAtDepth(CD3_DEPTHS[i] - 16384);
      if(c != undefined && c.cd3Done != true)
      {
         if(c.getReady1 != undefined)
         {
            cd3Style(c,c.getReady1,c.getReady2,MG_GOLD_LIGHT,MG_GOLD);
         }
         else if(c.go1 != undefined)
         {
            cd3Style(c,c.go1,c.go2,0x9CF57A,0x5BE04A);
         }
      }
   }
}

// face: the word, shadow: its offset copy beneath, glow: the halo's colour.
function cd3Style(c, face, shadow, color, glow)
{
   c.cd3Done = true;
   face.textColor = color;
   face.filters = [new flash.filters.GlowFilter(0x0A1030,1,5,5,10,2),new flash.filters.GlowFilter(glow,0.6,20,20,2,2)];
   shadow.textColor = 0x0A1030;
   shadow.filters = [new flash.filters.GlowFilter(0x0A1030,0.8,4,4,4,2)];
   // The banner, centred on the two fields (they're centre-aligned) and as wide as the word.
   var b = c.getBounds(c);
   var w = Math.max(160,face.textWidth + 80);
   var h = 92;
   var x = (b.xMin + b.xMax) / 2 - w / 2;
   var y = (b.yMin + b.yMax) / 2 - h / 2;
   var p = mgUnder(c,"cd3Plate");
   mgGradientRect(p,x,y,w,h,h / 2,0x2E4FB0,0x141F55,88,MG_GOLD,100,3);
   mgGloss(p,x + 16,y + 4,w - 32,h * 0.4,18,30);
   p.filters = [new flash.filters.DropShadowFilter(5,90,MG_SHADOW,0.6,14,14,1,2)];
}

cd3Build();
// ===== End Sanctuary Minigame Countdown =====

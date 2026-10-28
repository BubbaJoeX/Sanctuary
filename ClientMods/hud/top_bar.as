// ===== Sanctuary Top Bar =====
// Appended to FRUtilNavigation's main timeline (an empty frame script added at build time, FRAME_SCRIPTS) by
// build_client_mods.py. The top utility bar loses its screenshot and Ask for Help buttons (the video recorder,
// which sat in the gap after them, is the separate video_new movie, hidden by hud/no_video_capture.as); the
// buttons after them close up and the row is re-centred where the full bar was (its window is laid out for the
// stock width), and the bar's backing (a root shape, dropped at build time with REMOVED_DEPTHS)
// is redrawn as a navy glass pill around what's left.

var tbNav = _root.FRUtilNavigation;

function tbRoundRect(mc, x, y, w, h, r)
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

// The union of the clips' bounds on the stage.
function tbBounds(clips)
{
   var b = clips[0].getBounds(_root);
   var i;
   for(i = 1; i < clips.length; i++)
   {
      var o = clips[i].getBounds(_root);
      b.xMin = Math.min(b.xMin,o.xMin);
      b.yMin = Math.min(b.yMin,o.yMin);
      b.xMax = Math.max(b.xMax,o.xMax);
      b.yMax = Math.max(b.yMax,o.yMax);
   }
   return b;
}

function tbLayout()
{
   var stock = tbBounds([tbNav.holder,tbNav.cameraButton,tbNav.friendsButton,tbNav.welcomeButton,tbNav.atlasButton,tbNav.minmax,tbNav.settingsButton,tbNav.helpButton,tbNav.exitButton]);

   // The buttons right of the screenshot button move into its slot (and over the recorder's), and Exit takes
   // Ask for Help's place.
   var shift = tbNav.friendsButton._x - tbNav.cameraButton._x;
   var exitAfterSettings = tbNav.helpButton._x - tbNav.settingsButton._x;
   var moved = [tbNav.friendsButton,tbNav.welcomeButton,tbNav.atlasButton,tbNav.minmax,tbNav.settingsButton];
   var i;
   for(i = 0; i < moved.length; i++)
   {
      moved[i]._x -= shift;
   }
   tbNav.exitButton._x = tbNav.settingsButton._x + exitAfterSettings;
   tbNav.cameraButton._visible = false;
   tbNav.helpButton._visible = false;

   var shown = [tbNav.holder,tbNav.friendsButton,tbNav.welcomeButton,tbNav.atlasButton,tbNav.minmax,tbNav.settingsButton,tbNav.exitButton];
   var b = tbBounds(shown);
   var centre = (stock.xMin + stock.xMax - b.xMin - b.xMax) / 2;
   tbNav._x += centre;
   b.xMin += centre;
   b.xMax += centre;
   var x = b.xMin - 8;
   var y = b.yMin - 4;
   var w = b.xMax - b.xMin + 16;
   var h = b.yMax - b.yMin + 8;
   var bar = _root.createEmptyMovieClip("sanctuaryBar",-16384);
   bar.lineStyle(2,0x9DB8FF,80);
   bar.beginGradientFill("linear",[0x2E4FB0,0x141F55],[100,100],[0,255],{matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2});
   tbRoundRect(bar,x,y,w,h,h / 2);
   bar.endFill();
   bar.lineStyle();
   bar.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF],[22,0],[0,255],{matrixType:"box",x:x,y:y + 2,w:w,h:h / 2,r:Math.PI / 2});
   tbRoundRect(bar,x + 4,y + 2,w - 8,h / 2 - 2,(h / 2 - 2) / 2);
   bar.endFill();
   bar.filters = [new flash.filters.DropShadowFilter(3,90,0x050A24,0.5,8,8,1,2)];
}

tbLayout();
// ===== End Sanctuary Top Bar =====

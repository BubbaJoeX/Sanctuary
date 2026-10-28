// ===== Sanctuary Match Popups =====
// Appended to SSS_popups' main timeline (after minigames/mg_theme.as) by build_client_mods.py. The banner soccer and
// Pirates show between rounds ("Waiting for remaining players", "Congratulations Red team!"; wndSSSPopups and
// wndPiratesPopups): its pale glass pill becomes the Main Menu's navy frame with header sparkles and a gold hairline,
// the message is white with the dark outline, and the beige exit button is the gold close circle on the banner's
// corner. The banner is drawn over the stock pill inside the Background clip, so it shows and hides with it; the
// message is mirrored from the stock text field (hidden, still set by the stock code) every frame.

var POP_X = -168;
var POP_Y = -52;
var POP_W = 337;
var POP_H = 105;
var POP_TEXT_SIZE = 22;

var popBanner;

function popBuild()
{
   var b = popBanner = Background.createEmptyMovieClip("sanctuaryBanner",Background.getNextHighestDepth());
   var bg = b.createEmptyMovieClip("bg",1);
   mgFrame(bg,POP_X,POP_Y,POP_W,POP_H,30,0,7373);
   mgSparkles(bg,7373,POP_X + 30,POP_Y + 8,POP_W - 60,16,12);
   mgGloss(bg,POP_X + 16,POP_Y + 3,POP_W - 32,POP_H * 0.4,22,22);
   bg.lineStyle();
   bg.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:POP_X + 40,y:0,w:POP_W - 80,h:2,r:0});
   mgRoundRect(bg,POP_X + 40,POP_Y + POP_H - 12,POP_W - 80,2,1);
   bg.endFill();

   var t = b.message_txt = mgText(b,"message_txt","",POP_X + 20,POP_Y + 12,POP_W - 40,POP_H - 26,POP_TEXT_SIZE,0xFFFFFF);
   t.multiline = true;
   t.wordWrap = true;
   t.filters = mgOutline();
   Background.Text._alpha = 0;
   b.onEnterFrame = popMirror;
   popMirror.call(b);

   // The exit button (a stock FRButton, its art 77x72 before its half scale) moves onto the banner's top right
   // corner, under a gold close circle.
   exitBtn._x = 241 + POP_X + POP_W - 7 - 18.9;
   exitBtn._y = 100 + POP_Y + 7 - 18;
   mgSkin(exitBtn,"close","",{x:0,y:0,w:77,h:72});
}

// Mirrors the stock message, shrinking it until it fits and centering it vertically.
function popMirror()
{
   var src = Background.Text.text;
   if(src == this.last)
   {
      return;
   }
   this.last = src;
   var t = this.message_txt;
   var f = t.getTextFormat();
   f.size = POP_TEXT_SIZE;
   t.setNewTextFormat(f);
   t.text = src;
   t.setTextFormat(f);
   while((t.textHeight > POP_H - 30 || t.textWidth > POP_W - 46) && f.size > 12)
   {
      f.size -= 1;
      t.setTextFormat(f);
   }
   t._y = POP_Y + POP_H / 2 - t.textHeight / 2 - 4;
}

popBuild();
// ===== End Sanctuary Match Popups =====

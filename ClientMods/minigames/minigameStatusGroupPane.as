// ===== Sanctuary Minigame Group Pane =====
// Appended to minigameStatusGroupPane's main timeline (after minigames/mg_theme.as) by build_client_mods.py.
// The left column of the minigame launch screen (wndMinigameStatusGroupPane): you, and the group members joining the
// game. The stock blue panel, member plates and divider lines are client images loaded at runtime, so they're made
// invisible and redrawn in the Main Menu's style: a navy glass frame whose sparkling header holds your portrait in a
// gold-rimmed frosted socket over your name, and a glass card per member with the portrait in a frosted socket. The
// portraits are cropped to rounded corners. Names and lives counts get the dark outline. Everything stock keeps its
// role: the cards are drawn inside the member clips, so they show, hide and fade (joined/left) with them.

var MSG_W = 270;
var MSG_H = 532;
var MSG_HEADER_H = 126;
// A member card in its member clip's coordinates, and the portrait's icon area inside it.
var MSG_CARD = {x:-8,y:2,w:234,h:88};
var MSG_FACE = {x:1.2,y:13.7,w:46.2,h:47.4};
// Your portrait's icon area on the stage.
var MSG_MY_FACE = {x:92,y:29.5,w:59,h:59};

function msgBuild()
{
   var f = mgUnder(_root,"sanctuaryGroupPane");
   var bg = f.createEmptyMovieClip("bg",1);
   mgFrame(bg,4,4,MSG_W - 8,MSG_H - 8,28,MSG_HEADER_H,6161);
   // A gold rule under the header.
   bg.lineStyle();
   bg.beginGradientFill("linear",[MG_GOLD,MG_GOLD,MG_GOLD],[0,90,0],[0,128,255],{matrixType:"box",x:30,y:0,w:MSG_W - 60,h:2,r:0});
   mgRoundRect(bg,30,MSG_HEADER_H + 4,MSG_W - 60,2,1);
   bg.endFill();

   // Your portrait in a gold-rimmed frosted socket, with a soft gold halo.
   var s = f.createEmptyMovieClip("socket",2);
   var r = MSG_MY_FACE;
   s.lineStyle();
   s.beginGradientFill("radial",[MG_GOLD,MG_GOLD],[45,0],[0,255],{matrixType:"box",x:r.x - 30,y:r.y - 30,w:r.w + 60,h:r.h + 60,r:0});
   mgRoundRect(s,r.x - 30,r.y - 30,r.w + 60,r.h + 60,40);
   s.endFill();
   mgSocket(s,r.x - 4,r.y - 4,r.w + 8,r.h + 8,16,MG_GOLD,100,3);
   msgFace(_root,portrait,r,"myFaceMask");

   mgHold(_root.bg);
   mgHold(divider1);
   mgHold(divider2);
   mgHold(divider3);
   mgLight(playerName,0xFFFFFF);
   mgLight(lives.count,0xFFFFFF);

   var i;
   for(i = 1; i <= 4; i++)
   {
      msgCard(_root["group" + i]);
   }
}

// Hides a portrait's stock frame and crops its picture to a rounded square (rect is in parent's coordinates).
function msgFace(parent, p, rect, maskName)
{
   mgHold(p.char_icon_bg);
   var m = mgUnder(parent,maskName);
   m.beginFill(0,100);
   mgRoundRect(m,rect.x,rect.y,rect.w,rect.h,12);
   m.endFill();
   p.icon.setMask(m);
}

// A member's glass card, drawn inside the member clip under its stock parts.
function msgCard(g)
{
   if(g == undefined)
   {
      return;
   }
   mgHold(g.bg);
   var c = mgUnder(g,"sanctuaryCard");
   var r = MSG_CARD;
   mgGradientRect(c,r.x,r.y,r.w,r.h,18,0x284A9E,0x162660,100,MG_RIM,45,2);
   mgGloss(c,r.x + 8,r.y + 2,r.w - 16,r.h * 0.4,14,22);
   // The job badge's recess on the right.
   mgPanel(c,52,5,118,58,16,35,20);
   var face = MSG_FACE;
   mgSocket(c,face.x - 3,face.y - 3,face.w + 6,face.h + 6,13,MG_RIM,90,2);
   c.filters = [new flash.filters.DropShadowFilter(3,90,MG_SHADOW,0.5,8,8,1,2)];
   msgFace(g,g.portrait,face,"faceMask");
   mgLight(g.name,0xFFFFFF);
   mgLight(g.lives.count,0xFFFFFF);
}

msgBuild();
// ===== End Sanctuary Minigame Group Pane =====

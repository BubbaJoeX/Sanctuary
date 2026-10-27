// ===== Sanctuary home screen =====
// Appended to welcome's main timeline by build_client_mods.py, after no_station_cash.as.
// Widens the welcome screen (install_client_mods.py makes its window 1366 wide) into a home screen:
//  - a new frame, with the classic logo, an Open Source sticker, Update Notes and close,
//  - What's New: news the server sends as tagged announcements (WelcomeScreenHelper, Resources/WhatsNew.json),
//    in a carousel that replaces the stock one (stock image ads still show, as slides),
//  - Referee Picks: cards the server sends the same way (referees edit them with !pick),
//  - Friends Online, which Lua already feeds through addFriend; a click teleports to the friend,
//  - quick links (My Stuff, Houses, Hero's Journal, Achievements), rotating tips and a big Play button.
// The wallet, Chatty, trading card game, daily activity and game code panels are gone (codes go through !redeem).
// Icons are client textures build_client_mods.py embeds as sanctuaryIcon* symbols (TEXTURE_SYMBOLS), and the
// Play label is drawn in the client's Free Realms font at build time (sanctuaryPlayLabel, TEXT_SYMBOLS).

Stage.scaleMode = "noScale";
Stage.align = "TL";

var HOME_W = 1366;
var HOME_H = 768;
// The browser clip sits 8px down the stage; home clips undo that so they draw in stage coordinates.
var HOME_BROWSER_Y = 8;
var HOME_TOP = 132;
var HOME_BOTTOM = 684;
var HOME_PICK_TAG = "[[SANCTUARY_PICK]]";
var HOME_NEWS_TAG = "[[SANCTUARY_NEWS]]";
var HOME_NOTE_TAG = "[[SANCTUARY_NOTE]]";
var HOME_NEWS_SECONDS = 7;
var HOME_FRIENDS_PER_PAGE = 5;

var HOME_WHITE = 0xFFFFFF;
var HOME_SOFT = 0xC9D6FF;
var HOME_GOLD = 0xFFD54A;
var HOME_PURPLE_SOFT = 0xD9CCFF;
var HOME_GREEN = 0x7CE046;

// Pick / link icon names -> embedded symbols.
var HOME_ICONS = {house:"sanctuaryIconHouse", bag:"sanctuaryIconBag", journal:"sanctuaryIconJournal",
   trophy:"sanctuaryIconTrophy", book:"sanctuaryIconBook", paw:"sanctuaryIconPaw", coin:"sanctuaryIconCoin",
   friends:"sanctuaryIconFriends", tree:"sanctuaryIconTree", referee:"sanctuaryIconRefereeTree",
   wheel:"sanctuaryIconWheel", scroll:"sanctuaryIconScroll"};

var HOME_MONTHS = ["January","February","March","April","May","June","July","August","September","October",
   "November","December"];

var HOME_TIPS = [
   "Click a friend under Friends Online to teleport right to them.",
   "Have a code? Press Enter and type !redeem, or use the Redeem Code button on the dock.",
   "The referees change their picks often. Check back for new places to go!",
   "Earn coins playing, then spend them on outfits, mounts and pets in the Shop.",
   "Open the Main Menu from your portrait on the dock to find everything else.",
   "Open Source Free Realms is made by fans, for fans. Thanks for playing!"
];

var homePicks = [];
var homeNews = [];
var homeNewsIndex = 0;
var homeFriends = [];
var homeFriendsPage = 0;
var homeNotes = [];

// --- drawing helpers ---------------------------------------------------------------

function homeFormat(size, color, align)
{
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.align = align == undefined ? "left" : align;
   return f;
}

function homeText(mc, name, txt, x, y, w, h, size, color, align, wrap)
{
   mc.createTextField(name,mc.getNextHighestDepth(),x,y,w,h);
   var t = mc[name];
   t.embedFonts = true;
   t.selectable = false;
   t.wordWrap = wrap == true;
   t.multiline = wrap == true;
   t.setNewTextFormat(homeFormat(size,color,align));
   t.text = txt;
   return t;
}

function homeRoundRect(mc, x, y, w, h, r)
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

function homeGradientRect(mc, x, y, w, h, r, top, bottom, alpha, rim, rimAlpha, rimWidth)
{
   if(rim != undefined)
   {
      mc.lineStyle(rimWidth == undefined ? 2 : rimWidth,rim,rimAlpha == undefined ? 100 : rimAlpha);
   }
   else
   {
      mc.lineStyle();
   }
   var matrix = {matrixType:"box",x:x,y:y,w:w,h:h,r:Math.PI / 2};
   mc.beginGradientFill("linear",[top,bottom],[alpha,alpha],[0,255],matrix);
   homeRoundRect(mc,x,y,w,h,r);
   mc.endFill();
}

function homeShadow(mc, distance, blur, alpha)
{
   mc.filters = [new flash.filters.DropShadowFilter(distance,90,0x050A24,alpha / 100,blur,blur,1,2)];
}

function homeGlow(mc, color, blur, strength)
{
   mc.filters = [new flash.filters.GlowFilter(color,0.8,blur,blur,strength,2)];
}

// An embedded icon symbol (by name) or a client image id, fitted into size x size.
function homeIcon(parent, name, icon, x, y, size)
{
   var holder;
   if(HOME_ICONS[icon] != undefined)
   {
      holder = parent.attachMovie(HOME_ICONS[icon],name,parent.getNextHighestDepth());
      holder._width = holder._height = size;
   }
   else
   {
      holder = parent.createEmptyMovieClip(name,parent.getNextHighestDepth());
      var image = holder.createEmptyMovieClip("image",1);
      // The client's loader sizes an image to its holder's original bounds, so give it the icon's size.
      image.beginFill(0,0);
      homeRoundRect(image,0,0,size,size,0);
      image.endFill();
      if(Number(icon) > 0)
      {
         _root.frSetIconByIndex(image,Number(icon));
      }
   }
   holder._x = x;
   holder._y = y;
   holder.fullScale = holder._xscale;
   return holder;
}

function homeSend(command)
{
   fscommand("FR_event","PlaySound(DockSelect)");
   fscommand("FR_event",command);
}

// kind: "green" (Play), "gold" (actions), "purple" (picks), "blue" (secondary).
var HOME_BUTTON_COLORS = {green:[0x8BE04E,0x3F9B17,0xD9FFB0],gold:[0xFFE066,0xE79A13,0xFFF4C2],
   purple:[0xA98BFF,0x6440D6,0xE6DCFF],blue:[0x5E8CF0,0x2B4FB0,0xBFD3FF]};

function homeButton(parent, name, label, x, y, w, h, kind, size, onClick)
{
   var b = parent.createEmptyMovieClip(name,parent.getNextHighestDepth());
   b._x = x;
   b._y = y;
   b.w = w;
   b.h = h;
   b.colors = HOME_BUTTON_COLORS[kind];
   b.face = b.createEmptyMovieClip("face",1);
   b.draw = function(over)
   {
      this.face.clear();
      var c = this.colors;
      homeGradientRect(this.face,0,0,this.w,this.h,this.h / 2,over ? c[2] : c[0],c[1],100,c[2],over ? 100 : 70,2);
      // Gloss on the top half.
      this.face.lineStyle();
      this.face.beginFill(0xFFFFFF,over ? 30 : 20);
      homeRoundRect(this.face,4,3,this.w - 8,this.h / 2 - 3,(this.h / 2 - 3) / 2);
      this.face.endFill();
   };
   b.draw(false);
   var t = homeText(b,"label_txt",label,0,Math.round((h - size) / 2) - 4,w,size + 10,size,HOME_WHITE,"center",false);
   t.filters = [new flash.filters.DropShadowFilter(1,90,0x10183A,0.8,2,2,1,1)];
   homeShadow(b,3,6,45);
   b.onRollOver = function()
   {
      this.draw(true);
      fscommand("FR_event","PlaySound(DockOver)");
      gs.TweenLite.to(this,0.25,{_xscale:105,_yscale:105,_x:this.baseX - this.w * 0.025,_y:this.baseY - this.h * 0.025});
   };
   b.onRollOut = b.onReleaseOutside = function()
   {
      this.draw(false);
      gs.TweenLite.to(this,0.25,{_xscale:100,_yscale:100,_x:this.baseX,_y:this.baseY});
   };
   b.baseX = x;
   b.baseY = y;
   b.onRelease = onClick;
   return b;
}

// --- frame ----------------------------------------------------------------------------

// Four-point sparkles scattered over (left, top, width, height).
function homeStars(mc, count, seed, left, top, width, height)
{
   // A small LCG so the sparkle pattern is the same every time.
   var s = seed;
   var i;
   for(i = 0; i < count; i++)
   {
      s = (s * 1103515245 + 12345) % 2147483648;
      var x = left + s % width;
      s = (s * 1103515245 + 12345) % 2147483648;
      var y = top + s % height;
      s = (s * 1103515245 + 12345) % 2147483648;
      var r = 1 + s % 3;
      mc.lineStyle();
      mc.beginFill(0xFFFFFF,25 + s % 50);
      mc.moveTo(x,y - r * 2);
      mc.curveTo(x,y,x + r * 2,y);
      mc.curveTo(x,y,x,y + r * 2);
      mc.curveTo(x,y,x - r * 2,y);
      mc.curveTo(x,y,x,y - r * 2);
      mc.endFill();
   }
}

function homeDrawFrame()
{
   var bg = browser.createEmptyMovieClip("sanctuaryHomeBg",browser.getNextHighestDepth());
   bg._y = - HOME_BROWSER_Y;
   // Underneath everything else in the browser, where the old frame was.
   bg.swapDepths(browser.bg);

   homeGradientRect(bg,6,6,HOME_W - 12,HOME_H - 12,28,0x2E4FB0,0x141F55,100,0x9DB8FF,80,3);
   // Soft light from the top left.
   var light = bg.createEmptyMovieClip("light",1);
   light.beginGradientFill("radial",[0x8FB4FF,0x8FB4FF],[35,0],[0,255],{matrixType:"box",x:-200,y:-320,w:1100,h:760,r:0});
   homeRoundRect(light,8,8,HOME_W - 16,HOME_H - 16,26);
   light.endFill();
   // Header band and the bottom bar.
   bg.lineStyle();
   bg.beginFill(0x0A1440,30);
   homeRoundRect(bg,9,9,HOME_W - 18,HOME_TOP - 14,24);
   bg.endFill();
   bg.lineStyle(1,0xBFD3FF,25);
   bg.moveTo(30,HOME_TOP - 6);
   bg.lineTo(HOME_W - 30,HOME_TOP - 6);
   bg.lineStyle();
   bg.beginFill(0x0A1440,35);
   homeRoundRect(bg,18,HOME_BOTTOM + 8,HOME_W - 36,HOME_H - HOME_BOTTOM - 22,20);
   bg.endFill();
   homeStars(bg.createEmptyMovieClip("stars",2),46,1337,30,20,HOME_W - 60,110);
   homeShadow(bg,6,18,60);
}

function homeDrawHeader()
{
   var home = browser.sanctuaryHome;

   // The classic logo (a shape in the browser clip) stays put; an Open Source sticker hangs off it.
   var sticker = home.createEmptyMovieClip("sticker",home.getNextHighestDepth());
   homeGradientRect(sticker,-62,-15,124,30,15,0xB48CFF,0x6A3FD8,100,0xFFFFFF,90,2);
   homeText(sticker,"txt","OPEN SOURCE",-62,-10,124,24,13,HOME_WHITE,"center",false);
   sticker._x = 330;
   sticker._y = 112;
   sticker._rotation = -7;
   homeShadow(sticker,3,6,60);
   sticker._xscale = sticker._yscale = 10;
   gs.TweenLite.to(sticker,0.9,{_xscale:100,_yscale:100,delay:0.3,ease:mx.transitions.easing.Elastic.easeOut});

   // No wallet: the coin amount and Shop button go (the Shop is a quick pick, What's New and the dock away).
   browser.sc_amount_txt._visible = false;

   // Patch Notes (the server's latest commits) instead of the stock Update Notes button.
   releaseNotes_mc._visible = false;
   var notes = home.createEmptyMovieClip("notesButton",home.getNextHighestDepth());
   notes._x = 1010;
   notes._y = 36;
   notes.bg = notes.createEmptyMovieClip("bg",1);
   notes.draw = function(over)
   {
      this.bg.clear();
      this.bg.lineStyle(1,over ? 0xFFF4C2 : 0x9DB8FF,over ? 90 : 40);
      this.bg.beginFill(over ? 0x3557C0 : 0x0A1440,over ? 80 : 45);
      homeRoundRect(this.bg,0,0,246,62,31);
      this.bg.endFill();
   };
   notes.draw(false);
   homeIcon(notes,"icon","scroll",8,5,52);
   homeText(notes,"title_txt","Patch Notes",66,8,170,26,19,HOME_WHITE,"left",false);
   notes.date_txt = homeText(notes,"date_txt","",67,33,170,20,12,HOME_SOFT,"left",false);
   notes.onRollOver = function()
   {
      this.draw(true);
      fscommand("FR_event","PlaySound(DockOver)");
      gs.TweenLite.to(this.icon,0.6,{_rotation:-8,ease:mx.transitions.easing.Elastic.easeOut});
   };
   notes.onRollOut = notes.onReleaseOutside = function()
   {
      this.draw(false);
      gs.TweenLite.to(this.icon,0.6,{_rotation:0,ease:mx.transitions.easing.Elastic.easeOut});
   };
   notes.onRelease = function()
   {
      fscommand("FR_event","PlaySound(DockSelect)");
      homeOpenNotes();
   };
   homeUpdateNotesButton();

   browser.donebtn._x = 1296;
   browser.donebtn._y = 18 - HOME_BROWSER_Y;
   browser.donebtn.swapDepths(browser.getNextHighestDepth());
}

// --- panels ---------------------------------------------------------------------------

function homePanel(name, x, y, w, h, top, bottom, rim, title, subtitle, icon)
{
   var home = browser.sanctuaryHome;
   var p = home.createEmptyMovieClip(name,home.getNextHighestDepth());
   p._x = x;
   p._y = y;
   homeGradientRect(p,0,0,w,h,20,top,bottom,100,rim,70,2);
   homeShadow(p,4,10,50);
   homeIcon(p,"icon",icon,14,10,48);
   var t = homeText(p,"title_txt",title,68,12,w - 80,30,22,HOME_WHITE,"left",false);
   t.filters = [new flash.filters.DropShadowFilter(2,90,0x0A1030,0.9,3,3,1,1)];
   homeText(p,"subtitle_txt",subtitle,69,38,w - 80,20,12,HOME_SOFT,"left",false);
   p.content = p.createEmptyMovieClip("content",p.getNextHighestDepth());
   p.content._y = 72;
   p.w = w;
   p.h = h;
   // Slide up into place.
   p._alpha = 0;
   p._y = y + 30;
   gs.TweenLite.to(p,0.6,{_alpha:100,_y:y,delay:0.1 + x / 4000,ease:mx.transitions.easing.Strong.easeOut});
   return p;
}

// --- What's New -----------------------------------------------------------------------

function homeDot(mc, x, y, r, color, alpha)
{
   mc.beginFill(color,alpha);
   mc.moveTo(x + r,y);
   mc.curveTo(x + r,y + r,x,y + r);
   mc.curveTo(x - r,y + r,x - r,y);
   mc.curveTo(x - r,y - r,x,y - r);
   mc.curveTo(x + r,y - r,x + r,y);
   mc.endFill();
}

function homeDrawNews()
{
   var panel = browser.sanctuaryHome.news;
   panel.content.removeMovieClip();
   var content = panel.content = panel.createEmptyMovieClip("content",panel.getNextHighestDepth());
   content._x = 14;
   content._y = 72;
   content.w = panel.w - 28;
   content.h = panel.h - 86;
   clearInterval(_root.homeNewsInterval);

   if(homeNews.length == 0)
   {
      homeText(content,"empty_txt","No news yet. Check back soon!",0,180,content.w,40,18,HOME_SOFT,"center",false);
      return;
   }

   homeNewsIndex = homeNewsIndex % homeNews.length;
   homeShowNews(0);

   if(homeNews.length > 1)
   {
      var arrows = [["prev",4,-1,"<"],["next",content.w - 48,1,">"]];
      var i;
      for(i = 0; i < arrows.length; i++)
      {
         var b = homeButton(content,arrows[i][0],arrows[i][3],arrows[i][1],88,44,44,"blue",20,function()
         {
            homeStepNews(this.step);
         });
         b.step = arrows[i][2];
      }
      _root.homeNewsInterval = setInterval(homeStepNews,HOME_NEWS_SECONDS * 1000,1);
   }
}

function homeStepNews(step)
{
   homeNewsIndex = (homeNewsIndex + step + homeNews.length) % homeNews.length;
   homeShowNews(step);
   // A manual step restarts the countdown.
   clearInterval(_root.homeNewsInterval);
   _root.homeNewsInterval = setInterval(homeStepNews,HOME_NEWS_SECONDS * 1000,1);
}

function homeShowNews(direction)
{
   var content = browser.sanctuaryHome.news.content;
   var w = content.w;
   var item = homeNews[homeNewsIndex];

   var old = content.slide;
   if(old != undefined)
   {
      old._name = "oldSlide" + content.getNextHighestDepth();
      gs.TweenLite.to(old,0.35,{_x:- direction * 80,_alpha:0,ease:mx.transitions.easing.Strong.easeIn,onComplete:function()
      {
         old.removeMovieClip();
      }});
   }

   var slide = content.slide = content.createEmptyMovieClip("slide",content.getNextHighestDepth());

   // Art on a soft halo, with a NEW sticker.
   var halo = slide.createEmptyMovieClip("halo",slide.getNextHighestDepth());
   halo.beginGradientFill("radial",[0xBFE0FF,0x5E8CF0,0x5E8CF0],[70,25,0],[0,150,255],{matrixType:"box",x:w / 2 - 110,y:0,w:220,h:220,r:0});
   halo.moveTo(w / 2 - 110,0);
   halo.lineTo(w / 2 + 110,0);
   halo.lineTo(w / 2 + 110,220);
   halo.lineTo(w / 2 - 110,220);
   halo.lineTo(w / 2 - 110,0);
   halo.endFill();
   homeStars(slide.createEmptyMovieClip("stars",slide.getNextHighestDepth()),12,1000 + homeNewsIndex * 77,w / 2 - 150,10,300,190);
   var art = homeIcon(slide,"art",item.icon,w / 2 - 80,30,160);
   art._xscale = art._yscale = 10;
   gs.TweenLite.to(art,0.8,{_xscale:art.fullScale,_yscale:art.fullScale,delay:0.15,ease:mx.transitions.easing.Elastic.easeOut});
   var sticker = slide.createEmptyMovieClip("sticker",slide.getNextHighestDepth());
   homeGradientRect(sticker,-34,-15,68,30,15,0xFFE066,0xE79A13,100,0xFFFFFF,100,2);
   homeText(sticker,"txt","NEW!",-34,-10,68,24,15,0x6A2C00,"center",false);
   sticker._x = w / 2 + 92;
   sticker._y = 46;
   sticker._rotation = 12;
   homeShadow(sticker,2,4,60);

   var title = homeText(slide,"title_txt",item.title,30,200,w - 60,36,26,HOME_WHITE,"center",false);
   title.filters = [new flash.filters.DropShadowFilter(2,90,0x0A1030,0.9,3,3,1,1)];
   var text = homeText(slide,"text_txt","",50,244,w - 100,120,16,HOME_SOFT,"center",true);
   if(item.html)
   {
      text.html = true;
      text.htmlText = item.text;
   }
   else
   {
      text.text = item.text;
   }

   if(item.button.length > 0 && item.type.length > 0)
   {
      var go = homeButton(slide,"go",item.button,w / 2 - 100,372,200,40,"gold",16,function()
      {
         homePickSend(this.item);
      });
      go.item = item;
   }

   // Page dots.
   var dots = slide.createEmptyMovieClip("dots",slide.getNextHighestDepth());
   var i;
   for(i = 0; i < homeNews.length; i++)
   {
      homeDot(dots,i * 22,0,i == homeNewsIndex ? 7 : 5,i == homeNewsIndex ? HOME_GOLD : 0xFFFFFF,i == homeNewsIndex ? 100 : 40);
   }
   dots._x = w / 2 - (homeNews.length - 1) * 11;
   dots._y = content.h - 16;

   slide._x = direction * 80;
   slide._alpha = direction == 0 ? 100 : 0;
   gs.TweenLite.to(slide,0.45,{_x:0,_alpha:100,delay:direction == 0 ? 0 : 0.15,ease:mx.transitions.easing.Strong.easeOut});

   // Arrows stay on top of the slides.
   content.prev.swapDepths(content.getNextHighestDepth());
   content.next.swapDepths(content.getNextHighestDepth());
}

// --- Referee Picks --------------------------------------------------------------------

function homeDrawPicks()
{
   var panel = browser.sanctuaryHome.picks;
   var content = panel.content;
   var w = panel.w - 28;
   var gap = 10;
   var h = Math.floor((panel.h - 72 - 14 - gap * 3) / 4);
   var i;

   content.removeMovieClip();
   content = panel.content = panel.createEmptyMovieClip("content",panel.getNextHighestDepth());
   content._x = 14;
   content._y = 72;

   if(homePicks.length == 0)
   {
      homeText(content,"empty_txt","No picks right now.\nThe referees will be back with more soon!",0,120,w,60,15,HOME_PURPLE_SOFT,"center",true);
      return;
   }

   for(i = 0; i < homePicks.length && i < 4; i++)
   {
      var pick = homePicks[i];
      var card = content.createEmptyMovieClip("card" + i,content.getNextHighestDepth());
      card._y = i * (h + gap);
      card.pick = pick;
      card.w = w;
      card.h = h;
      card.bg = card.createEmptyMovieClip("bg",1);
      card.draw = function(over)
      {
         this.bg.clear();
         homeGradientRect(this.bg,0,0,this.w,this.h,16,over ? 0x7A5CE6 : 0x5A41B8,over ? 0x4A33A6 : 0x3A2A8C,over ? 95 : 70,0xD9CCFF,over ? 90 : 35,2);
      };
      card.draw(false);

      var art = card.createEmptyMovieClip("art",card.getNextHighestDepth());
      art.lineStyle(2,0xFFFFFF,25);
      art.beginFill(0x1A1150,45);
      homeRoundRect(art,0,0,h - 16,h - 16,14);
      art.endFill();
      art._x = art._y = 8;
      homeIcon(art,"icon",pick.icon,6,6,h - 28);

      // Numbered badge.
      var badge = card.createEmptyMovieClip("badge",card.getNextHighestDepth());
      homeGradientRect(badge,-13,-13,26,26,13,0xFFE066,0xE79A13,100,0xFFFFFF,100,2);
      homeText(badge,"n",String(i + 1),-13,-10,26,22,14,0x5A2A00,"center",false);
      badge._x = 10;
      badge._y = 10;

      var textX = h - 2;
      var title = homeText(card,"title_txt",pick.title,textX,6,w - textX - 10,26,17,HOME_WHITE,"left",false);
      title.filters = [new flash.filters.DropShadowFilter(1,90,0x10083A,0.9,2,2,1,1)];
      homeText(card,"text_txt",pick.text,textX,30,w - textX - 12,h - 62,12,HOME_PURPLE_SOFT,"left",true);
      card.go = homeButton(card,"go",pick.button,w - 112,h - 36,102,28,"gold",13,homePickGo);
      card.go.card = card;

      card.onRollOver = function()
      {
         this.draw(true);
         fscommand("FR_event","PlaySound(DockOver)");
      };
      card.onRollOut = card.onReleaseOutside = function()
      {
         this.draw(false);
      };
      card.onRelease = function()
      {
         homePickSend(this.pick);
      };

      card._alpha = 0;
      card._x = 40;
      gs.TweenLite.to(card,0.5,{_alpha:100,_x:0,delay:0.35 + i * 0.12,ease:mx.transitions.easing.Strong.easeOut});
   }
}

function homePickGo()
{
   homePickSend(this.card.pick);
}

function homePickSend(pick)
{
   // The same events the What's New button sends (WhatsNewPanel.onClick); Lua's OnUserEvent handles them.
   if(pick.type == "Marketplace")
   {
      homeSend(pick.type + "(" + pick.param1 + "," + pick.param2 + "," + pick.param3 + "  )");
   }
   else
   {
      homeSend(pick.type + "(" + pick.param1 + "  )");
   }
}

// --- Friends Online -------------------------------------------------------------------

function homeDrawFriends()
{
   var panel = browser.sanctuaryHome.friends;
   var w = panel.w - 28;
   var rowH = 40;
   var i;

   panel.count_mc.removeMovieClip();
   var count = panel.createEmptyMovieClip("count_mc",panel.getNextHighestDepth());
   var label = String(homeFriends.length);
   homeGradientRect(count,0,0,34,26,13,homeFriends.length > 0 ? 0x8BE04E : 0x6C7BA8,homeFriends.length > 0 ? 0x3F9B17 : 0x47527A,100,0xFFFFFF,80,2);
   homeText(count,"n",label,0,2,34,22,14,HOME_WHITE,"center",false);
   count._x = panel.w - 50;
   count._y = 16;

   panel.content.removeMovieClip();
   var content = panel.content = panel.createEmptyMovieClip("content",panel.getNextHighestDepth());
   content._x = 14;
   content._y = 72;

   if(homeFriends.length == 0)
   {
      homeText(content,"empty_txt","None of your friends are online right now.",0,30,w,50,14,HOME_SOFT,"center",true);
      homeButton(content,"find","Find Friends",w / 2 - 80,100,160,34,"blue",14,function()
      {
         homeSend("Communicator(0  )");
      });
      return;
   }

   var pages = Math.ceil(homeFriends.length / HOME_FRIENDS_PER_PAGE);
   homeFriendsPage = Math.min(homeFriendsPage,pages - 1);
   var first = homeFriendsPage * HOME_FRIENDS_PER_PAGE;

   for(i = first; i < homeFriends.length && i < first + HOME_FRIENDS_PER_PAGE; i++)
   {
      var friend = homeFriends[i];
      var row = content.createEmptyMovieClip("row" + i,content.getNextHighestDepth());
      row._y = (i - first) * (rowH + 4);
      row.friend = friend;
      row.w = w;
      row.h = rowH;
      row.bg = row.createEmptyMovieClip("bg",1);
      row.draw = function(over)
      {
         this.bg.clear();
         this.bg.lineStyle(1,0xBFD3FF,over ? 60 : 15);
         this.bg.beginFill(over ? 0x4D74D8 : 0x0A1440,over ? 70 : 35);
         homeRoundRect(this.bg,0,0,this.w,this.h,12);
         this.bg.endFill();
      };
      row.draw(false);

      // Online dot (amber while busy in an encounter).
      var dot = row.createEmptyMovieClip("dot",row.getNextHighestDepth());
      dot.lineStyle(2,0xFFFFFF,70);
      dot.beginFill(friend.busy ? 0xFFB13B : HOME_GREEN,100);
      dot.moveTo(6,0);
      dot.curveTo(6,6,0,6);
      dot.curveTo(-6,6,-6,0);
      dot.curveTo(-6,-6,0,-6);
      dot.curveTo(6,-6,6,0);
      dot.endFill();
      dot._x = 18;
      dot._y = rowH / 2;
      homeGlow(dot,friend.busy ? 0xFFB13B : HOME_GREEN,8,2);

      homeText(row,"name_txt",friend.name,34,friend.detail.length > 0 ? 2 : 9,w - 120,24,15,HOME_WHITE,"left",false);
      if(friend.detail.length > 0)
      {
         homeText(row,"detail_txt",friend.detail,35,21,w - 120,18,11,HOME_SOFT,"left",false);
      }
      row.join = homeButton(row,"join",friend.busy ? "Busy" : "Join",w - 76,7,68,26,friend.busy ? "blue" : "green",12,homeFriendJoin);
      row.join.row = row;

      row.onRollOver = function()
      {
         this.draw(true);
         ToolTipEnum.ShowToolTip(ToolTipEnum.TOOLTIP_LABEL,"Teleport to " + this.friend.name,this);
      };
      row.onRollOut = row.onReleaseOutside = function()
      {
         this.draw(false);
         ToolTipEnum.HideToolTip(this);
      };
      row.onRelease = function()
      {
         homeJoin(this.friend);
      };
   }

   if(pages > 1)
   {
      var pager = homeText(content,"page_txt",homeFriendsPage + 1 + " / " + pages,w / 2 - 40,5 * (rowH + 4) + 6,80,22,13,HOME_SOFT,"center",false);
      homeButton(content,"prev","<",w / 2 - 80,5 * (rowH + 4) + 2,34,28,"blue",14,function()
      {
         homeFriendsPage = (homeFriendsPage + pages - 1) % pages;
         homeDrawFriends();
      });
      homeButton(content,"next",">",w / 2 + 46,5 * (rowH + 4) + 2,34,28,"blue",14,function()
      {
         homeFriendsPage = (homeFriendsPage + 1) % pages;
         homeDrawFriends();
      });
   }
}

function homeFriendJoin()
{
   homeJoin(this.row.friend);
}

function homeJoin(friend)
{
   if(friend.busy)
   {
      return;
   }
   homeSend("teleportPlayerToFriend(" + friend.guid + ")");
}

// --- quick links ----------------------------------------------------------------------

function homeDrawLinks(x, y, w, h)
{
   var home = browser.sanctuaryHome;
   var links = [["My Stuff","bag","gotoMyStuff()"],["Houses","house","HouseBrowser(0  )"],
      ["Hero's Journal","journal","OpenHerosJournal()"],["Achievements","trophy","Achievements()"]];
   var gap = 12;
   var tileW = (w - gap) / 2;
   var tileH = (h - gap) / 2;
   var i;
   for(i = 0; i < links.length; i++)
   {
      var tile = home.createEmptyMovieClip("link" + i,home.getNextHighestDepth());
      tile.baseX = x + (i % 2) * (tileW + gap);
      tile.baseY = y + Math.floor(i / 2) * (tileH + gap);
      tile._x = tile.baseX;
      tile._y = tile.baseY;
      tile.w = tileW;
      tile.h = tileH;
      tile.command = links[i][2];
      tile.bg = tile.createEmptyMovieClip("bg",1);
      tile.draw = function(over)
      {
         this.bg.clear();
         homeGradientRect(this.bg,0,0,this.w,this.h,18,over ? 0x6A97FF : 0x3D63C9,over ? 0x3557C0 : 0x223F94,100,over ? 0xFFF4C2 : 0x9DB8FF,over ? 100 : 60,2);
      };
      tile.draw(false);
      homeShadow(tile,3,8,50);
      var icon = homeIcon(tile,"icon",links[i][1],tileW / 2 - 32,10,64);
      homeText(tile,"label_txt",links[i][0],0,tileH - 34,tileW,24,14,HOME_WHITE,"center",false);
      tile.onRollOver = function()
      {
         this.draw(true);
         fscommand("FR_event","PlaySound(DockOver)");
         gs.TweenLite.to(this,0.3,{_y:this.baseY - 4,ease:mx.transitions.easing.Strong.easeOut});
         gs.TweenLite.to(this.icon,0.6,{_rotation:-6,ease:mx.transitions.easing.Elastic.easeOut});
      };
      tile.onRollOut = tile.onReleaseOutside = function()
      {
         this.draw(false);
         gs.TweenLite.to(this,0.3,{_y:this.baseY,ease:mx.transitions.easing.Strong.easeOut});
         gs.TweenLite.to(this.icon,0.6,{_rotation:0,ease:mx.transitions.easing.Elastic.easeOut});
      };
      tile.onRelease = function()
      {
         homeSend(this.command);
      };
      tile._alpha = 0;
      gs.TweenLite.to(tile,0.5,{_alpha:100,delay:0.5 + i * 0.08});
   }
}

// --- Patch Notes ----------------------------------------------------------------------

function homeLongDate(date)
{
   var parts = String(date).split("-");
   var month = HOME_MONTHS[Number(parts[1]) - 1];
   return month == undefined ? String(date) : month + " " + Number(parts[2]) + ", " + parts[0];
}

function homeUpdateNotesButton()
{
   browser.sanctuaryHome.notesButton.date_txt.text = homeNotes.length > 0 ? "Updated " + homeLongDate(homeNotes[0].date) : "Nothing new yet";
}

var homeNotesWheel = {onMouseWheel:function(delta)
{
   homeScrollNotes(delta * 30);
}};

function homeOpenNotes()
{
   var home = browser.sanctuaryHome;
   home.notes.removeMovieClip();
   var overlay = home.createEmptyMovieClip("notes",home.getNextHighestDepth());

   // Dim the home screen; a click outside the panel closes it.
   var dim = overlay.createEmptyMovieClip("dim",1);
   dim.beginFill(0x050A24,70);
   homeRoundRect(dim,6,6,HOME_W - 12,HOME_H - 12,28);
   dim.endFill();
   dim.useHandCursor = false;
   dim.onRelease = homeCloseNotes;
   dim._alpha = 0;
   gs.TweenLite.to(dim,0.3,{_alpha:100});

   var w = 780;
   var h = 600;
   var panel = overlay.panel = overlay.createEmptyMovieClip("panel",2);
   panel._x = (HOME_W - w) / 2;
   panel._y = (HOME_H - h) / 2;
   homeGradientRect(panel,0,0,w,h,24,0x2D57C2,0x16296E,100,0xFFF4C2,80,3);
   homeShadow(panel,8,24,70);
   homeIcon(panel,"icon","scroll",22,16,60);
   var title = homeText(panel,"title_txt","Patch Notes",94,14,w - 200,42,30,HOME_WHITE,"left",false);
   title.filters = [new flash.filters.DropShadowFilter(2,90,0x0A1030,0.9,3,3,1,1)];
   homeText(panel,"subtitle_txt","The latest changes to the Sanctuary server",96,54,w - 200,22,14,HOME_SOFT,"left",false);
   homeButton(panel,"close","X",w - 64,18,44,44,"blue",18,homeCloseNotes);

   var listX = 30;
   var listY = 96;
   var listW = w - 110;
   var listH = h - listY - 26;
   var list = panel.list = panel.createEmptyMovieClip("list",panel.getNextHighestDepth());
   list._x = listX;
   list._y = listY;
   panel.listY = listY;
   panel.listH = listH;

   var y = 0;
   var lastDate = "";
   var i;
   for(i = 0; i < homeNotes.length; i++)
   {
      var note = homeNotes[i];
      if(note.date != lastDate)
      {
         lastDate = note.date;
         y += i == 0 ? 0 : 10;
         homeText(list,"date" + i,homeLongDate(note.date),0,y,listW,26,18,HOME_GOLD,"left",false);
         list.lineStyle(1,HOME_GOLD,35);
         list.moveTo(0,y + 28);
         list.lineTo(listW,y + 28);
         y += 36;
      }
      list.lineStyle();
      homeDot(list,10,y + 10,4,0xFFFFFF,80);
      var line = homeText(list,"note" + i,note.text,24,y,listW - 24,22,15,HOME_WHITE,"left",true);
      line.autoSize = "left";
      y += Math.max(22,line._height) + 6;
   }
   if(homeNotes.length == 0)
   {
      homeText(list,"empty_txt","No patch notes right now.",0,40,listW,30,18,HOME_SOFT,"center",false);
   }
   panel.contentH = y;

   var mask = panel.createEmptyMovieClip("mask",panel.getNextHighestDepth());
   mask.beginFill(0,100);
   homeRoundRect(mask,listX - 4,listY,listW + 8,listH,0);
   mask.endFill();
   list.setMask(mask);

   // Scroll buttons and a thumb when the notes don't fit.
   if(y > listH)
   {
      homeButton(panel,"up","^",w - 64,listY,40,36,"blue",16,function()
      {
         homeScrollNotes(120);
      });
      homeButton(panel,"down","v",w - 64,listY + listH - 36,40,36,"blue",14,function()
      {
         homeScrollNotes(-120);
      });
      var track = panel.createEmptyMovieClip("track",panel.getNextHighestDepth());
      track.beginFill(0x0A1440,50);
      homeRoundRect(track,w - 50,listY + 44,12,listH - 88,6);
      track.endFill();
      panel.thumb = panel.createEmptyMovieClip("thumb",panel.getNextHighestDepth());
      panel.thumbH = Math.max(30,(listH - 88) * listH / y);
      homeGradientRect(panel.thumb,0,0,12,panel.thumbH,6,0xFFE066,0xE79A13,100);
      panel.thumb._x = w - 50;
      panel.thumb._y = listY + 44;
      Mouse.addListener(homeNotesWheel);
   }

   panel._alpha = 0;
   var top = panel._y;
   panel._y = top + 30;
   gs.TweenLite.to(panel,0.45,{_alpha:100,_y:top,ease:mx.transitions.easing.Strong.easeOut});
}

function homeScrollNotes(delta)
{
   var panel = browser.sanctuaryHome.notes.panel;
   var lowest = panel.listY - Math.max(0,panel.contentH - panel.listH);
   var y = Math.max(lowest,Math.min(panel.listY,panel.list._y + delta));
   gs.TweenLite.to(panel.list,0.25,{_y:y,ease:mx.transitions.easing.Strong.easeOut});
   if(panel.thumb != undefined && lowest < panel.listY)
   {
      var travel = panel.listH - 88 - panel.thumbH;
      gs.TweenLite.to(panel.thumb,0.25,{_y:panel.listY + 44 + travel * (panel.listY - y) / (panel.listY - lowest)});
   }
}

function homeCloseNotes()
{
   Mouse.removeListener(homeNotesWheel);
   fscommand("FR_event","PlaySound(DockSelect)");
   browser.sanctuaryHome.notes.removeMovieClip();
}

// --- bottom bar -----------------------------------------------------------------------

function homeDrawBottom()
{
   var home = browser.sanctuaryHome;
   var y = HOME_BOTTOM + 8;
   var barH = HOME_H - HOME_BOTTOM - 22;

   var tip = home.createEmptyMovieClip("tip",home.getNextHighestDepth());
   tip._x = 44;
   tip._y = y;
   homeText(tip,"label_txt","TIP",0,Math.round(barH / 2) - 11,44,22,14,HOME_GOLD,"left",false);
   tip.txt = homeText(tip,"tip_txt","",46,Math.round(barH / 2) - 11,860,22,14,HOME_SOFT,"left",false);
   tip.index = Math.floor(Math.random() * HOME_TIPS.length);
   tip.next = function()
   {
      this.index = (this.index + 1) % HOME_TIPS.length;
      this.txt.text = HOME_TIPS[this.index];
      this.txt._alpha = 0;
      gs.TweenLite.to(this.txt,0.6,{_alpha:100});
   };
   tip.next();
   clearInterval(_root.homeTipInterval);
   _root.homeTipInterval = setInterval(tip,"next",7000);

   var play = homeButton(home,"play","PLAY!",HOME_W - 260,y + 4,232,barH - 8,"green",24,function()
   {
      clearInterval(_root.homeTipInterval);
      homeSend("closeWindow()");
   });
   homeGlow(play,0xB6FF7A,14,1);
   // The label is art in the client's Free Realms lettering (TEXT_SYMBOLS), fitted to the button.
   play.label_txt._visible = false;
   var label = play.attachMovie("sanctuaryPlayLabel","label_mc",play.getNextHighestDepth());
   var fit = Math.min(1,(barH - 12) / label._height,212 / label._width);
   label._xscale = label._yscale = fit * 100;
   label._x = Math.round((232 - label._width) / 2);
   label._y = Math.round((barH - 8 - label._height) / 2);
   // A shine that sweeps across Play now and then.
   var shine = play.createEmptyMovieClip("shine",play.getNextHighestDepth());
   shine.beginGradientFill("linear",[0xFFFFFF,0xFFFFFF,0xFFFFFF],[0,70,0],[0,128,255],{matrixType:"box",x:0,y:0,w:60,h:barH,r:0});
   shine.moveTo(20,0);
   shine.lineTo(60,0);
   shine.lineTo(40,barH);
   shine.lineTo(0,barH);
   shine.lineTo(20,0);
   shine.endFill();
   var mask = play.createEmptyMovieClip("shineMask",play.getNextHighestDepth());
   mask.beginFill(0,100);
   homeRoundRect(mask,0,0,232,barH - 8,(barH - 8) / 2);
   mask.endFill();
   shine.setMask(mask);
   shine._x = -80;
   shine.sweep = function()
   {
      this._x = -80;
      gs.TweenLite.to(this,0.9,{_x:260,ease:mx.transitions.easing.Regular.easeInOut});
   };
   clearInterval(_root.homeShineInterval);
   _root.homeShineInterval = setInterval(shine,"sweep",3200);
}

// --- data from Lua --------------------------------------------------------------------

// Lua sends every announcement (PopulateWNSArray), then the start index, then showWNS. Tagged ones are
// What's New items and Referee Picks (tag|title|text|button|icon); any other is a stock image ad, shown
// as a What's New slide. The stock panel (and its one-ad-at-a-time path, populateMainDisplay_lua) is hidden.
populateWnsData_lua = function(iconId, titleString, adText, buttonText, type, param1, param2, param3, string_param2)
{
   var tag = String(string_param2);
   var item = {type:type == undefined ? "" : String(type),param1:param1,param2:param2,param3:param3};
   if(tag.indexOf(HOME_NOTE_TAG) == 0)
   {
      var note = tag.split("|");
      homeNotes.push({date:note[1],text:note[2]});
      return;
   }
   if(tag.indexOf(HOME_PICK_TAG) == 0 || tag.indexOf(HOME_NEWS_TAG) == 0)
   {
      var fields = tag.split("|");
      item.title = fields[1];
      item.text = fields[2];
      item.button = fields[3];
      item.icon = fields[4];
      if(tag.indexOf(HOME_PICK_TAG) == 0)
      {
         homePicks.push(item);
      }
      else
      {
         homeNews.push(item);
      }
      return;
   }
   item.title = String(titleString);
   item.text = String(adText);
   item.html = true;
   item.button = buttonText == undefined ? "" : String(buttonText);
   item.icon = iconId;
   homeNews.push(item);
};

populateMainDisplay_lua = function(iconId, titleString, adText, buttonText, type, param1, param2, param3, string_param2)
{
};

showWNS = function()
{
   homeDrawNews();
   homeDrawPicks();
   homeUpdateNotesButton();
};

// Lua's PopulateFriendsList sends the online friends' rows.
addFriend = function(name, guid, status, profileName, profileRank, profileIconId, profileBackgroundId, inEncounter, isLocal)
{
   var detail = "";
   if(profileName != undefined && String(profileName).length > 0 && isNaN(Number(profileName)))
   {
      detail = String(profileName);
      if(Number(profileRank) > 0)
      {
         detail += "  -  Rank " + profileRank;
      }
   }
   homeFriends.push({name:String(name),guid:guid,detail:detail,busy:inEncounter == true || inEncounter == 1 || inEncounter == "true"});
   homeDrawFriends();
};
setCount = function(n)
{
};

// --- build ----------------------------------------------------------------------------

function homeBuild()
{
   // Only the close button stays from the stock browser; Chatty (no_station_cash.as) goes too.
   browser.sanctuaryChatty.removeMovieClip();
   var name;
   for(name in browser)
   {
      if(typeof browser[name] == "movieclip" && name != "donebtn" && browser[name]._parent == browser)
      {
         browser[name]._visible = false;
      }
   }

   homeDrawFrame();

   var home = browser.createEmptyMovieClip("sanctuaryHome",browser.getNextHighestDepth());
   home._y = - HOME_BROWSER_Y;

   homeDrawHeader();

   homePanel("news",24,HOME_TOP,576,HOME_BOTTOM - HOME_TOP,0x2D57C2,0x16296E,0x9DB8FF,"What's New","Fresh in Open Source Free Realms","book");
   homeDrawNews();

   homePanel("picks",614,HOME_TOP,372,HOME_BOTTOM - HOME_TOP,0x5B3FC4,0x2B1E73,0xD9CCFF,"Referee Picks","Hand-picked by the OSFR referees","referee");
   homeDrawPicks();

   homePanel("friends",1000,HOME_TOP,342,300,0x2D57C2,0x16296E,0x9DB8FF,"Friends Online","Click a friend to teleport","friends");
   homeDrawFriends();

   homeDrawLinks(1000,HOME_TOP + 312,342,HOME_BOTTOM - HOME_TOP - 312);
   homeDrawBottom();
}

homeBuild();

if(!frRunningInGame())
{
   populateWnsData_lua(0,"","","","OpenHerosJournal",0,0,0,"[[SANCTUARY_NEWS]]|Quests are here!|Talk to the folks with a marker over their heads to pick up quests.|Hero's Journal|journal");
   populateWnsData_lua(0,"","","","",0,0,0,"[[SANCTUARY_NEWS]]|Spin the daily wheel|Find the daily wheel in the Games window and give it a spin for a prize.||wheel");
   populateWnsData_lua(0,"","","","Teleport",5,0,0,"[[SANCTUARY_PICK]]|Meet up in Sanctuary|The heart of Sacred Grove. Show off your look and find people to play with.|Teleport|tree");
   populateWnsData_lua(0,"","","","HouseBrowser",0,0,0,"[[SANCTUARY_PICK]]|Make a house a home|Pick out a house, then fill it with furniture from the Coin Shop.|Houses|house");
   populateWnsData_lua(0,"","","","Marketplace",0,0,0,"[[SANCTUARY_PICK]]|Fresh in the Coin Shop|Spend the coins you earn on outfits, mounts and pets.|Shop|coin");
   showWNS();
   addFriend("Bubba","1","","Brawler",7);
   addFriend("Pixie","2","","Wizard",12,0,0,true);
}
// ===== End Sanctuary home screen =====

using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Collections.Concurrent;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.Applications;
public sealed class Catalog {
 public int AppleSource {get;set;}=15; public List<AppEntry> Apps {get;set;}=new(); public List<ChannelEntry> Channels {get;set;}=new(); public List<string> FavoriteApps {get;set;}=new(); public List<string> FavoriteChannels {get;set;}=new();
}
public sealed class Favorites {public List<string> Apps {get;set;}=new();public List<string> Channels {get;set;}=new();}
public sealed class WeatherReading {public double Temperature {get;set;}public int Code {get;set;}public DateTime UpdatedUtc {get;set;}}
public sealed class QuietHome : NUIApplication {

 const string Root="/opt/usr/apps/com.samsung.tv.adplayer/res/quiet-home/";
 const string StateFile="/tmp/quiet-home-qn85d-state.txt";
 string dataDir; Catalog catalog; List<AppEntry> appChoices=new(); bool appsRefreshing, pickerFromHome; Favorites favorites; bool prefsValid=true,active=true; TvServices tv;LogoStore logoStore;bool artworkBusy,artworkRefreshPending;
 readonly Color background=new(.19f,.23f,.29f,.30f),surface=new(.29f,.34f,.41f,.68f),ink=new(.96f,.96f,.97f,1),muted=new(.84f,.88f,.93f,1),accent=new(.94f,.94f,.97f,1);
 sealed class Tile {public View Card;public View FocusFill;public TextLabel Caption,Content;public Color NormalColor;public string Name,Action;public int X,Y,W,H,Rail,Index;}
 readonly List<Tile> tiles=new();readonly List<View> views=new();readonly ConcurrentQueue<Action> ui=new();
 readonly Dictionary<string,ImageView> art=new(); readonly Dictionary<string,View> artParent=new(); View artParking;
 Animation focusAnimation,railAnimation;FavoriteRail channelRail,appRail;View channelContent,appContent;
 TextLabel time,date,weather,weatherPlace,source,status;Tizen.NUI.Timer tick;int page,selected,pickerPage,tabFocus=-1;string selectedId="",selectedKind="",editing="",lastKey="",lastSourceLabel="Apple TV";long lastKeyAt;int lastSource=0;bool refreshSourceAfterResume;bool weatherBusy;WeatherReading reading;DateTime nextWeather;string weatherError="";List<(int Index,string Name)> tracks=new();bool isSubtitle;
 [DllImport("libsource-api.so")] static extern int connect_source(int sourceType);
 [DllImport("libvconf.so.0")] static extern int vconf_get_int(string key,out int value);
 [DllImport("libcapi-media-sound-manager-tv.so.0")]static extern int sound_manager_get_master_volume(out int value);
 [DllImport("libcapi-media-sound-manager-tv.so.0")]static extern int sound_manager_set_master_volume(int value);
 [DllImport("libcapi-media-sound-manager-tv.so.0")]static extern int sound_manager_get_master_mute(out int value);
 [DllImport("libcapi-media-sound-manager-tv.so.0")]static extern int sound_manager_set_master_mute(int value);
 [DllImport("libsettings-api.so")]static extern bool CSharpGetItemValue(string id,out string name,out string value);
 [DllImport("libsettings-api.so")]static extern bool CSharpSetItemValue(string id,string value);
 [DllImport("libsettings-api.so")]static extern bool CSharpGetItemEnable(string id,out bool enabled);
 [DllImport("libcapi-appfw-app-control.so.0")]static extern int app_control_create(out IntPtr c);
 [DllImport("libcapi-appfw-app-control.so.0")]static extern int app_control_set_app_id(IntPtr c,string id);
 [DllImport("libcapi-appfw-app-control.so.0")]static extern int app_control_set_operation(IntPtr c,string op);
 [DllImport("libcapi-appfw-app-control.so.0")]static extern int app_control_add_extra_data(IntPtr c,string key,string val);
 [DllImport("libcapi-appfw-app-control.so.0")]static extern int app_control_send_launch_request(IntPtr c,IntPtr cb,IntPtr data);
 [DllImport("libcapi-appfw-app-control.so.0")]static extern int app_control_destroy(IntPtr c);
 protected override void OnCreate(){base.OnCreate();SourceOverlay.Apply();Window.Default.SetTransparency(true);Window.Default.BackgroundColor=background;Window.Default.Title="Quiet Home";Window.Default.KeyEvent+=OnKey;try{FontClient.Instance.AddCustomFontDirectory(Root+"fonts");}catch{}
  dataDir=DirectoryInfo.Data+"quiet-home/";Directory.CreateDirectory(dataDir);
  catalog=JsonSerializer.Deserialize<Catalog>(File.ReadAllText(Root+"catalog.json"))??throw new InvalidDataException("Missing catalog");favorites=new Favorites{Apps=new(catalog.FavoriteApps),Channels=new(catalog.FavoriteChannels)};
  try{if(File.Exists(dataDir+"favorites.json"))favorites=JsonSerializer.Deserialize<Favorites>(File.ReadAllText(dataDir+"favorites.json"))??throw new InvalidDataException();}catch{prefsValid=false;}
  favorites.Apps=(favorites.Apps??new()).Distinct().ToList();favorites.Channels=(favorites.Channels??new()).Distinct().ToList();try{if(File.Exists(dataDir+"catalog-cache.json")){var cached=JsonSerializer.Deserialize<Catalog>(File.ReadAllText(dataDir+"catalog-cache.json"));if(cached?.Apps?.Count>0)catalog.Apps=cached.Apps;if(cached?.Channels?.Count>0)catalog.Channels=cached.Channels;}}catch{}catalog.Apps=catalog.Apps.Where(a=>IsCatalogApp(a.Id)).ToList();UpdateAppChoices();try{tv=new TvServices();}catch{}
  try{if(File.Exists(dataDir+"weather.json"))reading=JsonSerializer.Deserialize<WeatherReading>(File.ReadAllText(dataDir+"weather.json"));}catch{}
  artParking=new View{Opacity=0,Size2D=new Size2D(1920,1080)};Window.Default.Add(artParking);
  logoStore=new LogoStore(Root+"logo-sources.json",dataDir+"channel-art");foreach(var channel in catalog.Channels){if(string.IsNullOrWhiteSpace(channel.Asset)||!File.Exists(PathFor(channel.Asset)))channel.Asset=logoStore.Cached(channel.Name)??"";}RefreshSource();Build();_ = RefreshFavoriteArtwork();tick=new Tizen.NUI.Timer(1000);tick.Tick+=(s,e)=>{if(refreshSourceAfterResume){refreshSourceAfterResume=false;RefreshSource();WriteState("source_refreshed");}while(ui.TryDequeue(out var action)){try{action();}catch(Exception ex){Notice(ex.Message);}}UpdateHeader();if(active&&DateTime.UtcNow>=nextWeather&&!weatherBusy)_ = RefreshWeather();return true;};tick.Start();_ = RefreshApps();_ = RefreshWeather();WriteState("created");
 }
 protected override void OnPause(){SourceOverlay.Release();base.OnPause();active=false;tick?.Stop();WriteState("paused");}
 protected override void OnResume(){base.OnResume();SourceOverlay.Apply();active=true;refreshSourceAfterResume=true;tick?.Start();RefreshSource();if(time!=null)UpdateHeader();WriteState("resumed");}
 protected override void OnAppControlReceived(AppControlReceivedEventArgs e){base.OnAppControlReceived(e);string type;if(e.ReceivedAppControl.ExtraData.TryGet("launch_type",out type)&&type=="preload"){Exit();return;}string target;page=e.ReceivedAppControl.ExtraData.TryGet("quiet_page",out target)&&target=="settings"?2:0;selected=0;tabFocus=-1;if(catalog!=null){RefreshSource();Build();}}
 View Add(View v){views.Add(v);Window.Default.Add(v);return v;}
 TextLabel Label(string text,int x,int y,int w,int h,float size,Color color){return new TextLabel{Text=text,Position2D=new Position2D(x,y),Size2D=new Size2D(w,h),PixelSize=size,FontFamily="Quiet Inter Medium",TextColor=color,VerticalAlignment=VerticalAlignment.Center};}
 TextLabel Text(string t,int x,int y,int w,int h,float size,Color c)=>(TextLabel)Add(Label(t,x,y,w,h,size,c));
 View Panel(int x,int y,int w,int h,Color c,int r=10)=>Add(new View{Position2D=new Position2D(x,y),Size2D=new Size2D(w,h),BackgroundColor=c,CornerRadius=new Vector4(r,r,r,r)});
 Color Hex(string s){try{int n=Convert.ToInt32(s.TrimStart('#'),16);return new Color((n>>16&255)/255f,(n>>8&255)/255f,(n&255)/255f,.72f);}catch{return surface;}}
 string PathFor(string asset){
  if(asset=="icons/applemusic.svg"&&File.Exists(Root+"icons/applemusic-note.svg"))return Root+"icons/applemusic-note.svg";
  return System.IO.Path.IsPathRooted(asset)?asset:Root+asset;
 }
 static string AppCardColor(AppEntry app)=>app.Asset switch{
  "icons/plex.svg"=>"#67502E",
  "icons/hbo.svg"=>"#302D70",
  "icons/jellyfin.svg"=>"#49366B",
  "icons/primevideo.svg"=>"#245370",
  "icons/netflix.svg"=>"#53252E",
  "icons/applemusic.svg"=>"#15171D",
  _ when app.Id=="9Ur5IzDKqV.TizenYouTube"=>"#502D34",
  "icons/appletv.svg"=>"#485666",
  _=>app.Background??"#485666"
 };
 void Artwork(View parent,string asset,int w,int h){if(string.IsNullOrEmpty(asset))return;string path=PathFor(asset);if(!File.Exists(path))return;string key=path+"@"+w+"x"+h;if(!art.TryGetValue(key,out var image)){image=new ImageView{ResourceUrl=path,Size2D=new Size2D(w,h),FittingMode=FittingModeType.ShrinkToFit,SamplingMode=SamplingModeType.BoxThenLanczos,ReleasePolicy=ReleasePolicyType.Destroyed};art[key]=image;artParent[key]=artParking;artParking.Add(image);}artParent[key].Remove(image);parent.Add(image);artParent[key]=parent;image.Position2D=new Position2D(0,0);}
 void ParkArt(){foreach(var key in art.Keys){if(artParent[key]!=artParking){artParent[key].Remove(art[key]);artParking.Add(art[key]);artParent[key]=artParking;}}}
 bool HasArtwork(string asset)=>!string.IsNullOrWhiteSpace(asset)&&File.Exists(PathFor(asset));
 Tile Card(string name,string action,int x,int y,int w,int h,string asset="",string color=null,bool caption=true,bool fallbackName=false,string gradient=null){
  string gradientPath=gradient==null?null:Root+"favorite-palette/"+gradient+".png";
  bool hasGradient=File.Exists(gradientPath);
  var normal=hasGradient?new Color(0,0,0,0):color=="#000000"?new Color(0,0,0,1):color==null?surface:Hex(color);
  var card=Panel(x+w/2,y+h/2,w,h,normal,10);card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
  if(hasGradient)card.Add(new ImageView{ResourceUrl=gradientPath,Size2D=new Size2D(w,h)});
  View focusFill=null;
  if(page==0){
   focusFill=new View{
    Size2D=new Size2D(w,h),
    BackgroundColor=new Color(1,1,1,.18f),
    CornerRadius=new Vector4(10,10,10,10),
    Opacity=0
   };
   card.Add(focusFill);
  }
  TextLabel content=null;
  if(!string.IsNullOrWhiteSpace(asset)&&File.Exists(PathFor(asset))){int px=Math.Min(38,w/7),py=Math.Min(30,h/5);var inset=new View{Position2D=new Position2D(px,py),Size2D=new Size2D(w-px*2,h-py*2)};card.Add(inset);Artwork(inset,asset,w-px*2,h-py*2);}
  else if(w<=80){content=Label(name,0,0,w,h,32,ink);content.HorizontalAlignment=HorizontalAlignment.Center;card.Add(content);}
  else{bool missingLogo=action.StartsWith("app:")||action.StartsWith("channel:");content=Label(missingLogo&&!fallbackName?LogoStore.Initials(name):name,22,12,w-44,h-24,missingLogo&&!fallbackName?38:w<250?24:28,ink);if(missingLogo)content.HorizontalAlignment=HorizontalAlignment.Center;card.Add(content);}
  var label=caption?Text(name,x,y+h+18,w,50,w<=320?22:24,muted):null;
  var tile=new Tile{Card=card,FocusFill=focusFill,Caption=label,Content=content,NormalColor=normal,Name=name,Action=action,X=x,Y=y,W=w,H=h};tiles.Add(tile);return tile;
 }

 void Build(){if(railAnimation!=null){railAnimation.Stop();railAnimation.Dispose();railAnimation=null;}if(focusAnimation!=null){focusAnimation.Stop();focusAnimation.Dispose();focusAnimation=null;}ParkArt();foreach(var v in views){Window.Default.Remove(v);v.Dispose();}views.Clear();tiles.Clear();if(page!=0)Text(page==1?"Your Apps":page==2?"Quick Settings":page==3?"Favorite apps":page==4?"Favorite channels":page==5?"Add an app":page==6?"Add a channel":page==7?"Edit favorite":isSubtitle?"Subtitles":"Audio track",80,62,790,90,56,ink);
  if(page==0)BuildHomeHeader();
  else{
  source=Text("",900,78,350,70,22,ink);Text("Active source",900,49,350,25,18,muted);Panel(1260,62,1,76,new Color(1,1,1,.1f),0);Panel(1550,62,1,76,new Color(1,1,1,.1f),0);weather=Text("",1290,56,240,48,30,ink);weatherPlace=Text("",1290,106,270,30,19,muted);time=Text("",1610,50,220,55,42,ink);time.HorizontalAlignment=HorizontalAlignment.End;date=Text("",1570,107,260,34,20,muted);date.HorizontalAlignment=HorizontalAlignment.End;
  }
  string[] tabs={"Inputs","Apps","Settings"};
  for(int i=0;i<tabs.Length;i++){
   if(page==0){
    int y=475+i*138;
    bool activeTab=tabFocus==i||(tabFocus<0&&i==0);
    if(activeTab){
     var highlight=Panel(89,y,170,130,new Color(.91f,.94f,.93f,1),10);
     highlight.BorderlineWidth=2;
     highlight.BorderlineColor=accent;
    }
    Add(new ImageView{ResourceUrl=Root+"nav-icons/"+i+".svg",Position2D=new Position2D(154,y+29),Size2D=new Size2D(40,40),Color=activeTab?new Color(.12f,.16f,.16f,1):ink});
    CenteredText(tabs[i],89,y+74,170,48,25,activeTab?new Color(.12f,.16f,.16f,1):ink);
    continue;
   }
   var t=Text(tabs[i],80+i*200,180,180,62,27,page==i?ink:muted);
   t.HorizontalAlignment=HorizontalAlignment.Center;
   if(page==i){t.BackgroundColor=surface;t.CornerRadius=new Vector4(30,30,30,30);}
   if(tabFocus==i){t.BorderlineWidth=3;t.BorderlineColor=accent;}
  }
  status=Text("",80,1010,1740,42,22,muted);
  if(page==0)BuildHome();else if(page==1)BuildApps();else if(page==2)BuildSettings();else if(page==3||page==4)BuildFavorites();else if(page==5||page==6)BuildPicker();else if(page==7)BuildFavoriteActions();else BuildTracks();
  selected=Math.Clamp(selected,0,Math.Max(0,tiles.Count-1));Focus(false);UpdateHeader();if(!prefsValid)Notice("Saved favorites could not be read. The original file has been preserved.");WriteState("page_ready");
 }
 TextLabel CenteredText(string value,int x,int y,int width,int height,int size,Color color){
  var label=Text(value,x,y,width,height,size,color);
  label.HorizontalAlignment=HorizontalAlignment.Center;
  return label;
 }
 void GlassPanel(int x,int y,int width,int height,int radius){
  var panel=Panel(x,y,width,height,new Color(.125f,.149f,.169f,.88f),radius);
  panel.BorderlineWidth=1;
  panel.BorderlineColor=new Color(1,1,1,.16f);
 }
 void BuildHomeHeader(){
  const int x=959,y=351,column=294,inset=12;
  GlassPanel(76,462,196,432,14);
  GlassPanel(x,y,3*column,96,12);
  for(int i=1;i<3;i++)Panel(x+i*column,y+18,1,66,new Color(1,1,1,.18f),0);
  int width=column-2*inset;
  CenteredText("Active source",x+inset,y+12,width,25,19,muted);
  weatherPlace=CenteredText("",x+column+inset,y+12,width,25,19,muted);
  date=CenteredText("",x+2*column+inset,y+12,width,25,19,muted);
  source=CenteredText("",x+inset,y+40,width,48,21,ink);
  weather=CenteredText("",x+column+inset,y+42,width,42,31,ink);
  time=CenteredText("",x+2*column+inset,y+42,width,42,31,ink);
 }
 void CompactButton(string name,string action,string symbol,int x,int y){var tile=Card(symbol,action,x,y,64,52,caption:false);tile.Name=name;}
 void AttachRail(Tile tile,View content,int originX,int originY,int rail,int index,int scroll){
  foreach(var view in new View[]{tile.Card,tile.Caption}){if(view==null)continue;var position=view.Position2D;Window.Default.Remove(view);views.Remove(view);content.Add(view);view.Position2D=new Position2D(position.X-originX,position.Y-originY);}
  tile.Rail=rail;tile.Index=index;tile.X-=scroll;
 }
 View RailViewport(int x,int y,int w,int h,out View content,int contentWidth,int scroll){var viewport=Add(new View{Position2D=new Position2D(x,y),Size2D=new Size2D(w,h),ClippingMode=ClippingModeType.ClipToBoundingBox});content=new View{Position2D=new Position2D(-scroll,0),Size2D=new Size2D(contentWidth,h)};viewport.Add(content);return viewport;}
 const int HomeRailX=329;
 void BuildHome(){
  const int cardWidth=232,cardHeight=167,step=252,top=499,channelViewportY=top;
  GlassPanel(307,477,1536,211,12);
  GlassPanel(307,708,1536,183,12);
  Card("Apple TV","source:"+catalog.AppleSource,HomeRailX,top,cardWidth,cardHeight,"icons/appletv.svg","#485666",caption:false,fallbackName:true,gradient:"apple-tv");
  var channels=favorites.Channels.Select(id=>catalog.Channels.Find(c=>c.Id==id)).Where(c=>c!=null).ToList();
  channelRail=new FavoriteRail(1492,0,cardWidth,step,channels.Count+2,channelRail?.Scroll??0,0);
  RailViewport(HomeRailX,channelViewportY,1492,cardHeight,out channelContent,channelRail.ContentWidth,channelRail.Scroll);
  AttachRail(tiles[0],channelContent,HomeRailX,channelViewportY,1,0,channelRail.Scroll);
  for(int i=0;i<channels.Count;i++){var c=channels[i];var tile=Card(c.Name,"channel:"+c.Id,HomeRailX+(i+1)*step,top,cardWidth,cardHeight,c.Asset,c.Name=="History Channel HD"?"#D5D0C5":"#485666",caption:false,fallbackName:true,gradient:c.Asset switch{
   "icons/pro-logo.svg"=>"pro-tv","icons/discovery-logo.svg"=>"discovery",
   "icons/history-logo.png"=>"history","icons/id-logo.svg"=>"investigation",_=>null
  });AttachRail(tile,channelContent,HomeRailX,channelViewportY,1,i+1,channelRail.Scroll);}
  AddRailButton("channels",HomeRailX+(channels.Count+1)*step,top,cardWidth,cardHeight,channelContent,HomeRailX,channelViewportY,1,channels.Count+1,channelRail.Scroll);
  const int dockY=730;
  var apps=favorites.Apps.Select(id=>catalog.Apps.Find(a=>a.Id==id)).Where(a=>a!=null).ToList();const int spacing=216;
  const int appViewportWidth=1492;
  appRail=new FavoriteRail(appViewportWidth,0,spacing-20,spacing,apps.Count+1,appRail?.Scroll??0,0);
  RailViewport(HomeRailX,dockY,appViewportWidth,139,out appContent,appRail.ContentWidth,appRail.Scroll);
  for(int i=0;i<apps.Count;i++){var a=apps[i];var tile=Card(a.Name,"app:"+a.Id,HomeRailX+i*spacing,dockY,spacing-20,139,a.Asset,AppCardColor(a),caption:false,fallbackName:true,gradient:a.Asset switch{
   "icons/plex.svg"=>"plex","icons/youtube-brand.png"=>"youtube","icons/hbo.svg"=>"hbo",
   "icons/jellyfin.svg"=>"jellyfin","icons/primevideo.svg"=>"prime","icons/netflix.svg"=>"netflix",_=>null
  });AttachRail(tile,appContent,HomeRailX,dockY,2,i,appRail.Scroll);}
  AddRailButton("apps",HomeRailX+apps.Count*spacing,dockY,spacing-20,139,appContent,HomeRailX,dockY,2,apps.Count,appRail.Scroll);
 }
 bool TileVisible(Tile tile)=>tile.Rail==1?channelRail.IsVisible(tile.Index):tile.Rail==2?appRail.IsVisible(tile.Index):true;
 void RevealRail(Tile tile){if(tile.Rail==0)return;var rail=tile.Rail==1?channelRail:appRail;var content=tile.Rail==1?channelContent:appContent;if(!rail.Reveal(tile.Index))return;if(railAnimation!=null){railAnimation.Stop();railAnimation.Dispose();}railAnimation=new Animation(160);railAnimation.AnimateTo(content,"Position",new Position(-rail.Scroll,0,0));railAnimation.Play();foreach(var item in tiles.Where(t=>t.Rail==tile.Rail))item.X=HomeRailX+rail.Left(item.Index)-rail.Scroll;}

 void AddRailButton(string kind,int x,int y,int w,int h,View content,int originX,int originY,int rail,int index,int scroll){
  var tile=Card("+","picker:"+kind,x,y,w,h,caption:false);
  tile.Name=kind=="apps"?"Add an app":"Add a channel";
  tile.Content.HorizontalAlignment=HorizontalAlignment.Center;
  tile.Content.PixelSize=46;
  AttachRail(tile,content,originX,originY,rail,index,scroll);
 }
 bool HandleHomeVertical(string key){
  if(page!=0||tabFocus>=0||tiles.Count==0)return false;
  var current=tiles[selected];
  int rail=key=="Down"&&current.Rail!=2?2:key=="Up"&&current.Rail==2?1:0;
  if(rail==0)return false;
  int target=tiles.Select((tile,index)=>(tile,index))
   .Where(item=>item.tile.Rail==rail&&TileVisible(item.tile))
   .OrderBy(item=>Math.Abs(item.tile.X+item.tile.W/2-current.X-current.W/2))
   .Select(item=>item.index).DefaultIfEmpty(-1).First();
  if(target<0)return false;
  selected=target;RevealRail(tiles[selected]);Focus();return true;
 }
 bool HandleHomeHorizontal(string key){if(page!=0||tabFocus>=0||tiles.Count==0||(key!="Left"&&key!="Right"))return false;var current=tiles[selected];int delta=key=="Left"?-1:1;
  if(current.Rail==0){if(current.Action.StartsWith("source:")&&delta>0){int first=tiles.FindIndex(t=>t.Rail==1&&t.Index==0);if(first>=0){selected=first;RevealRail(tiles[selected]);Focus();return true;}}return false;}
  int next=tiles.FindIndex(t=>t.Rail==current.Rail&&t.Index==current.Index+delta);if(next>=0){selected=next;RevealRail(tiles[selected]);Focus();return true;}
  if(delta<0&&current.Index==0){tabFocus=current.Rail==2?1:0;Build();}
  return true;
 }

 void AppListCard(AppEntry app,string action,int x,int y,int width,int height,string marker=""){
  var tile=Card(app.Name,action,x,y,width,height,color:AppCardColor(app),caption:false);
  tile.Card.Remove(tile.Content);
  tile.Content.Dispose();
  tile.Content=Label(app.Name,110,8,width-160,height-16,23,ink);
  tile.Card.Add(tile.Content);
  var icon=new View{Position2D=new Position2D(15,(height-62)/2),Size2D=new Size2D(76,62)};
  tile.Card.Add(icon);
  Artwork(icon,app.Asset,76,62);
  if(marker.Length>0)tile.Card.Add(Label(marker,width-43,8,36,height-16,24,ink));
 }
 void BuildApps(){
  int start=pickerPage*15;
  for(int i=0;i<Math.Min(15,appChoices.Count-start);i++){
   var app=appChoices[start+i];
   AppListCard(app,"app:"+app.Id,80+i%3*600,285+i/3*140,560,110);
  }
  Notice(appChoices.Count==0?"No apps with available artwork":appChoices.Count>15?"Select to launch · Down at the bottom for more apps":"Select to launch · Back to Home");
 }
 void BuildSettings(){var actions=new[]{("Volume","volume"),("Brightness","brightness"),("Game Mode","game"),("Sleep Timer","sleep"),("Sound","mute"),("Subtitles","subtitles"),("Audio track","audio"),("Weather","weather"),("Controllers","controllers"),("Network","network"),("Advanced","advanced"),("Edit favorite apps","favorites"),("Edit favorite channels","favorite-channels")};for(int i=0;i<actions.Length;i++)Card(actions[i].Item1,"setting:"+actions[i].Item2,80+i%4*450,290+i/4*215,410,175,caption:false);Notice("Select a control · Back to Home");}
 void BuildFavorites(){bool apps=page==3;var ids=apps?favorites.Apps:favorites.Channels;for(int i=0;i<Math.Min(ids.Count,21);i++){string id=ids[i];var a=catalog.Apps.Find(x=>x.Id==id);var c=catalog.Channels.Find(x=>x.Id==id);string name=apps?a?.Name:c?.Name;string asset=apps?a?.Asset:c?.Asset;Card(name??"Unavailable favorite","edit:"+id,80+i%5*358,290+i/5*155,328,110,asset??"",caption:false);}Card("+ Add favorite","picker:"+(apps?"apps":"channels"),80+(ids.Count%5)*358,290+(ids.Count/5)*155,328,110,caption:false);Notice("Select a favorite to move or remove it · Select Add favorite to add one");}
 void BuildPicker(){
  bool apps=page==5;
  int count=apps?appChoices.Count:catalog.Channels.Count,start=pickerPage*15;
  for(int i=0;i<Math.Min(15,count-start);i++){
   int x=80+i%3*600,y=285+i/3*140;
   if(apps){
    var app=appChoices[start+i];
    AppListCard(app,"toggle:"+app.Id,x,y,560,110,favorites.Apps.Contains(app.Id)?"✓":"+");
   }else{
    var channel=catalog.Channels[start+i];
    Card((favorites.Channels.Contains(channel.Id)?"✓ ":"+ ")+channel.Name,"toggle:"+channel.Id,x,y,560,110,caption:false);
   }
  }
  if(pickerFromHome)Card("Edit favorites","manage:"+(apps?"apps":"channels"),1400,184,440,68,caption:false);
  Notice(count==0?"No apps with available artwork":count<=15?"Select to add or remove":"Select to add or remove · Page "+(pickerPage+1)+" of "+((count+14)/15)+" · Down at the bottom for more");
 }
 void BuildFavoriteActions(){string name=selectedKind=="apps"?catalog.Apps.Find(x=>x.Id==selectedId)?.Name:catalog.Channels.Find(x=>x.Id==selectedId)?.Name;Text(name??"Favorite",80,290,1600,80,40,ink);Card("Move earlier","move:-1",80,420,500,160,caption:false);Card("Move later","move:1",620,420,500,160,caption:false);Card("Remove favorite","remove",1160,420,600,160,caption:false);Notice("Only the shortcut changes. The app stays installed and the channel stays tuned.");}
 void BuildTracks(){if(lastSource!=0){Notice("Apple TV and streaming apps manage their own subtitles and audio.");Card("Open Samsung settings","setting:advanced",80,330,700,160,caption:false);return;}if(isSubtitle)Card("Off","track:-1",80,290,540,110,caption:false);for(int i=0;i<tracks.Count;i++){int j=i+(isSubtitle?1:0);Card(tracks[i].Name,"track:"+tracks[i].Index,80+j%3*600,290+j/3*140,560,110,caption:false);}if(tracks.Count==0)Notice("No tracks reported for the current channel. Use Advanced settings.");}
 void Focus(bool animate=true){
  if(focusAnimation!=null){focusAnimation.Stop();focusAnimation.Dispose();focusAnimation=null;}if(animate)focusAnimation=new Animation(160);
  for(int i=0;i<tiles.Count;i++){var t=tiles[i];bool f=tabFocus<0&&i==selected;float scale=f&&t.Rail==0?(t.W<=80?1.08f:1.055f):1;
   if(animate)focusAnimation.AnimateTo(t.Card,"Scale",new Vector3(scale,scale,1));else t.Card.Scale=new Vector3(scale,scale,1);
   if(t.FocusFill!=null)t.FocusFill.Opacity=f?1:0;
   t.Card.BorderlineWidth=f&&t.FocusFill==null?2:0;t.Card.BorderlineColor=accent;t.Card.BackgroundColor=f&&t.W<=80?new Color(.94f,.96f,1,.86f):t.NormalColor;
   if(t.Content!=null)t.Content.TextColor=f&&t.W<=80?new Color(.09f,.12f,.17f,1):ink;
   if(t.Caption!=null){t.Caption.TextColor=f?ink:muted;t.Caption.FontFamily=f?"Quiet Inter Semibold":"Quiet Inter Medium";}
  }
  if(focusAnimation!=null)focusAnimation.Play();WriteState("focus");
 }

 void OnKey(object sender,Window.KeyEventArgs e){if(e.Key==null||e.Key.State!=Key.StateType.Down)return;string key=e.Key.KeyPressedName;long now=Environment.TickCount64;if(key==lastKey&&now-lastKeyAt<150)return;lastKey=key;lastKeyAt=now;try{
  if(key=="XF86Home"){Go(0);return;}if(key=="XF86More"||key=="XF86SysMenu"){Setting("advanced");return;}
  if(editing!=""){if(key=="Left"||key=="Right")Adjust(editing,key=="Left"?-1:1);else if(IsBack(key)||IsSelect(key)){editing="";Build();}return;}
  if(IsBack(key)){if(page==0){ReturnToSource();}else if(page==7)Go(selectedKind=="apps"?3:4);else if(page==5||page==6){
   if(!pickerFromHome)Go(page==5?3:4);
   else{
    string addAction=page==5?"picker:apps":"picker:channels";
    Go(0);
    var index=tiles.FindIndex(t=>t.Action==addAction);
    if(index>=0){selected=index;RevealRail(tiles[selected]);Focus();}
   }
  }else Go(0);return;}
  if(page==0&&tabFocus>=0){
   if(key=="Up"||key=="Down"){tabFocus=Math.Clamp(tabFocus+(key=="Down"?1:-1),0,2);Build();}
   else if(key=="Right"||IsSelect(key)){
    int target=tabFocus;
    if(target==2){if(IsSelect(key))Setting("advanced");}
    else if(IsSelect(key)&&target==1){Go(1);_ = RefreshApps();}
    else{
     tabFocus=-1;
     selected=tiles.FindIndex(t=>t.Rail==target+1&&TileVisible(t));
     selected=Math.Max(0,selected);
     RevealRail(tiles[selected]);Build();
    }
   }
   return;
  }
  if(tabFocus>=0){if(key=="Left"||key=="Right"){int target=Math.Clamp(tabFocus+(key=="Left"?-1:1),0,2);if(target==2){Setting("advanced");return;}tabFocus=target;page=tabFocus;selected=0;pickerPage=0;Build();if(page==1)_ = RefreshApps();}else if(key=="Down"||IsSelect(key)){tabFocus=-1;Build();}return;}
  if(IsSelect(key)){if(tiles.Count>0)Activate(tiles[selected].Action);return;}
  if(!new[]{"Left","Right","Up","Down"}.Contains(key)||tiles.Count==0)return;
  if(HandleHomeVertical(key)||HandleHomeHorizontal(key))return;var current=tiles[selected];bool horizontal=key=="Left"||key=="Right";int sign=key=="Left"||key=="Up"?-1:1;double cx=current.X+current.W/2d,cy=current.Y+current.H/2d;
  var choices=tiles.Select((t,i)=>new{Tile=t,Index=i,Dx=t.X+t.W/2d-cx,Dy=t.Y+t.H/2d-cy}).Where(t=>t.Index!=selected&&TileVisible(t.Tile)&&(horizontal?t.Dx:t.Dy)*sign>8).OrderBy(t=>(horizontal?t.Dx:t.Dy)*sign+Math.Abs(horizontal?t.Dy:t.Dx)*3).ToList();if(choices.Count>0){selected=choices[0].Index;if(page==0)RevealRail(tiles[selected]);Focus();}else if(key=="Up"){if((page==1||page==5||page==6)&&pickerPage>0){pickerPage--;selected=0;Build();}else{tabFocus=Math.Clamp(page,0,2);Build();}}else if(key=="Down"&&(page==1||page==5||page==6)){int count=page==6?catalog.Channels.Count:appChoices.Count;if((pickerPage+1)*15<count){pickerPage++;selected=0;Build();}}
 }catch(Exception ex){Notice("Could not complete this action. "+ex.GetBaseException().Message);WriteState("error:"+ex.GetBaseException().Message);}}
 bool IsBack(string k)=>k=="XF86Back"||k=="Back"||k=="Escape"||k=="XF86Exit";
 bool IsSelect(string k)=>k=="Return"||k=="KP_Enter";
 void Go(int p){pickerFromHome=false;page=p;selected=0;pickerPage=0;tabFocus=-1;editing="";Build();}
 void Activate(string action){if(action.StartsWith("app:")){Launch(action[4..]);return;}if(action.StartsWith("source:")){int n=int.Parse(action[7..]);int result=connect_source(n);if(result<0)throw new InvalidOperationException("HDMI switch failed");lastSource=n;lastSourceLabel="Apple TV";Launch("org.tizen.tv-viewer");return;}
  if(action.StartsWith("channel:")){if(tv==null)throw new InvalidOperationException("Channel API unavailable");string id=action[8..];connect_source(0);if(!tv.Tune(id))throw new InvalidOperationException("Channel tune request failed");lastSource=0;lastSourceLabel=catalog.Channels.Find(c=>c.Id==id)?.Name??"Live TV";Launch("org.tizen.tv-viewer");return;}
  if(action.StartsWith("manage:")){Go(action.EndsWith("apps")?3:4);_ = RefreshFavoriteArtwork();return;}if(action.StartsWith("picker:")){bool fromHome=page==0;Go(action.EndsWith("apps")?5:6);pickerFromHome=fromHome;if(page==5)_ = RefreshApps();else RefreshChannels();return;}
  if(action.StartsWith("edit:")){selectedKind=page==3?"apps":"channels";selectedId=action[5..];Go(7);return;}
  if(action.StartsWith("move:")){var list=selectedKind=="apps"?favorites.Apps:favorites.Channels;int old=list.IndexOf(selectedId),next=Math.Clamp(old+int.Parse(action[5..]),0,list.Count-1);if(old>=0){list.RemoveAt(old);list.Insert(next,selectedId);SaveFavorites();Build();Notice("Order saved");}return;}
  if(action=="remove"){(selectedKind=="apps"?favorites.Apps:favorites.Channels).Remove(selectedId);SaveFavorites();Go(selectedKind=="apps"?3:4);return;}
  if(action.StartsWith("toggle:")){var list=page==5?favorites.Apps:favorites.Channels;string id=action[7..];if(list.Contains(id))list.Remove(id);else{if(list.Count>=15)throw new InvalidOperationException("Maximum 15 favorites");list.Add(id);}SaveFavorites();Build();_ = RefreshFavoriteArtwork();return;}
  if(action.StartsWith("track:")){int i=int.Parse(action[6..]);bool ok=isSubtitle?tv.SetSubtitle(i):tv.SetAudio(i);if(!ok)throw new InvalidOperationException("Track change failed. Use Advanced settings.");Notice("Track change requested");return;}
  if(action.StartsWith("setting:"))Setting(action[8..]);
 }
 void Setting(string name){if(name=="advanced"){Launch("org.tizen.menu");return;}if(name=="network"){Launch("org.tizen.menu","networksettings");return;}if(name=="controllers"){Launch("org.tizen.menu","add-bluetooth-keyboard");return;}if(name=="favorites"){Go(3);return;}if(name=="favorite-channels"){Go(4);return;}
  if(name=="weather"){_ = RefreshWeather(true);Notice("Otopeni · Updates every 30 min · Last successful reading stays available offline · Weather by Open-Meteo");return;}
  if(name=="subtitles"||name=="audio"){RefreshSource();isSubtitle=name=="subtitles";tracks=new();try{if(tv!=null&&lastSource==0)tracks=isSubtitle?tv.SubtitleTracks():tv.AudioTracks();}catch{}Go(8);return;}
  if(name=="mute"){if(sound_manager_get_master_mute(out var m)!=0||sound_manager_set_master_mute(m==1?0:1)!=0)throw new InvalidOperationException("Use the remote mute button");Notice(m==1?"Sound on":"Muted");return;}
  if(name=="volume"||name=="brightness"){editing=name;Notice("Left / Right to adjust "+name+" · Select or Back to finish");return;}
  string setting=name=="game"?"game-mode":"sleeptimer";if(!CSharpGetItemEnable(setting,out bool enabled)||!enabled||!CSharpGetItemValue(setting,out var _,out var value))throw new InvalidOperationException("Unavailable for this input. Use Advanced settings.");string wanted=name=="game"?(value=="1"?"0":"1"):((int.Parse(value)+1)%5).ToString();if(!CSharpSetItemValue(setting,wanted))throw new InvalidOperationException("Setting rejected");Notice(name=="game"?"Game Mode "+(wanted=="1"?"On":"Off"):"Sleep timer updated");
 }
 void Adjust(string setting,int delta){if(setting=="volume"){if(sound_manager_get_master_volume(out int n)!=0||sound_manager_set_master_volume(Math.Clamp(n+delta,0,100))!=0)throw new InvalidOperationException("Use the remote volume buttons");Notice("Volume "+Math.Clamp(n+delta,0,100));}else{if(!CSharpGetItemEnable("brightness",out var enabled)||!enabled||!CSharpGetItemValue("brightness",out var _,out var value)||!int.TryParse(value,out int n))throw new InvalidOperationException("Brightness unavailable");if(!CSharpSetItemValue("brightness",Math.Clamp(n+delta,0,50).ToString()))throw new InvalidOperationException("Brightness update rejected");Notice("Brightness "+Math.Clamp(n+delta,0,50));}}
 void Launch(string id,string menu=null){IntPtr c;int r=app_control_create(out c);if(r!=0)throw new InvalidOperationException("Could not create app request");try{r=app_control_set_app_id(c,id);if(r==0)r=app_control_set_operation(c,"http://tizen.org/appcontrol/operation/default");if(r==0&&menu!=null)r=app_control_add_extra_data(c,"menuitem",menu);if(r==0&&menu!=null)r=app_control_add_extra_data(c,"launchoption","pressed");if(r==0)r=app_control_send_launch_request(c,IntPtr.Zero,IntPtr.Zero);if(r!=0)throw new InvalidOperationException("App launch failed: "+r);}finally{app_control_destroy(c);}WriteState("launch:"+id);}

 static bool IsCatalogApp(string id){
  if(id is "com.samsung.tv.store" or "org.tizen.netflix-app" or "org.tizen.primevideo" or "org.tizen.apple.apple-music"
   or "9Ur5IzDKqV.TizenYouTube" or "5b8c3eb16b.BeamCTVDev" or "hgQnxRksVu.myTunerRadioSamsungTV"
   or "kIciSQlYEM.plex" or "rTn6ClEurC.Jellyfin")return true;
  if(string.IsNullOrEmpty(id))return false;
  int dot=id.IndexOf('.');
  if(dot!=10)return false;
  string package=id[..dot];
  if(package is "25emanual2" or "ByJlgt0XeD" or "tv6pbVMQFF" or "WSRedi0001" or "WykOS4t4F0" or "ZODRT12q2l")return false;
  // Samsung Store widgets on this TV use a ten-character package ID and a TMG payload.
  if(!package.All(c=>c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9'))return false;
  try{return Directory.EnumerateFiles("/opt/usr/apps/"+package+"/res","*.tmg").Any();}
  catch(IOException){return false;}
  catch(UnauthorizedAccessException){return false;}
 }
 void UpdateAppChoices(){
  foreach(var app in catalog.Apps){
   if(app.Id=="9Ur5IzDKqV.TizenYouTube"&&HasArtwork("icons/youtube-brand.png"))app.Asset="icons/youtube-brand.png";
   if(app.Id=="com.samsung.tv.store"){app.Name="Samsung Apps";if(HasArtwork("icons/samsung-apps.png"))app.Asset="icons/samsung-apps.png";}
   if(app.Id=="hgQnxRksVu.myTunerRadioSamsungTV"){app.Name="myTuner Radio";if(HasArtwork("icons/mytuner.png"))app.Asset="icons/mytuner.png";}
  }
  appChoices=catalog.Apps.Where(a=>HasArtwork(a.Asset)).OrderBy(a=>a.Name,StringComparer.OrdinalIgnoreCase).ToList();
 }
 async Task RefreshApps(){
  if(appsRefreshing)return;
  appsRefreshing=true;
  var previous=catalog.Apps.ToDictionary(a=>a.Id,StringComparer.Ordinal);
  try{
   var infos=await ApplicationManager.GetInstalledApplicationsAsync().ConfigureAwait(false);
   var items=new List<AppEntry>();
   foreach(var info in infos){
    try{
     string id=info.ApplicationId;
     if(info.IsNoDisplay||string.IsNullOrWhiteSpace(info.Label)||!IsCatalogApp(id))continue;
     previous.TryGetValue(id,out var known);
     string asset=known?.Asset;
     if(id=="9Ur5IzDKqV.TizenYouTube"&&HasArtwork("icons/youtube-brand.png"))asset="icons/youtube-brand.png";
     if(!HasArtwork(asset))asset=info.IconPath;
     if(!HasArtwork(asset)){
      string pkg=id.Split('.')[0];
      asset="/opt/usr/apps/"+pkg+"/shared/res/"+id+".png";
     }
     items.Add(new AppEntry{Id=id,Name=known?.Name??info.Label,Asset=asset,Background=known?.Background??"#30303D"});
    }finally{info.Dispose();}
   }
   ui.Enqueue(()=>{
    appsRefreshing=false;
    catalog.Apps=items.GroupBy(a=>a.Id).Select(g=>g.First()).ToList();
    UpdateAppChoices();
    if(page==1||page==5){
     pickerPage=Math.Min(pickerPage,Math.Max(0,(appChoices.Count-1)/15));
     Build();
    }else if(page==0||page==3)Build();
   });
  }catch{ui.Enqueue(()=>{appsRefreshing=false;if(page==1||page==5)Notice("Using the saved app catalog");});}
 }
 void RefreshChannels(){try{var channels=tv?.Channels();if(channels?.Count>0){foreach(var channel in channels){var known=catalog.Channels.Find(c=>c.Id==channel.Id||c.Name==channel.Name);if(known!=null){channel.Asset=known.Asset;channel.Background=known.Background;}}catalog.Channels=channels;AtomicJson(dataDir+"catalog-cache.json",catalog);if(page==6)Build();}}catch{Notice("Using the saved channel catalog");}}

 async Task RefreshFavoriteArtwork(){if(logoStore==null)return;if(artworkBusy){artworkRefreshPending=true;return;}artworkBusy=true;var wanted=catalog.Channels.Where(c=>favorites.Channels.Contains(c.Id)&&(string.IsNullOrWhiteSpace(c.Asset)||!File.Exists(PathFor(c.Asset)))).Select(c=>(c.Id,c.Name)).ToArray();try{foreach(var channel in wanted){string path=await logoStore.GetChannelAsync(channel.Name).ConfigureAwait(false);if(path==null)continue;ui.Enqueue(()=>{var entry=catalog.Channels.Find(c=>c.Id==channel.Id);if(entry==null)return;entry.Asset=path;AtomicJson(dataDir+"catalog-cache.json",catalog);if(page==0||page==4)Build();});}}catch{}finally{ui.Enqueue(()=>{artworkBusy=false;if(artworkRefreshPending){artworkRefreshPending=false;_ = RefreshFavoriteArtwork();}});}}
 void SaveFavorites(){if(!prefsValid)throw new InvalidOperationException("Existing preferences need recovery before saving");AtomicJson(dataDir+"favorites.json",favorites);}
 static void AtomicJson<T>(string path,T value){string temp=path+".new";File.WriteAllText(temp,JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,path,true);}
 void RefreshSource(){try{if(vconf_get_int("memory/system/source_type",out int n)==0)lastSource=n;lastSourceLabel=ForegroundAppName()??(lastSource==catalog.AppleSource?"Apple TV":lastSource==0?(tv?.CurrentChannel()??"Live TV"):"HDMI");}catch{lastSourceLabel=lastSource==catalog.AppleSource?"Apple TV":"Live TV";}}
 // The compositor lists the foreground windows first. The HDMI setting alone
 // cannot identify a streaming app displayed immediately below Quiet Home.
 [DllImport("libcapi-appfw-app-manager.so.0")]
 static extern int app_manager_get_app_id(int pid,out IntPtr appId);
 [DllImport("libc.so.6")] static extern void free(IntPtr memory);
 [DllImport("libaul.so.0")] static extern int aul_resume_app(string appId);
 string foregroundAppId;
 void ReturnToSource(){
  if(foregroundAppId!=null&&aul_resume_app(foregroundAppId)>=0)return;
  Window.Default.Lower();
 }
 string ForegroundAppName(){
  foregroundAppId=null;
  try{
   using var stack=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("/usr/bin/aul_window","foreach_window_stack"){
    UseShellExecute=false, RedirectStandardOutput=true
   });
   var read=stack.StandardOutput.ReadToEndAsync();
   if(!stack.WaitForExit(350)){
    stack.Kill();
    return null;
   }
   if(stack.ExitCode!=0||!read.Wait(100))return null;
   int pid=0;
   foreach(string entry in read.Result.Split('\n')){
    string line=entry.Trim();
    if(line.StartsWith("- Process ID:"))int.TryParse(line[13..].Trim(),out pid);
    if(line!="- Visibility: 0"||pid<=0||pid==Environment.ProcessId)continue;
    if(app_manager_get_app_id(pid,out var idPointer)!=0||idPointer==IntPtr.Zero)return null;
    string id;
    try{id=Marshal.PtrToStringUTF8(idPointer);}
    finally{free(idPointer);}
    var app=catalog.Apps.Find(candidate=>candidate.Id==id);
    foregroundAppId=app?.Id;
    return app?.Name;
   }
  }catch(Exception){ /* Source detection falls back to the tuner or HDMI. */ }
  return null;
 }
 void UpdateHeader(){if(time==null)return;var now=DateTime.Now;time.Text=now.ToString("HH:mm",CultureInfo.InvariantCulture);date.Text=now.ToString("ddd, d MMM",CultureInfo.GetCultureInfo("en-GB"));source.Text=lastSourceLabel;if(page==0)source.PixelSize=lastSourceLabel.Length>24?18:lastSourceLabel.Length>17?21:27;weather.Text=reading==null?"—":Math.Round(reading.Temperature)+"°  "+WeatherName(reading.Code);weatherPlace.Text="Otopeni"+(reading==null?" · Waiting":weatherError!=""?" · Saved "+Age(reading.UpdatedUtc):"");}
 static string Age(DateTime date){var age=DateTime.UtcNow-date;return age.TotalHours>=1?((int)age.TotalHours)+"h ago":Math.Max(0,(int)age.TotalMinutes)+"m ago";}
 static string WeatherName(int code)=>code==0?"Clear":code<=3?"Cloudy":code<=48?"Fog":code<=67?"Rain":code<=77?"Snow":code<=82?"Showers":code<=86?"Snow":"Storm";
 async Task RefreshWeather(bool force=false){if(weatherBusy||(!force&&DateTime.UtcNow<nextWeather))return;weatherBusy=true;nextWeather=DateTime.UtcNow.AddMinutes(30);try{using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(8)};var text=await http.GetStringAsync("https://api.open-meteo.com/v1/forecast?latitude=44.55&longitude=26.06667&current=temperature_2m,weather_code&timezone=Europe%2FBucharest").ConfigureAwait(false);using var doc=JsonDocument.Parse(text);var current=doc.RootElement.GetProperty("current");double temperature=current.GetProperty("temperature_2m").GetDouble();int code=current.GetProperty("weather_code").GetInt32();if(!double.IsFinite(temperature)||temperature< -60||temperature>65)throw new InvalidDataException();var updated=new WeatherReading{Temperature=temperature,Code=code,UpdatedUtc=DateTime.UtcNow};AtomicJson(dataDir+"weather.json",updated);ui.Enqueue(()=>{reading=updated;weatherError="";UpdateHeader();});}catch{ui.Enqueue(()=>{weatherError="offline";UpdateHeader();});}finally{weatherBusy=false;}}
 void Notice(string message){if(status!=null)status.Text=message;}
 void WriteState(string activity){try{File.WriteAllText(StateFile,DateTime.UtcNow.ToString("o")+"\nactivity="+activity+"\npage="+page+"\nselected="+selected+"\nselected_name="+(tiles.Count>selected?tiles[selected].Name:"")+"\nchannel_scroll="+(channelRail?.Scroll??0)+"\napp_scroll="+(appRail?.Scroll??0)+"\nsource="+lastSource+"\nsource_name="+lastSourceLabel+"\napps_with_icons="+appChoices.Count+"\napp_names="+string.Join(", ",appChoices.Select(a=>a.Name))+"\nfavorite_apps="+(favorites?.Apps.Count??0)+"\nfavorite_channels="+(favorites?.Channels.Count??0)+"\nweather="+(reading==null?"none":reading.Temperature.ToString(CultureInfo.InvariantCulture))+"\nweather_error="+weatherError+"\nsource_overlay="+SourceOverlay.Status+"\nprocess_id="+System.Diagnostics.Process.GetCurrentProcess().Id+"\n");}catch{}}
 public static void Main(string[] args){using var app=new QuietHome();app.Run(args);}
}

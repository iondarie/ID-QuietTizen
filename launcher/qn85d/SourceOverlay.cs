using System.Reflection;
using Tizen.NUI;
// A source-preserving system overlay, like Samsung's native menu. This does
// not change source, master mute, volume, or acquire a media session.
internal static class SourceOverlay {
 static uint sourceHint;
 static bool hintAdded;
 public static string Status { get; private set; } = "not-configured";
 public static void Release(){
  if(!hintAdded)return;
  try{Window.Default.SetAuxiliaryHintValue(sourceHint,"0");Status="paused; tvs_hint="+Window.Default.GetAuxiliaryHintValue(sourceHint);}catch(Exception error){Status="release error: "+error.GetBaseException().Message;}
 }
 public static void Apply() {
  try {
   var assembly=Assembly.LoadFrom("/usr/share/dotnet.tizen/framework/Tizen.TV.Application.dll");
   var context=assembly.GetType("Tizen.TV.Application.AppContextExtension",true);
   var uiType=assembly.GetType("Tizen.TV.Application.UIType",true);
   context.GetMethod("SetUIType",new[]{uiType}).Invoke(null,new[]{Enum.Parse(uiType,"Partial")});
   if(!hintAdded){sourceHint=Window.Default.AddAuxiliaryHint("wm.tvs.win.use.tvs","1");hintAdded=true;}
   bool accepted=Window.Default.SetAuxiliaryHintValue(sourceHint,"1");
   Status=Convert.ToString(context.GetMethod("GetUITypeByApplicationId").Invoke(null,new object[]{"com.samsung.tv.adplayer"}))+"; tvs_hint="+Window.Default.GetAuxiliaryHintValue(sourceHint)+"; accepted="+accepted;

  } catch(Exception error) { Status="error: "+error.GetBaseException().Message; }
 }
}

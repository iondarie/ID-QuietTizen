using System.Reflection;
using System.Collections;
using System.Globalization;
public sealed class ChannelEntry {
 public string Id {get;set;} public string Name {get;set;} public int Number {get;set;} public string Asset {get;set;}=""; public string Background {get;set;}="#29313E";
}
public sealed class AppEntry {
 public string Id {get;set;} public string Name {get;set;} public string Asset {get;set;}=""; public string Background {get;set;}="#30303D";
}
public sealed class TvServices {
 static TvServices(){AppDomain.CurrentDomain.AssemblyResolve+=(sender,args)=>{var path="/usr/share/dotnet.tizen/framework/"+new AssemblyName(args.Name).Name+".dll";return File.Exists(path)?Assembly.LoadFrom(path):null;};}
 readonly Assembly assembly;
 public TvServices(){assembly=Assembly.LoadFrom("/usr/share/dotnet.tizen/framework/Tizen.TV.Broadcast.TVService.dll");}
 Type T(string n)=>assembly.GetType("Tizen.TV.Broadcast.TVService."+n,true);
 public object Make(string n)=>Activator.CreateInstance(T("Types."+n));
 public object EnumValue(string n,string v)=>Enum.Parse(T("Types."+n),v);
 public static object Call(object instance,string method,params object[] args){var found=instance.GetType().GetMethods().Where(m=>m.Name==method&&m.GetParameters().Length==args.Length).ToArray();foreach(var m in found){try{return m.Invoke(instance,args);}catch(ArgumentException){}}throw new MissingMethodException(method);}
 public object API()=>T("TVServiceAPI").GetMethod("Instance").Invoke(null,null);
 public object Navigation()=>Call(API(),"CreateServiceNavigation",EnumValue("Profile","Main"),(ushort)0);
 object Criteria(){var c=Make("CriteriaHelper");foreach(string tag in new[]{"ServiceID","ServiceName","Major","ChannelType"})Call(c,"Fetch",Convert.ToInt32(EnumValue("ServiceDataTag",tag)));return c;}
 public List<ChannelEntry> Channels(){var list=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("Types.ServiceData")));Call(Navigation(),"GetServiceList",Criteria(),list);var result=new List<ChannelEntry>();foreach(var row in list){try{var id=Convert.ToUInt64(Call(row,"GetServiceId")).ToString(CultureInfo.InvariantCulture);var name=(string)Call(row,"GetServiceName");if(!string.IsNullOrWhiteSpace(name)&&!result.Any(x=>x.Id==id))result.Add(new ChannelEntry{Id=id,Name=name,Number=Convert.ToInt32(Call(row,"GetMajor"))});}finally{(row as IDisposable)?.Dispose();}}return result.OrderBy(x=>x.Number).ThenBy(x=>x.Name).ToList();}
 public string CurrentChannel(){var data=Make("ServiceData");try{Call(Navigation(),"GetCurrentServiceInfo",Criteria(),data);return (string)Call(data,"GetServiceName");}finally{(data as IDisposable)?.Dispose();}}
 public bool Tune(string id){var result=Convert.ToInt32(Call(Navigation(),"SetService",ulong.Parse(id,CultureInfo.InvariantCulture),EnumValue("ServiceChangeDirection","Direct"),false,"com.samsung.tv.adplayer"));return result>0;}
 public List<(int Index,string Name)> AudioTracks(){var service=Call(API(),"CreateServiceDataAux",EnumValue("Profile","Main"),(ushort)0);return Tracks(service,false);}
 public List<(int Index,string Name)> SubtitleTracks(){var service=Call(API(),"CreateServiceDataAux",EnumValue("Profile","Main"),(ushort)0);return Tracks(service,true);}
 List<(int Index,string Name)> Tracks(object service,bool subtitle){var nav=Navigation();var data=Make("ServiceData");ulong id;try{Call(nav,"GetCurrentServiceInfo",Criteria(),data);id=Convert.ToUInt64(Call(data,"GetServiceId"));}finally{(data as IDisposable)?.Dispose();}
  var result=new List<(int,string)>();object[] sizeArgs={id,(uint)0};Call(service,subtitle?"SizeOfHybridSBT":"SizeOfAudio",sizeArgs);uint count=Convert.ToUInt32(sizeArgs[1]);if(count>64)throw new InvalidDataException("Unexpected track count");for(uint i=0;i<count;i++){if(subtitle){var sbt=Make("HybridSBT");object[] args={id,i,Enum.ToObject(T("Types.LanguageCode"),0),sbt};Call(service,"GetHybridSBT",args);result.Add(((int)i,args[2].ToString()));}else{var audio=Make("AudioInformation");Call(service,"GetAudio",id,i,audio);string name=Convert.ToString(audio.GetType().GetProperty("audioName").GetValue(audio));if(string.IsNullOrWhiteSpace(name))name=Convert.ToString(audio.GetType().GetProperty("multiSound").GetValue(audio));result.Add(((int)i,name));}}return result;}
 public bool SetAudio(int index)=>Convert.ToInt32(Call(Call(API(),"CreateAVControl",EnumValue("Profile","Main"),(ushort)0),"SetCurrentAudioByIndex",index))>0;
 public bool SetSubtitle(int index)=>Convert.ToInt32(Call(Call(API(),"CreateDataControl",EnumValue("Profile","Main"),(ushort)0),"SetCurrentHybridSBT",index<0?uint.MaxValue:(uint)index))>0;
}

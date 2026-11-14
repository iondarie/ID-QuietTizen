using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
public sealed class LogoStore {
 public string LastError {get;private set;}="";
 readonly string directory; readonly Dictionary<string,string> sources;readonly SemaphoreSlim gate=new(1,1);
 public LogoStore(string sourcePath,string directory){this.directory=directory;Directory.CreateDirectory(directory);try{sources=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(sourcePath))??new();}catch{sources=new();}}
 public static string Normalize(string name){string text=Regex.Replace(name.Trim(),@"\s+(?:HD|FHD|UHD|4K|SD)$","",RegexOptions.IgnoreCase).Normalize(NormalizationForm.FormD);return new string(text.Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark&&char.IsLetterOrDigit(c)).Select(char.ToLowerInvariant).ToArray());}
 public static string Initials(string name){string text=Regex.Replace(name.Trim(),@"\s+(?:HD|FHD|UHD|4K|SD)$","",RegexOptions.IgnoreCase);var words=text.Split(' ',StringSplitOptions.RemoveEmptyEntries);return (words.Length>1?string.Concat(words.Take(2).Select(w=>w[0])):text[..Math.Min(2,text.Length)]).ToUpperInvariant();}
 string BasePath(string name)=>Path.Combine(directory,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(name)))).ToLowerInvariant());
 public string Cached(string name){string path=BasePath(name);foreach(string ext in new[]{".png",".svg",".jpg"})if(File.Exists(path+ext))return path+ext;return null;}
 public static string ImageType(byte[] data){if(data.Length>=8&&data[0]==137&&data[1]==80&&data[2]==78&&data[3]==71)return ".png";if(data.Length>=3&&data[0]==255&&data[1]==216&&data[2]==255)return ".jpg";string svg=Encoding.UTF8.GetString(data);if(!svg.Contains("<svg",StringComparison.OrdinalIgnoreCase))return null;
  if(Regex.IsMatch(svg,@"<!DOCTYPE|<!ENTITY|<script|<foreignObject|<style|@import|\bon\w+\s*=|(?:href|url)\s*[=(]\s*['""]?(?:https?:|//|data:)",RegexOptions.IgnoreCase))return null;return ".svg";}
 public async Task<string> GetChannelAsync(string name){string cached=Cached(name);if(cached!=null)return cached;if(!sources.TryGetValue(Normalize(name),out var url)||!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https")return null;
  await gate.WaitAsync().ConfigureAwait(false);try{cached=Cached(name);if(cached!=null)return cached;using var http=new HttpClient(new HttpClientHandler{UseProxy=false}){Timeout=TimeSpan.FromSeconds(10),MaxResponseContentBufferSize=1048576};http.DefaultRequestHeaders.UserAgent.ParseAdd("QuietHome/1.0");byte[] data=await http.GetByteArrayAsync(uri).ConfigureAwait(false);if(data.Length>1048576)return null;string type=ImageType(data);if(type==null)return null;string path=BasePath(name)+type;File.WriteAllBytes(path+".new",data);File.Move(path+".new",path,true);return path;}catch(Exception error){LastError=error.GetBaseException().Message;return null;}finally{gate.Release();}}
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Petrichor.Core;
using Windows.Networking.BackgroundTransfer;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
namespace Petrichor.App {
 public sealed class DownloadEntry : INotifyPropertyChanged {
  public Track Track {get;set;} public string FileName {get;set;} public bool Completed {get;set;} public bool Failed {get;set;}
  /// <summary>用户选择的下载目录在 FutureAccessList 里的令牌；null 表示应用内部存储。</summary>
  public string FolderToken {get;set;}
  [JsonIgnore] public string Title=>Track?.Name;
  [JsonIgnore] public string Artist=>Track?.ArtistText;
  [JsonIgnore] public double Percent {get;set;}
  [JsonIgnore] public bool Paused {get;set;}
  [JsonIgnore] public string Status=>Completed?"已下载":Failed?"下载失败，点击重试":Paused?"已暂停":Percent>0?"正在下载 · "+(int)Percent+"%":"等待下载";
  public event PropertyChangedEventHandler PropertyChanged;
  internal void Update(){foreach(var name in new[]{nameof(Percent),nameof(Paused),nameof(Completed),nameof(Failed),nameof(Status)})PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));}
 }
 /// <summary>下载目录迁移结果。</summary>
 public sealed class DownloadMigration { public int Moved {get;} public int Failed {get;} public DownloadMigration(int moved,int failed){Moved=moved;Failed=failed;} }
 public sealed class OfflineLibrary {
  private readonly NeteaseClient api;
  private readonly SemaphoreSlim manifestGate=new SemaphoreSlim(1,1),prepareGate=new SemaphoreSlim(1,1),imageGate=new SemaphoreSlim(3,3);
  private readonly Dictionary<string,Task<Uri>> images=new Dictionary<string,Task<Uri>>();
  private readonly Dictionary<long,DownloadOperation> operations=new Dictionary<long,DownloadOperation>();
  private readonly List<DownloadEntry> downloads=new List<DownloadEntry>();
  private readonly HttpClient http; private readonly string folderName;
  private readonly BackgroundTransferGroup group;
  private StorageFolder music,local,cache,covers,lyrics; private Task initialization;
  public event EventHandler Changed;
  public OfflineLibrary(NeteaseClient api,string folderName=null,HttpClient imageHttp=null){this.api=api;this.folderName=folderName;http=imageHttp??new HttpClient {Timeout=TimeSpan.FromSeconds(30)};group=BackgroundTransferGroup.CreateGroup(folderName??"PetrichorMusic");}
  public Task InitializeAsync()=>initialization??(initialization=InitializeCoreAsync());
  private async Task InitializeCoreAsync(){
   var root=folderName==null?ApplicationData.Current.LocalFolder:await ApplicationData.Current.LocalFolder.CreateFolderAsync(folderName,CreationCollisionOption.OpenIfExists);
   music=await root.CreateFolderAsync("Downloads",CreationCollisionOption.OpenIfExists);
   local=await root.CreateFolderAsync("LocalMusic",CreationCollisionOption.OpenIfExists);
   cache=await root.CreateFolderAsync("MusicCache",CreationCollisionOption.OpenIfExists);
   covers=await cache.CreateFolderAsync("Covers",CreationCollisionOption.OpenIfExists);
   lyrics=await cache.CreateFolderAsync("Lyrics",CreationCollisionOption.OpenIfExists);
   var index=await music.TryGetItemAsync("index.json") as StorageFile;
   if(index!=null){try{downloads.AddRange(JsonConvert.DeserializeObject<List<DownloadEntry>>(await FileIO.ReadTextAsync(index))??new List<DownloadEntry>());}catch(JsonException){}}
   foreach(var op in await BackgroundDownloader.GetCurrentDownloadsForTransferGroupAsync(group)){
    var entry=downloads.FirstOrDefault(x=>!x.Completed&&x.FileName==op.ResultFile.Name);
    if(entry!=null){operations[entry.Track.Id]=op;entry.Failed=false;entry.Paused=op.Progress.Status==BackgroundTransferStatus.PausedByApplication;ObserveAsync(entry,op,false);}
   }
   foreach(var entry in downloads.Where(x=>!x.Completed&&!operations.ContainsKey(x.Track.Id)))entry.Failed=true;
   Changed?.Invoke(this,EventArgs.Empty);
  }
  private async Task SaveAsync(){
   await manifestGate.WaitAsync();
   try{var pending=await music.CreateFileAsync("index.pending",CreationCollisionOption.ReplaceExisting);await FileIO.WriteTextAsync(pending,JsonConvert.SerializeObject(downloads));await pending.RenameAsync("index.json",NameCollisionOption.ReplaceExisting);}
   finally{manifestGate.Release();}
  }
  public async Task<List<DownloadEntry>> GetDownloadsAsync(){await InitializeAsync();return downloads.ToList();}
  public async Task<bool> IsDownloadedAsync(long id){await InitializeAsync();return downloads.Any(x=>x.Track.Id==id&&x.Completed);}
  public async Task DownloadAsync(Track track,AudioQuality quality,CancellationToken ct=default(CancellationToken)){
   if(track==null||track.Id<=0)throw new InvalidOperationException("Choose an online track");
   await InitializeAsync();await prepareGate.WaitAsync(ct);
   try{
    var entry=downloads.FirstOrDefault(x=>x.Track.Id==track.Id);
    if(entry?.Completed==true||operations.ContainsKey(track.Id))return;
    var stream=await api.GetStreamAsync(track.Id,quality,ct);
    if(stream.IsPreview)throw new MusicApiException(460,"Preview only");
    ct.ThrowIfCancellationRequested();
    string format=(stream.Format??"mp3").ToLowerInvariant();
    string extension=new[]{"mp3","flac","aac","m4a","ogg","wav"}.Contains(format)?"."+format:".mp3";
    var destination=await music.CreateFileAsync(track.Id+extension,CreationCollisionOption.ReplaceExisting);
    if(entry==null){entry=new DownloadEntry {Track=track};downloads.Add(entry);}
    entry.FileName=destination.Name;entry.Completed=false;entry.Failed=false;entry.Percent=0;entry.Paused=false;
    entry.FolderToken=await GetDownloadFolderTokenAsync();
    var operation=new BackgroundDownloader {TransferGroup=group}.CreateDownload(stream.Url,destination);
    operations[track.Id]=operation;
    await SaveAsync();entry.Update();Changed?.Invoke(this,EventArgs.Empty);ObserveAsync(entry,operation,true);
   }finally{prepareGate.Release();}
  }
  private async void ObserveAsync(DownloadEntry entry,DownloadOperation operation,bool start){
   try{
    var progress=new Progress<DownloadOperation>(op=>{entry.Percent=op.Progress.TotalBytesToReceive==0?0:100d*op.Progress.BytesReceived/op.Progress.TotalBytesToReceive;entry.Paused=op.Progress.Status==BackgroundTransferStatus.PausedByApplication;entry.Update();});
    if(start)await operation.StartAsync().AsTask(progress);else await operation.AttachAsync().AsTask(progress);
    var response=operation.GetResponseInformation();
    if(response.StatusCode<200||response.StatusCode>=300||(await operation.ResultFile.GetBasicPropertiesAsync()).Size==0)throw new InvalidOperationException("Invalid download response");
    entry.Completed=true;entry.Failed=false;entry.Percent=100;entry.Track.LocalFilePath=operation.ResultFile.Path;
    // 下载期间条目被删除（例如用户在下载页删掉这首歌）时不再搬运文件，也不刷新歌词封面
    if(!downloads.Contains(entry))return;
    await MoveToChosenFolderAsync(entry,operation.ResultFile);
   }catch(Exception error){entry.Failed=true;entry.Completed=false;await App.LogAsync(error);}
   finally{
    // 只清理自己的传输句柄，避免误删同名歌曲重新下载后的新任务
    DownloadOperation current;
    if(operations.TryGetValue(entry.Track.Id,out current)&&ReferenceEquals(current,operation))operations.Remove(entry.Track.Id);
    entry.Update();
    try{await SaveAsync();}catch(Exception error){await App.LogAsync(error);}
    Changed?.Invoke(this,EventArgs.Empty);
   }
   if(entry.Completed){try{await GetLyricsAsync(entry.Track);await GetCoverAsync(entry.Track.Album.CoverUrl);}catch(Exception error){await App.LogAsync(error);}}
  }
  public void TogglePause(DownloadEntry entry){
   DownloadOperation op;if(!operations.TryGetValue(entry.Track.Id,out op))return;
   if(op.Progress.Status==BackgroundTransferStatus.PausedByApplication){op.Resume();entry.Paused=false;}
   else if(op.Progress.Status==BackgroundTransferStatus.Running){op.Pause();entry.Paused=true;}
   entry.Update();
  }
  public async Task<StorageFile> FindAudioAsync(Track track){
   await InitializeAsync();
   var entry=downloads.FirstOrDefault(x=>x.Track.Id==track.Id&&x.Completed);
   if(entry!=null){var found=await FindStoredAsync(entry);if(found!=null)return found;}
   return await OpenByPathAsync(track.LocalFilePath);
  }
  /// <summary>按条目记录的位置找文件：先所选目录，再应用内部存储。</summary>
  private async Task<StorageFile> FindStoredAsync(DownloadEntry entry){
   var chosen=await OpenFolderAsync(entry.FolderToken);
   return (chosen==null?null:await chosen.TryGetItemAsync(entry.FileName) as StorageFile)??await music.TryGetItemAsync(entry.FileName) as StorageFile;
  }
  // 外部目录（SD 卡）按路径打开会抛 UnauthorizedAccessException 而不是 FileNotFound
  private static async Task<StorageFile> OpenByPathAsync(string path){
   if(string.IsNullOrWhiteSpace(path))return null;
   try{return await StorageFile.GetFileFromPathAsync(path);}
   catch(Exception error)when(error is System.IO.FileNotFoundException||error is UnauthorizedAccessException||error is ArgumentException){return null;}
  }
  /// <summary>删除一首下载（含下载中、失败）的条目：移除本地音频与歌词缓存，并从下载列表移除。返回 false 表示条目不存在或音频删除失败（可能正在播放或使用中），此时条目保留以便重试。</summary>
  public async Task<bool> DeleteAsync(long trackId){
   await InitializeAsync();
   var entry=downloads.FirstOrDefault(x=>x.Track.Id==trackId);
   if(entry==null)return false;
   // 后台传输没有取消 API：先暂停并解除引用，条目移除后不会再被续传或重试
   DownloadOperation operation;
   if(operations.TryGetValue(trackId,out operation)){
    try{if(operation.Progress.Status==BackgroundTransferStatus.Running)operation.Pause();}catch(Exception error){await App.LogAsync(error);}
    operations.Remove(trackId);
   }
   var file=await FindStoredAsync(entry)??await OpenByPathAsync(entry.Track.LocalFilePath);
   if(file!=null){
    try{await file.DeleteAsync();}
    catch(Exception error){await App.LogAsync(error);return false;}
   }
   var lyric=await lyrics.TryGetItemAsync(entry.Track.Id+".json") as StorageFile;
   if(lyric!=null){try{await lyric.DeleteAsync();}catch(Exception error){await App.LogAsync(error);}}
   entry.Track.LocalFilePath=null;
   downloads.Remove(entry);
   await SaveAsync();
   Changed?.Invoke(this,EventArgs.Empty);
   return true;
  }
  private const string FolderSetting="downloadFolderToken";
  private static async Task<StorageFolder> OpenFolderAsync(string token){
   if(string.IsNullOrEmpty(token)||!Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.ContainsItem(token))return null;
   try{return await Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);}
   catch(Exception error)when(error is System.IO.FileNotFoundException||error is UnauthorizedAccessException){return null;}
  }
  private static async Task<string> GetDownloadFolderTokenAsync(){
   var token=ApplicationData.Current.LocalSettings.Values[FolderSetting] as string;
   return await OpenFolderAsync(token)==null?null:token;
  }
  /// <returns>当前生效的下载目录；null 表示应用内部存储（也包括所选目录已不可用，比如拔掉了 SD 卡）。</returns>
  public Task<StorageFolder> GetDownloadFolderAsync()=>OpenFolderAsync(ApplicationData.Current.LocalSettings.Values[FolderSetting] as string);
  public bool HasCustomDownloadFolder=>ApplicationData.Current.LocalSettings.Values[FolderSetting] is string;
  /// <param name="folder">null 恢复为应用内部存储。旧目录的授权保留，已下载到那里的歌曲仍可播放。</param>
  public void SetDownloadFolder(StorageFolder folder){
   if(folder==null){ApplicationData.Current.LocalSettings.Values.Remove(FolderSetting);return;}
   var token=TokenFor(folder);
   Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace(token,folder);
   ApplicationData.Current.LocalSettings.Values[FolderSetting]=token;
  }
  /// <summary>目录令牌由路径（或 FolderRelativeId）派生，同一个目录每次得到同一个令牌。</summary>
  private static string TokenFor(StorageFolder folder)=>"download-"+Hash(string.IsNullOrEmpty(folder.Path)?folder.FolderRelativeId:folder.Path).Substring(0,16);
  /// <summary>已下载歌曲中有多少首不在当前生效的下载目录，需要迁移。</summary>
  public async Task<int> CountOutsideDownloadFolderAsync(){
   await InitializeAsync();
   var token=await GetDownloadFolderTokenAsync();
   return downloads.Count(x=>x.Completed&&x.FolderToken!=token);
  }
  /// <summary>把已下载歌曲迁移到指定目录；folder=null 表示迁回应用内部存储。迁移失败（正在播放、SD 卡已拔出等）的条目保留在原位置。</summary>
  public async Task<DownloadMigration> MigrateDownloadsAsync(StorageFolder folder=null){
   await InitializeAsync();
   var target=folder??music;string targetToken=folder==null?null:TokenFor(folder);
   int moved=0,failed=0;
   foreach(var entry in downloads.Where(x=>x.Completed).ToList()){
    if(entry.FolderToken==targetToken)continue;
    var source=await FindStoredAsync(entry)??await OpenByPathAsync(entry.Track.LocalFilePath);
    if(source==null){failed++;continue;}
    var name=SafeFileName(entry.Track.ArtistText+" - "+entry.Track.Name)+System.IO.Path.GetExtension(source.Name);
    try{
     await source.MoveAsync(target,name,NameCollisionOption.GenerateUniqueName);
     entry.FileName=source.Name;entry.FolderToken=targetToken;entry.Track.LocalFilePath=source.Path;moved++;
    }catch(Exception error){failed++;await App.LogAsync(error);}
   }
   if(moved>0)await SaveAsync();
   Changed?.Invoke(this,EventArgs.Empty);
   return new DownloadMigration(moved,failed);
  }
  private static string SafeFileName(string value){
   var invalid=System.IO.Path.GetInvalidFileNameChars();var clean=new string(value.Select(c=>invalid.Contains(c)?'_':c).ToArray()).Trim().TrimEnd('.');
   return clean.Length==0?"未命名":clean.Length>120?clean.Substring(0,120):clean;
  }
  // BackgroundDownloader 不保证能写可移动存储上的选取目录，所以先下到应用目录，完成后再移过去
  private async Task MoveToChosenFolderAsync(DownloadEntry entry,IStorageFile file){
   var folder=await OpenFolderAsync(entry.FolderToken);
   if(folder==null){entry.FolderToken=null;return;}
   var stored=file as StorageFile??await music.GetFileAsync(file.Name);
   var name=SafeFileName(entry.Track.ArtistText+" - "+entry.Track.Name)+System.IO.Path.GetExtension(stored.Name);
   try{
    await stored.MoveAsync(folder,name,NameCollisionOption.GenerateUniqueName);
    entry.FileName=stored.Name;entry.Track.LocalFilePath=stored.Path;
   }catch(Exception error){entry.FolderToken=null;await App.LogAsync(error);App.NotifyDownloadFallback(entry.Track.Name);}
  }
  private static string Hash(string value){var p=HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha256);return CryptographicBuffer.EncodeToHexString(p.HashData(CryptographicBuffer.ConvertStringToBinary(value,BinaryStringEncoding.Utf8)));}
  public Task<Uri> GetCoverAsync(string url){
   Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri))return Task.FromResult<Uri>(null);
   if(uri.Scheme=="ms-appdata"||uri.Scheme=="ms-appx")return Task.FromResult(uri);
   if(uri.Scheme!="http"&&uri.Scheme!="https")return Task.FromResult<Uri>(null);
   Task<Uri> task;lock(images){if(!images.TryGetValue(url,out task))images[url]=task=LoadCoverAsync(url);}return task;
  }
  private async Task<Uri> LoadCoverAsync(string url){
   await InitializeAsync();string name=Hash(url)+".img";
   var found=await covers.TryGetItemAsync(name) as StorageFile;if(found!=null)return CoverUri(name);
   await imageGate.WaitAsync();
   try{
    found=await covers.TryGetItemAsync(name) as StorageFile;if(found!=null)return CoverUri(name);
    using(var response=await http.GetAsync(url,HttpCompletionOption.ResponseHeadersRead)){
     response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>8*1024*1024)throw new InvalidOperationException("Image too large");
     var bytes=await response.Content.ReadAsByteArrayAsync();if(bytes.Length==0||bytes.Length>8*1024*1024)throw new InvalidOperationException("Invalid image");
     var pending=await covers.CreateFileAsync(name+".pending",CreationCollisionOption.ReplaceExisting);await FileIO.WriteBytesAsync(pending,bytes);await pending.RenameAsync(name,NameCollisionOption.ReplaceExisting);
    }
    return CoverUri(name);
   }catch{lock(images){images.Remove(url);}return null;}finally{imageGate.Release();}
  }
  private Uri CoverUri(string name)=>new Uri("ms-appdata:///local/"+(folderName==null?"":folderName+"/")+"MusicCache/Covers/"+name);
  public async Task<Lyrics> GetLyricsAsync(Track track,CancellationToken ct=default(CancellationToken)){
   await InitializeAsync();var existing=await lyrics.TryGetItemAsync(track.Id+".json") as StorageFile;
   if(existing!=null){try{return JsonConvert.DeserializeObject<Lyrics>(await FileIO.ReadTextAsync(existing))??EmptyLyrics();}catch(JsonException){}}
   if(track.Id<=0)return EmptyLyrics();
   var result=await api.GetLyricsAsync(track.Id,ct);ct.ThrowIfCancellationRequested();
   var pending=await lyrics.CreateFileAsync(track.Id+".pending",CreationCollisionOption.ReplaceExisting);await FileIO.WriteTextAsync(pending,JsonConvert.SerializeObject(result));await pending.RenameAsync(track.Id+".json",NameCollisionOption.ReplaceExisting);return result;
  }
  private static Lyrics EmptyLyrics()=>new Lyrics {Lines=new List<LyricLine>(),TranslatedLines=new List<LyricLine>()};
  public async Task ImportLyricsAsync(Track track,StorageFile file){
   await InitializeAsync();var text=await FileIO.ReadTextAsync(file);var result=new Lyrics {Original=text,Lines=LrcParser.Parse(text),TranslatedLines=new List<LyricLine>()};
   var target=await lyrics.CreateFileAsync(track.Id+".json",CreationCollisionOption.ReplaceExisting);await FileIO.WriteTextAsync(target,JsonConvert.SerializeObject(result));
  }
  public async Task<List<Track>> ImportAsync(IEnumerable<StorageFile> files){
   await InitializeAsync();var added=new List<Track>();
   foreach(var source in files){
    var file=await source.CopyAsync(local,source.Name,NameCollisionOption.GenerateUniqueName);
    var p=await file.Properties.GetMusicPropertiesAsync();
    var track=new Track {Id=-Math.Abs(DateTimeOffset.UtcNow.Ticks),Name=string.IsNullOrWhiteSpace(p.Title)?file.DisplayName:p.Title,LocalFilePath=file.Path,DurationMs=(int)Math.Min(int.MaxValue,p.Duration.TotalMilliseconds),Artists=new List<Artist>{new Artist{Name=string.IsNullOrWhiteSpace(p.Artist)?"未知歌手":p.Artist}},Album=new Album{Name=string.IsNullOrWhiteSpace(p.Album)?"本地音乐":p.Album}};
    using(var thumbnail=await file.GetThumbnailAsync(ThumbnailMode.MusicView,360,ThumbnailOptions.UseCurrentScale)){
     if(thumbnail!=null&&thumbnail.Type==ThumbnailType.Image){
      string name="local-"+Math.Abs(track.Id)+".jpg";var cover=await covers.CreateFileAsync(name,CreationCollisionOption.ReplaceExisting);
      using(var output=await cover.OpenAsync(FileAccessMode.ReadWrite)){await Windows.Storage.Streams.RandomAccessStream.CopyAndCloseAsync(thumbnail.GetInputStreamAt(0),output.GetOutputStreamAt(0));}
      track.Album.CoverUrl=CoverUri(name).ToString();
     }
    }
    var metadata=await local.CreateFileAsync(file.Name+".json",CreationCollisionOption.ReplaceExisting);await FileIO.WriteTextAsync(metadata,JsonConvert.SerializeObject(track));added.Add(track);
   }
   Changed?.Invoke(this,EventArgs.Empty);return added;
  }
  public async Task<List<Track>> GetLocalSongsAsync(){
   await InitializeAsync();var tracks=downloads.Where(x=>x.Completed).Select(x=>x.Track).ToList();
   foreach(var file in await local.GetFilesAsync())if(file.Name.EndsWith(".json",StringComparison.OrdinalIgnoreCase)){
    try{var t=JsonConvert.DeserializeObject<Track>(await FileIO.ReadTextAsync(file));if(t!=null)tracks.Add(t);}catch(JsonException){}
   }
   return tracks.OrderBy(t=>t.Name).ToList();
  }
  public async Task<ulong> CacheSizeAsync(){await InitializeAsync();ulong size=0;foreach(var dir in new[]{covers,lyrics})foreach(var file in await dir.GetFilesAsync())size+=(await file.GetBasicPropertiesAsync()).Size;return size;}
  public async Task ClearCacheAsync(){
   await InitializeAsync();
   // Imported artwork and local lyrics are user data, just like imported/downloaded music.
   foreach(var dir in new[]{covers,lyrics})foreach(var file in await dir.GetFilesAsync())if(!file.Name.StartsWith("local-",StringComparison.OrdinalIgnoreCase)&&!file.Name.StartsWith("-",StringComparison.Ordinal))await file.DeleteAsync();
   lock(images){images.Clear();}
  }
 }
}





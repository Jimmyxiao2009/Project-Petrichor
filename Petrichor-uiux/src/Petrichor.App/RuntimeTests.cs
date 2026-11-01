#if DEBUG
using System;using System.Collections.Generic;using System.Linq;using System.Threading.Tasks;using Newtonsoft.Json;using Petrichor.Core;using Petrichor.App.Pages;using Windows.Storage;using Windows.UI.Xaml;using Windows.UI.Xaml.Media.Imaging;using Windows.Graphics.Imaging;using System.Runtime.InteropServices.WindowsRuntime;
namespace Petrichor.App {internal static class RuntimeTests {
private sealed class UnavailableHandler : System.Net.Http.HttpMessageHandler {public int Calls; protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage r,System.Threading.CancellationToken ct) {Calls++;return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new System.Net.Http.StringContent("{\"code\":200,\"data\":[{\"id\":1,\"url\":null}]}")});}} private static bool running;private static StorageFolder folder;private static readonly List<object> results=new List<object>();
private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
private static async Task Check(string name,Func<Task> test){try{await test();results.Add(new{name,status="PASS"});}catch(Exception e){results.Add(new{name,status="FAIL",error=e.GetType().Name,message=e.Message});}await FileIO.WriteTextAsync(await folder.CreateFileAsync("results.json",CreationCollisionOption.ReplaceExisting),JsonConvert.SerializeObject(results,Formatting.Indented));}
private static async Task<T> Go<T>(object parameter=null)where T:MusicPage{Shell.Current.Navigate(typeof(T),parameter??(object)true);var p=(T)Shell.Current.NavigationFrame.Content;await p.LoadTask;await Task.Delay(500);return p;}
private static async Task Capture(string name){Window.Current.Content.UpdateLayout();var bitmap=new RenderTargetBitmap();var render=bitmap.RenderAsync(Window.Current.Content).AsTask();Require(await Task.WhenAny(render,Task.Delay(10000))==render,"Rendering timed out while window was inactive");await render;Require(bitmap.PixelWidth>0,"No rendered pixels");var pixels=await bitmap.GetPixelsAsync();var file=await folder.CreateFileAsync(name+".png",CreationCollisionOption.ReplaceExisting);using(var stream=await file.OpenAsync(FileAccessMode.ReadWrite)){var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,pixels.ToArray());await encoder.FlushAsync();}}
public static async Task RunAsync(){if(running)return;running=true;App.IsDiagnosticsRunning=true;results.Clear();var execution=new Windows.ApplicationModel.ExtendedExecution.ExtendedExecutionSession {Reason=Windows.ApplicationModel.ExtendedExecution.ExtendedExecutionReason.Unspecified,Description="Petrichor 开发版原生集成测试"};await execution.RequestExtensionAsync();folder=await ApplicationData.Current.LocalFolder.CreateFolderAsync("TestResults",CreationCollisionOption.OpenIfExists);List<Track> songs=null;Playlist selected=null;Track downloaded=null;
try{
await Check("Discovery XAML + native encrypted API",async()=>{var p=await Go<DiscoverPage>();Require(p.ItemCount>0,"No recommendations");await Capture("01-discover");});
await Check("Search XAML + real results",async()=>{var p=await Go<SearchPage>("晴天");Require(p.ItemCount>0,"No search results");songs=(await App.Api.SearchTracksAsync("晴天",5)).Items;Require(songs.Count>0,"No tracks");await Capture("02-search");});
await Check("Artist native Pivot + albums",async()=>{Require(songs!=null,"Search unavailable");var p=await Go<ArtistPage>(songs[0].Artists[0].Id);Require(p.TrackCount>0,"No artist songs");await Capture("03-artist");});
await Check("Album XAML + tracks",async()=>{Require(songs!=null,"Search unavailable");var p=await Go<AlbumPage>(songs[0].Album.Id);Require(p.TrackCount>0,"No album tracks");await Capture("04-album");});
await Check("Playlist plaza categories",async()=>{var p=await Go<PlaylistListPage>();Require(p.ItemCount>0,"No playlists");selected=(await App.Api.GetPlaylistsAsync("全部",3)).Items.First();await Capture("05-playlists");});
await Check("Playlist detail loads all tracks in one visit",async()=>{Require(selected!=null,"No playlist");var p=await Go<PlaylistPage>(selected);Require(p.TrackCount>30,"Playlist still limited to first page");await Capture("06-playlist");});
await Check("Playlist multi-selection preserves checked tracks",async()=>{var p=await Go<PlaylistPage>(selected);Require(p.TrackCount>=2,"Insufficient playlist tracks");p.SelectFirstTwo();Require(p.SelectedCount==2,"Multi-selection did not select two rows");await Capture("18-playlist-selected");p.SetSelectionMode(false);Require(p.SelectedCount==0,"Selection not cleared");});
await Check("Comments XAML and real public conversation",async()=>{Require(songs!=null,"No track");var p=await Go<CommentsPage>(songs[0]);Require(p.ItemCount>0,"No comments");await Capture("17-comments");});
await Check("Comment replies thread loads real conversation",async()=>{Require(songs!=null,"No track");var page=await App.Api.GetCommentsAsync(CommentThread.ForTrack(songs[0]),Petrichor.Core.CommentSort.Recommended,1,20,null);var source=page.Items.FirstOrDefault(c=>c.ReplyCount>0)??page.Items.FirstOrDefault();Require(source!=null,"No comments to open");var p=await Go<CommentRepliesPage>(new CommentThreadArgs{Track=songs[0],Parent=CommentEntry.From(source)});if(source.ReplyCount>0)Require(p.ReplyCount>0,"No replies loaded");await Capture("22-comment-replies");});
await Check("Podcast list loads real hot radios",async()=>{var p=await Go<PodcastsPage>();Require(p.ItemCount>0,"No podcasts");await Capture("20-podcasts");});
await Check("Podcast detail loads real programs",async()=>{var radios=(await App.Api.GetHotRadiosAsync(10,0)).Items;Require(radios.Count>0,"No podcasts");var radio=radios.FirstOrDefault(r=>r.ProgramCount>0)??radios[0];var p=await Go<PodcastPage>(new PodcastRef{Id=radio.Id,Name=radio.Name});Require(p.EpisodeCountShown>0,"No episodes");await Capture("21-podcast");});
await Check("QR challenge + locally generated PNG",async()=>{var p=await Go<LoginPage>();Require(p.HasQr,"No QR bitmap");await Capture("07-login");});
await Check("Home library page reflects restored session",async()=>{Shell.Current.Navigate(typeof(LibraryPage));var p=(LibraryPage)Shell.Current.NavigationFrame.Content;var nickname=(Windows.UI.Xaml.Controls.TextBlock)p.FindName("Nickname");var playlists=(Windows.UI.Xaml.Controls.ListView)p.FindName("Playlists");string expected=App.Api.CurrentUser?.Nickname??"登录云音乐";var deadline=DateTimeOffset.UtcNow.AddSeconds(15);while((nickname.Text!=expected||(App.Api.IsLoggedIn&&playlists.Items.Count==0))&&DateTimeOffset.UtcNow<deadline)await Task.Delay(200);Require(nickname.Text==expected,"Library shows '"+nickname.Text+"' instead of '"+expected+"'");Require(!App.Api.IsLoggedIn||playlists.Items.Count>0,"Logged in but no playlists");});
await Check("Liked songs follow liked playlist order",async()=>{if(!App.Api.IsLoggedIn)return;var me=App.Api.CurrentUser.Id;var mine=(await App.Api.GetMyPlaylistsAsync(30,0)).Where(x=>x.Creator?.Id==me).ToList();var liked=mine.FirstOrDefault(x=>x.SpecialType==5)??mine.FirstOrDefault();Require(liked!=null,"No liked playlist");var order=(await App.Api.GetPlaylistAsync(liked.Id)).TrackIds;var p=await Go<CollectionPage>("likes");var items=((Windows.UI.Xaml.Controls.ListView)p.FindName("Items")).Items.Cast<MusicItem>().Select(x=>((Track)x.Value).Id).ToList();Require(items.Count>0,"No liked songs");var expected=order.Where(new HashSet<long>(items).Contains).Take(20).ToList();Require(items.Take(expected.Count).SequenceEqual(expected),"Liked songs not in playlist order");});
await Check("Library / unauthenticated account state",async()=>{await Go<LibraryPage>();await Capture("08-library");});
await Check("Settings / mobile layout",async()=>{await Go<SettingsPage>();await Capture("09-settings");});
await Check("Native offline music and downloads page",async()=>{await Go<DownloadsPage>();await Capture("14-downloads");});
await Check("Persistent artwork / lyrics cache and imported local audio without network",OfflineTests.RunAsync);
await Check("Native MediaPlayer plays real network audio",async()=>{Require(songs!=null,"Search unavailable");await App.Playback.PlayAsync(songs);var session=App.Playback.Player.PlaybackSession;var deadline=DateTimeOffset.UtcNow.AddSeconds(18);while(session.PlaybackState!=Windows.Media.Playback.MediaPlaybackState.Playing&&DateTimeOffset.UtcNow<deadline)await Task.Delay(200);Require(session.PlaybackState==Windows.Media.Playback.MediaPlaybackState.Playing,"MediaPlayer did not play");await Task.Delay(1200);Require(session.Position.TotalMilliseconds>300,"Playback position did not advance");await Go<PlayerPage>();await Capture("10-player");});
await Check("Sleep timer countdown on player",async()=>{try{PendingLogic.SetSleepTimer(TimeSpan.FromMinutes(30),false);Require(PendingLogic.SleepActive,"Timer not active");await Go<PlayerPage>();await Task.Delay(600);await Capture("23-player-sleep");}finally{PendingLogic.SetSleepTimer(null,false);}Require(!PendingLogic.SleepActive,"Timer not cleared");});
await Check("Pause / resume / seek native playback",async()=>{var session=App.Playback.Player.PlaybackSession;Require(session.NaturalDuration.TotalSeconds>5,"Media unavailable");App.Playback.Pause();await Task.Delay(300);Require(session.PlaybackState==Windows.Media.Playback.MediaPlaybackState.Paused,"Pause failed");App.Playback.Seek(TimeSpan.FromSeconds(5));await App.Playback.ResumeAsync();await Task.Delay(1000);Require(session.Position.TotalSeconds>=5,"Seek/resume failed");});
await Check("Synchronized lyric lines + seeking",async()=>{var p=await Go<LyricsPage>();Require(p.LineCount>0,"No timed lyrics");App.Playback.Seek(TimeSpan.FromSeconds(45));await Task.Delay(500);p.UpdatePosition();Require(p.ActiveIndex>=0,"No active lyric");await Capture("11-lyrics");});
await Check("Lyric playback-controls choice frees content space",async()=>{await Go<LyricsPage>();bool original=Shell.Current.LyricsControlsVisible;try{Shell.Current.SetLyricsControlsVisible(false);Require(((Windows.UI.Xaml.Controls.Border)Shell.Current.FindName("MiniPlayer")).Visibility==Visibility.Collapsed,"Playback bar not hidden");await Capture("19-lyrics-hidden");Shell.Current.SetLyricsControlsVisible(true);Require(((Windows.UI.Xaml.Controls.Border)Shell.Current.FindName("MiniPlayer")).Visibility==Visibility.Visible,"Playback bar did not return");}finally{Shell.Current.SetLyricsControlsVisible(original);}});
await Check("Queue XAML + current track",async()=>{var p=await Go<QueuePage>();Require(p.TrackCount==App.Playback.Queue.Tracks.Count&&p.TrackCount>0,"Queue mismatch");await Capture("12-queue");});
await Check("360x640 viewport / two-column covers",async()=>{var view=Windows.UI.ViewManagement.ApplicationView.GetForCurrentView();Require(view.TryResizeView(new Windows.Foundation.Size(360,640)),"Resize rejected");var p=await Go<DiscoverPage>();Require(p.ActualWidth>=310&&p.ActualWidth<=370,"Unexpected narrow viewport");var list=(Windows.UI.Xaml.Controls.GridView)p.FindName("Recommendations");Require(list.Items.Count>1,"No covers");var first=(MusicItem)list.Items[0];Require((first.TileWidth+12)*2<=list.ActualWidth+1,"Two columns overflow");await Capture("13-narrow-discover");view.TryResizeView(new Windows.Foundation.Size(420,800));}); await Check("Native next / previous commands",async()=>{Require(App.Playback.Queue.Tracks.Count>1,"No queue");var first=App.Playback.Queue.Current.Id;await App.Playback.NextAsync();Require(App.Playback.Queue.Current.Id!=first,"Next did not move");await App.Playback.PreviousAsync();Require(App.Playback.Queue.Current.Id==first,"Previous did not restore");}); await Check("Unavailable song clears stale media and retries",async()=>{var handler=new UnavailableHandler();using(var client=new NeteaseClient(new Petrichor.Uwp.WindowsCryptoProvider(),new MemorySessionStore(),handler))using(var player=new Petrichor.Uwp.PlaybackService(client)){player.Player.Source=Windows.Media.Core.MediaSource.CreateFromUri(new Uri("https://music.163.com/"));try {await player.PlayAsync(new[]{new Track{Id=1,Name="Unavailable test"}});throw new Exception("Unavailable song accepted");}catch(MusicApiException){}Require(player.Player.Source==null&&player.CurrentStream==null,"Stale source retained");try{await player.ResumeAsync();throw new Exception("Retry accepted unavailable song");}catch(MusicApiException){}Require(handler.Calls==2,"Play retry did not request a new URL");}}); await Check("Playback history recorded",async()=>{var history=await App.History.ReadAsync();Require(history.Any(x=>x.Track.Id==App.Playback.Queue.Current?.Id),"History missing current track");});
await Check("Real BackgroundDownloader completion and offline media source",async()=>{
var found=(await App.Api.SearchTracksAsync("The Devil Incarnate",5)).Items;var candidate=found.FirstOrDefault(x=>x.ArtistText.Contains("Laur"))??found.FirstOrDefault();
Require(candidate!=null,"No download candidate");await App.Offline.DownloadAsync(candidate,AudioQuality.Standard);
var deadline=DateTimeOffset.UtcNow.AddSeconds(60);
while(!await App.Offline.IsDownloadedAsync(candidate.Id)&&DateTimeOffset.UtcNow<deadline){var entry=(await App.Offline.GetDownloadsAsync()).FirstOrDefault(x=>x.Track.Id==candidate.Id);Require(entry==null||!entry.Failed,"Background transfer failed");await Task.Delay(300);}
Require(await App.Offline.IsDownloadedAsync(candidate.Id),"Download did not complete");
downloaded=candidate;
await App.Playback.PlayAsync(new[]{candidate});Require(App.Playback.CurrentStream==null&&await App.Offline.FindAudioAsync(candidate)!=null,"Downloaded playback did not use local file");
var badge=MusicItem.From(candidate);await Task.Delay(200);Require(badge.CacheVisibility==Visibility.Visible,"Cached-track badge not visible");await Go<DownloadsPage>();await Capture("16-downloaded");
});
await Check("Downloaded track migration and deletion",async()=>{
Require(downloaded!=null,"No downloaded track to migrate");
// Completed 先于 ObserveAsync 的收尾保存置位，先等后台传输真正结束再迁移
for(int i=0;i<40;i++){var e=(await App.Offline.GetDownloadsAsync()).FirstOrDefault(x=>x.Track.Id==downloaded.Id);if(e!=null&&e.Completed&&e.Percent>=100)break;await Task.Delay(250);}
var start=await App.Offline.GetDownloadsAsync();
Require(start.Any(x=>x.Track.Id==downloaded.Id),"Download entry missing before migration: "+string.Join(",",start.Select(x=>x.Track.Id+(x.Completed?"@done":"@pending")))+" wanted="+downloaded.Id);
var target=await ApplicationData.Current.LocalFolder.CreateFolderAsync("MigrationTarget",CreationCollisionOption.OpenIfExists);
try{
 App.Offline.SetDownloadFolder(target);
 var forward=await App.Offline.MigrateDownloadsAsync(target);
 Require(forward.Moved>=1,"Migration to the chosen folder moved nothing: failed="+forward.Failed);
 var entry=(await App.Offline.GetDownloadsAsync()).FirstOrDefault(x=>x.Track.Id==downloaded.Id);
 Require(entry!=null,"Entry vanished after migrating to the chosen folder");
 Require(entry.FolderToken!=null&&await target.TryGetItemAsync(entry.FileName)!=null,"File did not reach the chosen folder");
 Require(await App.Offline.FindAudioAsync(downloaded)!=null,"Moved file is not resolvable");
 App.Offline.SetDownloadFolder(null);
 var backward=await App.Offline.MigrateDownloadsAsync(null);
 Require(backward.Moved>=1,"Migration back moved nothing: failed="+backward.Failed);
 var restored=(await App.Offline.GetDownloadsAsync()).FirstOrDefault(x=>x.Track.Id==downloaded.Id);
 Require(restored!=null,"Entry vanished after migrating back");
 Require(restored.FolderToken==null,"Entry still points at the custom folder");
 Require(await App.Offline.FindAudioAsync(downloaded)!=null,"Restored file is not resolvable");
 // 换到网络音源，释放本地文件句柄后再删除
 if(songs!=null&&songs.Count>0)await App.Playback.PlayAsync(songs);
 Require(await App.Offline.DeleteAsync(downloaded.Id),"Delete failed");
 Require(!await App.Offline.IsDownloadedAsync(downloaded.Id),"Deleted track still marked as downloaded");
 Require(await App.Offline.FindAudioAsync(downloaded)==null,"Deleted audio file is still resolvable");
}finally{App.Offline.SetDownloadFolder(null);try{await target.DeleteAsync();}catch(Exception){}}
});
}finally{App.Playback.Pause();await Go<DiscoverPage>();execution.Dispose();App.IsDiagnosticsRunning=false;running=false;}}
}}




#endif







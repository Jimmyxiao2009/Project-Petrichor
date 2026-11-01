using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Petrichor.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
namespace Petrichor.App {
 public sealed class MusicItem : INotifyPropertyChanged { private double tileWidth=148; public double TileWidth {get=>tileWidth;set {tileWidth=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(TileWidth)));}} public event PropertyChangedEventHandler PropertyChanged;
  private bool cached;private static readonly List<WeakReference<MusicItem>> cacheItems=new List<WeakReference<MusicItem>>();
  public Windows.UI.Xaml.Visibility CacheVisibility=>cached?Windows.UI.Xaml.Visibility.Visible:Windows.UI.Xaml.Visibility.Collapsed;
  private void SetCached(bool value){cached=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(CacheVisibility)));}
  private async void CheckCachedAsync(){try{var track=Value as Track;if(track!=null)SetCached(await App.Offline.IsDownloadedAsync(track.Id));}catch(Exception error){await App.LogAsync(error);}}
  public static async void RefreshCacheBadgesAsync(){try{var saved=new HashSet<long>((await App.Offline.GetDownloadsAsync()).Where(x=>x.Completed).Select(x=>x.Track.Id));cacheItems.RemoveAll(x=>{MusicItem item;return !x.TryGetTarget(out item);});foreach(var reference in cacheItems){MusicItem item;if(reference.TryGetTarget(out item))item.SetCached(saved.Contains(((Track)item.Value).Id));}}catch(Exception error){await App.LogAsync(error);}}
  public static void FitCovers(GridView list) {if(list.ActualWidth<=0)return;int columns=Math.Max(2,(int)Math.Floor(list.ActualWidth/168));double width=Math.Max(100,(list.ActualWidth-4)/columns-12);foreach(var item in list.Items.OfType<MusicItem>())item.TileWidth=width;}
  public object Value { get; set; } public string Title { get; set; } public string Subtitle { get; set; }
  /// <summary>第三行说明（播客简介、节目时长与播放量等）。</summary>
  public string Detail { get; set; }
  /// <summary>行首短标记，如节目期数。</summary>
  public string Badge { get; set; }
  public Windows.UI.Xaml.Visibility DetailVisibility=>string.IsNullOrEmpty(Detail)?Windows.UI.Xaml.Visibility.Collapsed:Windows.UI.Xaml.Visibility.Visible; private BitmapImage cover; public string CoverUrl {get;set;} public BitmapImage Cover {get=>cover??(cover=Image(CoverUrl));set=>cover=value;}
  public static BitmapImage Image(string url) {
   Uri uri;
   if(!Uri.TryCreate(url,UriKind.Absolute,out uri))return new BitmapImage(new Uri("ms-appx:///Assets/Forest.png"));
   if(uri.Scheme=="ms-appdata"||uri.Scheme=="ms-appx")return new BitmapImage(uri){DecodePixelWidth=360};
   var image=new BitmapImage(new Uri("ms-appx:///Assets/Forest.png")){DecodePixelWidth=360};
   SetCachedImageAsync(image,url);return image;
  }
  private static async void SetCachedImageAsync(BitmapImage image,string url){try {var uri=await App.Offline.GetCoverAsync(url);if(uri!=null)image.UriSource=uri;}catch(Exception e){await App.LogAsync(e);}}
  public static MusicItem From(Track t) {var item=new MusicItem {Value=t,Title=t.Name,Subtitle=t.ArtistText+" · "+t.Album.Name,CoverUrl=t.Album.CoverUrl};cacheItems.Add(new WeakReference<MusicItem>(item));item.CheckCachedAsync();return item;}
  public static MusicItem From(Playlist p) => new MusicItem { Value=p,Title=p.Name,Subtitle=p.TrackCount+" 首 · "+(p.Creator?.Nickname ?? "精选歌单"),CoverUrl=p.CoverUrl };
  public static MusicItem From(Album a) => new MusicItem { Value=a,Title=a.Name,Subtitle="专辑",CoverUrl=a.CoverUrl };
  public static MusicItem From(Artist a) => new MusicItem { Value=a,Title=a.Name,Subtitle="歌手",CoverUrl=null };
 }
 public class MusicPage : Page {
  private CancellationTokenSource source;
  public Task LoadTask { get; private set; }=Task.FromResult(0);
  protected CancellationToken Token => source?.Token ?? CancellationToken.None;
  protected bool IsCurrent => Frame?.Content==this;
  private bool loaded;
  protected override void OnNavigatedTo(NavigationEventArgs e) {
   base.OnNavigatedTo(e); source=new CancellationTokenSource();
   // 开启了页面缓存的页面（如评论）从下一级返回时保留列表和滚动位置，不重新加载
   if(e.NavigationMode==NavigationMode.Back&&loaded&&NavigationCacheMode!=Windows.UI.Xaml.Navigation.NavigationCacheMode.Disabled)return;
   loaded=true; LoadTask=ExecuteAsync(() => LoadAsync(e.Parameter));
  }
  protected virtual Task LoadAsync(object parameter) => Task.FromResult(0);
  protected override void OnNavigatedFrom(NavigationEventArgs e) { source?.Cancel(); base.OnNavigatedFrom(e); }
  public async Task ExecuteAsync(Func<Task> action) {
   Shell.Current?.SetBusy(true);
   try { await action(); }
   catch(OperationCanceledException) { }
   catch(MusicApiException e) { if(IsCurrent) Shell.Current?.Notify(UserMessages.For(e)); }
   catch(Exception e) { if(IsCurrent) Shell.Current?.Notify("无法完成操作，请重试。"); await App.LogAsync(e); }
   finally { Shell.Current?.SetBusy(false); }
  }
  protected async Task PlayTracksAsync(IReadOnlyList<Track> tracks, Track selected=null) {
   if(tracks.Count==0) { Shell.Current.Notify("还没有可播放的歌曲。");return; }
   int index=selected==null ? 0 : tracks.ToList().FindIndex(t=>t.Id==selected.Id);
   await App.Playback.PlayAsync(tracks,Math.Max(0,index),Token); if(IsCurrent) Shell.Current.Navigate(typeof(Pages.PlayerPage));
  }
  public virtual bool HandleBack() => false;
  public void BackClick(object sender,Windows.UI.Xaml.RoutedEventArgs e) => Shell.Current.GoBack();
  public async void TrackClick(object sender,ItemClickEventArgs e) {
   var item=(MusicItem)e.ClickedItem; var track=item.Value as Track;
   var list=((ListViewBase)sender).Items.Cast<MusicItem>().Select(x=>x.Value).OfType<Track>().ToList();
   if(track!=null) await ExecuteAsync(()=>PlayTracksAsync(list,track));
  }
  public void PlaylistClick(object sender,ItemClickEventArgs e) => Shell.Current.Navigate(typeof(Pages.PlaylistPage),(Playlist)((MusicItem)e.ClickedItem).Value);
 }
}






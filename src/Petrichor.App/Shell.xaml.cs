using System;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Petrichor.App.Pages;
namespace Petrichor.App {
 public sealed partial class Shell : Page {
  public static Shell Current { get; private set; }
  private readonly DispatcherTimer clock=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(400) };
  private int busyCount;
  public bool LyricsControlsVisible=>!Windows.Storage.ApplicationData.Current.LocalSettings.Values.ContainsKey("lyricsControls")||(bool)Windows.Storage.ApplicationData.Current.LocalSettings.Values["lyricsControls"];
  public void SetLyricsControlsVisible(bool visible){Windows.Storage.ApplicationData.Current.LocalSettings.Values["lyricsControls"]=visible;UpdateControlsVisibility();}
  private void UpdateControlsVisibility(){bool visible=ContentFrame.CurrentSourcePageType!=typeof(PlayerPage)&&(ContentFrame.CurrentSourcePageType!=typeof(LyricsPage)||LyricsControlsVisible);MiniPlayer.Visibility=visible?Visibility.Visible:Visibility.Collapsed;MiniPlayerRow.Height=new GridLength(visible?60:0);}
  public Frame NavigationFrame => ContentFrame;
  public Shell() {
   InitializeComponent(); Current=this; Loaded+=OnLoaded;
   App.Playback.TrackChanged+=OnTrackChanged; App.Playback.Error+=OnPlaybackError;
   App.Playback.Player.PlaybackSession.PlaybackStateChanged+=OnPlaybackStateChanged;
   SystemNavigationManager.GetForCurrentView().BackRequested+=OnBack;
   clock.Tick+=(s,e)=>UpdatePlayback();
   Navigate(typeof(DiscoverPage));
  }
  private async void OnLoaded(object sender,RoutedEventArgs e) { clock.Start(); try { await App.Offline.InitializeAsync(); await App.Api.RestoreSessionAsync(); App.NotifyAccountChanged(); } catch(Exception error) { Notify("当前无法恢复登录，可继续浏览或重新登录。"); await App.LogAsync(error); } }
  private static bool IsHome(Type type)=>type==typeof(DiscoverPage)||type==typeof(SearchPage)||type==typeof(LibraryPage)||type==typeof(SettingsPage);
  public void Navigate(Type type,object parameter=null) {
   if(ContentFrame.CurrentSourcePageType==type && parameter==null) return;
   ContentFrame.Navigate(type,parameter);
   // The tab bar switches peer roots; only their child pages belong in the back stack.
   if(IsHome(type)){ContentFrame.BackStack.Clear();ContentFrame.ForwardStack.Clear();SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility=AppViewBackButtonVisibility.Collapsed;}
  }
  public void GoBack() {
   var page=ContentFrame.Content as MusicPage;
   if(page!=null&&page.HandleBack())return;
   if(ContentFrame.CanGoBack) ContentFrame.GoBack();
  }
  private void OnBack(object sender,BackRequestedEventArgs e) { if(!e.Handled&&ContentFrame.CanGoBack) {e.Handled=true;GoBack();} }
  private void OnNavigated(object sender,NavigationEventArgs e) {
   UpdateControlsVisibility();var accent=(SolidColorBrush)Application.Current.Resources["Accent"];
   var muted=(SolidColorBrush)Application.Current.Resources["SecondaryText"];
   var type=e.SourcePageType;
   if(type==typeof(DiscoverPage)||type==typeof(SearchPage)||type==typeof(LibraryPage)||type==typeof(SettingsPage)||type==typeof(DownloadsPage)){
    DiscoverNav.Foreground=type==typeof(DiscoverPage)?accent:muted;
    SearchNav.Foreground=type==typeof(SearchPage)?accent:muted;
    LibraryNav.Foreground=(type==typeof(LibraryPage)||type==typeof(DownloadsPage))?accent:muted;
    SettingsNav.Foreground=type==typeof(SettingsPage)?accent:muted;
   }
   SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility=ContentFrame.CanGoBack ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
  }
  public void Notify(string message) { if(App.IsDiagnosticsRunning)return; NoticeText.Text=message;Notice.Visibility=Visibility.Visible; }
  public void SetBusy(bool busy) { busyCount=Math.Max(0,busyCount+(busy ? 1 : -1)); Busy.Visibility=busyCount>0 ? Visibility.Visible : Visibility.Collapsed; }
  private void DismissNotice(object sender,RoutedEventArgs e) => Notice.Visibility=Visibility.Collapsed;
  private void NavigateDiscover(object sender,RoutedEventArgs e) => Navigate(typeof(DiscoverPage));
  private void NavigateSearch(object sender,RoutedEventArgs e) => Navigate(typeof(SearchPage));
  private void NavigateLibrary(object sender,RoutedEventArgs e) => Navigate(typeof(LibraryPage));
  private void NavigateSettings(object sender,RoutedEventArgs e) => Navigate(typeof(SettingsPage));
  private void OpenPlayer(object sender,RoutedEventArgs e) => Navigate(typeof(PlayerPage));
  private void OpenQueue(object sender,RoutedEventArgs e) => Navigate(typeof(QueuePage));
  private async void TogglePlayback(object sender,RoutedEventArgs e) { try { if(App.Playback.Queue.Current==null) {Navigate(typeof(SearchPage));return;} if(App.Playback.Player.PlaybackSession.PlaybackState==MediaPlaybackState.Playing) App.Playback.Pause(); else await App.Playback.ResumeAsync(); } catch(Exception error) {Notify(UserMessages.For(error));await App.LogAsync(error);} }
  private async void OnTrackChanged(object sender,EventArgs e) => await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,UpdatePlayback);
  private async void OnPlaybackStateChanged(MediaPlaybackSession sender,object args) => await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,UpdatePlayback);
  private async void OnPlaybackError(object sender,Petrichor.Uwp.PlaybackErrorEventArgs e) => await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,()=>Notify("这首歌暂时无法播放，请重试或换一首。"));
  private void UpdatePlayback() { var t=App.Playback.Queue.Current; if(t!=null) { MiniTitle.Text=t.Name;MiniArtist.Text=t.ArtistText; if(MiniCover.Tag as string!=t.Album.CoverUrl) {MiniCover.Source=MusicItem.Image(t.Album.CoverUrl);MiniCover.Tag=t.Album.CoverUrl;} } MiniPlayGlyph.Glyph=App.Playback.Player.PlaybackSession.PlaybackState==MediaPlaybackState.Playing ? "\uE769" : "\uE768"; (ContentFrame.Content as PlayerPage)?.UpdatePosition(); (ContentFrame.Content as LyricsPage)?.UpdatePosition(); }
  public void SetBackground(bool isBackground) { if(isBackground) clock.Stop(); else {clock.Start();UpdatePlayback();} }
 }
}






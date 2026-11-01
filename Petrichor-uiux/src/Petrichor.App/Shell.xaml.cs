using System;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Petrichor.App.Pages;
namespace Petrichor.App {
 public sealed partial class Shell : Page {
  public static Shell Current { get; private set; }
  private readonly DispatcherTimer clock=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(400) };
  private int busyCount;
  public bool LyricsControlsVisible=>!Windows.Storage.ApplicationData.Current.LocalSettings.Values.ContainsKey("lyricsControls")||(bool)Windows.Storage.ApplicationData.Current.LocalSettings.Values["lyricsControls"];
  public void SetLyricsControlsVisible(bool visible){Windows.Storage.ApplicationData.Current.LocalSettings.Values["lyricsControls"]=visible;UpdateControlsVisibility();}
  private bool DetailVisible=>ContentFrame.Visibility==Visibility.Visible;
  private Frame HomeFrame=>RootPivot.SelectedIndex==1?SearchFrame:RootPivot.SelectedIndex==2?LibraryFrame:DiscoverFrame;
  private void UpdateControlsVisibility(){
   var type=DetailVisible?ContentFrame.CurrentSourcePageType:HomeFrame.CurrentSourcePageType;
   bool visible=type!=typeof(PlayerPage)&&(type!=typeof(LyricsPage)||LyricsControlsVisible);
   MiniPlayer.Visibility=visible?Visibility.Visible:Visibility.Collapsed;MiniPlayerRow.Height=new GridLength(visible?58:0);
  }
  private void UpdateBackVisibility()=>SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility=DetailVisible?AppViewBackButtonVisibility.Visible:AppViewBackButtonVisibility.Collapsed;
  public Frame NavigationFrame => DetailVisible?ContentFrame:HomeFrame;
  public Shell() {
   InitializeComponent(); Current=this; Loaded+=OnLoaded;
   App.Playback.TrackChanged+=OnTrackChanged; App.Playback.Error+=OnPlaybackError;
   App.Playback.Player.PlaybackSession.PlaybackStateChanged+=OnPlaybackStateChanged;
   SystemNavigationManager.GetForCurrentView().BackRequested+=OnBack;
   clock.Tick+=(s,e)=>UpdatePlayback();
   DiscoverFrame.Navigate(typeof(DiscoverPage));
   SearchFrame.Navigate(typeof(SearchPage));
   LibraryFrame.Navigate(typeof(LibraryPage));
   UpdateBackVisibility();
  }
  private async void OnLoaded(object sender,RoutedEventArgs e) { clock.Start(); try { await App.Offline.InitializeAsync(); await App.Api.RestoreSessionAsync(); App.NotifyAccountChanged(); } catch(Exception error) { Notify("当前无法恢复登录，可继续浏览或重新登录。"); await App.LogAsync(error); } }
  private static int HomeIndex(Type type)=>type==typeof(SearchPage)?1:type==typeof(LibraryPage)?2:type==typeof(DiscoverPage)?0:-1;
  public void Navigate(Type type,object parameter=null) {
   int home=HomeIndex(type);
   if(home>=0){
    RootPivot.SelectedIndex=home;
    if(parameter!=null||HomeFrame.Content==null)HomeFrame.Navigate(type,parameter);
    CloseDetails();
    UpdateControlsVisibility();UpdateBackVisibility();
    return;
   }
   if(DetailVisible&&ContentFrame.CurrentSourcePageType==type&&parameter==null)return;
   // A closed overlay starts a new detail route, never a child of the last hidden page.
   bool opening=!DetailVisible;
   ContentFrame.Navigate(type,parameter);
   if(opening){ContentFrame.BackStack.Clear();ContentFrame.ForwardStack.Clear();}
  }
  public void GoBack() {
   if(!DetailVisible)return;
   var page=ContentFrame.Content as MusicPage;
   if(page!=null&&page.HandleBack())return;
   if(ContentFrame.CanGoBack)ContentFrame.GoBack();
   else CloseDetails();
  }
  private void CloseDetails(){
   ContentFrame.Visibility=Visibility.Collapsed;
   ContentFrame.BackStack.Clear();ContentFrame.ForwardStack.Clear();
   UpdateControlsVisibility();UpdateBackVisibility();
  }
  private void OnBack(object sender,BackRequestedEventArgs e) { if(!e.Handled&&DetailVisible){e.Handled=true;GoBack();} }
  private async void OnPivotChanged(object sender,SelectionChangedEventArgs e) {
   if(HomeFrame.Content!=null)UpdateControlsVisibility();
   var library=RootPivot.SelectedIndex==2?LibraryFrame.Content as LibraryPage:null;
   if(library!=null){try{await library.RefreshRecentAsync();}catch(Exception error){await App.LogAsync(error);}}
  }
  private void OnNavigated(object sender,NavigationEventArgs e) { ContentFrame.Visibility=Visibility.Visible; UpdateControlsVisibility(); UpdateBackVisibility(); }
  public void Notify(string message) { if(App.IsDiagnosticsRunning)return; NoticeText.Text=message;Notice.Visibility=Visibility.Visible; }
  public void SetBusy(bool busy) { busyCount=Math.Max(0,busyCount+(busy ? 1 : -1)); Busy.Visibility=busyCount>0 ? Visibility.Visible : Visibility.Collapsed; }
  private void DismissNotice(object sender,RoutedEventArgs e) => Notice.Visibility=Visibility.Collapsed;
  private void OpenPlayer(object sender,RoutedEventArgs e) => Navigate(typeof(PlayerPage));
  private async void TogglePlayback(object sender,RoutedEventArgs e) { try { if(App.Playback.Queue.Current==null) {Navigate(typeof(SearchPage));return;} if(App.Playback.Player.PlaybackSession.PlaybackState==MediaPlaybackState.Playing) App.Playback.Pause(); else await App.Playback.ResumeAsync(); } catch(Exception error) {Notify(UserMessages.For(error));await App.LogAsync(error);} }
  private async void OnTrackChanged(object sender,EventArgs e) => await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,UpdatePlayback);
  private async void OnPlaybackStateChanged(MediaPlaybackSession sender,object args) => await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,UpdatePlayback);
  private async void OnPlaybackError(object sender,Petrichor.Uwp.PlaybackErrorEventArgs e) => await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,()=>Notify("这首歌暂时无法播放，请重试或换一首。"));
  private void UpdatePlayback() {
   var t=App.Playback.Queue.Current; var session=App.Playback.Player.PlaybackSession;
   if(t!=null) { MiniTitle.Text=t.Name;MiniArtist.Text=t.ArtistText; if(MiniCover.Tag as string!=t.Album.CoverUrl) {MiniCover.Source=MusicItem.Image(t.Album.CoverUrl);MiniCover.Tag=t.Album.CoverUrl;} }
   double total=session.NaturalDuration.TotalSeconds;
   MiniProgress.Maximum=Math.Max(1,total); MiniProgress.Value=total>0?Math.Min(session.Position.TotalSeconds,total):0;
   MiniPlayGlyph.Glyph=session.PlaybackState==MediaPlaybackState.Playing ? "\uE769" : "\uE768";
   (ContentFrame.Content as PlayerPage)?.UpdatePosition(); (ContentFrame.Content as LyricsPage)?.UpdatePosition();
  }
  public void SetBackground(bool isBackground) { if(isBackground) clock.Stop(); else {clock.Start();UpdatePlayback();} }
 }
}

using System;using System.Linq;using System.Threading.Tasks;using Petrichor.Core;using Windows.UI.Xaml;using Windows.UI.Xaml.Controls;namespace Petrichor.App.Pages {
 public sealed partial class PlayerPage : MusicPage {
  private bool updating,liked;private long shown,likedId;public long TrackId=>shown;
  public PlayerPage(){InitializeComponent();}
  protected override Task LoadAsync(object p){UpdatePosition();return Task.FromResult(0);}
  private async void RefreshLikedAsync(Track track){
   likedId=0;Like.IsEnabled=false;
   try{liked=App.Api.IsLoggedIn&&(await App.Api.GetLikedTrackIdsAsync(Token)).Contains(track.Id);
    if(IsCurrent&&shown==track.Id){likedId=track.Id;LikeGlyph.Glyph=liked?"\uEB52":"\uEB51";LikeGlyph.Foreground=liked?(Windows.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["Accent"]:new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White);}
   }catch(OperationCanceledException){}catch(Exception error){await App.LogAsync(error);}
   finally{if(IsCurrent&&shown==track.Id)Like.IsEnabled=true;}
  }
  public void UpdatePosition(){
   var t=App.Playback.Queue.Current;var session=App.Playback.Player.PlaybackSession;
   if(t!=null&&shown!=t.Id){shown=t.Id;Title.Text=t.Name;Artist.Content=t.ArtistText;AlbumButton.Content=t.Album.Name;Artist.IsEnabled=t.Artists.Any(a=>a.Id>0);AlbumButton.IsEnabled=t.Album.Id>0;Cover.Source=MusicItem.Image(t.Album.CoverUrl);if(t.Id>0)RefreshLikedAsync(t);}
   updating=true;Progress.Maximum=Math.Max(1,session.NaturalDuration.TotalSeconds);Progress.Value=Math.Min(Progress.Maximum,session.Position.TotalSeconds);updating=false;
   Elapsed.Text=session.Position.ToString(@"mm\:ss");Duration.Text=session.NaturalDuration.ToString(@"mm\:ss");PlayGlyph.Glyph=session.PlaybackState==Windows.Media.Playback.MediaPlaybackState.Playing?"\uE769":"\uE768";Download.IsEnabled=t!=null&&t.Id>0;
   Status.Text=App.Playback.CurrentStream?.IsPreview==true?"正在试听":session.PlaybackState==Windows.Media.Playback.MediaPlaybackState.Buffering?"正在缓冲…":"";
  }
  private void SeekChanged(object s,Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e){if(!updating&&shown!=0)App.Playback.Seek(TimeSpan.FromSeconds(e.NewValue));}
  private async void PlayClick(object s,RoutedEventArgs e){if(App.Playback.Queue.Current==null){Shell.Current.Navigate(typeof(SearchPage));return;}await ExecuteAsync(async()=>{if(App.Playback.Player.PlaybackSession.PlaybackState==Windows.Media.Playback.MediaPlaybackState.Playing)App.Playback.Pause();else await App.Playback.ResumeAsync();});}
  private async void PreviousClick(object s,RoutedEventArgs e)=>await ExecuteAsync(App.Playback.PreviousAsync);
  private async void NextClick(object s,RoutedEventArgs e)=>await ExecuteAsync(App.Playback.NextAsync);
  private void LyricsClick(object s,RoutedEventArgs e)=>Shell.Current.Navigate(typeof(LyricsPage));
  private void QueueClick(object s,RoutedEventArgs e)=>Shell.Current.Navigate(typeof(QueuePage));
  private void ArtistClick(object s,RoutedEventArgs e){var t=App.Playback.Queue.Current;if(t?.Artists.Count>0)Shell.Current.Navigate(typeof(ArtistPage),t.Artists[0].Id);}
  private void AlbumClick(object s,RoutedEventArgs e){var t=App.Playback.Queue.Current;if(t?.Album.Id>0)Shell.Current.Navigate(typeof(AlbumPage),t.Album.Id);}
  private void CommentsClick(object s,RoutedEventArgs e){var track=App.Playback.Queue.Current;if(track?.Id>0)Shell.Current.Navigate(typeof(CommentsPage),track);}
  private async void AddPlaylistClick(object s,RoutedEventArgs e){var track=App.Playback.Queue.Current;if(track?.Id>0)await ExecuteAsync(()=>PlaylistChooser.AddAsync(new[]{track},Token));}
  private async void DownloadClick(object s,RoutedEventArgs e){var track=App.Playback.Queue.Current;if(track?.Id>0)await ExecuteAsync(async()=>{await App.Offline.DownloadAsync(track,App.Playback.Quality,Token);Shell.Current.Notify("可在下载中查看和离线播放。");});}
  private async void LikeClick(object s,RoutedEventArgs e){
   var track=App.Playback.Queue.Current;if(track==null||track.Id<=0)return;
   if(!App.Api.IsLoggedIn){Shell.Current.Navigate(typeof(LoginPage));return;}
   await ExecuteAsync(async()=>{if(likedId!=track.Id){liked=(await App.Api.GetLikedTrackIdsAsync(Token)).Contains(track.Id);likedId=track.Id;}await App.Api.SetLikedAsync(track.Id,!liked,Token);liked=!liked;LikeGlyph.Glyph=liked?"\uEB52":"\uEB51";LikeGlyph.Foreground=liked?(Windows.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["Accent"]:new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White);Shell.Current.Notify(liked?"已加入喜欢的音乐。":"已取消喜欢。");});
  }
 }
}

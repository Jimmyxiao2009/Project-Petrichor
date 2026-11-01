using System;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using Petrichor.Core;using Windows.UI.Xaml;using Windows.UI.Xaml.Controls;using Windows.UI.Xaml.Input;using Windows.UI.Xaml.Media;namespace Petrichor.App.Pages {
 public sealed partial class PlaylistPage : MusicPage {
  private Playlist playlist;private readonly List<Track> tracks=new List<Track>();private bool selecting;
  public int TrackCount=>tracks.Count;public int SelectedCount=>Tracks.SelectedItems.Count;
  public PlaylistPage(){InitializeComponent();}
  protected override async Task LoadAsync(object p){playlist=await App.Api.GetPlaylistAsync(p is Playlist?((Playlist)p).Id:Convert.ToInt64(p),Token);Title.Text=playlist.Name;Cover.Source=MusicItem.Image(playlist.CoverUrl);DeletePlaylist.Visibility=CanEditPlaylist()?Visibility.Visible:Visibility.Collapsed;Subtitle.Text=(playlist.Creator?.Nickname??"云音乐")+" · "+playlist.TrackCount+" 首";Description.Text=playlist.Description??"";await FetchAsync();}
  private async Task FetchAsync(){tracks.AddRange(await App.Api.GetAllPlaylistTracksAsync(playlist,Token));if(IsCurrent)Tracks.ItemsSource=tracks.Select(MusicItem.From).ToList();}
  private async void PlayAll(object s,RoutedEventArgs e)=>await ExecuteAsync(()=>PlayTracksAsync(tracks));
  public void SetSelectionMode(bool value){selecting=value;Tracks.SelectionMode=value?ListViewSelectionMode.Multiple:ListViewSelectionMode.None;Tracks.IsItemClickEnabled=!value;SelectionBar.Visibility=value?Visibility.Visible:Visibility.Collapsed;SelectionCount.Text="已选 "+SelectedCount;}
  public void SelectFirstTwo(){SetSelectionMode(true);foreach(var item in Tracks.Items.Take(2))Tracks.SelectedItems.Add(item);}
  public override bool HandleBack(){if(!selecting)return false;SetSelectionMode(false);return true;}
  private void SelectClick(object s,RoutedEventArgs e)=>SetSelectionMode(!selecting);
  private void SelectionChanged(object s,SelectionChangedEventArgs e){if(SelectionCount!=null)SelectionCount.Text="已选 "+SelectedCount;}
  private List<Track> Selected()=>Tracks.SelectedItems.Cast<MusicItem>().Select(x=>(Track)x.Value).ToList();
  private async void SongClick(object s,ItemClickEventArgs e){if(!selecting)await ExecuteAsync(()=>PlayTracksAsync(tracks,(Track)((MusicItem)e.ClickedItem).Value));}
  private async void PlaySelected(object s,RoutedEventArgs e)=>await ExecuteAsync(()=>PlayTracksAsync(Selected()));
  private async void CacheSelected(object s,RoutedEventArgs e)=>await ExecuteAsync(()=>CacheAsync(Selected()));
  private async void AddSelected(object s,RoutedEventArgs e)=>await ExecuteAsync(()=>PlaylistChooser.AddAsync(Selected(),Token));
  private void AllSelected(object s,RoutedEventArgs e)=>Tracks.SelectAll();
  private async Task CacheAsync(IReadOnlyList<Track> list){int started=0,failed=0;foreach(var track in list){try{await App.Offline.DownloadAsync(track,App.Playback.Quality,System.Threading.CancellationToken.None);started++;}catch(MusicApiException){failed++;}}Shell.Current.Notify(started+" 首已加入下载"+(failed>0?"，"+failed+" 首暂时无法下载。":"。"));}
  private static MusicItem ItemAt(object source){var element=source as DependencyObject;while(element!=null){var framework=element as FrameworkElement;var item=framework?.DataContext as MusicItem;if(item?.Value is Track)return item;element=VisualTreeHelper.GetParent(element);}return null;}
  private bool menuShown;
  private void SongPointerPressed(object s,PointerRoutedEventArgs e)=>menuShown=false;
  private void ShowMenu(MusicItem item,Windows.Foundation.Point point){
   // 同一次按住里 Holding 与随后的 RightTapped 会各触发一次（手指抖一下就会重弹），只弹第一次
   if(item==null||menuShown)return;menuShown=true;var chosen=selecting&&Tracks.SelectedItems.Contains(item)?Selected():new List<Track>{(Track)item.Value};
   var menu=new MenuFlyout();
   Action<string,Func<Task>> add=(text,action)=>{var button=new MenuFlyoutItem {Text=text};button.Click+=async(s,e)=>await ExecuteAsync(action);menu.Items.Add(button);};
   add("播放",()=>PlayTracksAsync(chosen));add("下一首播放",()=>{App.Playback.Queue.InsertNext(chosen);Shell.Current.Notify("已加入下一首。");return Task.FromResult(0);});add("加入播放队列",()=>{App.Playback.Queue.Append(chosen);Shell.Current.Notify("已加入播放队列。");return Task.FromResult(0);});add("缓存歌曲",()=>CacheAsync(chosen));add("收藏到歌单",()=>PlaylistChooser.AddAsync(chosen,Token));
   if(CanEditPlaylist())add("从歌单删除",()=>RemoveFromPlaylistAsync(chosen));
   var select=new MenuFlyoutItem {Text="多选"};select.Click+=(s,e)=>{SetSelectionMode(true);Tracks.SelectedItems.Add(item);};menu.Items.Add(select);menu.ShowAt(Tracks,point);
  }
  private bool CanEditPlaylist()=>playlist!=null&&playlist.SpecialType==0&&App.Api.IsLoggedIn&&App.Api.CurrentUser!=null&&playlist.Creator!=null&&playlist.Creator.Id==App.Api.CurrentUser.Id;
  private async void DeletePlaylistClick(object s,RoutedEventArgs e){
   if(playlist==null)return;
   var dialog=new ContentDialog{Title="删除歌单？",Content="将删除「"+playlist.Name+"」，歌单里的歌曲不会从曲库消失。",PrimaryButtonText="删除",CloseButtonText="取消"};
   if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
   await ExecuteAsync(async()=>{
    await App.Api.DeletePlaylistAsync(playlist.Id,Token);
    Shell.Current.Notify("已删除「"+playlist.Name+"」。");
    Shell.Current.GoBack();
   });
  }
  private async Task RemoveFromPlaylistAsync(IReadOnlyList<Track> list){
   if(list.Count==0)return;
   bool single=list.Count==1;
   var dialog=new ContentDialog{Title=single?"从歌单删除？":"从歌单删除 "+list.Count+" 首？",Content=single?"将把「"+list[0].Name+"」从「"+playlist.Name+"」移除。":"将把所选 "+list.Count+" 首歌从「"+playlist.Name+"」移除。",PrimaryButtonText="删除",CloseButtonText="取消"};
   if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
   await ExecuteAsync(async()=>{
    await App.Api.RemoveTracksFromPlaylistAsync(playlist.Id,list.Select(x=>x.Id),Token);
    Shell.Current.Notify(single?"已移除「"+list[0].Name+"」。":list.Count+" 首已从歌单移除。");
    if(selecting)SetSelectionMode(false);
    tracks.Clear();await FetchAsync();
    Subtitle.Text=(playlist.Creator?.Nickname??"云音乐")+" · "+tracks.Count+" 首";playlist.TrackCount=tracks.Count;
   });
  }
  private void SongHolding(object s,HoldingRoutedEventArgs e){if(e.HoldingState==Windows.UI.Input.HoldingState.Started){ShowMenu(ItemAt(e.OriginalSource),e.GetPosition(Tracks));e.Handled=true;}}
  private void SongRightTapped(object s,RightTappedRoutedEventArgs e){ShowMenu(ItemAt(e.OriginalSource),e.GetPosition(Tracks));e.Handled=true;}
 }
}



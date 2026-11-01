using System;using System.Collections.Generic;using System.Linq;using System.Threading.Tasks;using Petrichor.Core;using Windows.UI.Xaml;using Windows.UI.Xaml.Controls;using Windows.UI.Xaml.Input;using Windows.UI.Xaml.Media;using Windows.UI.Xaml.Navigation;namespace Petrichor.App.Pages {
 public sealed partial class DownloadsPage : MusicPage {
  public DownloadsPage(){InitializeComponent();}
  public int ItemCount=>Saved.Items.Count+Pending.Items.Count;
  public int SelectedCount=>Saved.SelectedItems.Count;
  private bool selecting;private List<long> savedIds=new List<long>();
  protected override async Task LoadAsync(object p){App.Offline.Changed+=Changed;await RefreshAsync();}
  private async Task RefreshAsync(){
   var list=await App.Offline.GetDownloadsAsync();
   var completed=list.Where(x=>x.Completed).ToList();var ids=completed.Select(x=>x.Track.Id).ToList();
   // 下载进度会频繁触发 Changed；已下载集合没变时不重建列表，避免多选状态被清掉
   if(!ids.SequenceEqual(savedIds)){
    savedIds=ids;
    var previous=new HashSet<long>(Saved.SelectedItems.Cast<MusicItem>().Select(x=>((Track)x.Value).Id));
    var items=completed.Select(x=>MusicItem.From(x.Track)).ToList();
    Saved.ItemsSource=items;
    if(selecting)foreach(var item in items.Where(x=>previous.Contains(((Track)x.Value).Id)))Saved.SelectedItems.Add(item);
   }
   Pending.ItemsSource=list.Where(x=>!x.Completed).ToList();
   SavedEmpty.Visibility=Saved.Items.Count==0?Visibility.Visible:Visibility.Collapsed;
   PendingEmpty.Visibility=Pending.Items.Count==0?Visibility.Visible:Visibility.Collapsed;
   if(selecting&&Saved.Items.Count==0)SetSelectionMode(false);else UpdateSelection();
  }
  private void UpdateSelection()=>SelectionCount.Text="已选 "+SelectedCount;
  public void SetSelectionMode(bool value){
   selecting=value;
   Saved.SelectionMode=value?ListViewSelectionMode.Multiple:ListViewSelectionMode.None;
   Saved.IsItemClickEnabled=!value;
   SelectButton.Visibility=value?Visibility.Collapsed:Visibility.Visible;
   SelectionBar.Visibility=value?Visibility.Visible:Visibility.Collapsed;
   UpdateSelection();
  }
  public override bool HandleBack(){if(!selecting)return false;SetSelectionMode(false);return true;}
  private void SelectClick(object s,RoutedEventArgs e)=>SetSelectionMode(!selecting);
  private void AllSelected(object s,RoutedEventArgs e)=>Saved.SelectAll();
  private void SavedSelectionChanged(object s,SelectionChangedEventArgs e){if(SelectionCount!=null)UpdateSelection();}
  private async void Changed(object s,EventArgs e)=>await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,async()=>{if(IsCurrent)await ExecuteAsync(RefreshAsync);});
  private async void SavedClick(object s,ItemClickEventArgs e){if(selecting)return;var track=(Track)((MusicItem)e.ClickedItem).Value;await ExecuteAsync(()=>PlayTracksAsync(Saved.Items.Cast<MusicItem>().Select(x=>(Track)x.Value).ToList(),track));}
  private async void DownloadClick(object s,ItemClickEventArgs e){var item=(DownloadEntry)e.ClickedItem;await ExecuteAsync(async()=>{if(item.Failed)await App.Offline.DownloadAsync(item.Track,App.Playback.Quality,Token);else App.Offline.TogglePause(item);});}
  private async void RefreshClick(object s,RoutedEventArgs e)=>await ExecuteAsync(RefreshAsync);
  private void SavedHolding(object s,HoldingRoutedEventArgs e){if(e.HoldingState==Windows.UI.Input.HoldingState.Started){ShowSavedMenu(ItemAt(e.OriginalSource),e.GetPosition(Saved));e.Handled=true;}}
  private void SavedRightTapped(object s,RightTappedRoutedEventArgs e){ShowSavedMenu(ItemAt(e.OriginalSource),e.GetPosition(Saved));e.Handled=true;}
  private bool savedMenuShown,pendingMenuShown;
  private void SavedPointerPressed(object s,PointerRoutedEventArgs e)=>savedMenuShown=false;
  private void PendingPointerPressed(object s,PointerRoutedEventArgs e)=>pendingMenuShown=false;
  private void ShowSavedMenu(MusicItem item,Windows.Foundation.Point point){
   // 同一次按住里 Holding 与随后的 RightTapped 会各触发一次（手指抖一下就会重弹），只弹第一次
   if(item==null||selecting||savedMenuShown)return;savedMenuShown=true;
   var menu=new MenuFlyout();
   var play=new MenuFlyoutItem {Text="播放"};play.Click+=async(s,e)=>await ExecuteAsync(()=>PlayTracksAsync(Saved.Items.Cast<MusicItem>().Select(x=>(Track)x.Value).ToList(),(Track)item.Value));menu.Items.Add(play);
   var select=new MenuFlyoutItem {Text="多选"};select.Click+=(s,e)=>{SetSelectionMode(true);Saved.SelectedItems.Add(item);};menu.Items.Add(select);
   menu.Items.Add(new MenuFlyoutSeparator());
   var delete=new MenuFlyoutItem {Text="从设备删除"};delete.Click+=async(s,e)=>await DeleteOneAsync((Track)item.Value);menu.Items.Add(delete);
   menu.ShowAt(Saved,point);
  }
  private void PendingHolding(object s,HoldingRoutedEventArgs e){if(e.HoldingState==Windows.UI.Input.HoldingState.Started){ShowPendingMenu(PendingAt(e.OriginalSource),e.GetPosition(Pending));e.Handled=true;}}
  private void PendingRightTapped(object s,RightTappedRoutedEventArgs e){ShowPendingMenu(PendingAt(e.OriginalSource),e.GetPosition(Pending));e.Handled=true;}
  private void ShowPendingMenu(DownloadEntry entry,Windows.Foundation.Point point){
   if(entry==null||pendingMenuShown)return;pendingMenuShown=true;
   var menu=new MenuFlyout();
   if(entry.Failed){var retry=new MenuFlyoutItem {Text="重试"};retry.Click+=async(s,e)=>await ExecuteAsync(()=>App.Offline.DownloadAsync(entry.Track,App.Playback.Quality,Token));menu.Items.Add(retry);}
   else{var pause=new MenuFlyoutItem {Text=entry.Paused?"继续":"暂停"};pause.Click+=(s,e)=>App.Offline.TogglePause(entry);menu.Items.Add(pause);}
   menu.Items.Add(new MenuFlyoutSeparator());
   var delete=new MenuFlyoutItem {Text="删除"};delete.Click+=async(s,e)=>await DeleteOneAsync(entry.Track);menu.Items.Add(delete);
   menu.ShowAt(Pending,point);
  }
  private async void DeleteSelected(object s,RoutedEventArgs e){
   var chosen=Saved.SelectedItems.Cast<MusicItem>().Select(x=>(Track)x.Value).ToList();
   if(chosen.Count==0){Shell.Current.Notify("请先选择要删除的歌曲。");return;}
   if(!await ConfirmDeleteAsync(chosen.Count))return;
   await ExecuteAsync(async()=>{
    int removed=0,failed=0;
    foreach(var track in chosen){if(await App.Offline.DeleteAsync(track.Id))removed++;else failed++;}
    SetSelectionMode(false);
    Shell.Current.Notify(removed+" 首已删除"+(failed>0?"，"+failed+" 首删除失败（可能正在播放）。":"。"));
   });
  }
  private async Task DeleteOneAsync(Track track){
   if(!await ConfirmDeleteAsync(1,track.Name))return;
   await ExecuteAsync(async()=>{var ok=await App.Offline.DeleteAsync(track.Id);Shell.Current.Notify(ok?"已删除「"+track.Name+"」。":"无法删除，文件可能正在播放或使用中。");});
  }
  private async Task<bool> ConfirmDeleteAsync(int count,string name=null){
   var single=count==1;
   var dialog=new ContentDialog {Title=single?"删除下载？":"删除 "+count+" 首下载？",Content=single?"将从设备移除"+(name==null?"这首歌曲":"「"+name+"」")+"的离线文件，需要时可以重新下载。":"将从设备移除所选歌曲的离线文件，需要时可以重新下载。",PrimaryButtonText="删除",CloseButtonText="取消"};
   return await dialog.ShowAsync()==ContentDialogResult.Primary;
  }
  private static MusicItem ItemAt(object source){var element=source as DependencyObject;while(element!=null){var framework=element as FrameworkElement;var item=framework?.DataContext as MusicItem;if(item?.Value is Track)return item;element=VisualTreeHelper.GetParent(element);}return null;}
  private static DownloadEntry PendingAt(object source){var element=source as DependencyObject;while(element!=null){var entry=(element as FrameworkElement)?.DataContext as DownloadEntry;if(entry!=null)return entry;element=VisualTreeHelper.GetParent(element);}return null;}
  protected override void OnNavigatedFrom(NavigationEventArgs e){App.Offline.Changed-=Changed;base.OnNavigatedFrom(e);}
 }
}

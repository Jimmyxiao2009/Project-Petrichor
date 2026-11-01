using System;using System.Linq;using System.Threading.Tasks;using Windows.UI.Xaml;using Windows.UI.Xaml.Controls;using Windows.UI.Xaml.Navigation;namespace Petrichor.App.Pages {
 public sealed partial class DownloadsPage : MusicPage {
  public DownloadsPage(){InitializeComponent();}
  public int ItemCount=>Saved.Items.Count+Pending.Items.Count;
  protected override async Task LoadAsync(object p){App.Offline.Changed+=Changed;await RefreshAsync();}
  private async Task RefreshAsync(){var list=await App.Offline.GetDownloadsAsync();Saved.ItemsSource=list.Where(x=>x.Completed).Select(x=>MusicItem.From(x.Track)).ToList();Pending.ItemsSource=list.Where(x=>!x.Completed).ToList();SavedEmpty.Visibility=Saved.Items.Count==0?Visibility.Visible:Visibility.Collapsed;PendingEmpty.Visibility=Pending.Items.Count==0?Visibility.Visible:Visibility.Collapsed;}
  private async void Changed(object s,EventArgs e)=>await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,async()=>{if(IsCurrent)await ExecuteAsync(RefreshAsync);});
  private async void SavedClick(object s,ItemClickEventArgs e){var track=(Petrichor.Core.Track)((MusicItem)e.ClickedItem).Value;await ExecuteAsync(()=>PlayTracksAsync(Saved.Items.Cast<MusicItem>().Select(x=>(Petrichor.Core.Track)x.Value).ToList(),track));}
  private async void DownloadClick(object s,ItemClickEventArgs e){var item=(DownloadEntry)e.ClickedItem;await ExecuteAsync(async()=>{if(item.Failed)await App.Offline.DownloadAsync(item.Track,App.Playback.Quality,Token);else App.Offline.TogglePause(item);});}
  private async void RefreshClick(object s,RoutedEventArgs e)=>await ExecuteAsync(RefreshAsync);
  protected override void OnNavigatedFrom(NavigationEventArgs e){App.Offline.Changed-=Changed;base.OnNavigatedFrom(e);}
 }
}

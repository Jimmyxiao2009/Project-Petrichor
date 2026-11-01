using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
namespace Petrichor.App.Pages {
 public sealed partial class PodcastsPage : MusicPage {
  private readonly List<MusicItem> hot=new List<MusicItem>(),subscribed=new List<MusicItem>();
  private string hotCursor,subscribedCursor;private bool subscribedLoaded;
  public int ItemCount=>hot.Count;
  public PodcastsPage(){InitializeComponent();}
  protected override Task LoadAsync(object p){hot.Clear();subscribed.Clear();hotCursor=subscribedCursor=null;subscribedLoaded=false;return LoadHotAsync();}
  private async Task LoadHotAsync(){
   var page=await PendingLogic.GetPodcastsAsync(PodcastFeed.Hot,hotCursor,Token);if(!IsCurrent)return;
   hot.AddRange(page.Items);hotCursor=page.Cursor;Hot.ItemsSource=hot.ToList();HotMore.Visibility=page.HasMore?Visibility.Visible:Visibility.Collapsed;
   HotEmpty.Visibility=hot.Count==0?Visibility.Visible:Visibility.Collapsed;
   HotEmptyText.Text=PendingLogic.PodcastsSupported||App.IsDiagnosticsRunning?"暂时没有推荐的播客":"播客即将上线";
  }
  private async Task LoadSubscribedAsync(){
   subscribedLoaded=true;
   if(!App.Api.IsLoggedIn&&!App.IsDiagnosticsRunning){SubscribedEmpty.Visibility=Visibility.Visible;SubscribedEmptyText.Text="登录后查看订阅的播客";LoginButton.Visibility=Visibility.Visible;return;}
   var page=await PendingLogic.GetPodcastsAsync(PodcastFeed.Subscribed,subscribedCursor,Token);if(!IsCurrent)return;
   subscribed.AddRange(page.Items);subscribedCursor=page.Cursor;Subscribed.ItemsSource=subscribed.ToList();SubscribedMore.Visibility=page.HasMore?Visibility.Visible:Visibility.Collapsed;
   LoginButton.Visibility=Visibility.Collapsed;SubscribedEmpty.Visibility=subscribed.Count==0?Visibility.Visible:Visibility.Collapsed;
   SubscribedEmptyText.Text=PendingLogic.PodcastsSupported||App.IsDiagnosticsRunning?"还没有订阅的播客":"播客即将上线";
  }
  public Task ShowSubscribedAsync(){Feeds.SelectedIndex=1;return subscribedLoaded?Task.FromResult(0):ExecuteAsync(LoadSubscribedAsync);}
  private async void FeedChanged(object s,SelectionChangedEventArgs e){if(Feeds!=null&&Feeds.SelectedIndex==1&&!subscribedLoaded&&Frame!=null)await ExecuteAsync(LoadSubscribedAsync);}
  private async void HotMoreClick(object s,RoutedEventArgs e)=>await ExecuteAsync(LoadHotAsync);
  private async void SubscribedMoreClick(object s,RoutedEventArgs e)=>await ExecuteAsync(LoadSubscribedAsync);
  private void LoginClick(object s,RoutedEventArgs e)=>Shell.Current.Navigate(typeof(LoginPage));
  private void PodcastClick(object s,ItemClickEventArgs e){var podcast=((MusicItem)e.ClickedItem).Value as PodcastRef;if(podcast!=null)Shell.Current.Navigate(typeof(PodcastPage),podcast);}
 }
}

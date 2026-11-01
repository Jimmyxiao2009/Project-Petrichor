using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Petrichor.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
namespace Petrichor.App.Pages {
 public sealed partial class PodcastPage : MusicPage {
  private PodcastRef podcast;private PodcastHeader header;private bool oldestFirst;private string cursor,episodeFilter;
  private readonly List<MusicItem> episodes=new List<MusicItem>();
  public int EpisodeCountShown=>episodes.Count;
  public PodcastPage(){InitializeComponent();}
  protected override async Task LoadAsync(object p){
   podcast=p as PodcastRef??new PodcastRef();Title.Text=podcast.Name??"";
   try{header=await PendingLogic.GetPodcastAsync(podcast.Id,Token);}
   catch(NotSupportedException){ShowUnavailable();return;}
   if(!IsCurrent)return;
   Title.Text=header.Title;Host.Text=header.Host;Cover.Source=MusicItem.Image(header.CoverUrl);
   Stats.Text=header.EpisodeCount+" 期 · "+PendingLogic.FormatCount(header.Subscribers)+" 人订阅";
   Description.Text=header.Description??"";Description.Visibility=string.IsNullOrWhiteSpace(header.Description)?Visibility.Collapsed:Visibility.Visible;
   EpisodeCount.Text="共 "+header.EpisodeCount+" 期";
   UpdateSubscribe();await ReloadEpisodesAsync();
  }
  private void ShowUnavailable(){
   Cover.Source=MusicItem.Image(null);Actions.Visibility=Visibility.Collapsed;Order.Visibility=Visibility.Collapsed;
   Empty.Visibility=Visibility.Visible;EmptyText.Text="播客即将上线";
  }
  private async Task ReloadEpisodesAsync(){episodes.Clear();cursor=null;await FetchAsync();}
  private async Task FetchAsync(){
   var page=await PendingLogic.GetEpisodesAsync(podcast.Id,oldestFirst,cursor,Token);if(!IsCurrent)return;
   episodes.AddRange(page.Items);cursor=page.Cursor;ShowEpisodes();
  }
  private void EpisodeSearchChanged(object s,TextChangedEventArgs e){episodeFilter=EpisodeSearch.Text.Trim();ShowEpisodes();}
  private void ShowEpisodes(){
   var q=episodeFilter;
   var shown=string.IsNullOrEmpty(q)?episodes:episodes.Where(x=>MatchesEpisode(x,q)).ToList();
   Episodes.ItemsSource=shown;
   More.Visibility=string.IsNullOrEmpty(cursor)?Visibility.Collapsed:Visibility.Visible;
   Empty.Visibility=shown.Count==0?Visibility.Visible:Visibility.Collapsed;
   EmptyText.Text=episodes.Count==0?"还没有节目":(string.IsNullOrEmpty(q)?"还没有节目":"没有匹配「"+q+"」的节目");
   EpisodeCount.Text=string.IsNullOrEmpty(q)?"共 "+(header?.EpisodeCount??episodes.Count)+" 期":"匹配 "+shown.Count+" / 已加载 "+episodes.Count+" 期";
  }
  private static bool MatchesEpisode(MusicItem item,string q){
   var track=item.Value as Track;if(track==null)return false;
   return ContainsText(item.Title,q)||ContainsText(item.Subtitle,q)||ContainsText(track.ArtistText,q);
  }
  private static bool ContainsText(string text,string q)=>!string.IsNullOrEmpty(text)&&text.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0;
  private void UpdateSubscribe(){
   bool on=header?.Subscribed==true;SubscribeLabel.Glyph=on?"\uE735":"\uE734";SubscribeLabel.Text=on?"已订阅":"订阅";
   Windows.UI.Xaml.Automation.AutomationProperties.SetName(Subscribe,on?"取消订阅":"订阅");
  }
  private async void SubscribeClick(object s,RoutedEventArgs e){
   if(header==null)return;if(!App.Api.IsLoggedIn&&!App.IsDiagnosticsRunning){Shell.Current.Navigate(typeof(LoginPage));return;}
   await ExecuteAsync(async()=>{await PendingLogic.SetPodcastSubscribedAsync(header.Id,!header.Subscribed,Token);header.Subscribed=!header.Subscribed;UpdateSubscribe();Shell.Current.Notify(header.Subscribed?"已订阅「"+header.Title+"」。":"已取消订阅。");});
  }
  private async void OrderClick(object s,RoutedEventArgs e){oldestFirst=!oldestFirst;OrderLabel.Text=oldestFirst?"最早在前":"最新在前";await ExecuteAsync(ReloadEpisodesAsync);}
  private async void MoreClick(object s,RoutedEventArgs e)=>await ExecuteAsync(FetchAsync);
  private List<Track> Playable=>Episodes.Items.Cast<MusicItem>().Select(x=>x.Value).OfType<Track>().Where(t=>t.Id>0).ToList();
  private async void PlayAllClick(object s,RoutedEventArgs e)=>await ExecuteAsync(()=>PlayTracksAsync(Playable));
  private async void EpisodeClick(object s,ItemClickEventArgs e){var track=((MusicItem)e.ClickedItem).Value as Track;if(track!=null)await ExecuteAsync(()=>PlayTracksAsync(Playable,track));}
 }
}

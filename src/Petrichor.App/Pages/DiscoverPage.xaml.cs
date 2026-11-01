using System;using System.Linq;using System.Collections.Generic;using System.Threading;using System.Threading.Tasks;using Petrichor.Core;using Windows.UI.Xaml;using Windows.UI.Xaml.Controls;using Windows.UI.Xaml.Navigation;using Windows.Storage;namespace Petrichor.App.Pages {public sealed partial class DiscoverPage : MusicPage {public DiscoverPage(){InitializeComponent(); Recommendations.SizeChanged+=(s,e)=>MusicItem.FitCovers(Recommendations);}
private bool charts; public int ItemCount=>Recommendations.Items.Count;
protected override async Task LoadAsync(object p){Status.Text="正在发现好音乐…";var items=charts?await App.Api.GetChartsAsync(Token):await App.Api.GetRecommendedPlaylistsAsync(12,Token);Recommendations.ItemsSource=items.Select(MusicItem.From).ToList();MusicItem.FitCovers(Recommendations);Status.Text=items.Count==0?"没有推荐，请刷新。":"";}
private void ExploreClick(object s,RoutedEventArgs e)=>Shell.Current.Navigate(typeof(PlaylistListPage));
private async void RefreshClick(object s,RoutedEventArgs e)=>await ExecuteAsync(()=>LoadAsync(null));
private async void ChartsClick(object s,RoutedEventArgs e){charts=!charts;Section.Text=charts?"云音乐排行榜":"为你发现";await ExecuteAsync(()=>LoadAsync(null));}
private async void DailyClick(object s,RoutedEventArgs e){if(!App.Api.IsLoggedIn){Shell.Current.Navigate(typeof(LoginPage));return;}await ExecuteAsync(async()=>await PlayTracksAsync(await App.Api.GetDailySongsAsync(Token)));}
private async void FmClick(object s,RoutedEventArgs e){if(!App.Api.IsLoggedIn){Shell.Current.Navigate(typeof(LoginPage));return;}await ExecuteAsync(async()=>await PlayTracksAsync(await App.Api.GetPrivateFmAsync(Token)));}}}


using System;using System.Linq;using System.Collections.Generic;using System.Threading;using System.Threading.Tasks;using Petrichor.Core;using Windows.UI.Xaml;using Windows.UI.Xaml.Controls;using Windows.UI.Xaml.Navigation;using Windows.Storage;namespace Petrichor.App.Pages {public sealed partial class LibraryPage : MusicPage {public LibraryPage(){InitializeComponent();}
private int revision;
// 首页在 Shell 构造时就加载，早于会话恢复；登录态变化必须靠事件刷新，否则一直停在未登录
protected override void OnNavigatedTo(NavigationEventArgs e){App.AccountChanged+=OnAccountChanged;base.OnNavigatedTo(e);}
protected override void OnNavigatedFrom(NavigationEventArgs e){App.AccountChanged-=OnAccountChanged;base.OnNavigatedFrom(e);}
private async void OnAccountChanged(object s,EventArgs e)=>await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,async()=>await ExecuteAsync(()=>LoadAsync(null)));
public async Task RefreshRecentAsync(){var recent=await App.History.ReadAsync();Recent.ItemsSource=recent.Select(x=>MusicItem.From(x.Track)).ToList();}
protected override async Task LoadAsync(object p){
 int current=++revision;
 await RefreshRecentAsync();
 var user=App.Api.CurrentUser;
 Avatar.Source=MusicItem.Image(user?.AvatarUrl);
 Nickname.Text=user?.Nickname??"登录云音乐";
 Account.Content=new FontIcon {Glyph=user!=null?"\uE8AC":"\uE77B",FontSize=20};
 Windows.UI.Xaml.Automation.AutomationProperties.SetName(Account,user!=null?"退出登录":"登录云音乐");
 if(user==null){Playlists.ItemsSource=null;PlaylistStatus.Text="登录后查看你的歌单和收藏。";return;}
 PlaylistStatus.Text="正在加载歌单…";
 var playlists=(await App.Api.GetMyPlaylistsAsync(30,0,Token)).Select(MusicItem.From).ToList();
 if(current!=revision)return;
 Playlists.ItemsSource=playlists;PlaylistStatus.Text=playlists.Count==0?"还没有歌单。":"创建与收藏的歌单";
}
private async void AccountClick(object s,RoutedEventArgs e){if(!App.Api.IsLoggedIn){Shell.Current.Navigate(typeof(LoginPage));return;}var dialog=new ContentDialog{Title="退出登录？",Content="最近播放会保留在这台设备上。",PrimaryButtonText="退出",CloseButtonText="取消"};if(await dialog.ShowAsync()==ContentDialogResult.Primary){await App.Api.LogoutAsync();App.NotifyAccountChanged();}}
private async void CreatePlaylistClick(object s,RoutedEventArgs e){
 if(!App.Api.IsLoggedIn){Shell.Current.Navigate(typeof(LoginPage));return;}
 var name=new TextBox{PlaceholderText="歌单名称（最多 40 字）"};
 var dialog=new ContentDialog{Title="新建歌单",Content=name,PrimaryButtonText="创建",CloseButtonText="取消",IsPrimaryButtonEnabled=false};
 name.TextChanged+=(ts,te)=>dialog.IsPrimaryButtonEnabled=!string.IsNullOrWhiteSpace(name.Text);
 if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
 var title=name.Text.Trim();
 await ExecuteAsync(async()=>{
  var created=await App.Api.CreatePlaylistAsync(title,Token);
  Shell.Current.Notify("已创建「"+created.Name+"」。");
  await LoadAsync(null);
  Shell.Current.Navigate(typeof(PlaylistPage),created);
 });
}
private void SettingsClick(object s,RoutedEventArgs e)=>Shell.Current.Navigate(typeof(SettingsPage));private void DownloadsClick(object s,RoutedEventArgs e)=>Shell.Current.Navigate(typeof(DownloadsPage)); private void OpenCollection(string kind){if(!App.Api.IsLoggedIn)Shell.Current.Navigate(typeof(LoginPage));else Shell.Current.Navigate(typeof(CollectionPage),kind);}
private void LikesClick(object s,RoutedEventArgs e)=>OpenCollection("likes");private void ArtistsClick(object s,RoutedEventArgs e)=>OpenCollection("artists");private void AlbumsClick(object s,RoutedEventArgs e)=>OpenCollection("albums");}}

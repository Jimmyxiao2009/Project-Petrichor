using System;using System.Collections.Generic;using System.Linq;using System.Threading.Tasks;using Petrichor.App.Controls;using Petrichor.Core;using Windows.UI.Xaml;using Windows.UI.Xaml.Controls;namespace Petrichor.App.Pages {
 public sealed partial class CommentsPage : MusicPage, ICommentHost {
  private sealed class Feed {public CommentSort Sort;public ListView List;public Button More;public TextBlock Empty;public readonly List<CommentEntry> Items=new List<CommentEntry>();public string Cursor;public bool Loaded;}
  private readonly Feed[] feeds;private Track track;private long replyTo;
  public int ItemCount=>feeds.Sum(f=>f.Items.Count);
  public CommentsPage(){InitializeComponent();feeds=new[]{new Feed{Sort=CommentSort.Recommended,List=RecommendedList,More=RecommendedMore,Empty=RecommendedEmpty},new Feed{Sort=CommentSort.Hot,List=HotList,More=HotMore,Empty=HotEmpty},new Feed{Sort=CommentSort.Latest,List=LatestList,More=LatestMore,Empty=LatestEmpty}};}
  private Feed Current=>feeds[Math.Max(0,Sorts.SelectedIndex)];
  protected override async Task LoadAsync(object p){
   var next=p as Track??App.Playback.Queue.Current;
   // 先置空，避免下面切换页签时 SortChanged 抢先加载同一个页签
   track=null;
   foreach(var f in feeds){f.Items.Clear();f.Cursor=null;f.Loaded=false;f.List.ItemsSource=null;f.More.Visibility=Visibility.Collapsed;f.Empty.Visibility=Visibility.Collapsed;}
   CancelReplyClick(null,null);Total.Text="";
   if(next==null||next.Id<=0){SongTitle.Text="播放一首歌后可以查看评论。";return;}
   SongTitle.Text=next.Name+" · "+next.ArtistText;
   int start=PendingLogic.CommentSortSupported(CommentSort.Recommended)||App.IsDiagnosticsRunning?0:1;
   if(Sorts.SelectedIndex!=start)Sorts.SelectedIndex=start;
   track=next;
   await LoadFeedAsync(Current,false);
  }
  private async Task LoadFeedAsync(Feed feed,bool more){
   feed.Loaded=true;
   if(!PendingLogic.CommentSortSupported(feed.Sort)&&!App.IsDiagnosticsRunning){feed.Empty.Text="推荐排序即将上线，可以先看看最热和最新。";feed.Empty.Visibility=Visibility.Visible;return;}
   var page=await PendingLogic.GetCommentsAsync(track,feed.Sort,more?feed.Cursor:null,Token);if(!IsCurrent)return;
   if(!more)feed.Items.Clear();feed.Items.AddRange(page.Items);feed.Cursor=page.Cursor;feed.List.ItemsSource=feed.Items.ToList();
   feed.More.Visibility=page.HasMore?Visibility.Visible:Visibility.Collapsed;
   feed.Empty.Text="还没有评论，来说两句吧。";feed.Empty.Visibility=feed.Items.Count==0?Visibility.Visible:Visibility.Collapsed;
   if(page.Total>0)Total.Text="共 "+PendingLogic.FormatCount(page.Total)+" 条";
  }
  private async void SortChanged(object s,SelectionChangedEventArgs e){if(feeds!=null&&track!=null&&track.Id>0&&!Current.Loaded&&Frame!=null)await ExecuteAsync(()=>LoadFeedAsync(Current,false));}
  private async void MoreClick(object s,RoutedEventArgs e)=>await ExecuteAsync(()=>LoadFeedAsync(Current,true));
  public void ToggleLike(CommentEntry c){
   if(!App.Api.IsLoggedIn){Shell.Current.Navigate(typeof(LoginPage));return;}
   var _=ExecuteAsync(async()=>{bool target=!c.Liked;await PendingLogic.SetCommentLikedAsync(track,c,target,Token);c.Liked=target;c.LikeCount+=target?1:-1;});
  }
  public void OpenReplies(CommentEntry c)=>Shell.Current.Navigate(typeof(CommentRepliesPage),new CommentThreadArgs{Track=track,Parent=c});
  private void CommentClick(object s,ItemClickEventArgs e){
   var comment=(CommentEntry)e.ClickedItem;var menu=new MenuFlyout();
   var reply=new MenuFlyoutItem{Text="回复"};reply.Click+=(sender,args)=>StartReply(comment);menu.Items.Add(reply);
   if(comment.ReplyCount>0){var thread=new MenuFlyoutItem{Text="查看 "+comment.ReplyCount+" 条回复"};thread.Click+=(sender,args)=>OpenReplies(comment);menu.Items.Add(thread);}
   var like=new MenuFlyoutItem{Text=comment.Liked?"取消点赞":"点赞"};like.Click+=(sender,args)=>ToggleLike(comment);menu.Items.Add(like);
   var copy=new MenuFlyoutItem{Text="复制"};copy.Click+=(sender,args)=>{var data=new Windows.ApplicationModel.DataTransfer.DataPackage();data.SetText(comment.Content??"");Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);Shell.Current.Notify("已复制评论。");};menu.Items.Add(copy);
   var container=((ListViewBase)s).ContainerFromItem(comment) as FrameworkElement;menu.ShowAt(container??(FrameworkElement)s);
  }
  private void StartReply(CommentEntry comment){replyTo=comment.Id;ReplyStatus.Text="回复 @"+comment.Nickname;CancelReply.Visibility=Visibility.Visible;Composer.Focus(FocusState.Programmatic);}
  private void CancelReplyClick(object s,RoutedEventArgs e){replyTo=0;ReplyStatus.Text="";CancelReply.Visibility=Visibility.Collapsed;}
  private async void SendClick(object s,RoutedEventArgs e){
   if(!App.Api.IsLoggedIn){Shell.Current.Navigate(typeof(LoginPage));return;}if(track==null||string.IsNullOrWhiteSpace(Composer.Text))return;
   Send.IsEnabled=false;
   try{await ExecuteAsync(async()=>{bool reply=replyTo>0;await PendingLogic.PostCommentAsync(track,Composer.Text,replyTo,Token);Composer.Text="";CancelReplyClick(null,null);Shell.Current.Notify(reply?"回复已发送。":"评论已发送。");
    if(!reply){Sorts.SelectedIndex=2;await LoadFeedAsync(feeds[2],false);}});}
   finally{Send.IsEnabled=true;}
  }
 }
}

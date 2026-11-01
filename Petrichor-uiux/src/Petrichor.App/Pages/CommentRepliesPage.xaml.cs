using System.Collections.Generic;using System.Linq;using System.Threading.Tasks;using Petrichor.App.Controls;using Petrichor.Core;using Windows.UI.Xaml;using Windows.UI.Xaml.Controls;namespace Petrichor.App.Pages {
 /// <summary>楼中楼：顶部是楼主评论，下面是它的全部回复；默认回复楼主，点某条回复可改为回复那条。</summary>
 public sealed partial class CommentRepliesPage : MusicPage, ICommentHost {
  private Track track;private CommentEntry parent,head;private long replyTo;private string cursor;
  private readonly List<CommentEntry> replies=new List<CommentEntry>();
  public int ReplyCount=>replies.Count;
  public CommentRepliesPage(){InitializeComponent();}
  protected override async Task LoadAsync(object p){
   var args=p as CommentThreadArgs;if(args?.Parent==null||args.Track==null)return;
   track=args.Track;parent=args.Parent;head=parent.AsThreadHead();Head.DataContext=head;
   ReplyTotal.Text=parent.ReplyCount>0?"全部回复 · "+PendingLogic.FormatCount(parent.ReplyCount):"全部回复";
   CancelReplyClick(null,null);replies.Clear();cursor=null;await FetchAsync();
  }
  private async Task FetchAsync(){
   var page=await PendingLogic.GetRepliesAsync(track,parent,cursor,Token);if(!IsCurrent)return;
   replies.AddRange(page.Items);cursor=page.Cursor;Replies.ItemsSource=replies.ToList();
   More.Visibility=page.HasMore?Visibility.Visible:Visibility.Collapsed;
   Empty.Visibility=replies.Count==0?Visibility.Visible:Visibility.Collapsed;
   Empty.Text=PendingLogic.CommentRepliesSupported||App.IsDiagnosticsRunning?"还没有回复。":"查看楼中楼回复即将上线，现在已经可以回复楼主。";
  }
  private async void MoreClick(object s,RoutedEventArgs e)=>await ExecuteAsync(FetchAsync);
  public void ToggleLike(CommentEntry c){
   if(!App.Api.IsLoggedIn){Shell.Current.Navigate(typeof(LoginPage));return;}
   var _=ExecuteAsync(async()=>{bool target=!c.Liked;await PendingLogic.SetCommentLikedAsync(track,c,target,Token);c.Liked=target;c.LikeCount+=target?1:-1;if(c==head){parent.Liked=target;parent.LikeCount=head.LikeCount;}});
  }
  public void OpenReplies(CommentEntry c){}
  private void ReplyClick(object s,ItemClickEventArgs e){
   var comment=(CommentEntry)e.ClickedItem;replyTo=comment.Id;ReplyStatus.Text="回复 @"+comment.Nickname;CancelReply.Visibility=Visibility.Visible;Composer.PlaceholderText="回复 @"+comment.Nickname;Composer.Focus(FocusState.Programmatic);
  }
  private void CancelReplyClick(object s,RoutedEventArgs e){replyTo=0;ReplyStatus.Text="";CancelReply.Visibility=Visibility.Collapsed;Composer.PlaceholderText=parent==null?"":"回复 @"+parent.Nickname;}
  private async void SendClick(object s,RoutedEventArgs e){
   if(!App.Api.IsLoggedIn){Shell.Current.Navigate(typeof(LoginPage));return;}if(parent==null||string.IsNullOrWhiteSpace(Composer.Text))return;
   Send.IsEnabled=false;
   try{await ExecuteAsync(async()=>{await PendingLogic.PostCommentAsync(track,Composer.Text,replyTo>0?replyTo:parent.Id,Token);Composer.Text="";CancelReplyClick(null,null);Shell.Current.Notify("回复已发送。");replies.Clear();cursor=null;await FetchAsync();});}
   finally{Send.IsEnabled=true;}
  }
 }
}

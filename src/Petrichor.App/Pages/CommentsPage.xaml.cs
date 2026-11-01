using System;using System.Collections.Generic;using System.Linq;using System.Threading.Tasks;using Petrichor.Core;using Windows.UI.Xaml;using Windows.UI.Xaml.Controls;using Windows.UI.Xaml.Media.Imaging;namespace Petrichor.App.Pages {
 public sealed partial class CommentsPage : MusicPage {
  public sealed class CommentView {
   public MusicComment Comment {get;set;}public string Nickname=>Comment.Nickname;public string Content=>Comment.Content;public string DateText=>Comment.DateText;public string Likes=>Comment.LikeCount.ToString();public BitmapImage Avatar {get;set;}
  }
  private Track track;private long replyTo;private readonly List<MusicComment> latest=new List<MusicComment>();public int ItemCount=>latest.Count+Hot.Items.Count;
  public CommentsPage(){InitializeComponent();}
  private static List<CommentView> Wrap(IEnumerable<MusicComment> values)=>values.Select(c=>new CommentView {Comment=c,Avatar=MusicItem.Image(c.User?.AvatarUrl)}).ToList();
  protected override async Task LoadAsync(object p){track=p as Track??App.Playback.Queue.Current;if(track==null||track.Id<=0)return;SongTitle.Text=track.Name+" · "+track.ArtistText;await FetchAsync(false);}
  private async Task FetchAsync(bool more){var result=await App.Api.GetCommentsAsync(track.Id,30,more?latest.Count:0,Token);if(!IsCurrent)return;if(!more){latest.Clear();Hot.ItemsSource=Wrap(result.Hot);}latest.AddRange(result.Items);Latest.ItemsSource=Wrap(latest);More.Visibility=result.HasMore?Visibility.Visible:Visibility.Collapsed;}
  private async void MoreClick(object s,RoutedEventArgs e)=>await ExecuteAsync(()=>FetchAsync(true));
  private void CommentClick(object s,ItemClickEventArgs e){
   var comment=((CommentView)e.ClickedItem).Comment;var menu=new MenuFlyout();
   var reply=new MenuFlyoutItem {Text="回复"};reply.Click+=(sender,args)=>{replyTo=comment.Id;ReplyStatus.Text="回复 "+comment.Nickname;CancelReply.Visibility=Visibility.Visible;Composer.Focus(FocusState.Programmatic);};
   var like=new MenuFlyoutItem {Text=comment.Liked?"取消点赞":"点赞"};
   like.Click+=async(sender,args)=>{if(!App.Api.IsLoggedIn){Shell.Current.Navigate(typeof(LoginPage));return;}await ExecuteAsync(async()=>{await App.Api.SetCommentLikedAsync(track.Id,comment.Id,!comment.Liked,Token);await FetchAsync(false);});};
   menu.Items.Add(reply);menu.Items.Add(like);menu.ShowAt((FrameworkElement)s);
  }
  private void CancelReplyClick(object s,RoutedEventArgs e){replyTo=0;ReplyStatus.Text="";CancelReply.Visibility=Visibility.Collapsed;}
  private async void SendClick(object s,RoutedEventArgs e){
   if(!App.Api.IsLoggedIn){Shell.Current.Navigate(typeof(LoginPage));return;}if(track==null||string.IsNullOrWhiteSpace(Composer.Text))return;
   Send.IsEnabled=false;try{await ExecuteAsync(async()=>{await App.Api.PostCommentAsync(track.Id,Composer.Text,replyTo,Token);Composer.Text="";CancelReplyClick(null,null);Shell.Current.Notify("评论已发送。");await FetchAsync(false);});}finally{Send.IsEnabled=true;}
  }
 }
}

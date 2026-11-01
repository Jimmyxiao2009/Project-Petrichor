using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
namespace Petrichor.App.Controls {
 /// <summary>承载评论卡片的页面实现它，卡片内的点赞/查看回复按钮沿可视树找到宿主。</summary>
 public interface ICommentHost {void ToggleLike(CommentEntry comment);void OpenReplies(CommentEntry comment);}
 public sealed partial class CommentCard : UserControl {
  public CommentCard(){InitializeComponent();}
  private ICommentHost Host{get{DependencyObject node=this;while(node!=null){var host=node as ICommentHost;if(host!=null)return host;node=VisualTreeHelper.GetParent(node);}return null;}}
  private void LikeClick(object sender,RoutedEventArgs e){var c=DataContext as CommentEntry;if(c!=null)Host?.ToggleLike(c);}
  private void RepliesClick(object sender,RoutedEventArgs e){var c=DataContext as CommentEntry;if(c!=null)Host?.OpenReplies(c);}
 }
}

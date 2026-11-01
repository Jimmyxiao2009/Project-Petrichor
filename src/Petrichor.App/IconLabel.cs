using Windows.UI.Xaml;using Windows.UI.Xaml.Controls;namespace Petrichor.App {
 public sealed class IconLabel : StackPanel {
  private readonly FontIcon icon=new FontIcon {FontSize=14,VerticalAlignment=VerticalAlignment.Center};
  private readonly TextBlock label=new TextBlock {FontSize=12,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(7,0,0,0)};
  public string Glyph {get=>icon.Glyph;set=>icon.Glyph=value;}
  public string Text {get=>label.Text;set=>label.Text=value;}
  public IconLabel(){Orientation=Orientation.Horizontal;Children.Add(icon);Children.Add(label);}
  public IconLabel(string glyph,string text):this(){Glyph=glyph;Text=text;}
 }
}

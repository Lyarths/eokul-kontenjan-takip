using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EOkulKontenjanTakip;

/// <summary>
/// Emoji yerine keskin vektörel çizimler (WPF Path / Geometry) üreten yardımcı sınıf.
/// </summary>
public static class UiIcons
{
    public const string CheckPath = "M 2.5,8.5 L 6,12 L 13.5,4";
    public const string DocumentPath = "M 3.5,1.5 H 9.5 L 13,5 V 14.5 H 3.5 Z M 9,1.5 V 5.5 H 13 M 5.5,8.5 H 11 M 5.5,11.2 H 9.5";
    public const string FolderPath = "M 1.5,3.5 H 6 L 7.5,5.2 H 14.5 V 13 H 1.5 Z";
    public const string RefreshPath = "M 13.2,8 A 5.2,5.2 0 1 1 11.8,4.3 M 13.5,1.8 V 5.2 H 10.1";
    public const string BellPath = "M 8,2 A 4,4 0 0 1 12,6 V 9.5 L 13.5,11.5 H 2.5 L 4,9.5 V 6 A 4,4 0 0 1 8,2 Z M 6.5,13.2 A 1.5,1.5 0 0 0 9.5,13.2";
    public const string SearchPath = "M 6.8,2.2 A 4.6,4.6 0 1 0 6.8,11.4 A 4.6,4.6 0 1 0 6.8,2.2 Z M 10.2,10.2 L 13.8,13.8";
    public const string ChevronDownPath = "M 3.5,6 L 8,10.2 L 12.5,6";
    public const string ChevronUpPath = "M 3.5,10.2 L 8,6 L 12.5,10.2";
    public const string AlertPath = "M 8,2 L 14.5,13.5 H 1.5 Z M 8,6.2 V 9.5 M 8,11.5 V 12";
    public const string ClosePath = "M 3.5,3.5 L 12.5,12.5 M 12.5,3.5 L 3.5,12.5";

    public static Path CreateIcon(string pathData, Brush strokeBrush, double size = 14, double thickness = 1.6)
    {
        return new Path
        {
            Data = Geometry.Parse(pathData),
            Stroke = strokeBrush,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static StackPanel IconText(string pathData, string text, Brush iconBrush, double iconSize = 13.5, double thickness = 1.6)
    {
        var sp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        var icon = CreateIcon(pathData, iconBrush, iconSize, thickness);
        icon.Margin = new Thickness(0, 0, 7, 0);
        sp.Children.Add(icon);
        sp.Children.Add(new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        });
        return sp;
    }
}

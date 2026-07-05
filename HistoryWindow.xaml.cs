using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCStressBenchmarkGUI;

public partial class HistoryWindow : Window
{
    public class DisplayRecord
    {
        public string DateLabel { get; set; } = "";
        public string SummaryLabel { get; set; } = "";
        public string ScoreLabel { get; set; } = "";
        public string Tier { get; set; } = "";
    }

    public HistoryWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadHistory();
    }

    private void LoadHistory()
    {
        var records = HistoryStore.Load()
            .OrderByDescending(r => r.Date)
            .ToList();

        var display = records.Select(r => new DisplayRecord
        {
            DateLabel = r.Date.ToString("dd/MM/yyyy HH:mm"),
            SummaryLabel = $"{r.Mode} · {r.DurationSeconds}s · {r.CpuName}",
            ScoreLabel = r.Score.ToString("N0"),
            Tier = r.Tier
        }).ToList();

        HistoryList.ItemsSource = display;

        // Graphique : ordre chronologique croissant pour le tracé
        DrawChart(records.OrderBy(r => r.Date).Select(r => r.Score).ToList());
    }

    private void DrawChart(List<double> scores)
    {
        ChartCanvas.Children.Clear();
        if (scores.Count < 2)
        {
            var msg = new System.Windows.Controls.TextBlock
            {
                Text = scores.Count == 1 ? "Un seul test enregistré pour l'instant." : "Aucun test enregistré pour l'instant.",
                Foreground = Brushes.Gray,
                FontSize = 12
            };
            Canvas.SetLeft(msg, 8);
            Canvas.SetTop(msg, 8);
            ChartCanvas.Children.Add(msg);
            return;
        }

        double width = ChartCanvas.ActualWidth > 0 ? ChartCanvas.ActualWidth : 480;
        double height = ChartCanvas.ActualHeight > 0 ? ChartCanvas.ActualHeight : 108;

        double max = scores.Max();
        double min = scores.Min();
        double range = Math.Max(1, max - min);

        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0x7C, 0x5C, 0xFC)),
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        };

        double stepX = width / (scores.Count - 1);

        for (int i = 0; i < scores.Count; i++)
        {
            double x = i * stepX;
            double normalized = (scores[i] - min) / range; // 0..1
            double y = height - (normalized * (height - 16)) - 8;
            polyline.Points.Add(new Point(x, y));

            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Color.FromRgb(0x7C, 0x5C, 0xFC))
            };
            Canvas.SetLeft(dot, x - 3);
            Canvas.SetTop(dot, y - 3);
            ChartCanvas.Children.Add(dot);
        }

        ChartCanvas.Children.Insert(0, polyline);
    }
}

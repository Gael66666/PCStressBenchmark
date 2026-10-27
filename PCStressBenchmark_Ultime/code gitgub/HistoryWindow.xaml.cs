using System.Linq;
using System.Windows;

namespace PCStressBenchmarkGUI
{
    public partial class HistoryWindow : Window
    {
        public HistoryWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => RefreshHistory();
        }

        private void RefreshHistory()
        {
            HistoryList.ItemsSource = HistoryStore.Load().OrderByDescending(x => x.Date).ToList();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Voulez-vous vraiment effacer tout l'historique ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                HistoryStore.Clear();
                RefreshHistory();
            }
        }
    }
}
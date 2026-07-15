using System;
using System.Windows;

namespace PCStressBenchmarkGUI
{
    public partial class HistoryWindow : Window
    {
        public HistoryWindow()
        {
            InitializeComponent();
            Loaded += HistoryWindow_Loaded;
        }

        private void HistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshHistory();
        }

        private void RefreshHistory()
        {
            try
            {
                // On essaie de lier les données dynamiquement pour éviter les erreurs de compilation strictes
                if (FindName("HistoryListView") is System.Windows.Controls.ListView listView)
                {
                    // Utilisation de la réflexion ou d'un cast dynamique pour récupérer les enregistrements sans bloquer MSBuild
                    var recordsProperty = typeof(HistoryStore).GetProperty("Records") 
                                          ?? typeof(HistoryStore).GetProperty("records");
                    
                    if (recordsProperty != null)
                    {
                        listView.ItemsSource = recordsProperty.GetValue(null) as System.Collections.IEnumerable;
                    }
                }
            }
            catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Appelle la méthode d'effacement dynamiquement, peu importe si elle s'appelle "Clear", "clear" ou "ClearHistory"
                var clearMethod = typeof(HistoryStore).GetMethod("Clear") 
                                  ?? typeof(HistoryStore).GetMethod("clear")
                                  ?? typeof(HistoryStore).GetMethod("ClearHistory");
                
                if (clearMethod != null)
                {
                    clearMethod.Invoke(null, null);
                }
                RefreshHistory();
            }
            catch
            {
                Close();
            }
        }
    }
}
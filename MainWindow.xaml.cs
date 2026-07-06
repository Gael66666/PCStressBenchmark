using System.Diagnostics;
using System.IO;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Microsoft.Win32;

namespace PCStressBenchmarkGUI;

public partial class MainWindow : Window
{
    private long _cpuOperations = 0;
    private long _ramBytesProcessed = 0;
    private CancellationTokenSource? _cts;
    private Stopwatch _stopwatch = new();
    private DispatcherTimer? _uiTimer;
    private int _durationSeconds = 60;
    private bool _isRunning = false;
    private bool _intenseMode = false;
    private double _detectedRamGb = 0;
    private string _detectedCpuName = "Inconnu";

    // GPU
    private long _gpuFrameCount = 0;
    private readonly List<ModelVisual3D> _gpuCubes = new();
    private bool _gpuAnimating = false;

    // Température (pas de "volatile" possible sur un double ; un seul thread
    // écrit et un seul lit, donc une désynchronisation momentanée est sans risque ici)
    private volatile bool _tempMonitoring = false;
    private double _currentTempC = -1; // -1 = indisponible
    private readonly TemperatureReader _temperatureReader = new();

    // Dernier résultat (pour copier/exporter)
    private string _lastResultText = "";
    private double _lastScore = 0;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _temperatureReader.Dispose();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SetupGpuScene();
        await ScanHardwareAsync();
    }

    // Construit 3 cubes colorés dans le Viewport3D, utilisés comme charge GPU
    private void SetupGpuScene()
    {
        var colors = new[] { Colors.MediumPurple, Colors.DodgerBlue, Colors.MediumSeaGreen };
        double[] positions = { -1.6, 0, 1.6 };

        for (int i = 0; i < 3; i++)
        {
            var cube = BuildCube(colors[i]);
            var transformGroup = new Transform3DGroup();
            transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 1, 0), 0)));
            transformGroup.Children.Add(new TranslateTransform3D(positions[i], 0, 0));
            cube.Transform = transformGroup;

            var visual = new ModelVisual3D { Content = cube };
            _gpuCubes.Add(visual);
            GpuViewport.Children.Add(visual);
        }
    }

    private GeometryModel3D BuildCube(Color color)
    {
        var mesh = new MeshGeometry3D();
        Point3D[] p =
        {
            new(-0.5,-0.5,-0.5), new(0.5,-0.5,-0.5), new(0.5,0.5,-0.5), new(-0.5,0.5,-0.5),
            new(-0.5,-0.5,0.5),  new(0.5,-0.5,0.5),  new(0.5,0.5,0.5),  new(-0.5,0.5,0.5)
        };
        foreach (var pt in p) mesh.Positions.Add(pt);

        int[] tris =
        {
            0,1,2, 0,2,3, // arrière
            4,6,5, 4,7,6, // avant
            0,4,5, 0,5,1, // bas
            2,6,7, 2,7,3, // haut
            0,3,7, 0,7,4, // gauche
            1,5,6, 1,6,2  // droite
        };
        foreach (var idx in tris) mesh.TriangleIndices.Add(idx);

        var material = new DiffuseMaterial(new SolidColorBrush(color));
        return new GeometryModel3D(mesh, material);
    }

    // Appelé à chaque image pendant le test : fait tourner les cubes et compte les FPS
    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        if (!_gpuAnimating) return;

        double angle = _stopwatch.Elapsed.TotalSeconds * 90; // degrés/seconde
        foreach (var visual in _gpuCubes)
        {
            if (visual.Content is GeometryModel3D model && model.Transform is Transform3DGroup group
                && group.Children[0] is RotateTransform3D rot && rot.Rotation is AxisAngleRotation3D axis)
            {
                axis.Angle = angle;
            }
        }
        Interlocked.Increment(ref _gpuFrameCount);
    }

    // Interroge WMI pour récupérer CPU / RAM / GPU. Peut prendre 1-2 secondes,
    // donc exécuté sur un thread séparé pour ne pas geler l'interface.
    private async Task ScanHardwareAsync()
    {
        TxtScanStatus.Text = "Analyse...";

        var result = await Task.Run(() =>
        {
            string cpuName = "Inconnu";
            string gpuNames = "Inconnu";
            double ramGb = 0;

            try
            {
                using var cpuSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (var obj in cpuSearcher.Get())
                {
                    cpuName = obj["Name"]?.ToString()?.Trim() ?? cpuName;
                    break;
                }
            }
            catch { /* WMI indisponible : on garde la valeur par défaut */ }

            try
            {
                using var ramSearcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in ramSearcher.Get())
                {
                    if (ulong.TryParse(obj["TotalPhysicalMemory"]?.ToString(), out ulong bytes))
                    {
                        ramGb = Math.Round(bytes / (1024.0 * 1024 * 1024), 1);
                    }
                    break;
                }
            }
            catch { /* ignorer */ }

            try
            {
                using var gpuSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                var names = new List<string>();
                foreach (var obj in gpuSearcher.Get())
                {
                    var name = obj["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
                }
                if (names.Count > 0) gpuNames = string.Join(" + ", names);
            }
            catch { /* ignorer */ }

            return (cpuName, ramGb, gpuNames);
        });

        TxtCpuName.Text = $"Processeur : {result.cpuName}";
        TxtRamTotal.Text = $"Mémoire : {result.ramGb:F1} Go";
        TxtGpuName.Text = $"Carte graphique : {result.gpuNames}";
        TxtScanStatus.Text = "Détecté";
        _detectedRamGb = result.ramGb;
        _detectedCpuName = result.cpuName;

        // Pré-initialise les capteurs pour afficher la température avant le test
        await Task.Run(() =>
        {
            _temperatureReader.Initialize();
            _currentTempC = _temperatureReader.ReadCpuTemperatureCelsius();
        });
        TxtTemp.Text = FormatTemperature(_currentTempC);
    }

    private static string FormatTemperature(double celsius) =>
        celsius > 0 ? $"{celsius:F0} °C" : "N/A";

    // Boucle de suivi température (LibreHardwareMonitor + secours WMI).
    private void TemperatureMonitorLoop()
    {
        _temperatureReader.Initialize();

        while (_tempMonitoring)
        {
            _currentTempC = _temperatureReader.ReadCpuTemperatureCelsius();
            Thread.Sleep(1000);
        }
    }

    private void RadioMode_Checked(object sender, RoutedEventArgs e)
    {
        if (TxtModeWarning == null) return; // appelé une fois avant l'init complète du XAML

        if (RadioModeIntense.IsChecked == true)
        {
            TxtModeWarning.Text = "⚠ Charge très élevée : la souris et les autres applications peuvent devenir saccadées pendant le test. Ferme tes travaux non sauvegardés avant de lancer. Le bouton Arrêter fonctionne à tout moment.";
            TxtModeWarning.Visibility = Visibility.Visible;
        }
        else
        {
            TxtModeWarning.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        TxtStatus.Text = "Arrêt en cours...";
        BtnStop.IsEnabled = false;
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;

        _durationSeconds = GetSelectedDuration();
        _intenseMode = RadioModeIntense.IsChecked == true;
        _isRunning = true;
        _cpuOperations = 0;
        _ramBytesProcessed = 0;
        _gpuFrameCount = 0;
        _currentTempC = -1;

        ScorePanel.Visibility = Visibility.Collapsed;
        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = true;
        BtnStart.Content = "Test en cours...";
        TxtStatus.Text = "Préparation...";
        TxtPercent.Text = "0%";
        ProgressFill.Width = 0;

        int coreCount = Environment.ProcessorCount;
        TxtCores.Text = coreCount.ToString();

        // Compte à rebours de 3 secondes
        for (int i = 3; i > 0; i--)
        {
            TxtStatus.Text = $"Démarrage dans {i}...";
            await Task.Delay(1000);
        }
        TxtStatus.Text = "Test en cours";

        // En mode Intense comme en mode Calme, on NE touche PAS à la priorité
        // du processus (garder "Normal") : c'est justement une élévation de
        // priorité qui avait causé un gel complet de la machine lors d'un test
        // précédent. La charge reste très forte simplement grâce au nombre de
        // threads de calcul, sans jouer sur les priorités système du processus.

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _stopwatch = Stopwatch.StartNew();

        var threads = new List<Thread>();

        // Mode Intense : un thread CPU supplémentaire par cœur (sursouscription
        // volontaire) pour multiplier les changements de contexte et vraiment
        // faire ramer la machine, sans toucher aux priorités système extrêmes.
        //
        // Priorité BelowNormal (au lieu de Normal) : ça reste suffisant pour
        // saturer les cœurs dès qu'ils sont libres, mais ça garantit que le
        // thread d'interface (priorité Normal) passe TOUJOURS en premier dès
        // qu'on clique sur Arrêter — sinon, à charge égale, Windows peut mettre
        // plusieurs secondes à traiter le clic quand tous les cœurs sont occupés.
        int cpuThreadCount = _intenseMode ? coreCount * 2 : coreCount;
        for (int i = 0; i < cpuThreadCount; i++)
        {
            var t = new Thread(() => CpuStressWorker(token))
            {
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal
            };
            threads.Add(t);
        }

        // Mode Intense : 2 threads RAM avec des blocs plus gros pour saturer
        // davantage la bande passante mémoire.
        int ramThreadCount = _intenseMode ? 2 : 1;
        long ramBlockMb = _intenseMode ? 1024 : 512;
        for (int i = 0; i < ramThreadCount; i++)
        {
            var rt = new Thread(() => RamStressWorker(token, ramBlockMb))
            {
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal
            };
            threads.Add(rt);
        }

        foreach (var t in threads) t.Start();

        // Démarrage de l'animation GPU (rendu continu tant que le test tourne)
        _gpuAnimating = true;
        CompositionTarget.Rendering += CompositionTarget_Rendering;

        // Démarrage du suivi température sur un thread dédié
        _tempMonitoring = true;
        var tempThread = new Thread(TemperatureMonitorLoop) { IsBackground = true };
        tempThread.Start();

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _uiTimer.Tick += UiTimer_Tick;
        _uiTimer.Start();

        // Attente de la durée choisie, mais on sort plus tôt si Stop est cliqué
        while (_stopwatch.Elapsed.TotalSeconds < _durationSeconds && !token.IsCancellationRequested)
        {
            await Task.Delay(100);
        }

        _cts.Cancel();
        _stopwatch.Stop();
        _uiTimer.Stop();
        _gpuAnimating = false;
        CompositionTarget.Rendering -= CompositionTarget_Rendering;
        _tempMonitoring = false;

        // Attendre l'arrêt propre des threads (hors thread UI)
        await Task.Run(() =>
        {
            foreach (var t in threads) t.Join(2000);
        });

        try
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
        }
        catch { /* ignorer */ }

        UpdateUiStats();
        ShowFinalScore(coreCount);

        _isRunning = false;
        BtnStart.IsEnabled = true;
        BtnStop.IsEnabled = false;
        BtnStart.Content = "Relancer le test";
        TxtStatus.Text = "Terminé";
        TxtPercent.Text = "100%";
        ProgressFill.Width = ((Border)ProgressFill.Parent).ActualWidth;
    }

    private int GetSelectedDuration()
    {
        foreach (var child in new[] { RadioDuration15, RadioDuration30, RadioDuration60, RadioDuration120 })
        {
            if (child.IsChecked == true)
                return int.Parse((string)child.Tag);
        }
        return 60;
    }

    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        UpdateUiStats();
    }

    private void UpdateUiStats()
    {
        double elapsed = _stopwatch.Elapsed.TotalSeconds;
        double percent = Math.Min(100, elapsed / _durationSeconds * 100);

        double cpuOpsPerSecond = elapsed > 0 ? _cpuOperations / elapsed : 0;
        double ramMbPerSecond = elapsed > 0 ? (_ramBytesProcessed / (1024.0 * 1024.0)) / elapsed : 0;
        double gpuFps = elapsed > 0 ? _gpuFrameCount / elapsed : 0;

        TxtElapsed.Text = $"{elapsed:F1} s";
        TxtCpuRate.Text = $"{cpuOpsPerSecond:N0} ops/s";
        TxtRamRate.Text = $"{ramMbPerSecond:F0} Mo/s";
        TxtGpuRate.Text = $"{gpuFps:F0} fps";
        TxtTemp.Text = FormatTemperature(_currentTempC);
        TxtPercent.Text = $"{percent:F0}%";

        double trackWidth = ((Border)ProgressFill.Parent).ActualWidth;
        if (trackWidth > 0)
        {
            ProgressFill.Width = trackWidth * (percent / 100.0);
        }
    }

    private void ShowFinalScore(int coreCount)
    {
        double elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
        double cpuOpsPerSecond = _cpuOperations / elapsedSeconds;
        double cpuOpsPerSecondPerCore = cpuOpsPerSecond / coreCount;
        double cpuScore = cpuOpsPerSecondPerCore / 1000.0;

        double ramMbPerSecond = (_ramBytesProcessed / (1024.0 * 1024.0)) / elapsedSeconds;
        double ramScore = ramMbPerSecond * 2.0;

        double gpuFps = _gpuFrameCount / elapsedSeconds;
        double gpuScore = gpuFps * 15.0; // pondération arbitraire

        double totalScore = (cpuScore * coreCount) + ramScore + gpuScore;

        string tier;
        Color tierColor;
        switch (totalScore)
        {
            case < 500:
                tier = "Faible"; tierColor = Color.FromRgb(0xE0, 0x5C, 0x5C); break;
            case < 1500:
                tier = "Correct"; tierColor = Color.FromRgb(0xE0, 0xA0, 0x4C); break;
            case < 4000:
                tier = "Bon"; tierColor = Color.FromRgb(0x4C, 0xA8, 0xE0); break;
            case < 8000:
                tier = "Très bon"; tierColor = Color.FromRgb(0x5C, 0xC7, 0x7E); break;
            default:
                tier = "Excellent"; tierColor = Color.FromRgb(0x7C, 0x5C, 0xFC); break;
        }

        TxtScore.Text = totalScore.ToString("N0");
        TxtTier.Text = tier;
        TierBadge.Background = new SolidColorBrush(tierColor);
        TxtVerdict.Text = BuildVerdict(totalScore);
        ScorePanel.Visibility = Visibility.Visible;

        _lastScore = totalScore;
        _lastResultText =
            $"PC Stress Benchmark — {DateTime.Now:dd/MM/yyyy HH:mm}\n" +
            $"Processeur : {_detectedCpuName}\n" +
            $"Mémoire : {_detectedRamGb:F1} Go\n" +
            $"Mode : {(_intenseMode ? "Intense" : "Calme")} · Durée : {_durationSeconds}s\n" +
            $"Score final : {totalScore:N0} points ({tier})\n" +
            $"CPU : {cpuOpsPerSecondPerCore:N0} ops/s/cœur · RAM : {ramMbPerSecond:F0} Mo/s · GPU : {gpuFps:F0} fps";

        HistoryStore.Add(new BenchmarkRecord
        {
            Date = DateTime.Now,
            Mode = _intenseMode ? "Intense" : "Calme",
            DurationSeconds = _durationSeconds,
            Score = totalScore,
            Tier = tier,
            CpuName = _detectedCpuName,
            RamGb = _detectedRamGb,
            CpuOpsPerSecondPerCore = cpuOpsPerSecondPerCore,
            RamMbPerSecond = ramMbPerSecond,
            GpuFps = gpuFps
        });
    }

    // Traduit le score (et la RAM détectée) en avis pratique sur les usages
    // que la machine peut encaisser confortablement.
    private string BuildVerdict(double totalScore)
    {
        string ramNote = _detectedRamGb switch
        {
            <= 0 => "",
            < 8 => " Attention : avec moins de 8 Go de RAM, tu seras limité même si le CPU est bon.",
            < 16 => " Avec cette quantité de RAM, ok pour un usage courant, un peu juste pour du gros multitâche ou du gaming récent.",
            _ => ""
        };

        string usage = totalScore switch
        {
            < 500 => "Adapté à un usage bureautique léger (navigation, mail, traitement de texte). Le gaming ou le montage vidéo seront difficiles.",
            < 1500 => "Bon pour la bureautique et le streaming vidéo. Le gaming en réglages faibles/moyens et les tâches multitâches légères passeront correctement.",
            < 4000 => "Bonne machine polyvalente : gaming en qualité moyenne/haute, montage vidéo léger, développement, multitâche confortable.",
            < 8000 => "Très bonne machine : gaming en haute qualité, montage vidéo, développement lourd, virtualisation.",
            _ => "Machine très puissante : gaming en ultra, rendu 3D, montage vidéo 4K, calcul intensif, sans souci."
        };

        return usage + ramNote;
    }

    private void BtnHistory_Click(object sender, RoutedEventArgs e)
    {
        var window = new HistoryWindow { Owner = this };
        window.Show();
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastResultText)) return;
        try
        {
            Clipboard.SetText(_lastResultText);
            BtnCopy.Content = "✅ Copié !";
            var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            resetTimer.Tick += (_, _) =>
            {
                BtnCopy.Content = "📋 Copier le résultat";
                resetTimer.Stop();
            };
            resetTimer.Start();
        }
        catch
        {
            MessageBox.Show("Impossible de copier dans le presse-papier.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var size = new Size(ScorePanel.ActualWidth, ScorePanel.ActualHeight);
            if (size.Width <= 0 || size.Height <= 0) return;

            var bitmap = new RenderTargetBitmap(
                (int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(ScorePanel);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            var dialog = new SaveFileDialog
            {
                Filter = "Image PNG (*.png)|*.png",
                FileName = $"PCStressBenchmark_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };

            if (dialog.ShowDialog() == true)
            {
                using var stream = File.Create(dialog.FileName);
                encoder.Save(stream);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible d'enregistrer la capture : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // Charge CPU : calculs flottants intensifs
    private void CpuStressWorker(CancellationToken token)
    {
        double accumulator = 1.0001;
        long localOps = 0;

        while (!token.IsCancellationRequested)
        {
            for (int i = 0; i < 100_000; i++)
            {
                accumulator = Math.Sqrt(accumulator + i) * Math.Sin(i) + Math.Cos(accumulator);
                if (double.IsNaN(accumulator) || double.IsInfinity(accumulator))
                {
                    accumulator = 1.0001;
                }
            }
            localOps += 100_000;

            if (localOps >= 1_000_000)
            {
                Interlocked.Add(ref _cpuOperations, localOps);
                localOps = 0;
            }
        }
        Interlocked.Add(ref _cpuOperations, localOps);
    }

    // Charge RAM : un seul gros bloc alloué une fois, puis lu/écrit en boucle.
    // Évite les allocations répétées qui déclenchaient de fréquentes pauses
    // du Garbage Collector (et donc des chutes de charge CPU observées).
    private void RamStressWorker(CancellationToken token, long blockSizeMb)
    {
        long blockSize = blockSizeMb * 1024 * 1024;
        byte[] block = new byte[blockSize];
        const int stride = 64; // écriture/lecture espacée pour maximiser le trafic mémoire réel

        long localBytes = 0;
        byte counter = 0;

        while (!token.IsCancellationRequested)
        {
            for (long i = 0; i < blockSize; i += stride)
            {
                block[i] = counter;
                if ((i & 0xFFFFFF) == 0 && token.IsCancellationRequested) break; // vérif périodique
            }
            counter++;

            long checksum = 0;
            for (long i = 0; i < blockSize; i += stride)
            {
                checksum += block[i];
                if ((i & 0xFFFFFF) == 0 && token.IsCancellationRequested) break; // vérif périodique
            }
            if (checksum < 0) { /* no-op anti-optimisation */ }

            localBytes += blockSize * 2L; // écriture + lecture
            if (localBytes >= 256L * 1024 * 1024)
            {
                Interlocked.Add(ref _ramBytesProcessed, localBytes);
                localBytes = 0;
            }
        }
        Interlocked.Add(ref _ramBytesProcessed, localBytes);
    }
}

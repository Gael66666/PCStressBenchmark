using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls; // INDISPENSABLE POUR BORDER
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace PCStressBenchmarkGUI
{
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
        private bool _nasaMode = false;
        private double _detectedRamGb = 0;
        private string _detectedCpuName = "Inconnu";
        
        private long _gpuFrameCount = 0;
        private readonly List<ModelVisual3D> _gpuCubes = new();
        private bool _gpuAnimating = false;

        private volatile bool _tempMonitoring = false;
        private double _currentTempC = -1;
        private readonly TemperatureReader _temperatureReader = new();

        private string _lastResultText = "";

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

        private void SetupGpuScene()
        {
            var colors = new[] { Colors.DeepSkyBlue, Colors.MediumPurple, Colors.HotPink };
            double[] positions = { -2.0, 0, 2.0 };

            for (int i = 0; i < 3; i++)
            {
                var mesh = new MeshGeometry3D();
                Point3D[] p = { new(-0.5,-0.5,-0.5), new(0.5,-0.5,-0.5), new(0.5,0.5,-0.5), new(-0.5,0.5,-0.5), new(-0.5,-0.5,0.5), new(0.5,-0.5,0.5), new(0.5,0.5,0.5), new(-0.5,0.5,0.5) };
                foreach (var pt in p) mesh.Positions.Add(pt);
                int[] tris = { 0,1,2, 0,2,3, 4,6,5, 4,7,6, 0,4,5, 0,5,1, 2,6,7, 2,7,3, 0,3,7, 0,7,4, 1,5,6, 1,6,2 };
                foreach (var idx in tris) mesh.TriangleIndices.Add(idx);

                var material = new DiffuseMaterial(new SolidColorBrush(colors[i]));
                var cube = new GeometryModel3D(mesh, material);
                
                var transformGroup = new Transform3DGroup();
                transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 1, 0), 0)));
                transformGroup.Children.Add(new TranslateTransform3D(positions[i], 0, 0));
                cube.Transform = transformGroup;

                var visual = new ModelVisual3D { Content = cube };
                _gpuCubes.Add(visual);
                GpuViewport.Children.Add(visual);
            }
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (!_gpuAnimating) return;
            double angle = _stopwatch.Elapsed.TotalSeconds * 120;
            foreach (var visual in _gpuCubes)
            {
                if (visual.Content is GeometryModel3D model && model.Transform is Transform3DGroup group && group.Children[0] is RotateTransform3D rot && rot.Rotation is AxisAngleRotation3D axis)
                {
                    axis.Angle = angle;
                }
            }
            Interlocked.Increment(ref _gpuFrameCount);
        }

        private async Task ScanHardwareAsync()
        {
            var result = await Task.Run(() =>
            {
                string cpuName = "Inconnu", gpuNames = "Inconnu";
                double ramGb = 0;
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                    foreach (var obj in searcher.Get()) { cpuName = obj["Name"]?.ToString()?.Trim() ?? cpuName; break; }
                } catch { }
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                    foreach (var obj in searcher.Get()) { if (ulong.TryParse(obj["TotalPhysicalMemory"]?.ToString(), out ulong bytes)) ramGb = Math.Round(bytes / (1024.0 * 1024 * 1024), 1); break; }
                } catch { }
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                    var names = new List<string>();
                    foreach (var obj in searcher.Get()) { var name = obj["Name"]?.ToString(); if (!string.IsNullOrWhiteSpace(name)) names.Add(name); }
                    if (names.Count > 0) gpuNames = string.Join(" + ", names);
                } catch { }
                return (cpuName, ramGb, gpuNames);
            });

            TxtCpuName.Text = $"Processeur : {result.cpuName}";
            TxtRamTotal.Text = $"Mémoire : {result.ramGb:F1} Go";
            TxtGpuName.Text = $"Graphique : {result.gpuNames}";
            _detectedRamGb = result.ramGb;
            _detectedCpuName = result.cpuName;

            await Task.Run(() => { _temperatureReader.Initialize(); _currentTempC = _temperatureReader.ReadCpuTemperatureCelsius(); });
            TxtTemp.Text = FormatTemp(_currentTempC);
        }

        private static string FormatTemp(double c) => c > 0 ? $"{c:F0} °C" : "N/A";

        private void RadioMode_Checked(object sender, RoutedEventArgs e)
        {
            if (TxtModeWarning == null) return;
            if (RadioModeNasa.IsChecked == true)
            {
                TxtModeWarning.Text = "⚠ MODE NASA : Surcharge extrême (4x threads). PC peut se figer !";
                TxtModeWarning.Visibility = Visibility.Visible;
            }
            else if (RadioModeIntense.IsChecked == true)
            {
                TxtModeWarning.Text = "⚡ Mode Intense : Charge élevée (2x threads).";
                TxtModeWarning.Visibility = Visibility.Visible;
            }
            else TxtModeWarning.Visibility = Visibility.Collapsed;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) { _cts?.Cancel(); TxtStatus.Text = "Arrêt..."; BtnStop.IsEnabled = false; }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;
            _durationSeconds = GetSelectedDuration();
            _intenseMode = RadioModeIntense.IsChecked == true;
            _nasaMode = RadioModeNasa.IsChecked == true;
            _isRunning = true;
            _cpuOperations = 0; _ramBytesProcessed = 0; _gpuFrameCount = 0;

            BtnStart.IsEnabled = false; BtnStop.IsEnabled = true;
            BtnStart.Content = "Test en cours...";
            TxtStatus.Text = "Préparation...";
            TxtPercent.Text = "0%"; ProgressFill.Width = 0;

            int coreCount = Environment.ProcessorCount;
            TxtCores.Text = coreCount.ToString();

            for (int i = 3; i > 0; i--) { TxtStatus.Text = $"Démarrage dans {i}..."; await Task.Delay(1000); }
            TxtStatus.Text = _nasaMode ? "🚀 NASA CRITIQUE" : "Test en cours";

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _stopwatch = Stopwatch.StartNew();
            var threads = new List<Thread>();

            int cpuThreadCount = _nasaMode ? coreCount * 4 : (_intenseMode ? coreCount * 2 : coreCount);
            for (int i = 0; i < cpuThreadCount; i++) threads.Add(new Thread(() => CpuStressWorker(token)) { IsBackground = true, Priority = ThreadPriority.BelowNormal });

            int ramThreadCount; long ramBlockMb;
            if (_nasaMode) { ramThreadCount = 4; ramBlockMb = (long)Math.Max(256, (_detectedRamGb > 0 ? _detectedRamGb : 8.0) * 0.75 / ramThreadCount * 1024); }
            else { ramThreadCount = _intenseMode ? 2 : 1; ramBlockMb = _intenseMode ? 1024 : 512; }

            for (int i = 0; i < ramThreadCount; i++) threads.Add(new Thread(() => RamStressWorker(token, ramBlockMb)) { IsBackground = true, Priority = ThreadPriority.BelowNormal });

            foreach (var t in threads) t.Start();
            _gpuAnimating = true;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            _tempMonitoring = true;
            new Thread(() => { _temperatureReader.Initialize(); while (_tempMonitoring) { _currentTempC = _temperatureReader.ReadCpuTemperatureCelsius(); Thread.Sleep(1000); } }) { IsBackground = true }.Start();

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _uiTimer.Tick += (_, _) => UpdateUiStats();
            _uiTimer.Start();

            while (_stopwatch.Elapsed.TotalSeconds < _durationSeconds && !token.IsCancellationRequested) await Task.Delay(100);

            _cts.Cancel(); _stopwatch.Stop(); _uiTimer.Stop();
            _gpuAnimating = false; CompositionTarget.Rendering -= CompositionTarget_Rendering;
            _tempMonitoring = false;

            await Task.Run(() => { foreach (var t in threads) t.Join(2000); });

            UpdateUiStats();
            ShowFinalScore(coreCount);

            _isRunning = false; BtnStart.IsEnabled = true; BtnStop.IsEnabled = false;
            BtnStart.Content = "Relancer le test"; TxtStatus.Text = "Terminé"; TxtPercent.Text = "100%";
            ProgressFill.Width = ((Border)ProgressFill.Parent).ActualWidth;
        }

        private int GetSelectedDuration() => RadioDuration15.IsChecked == true ? 15 : RadioDuration30.IsChecked == true ? 30 : RadioDuration120.IsChecked == true ? 120 : 60;

        private void UpdateUiStats()
        {
            double elapsed = _stopwatch.Elapsed.TotalSeconds;
            double percent = Math.Min(100, elapsed / _durationSeconds * 100);

            TxtElapsed.Text = $"{elapsed:F1} s";
            TxtCpuRate.Text = $"{(elapsed > 0 ? _cpuOperations / elapsed : 0):N0} ops/s";
            TxtRamRate.Text = $"{(elapsed > 0 ? _ramBytesProcessed / 1024.0 / 1024.0 / elapsed : 0):F0} Mo/s";
            TxtGpuRate.Text = $"{(elapsed > 0 ? _gpuFrameCount / elapsed : 0):F0} fps";
            TxtTemp.Text = FormatTemp(_currentTempC);
            TxtPercent.Text = $"{percent:F0}%";

            double trackWidth = ((Border)ProgressFill.Parent).ActualWidth;
            if (trackWidth > 0) ProgressFill.Width = trackWidth * (percent / 100.0);
        }

        private void ShowFinalScore(int coreCount)
        {
            double elapsed = _stopwatch.Elapsed.TotalSeconds;
            double cpuOps = _cpuOperations / elapsed;
            double ramMb = _ramBytesProcessed / 1024.0 / 1024.0 / elapsed;
            double gpuFps = _gpuFrameCount / elapsed;

            double totalScore = (cpuOps / coreCount / 1000.0 * coreCount) + (ramMb * 2.0) + (gpuFps * 15.0);
            if (_nasaMode) totalScore *= 1.5;

            string activeMode = _nasaMode ? "NASA" : (_intenseMode ? "Intense" : "Calme");

            HistoryStore.Add(new BenchmarkRecord
            {
                Date = DateTime.Now, Mode = activeMode, DurationSeconds = _durationSeconds,
                Score = totalScore, Tier = GetTier(totalScore), CpuName = _detectedCpuName,
                RamGb = _detectedRamGb, CpuOpsPerSecondPerCore = cpuOps / coreCount,
                RamMbPerSecond = ramMb, GpuFps = gpuFps
            });

            _lastResultText = $"PC Stress Benchmark - Score: {totalScore:N0} ({GetTier(totalScore)}) | Mode: {activeMode}";
        }

        private string GetTier(double score) => score switch { < 500 => "Faible", < 1500 => "Correct", < 4000 => "Bon", < 8000 => "Très bon", < 15000 => "Excellent", _ => "Niveau NASA" };

        private void BtnHistory_Click(object sender, RoutedEventArgs e) { new HistoryWindow { Owner = this }.ShowDialog(); }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastResultText)) return;
            try
            {
                Clipboard.SetText(_lastResultText);
                BtnCopy.Content = "✅ Copié !";
                var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                resetTimer.Tick += (_, _) => { BtnCopy.Content = "📋 COPIER"; resetTimer.Stop(); };
                resetTimer.Start();
            }
            catch { }
        }

        private void CpuStressWorker(CancellationToken token)
        {
            double acc = 1.0001; long localOps = 0;
            while (!token.IsCancellationRequested)
            {
                for (int i = 0; i < 100_000; i++) { acc = Math.Sqrt(acc + i) * Math.Sin(i) + Math.Cos(acc); if (double.IsNaN(acc) || double.IsInfinity(acc)) acc = 1.0001; }
                localOps += 100_000;
                if (localOps >= 1_000_000) { Interlocked.Add(ref _cpuOperations, localOps); localOps = 0; }
            }
            Interlocked.Add(ref _cpuOperations, localOps);
        }

        private void RamStressWorker(CancellationToken token, long blockSizeMb)
        {
            long blockSize = blockSizeMb * 1024 * 1024;
            byte[] block;
            try { block = new byte[blockSize]; } catch { return; }
            const int stride = 64; long localBytes = 0; byte counter = 0;
            while (!token.IsCancellationRequested)
            {
                for (long i = 0; i < blockSize; i += stride) { block[i] = counter; if ((i & 0xFFFFFF) == 0 && token.IsCancellationRequested) break; }
                counter++;
                localBytes += blockSize;
                if (localBytes >= 256L * 1024 * 1024) { Interlocked.Add(ref _ramBytesProcessed, localBytes); localBytes = 0; }
            }
            Interlocked.Add(ref _ramBytesProcessed, localBytes);
        }
    }
}
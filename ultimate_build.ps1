# ultimate_build.ps1 - Génération et compilation de PC Stress Benchmark (Version Premium Corrigée)
$ErrorActionPreference = "Stop"
$Host.UI.RawUI.WindowTitle = "PC Stress Benchmark - Installation Ultime"

# 1. Nettoyage et création du dossier propre
$ProjectDir = "PCStressBenchmark_Ultime"
if (Test-Path $ProjectDir) { Remove-Item -Path $ProjectDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $ProjectDir | Out-Null
Set-Location $ProjectDir

Write-Host "== Création des fichiers du projet ==" -ForegroundColor Cyan

function Write-Utf8($Path, $Content) {
    [System.IO.File]::WriteAllText((Join-Path (Get-Location) $Path), $Content, [System.Text.Encoding]::UTF8)
    Write-Host "  [+] $Path" -ForegroundColor DarkGray
}

# ==============================================================================
# FICHIER PROJET
# ==============================================================================
Write-Utf8 "PCStressBenchmarkGUI.csproj" @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>PCStressBenchmarkGUI</AssemblyName>
    <RootNamespace>PCStressBenchmarkGUI</RootNamespace>
    <ServerGarbageCollection>true</ServerGarbageCollection>
    <ConcurrentGarbageCollection>false</ConcurrentGarbageCollection>
    <TieredPGO>true</TieredPGO>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="LibreHardwareMonitorLib" Version="0.9.6" />
    <PackageReference Include="System.Management" Version="10.0.9" />
  </ItemGroup>
</Project>
"@

# ==============================================================================
# MODELE ET STOCKAGE
# ==============================================================================
Write-Utf8 "BenchmarkRecord.cs" @"
using System;

namespace PCStressBenchmarkGUI;

public class BenchmarkRecord
{
    public DateTime Date { get; set; }
    public string Mode { get; set; } = "Calme";
    public int DurationSeconds { get; set; }
    public double Score { get; set; }
    public string Tier { get; set; } = "";
    public string CpuName { get; set; } = "";
    public double RamGb { get; set; }
    public double CpuOpsPerSecondPerCore { get; set; }
    public double RamMbPerSecond { get; set; }
    public double GpuFps { get; set; }

    // Propriétés calculées pour l'affichage dans le XAML
    public string DateLabel => Date.ToString("dd/MM/yyyy HH:mm");
    public string SummaryLabel => `$"{Mode} · {DurationSeconds}s · {CpuName}";
    public string ScoreLabel => `$"{Score:N0} pts";
}
"@

Write-Utf8 "HistoryStore.cs" @"
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PCStressBenchmarkGUI;

public static class HistoryStore
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCStressBenchmark");
    private static readonly string FilePath = Path.Combine(FolderPath, "history.json");

    public static List<BenchmarkRecord> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<BenchmarkRecord>();
            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<BenchmarkRecord>>(json) ?? new List<BenchmarkRecord>();
        }
        catch { return new List<BenchmarkRecord>(); }
    }

    public static void Add(BenchmarkRecord record)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            var records = Load();
            records.Add(record);
            if (records.Count > 200) records = records.Skip(records.Count - 200).ToList();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static void Clear()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
    }
}
"@

# ==============================================================================
# TEMPERATURE READER
# ==============================================================================
Write-Utf8 "TemperatureReader.cs" @"
using System.Management;
using LibreHardwareMonitor.Hardware;

namespace PCStressBenchmarkGUI;

public sealed class TemperatureReader : IDisposable
{
    private Computer? _computer;
    private bool _disposed;

    public void Initialize()
    {
        if (_computer != null) return;
        try
        {
            _computer = new Computer { IsCpuEnabled = true, IsMotherboardEnabled = true };
            _computer.Open();
        }
        catch { try { _computer?.Close(); } catch { } _computer = null; }
    }

    public double ReadCpuTemperatureCelsius()
    {
        double? fromLhm = TryReadLibreHardwareMonitor();
        if (fromLhm.HasValue) return fromLhm.Value;

        double? fromWmi = TryReadWmiAcpi();
        if (fromWmi.HasValue) return fromWmi.Value;

        return -1;
    }

    private double? TryReadLibreHardwareMonitor()
    {
        if (_computer == null) return null;
        try
        {
            double? package = null, cores = null, generic = null;
            foreach (var hardware in _computer.Hardware)
            {
                CollectTemperatures(hardware, ref package, ref cores, ref generic);
                foreach (var sub in hardware.SubHardware)
                    CollectTemperatures(sub, ref package, ref cores, ref generic);
            }
            return package ?? cores ?? generic;
        }
        catch { return null; }
    }

    private static void CollectTemperatures(IHardware hardware, ref double? package, ref double? cores, ref double? generic)
    {
        if (hardware.HardwareType is not (HardwareType.Cpu or HardwareType.Motherboard)) return;
        hardware.Update();
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue) continue;
            float value = sensor.Value.Value;
            if (value is <= 0 or > 150) continue;
            string name = sensor.Name;

            if (hardware.HardwareType == HardwareType.Cpu)
            {
                if (name.Contains("Package", StringComparison.OrdinalIgnoreCase)) package = Max(package, value);
                else if (name.Contains("Core", StringComparison.OrdinalIgnoreCase) || name.Contains("CCD", StringComparison.OrdinalIgnoreCase) || name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) || name.Contains("Tdie", StringComparison.OrdinalIgnoreCase) || name.Contains("Average", StringComparison.OrdinalIgnoreCase)) cores = Max(cores, value);
                else generic = Max(generic, value);
            }
            else if (name.Contains("CPU", StringComparison.OrdinalIgnoreCase)) { generic = Max(generic, value); }
        }
    }

    private static double? Max(double? current, float value) => current is null ? value : Math.Max(current.Value, value);

    private static double? TryReadWmiAcpi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            double? best = null;
            foreach (var obj in searcher.Get())
            {
                if (double.TryParse(obj["CurrentTemperature"]?.ToString(), out double raw))
                {
                    double celsius = (raw / 10.0) - 273.15;
                    if (celsius is > -50 and < 150) best = best is null ? celsius : Math.Max(best.Value, celsius);
                }
            }
            return best;
        }
        catch { return null; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _computer?.Close(); } catch { }
        _computer = null;
    }
}
"@

# ==============================================================================
# APP XAML
# ==============================================================================
Write-Utf8 "App.xaml" @"
<Application x:Class="PCStressBenchmarkGUI.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
    </Application.Resources>
</Application>
"@

Write-Utf8 "App.xaml.cs" @"
using System.Windows;
namespace PCStressBenchmarkGUI;
public partial class App : Application { }
"@

# ==============================================================================
# HISTORY WINDOW XAML
# ==============================================================================
Write-Utf8 "HistoryWindow.xaml" @"
<Window x:Class="PCStressBenchmarkGUI.HistoryWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Historique des tests" Height="600" Width="600"
        WindowStartupLocation="CenterOwner" Background="#0B0E14" FontFamily="Segoe UI">
    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="📊 Historique des performances" FontSize="22" FontWeight="Bold" Foreground="White" Margin="0,0,0,20"/>

        <Border Grid.Row="1" Background="#151A24" CornerRadius="12" Padding="8" BorderBrush="#2A3241" BorderThickness="1">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <ItemsControl x:Name="HistoryList">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Background="#1E2430" CornerRadius="8" Padding="12" Margin="4">
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="Auto"/>
                                    </Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="{Binding DateLabel}" Foreground="#8A8A94" FontSize="11"/>
                                        <TextBlock Text="{Binding SummaryLabel}" Foreground="#D1D5DB" FontSize="13" Margin="0,4,0,0" TextWrapping="Wrap"/>
                                    </StackPanel>
                                    <StackPanel Grid.Column="1" HorizontalAlignment="Right">
                                        <TextBlock Text="{Binding ScoreLabel}" Foreground="#00E5FF" FontSize="18" FontWeight="Bold" HorizontalAlignment="Right"/>
                                        <TextBlock Text="{Binding Tier}" Foreground="#8A8A94" FontSize="11" HorizontalAlignment="Right"/>
                                    </StackPanel>
                                </Grid>
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </ScrollViewer>
        </Border>

        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,16,0,0">
            <Button x:Name="BtnClear" Content="🗑 Effacer l'historique" Click="BtnClear_Click" Background="#FF3366" Foreground="White" FontWeight="Bold" Padding="15,10" BorderThickness="0" Margin="0,0,10,0">
                <Button.Template><ControlTemplate TargetType="Button"><Border Background="{TemplateBinding Background}" CornerRadius="6"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border></ControlTemplate></Button.Template>
            </Button>
            <Button x:Name="BtnClose" Content="Fermer" Click="BtnClose_Click" Background="#2D2D30" Foreground="White" FontWeight="Bold" Padding="25,10" BorderThickness="0">
                <Button.Template><ControlTemplate TargetType="Button"><Border Background="{TemplateBinding Background}" CornerRadius="6"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border></ControlTemplate></Button.Template>
            </Button>
        </StackPanel>
    </Grid>
</Window>
"@

Write-Utf8 "HistoryWindow.xaml.cs" @"
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
"@

# ==============================================================================
# MAIN WINDOW XAML (LE DESIGN MAGNIFIQUE)
# ==============================================================================
Write-Utf8 "MainWindow.xaml" @"
<Window x:Class="PCStressBenchmarkGUI.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="⚡ PC Stress Benchmark" Height="780" Width="950"
        WindowStartupLocation="CenterScreen" Background="#0B0E14" FontFamily="Segoe UI">
    <Window.Resources>
        <!-- Bouton Principal -->
        <Style x:Key="PrimaryButton" TargetType="Button">
            <Setter Property="Background" Value="#00E5FF"/>
            <Setter Property="Foreground" Value="#0B0E14"/>
            <Setter Property="FontSize" Value="16"/>
            <Setter Property="FontWeight" Value="Bold"/>
            <Setter Property="Padding" Value="20,12"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}" CornerRadius="8">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#33EBFF"/></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Background" Value="#2D2D30"/><Setter Property="Foreground" Value="#8A8A94"/></Trigger>
            </Style.Triggers>
        </Style>

        <!-- Bouton Danger (Arrêter) -->
        <Style x:Key="DangerButton" TargetType="Button" BasedOn="{StaticResource PrimaryButton}">
            <Setter Property="Background" Value="#FF3366"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}" CornerRadius="8">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#FF6699"/></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Background" Value="#2D2D30"/></Trigger>
            </Style.Triggers>
        </Style>

        <!-- Style des Cartes -->
        <Style x:Key="Card" TargetType="Border">
            <Setter Property="Background" Value="#151A24"/>
            <Setter Property="CornerRadius" Value="12"/>
            <Setter Property="Padding" Value="16"/>
            <Setter Property="BorderBrush" Value="#2A3241"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Margin" Value="0,0,0,16"/>
        </Style>

        <Style x:Key="StatLabel" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#8A8A94"/>
            <Setter Property="FontSize" Value="11"/>
            <Setter Property="FontWeight" Value="Bold"/>
        </Style>
        <Style x:Key="StatValue" TargetType="TextBlock">
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="FontSize" Value="18"/>
            <Setter Property="FontWeight" Value="Bold"/>
        </Style>
    </Window.Resources>

    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <Grid Margin="24">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="320"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- COLONNE GAUCHE : CONFIGURATION -->
            <StackPanel Grid.Column="0" Margin="0,0,20,0">
                <TextBlock Text="⚡ PC STRESS BENCHMARK" FontSize="22" FontWeight="Black" Foreground="White" Margin="0,0,0,20">
                    <TextBlock.Effect><DropShadowEffect Color="#00E5FF" BlurRadius="15" ShadowDepth="0"/></TextBlock.Effect>
                </TextBlock>

                <Border Style="{StaticResource Card}">
                    <StackPanel>
                        <TextBlock Text="MATÉRIEL DÉTECTÉ" Style="{StaticResource StatLabel}" Margin="0,0,0,10"/>
                        <TextBlock x:Name="TxtCpuName" Text="Processeur : Analyse..." Foreground="White" FontSize="13" Margin="0,0,0,4" TextWrapping="Wrap"/>
                        <TextBlock x:Name="TxtRamTotal" Text="Mémoire : Analyse..." Foreground="White" FontSize="13" Margin="0,0,0,4"/>
                        <TextBlock x:Name="TxtGpuName" Text="Graphique : Analyse..." Foreground="White" FontSize="13" TextWrapping="Wrap"/>
                    </StackPanel>
                </Border>

                <Border Style="{StaticResource Card}">
                    <StackPanel>
                        <TextBlock Text="MODE D'INTENSITÉ" Style="{StaticResource StatLabel}" Margin="0,0,0,10"/>
                        <RadioButton x:Name="RadioModeCalme" Content="🟢 Calme" GroupName="Mode" IsChecked="True" Checked="RadioMode_Checked" Foreground="White" Margin="0,0,0,8"/>
                        <RadioButton x:Name="RadioModeIntense" Content="🟠 Intense" GroupName="Mode" Checked="RadioMode_Checked" Foreground="#E0A04C" Margin="0,0,0,8"/>
                        <RadioButton x:Name="RadioModeNasa" Content="🔥 NASA (Extrême)" GroupName="Mode" Checked="RadioMode_Checked" Foreground="#FF4C4C" FontWeight="Bold"/>
                        <TextBlock x:Name="TxtModeWarning" Text="" Foreground="#FF4C4C" FontSize="11" Margin="0,10,0,0" TextWrapping="Wrap" Visibility="Collapsed"/>
                    </StackPanel>
                </Border>

                <Border Style="{StaticResource Card}">
                    <StackPanel>
                        <TextBlock Text="DURÉE DU TEST" Style="{StaticResource StatLabel}" Margin="0,0,0,10"/>
                        <StackPanel Orientation="Horizontal">
                            <RadioButton x:Name="RadioDuration15" Content="15s" GroupName="Duration" Tag="15" Foreground="#C8C8D4" Margin="0,0,10,0"/>
                            <RadioButton x:Name="RadioDuration30" Content="30s" GroupName="Duration" Tag="30" Foreground="#C8C8D4" Margin="0,0,10,0"/>
                            <RadioButton x:Name="RadioDuration60" Content="60s" GroupName="Duration" Tag="60" IsChecked="True" Foreground="#C8C8D4" Margin="0,0,10,0"/>
                            <RadioButton x:Name="RadioDuration120" Content="120s" GroupName="Duration" Tag="120" Foreground="#C8C8D4"/>
                        </StackPanel>
                    </StackPanel>
                </Border>

                <Button x:Name="BtnStart" Content="▶ LANCER LE TEST" Style="{StaticResource PrimaryButton}" Click="BtnStart_Click" Margin="0,0,0,10"/>
                <Button x:Name="BtnStop" Content="⏹ ARRÊTER" Style="{StaticResource DangerButton}" Click="BtnStop_Click" IsEnabled="False" Margin="0,0,0,10"/>
                
                <Grid>
                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                    <Button x:Name="BtnHistory" Grid.Column="0" Content="📊 HISTORIQUE" Click="BtnHistory_Click" Background="#2D2D30" Foreground="White" Padding="10" BorderThickness="0" FontWeight="Bold" Margin="0,0,5,0">
                        <Button.Template><ControlTemplate TargetType="Button"><Border Background="{TemplateBinding Background}" CornerRadius="8"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border></ControlTemplate></Button.Template>
                    </Button>
                    <Button x:Name="BtnCopy" Grid.Column="1" Content="📋 COPIER" Click="BtnCopy_Click" Background="#2D2D30" Foreground="White" Padding="10" BorderThickness="0" FontWeight="Bold" Margin="5,0,0,0">
                        <Button.Template><ControlTemplate TargetType="Button"><Border Background="{TemplateBinding Background}" CornerRadius="8"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border></ControlTemplate></Button.Template>
                    </Button>
                </Grid>
            </StackPanel>

            <!-- COLONNE DROITE : VITESSE & STATS -->
            <Grid Grid.Column="1">
                <Grid.RowDefinitions>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <!-- Viewport 3D (Cube) -->
                <Border Grid.Row="0" Background="#05070A" CornerRadius="12" BorderBrush="#2A3241" BorderThickness="1" ClipToBounds="True" Margin="0,0,0,16">
                    <Viewport3D x:Name="GpuViewport">
                        <Viewport3D.Camera>
                            <PerspectiveCamera Position="0,0,6" LookDirection="0,0,-1" UpDirection="0,1,0" FieldOfView="60"/>
                        </Viewport3D.Camera>
                        <ModelVisual3D>
                            <ModelVisual3D.Content>
                                <Model3DGroup>
                                    <DirectionalLight Color="#00E5FF" Direction="-1,-1,-1"/>
                                    <DirectionalLight Color="#FF3366" Direction="1,1,1"/>
                                    <AmbientLight Color="#333333"/>
                                </Model3DGroup>
                            </ModelVisual3D.Content>
                        </ModelVisual3D>
                    </Viewport3D>
                </Border>

                <!-- Barre de progression -->
                <Border Grid.Row="1" Style="{StaticResource Card}" Margin="0">
                    <StackPanel>
                        <Grid>
                            <TextBlock x:Name="TxtStatus" Text="Prêt à démarrer" Foreground="White" FontWeight="Bold"/>
                            <TextBlock x:Name="TxtPercent" Text="0%" Foreground="#00E5FF" FontWeight="Bold" HorizontalAlignment="Right"/>
                        </Grid>
                        <Border Background="#2A3241" CornerRadius="6" Height="8" Margin="0,8,0,16">
                            <Border x:Name="ProgressFill" Background="#00E5FF" CornerRadius="6" HorizontalAlignment="Left" Width="0"/>
                        </Border>
                        
                        <Grid>
                            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="*"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                            <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>

                            <StackPanel Grid.Row="0" Grid.Column="0" Margin="0,0,0,12">
                                <TextBlock Text="CŒURS" Style="{StaticResource StatLabel}"/>
                                <TextBlock x:Name="TxtCores" Text="—" Style="{StaticResource StatValue}"/>
                            </StackPanel>
                            <StackPanel Grid.Row="0" Grid.Column="1" Margin="0,0,0,12">
                                <TextBlock Text="TEMPS" Style="{StaticResource StatLabel}"/>
                                <TextBlock x:Name="TxtElapsed" Text="0.0 s" Style="{StaticResource StatValue}"/>
                            </StackPanel>
                            <StackPanel Grid.Row="0" Grid.Column="2" Margin="0,0,0,12">
                                <TextBlock Text="TEMPÉRATURE" Style="{StaticResource StatLabel}"/>
                                <TextBlock x:Name="TxtTemp" Text="N/A" Style="{StaticResource StatValue}" Foreground="#FF3366"/>
                            </StackPanel>

                            <StackPanel Grid.Row="1" Grid.Column="0">
                                <TextBlock Text="CPU" Style="{StaticResource StatLabel}"/>
                                <TextBlock x:Name="TxtCpuRate" Text="0 ops/s" Style="{StaticResource StatValue}"/>
                            </StackPanel>
                            <StackPanel Grid.Row="1" Grid.Column="1">
                                <TextBlock Text="RAM" Style="{StaticResource StatLabel}"/>
                                <TextBlock x:Name="TxtRamRate" Text="0 Mo/s" Style="{StaticResource StatValue}"/>
                            </StackPanel>
                            <StackPanel Grid.Row="1" Grid.Column="2">
                                <TextBlock Text="GPU" Style="{StaticResource StatLabel}"/>
                                <TextBlock x:Name="TxtGpuRate" Text="0 fps" Style="{StaticResource StatValue}"/>
                            </StackPanel>
                        </Grid>
                    </StackPanel>
                </Border>
            </Grid>
        </Grid>
    </ScrollViewer>
</Window>
"@

# ==============================================================================
# MAIN WINDOW CS (TOUS LES USING SONT LÀ)
# ==============================================================================
Write-Utf8 "MainWindow.xaml.cs" @"
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

            TxtCpuName.Text = `$"Processeur : {result.cpuName}";
            TxtRamTotal.Text = `$"Mémoire : {result.ramGb:F1} Go";
            TxtGpuName.Text = `$"Graphique : {result.gpuNames}";
            _detectedRamGb = result.ramGb;
            _detectedCpuName = result.cpuName;

            await Task.Run(() => { _temperatureReader.Initialize(); _currentTempC = _temperatureReader.ReadCpuTemperatureCelsius(); });
            TxtTemp.Text = FormatTemp(_currentTempC);
        }

        private static string FormatTemp(double c) => c > 0 ? `$"{c:F0} °C"` : "N/A";

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

            for (int i = 3; i > 0; i--) { TxtStatus.Text = `$"Démarrage dans {i}..."; await Task.Delay(1000); }
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

            TxtElapsed.Text = `$"{elapsed:F1} s";
            TxtCpuRate.Text = `$"{(elapsed > 0 ? _cpuOperations / elapsed : 0):N0} ops/s";
            TxtRamRate.Text = `$"{(elapsed > 0 ? _ramBytesProcessed / 1024.0 / 1024.0 / elapsed : 0):F0} Mo/s";
            TxtGpuRate.Text = `$"{(elapsed > 0 ? _gpuFrameCount / elapsed : 0):F0} fps";
            TxtTemp.Text = FormatTemp(_currentTempC);
            TxtPercent.Text = `$"{percent:F0}%";

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

            _lastResultText = `$"PC Stress Benchmark - Score: {totalScore:N0} ({GetTier(totalScore)}) | Mode: {activeMode}";
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
"@

# ==============================================================================
# COMPILATION
# ==============================================================================
Write-Host "`n== Compilation en cours (Patientez 1 à 2 minutes) ==" -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -eq 0) {
    $publishPath = Resolve-Path "bin\Release\net8.0-windows\win-x64\publish"
    Write-Host "`n✅ COMPILATION RÉUSSIE !" -ForegroundColor Green
    Write-Host "Ton fichier .exe est ici : $publishPath\PCStressBenchmarkGUI.exe" -ForegroundColor White
    Start-Sleep -Seconds 2
    Invoke-Item $publishPath
} else {
    Write-Host "`n❌ Erreur lors de la compilation." -ForegroundColor Red
}
Write-Host "`nAppuie sur Entrée pour fermer..."
Read-Host
using System.Management;
using LibreHardwareMonitor.Hardware;

namespace PCStressBenchmarkGUI;

/// <summary>
/// Lit la température CPU via LibreHardwareMonitor (prioritaire) puis WMI en secours.
/// </summary>
public sealed class TemperatureReader : IDisposable
{
    private Computer? _computer;
    private bool _disposed;

    public void Initialize()
    {
        if (_computer != null) return;

        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsMotherboardEnabled = true,
            };
            _computer.Open();
        }
        catch
        {
            try { _computer?.Close(); } catch { /* ignorer */ }
            _computer = null;
        }
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
            double? package = null;
            double? cores = null;
            double? generic = null;

            foreach (var hardware in _computer.Hardware)
            {
                CollectTemperatures(hardware, ref package, ref cores, ref generic);
                foreach (var sub in hardware.SubHardware)
                    CollectTemperatures(sub, ref package, ref cores, ref generic);
            }

            return package ?? cores ?? generic;
        }
        catch
        {
            return null;
        }
    }

    private static void CollectTemperatures(IHardware hardware, ref double? package, ref double? cores, ref double? generic)
    {
        if (hardware.HardwareType is not (HardwareType.Cpu or HardwareType.Motherboard))
            return;

        hardware.Update();

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue)
                continue;

            float value = sensor.Value.Value;
            if (value is <= 0 or > 150)
                continue;

            string name = sensor.Name;

            if (hardware.HardwareType == HardwareType.Cpu)
            {
                if (name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                    package = Max(package, value);
                else if (name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                         || name.Contains("CCD", StringComparison.OrdinalIgnoreCase)
                         || name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                         || name.Contains("Tdie", StringComparison.OrdinalIgnoreCase)
                         || name.Contains("Average", StringComparison.OrdinalIgnoreCase))
                    cores = Max(cores, value);
                else
                    generic = Max(generic, value);
            }
            else if (name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
            {
                generic = Max(generic, value);
            }
        }
    }

    private static double? Max(double? current, float value) =>
        current is null ? value : Math.Max(current.Value, value);

    private static double? TryReadWmiAcpi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI",
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

            double? best = null;
            foreach (var obj in searcher.Get())
            {
                if (double.TryParse(obj["CurrentTemperature"]?.ToString(), out double raw))
                {
                    double celsius = (raw / 10.0) - 273.15;
                    if (celsius is > -50 and < 150)
                        best = best is null ? celsius : Math.Max(best.Value, celsius);
                }
            }

            return best;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _computer?.Close(); } catch { /* ignorer */ }
        _computer = null;
    }
}

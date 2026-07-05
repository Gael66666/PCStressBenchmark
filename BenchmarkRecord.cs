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
}

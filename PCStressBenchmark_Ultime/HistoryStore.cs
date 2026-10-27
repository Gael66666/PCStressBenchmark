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
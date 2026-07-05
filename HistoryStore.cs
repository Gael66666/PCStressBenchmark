using System.IO;
using System.Text.Json;

namespace PCStressBenchmarkGUI;

public static class HistoryStore
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PCStressBenchmark");

    private static readonly string FilePath = Path.Combine(FolderPath, "history.json");

    public static List<BenchmarkRecord> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<BenchmarkRecord>();
            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<BenchmarkRecord>>(json) ?? new List<BenchmarkRecord>();
        }
        catch
        {
            // Fichier corrompu ou illisible : on repart d'un historique vide
            // plutôt que de faire planter l'application.
            return new List<BenchmarkRecord>();
        }
    }

    public static void Add(BenchmarkRecord record)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            var records = Load();
            records.Add(record);

            // On garde les 200 derniers résultats pour ne pas laisser grossir le fichier indéfiniment
            if (records.Count > 200)
            {
                records = records.Skip(records.Count - 200).ToList();
            }

            string json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Sauvegarde impossible (permissions, disque plein...) : on n'interrompt
            // pas l'utilisateur pour autant, le résultat reste visible à l'écran.
        }
    }
}

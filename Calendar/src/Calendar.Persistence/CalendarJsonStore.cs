using System.Text.Json;

namespace Calendar.Persistence;

public sealed class CalendarJsonStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly Action<string, string> _replaceFile;

    public CalendarJsonStore(string path, Action<string, string>? replaceFile = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A persistence file path is required.", nameof(path));
        }

        _path = Path.GetFullPath(path);
        _replaceFile = replaceFile ?? ((temporaryPath, destinationPath) =>
            File.Move(temporaryPath, destinationPath, overwrite: true));
    }

    public void Save(CalendarStoreDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureSupportedSchema(document);

        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $"{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, SerializerOptions));
            _replaceFile(temporaryPath, _path);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public CalendarStoreDocument Load()
    {
        if (!File.Exists(_path))
        {
            return new CalendarStoreDocument(CalendarStoreDocument.CurrentSchemaVersion, [], []);
        }

        var json = File.ReadAllText(_path);
        CalendarStoreDocument document;
        try
        {
            document = JsonSerializer.Deserialize<CalendarStoreDocument>(json, SerializerOptions)
                ?? throw new JsonException("The calendar store document is empty.");
        }
        catch (JsonException)
        {
            BackupCorruptStore();
            return new CalendarStoreDocument(CalendarStoreDocument.CurrentSchemaVersion, [], []);
        }

        EnsureSupportedSchema(document);
        return document;
    }

    private static void EnsureSupportedSchema(CalendarStoreDocument document)
    {
        if (document.SchemaVersion != CalendarStoreDocument.CurrentSchemaVersion)
        {
            throw new NotSupportedException(
                $"Calendar store schema version {document.SchemaVersion} is unsupported; expected {CalendarStoreDocument.CurrentSchemaVersion}.");
        }
    }

    private void BackupCorruptStore()
    {
        var backupPath = $"{_path}.corrupt";
        if (File.Exists(backupPath))
        {
            backupPath = $"{backupPath}.{Guid.NewGuid():N}";
        }

        File.Move(_path, backupPath);
    }
}

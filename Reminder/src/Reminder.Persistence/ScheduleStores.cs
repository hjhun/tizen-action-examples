using System.Text.Json;
using Reminder.Domain;

namespace Reminder.Persistence;

public interface IScheduleStore
{
    ScheduleDocument Load();
    void Save(ScheduleDocument document);
}

public sealed class ScheduleStoreCorruptException : Exception
{
    public ScheduleStoreCorruptException(string message, Exception? inner = null) : base(message, inner) { }
}

public sealed class MemoryScheduleStore : IScheduleStore
{
    private ScheduleDocument _document = ScheduleDocument.Empty;
    public bool FailSaves { get; set; }
    public ScheduleDocument Load() => _document;
    public void Save(ScheduleDocument document)
    {
        if (FailSaves) throw new IOException("Injected persistence failure.");
        _document = document;
    }
}

public sealed class JsonScheduleStore : IScheduleStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _path;

    public JsonScheduleStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A store path is required.", nameof(path));
        _path = Path.GetFullPath(path);
    }

    public ScheduleDocument Load()
    {
        if (!File.Exists(_path)) return ScheduleDocument.Empty;
        try
        {
            var document = JsonSerializer.Deserialize<ScheduleDocument>(File.ReadAllText(_path), Options)
                ?? throw new JsonException("The store is empty.");
            Validate(document);
            return document;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException)
        {
            var backup = _path + ".corrupt";
            if (File.Exists(backup)) backup += "." + Guid.NewGuid().ToString("N");
            File.Move(_path, backup);
            throw new ScheduleStoreCorruptException($"The Reminder store was moved to {backup}.", exception);
        }
    }

    public void Save(ScheduleDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Validate(document);
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, Path.GetFileName(_path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, document, Options);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, _path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    private static void Validate(ScheduleDocument document)
    {
        if (document.SchemaVersion != ScheduleDocument.CurrentSchemaVersion)
            throw new NotSupportedException($"Unsupported Reminder store schema {document.SchemaVersion}.");
        if (document.Reminders is null || document.Reservations is null)
            throw new ArgumentException("Store collections are required.");
        if (document.Reminders.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != document.Reminders.Count ||
            document.Reservations.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != document.Reservations.Count)
            throw new ArgumentException("Duplicate IDs are not allowed.");
    }
}

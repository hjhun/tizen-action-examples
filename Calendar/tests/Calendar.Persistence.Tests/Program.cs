using Calendar.Domain;
using Calendar.Persistence;

var root = Path.Combine(Path.GetTempPath(), $"calendar-persistence-tests-{Guid.NewGuid():N}");
Directory.CreateDirectory(root);

try
{
    var path = Path.Combine(root, "calendar.json");
    var calendarEvent = CalendarEvent.Create(
        "event-1",
        "Design review",
        new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero),
        "Review Candidate B",
        "Studio");
    var reminder = CalendarReminder.CreateForEvent(
        "reminder-1",
        "Design review soon",
        calendarEvent.Start,
        calendarEvent.Id,
        30,
        "Prepare notes") with { AlarmId = 42 };

    var store = new CalendarJsonStore(path);
    store.Save(new CalendarStoreDocument(CalendarStoreDocument.CurrentSchemaVersion, [calendarEvent], [reminder]));
    var loaded = store.Load();

    Assert(loaded.SchemaVersion == CalendarStoreDocument.CurrentSchemaVersion, "Round trip must retain the schema version.");
    Assert(loaded.Events.SequenceEqual([calendarEvent]), "Round trip must retain events.");
    Assert(loaded.Reminders.SequenceEqual([reminder]), "Round trip must retain reminders including links and alarm metadata.");

    var missing = new CalendarJsonStore(Path.Combine(root, "missing.json")).Load();
    Assert(missing.SchemaVersion == CalendarStoreDocument.CurrentSchemaVersion, "A missing store must start at the current schema version.");
    Assert(missing.Events.Count == 0 && missing.Reminders.Count == 0, "A missing store must load as an empty document.");

    foreach (var unsupportedVersion in new[] { 0, CalendarStoreDocument.CurrentSchemaVersion + 1 })
    {
        var unsupportedPath = Path.Combine(root, $"schema-{unsupportedVersion}.json");
        File.WriteAllText(unsupportedPath, $$"""
            { "schemaVersion": {{unsupportedVersion}}, "events": [], "reminders": [] }
            """);
        AssertThrows<NotSupportedException>(
            () => new CalendarJsonStore(unsupportedPath).Load(),
            $"Schema version {unsupportedVersion} must be rejected at the migration boundary.");
    }

    var unsupportedSavePath = Path.Combine(root, "unsupported-save.json");
    AssertThrows<NotSupportedException>(
        () => new CalendarJsonStore(unsupportedSavePath).Save(new CalendarStoreDocument(0, [], [])),
        "Saving an unsupported schema version must be rejected at the migration boundary.");
    Assert(!File.Exists(unsupportedSavePath), "A rejected schema must not be published.");

    var atomicPath = Path.Combine(root, "atomic.json");
    var oldDocument = new CalendarStoreDocument(CalendarStoreDocument.CurrentSchemaVersion, [calendarEvent], []);
    var newDocument = new CalendarStoreDocument(CalendarStoreDocument.CurrentSchemaVersion, [], [reminder]);
    new CalendarJsonStore(atomicPath).Save(oldDocument);
    var replaceObservedCompleteTemp = false;
    var atomicStore = new CalendarJsonStore(atomicPath, (temporaryPath, destinationPath) =>
    {
        replaceObservedCompleteTemp = File.Exists(temporaryPath) &&
            File.ReadAllText(temporaryPath).Contains("reminder-1", StringComparison.Ordinal) &&
            File.Exists(destinationPath) &&
            File.ReadAllText(destinationPath).Contains("event-1", StringComparison.Ordinal);
        File.Move(temporaryPath, destinationPath, overwrite: true);
    });
    atomicStore.Save(newDocument);
    Assert(replaceObservedCompleteTemp, "Save must fully write a sibling temporary file before replacing the old store.");
    Assert(atomicStore.Load().Reminders.SequenceEqual([reminder]), "Atomic replace must publish the complete new document.");
    Assert(Directory.GetFiles(root, "*.tmp").Length == 0, "Successful atomic replace must not leave temporary files.");

    var failurePath = Path.Combine(root, "replace-failure.json");
    var initialFailureStore = new CalendarJsonStore(failurePath);
    initialFailureStore.Save(oldDocument);
    var failingStore = new CalendarJsonStore(failurePath, (_, _) => throw new IOException("simulated replace failure"));
    AssertThrows<IOException>(
        () => failingStore.Save(newDocument),
        "A replace failure must be reported to the caller.");
    Assert(initialFailureStore.Load().Events.SequenceEqual([calendarEvent]), "A failed replace must preserve the old store data.");
    Assert(Directory.GetFiles(root, "*.tmp").Length == 0, "A failed replace must clean up its temporary file.");

    var corruptPath = Path.Combine(root, "corrupt.json");
    const string corruptJson = "{ definitely-not-json";
    File.WriteAllText(corruptPath, corruptJson);
    var recovered = new CalendarJsonStore(corruptPath).Load();
    Assert(recovered.SchemaVersion == CalendarStoreDocument.CurrentSchemaVersion, "Corrupt-store recovery must use the current schema.");
    Assert(recovered.Events.Count == 0 && recovered.Reminders.Count == 0, "Corrupt-store recovery must return an empty document.");
    Assert(!File.Exists(corruptPath), "Corrupt-store recovery must remove the corrupt payload from the active path.");
    var backups = Directory.GetFiles(root, "corrupt.json.corrupt*");
    Assert(backups.Length == 1, "Corrupt-store recovery must create exactly one backup.");
    Assert(File.ReadAllText(backups[0]) == corruptJson, "The corrupt backup must preserve the original bytes.");

    Console.WriteLine("PASS: Calendar persistence tests");
}
finally
{
    Directory.Delete(root, recursive: true);
}

static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

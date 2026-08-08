using Calendar.Persistence;
using Calendar.UseCases;

namespace Calendar.App;

internal sealed class CalendarJsonPersistenceAdapter : ICalendarPersistence
{
    private readonly CalendarJsonStore _store;

    public CalendarJsonPersistenceAdapter(CalendarJsonStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public CalendarStoreDocument Load() => _store.Load();

    public void Save(CalendarStoreDocument document) => _store.Save(document);
}

using DisplayPresentation.Domain;
using DisplayPresentation.Persistence;

var store = new NoPresentationStore();
store.Save(new PresentationInput("{}", "{}"));
if (store.Load() is not null)
{
    throw new InvalidOperationException("Provider-produced presentations must not be persisted.");
}
Console.WriteLine("DisplayPresentation.Persistence.Tests: PASS");

using DisplayPresentation.Domain;

namespace DisplayPresentation.Persistence;

/// <summary>Explicitly prevents provider-produced Presentation payloads from being persisted.</summary>
public sealed class NoPresentationStore
{
    public PresentationInput? Load() => null;

    public void Save(PresentationInput presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        // Presentations are intentionally in-memory only.
    }
}

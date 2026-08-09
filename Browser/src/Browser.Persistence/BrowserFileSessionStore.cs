using System.Text;

namespace Browser.Persistence;

public interface IBrowserSessionStore
{
    Task<string?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(string serializedSnapshot, CancellationToken cancellationToken);
}

public sealed class BrowserFileSessionStore : IBrowserSessionStore
{
    public const int MaximumSerializedBytes = 256 * 1024;
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
    private readonly string _path;

    public BrowserFileSessionStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public async Task<string?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        var file = new FileInfo(_path);
        if (file.Length > MaximumSerializedBytes)
        {
            throw new InvalidDataException("Browser session file exceeds 256KiB.");
        }

        var serialized = await File.ReadAllTextAsync(_path, Utf8WithoutBom, cancellationToken).ConfigureAwait(false);
        if (Utf8WithoutBom.GetByteCount(serialized) > MaximumSerializedBytes)
        {
            throw new InvalidDataException("Browser session file exceeds 256KiB.");
        }

        return serialized;
    }

    public async Task SaveAsync(string serializedSnapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serializedSnapshot);
        if (Utf8WithoutBom.GetByteCount(serializedSnapshot) > MaximumSerializedBytes)
        {
            throw new InvalidDataException("Browser session file exceeds 256KiB.");
        }

        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("Browser session path must include a directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, serializedSnapshot, Utf8WithoutBom, cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

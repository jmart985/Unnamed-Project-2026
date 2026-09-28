namespace Simulation;

public sealed class SaveWriteException : IOException
{
    public SaveWriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public interface ISaveFileSystem
{
    bool Exists(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string contents);

    void Replace(string sourcePath, string destinationPath);

    void DeleteIfExists(string path);
}

public sealed class SystemSaveFileSystem : ISaveFileSystem
{
    public bool Exists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, contents);
    }

    public void Replace(string sourcePath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            File.Move(sourcePath, destinationPath, overwrite: true);
            return;
        }

        File.Move(sourcePath, destinationPath);
    }

    public void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

public sealed class SaveSlotStore
{
    private readonly ISaveFileSystem _files;

    public SaveSlotStore(ISaveFileSystem? files = null)
    {
        _files = files ?? new SystemSaveFileSystem();
    }

    public string Load(string path)
    {
        ValidatePath(path);
        return _files.ReadAllText(path);
    }

    public void Save(string path, Func<string> serialize)
    {
        ValidatePath(path);
        ArgumentNullException.ThrowIfNull(serialize);

        var temporaryPath = path + ".tmp";
        try
        {
            var contents = serialize();
            if (string.IsNullOrWhiteSpace(contents))
            {
                throw new InvalidDataException("Save serialization produced no data.");
            }

            _files.WriteAllText(temporaryPath, contents);
            _files.Replace(temporaryPath, path);
        }
        catch (Exception exception) when (exception is not SaveWriteException)
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw new SaveWriteException(
                $"Could not write save slot '{path}'. The previous save was preserved.",
                exception);
        }
    }

    private void TryDeleteTemporaryFile(string path)
    {
        try
        {
            _files.DeleteIfExists(path);
        }
        catch
        {
            // Cleanup failure must not hide the original save failure.
        }
    }

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A save-slot path is required.", nameof(path));
        }
    }
}

using Simulation;

namespace Simulation.Tests;

public sealed class SaveSlotStoreTests
{
    [Fact]
    public void SuccessfulWriteReplacesTheSaveSlot()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"unnamed-project-save-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "save.json");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "old save");
            var store = new SaveSlotStore();

            store.Save(path, () => "new save");

            Assert.Equal("new save", store.Load(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SerializationFailurePreservesThePreviousSave()
    {
        var files = new FakeSaveFileSystem("save.json", "previous save");
        var store = new SaveSlotStore(files);

        var exception = Assert.Throws<SaveWriteException>(() =>
            store.Save("save.json", () => throw new InvalidDataException("serialization failed")));

        Assert.IsType<InvalidDataException>(exception.InnerException);
        Assert.Contains("previous save was preserved", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("previous save", store.Load("save.json"));
        Assert.False(files.Exists("save.json.tmp"));
    }

    [Fact]
    public void ReplaceFailurePreservesThePreviousSaveAndCleansTheTemporaryFile()
    {
        var files = new FakeSaveFileSystem("save.json", "previous save")
        {
            FailReplace = true
        };
        var store = new SaveSlotStore(files);

        var exception = Assert.Throws<SaveWriteException>(() =>
            store.Save("save.json", () => "replacement save"));

        Assert.IsType<IOException>(exception.InnerException);
        Assert.Equal("previous save", store.Load("save.json"));
        Assert.False(files.Exists("save.json.tmp"));
    }

    private sealed class FakeSaveFileSystem : ISaveFileSystem
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

        public FakeSaveFileSystem(string path, string contents)
        {
            _files.Add(path, contents);
        }

        public bool FailReplace { get; init; }

        public bool Exists(string path) => _files.ContainsKey(path);

        public string ReadAllText(string path) => _files[path];

        public void WriteAllText(string path, string contents) => _files[path] = contents;

        public void Replace(string sourcePath, string destinationPath)
        {
            if (FailReplace)
            {
                throw new IOException("replace failed");
            }

            _files[destinationPath] = _files[sourcePath];
            _files.Remove(sourcePath);
        }

        public void DeleteIfExists(string path) => _files.Remove(path);
    }
}

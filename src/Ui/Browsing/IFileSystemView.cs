namespace PixieEditor.Ui.Browsing;

internal enum EntryKind { Folder, File }

// One row of a listing. Size is null for folders; Link marks symbolic links and junctions,
// which are shown but never followed implicitly.
internal sealed record BrowserEntry(string Name, string FullPath, EntryKind Kind, long? Size, DateTime? Modified, bool Hidden, bool Link);

// A shortcut on the side: home, Desktop, a drive or a mount.
internal sealed record BrowserPlace(string Label, string Path);

// Everything the browser needs from a file system. The model talks only to this, so it can be
// driven by a fake in tests and by another implementation (a project archive, a remote) later.
internal interface IFileSystemView
{
    // Normalizes a typed path to an absolute one; throws ArgumentException for unusable input.
    string FullPath(string path);
    bool FolderExists(string path);
    bool FileExists(string path);
    // The direct children of a folder. Throws IOException or UnauthorizedAccessException.
    IReadOnlyList<BrowserEntry> List(string folder);
    // A child path of a folder, joined with this file system's separator.
    string Combine(string folder, string name);
    // The parent folder, or null at a root.
    string? Parent(string path);
    IReadOnlyList<BrowserPlace> Places();
    // Creates exactly this folder. Throws IOException when anything already exists at the path.
    void CreateFolder(string path);
    // Whether names differ only by case on this file system.
    bool CaseInsensitive { get; }
}

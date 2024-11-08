using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents the differences between two tree entries in a Git repository.
/// </summary>
/// <param name="changes">The tree entry changes object.</param>
/// <param name="repository">The Git repository object.</param>
public class DifferenceEntity(TreeEntryChanges changes, Repository repository)
{
    /// <summary>
    /// Gets the path of the changed file.
    /// </summary>
    public string Path => changes.Path;
    
    /// <summary>
    /// The file exists in the new side of the diff.
    /// </summary>
    public bool Exists => changes.Exists;

    /// <summary>
    /// Gets the kind of change (e.g., Added, Deleted, Modified).
    /// </summary>
    public string ChangeKind => changes.Status.ToString();

    /// <summary>
    /// Gets the old path of the file before the change.
    /// </summary>
    public string OldPath => changes.OldPath;

    /// <summary>
    /// Gets the old file mode before the change.
    /// </summary>
    public string OldMode => changes.OldMode.ToString();

    /// <summary>
    /// Gets the new file mode after the change.
    /// </summary>
    public string NewMode => changes.Mode.ToString();

    /// <summary>
    /// Gets the SHA of the old file before the change.
    /// </summary>
    public string OldSha => changes.OldOid.Sha;

    /// <summary>
    /// Gets the SHA of the new file after the change.
    /// </summary>
    public string NewSha => changes.Oid.Sha;

    /// <summary>
    /// Gets the content of the old file as a string.
    /// </summary>
    public string? OldContent
    {
        get
        {
            if (changes.OldOid == null)
                return null;

            var blob = repository.Lookup<Blob>(changes.OldOid);
            return blob.GetContentText();
        }
    }
    
    /// <summary>
    /// Gets the content of the old file as a byte array.
    /// </summary>
    public byte[]? OldContentBytes
    {
        get
        {
            if (changes.OldOid == null)
                return null;

            var blob = repository.Lookup<Blob>(changes.OldOid);
            using var contentStream = blob.GetContentStream();

            var buffer = new byte[contentStream.Length];

            while (contentStream.Position < contentStream.Length)
            {
                var read = contentStream.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    break;
            }

            return buffer;
        }
    }

    /// <summary>
    /// Gets the content of the new file as a string.
    /// </summary>
    public string? NewContent
    {
        get
        {
            if (changes.Status == LibGit2Sharp.ChangeKind.Deleted)
                return null;
            
            var blob = repository.Lookup<Blob>(changes.Oid);

            if (blob == null)
                return null;
            
            var contentText = blob.GetContentText();

            return contentText;
        }
    }

    /// <summary>
    /// Gets the content of the new file as a byte array.
    /// </summary>
    public byte[]? NewContentBytes
    {
        get
        {
            if (changes.Status == LibGit2Sharp.ChangeKind.Deleted)
                return null;
            
            var blob = repository.Lookup<Blob>(changes.Oid);
            
            if (blob == null)
                return null;
            
            using var contentStream = blob.GetContentStream();

            var buffer = new byte[contentStream.Length];

            while (contentStream.Position < contentStream.Length)
            {
                var read = contentStream.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    break;
            }

            return buffer;
        }
    }
}
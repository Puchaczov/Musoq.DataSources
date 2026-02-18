using Musoq.Plugins;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.GitHub;

/// <summary>
///     GitHub helper methods for use in queries.
/// </summary>
public class GitHubLibrary : LibraryBase
{
    /// <summary>
    ///     Parses owner and repository name from a full repository name (owner/repo format).
    /// </summary>
    /// <param name="fullName">The full repository name in owner/repo format.</param>
    /// <returns>A tuple containing the owner and repository name.</returns>
    [BindableMethod]
    public (string Owner, string Repo) ParseRepoFullName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName))
            return (string.Empty, string.Empty);

        var parts = fullName.Split('/');
        if (parts.Length != 2)
            return (string.Empty, string.Empty);

        return (parts[0], parts[1]);
    }

    /// <summary>
    ///     Checks if a label list contains a specific label.
    /// </summary>
    /// <param name="labels">Comma-separated list of labels.</param>
    /// <param name="label">Label to search for.</param>
    /// <returns>True if the label is found, false otherwise.</returns>
    [BindableMethod]
    public bool HasLabel(string? labels, string label)
    {
        if (string.IsNullOrEmpty(labels))
            return false;

        var labelList = labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return labelList.Contains(label, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Counts the number of labels.
    /// </summary>
    /// <param name="labels">Comma-separated list of labels.</param>
    /// <returns>The number of labels.</returns>
    [BindableMethod]
    public int LabelCount(string? labels)
    {
        if (string.IsNullOrEmpty(labels))
            return 0;

        return labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    /// <summary>
    ///     Calculates the time between two dates in days.
    /// </summary>
    /// <param name="start">Start date.</param>
    /// <param name="end">End date (optional, defaults to now).</param>
    /// <returns>Number of days between the dates.</returns>
    [BindableMethod]
    public double DaysBetween(DateTimeOffset start, DateTimeOffset? end = null)
    {
        var endDate = end ?? DateTimeOffset.UtcNow;
        return (endDate - start).TotalDays;
    }

    /// <summary>
    ///     Calculates the age of an item in days since creation.
    /// </summary>
    /// <param name="createdAt">Creation date.</param>
    /// <returns>Number of days since creation.</returns>
    [BindableMethod]
    public double AgeDays(DateTimeOffset createdAt)
    {
        return (DateTimeOffset.UtcNow - createdAt).TotalDays;
    }

    /// <summary>
    ///     Checks if an issue/PR is stale (not updated in specified days).
    /// </summary>
    /// <param name="updatedAt">Last update date.</param>
    /// <param name="staleDays">Number of days after which to consider stale.</param>
    /// <returns>True if stale, false otherwise.</returns>
    [BindableMethod]
    public bool IsStale(DateTimeOffset? updatedAt, int staleDays = 30)
    {
        if (!updatedAt.HasValue)
            return false;

        return (DateTimeOffset.UtcNow - updatedAt.Value).TotalDays > staleDays;
    }

    /// <summary>
    ///     Gets a short version of a SHA hash.
    /// </summary>
    /// <param name="sha">Full SHA hash.</param>
    /// <param name="length">Desired length (default 7).</param>
    /// <returns>Shortened SHA.</returns>
    [BindableMethod]
    public string ShortSha(string? sha, int length = 7)
    {
        if (string.IsNullOrEmpty(sha))
            return string.Empty;

        return sha.Length > length ? sha[..length] : sha;
    }
}
namespace FramePress.Core;

public sealed record BatchUpdate(string Kind, string Path, MediaInfo? Media = null,
    JobProgress? Progress = null, JobResult? Result = null, string Detail = "");

/// <summary>Lazy discovery → probe → encode. No complete metadata scan is required first.</summary>
public sealed class BatchProcessor(MediaEngine engine)
{
    public async Task RunAsync(string root, bool recursive, bool includeImages,
        IReadOnlyDictionary<string, CompressionProfile> profiles, JobOptions options,
        Action<BatchUpdate> notify, CancellationToken token)
    {
        foreach (var profile in profiles.Values) profile.Validate();
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var path in MediaEngine.Discover(root, recursive, message => notify(new("Warning", "", Detail: message))))
        {
            token.ThrowIfCancellationRequested();
            if (!seen.Add(Path.GetFullPath(path))) continue;
            if (!includeImages && MediaEngine.ImageExtensions.Contains(Path.GetExtension(path))) continue;
            notify(new("Inspecting", path));
            try
            {
                var media = await engine.ProbeAsync(path, token).ConfigureAwait(false);
                notify(new("Found", path, Media: media));
                if (!media.IsImage && (!profiles.TryGetValue(media.Tier, out var profile) || !profile.Enabled))
                { notify(new("Skipped", path, Detail: "Resolution profile disabled.")); continue; }
                var selected = media.IsImage ? new CompressionProfile() : profiles[media.Tier];
                var updates = new CallbackProgress(p => notify(new("Progress", path, Progress: p)));
                var result = await engine.ProcessAsync(media, selected, options, updates, token).ConfigureAwait(false);
                notify(new("Result", path, Result: result));
            }
            catch (OperationCanceledException) { notify(new("Cancelled", path)); throw; }
            catch (Exception e) { notify(new("Error", path, Detail: e.Message)); }
        }
    }
    private sealed class CallbackProgress(Action<JobProgress> callback) : IProgress<JobProgress>
    { public void Report(JobProgress value) => callback(value); }
}

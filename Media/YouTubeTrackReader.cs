// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Text.Json;
using rexmit.Services.Interfaces;

namespace rexmit.Media;

public sealed class YouTubeTrackReader : IYouTubeTrackReader
{
    public async Task<PlaybackTrack> ReadAsync(string url, string requestedBy)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "yt-dlp",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
        {
            "--no-warnings", "--no-playlist", "--skip-download", "--dump-single-json", "--js-runtimes", "deno", "--", url
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start yt-dlp to read track details.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Timed out while reading YouTube track details.");
        }

        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"yt-dlp could not read track details (exit {process.ExitCode}): {SanitizeError(error)}"
            );
        }

        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;
        var title = root.TryGetProperty("title", out var titleValue) ? titleValue.GetString() : null;
        var artist = root.TryGetProperty("artist", out var artistValue) ? artistValue.GetString() : null;
        artist ??= root.TryGetProperty("uploader", out var uploaderValue) ? uploaderValue.GetString() : null;
        var thumbnail = root.TryGetProperty("thumbnail", out var thumbnailValue) ? thumbnailValue.GetString() : null;
        TimeSpan? duration = root.TryGetProperty("duration", out var durationValue) && durationValue.TryGetDouble(out var seconds)
            ? TimeSpan.FromSeconds(Math.Max(0, seconds))
            : null;
        var isLive = root.TryGetProperty("is_live", out var liveValue) && liveValue.ValueKind == JsonValueKind.True;

        return new PlaybackTrack(
            Guid.NewGuid(),
            url,
            string.IsNullOrWhiteSpace(title) ? url : title,
            artist,
            thumbnail,
            duration,
            isLive,
            requestedBy
        );
    }

    private static string SanitizeError(string error) => string.Join(
        Environment.NewLine,
        error.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !line.Contains("http://", StringComparison.OrdinalIgnoreCase) &&
                           !line.Contains("https://", StringComparison.OrdinalIgnoreCase))
    ).Trim();
}

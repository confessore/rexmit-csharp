// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.Audio;
using rexmit.Services.Interfaces;

namespace rexmit.Services;

public partial class FFmpegService : IFFmpegService
{
    public Process CreateStream(string path)
    {
        return Process.Start(
            new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments =
                    $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            }
        );
    }

    public async IAsyncEnumerable<string> DownloadVideoAsync(string videoUrl)
    {
        var youtubeDlPath = "yt-dlp"; // Path to youtube-dl executable
        var processStartInfo = new ProcessStartInfo
        {
            FileName = youtubeDlPath,
            Arguments = $"--dump-json {videoUrl}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process
        {
            StartInfo = processStartInfo,
            EnableRaisingEvents = true,
        };

        process.Start();

        using var reader = process.StandardOutput;
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }

    public async IAsyncEnumerable<string> DownloadVideosAsync(string playlistUrl)
    {
        var youtubeDlPath = "yt-dlp"; // Path to youtube-dl executable
        var processStartInfo = new ProcessStartInfo
        {
            FileName = youtubeDlPath,
            Arguments = $"--dump-json --flat-playlist {playlistUrl}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process
        {
            StartInfo = processStartInfo,
            EnableRaisingEvents = true,
        };

        process.Start();

        using var reader = process.StandardOutput;
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }

    private string ExtractVideoUrl(string json)
    {
        // Extract video URL from JSON
        var regex = UrlRegex();
        var match = regex.Match(json);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return "";
    }

    [GeneratedRegex(@"""url"": ""([^""]+)""")]
    private static partial Regex UrlRegex();
}

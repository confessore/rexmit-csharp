// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using rexmit.Services.Interfaces;

namespace rexmit.Services;

public partial class FFmpegService : IFFmpegService
{
    public async Task<MemoryStream> CreateStreamAsync(string videoStreamUrl)
    {
        using var ffmpegProcess = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments =
                    $"-hide_banner -loglevel panic -i \"{videoStreamUrl}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            }
        };

        ffmpegProcess.Start();
        using var memoryStream = new MemoryStream();
        await ffmpegProcess.StandardOutput.BaseStream.CopyToAsync(memoryStream);
        return memoryStream;
    }

    public async IAsyncEnumerable<string> DownloadVideoAsync(string videoUrl)
    {
        var youtubeDlPath = "yt-dlp"; // Path to youtube-dl executable
        var processStartInfo = new ProcessStartInfo
        {
            FileName = youtubeDlPath,
            Arguments = $"-g {videoUrl}",
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

    public static async IAsyncEnumerable<string> DownloadVideosAsync(string playlistUrl)
    {
        var youtubeDlPath = "yt-dlp"; // Path to youtube-dl executable
        var processStartInfo = new ProcessStartInfo
        {
            FileName = youtubeDlPath,
            Arguments = $"-g --flat-playlist {playlistUrl}",
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

    [GeneratedRegex(@"""url"": ""([^""]+)""")]
    private static partial Regex UrlRegex();
}

// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Discord.Audio;
using rexmit.Services.Interfaces;

namespace rexmit.Services;

public partial class FFmpegService : IFFmpegService
{
    public Process? CreateStream(string videoUrl)
    {
        var info = new ProcessStartInfo()
        {
            FileName = "yt-dlp",
            Arguments =
                $"{videoUrl} -o - | \"ffmpeg -hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000\"",
            UseShellExecute = false,
            RedirectStandardOutput = true
        };
        return Process.Start(info);
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

    public async Task SendAsync(IAudioClient client, string path)
    {
        // Create FFmpeg using the previous example
        using (var ffmpeg = CreateStream(path))
        using (var output = ffmpeg.StandardOutput.BaseStream)
        using (var discord = client.CreatePCMStream(AudioApplication.Mixed))
        {
            try
            {
                await output.CopyToAsync(discord);
                Console.WriteLine("copied to output");
            }
            finally
            {
                await discord.FlushAsync();
                Console.WriteLine("flushed");
            }
        }
    }
}

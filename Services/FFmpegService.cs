// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord.Audio;
using rexmit.Services.Interfaces;

namespace rexmit.Services;

public sealed class FFmpegService : IFFmpegService
{
    private static Process CreateStream(string videoStreamUrl)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
        {
            "-hide_banner", "-loglevel", "error", "-i", videoStreamUrl,
            "-vn", "-ac", "2", "-ar", "48000", "-f", "s16le", "pipe:1"
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start ffmpeg.");
    }

    public async Task SendAsync(IAudioClient client, string youtubeUrl, CancellationToken cancellationToken)
    {
        var streamUrl = await ResolveStreamUrlAsync(youtubeUrl, cancellationToken);
        using var ffmpeg = CreateStream(streamUrl);
        var stderrTask = ffmpeg.StandardError.ReadToEndAsync();
        using var discord = client.CreatePCMStream(AudioApplication.Mixed);
        try
        {
            await ffmpeg.StandardOutput.BaseStream.CopyToAsync(discord, cancellationToken);
            await ffmpeg.WaitForExitAsync(cancellationToken);
            var error = await stderrTask;
            if (ffmpeg.ExitCode != 0)
            {
                throw new InvalidOperationException($"ffmpeg exited with code {ffmpeg.ExitCode}: {error.Trim()}");
            }
        }
        catch (OperationCanceledException)
        {
            Kill(ffmpeg);
            throw;
        }
        finally
        {
            await discord.FlushAsync();
        }
    }

    private static async Task<string> ResolveStreamUrlAsync(string youtubeUrl, CancellationToken cancellationToken)
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
            "--no-warnings", "--no-playlist", "--js-runtimes", "deno", "--format", "bestaudio/best", "--get-url", "--", youtubeUrl
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start yt-dlp.");
        }

        var stderrTask = process.StandardError.ReadToEndAsync();
        try
        {
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var error = await stderrTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"yt-dlp exited with code {process.ExitCode}: {error.Trim()}");
            }

            var streamUrl = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(streamUrl))
            {
                throw new InvalidOperationException("yt-dlp did not return an audio stream URL.");
            }

            return streamUrl;
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Process exited between the check and Kill.
        }
    }
}

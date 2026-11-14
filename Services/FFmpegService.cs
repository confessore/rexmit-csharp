// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Diagnostics;
using Discord.Audio;
using rexmit.Services.Interfaces;

namespace rexmit.Services;

public sealed class FFmpegService : IFFmpegService
{
    private static Process CreateTranscoder()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
        {
            "-hide_banner", "-loglevel", "error", "-i", "pipe:0",
            "-vn", "-ac", "2", "-ar", "48000", "-f", "s16le", "pipe:1"
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new Process { StartInfo = startInfo };
    }

    private static Process CreateDownloader(string youtubeUrl)
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
            "--no-warnings", "--no-progress", "--no-playlist", "--js-runtimes", "deno",
            "--format", "bestaudio/best", "--output", "-", "--", youtubeUrl
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new Process { StartInfo = startInfo };
    }

    public async Task SendAsync(
        IAudioClient client,
        string youtubeUrl,
        Func<CancellationToken, Task> waitUntilResumed,
        Action<TimeSpan> reportPosition,
        CancellationToken cancellationToken
    )
    {
        using var downloader = CreateDownloader(youtubeUrl);
        using var ffmpeg = CreateTranscoder();
        try
        {
            if (!ffmpeg.Start() || !downloader.Start())
            {
                throw new InvalidOperationException("Could not start the audio pipeline.");
            }

            var downloaderErrorTask = downloader.StandardError.ReadToEndAsync();
            var ffmpegErrorTask = ffmpeg.StandardError.ReadToEndAsync();
            using var discord = client.CreatePCMStream(AudioApplication.Mixed);
            try
            {
                var downloadToFfmpegTask = downloader.StandardOutput.BaseStream.CopyToAsync(
                    ffmpeg.StandardInput.BaseStream,
                    cancellationToken
                );
                var ffmpegToDiscordTask = CopyAudioAsync(
                    ffmpeg.StandardOutput.BaseStream,
                    discord,
                    waitUntilResumed,
                    reportPosition,
                    cancellationToken
                );
                var firstCompletedTask = await Task.WhenAny(downloadToFfmpegTask, ffmpegToDiscordTask);
                await firstCompletedTask;

                if (ReferenceEquals(firstCompletedTask, downloadToFfmpegTask))
                {
                    ffmpeg.StandardInput.Close();
                    await ffmpegToDiscordTask;
                }
                else
                {
                    await ffmpeg.WaitForExitAsync(cancellationToken);
                    await downloadToFfmpegTask;
                }

                await downloader.WaitForExitAsync(cancellationToken);
                await ffmpeg.WaitForExitAsync(cancellationToken);
                var downloaderError = await downloaderErrorTask;
                var ffmpegError = await ffmpegErrorTask;
                if (downloader.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"yt-dlp exited with code {downloader.ExitCode}: {SanitizeErrorOutput(downloaderError)}"
                    );
                }

                if (ffmpeg.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"ffmpeg exited with code {ffmpeg.ExitCode}: {SanitizeErrorOutput(ffmpegError)}"
                    );
                }
            }
            finally
            {
                await discord.FlushAsync();
            }
        }
        catch
        {
            Kill(downloader);
            Kill(ffmpeg);
            throw;
        }
    }

    private static async Task CopyAudioAsync(
        Stream source,
        Stream destination,
        Func<CancellationToken, Task> waitUntilResumed,
        Action<TimeSpan> reportPosition,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[3840];
        long bytesWritten = 0;
        while (true)
        {
            await waitUntilResumed(cancellationToken);
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                return;
            }

            await waitUntilResumed(cancellationToken);
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            bytesWritten += bytesRead;
            reportPosition(TimeSpan.FromSeconds(bytesWritten / 192000d));
        }
    }

    private static string SanitizeErrorOutput(string error) => string.Join(
        Environment.NewLine,
        error.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !line.Contains("http://", StringComparison.OrdinalIgnoreCase) &&
                           !line.Contains("https://", StringComparison.OrdinalIgnoreCase))
    ).Trim();

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

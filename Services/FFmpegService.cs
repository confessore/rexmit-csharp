// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Threading.Tasks;
using Discord.Audio;
using rexmit.Services.Interfaces;

namespace rexmit.Services;

public class FFmpegService : IFFmpegService
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
}

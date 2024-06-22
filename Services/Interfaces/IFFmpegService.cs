// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Discord.Audio;

namespace rexmit.Services.Interfaces;

public interface IFFmpegService
{
    Process? CreateStream(string videoStreamUrl);
    Task SendAsync(IAudioClient client, string path);
    Task SendAsync(IAudioClient client, string path, CancellationToken cancellationToken);
    IAsyncEnumerable<string> DownloadVideoAsync(string videoUrl);
}

// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Discord.Audio;

namespace rexmit.Services.Interfaces;

public interface IFFmpegService
{
    Task SendAsync(
        IAudioClient client,
        string youtubeUrl,
        Func<CancellationToken, Task> waitUntilResumed,
        Action<TimeSpan> reportPosition,
        CancellationToken cancellationToken
    );
}

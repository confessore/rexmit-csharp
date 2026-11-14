// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Discord.Audio;

namespace rexmit.Services.Interfaces;

public interface IFFmpegService
{
    Task SendAsync(IAudioClient client, string youtubeUrl, CancellationToken cancellationToken);
}

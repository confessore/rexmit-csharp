// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using Discord;

namespace rexmit.Services.Interfaces;

public interface IAudioService
{
    Task EnqueueAsync(ulong guildId, IVoiceChannel voiceChannel, IMessageChannel textChannel, string youtubeUrl);
    bool Skip(ulong guildId);
    bool Remove(ulong guildId, int position);
    int Clear(ulong guildId);
    Task<bool> LeaveAsync(ulong guildId);
}

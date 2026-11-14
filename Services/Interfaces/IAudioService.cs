// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Discord;

namespace rexmit.Services.Interfaces;

public interface IAudioService
{
    Task EnqueueAsync(ulong guildId, IVoiceChannel voiceChannel, IMessageChannel textChannel, string youtubeUrl, string requestedBy);
    bool Skip(ulong guildId);
    bool SetPaused(ulong guildId, bool paused);
    bool Shuffle(ulong guildId);
    bool Remove(ulong guildId, int position);
    bool Remove(ulong guildId, Guid trackId);
    bool Move(ulong guildId, Guid trackId, int offset);
    int Clear(ulong guildId);
    Task<bool> LeaveAsync(ulong guildId);
}

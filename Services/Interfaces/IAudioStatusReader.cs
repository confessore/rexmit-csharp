// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Collections.Generic;

namespace rexmit.Services.Interfaces;

public interface IAudioStatusReader
{
    GuildPlaybackStatus? GetStatus(ulong guildId);
    IReadOnlyList<GuildPlaybackStatus> GetAllStatuses();
}

public sealed record GuildPlaybackStatus(
    ulong GuildId,
    ulong VoiceChannelId,
    string? Current,
    IReadOnlyList<string> Upcoming
);

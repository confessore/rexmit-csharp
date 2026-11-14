// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

namespace rexmit.Services.Interfaces;

public interface IAudioStatusReader
{
    GuildPlaybackStatus? GetStatus(ulong guildId);
    IReadOnlyList<GuildPlaybackStatus> GetAllStatuses();
}

public sealed record GuildPlaybackStatus(
    ulong GuildId,
    ulong VoiceChannelId,
    PlaybackTrack? Current,
    IReadOnlyList<PlaybackTrack> Upcoming,
    TimeSpan Position,
    bool IsPaused
);

public sealed record PlaybackTrack(
    Guid Id,
    string Url,
    string Title,
    string? Artist,
    string? ThumbnailUrl,
    TimeSpan? Duration,
    bool IsLive,
    string RequestedBy
);

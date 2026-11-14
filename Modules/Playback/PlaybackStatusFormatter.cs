// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using rexmit.Services.Interfaces;

namespace rexmit.Modules.Playback;

internal static class PlaybackStatusFormatter
{
    public static string FormatQueue(GuildPlaybackStatus? snapshot)
    {
        var lines = new List<string>();
        if (snapshot?.Current is not null)
        {
            var current = snapshot.Current;
            var duration = current.IsLive ? "LIVE" : current.Duration is { } length ? $" / {FormatTime(length)}" : string.Empty;
            lines.Add($"**Now playing:** {Shorten(current.Title)} — {FormatTime(snapshot.Position)}{duration}");
        }

        var upcoming = snapshot?.Upcoming ?? Array.Empty<PlaybackTrack>();
        lines.AddRange(upcoming.Take(10).Select((track, index) => $"{index + 1}. {Shorten(track.Title)}"));
        if (upcoming.Count > 10)
        {
            lines.Add($"…and {upcoming.Count - 10} more.");
        }

        return lines.Count == 0 ? "The queue is empty." : string.Join('\n', lines);
    }

    public static string Shorten(string url) => url.Length <= 150 ? url : $"{url[..147]}…";

    private static string FormatTime(TimeSpan value) => value.TotalHours >= 1
        ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
        : $"{(int)value.TotalMinutes}:{value.Seconds:00}";
}

// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Discord;
using rexmit.Services.Interfaces;

namespace rexmit.Modules.Playback;

internal static class PlaybackPanelRenderer
{
    public static Embed CreateEmbed(GuildPlaybackStatus? status)
    {
        var embed = new EmbedBuilder().WithColor(new Color(88, 101, 242));
        if (status?.Current is not { } current)
        {
            embed.WithTitle("Music player").WithDescription("Nothing is playing.");
        }
        else
        {
            embed.WithTitle(Shorten(current.Title, 256)).WithUrl(current.Url);
            if (Uri.TryCreate(current.ThumbnailUrl, UriKind.Absolute, out var thumbnailUri) &&
                thumbnailUri.Scheme == Uri.UriSchemeHttps)
            {
                embed.WithThumbnailUrl(thumbnailUri.ToString());
            }

            var timing = current.IsLive
                ? $"LIVE • {FormatTime(status.Position)}"
                : current.Duration is { } duration
                    ? $"{FormatTime(status.Position)} / {FormatTime(duration)}\n{Progress(status.Position, duration)}"
                    : $"{FormatTime(status.Position)} elapsed • duration unavailable";
            var artist = string.IsNullOrWhiteSpace(current.Artist) ? string.Empty : $"\n**{Shorten(current.Artist, 256)}**";
            var requester = Shorten(current.RequestedBy, 64);
            embed.WithDescription($"{artist}\n{(status.IsPaused ? "⏸ Paused" : "▶ Playing")} • requested by **{requester}**\n{timing}");
        }

        var upcoming = status?.Upcoming ?? Array.Empty<PlaybackTrack>();
        if (upcoming.Count > 0)
        {
            var lines = upcoming.Take(10).Select((track, index) =>
                $"{index + 1}. {Shorten(track.Title, 80)} • {TrackDuration(track)}");
            var suffix = upcoming.Count > 10 ? $"\n…and {upcoming.Count - 10} more" : string.Empty;
            embed.AddField($"Up next · {upcoming.Count} track(s)", string.Join('\n', lines) + suffix);
        }
        else
        {
            embed.AddField("Up next", "The queue is empty.");
        }

        return embed.Build();
    }

    public static MessageComponent CreateComponents(GuildPlaybackStatus? status)
    {
        var hasCurrent = status?.Current is not null;
        var hasQueue = status is { Upcoming.Count: > 0 };
        var components = new ComponentBuilder()
            .WithButton(status?.IsPaused == true ? "Resume" : "Pause", "rexmit:pause", ButtonStyle.Primary, disabled: !hasCurrent)
            .WithButton("Skip", "rexmit:skip", ButtonStyle.Secondary, disabled: !hasCurrent && !hasQueue)
            .WithButton("Shuffle", "rexmit:shuffle", ButtonStyle.Secondary, disabled: !hasQueue)
            .WithButton("Add track", "rexmit:add", ButtonStyle.Success)
            .WithButton("Clear queue", "rexmit:clear", ButtonStyle.Danger, disabled: !hasQueue)
            .WithButton("Disconnect", "rexmit:leave", ButtonStyle.Danger, disabled: status is null, row: 1);

        if (hasQueue)
        {
            var options = status!.Upcoming.Select((track, index) => new SelectMenuOptionBuilder()
                .WithLabel($"{index + 1}. {Shorten(track.Title, 90)}")
                .WithValue(track.Id.ToString("N"))
                .WithDescription(TrackDuration(track)))
                .ToList();
            components.WithSelectMenu("rexmit:remove", options, "Select a queued track to manage", row: 2);
        }

        return components.Build();
    }

    private static string Progress(TimeSpan position, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return string.Empty;
        }

        var filled = Math.Clamp((int)Math.Round(position.TotalSeconds / duration.TotalSeconds * 20), 0, 20);
        return $"{new string('▬', filled)}🔘{new string('▬', 20 - filled)}";
    }

    private static string TrackDuration(PlaybackTrack track) => track.IsLive
        ? "LIVE"
        : track.Duration is { } duration ? FormatTime(duration) : "Unknown length";

    private static string FormatTime(TimeSpan value) => value.TotalHours >= 1
        ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
        : $"{(int)value.TotalMinutes}:{value.Seconds:00}";

    private static string Shorten(string value, int maxLength)
    {
        var normalized = value.Replace('\r', ' ').Replace('\n', ' ');
        return normalized.Length <= maxLength ? normalized : $"{normalized[..(maxLength - 1)]}…";
    }
}

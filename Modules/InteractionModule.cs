// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using rexmit.Services.Interfaces;
using RunMode = Discord.Interactions.RunMode;

namespace rexmit.Modules;

public class InteractionModule(IAudioService audioService, IAudioStatusReader audioStatusReader)
    : InteractionModuleBase<ShardedInteractionContext>
{
    private readonly IAudioService _audioService = audioService;
    private readonly IAudioStatusReader _audioStatusReader = audioStatusReader;

    [SlashCommand("help", "Show music commands.")]
    public async Task HelpAsync()
    {
        await DeferAsync(ephemeral: true);
        var embed = new EmbedBuilder()
            .AddField("/play <YouTube URL>", "Join your voice channel and add a video to the queue")
            .AddField("/queue", "Show the current track and waiting queue")
            .AddField("/nowplaying", "Show the current track")
            .AddField("/skip", "Skip the current track")
            .AddField("/remove <position>", "Remove a waiting track (positions start at 1)")
            .AddField("/clear", "Clear waiting tracks")
            .AddField("/leave", "Stop playback and leave voice")
            .Build();
        await FollowupAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("play", "Add a YouTube video to the voice queue.", runMode: RunMode.Async)]
    public async Task PlayAsync(string url)
    {
        await DeferAsync(ephemeral: true);
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
        if (Context.Guild is null || voiceChannel is null)
        {
            await FollowupAsync("Join a voice channel in a server first.", ephemeral: true);
            return;
        }

        try
        {
            await _audioService.EnqueueAsync(Context.Guild.Id, voiceChannel, Context.Channel, url);
            await FollowupAsync("Added to the queue.", ephemeral: true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            await FollowupAsync(ex.Message, ephemeral: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not queue track: {ex}");
            await FollowupAsync("Could not join voice or add that track. Check the bot's voice permissions and logs.", ephemeral: true);
        }
    }

    [SlashCommand("queue", "Show the current track and waiting queue.", runMode: RunMode.Async)]
    public async Task QueueAsync()
    {
        await DeferAsync(ephemeral: true);
        if (Context.Guild is null)
        {
            await FollowupAsync("This command only works in a server.", ephemeral: true);
            return;
        }

        await FollowupAsync(
            FormatQueue(_audioStatusReader.GetStatus(Context.Guild.Id)),
            ephemeral: true,
            allowedMentions: AllowedMentions.None
        );
    }

    [SlashCommand("nowplaying", "Show the current track.", runMode: RunMode.Async)]
    public async Task NowPlayingAsync()
    {
        await DeferAsync(ephemeral: true);
        var current = Context.Guild is null ? null : _audioStatusReader.GetStatus(Context.Guild.Id)?.Current;
        await FollowupAsync(
            current is null ? "Nothing is playing." : $"Now playing: {Shorten(current)}",
            ephemeral: true,
            allowedMentions: AllowedMentions.None
        );
    }

    [SlashCommand("skip", "Skip the current track.", runMode: RunMode.Async)]
    public async Task SkipAsync()
    {
        await DeferAsync(ephemeral: true);
        var skipped = Context.Guild is not null && _audioService.Skip(Context.Guild.Id);
        await FollowupAsync(skipped ? "Skipped." : "Nothing is playing or queued.", ephemeral: true);
    }

    [SlashCommand("remove", "Remove a waiting track by its queue position.", runMode: RunMode.Async)]
    public async Task RemoveAsync(int position)
    {
        await DeferAsync(ephemeral: true);
        var removed = Context.Guild is not null && _audioService.Remove(Context.Guild.Id, position);
        await FollowupAsync(removed ? "Removed from the queue." : "That queue position does not exist.", ephemeral: true);
    }

    [SlashCommand("clear", "Clear waiting tracks.", runMode: RunMode.Async)]
    public async Task ClearAsync()
    {
        await DeferAsync(ephemeral: true);
        var removed = Context.Guild is null ? 0 : _audioService.Clear(Context.Guild.Id);
        await FollowupAsync($"Removed {removed} waiting track(s).", ephemeral: true);
    }

    [SlashCommand("leave", "Stop playback and leave voice.", runMode: RunMode.Async)]
    public async Task LeaveAsync()
    {
        await DeferAsync(ephemeral: true);
        var left = Context.Guild is not null && await _audioService.LeaveAsync(Context.Guild.Id);
        await FollowupAsync(left ? "Stopped playback and left voice." : "I am not playing in this server.", ephemeral: true);
    }

    private static string FormatQueue(GuildPlaybackStatus? snapshot)
    {
        var lines = new List<string>();
        if (snapshot?.Current is not null)
        {
            lines.Add($"**Now playing:** {Shorten(snapshot.Current)}");
        }

        var upcoming = snapshot?.Upcoming ?? Array.Empty<string>();
        lines.AddRange(upcoming.Take(10).Select((track, index) => $"{index + 1}. {Shorten(track)}"));
        if (upcoming.Count > 10)
        {
            lines.Add($"…and {upcoming.Count - 10} more.");
        }

        return lines.Count == 0 ? "The queue is empty." : string.Join('\n', lines);
    }

    private static string Shorten(string url) => url.Length <= 150 ? url : $"{url[..147]}…";
}

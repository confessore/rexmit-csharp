// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Discord;
using Discord.Interactions;
using rexmit.Services.Interfaces;
using RunMode = Discord.Interactions.RunMode;

namespace rexmit.Modules.Playback;

public sealed class PlaybackStatusModule(
    IAudioStatusReader audioStatusReader,
    IPlaybackPanelService playbackPanel
)
    : InteractionModuleBase<ShardedInteractionContext>
{
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
            .AddField("/panel", "Post the interactive music player in this channel")
            .Build();
        await FollowupAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("panel", "Post or refresh the interactive music player in this channel.", runMode: RunMode.Async)]
    public async Task PanelAsync()
    {
        await DeferAsync(ephemeral: true);
        if (Context.Guild is null)
        {
            await FollowupAsync("This command only works in a server.", ephemeral: true);
            return;
        }

        await playbackPanel.PublishAsync(Context.Guild.Id, Context.Channel);
        await FollowupAsync("Music player panel posted or refreshed.", ephemeral: true);
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
            PlaybackStatusFormatter.FormatQueue(audioStatusReader.GetStatus(Context.Guild.Id)),
            ephemeral: true,
            allowedMentions: AllowedMentions.None
        );
    }

    [SlashCommand("nowplaying", "Show the current track.", runMode: RunMode.Async)]
    public async Task NowPlayingAsync()
    {
        await DeferAsync(ephemeral: true);
        var current = Context.Guild is null ? null : audioStatusReader.GetStatus(Context.Guild.Id)?.Current;
        await FollowupAsync(
            current is null ? "Nothing is playing." : $"Now playing: {PlaybackStatusFormatter.Shorten(current.Title)}",
            ephemeral: true,
            allowedMentions: AllowedMentions.None
        );
    }
}

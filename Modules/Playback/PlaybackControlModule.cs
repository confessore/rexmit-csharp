// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using rexmit.Services.Interfaces;
using RunMode = Discord.Interactions.RunMode;

namespace rexmit.Modules.Playback;

public sealed class PlaybackControlModule(
    IAudioService audioService,
    IAudioStatusReader statusReader,
    IPlaybackPanelService playbackPanel
)
    : InteractionModuleBase<ShardedInteractionContext>
{
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
            await audioService.EnqueueAsync(Context.Guild.Id, voiceChannel, Context.Channel, url, Context.User.Username);
            try
            {
                await playbackPanel.PublishAsync(Context.Guild.Id, Context.Channel);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Track was queued, but the player panel could not be updated: {ex}");
                await FollowupAsync("Added to the queue, but I could not post the player panel. Use /panel to retry.", ephemeral: true);
                return;
            }

            await FollowupAsync("Added to the queue.", ephemeral: true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            await FollowupAsync(ex.Message, ephemeral: true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not queue track: {ex}");
            await FollowupAsync("Could not join voice or add that track. Check the bot's voice permissions and logs.", ephemeral: true);
        }
    }

    [SlashCommand("skip", "Skip the current track.", runMode: RunMode.Async)]
    public async Task SkipAsync()
    {
        await DeferAsync(ephemeral: true);
        if (!TryGetVoiceGuild(out var voiceGuildId, out var error))
        {
            await FollowupAsync(error, ephemeral: true);
            return;
        }

        var skipped = audioService.Skip(voiceGuildId);
        await playbackPanel.RefreshAsync(voiceGuildId);
        await FollowupAsync(skipped ? "Skipped." : "Nothing is playing or queued.", ephemeral: true);
    }

    [SlashCommand("remove", "Remove a waiting track by its queue position.", runMode: RunMode.Async)]
    public async Task RemoveAsync(int position)
    {
        await DeferAsync(ephemeral: true);
        if (!TryGetVoiceGuild(out var guildId, out var error))
        {
            await FollowupAsync(error, ephemeral: true);
            return;
        }

        var removed = audioService.Remove(guildId, position);
        await playbackPanel.RefreshAsync(guildId);
        await FollowupAsync(removed ? "Removed from the queue." : "That queue position does not exist.", ephemeral: true);
    }

    [SlashCommand("clear", "Clear waiting tracks.", runMode: RunMode.Async)]
    public async Task ClearAsync()
    {
        await ConfirmActionAsync("clear", "Clear all waiting tracks?", ButtonStyle.Danger);
    }

    [SlashCommand("leave", "Stop playback and leave voice.", runMode: RunMode.Async)]
    public async Task LeaveAsync()
    {
        await ConfirmActionAsync("leave", "Stop playback and disconnect?", ButtonStyle.Danger);
    }

    [ComponentInteraction("rexmit:pause")]
    public async Task TogglePauseAsync()
    {
        if (!TryGetVoiceGuild(out var guildId, out var error))
        {
            await RespondAsync(error, ephemeral: true);
            return;
        }

        var status = statusReader.GetStatus(guildId);
        if (!audioService.SetPaused(guildId, status?.IsPaused != true))
        {
            await RespondAsync("There is no active track to pause.", ephemeral: true);
            return;
        }

        await RespondAsync(status?.IsPaused == true ? "Playback resumed." : "Playback paused.", ephemeral: true);
        await playbackPanel.RefreshAsync(guildId);
    }

    [ComponentInteraction("rexmit:add")]
    public Task OpenAddTrackModalAsync() => RespondWithModalAsync<AddTrackModal>("rexmit:add");

    [ModalInteraction("rexmit:add")]
    public async Task AddTrackFromModalAsync(AddTrackModal modal)
    {
        await DeferAsync(ephemeral: true);
        var guild = Context.Guild;
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
        if (guild is null || voiceChannel is null)
        {
            await FollowupAsync("Join a voice channel in this server first.", ephemeral: true);
            return;
        }

        try
        {
            await audioService.EnqueueAsync(guild.Id, voiceChannel, Context.Channel, modal.Url, Context.User.Username);
            try
            {
                await playbackPanel.PublishAsync(guild.Id, Context.Channel);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Track was queued, but the player panel could not be updated: {ex}");
                await FollowupAsync("Added to the queue, but I could not post the player panel. Use /panel to retry.", ephemeral: true);
                return;
            }

            await FollowupAsync("Added to the queue.", ephemeral: true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            await FollowupAsync(ex.Message, ephemeral: true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not queue track from the player panel: {ex}");
            await FollowupAsync("Could not add that track. Check the bot's voice permissions and logs.", ephemeral: true);
        }
    }

    [ComponentInteraction("rexmit:skip")]
    public async Task SkipFromPanelAsync()
    {
        if (!TryGetVoiceGuild(out var guildId, out var error))
        {
            await RespondAsync(error, ephemeral: true);
            return;
        }

        var skipped = audioService.Skip(guildId);
        await RespondAsync(skipped ? "Skipped." : "Nothing is playing or queued.", ephemeral: true);
        await playbackPanel.RefreshAsync(guildId);
    }

    [ComponentInteraction("rexmit:shuffle")]
    public async Task ShuffleAsync()
    {
        if (!TryGetVoiceGuild(out var guildId, out var error))
        {
            await RespondAsync(error, ephemeral: true);
            return;
        }

        var shuffled = audioService.Shuffle(guildId);
        await RespondAsync(shuffled ? "Shuffled the upcoming queue." : "Add at least two tracks to shuffle.", ephemeral: true);
        await playbackPanel.RefreshAsync(guildId);
    }

    [ComponentInteraction("rexmit:remove")]
    public async Task ManageFromPanelAsync(string[] selections)
    {
        if (!TryGetVoiceGuild(out var guildId, out var error))
        {
            await RespondAsync(error, ephemeral: true);
            return;
        }

        if (selections.Length != 1 || !Guid.TryParseExact(selections[0], "N", out var trackId))
        {
            await RespondAsync("Select one queued track to remove.", ephemeral: true);
            return;
        }

        var status = statusReader.GetStatus(guildId);
        var trackIndex = status?.Upcoming.ToList().FindIndex(track => track.Id == trackId) ?? -1;
        if (status is null || trackIndex < 0)
        {
            await RespondAsync("That track is no longer in the queue.", ephemeral: true);
            return;
        }

        await RespondAsync(
            $"Managing **{PlaybackStatusFormatter.Shorten(status.Upcoming[trackIndex].Title)}** · queue position {trackIndex + 1}",
            components: QueueTrackActions(trackId, trackIndex, status.Upcoming.Count),
            ephemeral: true,
            allowedMentions: AllowedMentions.None
        );
    }

    [ComponentInteraction("rexmit:queue-up:*")]
    public Task MoveTrackUpAsync(string trackId) => ManageTrackAsync(trackId, -1, remove: false);

    [ComponentInteraction("rexmit:queue-down:*")]
    public Task MoveTrackDownAsync(string trackId) => ManageTrackAsync(trackId, 1, remove: false);

    [ComponentInteraction("rexmit:queue-delete:*")]
    public Task RemoveManagedTrackAsync(string trackId) => ManageTrackAsync(trackId, 0, remove: true);

    [ComponentInteraction("rexmit:clear")]
    public Task ConfirmClearFromPanelAsync() => ConfirmActionAsync("clear", "Clear all waiting tracks?", ButtonStyle.Danger);

    [ComponentInteraction("rexmit:leave")]
    public Task ConfirmLeaveFromPanelAsync() => ConfirmActionAsync("leave", "Stop playback and disconnect?", ButtonStyle.Danger);

    [ComponentInteraction("rexmit:clear-confirm:*")]
    public async Task ClearConfirmedAsync(string userId)
    {
        if (userId != Context.User.Id.ToString())
        {
            await RespondAsync("Only the member who requested this confirmation can use it.", ephemeral: true);
            return;
        }

        if (!TryGetVoiceGuild(out var guildId, out var error))
        {
            await RespondAsync(error, ephemeral: true);
            return;
        }

        await DeferAsync();
        var removed = audioService.Clear(guildId);
        await ModifyOriginalResponseAsync(properties =>
        {
            properties.Content = $"Removed {removed} waiting track(s).";
            properties.Components = new ComponentBuilder().Build();
        });
        await playbackPanel.RefreshAsync(guildId);
    }

    [ComponentInteraction("rexmit:leave-confirm:*")]
    public async Task LeaveConfirmedAsync(string userId)
    {
        if (userId != Context.User.Id.ToString())
        {
            await RespondAsync("Only the member who requested this confirmation can use it.", ephemeral: true);
            return;
        }

        if (!TryGetVoiceGuild(out var guildId, out var error))
        {
            await RespondAsync(error, ephemeral: true);
            return;
        }

        await DeferAsync();
        var left = await audioService.LeaveAsync(guildId);
        await ModifyOriginalResponseAsync(properties =>
        {
            properties.Content = left ? "Stopped playback and disconnected." : "I am not playing in this server.";
            properties.Components = new ComponentBuilder().Build();
        });
        await playbackPanel.RefreshAsync(guildId);
    }

    private async Task ConfirmActionAsync(string action, string prompt, ButtonStyle style)
    {
        if (!TryGetVoiceGuild(out _, out var error))
        {
            await RespondAsync(error, ephemeral: true);
            return;
        }

        var customId = $"rexmit:{action}-confirm:{Context.User.Id}";
        var components = new ComponentBuilder()
            .WithButton("Confirm", customId, style)
            .WithButton("Cancel", "rexmit:cancel", ButtonStyle.Secondary)
            .Build();
        await RespondAsync(prompt, components: components, ephemeral: true);
    }

    private async Task ManageTrackAsync(string rawTrackId, int offset, bool remove)
    {
        if (!TryGetVoiceGuild(out var guildId, out var error))
        {
            await RespondAsync(error, ephemeral: true);
            return;
        }

        if (!Guid.TryParseExact(rawTrackId, "N", out var trackId))
        {
            await RespondAsync("That queue control is no longer valid.", ephemeral: true);
            return;
        }

        var changed = remove
            ? audioService.Remove(guildId, trackId)
            : audioService.Move(guildId, trackId, offset);
        var status = statusReader.GetStatus(guildId);
        var index = status?.Upcoming.ToList().FindIndex(track => track.Id == trackId) ?? -1;
        var content = index < 0
            ? changed ? "Removed from the queue." : "That track is no longer in the queue."
            : changed ? $"Updated queue position: {index + 1}." : "That track cannot move any further in that direction.";
        var components = index < 0 || status is null
            ? new ComponentBuilder().Build()
            : QueueTrackActions(trackId, index, status.Upcoming.Count);
        if (Context.Interaction is not SocketMessageComponent component)
        {
            throw new InvalidOperationException("Queue actions are only available from a message component.");
        }

        await component.UpdateAsync(properties =>
        {
            properties.Content = content;
            properties.Components = components;
        });
        if (changed)
        {
            await playbackPanel.RefreshAsync(guildId);
        }
    }

    private static MessageComponent QueueTrackActions(Guid trackId, int index, int queueCount) => new ComponentBuilder()
        .WithButton("Move up", $"rexmit:queue-up:{trackId:N}", ButtonStyle.Secondary, disabled: index == 0)
        .WithButton("Move down", $"rexmit:queue-down:{trackId:N}", ButtonStyle.Secondary, disabled: index == queueCount - 1)
        .WithButton("Remove", $"rexmit:queue-delete:{trackId:N}", ButtonStyle.Danger)
        .Build();

    [ComponentInteraction("rexmit:cancel")]
    public async Task CancelConfirmationAsync()
    {
        if (Context.Interaction is not SocketMessageComponent component)
        {
            throw new InvalidOperationException("Confirmation controls are only available from a message component.");
        }

        await component.UpdateAsync(properties =>
        {
            properties.Content = "Cancelled.";
            properties.Components = new ComponentBuilder().Build();
        });
    }

    private bool TryGetVoiceGuild(out ulong guildId, out string error)
    {
        guildId = Context.Guild?.Id ?? 0;
        var status = Context.Guild is null ? null : statusReader.GetStatus(guildId);
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
        if (Context.Guild is null || status is null || voiceChannel?.Id != status.VoiceChannelId)
        {
            error = "Join the bot's voice channel in this server to use these controls.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

public sealed class AddTrackModal : IModal
{
    public string Title => "Add a YouTube track";

    [InputLabel("YouTube URL")]
    [ModalTextInput("url", placeholder: "https://youtube.com/watch?v=…", maxLength: 2048)]
    public string Url { get; set; } = string.Empty;
}

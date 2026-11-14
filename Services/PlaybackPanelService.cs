// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Discord;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using rexmit.Modules.Playback;
using rexmit.Services.Interfaces;

namespace rexmit.Services;

public sealed class PlaybackPanelService(
    IAudioStatusReader statusReader,
    ILogger<PlaybackPanelService> logger
) : BackgroundService, IPlaybackPanelService
{
    private readonly ConcurrentDictionary<ulong, (IMessageChannel Channel, IUserMessage Message)> _panels = new();
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _publishGates = new();

    public async Task PublishAsync(ulong guildId, IMessageChannel channel)
    {
        var gate = _publishGates.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (_panels.TryGetValue(guildId, out var current) && current.Channel.Id == channel.Id)
            {
                try
                {
                    await UpdateAsync(guildId, current.Message);
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not update the music panel for guild {GuildId}; posting a new one", guildId);
                }
            }

            var status = statusReader.GetStatus(guildId);
            var message = await channel.SendMessageAsync(
                embed: PlaybackPanelRenderer.CreateEmbed(status),
                components: PlaybackPanelRenderer.CreateComponents(status),
                allowedMentions: AllowedMentions.None
            );
            if (_panels.TryGetValue(guildId, out current))
            {
                try
                {
                    await current.Message.DeleteAsync();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not remove the previous music panel for guild {GuildId}", guildId);
                }
            }

            _panels[guildId] = (channel, message);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RefreshAsync(ulong guildId)
    {
        if (!_panels.TryGetValue(guildId, out var panel))
        {
            return;
        }

        try
        {
            await UpdateAsync(guildId, panel.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not refresh the music panel for guild {GuildId}; attempting to replace it", guildId);
            try
            {
                await PublishAsync(guildId, panel.Channel);
            }
            catch (Exception publishException)
            {
                logger.LogError(publishException, "Could not replace the music panel for guild {GuildId}", guildId);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var (guildId, panel) in _panels)
                {
                    try
                    {
                        await RefreshAsync(guildId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not refresh the music panel for guild {GuildId}", guildId);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is expected.
        }
    }

    private Task UpdateAsync(ulong guildId, IUserMessage message)
    {
        var status = statusReader.GetStatus(guildId);
        return message.ModifyAsync(properties =>
        {
            properties.Embed = PlaybackPanelRenderer.CreateEmbed(status);
            properties.Components = PlaybackPanelRenderer.CreateComponents(status);
            properties.AllowedMentions = AllowedMentions.None;
        });
    }
}

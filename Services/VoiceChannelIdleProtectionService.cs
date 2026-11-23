// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using rexmit.Services.Interfaces;

namespace rexmit.Services;

internal sealed class VoiceChannelIdleProtectionService(
    DiscordShardedClient client,
    IAudioService audioService,
    IAudioStatusReader statusReader,
    ILogger<VoiceChannelIdleProtectionService> logger
) : BackgroundService
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<ulong, PendingCleanup> _pendingCleanups = new();
    private CancellationToken _stoppingToken;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        client.UserVoiceStateUpdated += OnUserVoiceStateUpdatedAsync;
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is expected.
        }
        finally
        {
            client.UserVoiceStateUpdated -= OnUserVoiceStateUpdatedAsync;
            foreach (var cleanup in _pendingCleanups.Values)
            {
                cleanup.Cancel();
            }
        }
    }

    private Task OnUserVoiceStateUpdatedAsync(
        SocketUser user,
        SocketVoiceState before,
        SocketVoiceState after
    )
    {
        if (user is not SocketGuildUser guildUser)
        {
            return Task.CompletedTask;
        }

        var guildId = guildUser.Guild.Id;
        var status = statusReader.GetStatus(guildId);
        if (status is null)
        {
            CancelCleanup(guildId);
            return Task.CompletedTask;
        }

        if (before.VoiceChannelId != status.VoiceChannelId && after.VoiceChannelId != status.VoiceChannelId)
        {
            return Task.CompletedTask;
        }

        var channel = guildUser.Guild.GetVoiceChannel(status.VoiceChannelId);
        if (channel is null || VoiceChannelIdleProtectionPolicy.ContainsHumanMember(channel.ConnectedUsers.Select(member => member.IsBot)))
        {
            CancelCleanup(guildId);
            return Task.CompletedTask;
        }

        ScheduleCleanup(guildId, status.VoiceChannelId);
        return Task.CompletedTask;
    }

    private void ScheduleCleanup(ulong guildId, ulong voiceChannelId)
    {
        var cleanup = new PendingCleanup(voiceChannelId, _stoppingToken);
        PendingCleanup? previous = null;
        _pendingCleanups.AddOrUpdate(guildId, cleanup, (_, current) =>
        {
            previous = current;
            return cleanup;
        });
        previous?.Cancel();

        _ = CleanupAfterDelayAsync(guildId, cleanup);
    }

    private void CancelCleanup(ulong guildId)
    {
        if (_pendingCleanups.TryRemove(guildId, out var cleanup))
        {
            cleanup.Cancel();
        }
    }

    private async Task CleanupAfterDelayAsync(ulong guildId, PendingCleanup cleanup)
    {
        try
        {
            await Task.Delay(IdleTimeout, cleanup.Token);
            var status = statusReader.GetStatus(guildId);
            if (status?.VoiceChannelId != cleanup.VoiceChannelId)
            {
                return;
            }

            var channel = client.GetGuild(guildId)?.GetVoiceChannel(cleanup.VoiceChannelId);
            if (channel is null || VoiceChannelIdleProtectionPolicy.ContainsHumanMember(channel.ConnectedUsers.Select(member => member.IsBot)))
            {
                return;
            }

            cleanup.Token.ThrowIfCancellationRequested();
            if (await audioService.LeaveAsync(guildId))
            {
                logger.LogInformation(
                    "Disconnected from empty voice channel {VoiceChannelId} in guild {GuildId} after {IdleTimeout}",
                    cleanup.VoiceChannelId,
                    guildId,
                    IdleTimeout
                );
            }
        }
        catch (OperationCanceledException) when (cleanup.Token.IsCancellationRequested)
        {
            // A human returned or the host is shutting down.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not clean up idle voice channel {VoiceChannelId} in guild {GuildId}", cleanup.VoiceChannelId, guildId);
        }
        finally
        {
            ((ICollection<KeyValuePair<ulong, PendingCleanup>>)_pendingCleanups)
                .Remove(new KeyValuePair<ulong, PendingCleanup>(guildId, cleanup));
            cleanup.Dispose();
        }
    }

    private sealed class PendingCleanup(ulong voiceChannelId, CancellationToken stoppingToken) : IDisposable
    {
        private readonly object _sync = new();
        private readonly CancellationTokenSource _cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        private bool _disposed;

        public ulong VoiceChannelId { get; } = voiceChannelId;
        public CancellationToken Token => _cancellation.Token;

        public void Cancel()
        {
            lock (_sync)
            {
                if (!_disposed)
                {
                    _cancellation.Cancel();
                }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _disposed = true;
                _cancellation.Dispose();
            }
        }
    }
}

internal static class VoiceChannelIdleProtectionPolicy
{
    public static bool ContainsHumanMember(IEnumerable<bool> memberIsBotFlags) => memberIsBotFlags.Any(isBot => !isBot);
}

// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Discord;
using rexmit.Media;
using rexmit.Playback;
using rexmit.Services.Interfaces;

namespace rexmit.Services;

public sealed class AudioService(
    IFFmpegService ffmpegService,
    IYouTubeTrackReader trackReader
) : IAudioService, IAudioStatusReader
{
    private const int MaxQueuedTracks = 25;
    private readonly ConcurrentDictionary<ulong, GuildPlayer> _players = new();
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _guildGates = new();

    public async Task EnqueueAsync(
        ulong guildId,
        IVoiceChannel voiceChannel,
        IMessageChannel textChannel,
        string youtubeUrl,
        string requestedBy
    )
    {
        YouTubeUrl.Validate(youtubeUrl);
        var track = await trackReader.ReadAsync(youtubeUrl, requestedBy);
        var gate = _guildGates.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (!_players.TryGetValue(guildId, out var player))
            {
                var audioClient = await voiceChannel.ConnectAsync();
                player = new GuildPlayer(voiceChannel.Id, audioClient, textChannel, ffmpegService, MaxQueuedTracks);
                _players[guildId] = player;
            }
            else if (player.VoiceChannelId != voiceChannel.Id)
            {
                throw new InvalidOperationException("I am connected to a different voice channel. Use /leave first.");
            }

            if (!player.TryEnqueue(track))
            {
                throw new InvalidOperationException($"The queue is full (maximum {MaxQueuedTracks} waiting tracks).");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public GuildPlaybackStatus? GetStatus(ulong guildId) =>
        _players.TryGetValue(guildId, out var player) ? player.GetStatus(guildId) : null;

    public IReadOnlyList<GuildPlaybackStatus> GetAllStatuses()
    {
        var snapshots = _players.Select(pair => pair.Value.GetStatus(pair.Key)).ToArray();
        return Array.AsReadOnly(snapshots);
    }

    public bool Skip(ulong guildId) => _players.TryGetValue(guildId, out var player) && player.Skip();

    public bool SetPaused(ulong guildId, bool paused) =>
        _players.TryGetValue(guildId, out var player) && player.SetPaused(paused);

    public bool Shuffle(ulong guildId) => _players.TryGetValue(guildId, out var player) && player.Shuffle();

    public bool Remove(ulong guildId, int position) =>
        _players.TryGetValue(guildId, out var player) && player.Remove(position);

    public bool Remove(ulong guildId, Guid trackId) =>
        _players.TryGetValue(guildId, out var player) && player.Remove(trackId);

    public bool Move(ulong guildId, Guid trackId, int offset) =>
        _players.TryGetValue(guildId, out var player) && player.Move(trackId, offset);

    public int Clear(ulong guildId) => _players.TryGetValue(guildId, out var player) ? player.Clear() : 0;

    public async Task<bool> LeaveAsync(ulong guildId)
    {
        var gate = _guildGates.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (!_players.TryRemove(guildId, out var player))
            {
                return false;
            }

            await player.StopAsync();
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task StopAllAsync()
    {
        List<Exception>? failures = null;
        foreach (var guildId in _players.Keys)
        {
            try
            {
                await LeaveAsync(guildId);
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException("One or more guild players failed to stop cleanly.", failures);
        }
    }
}

// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using rexmit.Services.Interfaces;

namespace rexmit.Services;

public sealed class AudioService(IFFmpegService ffmpegService) : IAudioService, IAudioStatusReader
{
    private const int MaxQueuedTracks = 25;
    private readonly ConcurrentDictionary<ulong, GuildPlayer> _players = new();
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _guildGates = new();

    public async Task EnqueueAsync(
        ulong guildId,
        IVoiceChannel voiceChannel,
        IMessageChannel textChannel,
        string youtubeUrl
    )
    {
        ValidateYouTubeUrl(youtubeUrl);
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

            if (!player.TryEnqueue(youtubeUrl))
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

    public bool Remove(ulong guildId, int position) =>
        _players.TryGetValue(guildId, out var player) && player.Remove(position);

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

    private static void ValidateYouTubeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 2048 || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Provide a valid YouTube video URL.", nameof(url));
        }

        var host = uri.Host;
        if (uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 || !string.IsNullOrEmpty(uri.UserInfo) ||
            !(host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
              host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) ||
              host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Only https://youtube.com, https://music.youtube.com, and https://youtu.be links are supported.", nameof(url));
        }
    }

    private sealed class GuildPlayer
    {
        private readonly object _sync = new();
        private readonly List<string> _queue = [];
        private readonly SemaphoreSlim _availableTracks = new(0);
        private readonly CancellationTokenSource _shutdown = new();
        private readonly IAudioClient _audioClient;
        private readonly IMessageChannel _textChannel;
        private readonly IFFmpegService _ffmpegService;
        private readonly int _maxQueuedTracks;
        private readonly Task _worker;
        private CancellationTokenSource? _currentTrackCancellation;
        private string? _current;

        public GuildPlayer(
            ulong voiceChannelId,
            IAudioClient audioClient,
            IMessageChannel textChannel,
            IFFmpegService ffmpegService,
            int maxQueuedTracks
        )
        {
            VoiceChannelId = voiceChannelId;
            _audioClient = audioClient;
            _textChannel = textChannel;
            _ffmpegService = ffmpegService;
            _maxQueuedTracks = maxQueuedTracks;
            _worker = RunAsync();
        }

        public ulong VoiceChannelId { get; }

        public bool TryEnqueue(string url)
        {
            lock (_sync)
            {
                if (_queue.Count >= _maxQueuedTracks)
                {
                    return false;
                }

                _queue.Add(url);
            }

            _availableTracks.Release();
            return true;
        }

        public GuildPlaybackStatus GetStatus(ulong guildId)
        {
            lock (_sync)
            {
                return new GuildPlaybackStatus(guildId, VoiceChannelId, _current, Array.AsReadOnly(_queue.ToArray()));
            }
        }

        public bool Skip()
        {
            lock (_sync)
            {
                if (_currentTrackCancellation is not null)
                {
                    _currentTrackCancellation.Cancel();
                    return true;
                }

                if (_queue.Count == 0)
                {
                    return false;
                }

                _queue.RemoveAt(0);
                _availableTracks.Wait(0);
                return true;
            }
        }

        public bool Remove(int position)
        {
            lock (_sync)
            {
                if (position < 1 || position > _queue.Count)
                {
                    return false;
                }

                _queue.RemoveAt(position - 1);
                _availableTracks.Wait(0);
                return true;
            }
        }

        public int Clear()
        {
            lock (_sync)
            {
                var removed = _queue.Count;
                _queue.Clear();
                while (_availableTracks.Wait(0))
                {
                }

                return removed;
            }
        }

        public async Task StopAsync()
        {
            _shutdown.Cancel();
            lock (_sync)
            {
                _currentTrackCancellation?.Cancel();
            }

            _availableTracks.Release();
            await _worker;
            await _audioClient.StopAsync();
        }

        private async Task RunAsync()
        {
            while (!_shutdown.IsCancellationRequested)
            {
                try
                {
                    await _availableTracks.WaitAsync(_shutdown.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                string track;
                CancellationTokenSource trackCancellation;
                lock (_sync)
                {
                    if (_queue.Count == 0)
                    {
                        continue;
                    }

                    track = _queue[0];
                    _queue.RemoveAt(0);
                    _current = track;
                    trackCancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                    _currentTrackCancellation = trackCancellation;
                }

                try
                {
                    await _textChannel.SendMessageAsync($"Now playing: {Shorten(track)}", allowedMentions: AllowedMentions.None);
                    await _ffmpegService.SendAsync(_audioClient, track, trackCancellation.Token);
                }
                catch (OperationCanceledException) when (trackCancellation.IsCancellationRequested)
                {
                    // Expected when /skip or /leave cancels playback.
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Audio playback failed: {ex.Message}");
                    try
                    {
                        await _textChannel.SendMessageAsync(
                            $"Could not play {Shorten(track)}. It may be unavailable or blocked.",
                            allowedMentions: AllowedMentions.None
                        );
                    }
                    catch (Exception messageException)
                    {
                        Console.WriteLine($"Could not report playback failure: {messageException.Message}");
                    }
                }
                finally
                {
                    lock (_sync)
                    {
                        if (ReferenceEquals(_currentTrackCancellation, trackCancellation))
                        {
                            _currentTrackCancellation = null;
                            _current = null;
                        }
                    }

                    trackCancellation.Dispose();
                }
            }
        }

        private static string Shorten(string url) => url.Length <= 150 ? url : $"{url[..147]}…";
    }
}

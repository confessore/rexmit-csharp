// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Discord;
using Discord.Audio;
using rexmit.Services.Interfaces;

namespace rexmit.Playback;

internal sealed class GuildPlayer
{
    private readonly object _sync = new();
    private readonly List<PlaybackTrack> _queue = [];
    private readonly SemaphoreSlim _availableTracks = new(0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly IAudioClient _audioClient;
    private readonly IAudioChannel _voiceChannel;
    private readonly IMessageChannel _textChannel;
    private readonly IFFmpegService _ffmpegService;
    private readonly int _maxQueuedTracks;
    private readonly Task _worker;
    private CancellationTokenSource? _currentTrackCancellation;
    private PlaybackTrack? _current;
    private TimeSpan _position;
    private bool _isPaused;
    private TaskCompletionSource _resumeSignal = CompletedSignal();

    public GuildPlayer(
        ulong voiceChannelId,
        IAudioChannel voiceChannel,
        IAudioClient audioClient,
        IMessageChannel textChannel,
        IFFmpegService ffmpegService,
        int maxQueuedTracks
    )
    {
        VoiceChannelId = voiceChannelId;
        _voiceChannel = voiceChannel;
        _audioClient = audioClient;
        _textChannel = textChannel;
        _ffmpegService = ffmpegService;
        _maxQueuedTracks = maxQueuedTracks;
        _worker = RunAsync();
    }

    public ulong VoiceChannelId { get; }

    public bool IsUsable => IsUsableState(
        _audioClient.ConnectionState,
        _worker.IsCompleted,
        _shutdown.IsCancellationRequested
    );

    internal static bool IsUsableState(ConnectionState connectionState, bool workerCompleted, bool stopping) =>
        connectionState == ConnectionState.Connected && !workerCompleted && !stopping;

    public bool TryEnqueue(PlaybackTrack track)
    {
        lock (_sync)
        {
            if (_queue.Count >= _maxQueuedTracks)
            {
                return false;
            }

            _queue.Add(track);
        }

        _availableTracks.Release();
        return true;
    }

    public GuildPlaybackStatus GetStatus(ulong guildId)
    {
        lock (_sync)
        {
            return new GuildPlaybackStatus(
                guildId,
                VoiceChannelId,
                _current,
                Array.AsReadOnly(_queue.ToArray()),
                _position,
                _isPaused
            );
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

    public bool SetPaused(bool paused)
    {
        lock (_sync)
        {
            if (_current is null)
            {
                return false;
            }

            if (_isPaused == paused)
            {
                return true;
            }

            _isPaused = paused;
            if (paused)
            {
                _resumeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            else
            {
                _resumeSignal.TrySetResult();
            }

            return true;
        }
    }

    public bool Shuffle()
    {
        lock (_sync)
        {
            if (_queue.Count < 2)
            {
                return false;
            }

            for (var index = _queue.Count - 1; index > 0; index--)
            {
                var other = Random.Shared.Next(index + 1);
                (_queue[index], _queue[other]) = (_queue[other], _queue[index]);
            }

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

    public bool Remove(Guid trackId)
    {
        lock (_sync)
        {
            var index = _queue.FindIndex(track => track.Id == trackId);
            if (index < 0)
            {
                return false;
            }

            _queue.RemoveAt(index);
            _availableTracks.Wait(0);
            return true;
        }
    }

    public bool Move(Guid trackId, int offset)
    {
        lock (_sync)
        {
            return QueueOrder.MoveOne(_queue, trackId, offset);
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
            if (_isPaused)
            {
                _isPaused = false;
                _resumeSignal.TrySetResult();
            }
        }

        _availableTracks.Release();
        try
        {
            await _voiceChannel.DisconnectAsync();
        }
        finally
        {
            try
            {
                await _worker;
            }
            finally
            {
                _shutdown.Dispose();
                _availableTracks.Dispose();
            }
        }
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

            PlaybackTrack track;
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
                _position = TimeSpan.Zero;
                trackCancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                _currentTrackCancellation = trackCancellation;
            }

            try
            {
                await _ffmpegService.SendAsync(
                    _audioClient,
                    track.Url,
                    WaitUntilResumedAsync,
                    SetPosition,
                    trackCancellation.Token
                );
            }
            catch (OperationCanceledException) when (trackCancellation.IsCancellationRequested)
            {
                // Expected when /skip, /leave, or host shutdown cancels playback.
            }
            catch (Exception) when (trackCancellation.IsCancellationRequested)
            {
                // A cancelled audio stream can fail while the voice client is stopping.
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Audio playback failed: {ex}");
                try
                {
                    await _textChannel.SendMessageAsync(
                        $"Could not play {Shorten(track.Title)}. It may be unavailable or blocked.",
                        allowedMentions: AllowedMentions.None
                    );
                }
                catch (Exception messageException)
                {
                    Console.Error.WriteLine($"Could not report playback failure: {messageException}");
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
                        _position = TimeSpan.Zero;
                        if (_isPaused)
                        {
                            _isPaused = false;
                            _resumeSignal.TrySetResult();
                        }
                    }
                }

                trackCancellation.Dispose();
            }
        }
    }

    private async Task WaitUntilResumedAsync(CancellationToken cancellationToken)
    {
        Task resumeTask;
        lock (_sync)
        {
            resumeTask = _resumeSignal.Task;
        }

        await resumeTask.WaitAsync(cancellationToken);
    }

    private void SetPosition(TimeSpan position)
    {
        lock (_sync)
        {
            _position = position;
        }
    }

    private static TaskCompletionSource CompletedSignal()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult();
        return signal;
    }

    private static string Shorten(string value) => value.Length <= 150 ? value : $"{value[..147]}…";
}

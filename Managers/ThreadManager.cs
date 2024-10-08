﻿// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Discord.Audio;
using Discord.Interactions;
using rexmit.Modules;
using rexmit.Services.Interfaces;

namespace rexmit.Managers
{
    public class ThreadManager
    {
        private readonly InteractionModule _interactionModule;
        private readonly IFFmpegService _ffmpegService;

        public ThreadManager(
            InteractionModule interactionModule,
            IFFmpegService ffmpegService,
            IAudioClient client)
        {
            _interactionModule = interactionModule;
            _ffmpegService = ffmpegService;
            _audioClient = client;
            Id = interactionModule.Context.Channel.Id;
        }

        public ulong Id { get; }
        private bool _started;
        public IAudioClient _audioClient;
        private Thread _thread;
        private CancellationTokenSource _cancellationTokenSource;
        public event Action OnTrackStart;
        public event Action OnTrackEnd;
        private List<string> _queue;

        public void Queue(string url)
        {
            _queue ??= [];
            _queue.Add(url);
            StartThread();
        }

        public void Dequeue()
        {
            _queue ??= [];
            _queue.RemoveAt(_queue.Count - 1);
            StartThread();
        }

        public void Skip()
        {
            _queue ??= [];
            _queue.RemoveAt(0);
            _cancellationTokenSource.Cancel();
            _started = false;
            StartThread();
        }

        public void Insert(string url)
        {
            _queue ??= [];
            _queue.Insert(1, url);
            StartThread();
        }

        // Start the thread and store its reference
        public void StartThread()
        {
            if (!_started)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _thread = new Thread(async () => await ThreadWork(_cancellationTokenSource.Token));
                _thread.Start();
                Console.WriteLine("Thread started.");
            }
        }

        // Stop the thread using the stored reference
        public void StopThread()
        {
            if (_cancellationTokenSource != null)
            {
                _started = false;
                _cancellationTokenSource.Cancel();
                _thread.Join(); // Wait for the thread to finish
                Console.WriteLine("Thread stopped.");
            }
        }

        // Retrieve the thread reference
        public Thread GetThread()
        {
            return _thread;
        }

        // The work that the thread will perform
        private async Task ThreadWork(CancellationToken token)
        {
            try
            {
                _started = true;
                while (!token.IsCancellationRequested)
                {
                    Console.WriteLine("Thread is working...");

                    OnTrackStart?.Invoke();
                    await _interactionModule.Context.Channel.SendMessageAsync($"Now playing {_queue[0]}");
                    await _ffmpegService.SendAsync(_audioClient, _queue[0], token);
                    _queue.RemoveAt(0);
                    OnTrackEnd?.Invoke();
                    if (_queue.Count == 0)
                    {
                        StopThread();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Thread is stopping due to cancellation.");
            }
            finally
            {
                if (_queue.Count == 0)
                {
                    StopThread();
                }
                else
                {
                    StopThread();
                    StartThread();
                }

                Console.WriteLine("Thread has stopped.");
            }
        }
    }
}

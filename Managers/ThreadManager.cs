// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord.Audio;
using rexmit.Services.Interfaces;

namespace rexmit.Managers
{
    public class ThreadManager
    {
        private readonly IFFmpegService _ffmpegService;

        public ThreadManager(
            IFFmpegService ffmpegService,
            IAudioClient client, string youtubeUrl)
        {
            _ffmpegService = ffmpegService;
            _audioClient = client;
            _youtubeUrl = youtubeUrl;
        }

        private IAudioClient _audioClient;
        private readonly string _youtubeUrl;
        private Thread _thread;
        private CancellationTokenSource _cancellationTokenSource;

        // Start the thread and store its reference
        public void StartThread()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _thread = new Thread(async () => await ThreadWork(_cancellationTokenSource.Token));
            _thread.Start();
            Console.WriteLine("Thread started.");
        }

        // Stop the thread using the stored reference
        public void StopThread()
        {
            if (_cancellationTokenSource != null)
            {
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
                while (!token.IsCancellationRequested)
                {
                    Console.WriteLine("Thread is working...");
                    await _ffmpegService.SendAsync(_audioClient, _youtubeUrl, _cancellationTokenSource.Token);
                    Thread.Sleep(1000); // Simulate work
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Thread is stopping due to cancellation.");
            }
            finally
            {
                Console.WriteLine("Thread has stopped.");
            }
        }
    }
}

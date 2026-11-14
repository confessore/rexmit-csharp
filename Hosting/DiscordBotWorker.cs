// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Discord;
using Discord.LibDave;
using Discord.LibDave.Binding;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using rexmit.Services;

namespace rexmit.Hosting;

internal sealed class DiscordBotWorker(
    DiscordShardedClient client,
    InteractionHandlingService interactions,
    AudioService audioService,
    DiscordBotOptions options,
    ILogger<DiscordBotWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Dave.SetLogSink((severity, _, lineNumber, message) =>
        {
            if (severity is LoggingSeverity.Warning or LoggingSeverity.Error && message.Length <= 256)
            {
                logger.LogWarning("libdave {Severity} (line {LineNumber}): {Message}", severity, lineNumber, message);
            }
        });

        client.ShardReady += OnShardReadyAsync;
        client.Log += OnLogAsync;

        var loggedIn = false;
        var started = false;
        try
        {
            await interactions.InitializeAsync();
            await client.LoginAsync(TokenType.Bot, options.Token);
            loggedIn = true;
            await client.StartAsync();
            started = true;
            await client.SetCustomStatusAsync("/play YouTube links • /panel player");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is expected; finally disconnects the client below.
        }
        finally
        {
            try
            {
                if (started)
                {
                    await client.StopAsync();
                }
            }
            finally
            {
                try
                {
                    await audioService.StopAllAsync();
                }
                finally
                {
                    if (loggedIn)
                    {
                        await client.LogoutAsync();
                    }
                }
            }
        }
    }

    private Task OnShardReadyAsync(DiscordSocketClient shard)
    {
        logger.LogInformation("Discord shard {ShardId} is connected and ready", shard.ShardId);
        Console.WriteLine($"REXMIT_READY pid={Environment.ProcessId}");
        return Task.CompletedTask;
    }

    private Task OnLogAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical or LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            _ => LogLevel.Debug
        };
        logger.Log(level, message.Exception, "Discord: {Message}", message.Message);
        return Task.CompletedTask;
    }
}

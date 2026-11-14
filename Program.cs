// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.LibDave;
using Discord.LibDave.Binding;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using rexmit.Services;
using rexmit.Services.Interfaces;

namespace rexmit;

internal class Program
{
    private static Task Main() => MainAsync();

    private static async Task MainAsync()
    {
        // Native verbose logs can dump full MLS payloads into the service journal.
        Dave.SetLogSink((severity, _, lineNumber, message) =>
        {
            if ((severity == LoggingSeverity.Warning || severity == LoggingSeverity.Error) && message.Length <= 256)
            {
                Console.WriteLine($"libdave {severity} (line {lineNumber}): {message}");
            }
        });

        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Set the DISCORD_TOKEN environment variable before starting the bot.");
        }

        var config = new DiscordSocketConfig
        {
            TotalShards = 1,
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
            EnableVoiceDaveEncryption = true
        };

        using var services = ConfigureServices(config);
        var client = services.GetRequiredService<DiscordShardedClient>();
        client.ShardReady += ReadyAsync;
        client.Log += LogAsync;

        await services.GetRequiredService<InteractionHandlingService>().InitializeAsync();
        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();
        await client.SetCustomStatusAsync("/play YouTube links");
        await Task.Delay(Timeout.Infinite);
    }

    private static ServiceProvider ConfigureServices(DiscordSocketConfig config) =>
        new ServiceCollection()
            .AddSingleton(new DiscordShardedClient(config))
            .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordShardedClient>()))
            .AddSingleton<InteractionHandlingService>()
            .AddSingleton<IFFmpegService, FFmpegService>()
            .AddSingleton<AudioService>()
            .AddSingleton<IAudioService>(x => x.GetRequiredService<AudioService>())
            .AddSingleton<IAudioStatusReader>(x => x.GetRequiredService<AudioService>())
            .BuildServiceProvider();

    private static Task ReadyAsync(DiscordSocketClient shard)
    {
        Console.WriteLine($"Shard Number {shard.ShardId} is connected and ready!");
        return Task.CompletedTask;
    }

    private static Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }
}

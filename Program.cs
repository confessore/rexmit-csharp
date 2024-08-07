﻿// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using rexmit.DbContexts;
using rexmit.Services;
using rexmit.Services.Interfaces;

namespace rexmit;

// This is a minimal example of using Discord.Net's Sharded Client
// The provided DiscordShardedClient class simplifies having multiple
// DiscordSocketClient instances (or shards) to serve a large number of guilds.
internal class Program
{
    private static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

    public async Task MainAsync()
    {
        // You specify the amount of shards you'd like to have with the
        // DiscordSocketConfig. Generally, it's recommended to
        // have 1 shard per 1500-2000 guilds your bot is in.
        var config = new DiscordSocketConfig()
        {
            TotalShards = 1,
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        };

        // You should dispose a service provider created using ASP.NET
        // when you are finished using it, at the end of your app's lifetime.
        // If you use another dependency injection framework, you should inspect
        // its documentation for the best way to do this.
        using var services = ConfigureServices(config);
        var client = services.GetRequiredService<DiscordShardedClient>();

        // The Sharded Client does not have a Ready event.
        // The ShardReady event is used instead, allowing for individual
        // control per shard.
        client.ShardReady += ReadyAsync;
        client.Log += LogAsync;

        await services.GetRequiredService<InteractionHandlingService>().InitializeAsync();

        await services.GetRequiredService<CommandHandlingService>().InitializeAsync();

        // Tokens should be considered secret data, and never hard-coded.
        await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DISCORD_TOKEN"));
        await client.StartAsync();
        await client.SetCustomStatusAsync("type /help for commands");
        await Task.Delay(Timeout.Infinite);
    }

    private ServiceProvider ConfigureServices(DiscordSocketConfig config) =>
        new ServiceCollection()
            .AddDbContextPool<DefaultDbContext>(options =>
            {
                options.UseNpgsql(
                    Environment.GetEnvironmentVariable("POSTGRES_URL"),
                    b => b.MigrationsAssembly("rexmit")
                );
                //options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            })
            .AddSingleton(new DiscordShardedClient(config))
            .AddSingleton<CommandService>()
            .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordShardedClient>()))
            .AddSingleton<CommandHandlingService>()
            .AddSingleton<InteractionHandlingService>()
            .AddScoped<IFFmpegService, FFmpegService>()
            .AddScoped<IAudioService, AudioService>()
            .AddSingleton<ThreadManagerService>()
            .AddHttpClient<IGPTService, GPTService>(
                nameof(GPTService),
                options =>
                {
                    options.BaseAddress = new Uri("https://api.openai.com/v1/chat/completions");
                    //options.BaseAddress = new Uri("http://192.168.1.147:4891/v1/completions");
                    options.DefaultRequestHeaders.Add("User-Agent", "balasolu");
                    options.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                        "Bearer",
                        Environment.GetEnvironmentVariable("GPT_SECRET")
                    );
                }
            )
            .Services.AddScoped<IGPTService, GPTService>()
            .BuildServiceProvider();

    private Task ReadyAsync(DiscordSocketClient shard)
    {
        Console.WriteLine($"Shard Number {shard.ShardId} is connected and ready!");
        return Task.CompletedTask;
    }

    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }
}

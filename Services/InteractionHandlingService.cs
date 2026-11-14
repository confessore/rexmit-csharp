// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace rexmit.Services;

public class InteractionHandlingService
{
    private readonly InteractionService _service;
    private readonly DiscordShardedClient _client;
    private readonly IServiceProvider _provider;

    public InteractionHandlingService(IServiceProvider services)
    {
        _service = services.GetRequiredService<InteractionService>();
        _client = services.GetRequiredService<DiscordShardedClient>();
        _provider = services;

        _service.Log += LogAsync;
        _client.InteractionCreated += OnInteractionAsync;
        _client.ShardReady += ReadyAsync;
    }

    public async Task InitializeAsync()
    {
        await _service.AddModulesAsync(typeof(InteractionHandlingService).Assembly, _provider);
    }

    private async Task OnInteractionAsync(SocketInteraction interaction)
    {
        var context = new ShardedInteractionContext(_client, interaction);
        await _service.ExecuteCommandAsync(context, _provider);
    }

    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }

    private Task ReadyAsync(DiscordSocketClient _)
    {
        return _service.RegisterCommandsGloballyAsync();
    }
}

// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using rexmit.Media;
using rexmit.Services;
using rexmit.Services.Interfaces;

namespace rexmit.Hosting;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRexmit(this IServiceCollection services, string token)
    {
        var config = new DiscordSocketConfig
        {
            TotalShards = 1,
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
            EnableVoiceDaveEncryption = true
        };

        services.AddSingleton(new DiscordBotOptions(token));
        services.AddSingleton(new DiscordShardedClient(config));
        services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordShardedClient>()));
        services.AddSingleton<InteractionHandlingService>();
        services.AddSingleton<IFFmpegService, FFmpegService>();
        services.AddSingleton<IYouTubeTrackReader, YouTubeTrackReader>();
        services.AddSingleton<AudioService>();
        services.AddSingleton<IAudioService>(x => x.GetRequiredService<AudioService>());
        services.AddSingleton<IAudioStatusReader>(x => x.GetRequiredService<AudioService>());
        services.AddSingleton<PlaybackPanelService>();
        services.AddSingleton<IPlaybackPanelService>(x => x.GetRequiredService<PlaybackPanelService>());
        services.AddHostedService<PlaybackPanelService>(x => x.GetRequiredService<PlaybackPanelService>());
        services.AddHostedService<VoiceChannelIdleProtectionService>();
        services.AddHostedService<DiscordBotWorker>();
        return services;
    }
}

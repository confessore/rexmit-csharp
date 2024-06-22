// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using rexmit.Managers;
using rexmit.Services.Interfaces;
using Victoria.Node;
using RunMode = Discord.Interactions.RunMode;

namespace rexmit.Modules;

// A display of portability, which shows how minimal the difference between the 2 frameworks is.
public class InteractionModule(
    IAudioService audioService,
    IFFmpegService ffmpegService,
    IGPTService gptService) : InteractionModuleBase<ShardedInteractionContext>
{
    private readonly IAudioService _audioService = audioService;
    private readonly IFFmpegService _ffmpegService = ffmpegService;
    private readonly IGPTService _gptService = gptService;

    [SlashCommand("info", "Information about this shard.")]
    public async Task InfoAsync([Remainder] string prompt)
    {
        await DeferAsync(false);
        var chatCompletion = await _gptService.RequestGPTChatCompletionAsync(prompt);
        await FollowupAsync(chatCompletion);
    }

    // The command's Run Mode MUST be set to RunMode.Async, otherwise, being connected to a voice channel will block the gateway thread.
    [SlashCommand("join", "Joins the voice channel.", runMode: RunMode.Async)]
    public async Task JoinChannel(IVoiceChannel channel = null)
    {
        await DeferAsync(false);
        //await _lavaNode.ConnectAsync();
        //var voiceState = Context.User as IVoiceState;
        //await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
        // Get the audio channel
        channel = channel ?? (Context.User as IGuildUser)?.VoiceChannel;
        if (channel == null)
        {
            await Context.Channel.SendMessageAsync(
                "User must be in a voice channel, or a voice channel must be passed as an argument."
            );
            return;
        }

        // For the next step with transmitting audio, you would want to pass this Audio Client in to a service.
        var client = await channel.ConnectAsync();

        await FollowupAsync($"Joined to voice channel {channel}");
    }

    [SlashCommand("queue", "Queues a youtube video.", runMode: RunMode.Async)]
    public async Task Queue([Remainder] string youtubeUrl, IVoiceChannel channel = null)
    {
        await DeferAsync(false);
        channel = channel ?? (Context.User as IGuildUser)?.VoiceChannel;
        if (channel == null)
        {
            await Context.Channel.SendMessageAsync(
                "User must be in a voice channel, or a voice channel must be passed as an argument."
            );
            return;
        }

        var client = await channel.ConnectAsync();
        //var document = JsonDocument.Parse(builder.ToString());
        //var json = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions() { WriteIndented = true });
        //Console.WriteLine(array[0]);
        var threadManager = new ThreadManager(_ffmpegService, client, youtubeUrl);
        threadManager.StartThread();
        await FollowupAsync($"Playing {channel}");
        await Task.Delay(10000);
        threadManager.StopThread();
        //await Task.Run(async () => await _ffmpegService.SendAsync(client, youtubeUrl));
    }
}

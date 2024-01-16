// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using rexmit.Services.Interfaces;
using RunMode = Discord.Interactions.RunMode;

namespace rexmit.Modules;

// A display of portability, which shows how minimal the difference between the 2 frameworks is.
public class InteractionModule : InteractionModuleBase<ShardedInteractionContext>
{
    private readonly IFFmpegService _ffmpegService;
    private readonly IGPTService _gptService;

    public InteractionModule(IFFmpegService ffmpegService, IGPTService gptService)
    {
        _ffmpegService = ffmpegService;
        _gptService = gptService;
    }

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
        _ = await channel.ConnectAsync();
        _ffmpegService.CreateStream("./");
        await FollowupAsync($"Joined to voice channel {channel}");
    }
}

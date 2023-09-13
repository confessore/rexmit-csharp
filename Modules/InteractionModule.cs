// Copyright (c) Balanced Solutions Software.  All Rights Reserved.  Licensed under the MIT license.  See LICENSE in the project root for license information.

using Discord.Commands;
using Discord.Interactions;
using rexmit.Services.Interfaces;

namespace rexmit.Modules;

// A display of portability, which shows how minimal the difference between the 2 frameworks is.
public class InteractionModule : InteractionModuleBase<ShardedInteractionContext>
{
    private readonly IGPTService _gptService;

    public InteractionModule(IGPTService gptService)
    {
        _gptService = gptService;
    }

    [SlashCommand("info", "Information about this shard.")]
    public async Task InfoAsync([Remainder] string prompt)
    {
        await DeferAsync(false);
        var chatCompletion = await _gptService.RequestGPTChatCompletionAsync(prompt);
        await FollowupAsync(chatCompletion);
    }
}

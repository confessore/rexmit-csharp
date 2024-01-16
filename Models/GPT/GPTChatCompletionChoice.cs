// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

namespace rexmit.Models.GPT;

public class GPTChatCompletionChoice
{
    public int Index { get; set; }
    public GPTChatCompletionChoiceMessage Message { get; set; } = new();
    public string Finish_Reason { get; set; } = string.Empty;
}

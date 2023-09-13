// Copyright (c) Balanced Solutions Software.  All Rights Reserved.  Licensed under the MIT license.  See LICENSE in the project root for license information.

namespace rexmit.Models.GPT;

public class GPTChatCompletion
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public int Created { get; set; }
    public string Model { get; set; } = string.Empty;
    public GPTChatCompletionChoice[] Choices { get; set; } = new GPTChatCompletionChoice[1];
    public GPTChatCompletionUsage Usage { get; set; } = new();
}

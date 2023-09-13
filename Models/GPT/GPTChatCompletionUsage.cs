// Copyright (c) Balanced Solutions Software.  All Rights Reserved.  Licensed under the MIT license.  See LICENSE in the project root for license information.

namespace rexmit.Models.GPT;

public class GPTChatCompletionUsage
{
    public int Prompt_Tokens { get; set; }
    public int Completion_Tokens { get; set; }
    public int Total_Tokens { get; set; }
}

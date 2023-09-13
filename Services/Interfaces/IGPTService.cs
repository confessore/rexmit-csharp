// Copyright (c) Balanced Solutions Software.  All Rights Reserved.  Licensed under the MIT license.  See LICENSE in the project root for license information.

namespace rexmit.Services.Interfaces;

public interface IGPTService
{
    Task<string> RequestGPTChatCompletionAsync(string prompt);
}

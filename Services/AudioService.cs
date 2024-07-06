// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Collections.Generic;
using rexmit.Managers;
using rexmit.Services.Interfaces;

namespace rexmit.Services;

public class AudioService : IAudioService
{
    public AudioService()
    {
    }

    private readonly List<ThreadManager> _threadManagers;
}


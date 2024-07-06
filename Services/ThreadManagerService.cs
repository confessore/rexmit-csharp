// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using rexmit.Managers;

namespace rexmit.Services
{
    public sealed class ThreadManagerService
    {
        public ThreadManagerService()
        {
            ThreadManagers = [];
        }

        public List<ThreadManager> ThreadManagers { get; set; }
    }
}

// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using rexmit.Services;

namespace Rexmit.Tests;

[TestClass]
public sealed class VoiceChannelIdleProtectionPolicyTests
{
    [TestMethod]
    public void ContainsHumanMember_OnlyHumanMembersPreventCleanup()
    {
        Assert.IsFalse(VoiceChannelIdleProtectionPolicy.ContainsHumanMember([]));
        Assert.IsFalse(VoiceChannelIdleProtectionPolicy.ContainsHumanMember([true, true]));
        Assert.IsTrue(VoiceChannelIdleProtectionPolicy.ContainsHumanMember([true, false]));
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Server;
using Unity.Core;
using Unity.Entities;

public sealed class BattleFrameClockTests
{
    [Test]
    public void ClockPreservesRemainderAndDoesNotChangeLogicalStep()
    {
        BattleFrameClock clock = new();
        clock.Reset(0);
        clock.Advance(100, 1);
        Assert.That(clock.TakeFrame(33, false), Is.True);
        Assert.That(clock.TakeFrame(33, false), Is.True);
        Assert.That(clock.TakeFrame(33, false), Is.True);
        Assert.That(clock.TakeFrame(33, false), Is.False);
        Assert.That(clock.AccumulatedMilliseconds, Is.EqualTo(1));
        clock.Advance(130, 1.1);
        Assert.That(clock.TakeFrame(33, false), Is.True);
        Assert.That(clock.AccumulatedMilliseconds, Is.EqualTo(1).Within(0.00001));
    }

    [Test]
    public void RateManagerBoundsCatchUpAndRestoresWorldTime()
    {
        using World world = new("Frame clock test");
        ComponentSystemGroup group = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
        ServerFrameManager frame = new() { running = true };
        frame.clock.Reset(0);
        long now = 100;
        BattleFrameRateManager rate = new(frame, () => now);
        world.SetTime(new TimeData(10, 0.016f));
        int count = 0;
        while (rate.ShouldGroupUpdate(group))
        {
            Assert.That(world.Time.DeltaTime, Is.EqualTo(0.033f).Within(0.000001f));
            Assert.That(frame.currentFrame, Is.EqualTo((uint)count));
            count++;
        }
        Assert.That(count, Is.EqualTo(3));
        Assert.That(frame.currentFrame, Is.EqualTo(3));
        Assert.That(world.Time.ElapsedTime, Is.EqualTo(10));
        Assert.That(world.Time.DeltaTime, Is.EqualTo(0.016f));
        now = 1100;
        while (rate.ShouldGroupUpdate(group)) { }
        Assert.That(rate.LastStepCount, Is.EqualTo(4));
        Assert.That(frame.clock.AccumulatedMilliseconds, Is.EqualTo(869));
    }

    [Test]
    public void PreparationRunsOnceWithoutAdvancingBattleTime()
    {
        using World world = new("Frame preparation test");
        ComponentSystemGroup group = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
        FrameManager frame = new();
        BattleFrameRateManager rate = new(frame, () => 1000);
        Assert.That(rate.ShouldGroupUpdate(group), Is.True);
        Assert.That(world.Time.DeltaTime, Is.Zero);
        Assert.That(rate.ShouldGroupUpdate(group), Is.False);
        Assert.That(frame.currentFrame, Is.Zero);
        Assert.That(frame.clock.AccumulatedMilliseconds, Is.Zero);
    }

    [Test]
    public void RttControlsLeadAndDebugSwitchOnlyDisablesAdjustment()
    {
        ClientFrameManager frame = new() { currentFrame = 100 };
        frame.ReceiveClockSample(new B2C_FramePong
        {
            clientSendTime = 0, running = true, serverFrame = 98,
        }, 100);
        Assert.That(frame.UpdateSimulationSpeed(100), Is.GreaterThan(1));
        Assert.That(frame.TargetAheadFrames, Is.EqualTo(3));
        frame.currentFrame = 107;
        Assert.That(frame.UpdateSimulationSpeed(100), Is.LessThan(1));
        frame.SetFrameSpeedAdjustmentEnabled(false);
        Assert.That(frame.UpdateSimulationSpeed(100), Is.EqualTo(1));
        Assert.That(frame.SmoothedRttMs, Is.EqualTo(100));
        frame.SetFrameSpeedAdjustmentEnabled(true);
        Assert.That(frame.UpdateSimulationSpeed(100), Is.LessThan(1));
        Assert.That(frame.UpdateSimulationSpeed(2200), Is.EqualTo(1));
    }

    [Test]
    public void DisabledClientDoesNotForceExtraCatchUpFrames()
    {
        using World world = new("Frame adjustment test");
        ComponentSystemGroup group = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
        ClientFrameManager frame = new() { running = true };
        frame.clock.Reset(100);
        frame.ReceiveClockSample(new B2C_FramePong
        {
            clientSendTime = 0, running = true, serverFrame = 10,
        }, 100);
        BattleFrameRateManager rate = new(frame, () => 100);
        frame.SetFrameSpeedAdjustmentEnabled(false);
        Assert.That(rate.ShouldGroupUpdate(group), Is.False);
        frame.SetFrameSpeedAdjustmentEnabled(true);
        while (rate.ShouldGroupUpdate(group)) { }
        Assert.That(frame.currentFrame, Is.EqualTo(4));
        Assert.That(rate.LastStepCount, Is.EqualTo(4));
    }

    [Test]
    public void ServerConsumesCurrentInputAndClientConsumesOnlyCompletedFrames()
    {
        ServerFrameManager server = new() { currentFrame = 101 };
        ClientFrameManager client = new() { currentFrame = 101 };
        List<uint> serverApplied = new();
        List<uint> clientApplied = new();
        server.onHandleReceive = (frame, states) => serverApplied.Add(frame);
        client.onHandleReceive = (frame, states) => clientApplied.Add(frame);
        for (uint frame = 99; frame <= 102; frame++)
        {
            server.receivedOrder.Add(frame, new Queue<NetworkState>());
            client.receivedOrder.Add(frame, new Queue<NetworkState>());
        }
        server.HandleReceive();
        client.HandleReceive();
        CollectionAssert.AreEqual(new uint[] { 101 }, serverApplied);
        CollectionAssert.AreEqual(new uint[] { 99, 100 }, clientApplied);
        Assert.That(server.receivedOrder.ContainsKey(102), Is.True);
        Assert.That(client.receivedOrder.ContainsKey(101), Is.True);
    }

    [Test]
    public void SeparateRoomsHaveIndependentClockAccumulators()
    {
        BattleFrameClock first = new();
        BattleFrameClock second = new();
        first.Reset(0);
        second.Reset(0);
        first.Advance(33, 1);
        second.Advance(16, 1);
        Assert.That(first.TakeFrame(33, false), Is.True);
        Assert.That(second.TakeFrame(33, false), Is.False);
    }
}

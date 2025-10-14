﻿using GW2EIEvtcParser.ParsedData;

namespace GW2EIEvtcParser.EIData.BuffSimulators;

internal class BuffSimulatorIntensity : BuffSimulator
{
    private readonly List<(AgentItem agent, bool extension)> _lastSrcRemoves = [];
    // Constructor
    public BuffSimulatorIntensity(ParsedEvtcLog log, Buff buff, BuffStackItemPool pool, int capacity) : base(log, buff, pool, capacity)
    {
    }

    public override void Extend(long extension, long oldValue, AgentItem src, long start, uint stackID)
    {
        if ((BuffStack.Count != 0 && oldValue > 0) || IsFull)
        {
            BuffStackItem minItem = BuffStack.MinBy(x => Math.Abs(x.TotalDuration - oldValue));
            if (minItem != null)
            {
                minItem.Extend(extension, src);
            }
        }
        else
        {
            if (_lastSrcRemoves.Count != 0)
            {
                Add(oldValue + extension, src, _lastSrcRemoves.First().agent, start, false, _lastSrcRemoves.First().extension, stackID);
                _lastSrcRemoves.RemoveAt(0);
            }
            else
            {
                Add(oldValue + extension, src, start, stackID, true, 0, 0);
            }
        }
    }

    // Public Methods

    protected override void Update(long timePassed)
    {
        if (BuffStack.Count != 0 && timePassed > 0)
        {
            var toAdd = new BuffSimulationItemIntensity(BuffStack);
            GenerationSimulation.Add(toAdd);
            long diff = Math.Min(BuffStack.Min(x => x.Duration), timePassed);
            long leftOver = timePassed - diff;
            if (toAdd.End > toAdd.Start + diff)
            {
                toAdd.OverrideEnd(toAdd.Start + diff);
            }
            if (leftOver == 0)
            {
                _lastSrcRemoves.Clear();
            }
            // Subtract from each
            foreach (BuffStackItem buffStackItem in BuffStack)
            {
                buffStackItem.Shift(diff, diff);
                if (buffStackItem.Duration == 0)
                {
                    Pool.ReleaseBuffStackItem(buffStackItem);
                    if (leftOver == 0)
                    {
                        _lastSrcRemoves.Add((buffStackItem.SeedSrc, buffStackItem.IsExtension));
                    }
                }
            }
            BuffStack.RemoveAll(x => x.Duration == 0);
            Update(leftOver);
        }
    }
}

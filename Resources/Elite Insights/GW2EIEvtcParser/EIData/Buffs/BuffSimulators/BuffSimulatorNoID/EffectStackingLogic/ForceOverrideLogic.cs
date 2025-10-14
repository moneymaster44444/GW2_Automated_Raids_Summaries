﻿using GW2EIEvtcParser.ParsedData;

namespace GW2EIEvtcParser.EIData.BuffSimulators;

internal class ForceOverrideLogic : StackingLogic
{

    protected override void Sort(ParsedEvtcLog log, List<BuffStackItem> stacks)
    {
        // no sort
    }

    public override bool FindLowestValue(ParsedEvtcLog log, BuffStackItemPool pool, BuffStackItem toAdd, List<BuffStackItem> stacks, List<BuffSimulationItemWasted> wastes, long overridenDuration, uint overridenStackID)
    {
        if (stacks.Count == 0)
        {
            return false;
        }
        BuffStackItem toRemove = stacks[0];
        wastes.Add(new BuffSimulationItemWasted(toRemove.Src, toRemove.Duration, toRemove.Start));
        if (toRemove.Extensions.Count > 0)
        {
            foreach ((AgentItem src, long value) in toRemove.Extensions)
            {
                wastes.Add(new BuffSimulationItemWasted(src, value, toRemove.Start));
            }
        }
        pool.ReleaseBuffStackItem(toRemove);
        stacks[0] = toAdd;
        return true;
    }

    public override bool IsFull(List<BuffStackItem> stacks, int capacity)
    {
        return stacks.Count == 1;
    }
}

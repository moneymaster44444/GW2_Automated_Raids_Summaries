﻿namespace GW2EIEvtcParser.EIData.BuffSimulators;

internal class CappedDurationLogic : StackingLogic
{
    protected override void Sort(ParsedEvtcLog log, List<BuffStackItem> stacks)
    {
        // nothing to sort
    }

    public override bool IsFull(List<BuffStackItem> stacks, int capacity)
    {
        // never full
        return false;
    }

    public override bool FindLowestValue(ParsedEvtcLog log, BuffStackItemPool pool, BuffStackItem stackItem, List<BuffStackItem> stacks, List<BuffSimulationItemWasted> wastes, long overridenDuration, uint overridenStackID)
    {
        // no lowest value to find
        return false;
    }
}

﻿using GW2EIEvtcParser.Interfaces;

namespace GW2EIEvtcParser.ParsedData;

public class HealthUpdateEvent : StatusEvent, IStateable
{
    public readonly double HealthPercent;

    internal HealthUpdateEvent(CombatItem evtcItem, AgentData agentData) : base(evtcItem, agentData)
    {
        HealthPercent = GetHealthPercent(evtcItem);
        if (HealthPercent > 100.0)
        {
            HealthPercent = 100;
        }
    }

    internal static double GetHealthPercent(CombatItem evtcItem)
    {
        return Math.Round(evtcItem.DstAgent / 100.0, 2);
    }

    public (long start, double value) ToState()
    {
        return (Time, HealthPercent);
    }
    public (long start, double value) ToState(long overridenStart)
    {
        return (overridenStart, HealthPercent);
    }
}

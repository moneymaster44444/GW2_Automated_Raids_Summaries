﻿namespace GW2EIEvtcParser.ParsedData;

public class SpawnEvent : StatusEvent
{
    internal SpawnEvent(CombatItem evtcItem, AgentData agentData) : base(evtcItem, agentData)
    {

    }

    internal SpawnEvent(AgentItem src, long time) : base(src, time)
    {

    }

}

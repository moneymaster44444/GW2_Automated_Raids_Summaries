﻿using GW2EIEvtcParser.EIData;

namespace GW2EIEvtcParser.ParsedData;

public class MechanicEvent : TimeCombatEvent
{
    private readonly Mechanic _mechanic;
    public readonly SingleActor Actor;
    public string ShortName => _mechanic.ShortName;
    public string Description => _mechanic.Description;

    internal MechanicEvent(long time, Mechanic mech, SingleActor actor) : base(time)
    {
        Actor = actor;
        _mechanic = mech;
    }
}

﻿namespace GW2EIEvtcParser.ParsedData;

public class SpeciesGUIDEvent : IDToGUIDEvent
{
    internal static SpeciesGUIDEvent DummySpeciesGUID = new();
    internal SpeciesGUIDEvent(CombatItem evtcItem, EvtcVersionEvent evtcVersion) : base(evtcItem)
    {
    }

    internal SpeciesGUIDEvent() : base()
    {
    }
}


﻿using System.Diagnostics.CodeAnalysis;
using GW2EIEvtcParser.ParsedData;

namespace GW2EIEvtcParser.EIData;


internal class PlayerSrcAllHealthDamageHitsMechanic : PlayerSrcHealthDamageHitMechanic
{

    public PlayerSrcAllHealthDamageHitsMechanic(MechanicPlotlySetting plotlySetting, string shortName, string description, string fullName, int internalCoolDown) : base(0, plotlySetting, shortName, description, fullName, internalCoolDown)
    {
    }
    protected override bool TryGetActor(ParsedEvtcLog log, AgentItem agentItem, Dictionary<int, SingleActor> regroupedMobs, [NotNullWhen(true)] out SingleActor? actor)
    {
        throw new InvalidOperationException();
    }

    protected override AgentItem GetAgentItem(HealthDamageEvent ahde)
    {
        throw new InvalidOperationException();
    }

    internal override void CheckMechanic(ParsedEvtcLog log, Dictionary<Mechanic, List<MechanicEvent>> mechanicLogs, Dictionary<int, SingleActor> regroupedMobs)
    {
        foreach (Player p in log.PlayerList)
        {
            foreach (HealthDamageEvent ahde in (Minions ? p.GetDamageEvents(null, log) : p.GetJustActorDamageEvents(null, log)))
            {
                if (Keep(ahde, log))
                {
                    InsertMechanic(log, mechanicLogs, ahde.Time, p);
                }
            }
        }
    }
}

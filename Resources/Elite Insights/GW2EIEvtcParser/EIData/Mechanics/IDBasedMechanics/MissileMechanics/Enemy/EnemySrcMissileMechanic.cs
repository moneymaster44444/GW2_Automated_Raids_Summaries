﻿using System.Diagnostics.CodeAnalysis;
using GW2EIEvtcParser.ParsedData;

namespace GW2EIEvtcParser.EIData;


internal class EnemySrcMissileMechanic : SrcMissileMechanic
{

    public EnemySrcMissileMechanic(long mechanicID, MechanicPlotlySetting plotlySetting, string shortName, string description, string fullName, int internalCoolDown) : this([mechanicID], plotlySetting, shortName, description, fullName, internalCoolDown)
    {
    }

    public EnemySrcMissileMechanic(long[] mechanicIDs, MechanicPlotlySetting plotlySetting, string shortName, string description, string fullName, int internalCoolDown) : base(mechanicIDs, plotlySetting, shortName, description, fullName, internalCoolDown)
    {
        IsEnemyMechanic = true;
    }
    protected override bool TryGetActor(ParsedEvtcLog log, AgentItem agentItem, Dictionary<int, SingleActor> regroupedMobs, [NotNullWhen(true)] out SingleActor? actor)
    {
        actor = MechanicHelper.FindEnemyActor(log, agentItem, regroupedMobs);
        return actor != null;
    }
}

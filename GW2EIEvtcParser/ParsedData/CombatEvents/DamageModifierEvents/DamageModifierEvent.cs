﻿using GW2EIEvtcParser.EIData;

namespace GW2EIEvtcParser.ParsedData;

internal class DamageModifierEvent : TimeCombatEvent
{
    public readonly DamageModifier DamageModifier;
    public readonly AgentItem Src;
    public readonly AgentItem Dst;
    public readonly double DamageGain;

    internal DamageModifierEvent(HealthDamageEvent evt, DamageModifier damageModifier, double damageGain) : base(evt.Time)
    {
        Src = evt.From.FindEnglobedAgentItem(Time);
        Dst = evt.To.FindEnglobedAgentItem(Time);
        DamageGain = damageGain;
        DamageModifier = damageModifier;
    }
}

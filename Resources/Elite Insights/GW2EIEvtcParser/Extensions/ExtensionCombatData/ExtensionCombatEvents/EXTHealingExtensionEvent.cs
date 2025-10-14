﻿using GW2EIEvtcParser.ParsedData;

namespace GW2EIEvtcParser.Extensions;

public abstract class EXTHealingExtensionEvent : DamageEvent
{

    public readonly bool SrcIsPeer;
    public readonly bool DstIsPeer;

    [Flags]
    protected enum HealingExtensionOffcycleBits : byte
    {
        BuffDamageDstIsDowned = 1 << 5,
        DstPeerMask = 1 << 6,
        SrcPeerMask = 1 << 7,
        PeerMask = DstPeerMask | SrcPeerMask,
    }

    internal EXTHealingExtensionEvent(CombatItem evtcItem, AgentData agentData, SkillData skillData) : base(evtcItem, agentData, skillData)
    {
        SrcIsPeer = (evtcItem.IsOffcycle & (byte)HealingExtensionOffcycleBits.SrcPeerMask) > 0;
        DstIsPeer = (evtcItem.IsOffcycle & (byte)HealingExtensionOffcycleBits.DstPeerMask) > 0;
        AgainstDowned = (evtcItem.IsOffcycle & (byte)HealingExtensionOffcycleBits.BuffDamageDstIsDowned) > 0;
        if (!SrcIsPeer && !DstIsPeer)
        {
            SrcIsPeer = true;
        }
    }

}

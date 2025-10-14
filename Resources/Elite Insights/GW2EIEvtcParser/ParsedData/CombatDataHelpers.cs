﻿using static GW2EIEvtcParser.ParserHelper;

namespace GW2EIEvtcParser.ParsedData;

partial class CombatData
{
    public static IEnumerable<T> FindRelatedEvents<T>(IEnumerable<T> events, long time, long epsilon = ServerDelayConstant) where T : TimeCombatEvent
    {
        return events.Where(evt => Math.Abs(evt.Time - time) < epsilon);
    }
    public bool HasRelatedHit(long skillID, AgentItem agent, long time, long epsilon = ServerDelayConstant)
    {
        return FindRelatedEvents(GetDamageData(skillID), time, epsilon)
            .Any(hit => hit.CreditedFrom.Is(agent));
    }
    public bool HasPreviousCast(long skillID, AgentItem agent, long time, long epsilon = ServerDelayConstant)
    {
        return FindRelatedEvents(GetAnimatedCastData(skillID), time, epsilon)
            .Any(cast => cast.Caster.Is(agent) && cast.Time <= time);
    }
    public bool IsCasting(long skillID, AgentItem agent, long time, long epsilon = ServerDelayConstant)
    {
        return GetAnimatedCastData(skillID)
            .Any(cast => cast.Caster.Is(agent) && cast.Time - epsilon <= time && cast.EndTime + epsilon >= time);
    }
    public bool HasGainedBuff(long buffID, AgentItem agent, long time, long epsilon = ServerDelayConstant)
    {
        return FindRelatedEvents(GetBuffApplyDataByIDByDst(buffID, agent).OfType<BuffApplyEvent>(), time, epsilon)
            .Any();
    }
    public bool HasGainedBuff(long buffID, AgentItem agent, long time, AgentItem source, long epsilon = ServerDelayConstant)
    {
        return FindRelatedEvents(GetBuffApplyDataByIDByDst(buffID, agent).OfType<BuffApplyEvent>(), time, epsilon)
            .Any(apply => apply.CreditedBy.Is(source));
    }
    public bool HasGainedBuff(long buffID, AgentItem agent, long time, long appliedDuration, long epsilon = ServerDelayConstant)
    {
        return FindRelatedEvents(GetBuffApplyDataByIDByDst(buffID, agent).OfType<BuffApplyEvent>(), time, epsilon)
            .Any(apply => Math.Abs(apply.AppliedDuration - appliedDuration) < epsilon);
    }
    public bool HasGainedBuff(long buffID, AgentItem agent, long time, long appliedDuration, AgentItem source, long epsilon = ServerDelayConstant)
    {
        return FindRelatedEvents(GetBuffApplyDataByIDByDst(buffID, agent).OfType<BuffApplyEvent>(), time, epsilon)
            .Any(apply => apply.CreditedBy.Is(source) && Math.Abs(apply.AppliedDuration - appliedDuration) < epsilon);
    }
    public bool HasLostBuff(long buffID, AgentItem agent, long time, long epsilon = ServerDelayConstant)
    {
        return FindRelatedEvents(GetBuffDataByIDByDst(buffID, agent).OfType<BuffRemoveAllEvent>(), time, epsilon)
            .Any();
    }
    public bool HasLostBuffStack(long buffID, AgentItem agent, long time, long epsilon = ServerDelayConstant)
    {
        return FindRelatedEvents(GetBuffDataByIDByDst(buffID, agent).OfType<AbstractBuffRemoveEvent>(), time, epsilon)
            .Any();
    }

    public bool HasRelatedEffect(GUID effectGUID, AgentItem agent, long time, long epsilon = ServerDelayConstant)
    {
        if (TryGetEffectEventsBySrcWithGUID(agent, effectGUID, out var effectEvents))
        {
            return FindRelatedEvents(effectEvents, time, epsilon).Any();
        }
        return false;
    }

    public bool HasRelatedEffectDst(GUID effectGUID, AgentItem agent, long time, long epsilon = ServerDelayConstant)
    {
        if (TryGetEffectEventsByDstWithGUID(agent, effectGUID, out var effectEvents))
        {
            return FindRelatedEvents(effectEvents, time, epsilon).Any();
        }
        return false;
    }
    public bool HasExtendedBuff(long buffID, AgentItem agent, long time, long epsilon = ServerDelayConstant)
    {
        return FindRelatedEvents(GetBuffApplyDataByIDByDst(buffID, agent).OfType<BuffExtensionEvent>(), time, epsilon)
            .Any();
    }
    public bool HasExtendedBuff(long buffID, AgentItem agent, long time, AgentItem source, long epsilon = ServerDelayConstant)
    {
        return FindRelatedEvents(GetBuffApplyDataByIDByDst(buffID, agent).OfType<BuffExtensionEvent>(), time, epsilon)
            .Any(apply => apply.CreditedBy.Is(source));
    }
    public bool HasExtendedBuff(long buffID, AgentItem agent, long time, long extendedDuration, long epsilon = ServerDelayConstant)
    {
        return FindRelatedEvents(GetBuffApplyDataByIDByDst(buffID, agent).OfType<BuffExtensionEvent>(), time, epsilon)
            .Any(apply => Math.Abs(apply.ExtendedDuration - extendedDuration) < epsilon);
    }
    public bool HasExtendedBuff(long buffID, AgentItem agent, long time, long extendedDuration, AgentItem source, long epsilon = ServerDelayConstant)
    {
        return FindRelatedEvents(GetBuffApplyDataByIDByDst(buffID, agent).OfType<BuffExtensionEvent>(), time, epsilon)
            .Any(apply => apply.CreditedBy.Is(source) && Math.Abs(apply.ExtendedDuration - extendedDuration) < epsilon);
    }

}

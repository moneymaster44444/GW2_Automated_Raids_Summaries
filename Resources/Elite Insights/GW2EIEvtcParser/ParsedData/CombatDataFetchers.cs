﻿using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using GW2EIEvtcParser.Exceptions;
using static GW2EIEvtcParser.ArcDPSEnums;
using static GW2EIEvtcParser.ParserHelper;

namespace GW2EIEvtcParser.ParsedData;

partial class CombatData
{
    public IReadOnlyCollection<long> GetSkills()
    {
        return _skillIDs;
    }

    internal static IReadOnlyList<T> GetValueOrEmpty<T>(Dictionary<AgentItem, List<T>> dict, AgentItem agent) where T : MetaDataEvent
    {
        return dict.GetValueOrEmpty(agent.EnglobingAgentItem);
    }

    internal static IReadOnlyList<T> GetTimeValueOrEmpty<T>(Dictionary<AgentItem, List<T>> dict, AgentItem agent) where T : TimeCombatEvent
    {
        if (agent.IsEnglobedAgent)
        {
            return dict.GetValueOrEmpty(agent.EnglobingAgentItem).Where(x => x.Time >= agent.FirstAware && x.Time <= agent.LastAware).ToList();
        }
        return dict.GetValueOrEmpty(agent);
    }

    #region BUILD
    public EvtcVersionEvent GetEvtcVersionEvent()
    {
        return _metaDataEvents.EvtcVersionEvent!;
    }
    public GW2BuildEvent GetGW2BuildEvent()
    {
        if (_metaDataEvents.GW2BuildEvent == null)
        {
            throw new EvtcCombatEventException("Missing Build Event");
        }
        return _metaDataEvents.GW2BuildEvent;
    }
    #endregion BUILD
    #region STATUS
    public IReadOnlyList<AliveEvent> GetAliveEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.AliveEvents, src);
    }
    public IReadOnlyList<DeadEvent> GetDeadEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.DeadEvents, src);
    }
    public IReadOnlyList<DespawnEvent> GetDespawnEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.DespawnEvents, src);
    }
    public IReadOnlyList<DownEvent> GetDownEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.DownEvents, src);
    }
    public IReadOnlyList<SpawnEvent> GetSpawnEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.SpawnEvents, src);
    }
    public IReadOnlyList<BreakbarStateEvent> GetBreakbarStateEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.BreakbarStateEvents, src);
    }
    public IReadOnlyList<EnterCombatEvent> GetEnterCombatEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.EnterCombatEvents, src);
    }
    public IReadOnlyList<ExitCombatEvent> GetExitCombatEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.ExitCombatEvents, src);
    }
    public IReadOnlyList<TeamChangeEvent> GetTeamChangeEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.TeamChangeEvents, src);
    }
    #endregion STATUS
    #region ATTACKTARGETS
    public IReadOnlyList<AttackTargetEvent> GetAttackTargetEvents()
    {
        return _metaDataEvents.AttackTargetEvents;
    }
    public IReadOnlyList<AttackTargetEvent> GetAttackTargetEventsBySrc(AgentItem targetedAgent)
    {
        return GetValueOrEmpty(_metaDataEvents.AttackTargetEventsBySrc, targetedAgent);
    }

    public AttackTargetEvent? GetAttackTargetEventByAttackTarget(AgentItem attackTarget)
    {
        if (_metaDataEvents.AttackTargetEventByAttackTarget.TryGetValue(attackTarget, out var attackTargetEvent)) 
        {
            return attackTargetEvent;
        }
        return null;
    }
    public IReadOnlyList<TargetableEvent> GetTargetableEventsBySrc(AgentItem attackTarget)
    {
        return _statusEvents.TargetableEventsBySrc.GetValueOrEmpty(attackTarget.EnglobingAgentItem);
    }
    #endregion ATTACKTARGETS
    #region DATE
    public InstanceStartEvent? GetInstanceStartEvent()
    {
        return _metaDataEvents.InstanceStartEvent;
    }

    public SquadCombatStartEvent? GetLogStartEvent()
    {
        return _metaDataEvents.LogStartEvent;
    }

    public IReadOnlyList<SquadCombatStartEvent> GetSquadCombatStartEvents()
    {
        return _metaDataEvents.SquadCombatStartEvents;
    }

    public IReadOnlyList<LogNPCUpdateEvent> GetLogNPCUpdateEvents()
    {
        return _metaDataEvents.LogNPCUpdateEvents;
    }

    public SquadCombatEndEvent? GetLogEndEvent()
    {
        return _metaDataEvents.LogEndEvent;
    }
    public IReadOnlyList<SquadCombatEndEvent> GetSquadCombatEndEvents()
    {
        return _metaDataEvents.SquadCombatEndEvents;
    }
    #endregion DATE

    public IReadOnlyList<GuildEvent> GetGuildEvents(AgentItem src)
    {
        return GetValueOrEmpty(_metaDataEvents.GuildEvents, src);
    }

    public PointOfViewEvent? GetPointOfViewEvent()
    {
        return _metaDataEvents.PointOfViewEvent;
    }
    public FractalScaleEvent? GetFractalScaleEvent()
    {
        return _metaDataEvents.FractalScaleEvent;
    }
    public LanguageEvent? GetLanguageEvent()
    {
        return _metaDataEvents.LanguageEvent;
    }
    public IReadOnlyList<MapIDEvent> GetMapIDEvents()
    {
        return _metaDataEvents.MapIDEvents;
    }

    public IReadOnlyList<MapChangeEvent> GetMapChangeEvents()
    {
        return _metaDataEvents.MapChangeEvents;
    }

    public IReadOnlyList<RewardEvent> GetRewardEvents()
    {
        return _rewardEvents;
    }

    public IReadOnlyList<ErrorEvent> GetErrorEvents()
    {
        return _metaDataEvents.ErrorEvents;
    }

    public IReadOnlyList<ShardEvent> GetShardEvents()
    {
        return _metaDataEvents.ShardEvents;
    }

    public IReadOnlyList<TickRateEvent> GetTickRateEvents()
    {
        return _metaDataEvents.TickRateEvents;
    }

    #region MARKERS

    /// <summary>
    /// Returns squad marker events of given marker index
    /// </summary>
    /// <param name="markerIndex">marker index</param>
    /// <returns></returns>
    public IReadOnlyList<SquadMarkerEvent> GetSquadMarkerEvents(SquadMarkerIndex markerIndex)
    {
        return _statusEvents.SquadMarkerEventsByIndex.GetValueOrEmpty(markerIndex);
    }
    /// <summary>
    /// Returns marker events owned by agent
    /// </summary>
    /// <param name="agent"></param>
    /// <returns></returns>
    public IReadOnlyList<MarkerEvent> GetMarkerEvents(AgentItem agent)
    {
        return GetTimeValueOrEmpty(_statusEvents.MarkerEventsBySrc, agent);
    }
    /// <summary>
    /// Returns marker events of given marker ID
    /// </summary>
    /// <param name="markerID">marker ID</param>
    /// <returns></returns>
    public IReadOnlyList<MarkerEvent> GetMarkerEventsByMarkerID(long markerID)
    {
        return _statusEvents.MarkerEventsByID.GetValueOrEmpty(markerID);
    }
    /// <summary>
    /// True if marker events of given marker GUID has been found
    /// </summary>
    /// <param name="marker">marker GUID</param>
    /// <param name="markerEvents">Found marker events</param>
    public bool TryGetMarkerEventsByGUID(GUID marker, [NotNullWhen(true)] out IReadOnlyList<MarkerEvent>? markerEvents)
    {
        var markerGUIDEvent = GetMarkerGUIDEvent(marker);
        markerEvents = GetMarkerEventsByMarkerID(markerGUIDEvent.ContentID);
        if (markerEvents.Count > 0)
        {
            return true;
        }
        markerEvents = null;
        return false;
    }
    /// <summary>
    /// True if marker events of given marker GUID has been found on given agent
    /// </summary>
    /// <param name="agent">marker owner</param>
    /// <param name="marker">marker GUID</param>
    /// <param name="markerEvents">Found marker events</param>
    public bool TryGetMarkerEventsBySrcWithGUID(AgentItem agent, GUID marker, [NotNullWhen(true)] out IReadOnlyList<MarkerEvent>? markerEvents)
    {
        if (TryGetMarkerEventsByGUID(marker, out var markers))
        {
            markerEvents = markers.Where(effect => effect.Src.Is(agent)).ToList();
            return true;
        }
        markerEvents = null;
        return false;
    }

    #endregion MARKERS
    
    #region UPDATES

    public IReadOnlyList<BarrierUpdateEvent> GetBarrierUpdateEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.BarrierUpdateEvents, src);
    }

    public IReadOnlyList<MaxHealthUpdateEvent> GetMaxHealthUpdateEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.MaxHealthUpdateEvents, src);
    }
    public IReadOnlyList<HealthUpdateEvent> GetHealthUpdateEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.HealthUpdateEvents, src);
    }
    public IReadOnlyList<BreakbarPercentEvent> GetBreakbarPercentEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.BreakbarPercentEvents, src);
    }
    #endregion UPDATES

    #region INFO

    public BuffInfoEvent? GetBuffInfoEvent(long buffID)
    {
        return _metaDataEvents.BuffInfoEvents.GetValueOrDefault(buffID);
    }

    public IReadOnlyList<BuffInfoEvent> GetBuffInfoEvent(byte category)
    {
        return _metaDataEvents.BuffInfoEventsByCategory.GetValueOrEmpty(category);
    }

    public SkillInfoEvent? GetSkillInfoEvent(long skillID)
    {
        return _metaDataEvents.SkillInfoEvents.GetValueOrDefault(skillID);
    }
    #endregion INFO
    #region LAST90
    public IReadOnlyList<Last90BeforeDownEvent> GetLast90BeforeDownEvents()
    {
        return _statusEvents.Last90BeforeDownEvents;
    }

    public IReadOnlyList<Last90BeforeDownEvent> GetLast90BeforeDownEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.Last90BeforeDownEventsBySrc, src);
    }
    #endregion LAST90
    #region BUFFS
    public IReadOnlyList<BuffEvent> GetBuffData(long buffID)
    {
        return _buffData.GetValueOrEmpty(buffID);
    }
    public IReadOnlyList<AbstractBuffApplyEvent> GetBuffApplyData(long buffID)
    {
        return _buffApplyData.GetValueOrEmpty(buffID);
    }
    public IReadOnlyList<BuffApplyEvent> GetBuffApplyDataByIDBySrc(long buffID, AgentItem src)
    {
        if (_buffApplyDataByIDBySrc.TryGetValue(buffID, out var agentDict))
        {
            return GetTimeValueOrEmpty(agentDict, src);
        }
        return [];
    }
    /// <summary>
    /// Won't return Buff Extension events
    /// </summary>
    /// <param name="src"></param>
    /// <returns></returns>
    public IReadOnlyList<BuffEvent> GetBuffDataBySrc(AgentItem src)
    {
        return GetTimeValueOrEmpty(_buffDataBySrc, src);
    }
    /// <summary>
    /// Returns list of buff events applied on agent for given id
    /// </summary>
    public IReadOnlyList<BuffEvent> GetBuffDataByIDByDst(long buffID, AgentItem dst)
    {
        if (_buffDataByIDByDst.TryGetValue(buffID, out var agentDict))
        {
            return GetTimeValueOrEmpty(agentDict, dst);
        }
        return [];
    }
    /// <summary>
    /// Returns list of buff apply events applied on agent for given id
    /// </summary>
    public IReadOnlyList<AbstractBuffApplyEvent> GetBuffApplyDataByIDByDst(long buffID, AgentItem dst)
    {
        if (_buffApplyDataByIDByDst.TryGetValue(buffID, out var agentDict))
        {
            return GetTimeValueOrEmpty(agentDict, dst);
        }
        return [];
    }
    /// <summary>
    /// Returns list of buff apply events applied on agent 
    /// </summary>
    public IReadOnlyList<AbstractBuffApplyEvent> GetBuffApplyDataByDst(AgentItem dst)
    {
        return GetTimeValueOrEmpty(_buffApplyDataByDst, dst);
    }
    public IReadOnlyList<BuffEvent> GetBuffDataByInstanceID(long buffID, uint instanceID)
    {
        if (instanceID == 0)
        {
            return GetBuffData(buffID);
        }
        if (_buffDataByInstanceID.TryGetValue(buffID, out var dict))
        {
            return dict.GetValueOrEmpty(instanceID);
        }
        return [];
    }
    public IReadOnlyList<BuffRemoveAllEvent> GetBuffRemoveAllData(long buffID)
    {
        return _buffRemoveAllData.GetValueOrEmpty(buffID);
    }
    public IReadOnlyList<BuffRemoveAllEvent> GetBuffRemoveAllDataByIDBySrc(long buffID, AgentItem src)
    {
        if (_buffRemoveAllDataByIDBySrc.TryGetValue(buffID, out var bySrc))
        {
            return GetTimeValueOrEmpty(bySrc, src);
        }
        return [];
    }

    public IReadOnlyList<BuffRemoveAllEvent> GetBuffRemoveAllDataBySrc( AgentItem src)
    {
        return GetTimeValueOrEmpty(_buffRemoveAllDataBySrc, src);
    }

    public IReadOnlyList<BuffRemoveAllEvent> GetBuffRemoveAllDataByIDByDst(long buffID, AgentItem dst)
    {
        if (_buffRemoveAllDataByIDByDst.TryGetValue(buffID, out var byDst))
        {
            return GetTimeValueOrEmpty(byDst, dst);
        }
        return [];
    }

    public IReadOnlyList<BuffRemoveAllEvent> GetBuffRemoveAllDataByDst( AgentItem dst)
    {
        return GetTimeValueOrEmpty(_buffRemoveAllDataByDst, dst);
    }

    /// <summary>
    /// Returns list of buff events applied on agent
    /// </summary>
    public IReadOnlyList<BuffEvent> GetBuffDataByDst(AgentItem dst)
    {
        return GetTimeValueOrEmpty(_buffDataByDst, dst);
    }
    #endregion BUFFS
    #region DAMAGE
    /// <summary>
    /// Returns list of damage events done by agent
    /// </summary>
    public IReadOnlyList<HealthDamageEvent> GetDamageData(AgentItem src)
    {
        return GetTimeValueOrEmpty(_damageData, src);
    }

    /// <summary>
    /// Returns list of breakbar damage events done by agent
    /// </summary>
    public IReadOnlyList<BreakbarDamageEvent> GetBreakbarDamageData(AgentItem src)
    {
        return GetTimeValueOrEmpty(_breakbarDamageData, src);
    }
    /// <summary>
    /// Returns list of breakbar damage events done by skill id
    /// </summary>
    public IReadOnlyList<BreakbarDamageEvent> GetBreakbarDamageData(long skillID)
    {
        return _breakbarDamageDataByID.GetValueOrEmpty(skillID);
    }
    /// <summary>
    /// Returns list of damage events applied by a skill
    /// </summary>
    public IReadOnlyList<HealthDamageEvent> GetDamageData(long skillID)
    {
        return _damageDataByID.GetValueOrEmpty(skillID);
    }
    /// <summary>
    /// Returns list of damage events taken by Agent
    /// </summary>
    public IReadOnlyList<HealthDamageEvent> GetDamageTakenData(AgentItem dst)
    {
        return GetTimeValueOrEmpty(_damageTakenData, dst);
    }

    /// <summary>
    /// Returns list of breakbar damage events taken by Agent
    /// </summary>
    public IReadOnlyList<BreakbarDamageEvent> GetBreakbarDamageTakenData(AgentItem dst)
    {
        return GetTimeValueOrEmpty(_breakbarDamageTakenData, dst);
    }
    /// <summary>
    /// Returns list of breakbar recover events done by skill id
    /// </summary>
    public IReadOnlyList<BreakbarRecoveryEvent> GetBreakbarRecoveredData(long skillID)
    {
        return _breakbarRecoveredDataByID.GetValueOrEmpty(skillID);
    }
    /// <summary>
    /// Returns list of breakbar recover events on agent
    /// </summary>
    public IReadOnlyList<BreakbarRecoveryEvent> GetBreakbarRecoveredData(AgentItem dst)
    {
        return GetTimeValueOrEmpty(_breakbarRecoveredData, dst);
    }
    #endregion DAMAGE
    #region CROWDCONTROL
    /// <summary>
    /// Returns list of crowd control events done by agent
    /// </summary>
    public IReadOnlyList<CrowdControlEvent> GetOutgoingCrowdControlData(AgentItem src)
    {
        return GetTimeValueOrEmpty(_crowControlData, src);
    }
    /// <summary>
    /// Returns list of crowd control events done by skill id
    /// </summary>
    public IReadOnlyList<CrowdControlEvent> GetCrowdControlData(long skillID)
    {
        return _crowControlDataByID.GetValueOrEmpty(skillID);
    }

    /// <summary>
    /// Returns list of crowd control events taken by Agent
    /// </summary>
    public IReadOnlyList<CrowdControlEvent> GetIncomingCrowdControlData(AgentItem dst)
    {
        return GetTimeValueOrEmpty(_crowControlTakenData, dst);
    }

    public IReadOnlyList<StunBreakEvent> GetStunBreakEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.StunBreakEventsBySrc, src);
    }
    #endregion CROWDCONTROL
    #region CAST
    /// <summary>
    /// Returns list of animated cast events done by Agent
    /// </summary>
    public IReadOnlyList<AnimatedCastEvent> GetAnimatedCastData(AgentItem caster)
    {
        return GetTimeValueOrEmpty(_animatedCastData, caster);
    }

    /// <summary>
    /// Returns list of instant cast events done by Agent
    /// </summary>
    public IReadOnlyList<InstantCastEvent> GetInstantCastData(AgentItem caster)
    {
        return GetTimeValueOrEmpty(_instantCastData, caster);
    }
    /// <summary>
    /// Returns list of instant cast events done by Agent
    /// </summary>
    public IReadOnlyList<InstantCastEvent> GetInstantCastData(long skillID)
    {
        return _instantCastDataByID.GetValueOrEmpty(skillID);
    }

    /// <summary>
    /// Returns list of weapon swap events done by Agent
    /// </summary>
    public IReadOnlyList<WeaponSwapEvent> GetWeaponSwapData(AgentItem caster)
    {
        return GetTimeValueOrEmpty(_weaponSwapData, caster);
    }
    /// <summary>
    /// Returns list of cast events from skill
    /// </summary>
    public IReadOnlyList<AnimatedCastEvent> GetAnimatedCastData(long skillID)
    {
        return _animatedCastDataByID.GetValueOrEmpty(skillID);
    }
    #endregion CAST
    #region MOVEMENTS
    public IReadOnlyList<MovementEvent> GetMovementData(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.MovementEvents, src);
    }
    public IReadOnlyList<GliderEvent> GetGliderEvents(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.GliderEventsBySrc, src);
    }
    #endregion MOVEMENTS
    #region EFFECTS
    public IReadOnlyList<EffectEvent> GetEffectEventsBySrc(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.EffectEventsBySrc, src);
    }

    public IReadOnlyList<EffectEvent> GetEffectEventsByDst(AgentItem dst)
    {
        return GetTimeValueOrEmpty(_statusEvents.EffectEventsByDst, dst);
    }

    public IReadOnlyList<EffectEvent> GetEffectEventsByEffectID(long effectID)
    {
        return _statusEvents.EffectEventsByEffectID.GetValueOrEmpty(effectID);
    }

    /// <returns>true on success</returns>
    public bool TryGetEffectEventsByGUID(GUID effectGUID, [NotNullWhen(true)] out IReadOnlyList<EffectEvent>? effectEvents)
    {
        var effectGUIDEvent = GetEffectGUIDEvent(effectGUID);
        effectEvents = GetEffectEventsByEffectID(effectGUIDEvent.ContentID);
        if (effectEvents.Count > 0)
        {
            return true;
        }
        effectEvents = null;
        return false;
    }

    /// <returns>true on success</returns>
    public bool TryGetEffectEventsByGUIDs(Span<GUID> effects, out List<EffectEvent> effectEvents)
    {
        //TODO(Rennorb) @perf: fid average complexity
        effectEvents = new(effects.Length * 10);
        foreach (var effectGUID in effects)
        {
            if (TryGetEffectEventsByGUID(effectGUID, out var events))
            {
                effectEvents.AddRange(events);
            }
        }

        return effectEvents.Count > 0;
    }

    private static List<EffectEvent> GetSrcEffectEventsCheckingParent(AgentItem agent, IReadOnlyList<EffectEvent> effects)
    {
        List<EffectEvent> result;
        if (agent.IsEnglobedAgent)
        {
            result = effects.Where(effect => effect.Src.Is(agent) && effect.Time >= agent.FirstAware && effect.Time <= agent.LastAware).ToList();
        }
        else
        {
            result = effects.Where(effect => effect.Src.Is(agent)).ToList();
        }
        return result;
    }

    private static List<EffectEvent> GetSrcWithMasterEffectEventsCheckingParent(AgentItem agent, IReadOnlyList<EffectEvent> effects)
    {
        List<EffectEvent> result;
        if (agent.IsEnglobedAgent)
        {
            var parentAgent = agent.EnglobingAgentItem;
            result = effects.Where(effect => parentAgent.IsMasterOf(effect.Src) && effect.Time >= agent.FirstAware && effect.Time <= agent.LastAware).ToList();
        }
        else
        {
            result = effects.Where(effect => agent.IsMasterOf(effect.Src)).ToList();
        }
        return result;
    }

    private static List<EffectEvent> GetDstEffectEventsCheckingParent(AgentItem agent, IReadOnlyList<EffectEvent> effects)
    {
        List<EffectEvent> result;
        if (agent.IsEnglobedAgent)
        {
            result = effects.Where(effect => effect.Dst.Is(agent) && effect.Time >= agent.FirstAware && effect.Time <= agent.LastAware).ToList();
        }
        else
        {
            result = effects.Where(effect => effect.Dst.Is(agent)).ToList();
        }
        return result;
    }

    /// <summary>
    /// Returns effect events by the given agent and effect GUID.
    /// </summary>
    /// <returns>true on found effect with entries > 0</returns>
    public bool TryGetEffectEventsBySrcWithGUID(AgentItem agent, GUID effect, [NotNullWhen(true)] out IReadOnlyList<EffectEvent>? effectEvents)
    {
        if (TryGetEffectEventsByGUID(effect, out var effects))
        {
            List<EffectEvent> result = GetSrcEffectEventsCheckingParent(agent, effects);
            if (result.Count > 0)
            {
                effectEvents = result;
                return true;
            }
        }

        effectEvents = null;
        return false;
    }

    /// <summary>
    /// Appends effect events by the given agent and effect GUID.
    /// </summary>
    public void AppendEffectEventsBySrcWithGUID(AgentItem agent, GUID effect, List<EffectEvent> effectEvents)
    {
        if (TryGetEffectEventsByGUID(effect, out var effects))
        {
            effectEvents.AddRange(GetSrcEffectEventsCheckingParent(agent, effects));
        }
    }


    /// <summary>
    /// Returns effect events on the given agent and effect GUID.
    /// </summary>
    /// <returns>true on success</returns>
    public bool TryGetEffectEventsByDstWithGUID(AgentItem agent, GUID effect, [NotNullWhen(true)] out IReadOnlyList<EffectEvent>? effectEvents)
    {
        if (TryGetEffectEventsByGUID(effect, out var effects))
        {
            List<EffectEvent> result = GetDstEffectEventsCheckingParent(agent, effects);
            if (result.Count > 0)
            {
                effectEvents = result;
                return true;
            }
        }

        effectEvents = null;
        return false;
    }
    /// <summary>
    /// Append effect events on the given agent and effect GUID.
    /// </summary>
    public void AppendEffectEventsByDstWithGUID(AgentItem agent, GUID effect, List<EffectEvent> effectEvents)
    {
        if (TryGetEffectEventsByGUID(effect, out var effects))
        {
            effectEvents.AddRange(GetDstEffectEventsCheckingParent(agent, effects));
        }
    }

    /// <summary>
    /// Returns effect events by the given agent and effect GUIDs.
    /// </summary>
    /// <returns>true on success</returns>
    public bool TryGetEffectEventsBySrcWithGUIDs(AgentItem agent, ReadOnlySpan<GUID> effects, out List<EffectEvent> effectEvents)
    {
        //TODO(Rennorb) @perf: find average complexity
        effectEvents = new List<EffectEvent>(effects.Length * 10);
        foreach (var effectGUID in effects)
        {
            AppendEffectEventsBySrcWithGUID(agent, effectGUID, effectEvents);
        }

        return effectEvents.Count > 0;
    }
    /// <summary>
    /// Returns effect events on the given agent and effect GUIDs.
    /// </summary>
    /// <returns>true on success</returns>
    public bool TryGetEffectEventsByDstWithGUIDs(AgentItem agent, ReadOnlySpan<GUID> effects, out List<EffectEvent> effectEvents)
    {
        //TODO(Rennorb) @perf: find average complexity
        effectEvents = new List<EffectEvent>(effects.Length * 10);
        foreach (var effectGUID in effects)
        {
            AppendEffectEventsByDstWithGUID(agent, effectGUID, effectEvents);
        }

        return effectEvents.Count > 0;
    }

    /// <summary>
    /// Returns effect events by the given agent <b>including</b> minions and the given effect GUID.
    /// </summary>
    /// <returns>true on success</returns>
    public bool TryGetEffectEventsByMasterWithGUID(AgentItem agent, GUID effect, [NotNullWhen(true)] out IReadOnlyList<EffectEvent>? effectEvents)
    {
        if (TryGetEffectEventsByGUID(effect, out var effects))
        {
            List<EffectEvent> result = GetSrcWithMasterEffectEventsCheckingParent(agent, effects);
            if (result.Count > 0)
            {
                effectEvents = result;
                return true;
            }
        }

        effectEvents = null;
        return false;
    }

    /// <summary>
    /// Returns effect events by the given agent <b>including</b> minions and the given effect GUID.
    /// </summary>
    public void AppendEffectEventsByMasterWithGUID(AgentItem agent, GUID effect, List<EffectEvent> effectEvents)
    {
        if (TryGetEffectEventsByGUID(effect, out var effects))
        {
            effectEvents.AddRange(GetSrcWithMasterEffectEventsCheckingParent(agent, effects));
        }
    }

    /// <summary>
    /// Returns effect events by the given agent <b>including</b> minions and the given effect GUIDs.
    /// </summary>
    /// <returns>true on success</returns>
    public bool TryGetEffectEventsByMasterWithGUIDs(AgentItem agent, Span<GUID> effects, out List<EffectEvent> effectEvents)
    {
        effectEvents = [];
        foreach (var effectGUID in effects)
        {
            AppendEffectEventsByMasterWithGUID(agent, effectGUID, effectEvents);
        }

        return effectEvents.Count > 0;
    }

    /// <summary>
    /// Returns effect events by the given agent and effect GUID.
    /// Effects happening within epsilon milliseconds are grouped together.
    /// </summary>
    /// <param name="epsilon">Windows size</param>
    /// <returns>true on success</returns>
    public bool TryGetGroupedEffectEventsBySrcWithGUID(AgentItem agent, GUID guid, [NotNullWhen(true)] out List<List<EffectEvent>>? groupedEffectEvents, long epsilon = ServerDelayConstant)
    {
        if (!TryGetEffectEventsBySrcWithGUID(agent, guid, out var effects))
        {
            groupedEffectEvents = null;
            return false;
        }
        groupedEffectEvents = EpsilonWindowOverTime(effects, epsilon);

        return true;
    }
    /// <summary>
    /// Returns effect events by the given agent and effect GUIDs.
    /// Effects happening within epsilon milliseconds are grouped together.
    /// </summary>
    /// <param name="epsilon">Windows size</param>
    /// <returns>true on success</returns>
    public bool TryGetGroupedEffectEventsBySrcWithGUIDs(AgentItem agent, Span<GUID> guids, [NotNullWhen(true)] out List<List<EffectEvent>>? groupedEffectEvents, long epsilon = ServerDelayConstant)
    {
        if (!TryGetEffectEventsBySrcWithGUIDs(agent, guids, out var effects))
        {
            groupedEffectEvents = null;
            return false;
        }
        groupedEffectEvents = EpsilonWindowOverTime(effects, epsilon);

        return true;
    }
    /// <summary>
    /// Returns effect events for the given effect GUID.
    /// Effects happening within epsilon milliseconds are grouped together.
    /// </summary>
    /// <param name="epsilon">Window size</param>
    /// <returns>true on success</returns>
    public bool TryGetGroupedEffectEventsByGUID(GUID effect, [NotNullWhen(true)] out List<List<EffectEvent>>? groupedEffectEvents, long epsilon = ServerDelayConstant)
    {
        if (!TryGetEffectEventsByGUID(effect, out var effects))
        {
            groupedEffectEvents = null;
            return false;
        }

        groupedEffectEvents = EpsilonWindowOverTime(effects, epsilon);

        return true;
    }
    /// <summary>
    /// Returns effect events for the given effect GUIDs.
    /// Effects happening within epsilon milliseconds are grouped together.
    /// </summary>
    /// <param name="epsilon">Window size</param>
    /// <returns>true on success</returns>
    public bool TryGetGroupedEffectEventsByGUIDs(Span<GUID> guids, [NotNullWhen(true)] out List<List<EffectEvent>>? groupedEffectEvents, long epsilon = ServerDelayConstant)
    {
        if (!TryGetEffectEventsByGUIDs(guids, out var effects))
        {
            groupedEffectEvents = null;
            return false;
        }

        groupedEffectEvents = EpsilonWindowOverTime(effects, epsilon);

        return true;
    }

    static List<List<EffectEvent>> EpsilonWindowOverTime(IReadOnlyList<EffectEvent> effectEvents, long epsilon)
    {
        //NOTE(Rennorb): Has entries due to invariant on TryGetEffectEventsBySrcWithGUID
        var startTime = effectEvents[0].Time;
        var endTime = effectEvents[^1].Time;
        var slices = Math.Max(1, (int)((endTime - startTime + (epsilon - 1)) / epsilon)); // ceiling of total duration / epsilon, and at least one slice
        var groupedEffectEvents = new List<List<EffectEvent>>(slices);


        var blockStart = startTime;
        var blockEnd = blockStart + epsilon;
        var currentBlock = new List<EffectEvent>(effectEvents.Count / slices); // assume average distribution
        int index = 0;
        foreach (var effectEvent in effectEvents)
        {
            if (effectEvent.Time >= blockEnd)
            {
                groupedEffectEvents.Add(currentBlock);
                currentBlock = new((effectEvents.Count - index) / slices); // assume average distribution in remaining blocks
                blockStart = effectEvent.Time;
                blockEnd = blockStart + epsilon;
            }

            currentBlock.Add(effectEvent);
            index++;
        }
        groupedEffectEvents.Add(currentBlock);

        return groupedEffectEvents;
    }


    public IReadOnlyList<EffectEvent> GetEffectEvents()
    {
        return _statusEvents.EffectEvents;
    }
    #endregion EFFECTS
    #region GUIDS
    public EffectGUIDEvent GetEffectGUIDEvent(GUID effectGUID)
    {
        return _metaDataEvents.EffectGUIDEventsByGUID.TryGetValue(effectGUID, out var evt) ? evt : EffectGUIDEvent.DummyEffectGUID;
    }

    internal EffectGUIDEvent GetEffectGUIDEvent(long effectID)
    {
        if (_metaDataEvents.EffectGUIDEventsByEffectID.TryGetValue(effectID, out var evt))
        {
            return evt;
        }
#if DEBUG2
        if (GetEffectEventsByEffectID(effectID).Count > 0)
        {
            throw new EvtcCombatEventException("Missing GUID event for effect " + effectID);
        }
#endif
        return EffectGUIDEvent.DummyEffectGUID;
    }

    public SkillGUIDEvent? GetSkillGUIDEvent(GUID skill)
    {
        return _metaDataEvents.SkillGUIDEventsByGUID.TryGetValue(skill, out var evt) ? evt : HasSpeciesAndSkillGUIDs ? SkillGUIDEvent.DummySkillGUID : null;
    }

    internal SkillGUIDEvent? GetSkillGUIDEvent(long skillID)
    {
        return _metaDataEvents.SkillGUIDEventsBySkillID.TryGetValue(skillID, out var evt) ? evt : HasSpeciesAndSkillGUIDs ? SkillGUIDEvent.DummySkillGUID : null;
    }

    public SpeciesGUIDEvent? GetSpeciesGUIDEvent(GUID species)
    {
        return _metaDataEvents.SpeciesGUIDEventsByGUID.TryGetValue(species, out var evt) ? evt : HasSpeciesAndSkillGUIDs ? SpeciesGUIDEvent.DummySpeciesGUID : null;
    }

    internal SpeciesGUIDEvent? GetSpeciesGUIDEvent(long speciesID)
    {
        return _metaDataEvents.SpeciesGUIDEventsBySpeciesID.TryGetValue(speciesID, out var evt) ? evt : HasSpeciesAndSkillGUIDs ? SpeciesGUIDEvent.DummySpeciesGUID : null;
    }

    public MarkerGUIDEvent GetMarkerGUIDEvent(GUID marker)
    {
        return _metaDataEvents.MarkerGUIDEventsByGUID.TryGetValue(marker, out var evt) ? evt : MarkerGUIDEvent.DummyMarkerGUID;
    }

    internal MarkerGUIDEvent GetMarkerGUIDEvent(long markerID)
    {
        return _metaDataEvents.MarkerGUIDEventsByMarkerID.TryGetValue(markerID, out var evt) ? evt : MarkerGUIDEvent.DummyMarkerGUID;
    }
    #endregion GUIDS
    #region MISSILE

    public IReadOnlyList<MissileEvent> GetMissileEvents()
    {
        return _statusEvents.MissileEvents;
    }
    public IReadOnlyList<MissileEvent> GetMissileEventsBySrc(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.MissileEventsBySrc, src);
    }
    public IReadOnlyList<MissileEvent> GetMissileEventsBySrcBySkillID(AgentItem src, long skillID)
    {
        return GetMissileEventsBySrc(src).Where(x => x.SkillID == skillID).ToList();
    }
    public IReadOnlyList<MissileEvent> GetMissileEventsBySrcBySkillIDs(AgentItem src, long[] skillIDs)
    {
        var events = new List<MissileEvent>();
        foreach (long id in skillIDs)
        {
            events.AddRange(GetMissileEventsBySrcBySkillID(src, id));
        }
        return events;
    }
    public IReadOnlyList<MissileEvent> GetMissileEventsBySkillID(long skillID)
    {
        return _statusEvents.MissileEventsBySkillID.GetValueOrEmpty(skillID);
    }

    public IReadOnlyList<MissileEvent> GetMissileEventsBySkillIDs(long[] skillIDs)
    {
        var events = new List<MissileEvent>();
        foreach (long id in skillIDs)
        {
            events.AddRange(GetMissileEventsBySkillID(id));
        }
        return events;
    }
    public IReadOnlyList<MissileLaunchEvent> GetMissileLaunchEventsByDst(AgentItem dst)
    {
        return GetTimeValueOrEmpty(_statusEvents.MissileLaunchEventsByDst, dst);
    }
    public IReadOnlyList<MissileEvent> GetMissileDamagingEventsBySrc(AgentItem src)
    {
        return GetTimeValueOrEmpty(_statusEvents.MissileDamagingEventsBySrc, src);
    }
    #endregion MISSILE
}

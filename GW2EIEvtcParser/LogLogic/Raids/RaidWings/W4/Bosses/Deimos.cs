﻿using System.Numerics;
using GW2EIEvtcParser.EIData;
using GW2EIEvtcParser.Exceptions;
using GW2EIEvtcParser.Extensions;
using GW2EIEvtcParser.ParsedData;
using GW2EIEvtcParser.ParserHelpers;
using static GW2EIEvtcParser.ArcDPSEnums;
using static GW2EIEvtcParser.LogLogic.LogLogicPhaseUtils;
using static GW2EIEvtcParser.LogLogic.LogLogicTimeUtils;
using static GW2EIEvtcParser.LogLogic.LogLogicUtils;
using static GW2EIEvtcParser.ParserHelper;
using static GW2EIEvtcParser.ParserHelpers.LogImages;
using static GW2EIEvtcParser.SkillIDs;
using static GW2EIEvtcParser.SpeciesIDs;

namespace GW2EIEvtcParser.LogLogic;

internal class Deimos : BastionOfThePenitent
{
    internal readonly MechanicGroup Mechanics = new MechanicGroup([
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic(RapidDecay, new MechanicPlotlySetting(Symbols.CircleOpen,Colors.Black), "Oil", "Rapid Decay (Black expanding oil)","Black Oil", 0),
                new PlayerDstFirstHealthDamageHitMechanic(RapidDecay, new MechanicPlotlySetting(Symbols.Circle,Colors.Black), "Oil T.","Rapid Decay Trigger (Black expanding oil)", "Black Oil Trigger",0)
                    .UsingChecker((ce, log) => {
                        SingleActor? actor = log.FindActor(ce.To);
                        if (actor == null)
                        {
                            return false;
                        }
                        (_, IReadOnlyList<Segment> downs , _, _) = actor.GetStatus(log);
                        bool hitInDown = downs.Any(x => x.ContainsPoint(ce.Time));
                        return !hitInDown;
                    }
                ),
            ]),
            new MechanicGroup([
                new EnemyCastStartMechanic(OffBalance, new MechanicPlotlySetting(Symbols.DiamondTall,Colors.DarkTeal), "TP CC", "Off Balance (Saul TP Breakbar)","Saul TP Start", 0),
                new EnemyCastEndMechanic(OffBalance, new MechanicPlotlySetting(Symbols.DiamondTall,Colors.Red), "TP CC Fail", "Failed Saul TP CC","Failed CC (TP)", 0)
                    .UsingChecker((ce,log) => ce.ActualDuration >= 2200),
                new EnemyCastEndMechanic(OffBalance, new MechanicPlotlySetting(Symbols.DiamondTall,Colors.DarkGreen), "TP CCed", "Saul TP CCed","CCed (TP)", 0)
                    .UsingChecker((ce, log) => ce.ActualDuration < 2200),
            ]),
            new MechanicGroup([
                new EnemyCastStartMechanic(BoonThief, new MechanicPlotlySetting(Symbols.DiamondWide,Colors.DarkTeal), "Thief CC", "Boon Thief (Saul Breakbar)","Boon Thief Start", 0),
                new EnemyCastEndMechanic(BoonThief, new MechanicPlotlySetting(Symbols.DiamondWide,Colors.Red), "Thief CC Fail", "Failed Boon Thief CC","Failed CC (Thief)", 0)
                    .UsingChecker((ce,log) => ce.ActualDuration >= 4400),
                new EnemyCastEndMechanic(BoonThief, new MechanicPlotlySetting(Symbols.DiamondWide,Colors.DarkGreen), "Thief CCed", "Boon Thief CCed","CCed (Thief)", 0)
                    .UsingChecker((ce, log) => ce.ActualDuration < 4400),
            ]),
            new PlayerDstHealthDamageHitMechanic([Annihilate2, Annihilate1], new MechanicPlotlySetting(Symbols.Hexagon,Colors.Yellow), "Pizza", "Annihilate (Cascading Pizza attack)","Boss Smash", 0),
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic(DemonicShockWaveRight, new MechanicPlotlySetting(Symbols.TriangleRightOpen,Colors.Red), "10% RSmash", "Knockback (right hand) in 10% Phase","10% Right Smash", 0),
                new PlayerDstHealthDamageHitMechanic(DemonicShockWaveLeft, new MechanicPlotlySetting(Symbols.TriangleLeftOpen,Colors.Red), "10% LSmash", "Knockback (left hand) in 10% Phase","10% Left Smash", 0),
                new PlayerDstHealthDamageHitMechanic(DemonicShockWaveCenter, new MechanicPlotlySetting(Symbols.Bowtie,Colors.Red), "10% DSmash", "Knockback (both hands) in 10% Phase","10% Double Smash", 0),
            ]),
            new PlayerDstBuffApplyMechanic(TearInstability, new MechanicPlotlySetting(Symbols.Diamond,Colors.DarkTeal), "Tear", "Collected a Demonic Tear","Tear", 0),
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic(MindCrush, new MechanicPlotlySetting(Symbols.Square,Colors.Blue), "Mind Crush", "Hit by Mind Crush without Bubble Protection","Mind Crush", 0)
                    .UsingChecker( (de,log) => de.HealthDamage > 0),
                new PlayerDstBuffApplyMechanic(WeakMinded, new MechanicPlotlySetting(Symbols.SquareOpen,Colors.LightPurple), "Weak Mind", "Weak Minded (Debuff after Mind Crush)","Weak Minded", 0),
            ]),
            new MechanicGroup([
                new PlayerDstBuffApplyMechanic(DeimosSelectedByGreen, new MechanicPlotlySetting(Symbols.Circle,Colors.Green), "Green", "Chosen by the Eye of Janthir","Chosen (Green)", 0),
                new PlayerDstBuffApplyMechanic(GreenTeleport, new MechanicPlotlySetting(Symbols.CircleOpen,Colors.Green), "TP", "Teleport to/from Demonic Realm","Teleport", 0),
            ]),
            new EnemyDstBuffApplyMechanic(UnnaturalSignet, new MechanicPlotlySetting(Symbols.SquareOpen,Colors.Teal), "DMG Debuff", "Double Damage Debuff on Deimos","+100% Dmg Buff", 0)
        ]);

    private bool _hasPreEvent = false;
    private const long PreEventConsiderationConstant = 5000;

    public Deimos(int triggerID) : base(triggerID)
    {
        MechanicList.Add(Mechanics);
        Extension = "dei";
        GenericFallBackMethod = FallBackMethod.None;
        Icon = EncounterIconDeimos;
        LogCategoryInformation.InSubCategoryOrder = 3;
        LogID |= 0x000004;
        // TODO: verify this works even in demonic realm
        ChestID = ChestID.SaulsTreasureChest;
    }

    internal override CombatReplayMap GetCombatMapInternal(ParsedEvtcLog log, CombatReplayDecorationContainer arenaDecorations)
    {
        var crMap = new CombatReplayMap(
                        (765, 1000),
                        (-9542, 1932, -7004, 5250));
        AddArenaDecorationsPerEncounter(log, arenaDecorations, LogID, CombatReplayDeimos, crMap);
        return crMap;
    }
    internal override List<InstantCastFinder> GetInstantCastFinders()
    {
        return
        [
            new DamageCastFinder(DemonicAura, DemonicAura),
        ];
    }

    internal override IReadOnlyList<TargetID>  GetFriendlyNPCIDs()
    {
        return
        [
            TargetID.Saul,
            TargetID.ShackledPrisoner
        ];
    }

    private static void MergeWithGadgets(AgentItem deimos, long upperTimeThreshold, HashSet<AgentItem> gadgets, AgentItem mainBody, List<CombatItem> combatData, AgentData agentData, IReadOnlyDictionary<uint, ExtensionHandler> extensions)
    {
        foreach (AgentItem gadget in gadgets)
        {
            AgentManipulationHelper.RedirectNPCEventsAndCopyPreviousStates(combatData, extensions, agentData, gadget, [gadget], deimos, false,
                (evt, from, to) =>
                {
                    // Only keep damage events from arms
                    if (from != mainBody && !evt.IsDamage())
                    {
                        return false;
                    }
                    // Deimos can't go above 10% hp during that phase
                    if (evt.IsStateChange == StateChange.HealthUpdate && HealthUpdateEvent.GetHealthPercent(evt) > 10.01)
                    {
                        return false;
                    }
                    return true;
                },
                (evt, from, to) =>
                {
                    if (evt.IsStateChange == StateChange.MaxHealthUpdate)
                    {
                        evt.OverrideSrcAgent(ParserHelper._unknownAgent);
                    }
                    if (evt.IsGeographical && evt.Time < upperTimeThreshold)
                    {
                        evt.OverrideTime(upperTimeThreshold);
                    }
                }
            );
        }
    }

    internal override List<BuffEvent> SpecialBuffEventProcess(CombatData combatData, SkillData skillData)
    {
        var res = new List<BuffEvent>();
        IReadOnlyList<BuffEvent> signets = combatData.GetBuffData(UnnaturalSignet);
        foreach (BuffEvent bfe in signets)
        {
            if (bfe is BuffApplyEvent ba)
            {
                BuffEvent? removal = signets.FirstOrDefault(x => x is BuffRemoveAllEvent && x.Time > bfe.Time && x.Time < bfe.Time + 30000);
                if (removal == null)
                {
                    res.Add(new BuffRemoveAllEvent(_unknownAgent, ba.To, ba.Time + ba.AppliedDuration, 0, skillData.Get(UnnaturalSignet), IFF.Unknown, 1, 0));
                    res.Add(new BuffRemoveManualEvent(_unknownAgent, ba.To, ba.Time + ba.AppliedDuration, 0, skillData.Get(UnnaturalSignet), IFF.Unknown));
                }
            }
            else if (bfe is BuffRemoveAllEvent brea)
            {
                BuffEvent? apply = signets.FirstOrDefault(x => x is BuffApplyEvent && x.Time < bfe.Time && x.Time > bfe.Time - 30000);
                if (apply == null)
                {
                    res.Add(new BuffApplyEvent(_unknownAgent, brea.To, bfe.Time - 10000, 10000, skillData.Get(UnnaturalSignet), IFF.Unknown, uint.MaxValue, true));
                }
            }
        }
        return res;
    }

    internal override void CheckSuccess(CombatData combatData, AgentData agentData, LogData logData, IReadOnlyCollection<AgentItem> playerAgents)
    {
        base.CheckSuccess(combatData, agentData, logData, playerAgents);
        if (logData.Success)
        {
            return;
        }
        SingleActor deimos = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.Deimos)) ?? throw new MissingKeyActorsException("Deimos not found");
        var percent10Start = deimos.AgentItem.Merges.FirstOrNull((in AgentItem.MergedAgentItem x) => x.Merged.Is(deimos.AgentItem));
        if (percent10Start != null)
        {
            long percent10StartTime = percent10Start.Value.MergeEnd;
            if (!agentData.TryGetFirstAgentItem(TargetID.Saul, out var saul))
            {
                throw new MissingKeyActorsException("Saul not found");
            }
            if (combatData.GetDeadEvents(saul).Count > 0)
            {
                return;
            }
            IReadOnlyList<AttackTargetEvent> attackTargets = combatData.GetAttackTargetEventsBySrc(deimos.AgentItem);
            if (attackTargets.Count == 0)
            {
                return;
            }
            AttackTargetEvent attackTargetEvent = attackTargets.Last();
            // sanity check
            TargetableEvent? attackableEvent = attackTargetEvent.GetTargetableEvents(combatData).LastOrDefault(x => x.Targetable && x.Time > percent10StartTime - ServerDelayConstant);
            if (attackableEvent == null)
            {
                return;
            }
            TargetableEvent? notAttackableEvent = attackTargetEvent.GetTargetableEvents(combatData).LastOrDefault(x => !x.Targetable && x.Time > attackableEvent.Time);
            if (notAttackableEvent == null)
            {
                return;
            }
            // Saul stays around post encounter
            if (saul.LastAware <= notAttackableEvent.Time || combatData.GetDespawnEvents(saul).Any(x => x.Time <= notAttackableEvent.Time && x.Time >= percent10StartTime))
            {
                return;
            }
            HealthDamageEvent? lastDamageTaken = combatData.GetDamageTakenData(deimos.AgentItem).LastOrDefault(x => (x.HealthDamage > 0) && x.Time > percent10StartTime && playerAgents.Any(x.From.IsMasterOrSelf) && !x.ToFriendly);
            if (lastDamageTaken != null)
            {
                // This means Deimos received damage after becoming non attackable, that means it did not die
                HealthDamageEvent? friendlyDamageToDeimos = combatData.GetDamageTakenData(deimos.AgentItem).LastOrDefault(x => (x.HealthDamage > 0) && x.Time > percent10StartTime && x.Time > lastDamageTaken.Time && x.ToFriendly);
                if (friendlyDamageToDeimos != null || !AtLeastOnePlayerAlive(combatData, logData, notAttackableEvent.Time, playerAgents))
                {
                    return;
                }
                logData.SetSuccess(true, notAttackableEvent.Time);
            }
            if (!logData.Success)
            {
                logData.SetSuccess(false, notAttackableEvent.Time);
            }
        }
    }

    private static AgentItem? GetShackledPrisonerFirstOrDefault(AgentData agentData, List<CombatItem> combatData)
    {
        CombatItem? shackledPrisonerMaxHP = combatData.FirstOrDefault(x => MaxHealthUpdateEvent.GetMaxHealth(x) == 1000980 && x.IsStateChange == StateChange.MaxHealthUpdate && !agentData.GetAgent(x.SrcAgent, x.Time).IsNonIdentifiedSpecies());
        if (shackledPrisonerMaxHP != null)
        {
            return agentData.GetAgent(shackledPrisonerMaxHP.SrcAgent, shackledPrisonerMaxHP.Time);
        }
        return null;
    }

    internal override long GetLogOffset(EvtcVersionEvent evtcVersion, LogData logData, AgentData agentData, List<CombatItem> combatData)
    {
        var deimos = agentData.GetNPCsByID(TargetID.Deimos).FirstOrDefault() ?? throw new MissingKeyActorsException("Deimos not found");
        long start = long.MinValue;
        long genericStart = GetGenericLogOffset(logData);
        // enter combat
        CombatItem? spawnProtectionRemove = combatData.FirstOrDefault(x => x.DstMatchesAgent(deimos) && x.IsBuffRemove == BuffRemove.All && x.SkillID == SpawnProtection);
        if (spawnProtectionRemove != null)
        {
            start = Math.Max(start, spawnProtectionRemove.Time);
            if (start - genericStart > PreEventConsiderationConstant)
            {
                AgentItem? shackledPrisoner = GetShackledPrisonerFirstOrDefault(agentData, combatData);
                if (shackledPrisoner != null)
                {
                    CombatItem? firstGreen = combatData.FirstOrDefault(x => x.IsBuffApply() && x.SkillID == DeimosSelectedByGreen);
                    CombatItem? firstHPUpdate = combatData.FirstOrDefault(x => x.IsStateChange == StateChange.HealthUpdate && x.SrcMatchesAgent(shackledPrisoner));
                    if (firstGreen != null && firstGreen.Time < start && firstHPUpdate != null && HealthUpdateEvent.GetHealthPercent(firstHPUpdate) == 100) // sanity check
                    {
                        _hasPreEvent = true;
                        start = firstHPUpdate.Time;
                    }
                }
            }
        }
        return start >= 0 ? start : genericStart;
    }

    internal override IEnumerable<ErrorEvent> GetCustomWarningMessages(LogData logData, AgentData agentData, CombatData combatData, EvtcVersionEvent evtcVersion)
    {
        var res = base.GetCustomWarningMessages(logData, agentData, combatData, evtcVersion);
        if (!logData.IsCM)
        {
            return res.Concat(new ErrorEvent("Missing outgoing Saul damage due to % based damage").ToEnumerable());
        }
        return res;
    }

    internal static bool HandleDemonicBonds(AgentData agentData, List<CombatItem> combatData)
    {
        var maxHPUpdates = combatData.Where(x => MaxHealthUpdateEvent.GetMaxHealth(x) == 239040 && x.IsStateChange == StateChange.MaxHealthUpdate).ToList();
        var demonicBonds = maxHPUpdates.Select(x => agentData.GetAgent(x.SrcAgent, x.Time)).Distinct().Where(x => x.Type == AgentItem.AgentType.Gadget);
        bool hasBonds = false;
        var attackTargetEvents = combatData.Where(x => x.IsStateChange == StateChange.AttackTarget);
        foreach (AgentItem demonicBond in demonicBonds)
        {
            hasBonds = true;
            demonicBond.OverrideID(TargetID.DemonicBond, agentData);
            demonicBond.OverrideType(AgentItem.AgentType.NPC, agentData);
            foreach (var atAgent in attackTargetEvents.Where(x => x.DstMatchesAgent(demonicBond)).Select(x => agentData.GetAgent(x.SrcAgent, x.Time)))
            {
                atAgent.OverrideID(TargetID.DemonicBondAttackTarget, agentData);
            }
        }
        return hasBonds;
    }

    internal static void HandleShackledPrisoners(AgentData agentData, List<CombatItem> combatData)
    {
        var shackledPrisonerMaxHPs = combatData.Where(x => MaxHealthUpdateEvent.GetMaxHealth(x) == 1000980 && x.IsStateChange == StateChange.MaxHealthUpdate);
        foreach(var shackledPrisonerMaxHP in shackledPrisonerMaxHPs)
        {
            AgentItem shackledPrisoner = agentData.GetAgent(shackledPrisonerMaxHP.SrcAgent, shackledPrisonerMaxHP.Time);
            if (shackledPrisoner.ID > 0) // sanity check against unknown
            {
                shackledPrisoner.OverrideID(TargetID.ShackledPrisoner, agentData);
                shackledPrisoner.OverrideType(AgentItem.AgentType.NPC, agentData);
            }
        }
    }

    internal static void RenameTargetSauls(IReadOnlyList<SingleActor> targets)
    {
        foreach (SingleActor target in targets)
        {
            if (target.IsSpecies(TargetID.Thief) || target.IsSpecies(TargetID.Drunkard) || target.IsSpecies(TargetID.Gambler))
            {
                string name = (target.IsSpecies(TargetID.Thief) ? "Thief" : (target.IsSpecies(TargetID.Drunkard) ? "Drunkard" : (target.IsSpecies(TargetID.Gambler) ? "Gambler" : "")));
                target.OverrideName(name);
            }
        }
    }

    internal static (AgentItem? deimosStruct, HashSet<AgentItem> gadgetAgents, long deimos10PercentTargetable, long notTargetable) FindDeimos10PercentBodyStructWithAttackTargets(SingleActor deimos, LogData logData, AgentData agentData, List<CombatItem> combatData, IEnumerable<AttackTargetEvent> attackTargetEvents, IEnumerable<TargetableEvent> targetableEvents)
    { 
        var firstTargetable = targetableEvents.FirstOrDefault(x => x.Time >= deimos.FirstAware && x.Targetable);
        var gadgetsAgents = new HashSet<AgentItem>();
        if (firstTargetable != null)
        {
            var attackTarget = attackTargetEvents.FirstOrDefault(x => x.AttackTarget.Is(firstTargetable.Src));
            if (attackTarget != null && attackTarget.Src.Type == AgentItem.AgentType.Gadget)
            {
                attackTarget.AttackTarget.OverrideID(TargetID.DeimosAttackTarget, agentData);
                var bodyStruct = attackTarget.Src;
                bodyStruct.OverrideID(TargetID.DeimosBodyStruct, agentData);
                gadgetsAgents.Add(bodyStruct);
                long targetableTime = firstTargetable.Time;

                var notTargetable = targetableEvents.FirstOrDefault(x => x.Time >= firstTargetable.Time && x.Src.Is(firstTargetable.Src) && !x.Targetable);
                long upperThreshold = notTargetable != null ? notTargetable.Time : logData.EvtcLogEnd;
                var armStructs = combatData.Where(x => x.Time >= targetableTime && x.Time <= upperThreshold && x.IsDamage() && (x.SkillID == DemonicShockWaveRight || x.SkillID == DemonicShockWaveCenter || x.SkillID == DemonicShockWaveLeft) && x.SrcAgent != 0 && x.SrcInstid != 0).Select(x => agentData.GetAgent(x.SrcAgent, x.Time));
                gadgetsAgents.UnionWith(armStructs);
                foreach (var armStruct in gadgetsAgents)
                {
                    if (!armStruct.IsSpecies(TargetID.DeimosBodyStruct))
                    {
                        bodyStruct.OverrideID(TargetID.DeimosArmStruct, agentData);
                    }
                }
                return (bodyStruct, gadgetsAgents, targetableTime, upperThreshold);
            }
        }
        return (null, gadgetsAgents, long.MaxValue, deimos.LastAware);
    }

    internal static void HandleDeimosAndItsGadgets(SingleActor deimos, AgentItem? deimosStructBody, HashSet<AgentItem> gadgetAgents, AgentData agentData, List<CombatItem> combatData, IReadOnlyDictionary<uint, ExtensionHandler> extensions, long structStartTime, long lastAware)
    {
        // invul correction
        CombatItem? invulApp = combatData.FirstOrDefault(x => x.DstMatchesAgent(deimos.AgentItem) && x.IsBuffApply() && x.SkillID == Determined762);
        invulApp?.OverrideValue((int)(deimos.LastAware - invulApp.Time));
        deimos.OverrideName("Deimos");
        deimos.AgentItem.OverrideAwareTimes(deimos.FirstAware, lastAware);
        if (deimosStructBody != null)
        {
            deimos.AgentItem.AddMergeFrom(deimos.AgentItem, deimos.FirstAware, structStartTime);
            MergeWithGadgets(deimos.AgentItem, structStartTime, gadgetAgents, deimosStructBody, combatData, agentData, extensions);
            // Add custom spawn event
            combatData.Add(new CombatItem(structStartTime, deimos.AgentItem.Agent, 0, 0, 0, 0, 0, deimos.AgentItem.InstID, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6, 0, 0, 0, 0));
            combatData.SortByTime();
        }
    }

    internal override void EIEvtcParse(ulong gw2Build, EvtcVersionEvent evtcVersion, LogData logData, AgentData agentData, List<CombatItem> combatData, IReadOnlyDictionary<uint, ExtensionHandler> extensions)
    {
        bool needsDummy = _hasPreEvent && !HandleDemonicBonds(agentData, combatData);
        HandleShackledPrisoners(agentData, combatData);
        if (_hasPreEvent && needsDummy)
        {
            agentData.AddCustomNPCAgent(logData.LogStart, logData.LogEnd, "Deimos Pre Event", Spec.NPC, TargetID.DummyTarget, true);
        }
        base.EIEvtcParse(gw2Build, evtcVersion, logData, agentData, combatData, extensions);
        // Find target
        SingleActor deimos = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.Deimos)) ?? throw new MissingKeyActorsException("Deimos not found");
        // Deimos gadgets via attack targets
        var attackTargetEvents = combatData.Where(x => x.IsStateChange == StateChange.AttackTarget).Select(x => new AttackTargetEvent(x, agentData)).Where(x => !x.AttackTarget.IsSpecies(TargetID.DemonicBondAttackTarget));
        var targetableEvents = new List<TargetableEvent>();
        foreach (var attackTarget in attackTargetEvents)
        {
            targetableEvents.AddRange(attackTarget.GetTargetableEvents(combatData, agentData));
        }
        targetableEvents.SortByTime();
        (AgentItem? deimosStructBody, HashSet<AgentItem> gadgetAgents, long deimos10PercentTargetable, _) = FindDeimos10PercentBodyStructWithAttackTargets(deimos, logData, agentData, combatData, attackTargetEvents, targetableEvents);
        long deimos10PercentTime = long.MaxValue;
        if (deimosStructBody != null)
        {
            deimos10PercentTime = deimos10PercentTargetable;
        } 
        else
        {
            // Deimos gadgets via legacy, when attack targets fail
            CombatItem? armDeimosDamageEvent = combatData.FirstOrDefault(x => x.Time >= deimos.LastAware && (x.SkillID == DemonicShockWaveRight || x.SkillID == DemonicShockWaveCenter || x.SkillID == DemonicShockWaveLeft) && x.IsDamage());
            if (armDeimosDamageEvent != null)
            {
                var deimosGadgets = agentData.GetAgentByType(AgentItem.AgentType.Gadget).Where(x => x.Name.Contains("Deimos") && x.LastAware > armDeimosDamageEvent.Time);
                if (deimosGadgets.Any())
                {
                    deimos10PercentTargetable = deimosGadgets.Max(x => x.FirstAware);
                    gadgetAgents = [.. deimosGadgets];
                    deimosStructBody = gadgetAgents.FirstOrDefault(agent => !combatData.Any(evt => (evt.SkillID == DemonicShockWaveRight || evt.SkillID == DemonicShockWaveCenter || evt.SkillID == DemonicShockWaveLeft) && evt.IsDamage() && evt.SrcMatchesAgent(agent)));
                    deimos10PercentTime = (deimos10PercentTargetable >= deimos.LastAware ? deimos10PercentTargetable : deimos.LastAware);
                }
            }
        }
        //
        HandleDeimosAndItsGadgets(deimos, deimosStructBody, gadgetAgents, agentData, combatData, extensions, deimos10PercentTime, logData.LogEnd);
        RenameTargetSauls(Targets);
    }

    internal override LogData.LogStartStatus GetLogStartStatus(CombatData combatData, AgentData agentData, LogData logData)
    {
        // We expect pre event with logs with LogStartNPCUpdate events
        if (!_hasPreEvent && combatData.GetLogNPCUpdateEvents().Any())
        {
            return LogData.LogStartStatus.NoPreEvent;
        }
        else
        {
            return LogData.LogStartStatus.Normal;
        }
    }

    private static long GetMainFightStart(ParsedEvtcLog log, AgentItem deimos, long start)
    {
        var deimosMainFightStart = log.CombatData.GetBuffRemoveAllDataByIDByDst(SpawnProtection, deimos).FirstOrDefault();
        if (deimosMainFightStart != null && deimosMainFightStart.Time - start > PreEventConsiderationConstant)
        {
            return deimosMainFightStart.Time;
        }
        return start;
    }

    internal static List<PhaseData> ComputePhases(ParsedEvtcLog log, SingleActor deimos, IReadOnlyList<SingleActor> targets, EncounterPhaseData encounterPhase, bool requirePhases)
    {
        if (!requirePhases)
        {
            return [];
        }
        var phases = new List<PhaseData>(10);
        var fullFight = encounterPhase;
        PhaseData phase100to0 = fullFight;
        if (log.CombatData.GetLogNPCUpdateEvents().Count > 0 && encounterPhase.StartStatus == LogData.LogStartStatus.Normal)
        {
            var deimosMainFightStart = GetMainFightStart(log, deimos.AgentItem, encounterPhase.Start);
            var phasePreEvent = new SubPhasePhaseData(0, deimosMainFightStart, "Pre Event");
            phasePreEvent.AddParentPhase(fullFight);
            phasePreEvent.AddTargets(targets.Where(x => x.IsSpecies(TargetID.DemonicBond)), log);
            if (phasePreEvent.Targets.Count == 0)
            {
                phasePreEvent.AddTarget(targets.FirstOrDefault(x => x.IsSpecies(TargetID.DummyTarget)), log);
            }
            phases.Add(phasePreEvent);

            phase100to0 = new SubPhasePhaseData(deimosMainFightStart, encounterPhase.End, "Main Fight");
            phase100to0.AddParentPhase(fullFight);
            phase100to0.AddTarget(deimos, log);
            phase100to0.AddTargets(targets.Where(x => x.IsAnySpecies([TargetID.Drunkard, TargetID.Gambler, TargetID.Thief])), log, PhaseData.TargetPriority.NonBlocking);
            phases.Add(phase100to0);
        }
        (var phase100to10, var phase10to0) = AddBossPhases(phases, log, deimos, phase100to0, encounterPhase);
        AddAddPhases(phases, targets, log, deimos, encounterPhase);
        AddBurstPhases(phases, log, deimos, [phase100to0, phase100to10, phase10to0], encounterPhase);
        return phases;
    }
    internal override List<PhaseData> GetPhases(ParsedEvtcLog log, bool requirePhases)
    {
        List<PhaseData> phases = GetInitialPhase(log);
        SingleActor mainTarget = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.Deimos)) ?? throw new MissingKeyActorsException("Deimos not found");
        phases[0].AddTarget(mainTarget, log);
        phases[0].AddTargets(Targets.Where(x => x.IsSpecies(TargetID.DemonicBond)), log, PhaseData.TargetPriority.Blocking);
        phases[0].AddTargets(Targets.Where(x => x.IsAnySpecies([TargetID.Drunkard, TargetID.Gambler, TargetID.Thief])), log, PhaseData.TargetPriority.NonBlocking);
        phases.AddRange(ComputePhases(log, mainTarget, Targets, (EncounterPhaseData)phases[0], requirePhases));

        return phases;
    }

    private static (PhaseData, PhaseData?) AddBossPhases(List<PhaseData> phases, ParsedEvtcLog log, SingleActor deimos, PhaseData mainFightPhase, PhaseData encounterPhase)
    {
        // Determined + additional data on inst change
        BuffEvent? invulDei = log.CombatData.GetBuffDataByIDByDst(Determined762, deimos.AgentItem).FirstOrDefault(x => x is BuffApplyEvent);
        var phase100to10 = mainFightPhase;
        PhaseData? phase10to0 = null;
        var percent10Start = deimos.AgentItem.Merges.FirstOrNull((in AgentItem.MergedAgentItem x) => x.Merged.Is(deimos.AgentItem));
        if (invulDei != null || percent10Start != null)
        {
            long percent10StartTime = percent10Start.HasValue ? percent10Start.Value.MergeEnd : long.MaxValue;
            long npcDeimosEnd = percent10StartTime;
            var mainDeimosPhaseName = "100% - 10%";
            if (invulDei != null)
            {
                npcDeimosEnd = invulDei.Time;
            }
            var mainFightStart = GetMainFightStart(log, deimos.AgentItem, encounterPhase.Start);
            phase100to10 = new SubPhasePhaseData(mainFightStart, npcDeimosEnd, mainDeimosPhaseName);
            phase100to10.AddTarget(deimos, log);
            phase100to10.AddParentPhase(mainFightPhase);
            phases.Add(phase100to10);

            if (encounterPhase.End - percent10StartTime > PhaseTimeLimit)
            {
                phase10to0 = new SubPhasePhaseData(percent10StartTime, encounterPhase.End, "10% - 0%");
                phase10to0.AddTarget(deimos, log);
                phase10to0.AddParentPhase(mainFightPhase);
                phases.Add(phase10to0);
            }
            //mainTarget.AddCustomCastLog(end, -6, (int)(start - end), ParseEnum.Activation.None, (int)(start - end), ParseEnum.Activation.None, log);
        }

        return (phase100to10, phase10to0);
    }

    private static void AddAddPhases(List<PhaseData> phases, IReadOnlyList<SingleActor> targets, ParsedEvtcLog log, SingleActor mainTarget, EncounterPhaseData encounterPhase)
    {
        foreach (SingleActor target in targets)
        {
            if (target.IsSpecies(TargetID.Thief) || target.IsSpecies(TargetID.Drunkard) || target.IsSpecies(TargetID.Gambler))
            {
                var addPhase = new SubPhasePhaseData(target.FirstAware - 1000, Math.Min(target.LastAware + 1000, encounterPhase.End));
                switch(target.ID)
                {
                    case (int)TargetID.Thief:
                        addPhase.Name = "Thief";
                        break;
                    case (int)TargetID.Drunkard:
                        addPhase.Name = "Drunkard";
                        break;
                    case (int)TargetID.Gambler:
                        addPhase.Name = "Gambler";
                        break;
                }
                addPhase.AddTarget(target, log);
                addPhase.OverrideTimes(log);
                // override first then add Deimos so that it does not disturb the override process
                addPhase.AddTarget(mainTarget, log);
                phases.Add(addPhase);
            }
        }
    }

    private static void AddBurstPhases(List<PhaseData> phases, ParsedEvtcLog log, SingleActor mainTarget, List<PhaseData> parentPhases, EncounterPhaseData encounterPhase)
    {
        var signets = mainTarget.GetBuffStatus(log, UnnaturalSignet).Where(x => x.Value > 0);
        int burstID = 1;
        foreach (Segment signet in signets)
        {
            long sigStart = Math.Max(signet.Start, encounterPhase.Start);
            long sigEnd = Math.Min(signet.End, encounterPhase.End);
            var burstPhase = new SubPhasePhaseData(sigStart, sigEnd, "Burst " + burstID++);
            burstPhase.AddTarget(mainTarget, log);
            burstPhase.AddParentPhases(parentPhases);
            phases.Add(burstPhase);
        }
    }

    internal override IReadOnlyList<TargetID>  GetTargetsIDs()
    {
        return
        [
            TargetID.Deimos,
            TargetID.DummyTarget,
            TargetID.Thief,
            TargetID.Gambler,
            TargetID.Drunkard,
            TargetID.DemonicBond
        ];
    }

    internal override IReadOnlyList<TargetID> GetTrashMobsIDs()
    {
        return
        [
            TargetID.GamblerClones,
            TargetID.GamblerReal,
            TargetID.Greed,
            TargetID.Pride,
            TargetID.Oil,
            TargetID.Tear,
            TargetID.Hands
        ];
    }

    internal override void ComputeNPCCombatReplayActors(NPC target, ParsedEvtcLog log, CombatReplay replay)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputeNPCCombatReplayActors(target, log, replay);
        }
        long castDuration;
        (long start, long end) lifespan = (replay.TimeOffsets.start, replay.TimeOffsets.end);

        switch (target.ID)
        {
            case (int)TargetID.Deimos:
                var hasSaul = log.AgentData.GetNPCsByID(TargetID.Saul).Any(x => x.InAwareTimes(target.FirstAware, target.FirstAware + 20000));
                foreach (CastEvent cast in target.GetAnimatedCastEvents(log))
                {
                    switch (cast.SkillID)
                    {
                        // Mind Crush
                        case MindCrush:
                            castDuration = 5000;
                            lifespan = (cast.Time, cast.Time + castDuration);
                            replay.Decorations.Add(new OverheadProgressBarDecoration(CombatReplayOverheadProgressBarMajorSizeInPixel, lifespan, Colors.Red, 0.6, Colors.Black, 0.2, [(lifespan.start, 0), (lifespan.end, 100)], new AgentConnector(target))
                                .UsingRotationConnector(new AngleConnector(180)));
                            if (hasSaul)
                            {
                                replay.Decorations.Add(new CircleDecoration(180, lifespan, Colors.Blue, 0.3, new PositionConnector(new Vector3(-8421.818f, 3091.72949f, -9.818082e8f))));
                            }
                            break;
                        // Annihilate - Slices
                        case Annihilate1:
                        case Annihilate2:
                            long start = cast.Time;
                            long end = start + 2400;
                            int delay = 1000;
                            int duration = 120;
                            if (target.TryGetCurrentFacingDirection(log, start, out var facing))
                            {
                                float initialAngle = facing.GetRoundedZRotationDeg();
                                var connector = new AgentConnector(target);
                                for (int i = 0; i < 6; i++)
                                {
                                    var rotationConnector1 = new AngleConnector(initialAngle + i * 360 / 10);
                                    replay.Decorations.Add(new PieDecoration(900, 360 / 10, (start + delay + i * duration, end + i * duration), Colors.Yellow, 0.5, connector).UsingRotationConnector(rotationConnector1));
                                    replay.Decorations.Add(new PieDecoration(900, 360 / 10, (start + delay + i * duration, end + i * 120), Colors.LightOrange, 0.5, connector).UsingFilled(false).UsingRotationConnector(rotationConnector1));
                                    if (i % 5 != 0)
                                    {
                                        var rotationConnector2 = new AngleConnector(initialAngle - i * 360 / 10);
                                        replay.Decorations.Add(new PieDecoration(900, 360 / 10, (start + delay + i * duration, end + i * 120), Colors.Yellow, 0.5, connector).UsingRotationConnector(rotationConnector2));
                                        replay.Decorations.Add(new PieDecoration(900, 360 / 10, (start + delay + i * duration, end + i * 120), Colors.LightOrange, 0.5, connector).UsingFilled(false).UsingRotationConnector(rotationConnector2));
                                    }
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }

                // Unnatural Signet - Overhead
                var signets = target.GetBuffStatus(log, UnnaturalSignet).Where(x => x.Value > 0);
                replay.Decorations.AddOverheadIcons(signets, target, BuffImages.UnnaturalSignet);
                break;
            case (int)TargetID.Gambler:
            case (int)TargetID.Thief:
            case (int)TargetID.Drunkard:
                break;
            case (int)TargetID.GamblerClones:
            case (int)TargetID.GamblerReal:
            case (int)TargetID.Greed:
            case (int)TargetID.Pride:
            case (int)TargetID.Tear:
                break;
            case (int)TargetID.Hands:
                replay.Decorations.Add(new CircleDecoration(90, lifespan, Colors.Red, 0.2, new AgentConnector(target)));
                break;
            case (int)TargetID.Oil:
                if (!log.CombatData.HasEffectData)
                {
                    int delayOil = 3000;
                    (long start, long end) lifespanWarning = (lifespan.start, lifespan.start + delayOil);
                    (long start, long end) lifespanOil = (lifespanWarning.start, lifespan.end);
                    replay.Decorations.AddWithGrowing(new CircleDecoration(200, lifespanWarning, Colors.LightOrange, 0.5, new AgentConnector(target)), lifespanWarning.end);
                    replay.Decorations.AddWithBorder(new CircleDecoration(200, lifespanOil, Colors.Black, 0.5, new AgentConnector(target)), Colors.Red, 0.2);
                }
                break;
            case (int)TargetID.ShackledPrisoner:
                var Sauls = log.AgentData.GetNPCsByID(TargetID.Saul).Where(x => x.InAwareTimes(target.AgentItem));
                foreach (var Saul in Sauls)
                {
                    replay.Hidden.Add(new Segment(Saul.FirstAware, Saul.LastAware));
                }
                break;
            case (int)TargetID.DemonicBond:
                var demonicCenter = new Vector3(-8092.57f, 4176.98f, 0);
                var arenaCenter = new Vector3(-8411.66f, 3089.07f, -4109.54f);
                float diffX = 0;
                float diffY = 0;
                var pos = replay.PolledPositions[0].XYZ;
                if (pos.X > demonicCenter.X)
                {
                    if (pos.Y > demonicCenter.Y)
                    {
                        // top
                        diffX = 55;
                        diffY = 1080;
                    }
                    else
                    {
                        // right
                        diffX = 1115;
                        diffY = -35;
                    }
                }
                else
                {
                    if (pos.Y > demonicCenter.Y)
                    {
                        // left 
                        diffX = -1100;
                        diffY = 40;
                    }
                    else
                    {
                        // bottom
                        diffX = -38;
                        diffY = -1130;
                    }
                }
                var arenaPos = arenaCenter + new Vector3(diffX, diffY, 0);

                var attackTargetEvent = log.CombatData.GetAttackTargetEventsBySrc(target.AgentItem).FirstOrDefault();
                long hiddenStart = target.FirstAware;
                if (attackTargetEvent != null)
                {
                    var attackTarget = attackTargetEvent.AttackTarget;
                    var targetableEvents = attackTargetEvent.GetTargetableEvents(log.CombatData);
                    long lineStart = 0;
                    foreach (var targetableEvent in targetableEvents)
                    {
                        if (targetableEvent.Targetable)
                        {
                            replay.Hidden.Add(new Segment(hiddenStart, targetableEvent.Time));
                            hiddenStart = target.LastAware;
                            lineStart = targetableEvent.Time;
                        } 
                        else
                        {
                            if (targetableEvent.Time > hiddenStart)
                            {
                                replay.Hidden.Add(new Segment(hiddenStart , targetableEvent.Time));
                            } else
                            {
                                replay.Decorations.Add(new LineDecoration((lineStart, targetableEvent.Time), Colors.Teal, 0.4, new AgentConnector(target), new PositionConnector(demonicCenter)));
                                replay.Decorations.Add(new LineDecoration((lineStart, targetableEvent.Time), Colors.Teal, 0.4, new PositionConnector(arenaCenter), new PositionConnector(arenaPos)));
                            }
                            hiddenStart = targetableEvent.Time;
                        }
                    }
                }
                replay.Hidden.Add(new Segment(hiddenStart, target.LastAware));
                break;
            default:
                break;
        }

    }

    internal override void ComputePlayerCombatReplayActors(PlayerActor p, ParsedEvtcLog log, CombatReplay replay)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputePlayerCombatReplayActors(p, log, replay);
        }
        // teleport zone
        var tpDeimos = p.GetBuffStatus(log, DeimosSelectedByGreen).Where(x => x.Value > 0);
        foreach (Segment seg in tpDeimos)
        {
            var circle = new CircleDecoration(180, seg, "rgba(0, 150, 0, 0.3)", new AgentConnector(p));
            replay.Decorations.AddWithGrowing(circle, seg.End);
        }
        // Tear Instability
        IEnumerable<Segment> tearInstabs = p.GetBuffStatus(log, TearInstability).Where(x => x.Value > 0);
        replay.Decorations.AddOverheadIcons(tearInstabs, p, ParserIcons.TearInstabilityOverhead);
    }

    internal override void ComputeEnvironmentCombatReplayDecorations(ParsedEvtcLog log, CombatReplayDecorationContainer environmentDecorations)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputeEnvironmentCombatReplayDecorations(log, environmentDecorations);
        }

        // Rapid Decay - Orange Indicator - Oil
        if (log.CombatData.TryGetEffectEventsByGUID(EffectGUIDs.DeimosRapidDecayIndicator, out var rapidDecayIndicator))
        {
            foreach (EffectEvent effect in rapidDecayIndicator)
            {
                // Logged duration is 3500, replacing to 3000 for better visual with the Oil.
                (long start, long end) lifespan = effect.ComputeLifespan(log, 3000);
                environmentDecorations.AddWithGrowing(new CircleDecoration(200, lifespan, Colors.LightOrange, 0.2, new PositionConnector(effect.Position)), lifespan.end);
            }
        }

        // Rapid Decay - Black Indicator - Oil
        AddRapidDecayDecoration(log, EffectGUIDs.DeimosRapidDecay200Radius, 200, environmentDecorations);
        AddRapidDecayDecoration(log, EffectGUIDs.DeimosRapidDecay300Radius, 300, environmentDecorations);
        AddRapidDecayDecoration(log, EffectGUIDs.DeimosRapidDecay400Radius, 400, environmentDecorations);
        AddRapidDecayDecoration(log, EffectGUIDs.DeimosRapidDecay500Radius, 500, environmentDecorations);
        AddRapidDecayDecoration(log, EffectGUIDs.DeimosRapidDecay600Radius, 600, environmentDecorations);
        AddRapidDecayDecoration(log, EffectGUIDs.DeimosRapidDecay700Radius, 700, environmentDecorations);
        AddRapidDecayDecoration(log, EffectGUIDs.DeimosRapidDecay800Radius, 800, environmentDecorations);
        AddRapidDecayDecoration(log, EffectGUIDs.DeimosRapidDecay900Radius, 900, environmentDecorations);
        AddRapidDecayDecoration(log, EffectGUIDs.DeimosRapidDecay1000Radius, 1000, environmentDecorations);

        // This causes a discreet rendering + ghosting effect when Deimos pulls them in
        // Soul Feast - Hands
        /*if (log.CombatData.TryGetEffectEventsByGUID(EffectGUIDs.DeimosSoulFeast, out var soulFeasts))
        {
            foreach (EffectEvent effect in soulFeasts)
            {
                (long start, long end) lifespan = effect.ComputeLifespan(log, 2000);
                environmentDecorations.Add(new CircleDecoration(90, lifespan, Colors.Red, 0.2, new PositionConnector(effect.Position)));
            }
        }
        */
    }
    internal override void SetInstanceBuffs(ParsedEvtcLog log, List<InstanceBuff> instanceBuffs)
    {
        if (!log.LogData.IsInstance)
        {
            base.SetInstanceBuffs(log, instanceBuffs);
        }
    }

    internal static void AdjustDeimosHP(SingleActor deimos, bool isCM)
    {
        // Deimos gains additional health during the last 10% so the max-health needs to be corrected
        // done here because this method will get called during the creation of the ParsedEvtcLog and the ParsedEvtcLog should contain complete and correct values after creation
        if (isCM)
        {
            deimos.SetManualHealth(42804900, new List<(long hpValue, double percent)>()
                {
                    (42000000 , 100),
                    (50049000, 10)
                });
        }
        else
        {
            deimos.SetManualHealth(37388210, new List<(long hpValue, double percent)>()
                {
                    (35981456 , 100),
                    (50049000, 10)
                });
        }
    }

    internal override LogData.LogMode GetLogMode(CombatData combatData, AgentData agentData, LogData logData)
    {
        SingleActor target = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.Deimos)) ?? throw new MissingKeyActorsException("Deimos not found");
        LogData.LogMode cmStatus = (target.GetHealth(combatData) > 40e6) ? LogData.LogMode.CM : LogData.LogMode.Normal;
        AdjustDeimosHP(target, cmStatus == LogData.LogMode.CM);

        return cmStatus;
    }

    /// <summary>
    /// Adds Rapid Decay oils to the combat replay.<br></br>
    /// Each effect lasts 1500ms, a new effect appears every 1000s, giving a 500ms overlap for a pulsating visual effect.
    /// Duration overriden to 1000ms to remove the pulse effect, a position based check makes sure the "pulses" remain properly connected.
    /// </summary>
    /// <param name="log">The log.</param>
    /// <param name="guid">Effect GUID of the different oil sizes.</param>
    /// <param name="radius">Radius of the oil, 200 to 1000.</param>
    private static void AddRapidDecayDecoration(ParsedEvtcLog log, GUID guid, uint radius, CombatReplayDecorationContainer environmentDecorations)
    {
        if (log.CombatData.TryGetEffectEventsByGUID(guid, out var rapidDecay))
        {
            var positionDict = new Dictionary<float, Dictionary<float, (long start, long end)>>();
            foreach (EffectEvent effect in rapidDecay)
            {
                (long start, long end) lifespan = effect.ComputeLifespan(log, 1000);
                if (positionDict.TryGetValue(effect.Position.X, out var yDict))
                {
                    if (yDict.TryGetValue(effect.Position.Y, out var previousLifeSpan))
                    {
                        if (Math.Abs(previousLifeSpan.end - lifespan.start) < 50)
                        {
                            lifespan.start = previousLifeSpan.end + 1;
                        }
                    } 
                    yDict[effect.Position.Y] = lifespan;
                } 
                else
                {
                    positionDict[effect.Position.X] = new Dictionary<float, (long start, long end)>
                    {
                        [effect.Position.Y] = lifespan
                    };
                }
                environmentDecorations.AddWithBorder(new CircleDecoration(radius, lifespan, Colors.Black, 0.2, new PositionConnector(effect.Position)), Colors.Red, 0.2);
            }
        }
    }
}

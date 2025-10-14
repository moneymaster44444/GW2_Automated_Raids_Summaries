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
using static GW2EIEvtcParser.ParserHelpers.LogImages;
using static GW2EIEvtcParser.SkillIDs;
using static GW2EIEvtcParser.SpeciesIDs;

namespace GW2EIEvtcParser.LogLogic;

internal class ConjuredAmalgamate : MythwrightGambit
{

    internal readonly MechanicGroup Mechanics = new MechanicGroup([
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic(Pulverize, new MechanicPlotlySetting(Symbols.Square,Colors.LightOrange), "Arm Slam", "Pulverize (Arm Slam)","Arm Slam", 0)
                    .WithStabilitySubMechanic(
                        new PlayerDstHealthDamageHitMechanic(Pulverize, new MechanicPlotlySetting(Symbols.SquareOpen,Colors.LightOrange), "Stab.Slam", "Pulverize (Arm Slam) while affected by stability","Stabilized Arm Slam", 0),
                        true
                    ),
            ]),
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic(JunkAbsorption, new MechanicPlotlySetting(Symbols.CircleOpen,Colors.Purple), "Balls", "Junk Absorption (Purple Balls during collect)","Purple Balls", 0),
                new PlayerDstHealthDamageHitMechanic([JunkFall1, JunkFall2], new MechanicPlotlySetting(Symbols.CircleOpen,Colors.Pink), "Junk", "Junk Fall (Falling Debris)","Junk Fall", 0),
                new PlayerDstHealthDamageHitMechanic(JunkTorrent, new MechanicPlotlySetting(Symbols.SquareOpen,Colors.Red), "Wall", "Junk Torrent (Moving Wall)","Junk Torrent (Wall)", 0)
                    .UsingChecker((de,log) => de.HealthDamage > 0),
            ]),
            new PlayerDstHealthDamageHitMechanic(RupturedGround, new MechanicPlotlySetting(Symbols.SquareOpen,Colors.Teal), "Ground", "Ruptured Ground (Relics after Junk Wall)","Ruptured Ground", 0).UsingChecker((de,log) => de.HealthDamage > 0),
            new PlayerDstHealthDamageHitMechanic(Tremor, new MechanicPlotlySetting(Symbols.CircleOpen,Colors.Red), "Tremor", "Tremor (Field adjacent to Arm Slam)","Near Arm Slam", 0).UsingChecker((de,log) => de.HealthDamage > 0),
            new MechanicGroup([
                new PlayerCastStartMechanic(ConjuredSlashSAK, new MechanicPlotlySetting(Symbols.Square,Colors.Red), "Sword.Cst", "Conjured Slash (Special action sword)","Sword Cast", 0),
                new PlayerCastStartMechanic(ConjuredProtectionSAK, new MechanicPlotlySetting(Symbols.Square,Colors.Green), "Shield.Cst", "Conjured Protection (Special action shield)","Shield Cast", 0),
            ]),
            new MechanicGroup([
                new PlayerDstBuffApplyMechanic(GreatswordPower, new MechanicPlotlySetting(Symbols.DiamondTall,Colors.Red), "Sword.C", "Collected Sword","Sword Collect", 50),
                new PlayerDstBuffApplyMechanic(ConjuredShield, new MechanicPlotlySetting(Symbols.DiamondTall,Colors.Green), "Shield.C", "Collected Shield","Shield Collect", 50),
            ]),
            new MechanicGroup([
                new EnemyDstBuffApplyMechanic(AugmentedPower, new MechanicPlotlySetting(Symbols.BowtieOpen,Colors.Red), "Augmented Power", "Augmented Power","Augmented Power", 50),
                new EnemyDstBuffApplyMechanic(ShieldedCA, new MechanicPlotlySetting(Symbols.BowtieOpen,Colors.Green), "Shielded", "Shielded","Shielded", 50),
            ]),
        ]);
    public ConjuredAmalgamate(int triggerID) : base((int)TargetID.ConjuredAmalgamate)
    {
        MechanicList.Add(Mechanics);
        Extension = "ca";
        GenericFallBackMethod = FallBackMethod.None;
        Icon = EncounterIconConjuredAmalgamate;
        LogCategoryInformation.InSubCategoryOrder = 0;
        LogID |= 0x000001;
        ChestID = ChestID.CAChest;
    }

    internal override CombatReplayMap GetCombatMapInternal(ParsedEvtcLog log, CombatReplayDecorationContainer arenaDecorations)
    {
        var crMap = new CombatReplayMap(
                        (544, 1000),
                        (-5064, -15030, -2864, -10830));
        AddArenaDecorationsPerEncounter(log, arenaDecorations, LogID, CombatReplayConjuredAmalgamate, crMap);
        return crMap;
    }

    private static readonly Vector3 BodyAttackTargetPos = new(-3325f, -12925f, -2451.05f);
    private static readonly Vector3 LeftArmAttackTargetPosForDamage = new(-4239.84f, -13354f, -2061.94f);
    private static readonly Vector3 LeftArmAttackTargetPosNoDamage = new(-2844.18f, -13942.6f, -2316.01f);
    private static readonly Vector3 RightArmAttackTargetPosForDamage = new (-4321.68f, -12616.5f, -2061.94f);
    private static readonly Vector3 RightArmAttackTargetPosNoDamage = new(-2900.12f, -11787.7f, -2391.01f);

    internal static void HandleCAAgents(AgentData agentData, List<CombatItem> combatData)
    {
        var attackTargetEvents = combatData
            .Where(x => x.IsStateChange == StateChange.AttackTarget)
            .Select(x => new AttackTargetEvent(x, agentData))
            .ToList();
        var positionEvents = combatData.Where(x => x.IsStateChange == StateChange.Position);
        var attackTargetPositions = positionEvents.Where(x => attackTargetEvents.Any(y => x.SrcMatchesAgent(y.AttackTarget)));
        foreach (var positionEvent in attackTargetPositions)
        {
            var position = new PositionEvent(positionEvent, agentData);
            var attackTargetEvent = attackTargetEvents.First(x => position.Src.Is(x.AttackTarget));
            var atAgent = attackTargetEvent.AttackTarget;
            var agent = attackTargetEvent.Src;
            var atPos = position.GetPoint3D();
            if (agent.Type == AgentItem.AgentType.Gadget)
            {
                if ((atPos - BodyAttackTargetPos).Length() < 5)
                {
                    agent.OverrideType(AgentItem.AgentType.NPC, agentData);
                    agent.OverrideID(TargetID.ConjuredAmalgamate, agentData);
                    atAgent.OverrideID(TargetID.CABodyAttackTarget, agentData);
                    atAgent.OverrideType(AgentItem.AgentType.NPC, agentData);
                    atAgent.OverrideHitbox(500, atAgent.HitboxHeight);
                }
                else if ((atPos - LeftArmAttackTargetPosNoDamage).Length() < 5)
                {
                    agent.OverrideType(AgentItem.AgentType.NPC, agentData);
                    agent.OverrideID(TargetID.CALeftArm, agentData);
                }
                else if ((atPos - RightArmAttackTargetPosNoDamage).Length() < 5)
                {
                    agent.OverrideType(AgentItem.AgentType.NPC, agentData);
                    agent.OverrideID(TargetID.CARightArm, agentData);
                }
            }
        }
        foreach (var positionEvent in attackTargetPositions)
        {
            var position = new PositionEvent(positionEvent, agentData);
            var attackTargetEvent = attackTargetEvents.First(x => position.Src.Is(x.AttackTarget));
            var atAgent = attackTargetEvent.AttackTarget;
            var agent = attackTargetEvent.Src;
            var atPos = position.GetPoint3D();
            if (agent.IsSpecies(TargetID.CALeftArm) && (atPos - LeftArmAttackTargetPosForDamage).Length() < 5)
            {
                atAgent.OverrideID(TargetID.CALeftArmAttackTarget, agentData);
                atAgent.OverrideType(AgentItem.AgentType.NPC, agentData);
                atAgent.OverrideHitbox(500, atAgent.HitboxHeight);
            }
            else if (agent.IsSpecies(TargetID.CARightArm) && (atPos - RightArmAttackTargetPosForDamage).Length() < 5)
            {
                atAgent.OverrideID(TargetID.CARightArmAttackTarget, agentData);
                atAgent.OverrideType(AgentItem.AgentType.NPC, agentData);
                atAgent.OverrideHitbox(500, atAgent.HitboxHeight);
            }
        }
    }

    internal override void HandleCriticalAgents(EvtcVersionEvent evtcVersion, LogData logData, AgentData agentData, List<CombatItem> combatData, IReadOnlyDictionary<uint, ExtensionHandler> extensions)
    {
        HandleCAAgents(agentData, combatData);
    }

    internal override long GetLogOffset(EvtcVersionEvent evtcVersion, LogData logData, AgentData agentData, List<CombatItem> combatData)
    {
        // time starts at first smash
        var effectIDToGUIDs = combatData.Where(x => x.IsStateChange == StateChange.IDToGUID);
        if (effectIDToGUIDs.Any())
        {
            CombatItem? armSmashGUID = effectIDToGUIDs.FirstOrDefault(x => new GUID(x.SrcAgent, x.DstAgent) == EffectGUIDs.CAArmSmash);
            if (armSmashGUID != null)
            {
                CombatItem? firstArmSmash = combatData.FirstOrDefault(x => x.IsEffect && x.SkillID == armSmashGUID.SkillID);
                if (firstArmSmash != null)
                {
                    CombatItem? logStartNPCUpdate = combatData.FirstOrDefault(x => x.IsStateChange == StateChange.LogNPCUpdate);
                    if (logStartNPCUpdate != null)
                    {
                        // we couldn't have hit CA before the initial smash
                        return firstArmSmash.Time > GetPostLogStartNPCUpdateDamageEventTime(logData, agentData, combatData, logStartNPCUpdate.Time, agentData.GetNPCsByID(TargetID.ConjuredAmalgamate).FirstOrDefault()) ? logStartNPCUpdate.Time : firstArmSmash.Time;
                    }
                    else
                    {
                        // Before new logging, log would start when everyone in combat + boss in combat or enters combat
                        // as such the first smash can only happen within the first few seconds of the start
                        return firstArmSmash.Time - logData.EvtcLogStart > 6000 ? GetGenericLogOffset(logData) : firstArmSmash.Time;
                    }
                }
            }
        }
        return GetGenericLogOffset(logData);
    }

    internal override LogData.LogStartStatus GetLogStartStatus(CombatData combatData, AgentData agentData, LogData logData)
    {
        // Can be improved
        if (TargetHPPercentUnderThreshold(TargetID.ConjuredAmalgamate, logData.LogStart, combatData, Targets, 90))
        {
            return LogData.LogStartStatus.Late;
        }
        return LogData.LogStartStatus.Normal;
    }

    protected override IReadOnlyList<TargetID> GetSuccessCheckIDs()
    {
        return [];
    }

    internal override IReadOnlyList<TargetID>  GetTargetsIDs()
    {
        return
        [
            TargetID.ConjuredAmalgamate,
            TargetID.CARightArm,
            TargetID.CALeftArm
        ];
    }

    internal override IReadOnlyList<TargetID> GetTrashMobsIDs()
    {
        return
        [
            TargetID.ConjuredGreatsword,
            TargetID.ConjuredShield,
            TargetID.CALeftArmAttackTarget,
            TargetID.CARightArmAttackTarget,
            TargetID.CABodyAttackTarget,
        ];
    }

    internal override IReadOnlyList<TargetID>  GetFriendlyNPCIDs()
    {
        return
        [
            TargetID.ConjuredPlayerSword
        ];
    }

    internal static AgentItem CreateCustomSwordAgent(LogData logData, AgentData agentData)
    {    
        return agentData.AddCustomNPCAgent(logData.LogStart, logData.LogEnd, "Conjured Sword\0:Conjured Sword\051", ParserHelper.Spec.NPC, TargetID.ConjuredPlayerSword, true);
    }

    internal static void RedirectSwordDamageToSwordAgent(AgentItem sword, List<CombatItem> combatData, IReadOnlyDictionary<uint, ExtensionHandler> extensions)
    {
        foreach (CombatItem c in combatData)
        {
            if (c.IsDamage(extensions) && c.SkillID == ConjuredSlashPlayer)
            {
                c.OverrideSrcAgent(sword);
            }
        }
    }

    internal override void EIEvtcParse(ulong gw2Build, EvtcVersionEvent evtcVersion, LogData logData, AgentData agentData, List<CombatItem> combatData, IReadOnlyDictionary<uint, ExtensionHandler> extensions)
    {
        var sword = CreateCustomSwordAgent(logData, agentData);
        base.EIEvtcParse(gw2Build, evtcVersion, logData, agentData, combatData, extensions);
        RedirectSwordDamageToSwordAgent(sword, combatData, extensions);
    }

    internal override void ComputeNPCCombatReplayActors(NPC target, ParsedEvtcLog log, CombatReplay replay)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputeNPCCombatReplayActors(target, log, replay);
        }
        switch (target.ID)
        {
            case (int)TargetID.ConjuredAmalgamate:
                var shieldCA = target.GetBuffStatus(log, ShieldedCA).Where(x => x.Value > 0);
                uint CAShieldRadius = 800;
                foreach (Segment seg in shieldCA)
                {
                    replay.Decorations.Add(new CircleDecoration(CAShieldRadius, seg, "rgba(0, 150, 255, 0.3)", new AgentConnector(target)));
                }
                break;
            case (int)TargetID.CALeftArm:
            case (int)TargetID.CARightArm:
                break;
            case (int)TargetID.CABodyAttackTarget:
                var bodyTargetableEvent = log.CombatData.GetTargetableEventsBySrc(target.AgentItem);
                var body = log.CombatData.GetAttackTargetEventByAttackTarget(target.AgentItem)?.Src;
                var bodyInvulStatus = body?.GetBuffStatus(log, CAInvul).Where(x => x.Value == 0) ?? [];
                var bodyAtHideStart = log.LogData.LogStart;
                foreach (var noInvul in bodyInvulStatus)
                {
                    replay.Hidden.Add(new Segment(bodyAtHideStart, noInvul.Start));
                    bodyAtHideStart = noInvul.End;
                }
                replay.Hidden.Add(new Segment(bodyAtHideStart, log.LogData.LogEnd));
                break;
            case (int)TargetID.CALeftArmAttackTarget:
            case (int)TargetID.CARightArmAttackTarget:
                var armTargetableEvents = log.CombatData.GetAttackTargetEventByAttackTarget(target.AgentItem)?.GetTargetableEvents(log) ?? [];
                var armAtHideStart = log.LogData.LogStart;
                foreach (var targetable in armTargetableEvents)
                {
                    if (targetable.Targetable)
                    {
                        replay.Hidden.Add(new Segment(armAtHideStart, targetable.Time));
                    }
                    else
                    {
                        armAtHideStart = targetable.Time;
                    }
                }
                replay.Hidden.Add(new Segment(armAtHideStart, log.LogData.LogEnd));
                break;
            case (int)TargetID.ConjuredGreatsword:
                break;
            case (int)TargetID.ConjuredShield:
                var shieldShield = target.GetBuffStatus(log, ShieldedCA).Where(x => x.Value > 0);
                uint ShieldShieldRadius = 100;
                foreach (Segment seg in shieldShield)
                {
                    replay.Decorations.Add(new CircleDecoration(ShieldShieldRadius, seg, "rgba(0, 150, 255, 0.3)", new AgentConnector(target)));
                }
                break;
            default:
                break;
        }
    }

    internal override void CheckSuccess(CombatData combatData, AgentData agentData, LogData logData, IReadOnlyCollection<AgentItem> playerAgents)
    {
        base.CheckSuccess(combatData, agentData, logData, playerAgents);
        if (!logData.Success)
        {
            SingleActor? target = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.ConjuredAmalgamate));
            SingleActor? leftArm = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.CALeftArm));
            SingleActor? rightArm = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.CARightArm));
            if (target == null)
            {
                throw new MissingKeyActorsException("Conjured Amalgamate not found");
            }
            AgentItem? zommoros = agentData.GetNPCsByID(TargetID.ChillZommoros).LastOrDefault();
            if (zommoros == null)
            {
                return;
            }
            SpawnEvent? npcSpawn = combatData.GetSpawnEvents(zommoros).LastOrDefault();
            HealthDamageEvent? lastDamageTaken = combatData.GetDamageTakenData(target.AgentItem).LastOrDefault(x => (x.HealthDamage > 0) && !x.ToFriendly && playerAgents.Any(x.From.IsMasterOrSelf));
            if (lastDamageTaken == null)
            {
                return;
            }
            if (rightArm != null)
            {
                HealthDamageEvent? lastDamageTakenArm = combatData.GetDamageTakenData(rightArm.AgentItem).LastOrDefault(x => (x.HealthDamage > 0) && playerAgents.Any(x.From.IsMasterOrSelf));
                if (lastDamageTakenArm != null)
                {
                    lastDamageTaken = lastDamageTaken.Time > lastDamageTakenArm.Time ? lastDamageTaken : lastDamageTakenArm;
                }
            }
            if (leftArm != null)
            {
                HealthDamageEvent? lastDamageTakenArm = combatData.GetDamageTakenData(leftArm.AgentItem).LastOrDefault(x => (x.HealthDamage > 0) && playerAgents.Any(x.From.IsMasterOrSelf));
                if (lastDamageTakenArm != null)
                {
                    lastDamageTaken = lastDamageTaken.Time > lastDamageTakenArm.Time ? lastDamageTaken : lastDamageTakenArm;
                }
            }
            if (npcSpawn != null)
            {
                logData.SetSuccess(true, lastDamageTaken.Time);
            }
        }
    }

    private static List<long> GetTargetableTimes(ParsedEvtcLog log, SingleActor? target, TargetID atID, long start, long end)
    {
        if (target == null)
        {
            return [];
        }
        var attackTargetEvent = log.CombatData.GetAttackTargetEventsBySrc(target.AgentItem).FirstOrDefault(x => x.AttackTarget.IsSpecies(atID));
        if (attackTargetEvent != null)
        {
            return attackTargetEvent.GetTargetableEvents(log).Where(x => x.Targetable && x.Time >= start && x.Time <= end).Select(x => x.Time).ToList();
        }
        return [];
    }

    internal static List<PhaseData> ComputePhases(ParsedEvtcLog log, SingleActor conjuredAmalgamate, SingleActor? rightArm, SingleActor? leftArm, EncounterPhaseData encounterPhase, bool requirePhases)
    {
        if (!requirePhases)
        {
            return [];
        }
        long start = encounterPhase.Start;
        long end = encounterPhase.End;
        var phases = new List<PhaseData>(5);
        phases.AddRange(GetPhasesByInvul(log, CAInvul, conjuredAmalgamate, true, false, start, end));
        int burnPhase = 0, armPhase = 0;
        for (int i = 0; i < phases.Count; i++)
        {
            string name;
            PhaseData phase = phases[i];
            phase.AddParentPhase(encounterPhase);
            if (i % 2 == 0)
            {
                name = "Arm Phase " + (++armPhase);
            }
            else
            {
                name = "Burn Phase " + (++burnPhase);
                phase.AddTarget(conjuredAmalgamate, log);
            }
            phase.Name = name;
        }
        if (leftArm != null || rightArm != null)
        {
            int leftArmPhase = 0, rightArmPhase = 0, bothArmPhase = 0;
            var targetablesL = GetTargetableTimes(log, leftArm, TargetID.CALeftArmAttackTarget, start, end);
            var targetablesR = GetTargetableTimes(log, rightArm, TargetID.CARightArmAttackTarget, start, end);
            for (int i = 0; i < phases.Count; i++)
            {
                PhaseData phase = phases[i];
                var leftExists = targetablesL.Exists(x => phase.InInterval(x));
                var rightExists = targetablesR.Exists(x => phase.InInterval(x));
                if (phase.Name.Contains("Arm"))
                {
                    if (leftExists && rightExists)
                    {
                        phase.Name = "Both Arms Phase " + (++bothArmPhase);
                        phase.AddTarget(leftArm, log);
                        phase.AddTarget(rightArm, log);
                    }
                    else if (leftExists)
                    {
                        phase.Name = "Left Arm Phase " + (++leftArmPhase);
                        phase.AddTarget(leftArm, log);
                    }
                    else if (rightExists)
                    {
                        phase.Name = "Right Arm Phase " + (++rightArmPhase);
                        phase.AddTarget(rightArm, log);
                    }
                }
                else
                {
                    if (leftExists && rightExists)
                    {
                        phase.AddTarget(leftArm, log, PhaseData.TargetPriority.NonBlocking);
                        phase.AddTarget(rightArm, log, PhaseData.TargetPriority.NonBlocking);
                    }
                    else if (leftExists)
                    {
                        phase.AddTarget(leftArm, log, PhaseData.TargetPriority.NonBlocking);
                    }
                    else if (rightExists)
                    {
                        phase.AddTarget(rightArm, log, PhaseData.TargetPriority.NonBlocking);
                    }
                }
            }
        }
        return phases;
    }
    internal override List<PhaseData> GetPhases(ParsedEvtcLog log, bool requirePhases)
    {
        List<PhaseData> phases = GetInitialPhase(log);
        SingleActor ca = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.ConjuredAmalgamate)) ?? throw new MissingKeyActorsException("Conjured Amalgamate not found");
        SingleActor? leftArm = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.CALeftArm));
        SingleActor? rightArm = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.CARightArm));
        phases[0].AddTarget(ca, log);
        phases[0].AddTarget(leftArm, log, PhaseData.TargetPriority.Blocking);
        phases[0].AddTarget(rightArm, log, PhaseData.TargetPriority.Blocking);
        phases.AddRange(ComputePhases(log, ca, rightArm, leftArm, (EncounterPhaseData)phases[0], requirePhases));      
        return phases;
    }

    internal override void ComputePlayerCombatReplayActors(PlayerActor p, ParsedEvtcLog log, CombatReplay replay)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputePlayerCombatReplayActors(p, log, replay);
        }
        // Conjured Protection - Shield AoE
        var casts = p.GetCastEvents(log);
        var shieldCast = casts.Where(x => x.SkillID == ConjuredProtectionSAK);
        foreach (CastEvent c in shieldCast)
        {
            int duration = 10000;
            uint radius = 300;
            (long start, long end) lifespan = (c.Time, c.Time + duration);
            if (p.TryGetCurrentInterpolatedPosition(log, lifespan.start, out var position))
            {
                var circle = new CircleDecoration(radius, lifespan, Colors.Magenta, 0.2, new PositionConnector(position));
                replay.Decorations.AddWithBorder(circle);
            }
        }
        // Shields and Greatswords Overheads
        replay.Decorations.AddOverheadIcons(p.GetBuffStatus(log, ConjuredShield).Where(x => x.Value > 0), p, ParserIcons.ConjuredShieldEmptyOverhead);
        replay.Decorations.AddOverheadIcons(p.GetBuffStatus(log, GreatswordPower).Where(x => x.Value > 0), p, ParserIcons.GreatswordPowerEmptyOverhead);
    }

    internal override LogData.LogMode GetLogMode(CombatData combatData, AgentData agentData, LogData logData)
    {
        SingleActor target = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.ConjuredAmalgamate)) ?? throw new MissingKeyActorsException("Conjured Amalgamate not found");
        return combatData.GetBuffData(LockedOn).Count > 0 ? LogData.LogMode.CM : LogData.LogMode.Normal;
    }

    internal override void ComputeEnvironmentCombatReplayDecorations(ParsedEvtcLog log, CombatReplayDecorationContainer environmentDecorations)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputeEnvironmentCombatReplayDecorations(log, environmentDecorations);
        }

        (long start, long end) lifespan;

        // Junk Absorption - Collecting Phase
        var purpleOrbs = log.CombatData.GetMissileEventsBySkillID(JunkAbsorption);
        var swordOrbs = log.CombatData.GetMissileEventsBySkillID(JunkAbsorptionSword);
        var shieldOrbs = log.CombatData.GetMissileEventsBySkillID(JunkAbsorptionShield);

        // Purple Orbs
        environmentDecorations.AddNonHomingMissiles(log, purpleOrbs, Colors.Purple, 0.5, 50);

        // Sword Orbs
        foreach (MissileEvent missileEvent in swordOrbs)
        {
            lifespan = (missileEvent.Time, missileEvent.RemoveEvent?.Time ?? log.LogData.LogEnd);
            for (int i = 0; i < missileEvent.LaunchEvents.Count; i++)
            {
                var launch = missileEvent.LaunchEvents[i];
                lifespan = (launch.Time, i != missileEvent.LaunchEvents.Count - 1 ? missileEvent.LaunchEvents[i + 1].Time : lifespan.end);
                var connector = new InterpolationConnector([new ParametricPoint3D(launch.LaunchPosition, lifespan.start), launch.GetFinalPosition(lifespan)], Connector.InterpolationMethod.Linear);
                environmentDecorations.Add(new CircleDecoration(50, lifespan, Colors.Emerald, 0.5, connector));
                environmentDecorations.Add(new RectangleDecoration(25, 200, lifespan, Colors.White, 0.3, connector));
            }
        }

        // Shield Orbs
        foreach (MissileEvent missileEvent in shieldOrbs)
        {
            lifespan = (missileEvent.Time, missileEvent.RemoveEvent?.Time ?? log.LogData.LogEnd);
            for (int i = 0; i < missileEvent.LaunchEvents.Count; i++)
            {
                var launch = missileEvent.LaunchEvents[i];
                lifespan = (launch.Time, i != missileEvent.LaunchEvents.Count - 1 ? missileEvent.LaunchEvents[i + 1].Time : lifespan.end);
                var connector = new InterpolationConnector([new ParametricPoint3D(launch.LaunchPosition, lifespan.start),launch.GetFinalPosition(lifespan)],Connector.InterpolationMethod.Linear);
                environmentDecorations.Add(new CircleDecoration(50, lifespan, Colors.Emerald, 0.5, connector));
                environmentDecorations.Add(new DoughnutDecoration(100, 125, lifespan, Colors.White, 0.3, connector));
            }
        }
    }
    internal override void SetInstanceBuffs(ParsedEvtcLog log, List<InstanceBuff> instanceBuffs)
    {
        if (!log.LogData.IsInstance)
        {
            base.SetInstanceBuffs(log, instanceBuffs);
        }
    }
}

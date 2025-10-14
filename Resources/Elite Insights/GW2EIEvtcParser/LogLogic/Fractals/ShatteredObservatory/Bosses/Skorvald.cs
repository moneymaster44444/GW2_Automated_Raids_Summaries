﻿using GW2EIEvtcParser.EIData;
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

internal class Skorvald : ShatteredObservatory
{
    internal readonly MechanicGroup Mechanics = new MechanicGroup(
        [
            new PlayerDstHealthDamageHitMechanic([CombustionRush1, CombustionRush2, CombustionRush3], new MechanicPlotlySetting(Symbols.TriangleLeft,Colors.Magenta), "Charge", "Combustion Rush","Charge", 0),
            new PlayerDstHealthDamageHitMechanic([PunishingKickAnomaly, PunishingKickSkorvald], new MechanicPlotlySetting(Symbols.TriangleRightOpen,Colors.Magenta), "Add Kick", "Punishing Kick (Single purple Line, Add)","Kick (Add)", 0),
            new PlayerDstHealthDamageHitMechanic([CranialCascadeAnomaly,CranialCascade2], new MechanicPlotlySetting(Symbols.TriangleRightOpen,Colors.Yellow), "Add Cone KB", "Cranial Cascade (3 purple Line Knockback, Add)","Small Cone KB (Add)", 0),
            new PlayerDstHealthDamageHitMechanic([RadiantFurySkorvald, RadiantFury2], new MechanicPlotlySetting(Symbols.Octagon,Colors.Red), "Burn Circle", "Radiant Fury (expanding burn circles)","Expanding Circles", 0),
            new PlayerDstHealthDamageHitMechanic(FocusedAnger, new MechanicPlotlySetting(Symbols.TriangleDown,Colors.Orange), "Large Cone KB", "Focused Anger (Large Cone Overhead Crosshair Knockback)","Large Cone Knockback", 0),
            new MechanicGroup(
                [
                    new PlayerDstHealthDamageHitMechanic([HorizonStrikeSkorvald1, HorizonStrikeSkorvald2], new MechanicPlotlySetting(Symbols.Circle,Colors.LightOrange), "Horizon Strike", "Horizon Strike (turning pizza slices during Skorvald)","Horizon Strike (Skorvald)", 0), // 
                    new PlayerDstHealthDamageHitMechanic(CrimsonDawn, new MechanicPlotlySetting(Symbols.Circle,Colors.DarkRed), "Horizon Strike End", "Crimson Dawn (almost Full platform attack after Horizon Strike)","Horizon Strike (last)", 0),
                ]
            ),
            new PlayerDstHealthDamageHitMechanic(SolarCyclone, new MechanicPlotlySetting(Symbols.BowtieOpen,Colors.DarkMagenta), "Cyclone", "Solar Cyclone (Circling Knockback)","KB Cyclone", 0),
            new PlayerDstBuffApplyMechanic(SkorvaldsIre, new MechanicPlotlySetting(Symbols.CircleOpenDot, Colors.Purple), "Skor Fixate", "Fixated by Skorvald's Ire", "Skorvald's Fixate",  0),
            new PlayerDstHealthDamageHitMechanic(BloomExplode, new MechanicPlotlySetting(Symbols.Circle,Colors.Yellow), "Bloom Expl", "Hit by Solar Bloom Explosion","Bloom Explosion", 0), //shockwave, not damage? (damage is 50% max HP, not tracked)
            new PlayerDstHealthDamageHitMechanic(SpiralStrike, new MechanicPlotlySetting(Symbols.CircleOpen,Colors.DarkGreen), "Spiral", "Hit after Warp (Jump to Player with overhead bomb)","Spiral Strike", 0),
            new PlayerDstHealthDamageHitMechanic(WaveOfMutilation, new MechanicPlotlySetting(Symbols.TriangleSW,Colors.DarkGreen), "KB Jump", "Hit by KB Jump (player targeted)","Knockback jump", 0),
        ]);
    public Skorvald(int triggerID) : base(triggerID)
    {
        MechanicList.Add(Mechanics);
        Extension = "skorv";
        Icon = EncounterIconSkorvald;
        LogCategoryInformation.InSubCategoryOrder = 0;
        LogID |= 0x000001;
    }

    internal override CombatReplayMap GetCombatMapInternal(ParsedEvtcLog log, CombatReplayDecorationContainer arenaDecorations)
    {
        var crMap = new CombatReplayMap(
                        (987, 1000),
                        (-22267, 14955, -17227, 20735));
        AddArenaDecorationsPerEncounter(log, arenaDecorations, LogID, CombatReplaySkorvald, crMap);
        return crMap;
    }

    internal static List<PhaseData> ComputePhases(ParsedEvtcLog log, SingleActor skorvald, IReadOnlyList<SingleActor> targets, EncounterPhaseData encounterPhase, bool requirePhases)
    {
        if (!requirePhases)
        {
            return [];
        }
        var phases = new List<PhaseData>(5);
        phases.AddRange(GetPhasesByInvul(log, Determined762, skorvald, true, true, encounterPhase.Start, encounterPhase.End));
        for (int i = 0; i < phases.Count; i++)
        {
            int phaseIndex = i + 1;
            PhaseData phase = phases[i];
            phase.AddParentPhase(encounterPhase);
            if (phaseIndex % 2 == 0)
            {
                phase.Name = "Split " + (phaseIndex) / 2;
                AddTargetsToPhaseAndFit(phase, targets, FluxAnomalies, log);
            }
            else
            {
                phase.Name = "Phase " + (phaseIndex + 1) / 2;
                phase.AddTarget(skorvald, log);
            }
        }
        return phases;
    }

    internal override List<PhaseData> GetPhases(ParsedEvtcLog log, bool requirePhases)
    {
        // generic method for fractals
        List<PhaseData> phases = GetInitialPhase(log);
        SingleActor skorvald = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.Skorvald)) ?? throw new MissingKeyActorsException("Skorvald not found");
        phases[0].AddTarget(skorvald, log);
        phases[0].AddTargets(Targets.Where(x => x.IsAnySpecies(FluxAnomalies)), log, PhaseData.TargetPriority.Blocking);
        phases.AddRange(ComputePhases(log, skorvald, Targets, (EncounterPhaseData)phases[0], requirePhases));
        
        return phases;
    }

    protected override HashSet<int> IgnoreForAutoNumericalRenaming()
    {
        return [
            (int)TargetID.FluxAnomaly1,
            (int)TargetID.FluxAnomaly2,
            (int)TargetID.FluxAnomaly3,
            (int)TargetID.FluxAnomaly4,
            (int)TargetID.FluxAnomalyCM1,
            (int)TargetID.FluxAnomalyCM2,
            (int)TargetID.FluxAnomalyCM3,
            (int)TargetID.FluxAnomalyCM4,
        ];
    }

    internal static void DetectUnknownAnomalies(AgentData agentData, List<CombatItem> combatData)
    {
        var fluxAnomalies = new List<AgentItem>();
        for (int i = 0; i < FluxAnomalies.Count; i++)
        {
            fluxAnomalies.AddRange(agentData.GetNPCsByID(FluxAnomalies[i]));
        }
        foreach (AgentItem fluxAnomaly in fluxAnomalies)
        {
            if (combatData.Any(x => x.SkillID == Determined762 && x.IsBuffApply() && x.DstMatchesAgent(fluxAnomaly)))
            {
                fluxAnomaly.OverrideID(TargetID.UnknownAnomaly, agentData);
            }
        }
    }

    internal static void RenameAnomalies(IReadOnlyList<SingleActor> targets)
    {
        int[] nameCount = [0, 0, 0, 0];
        foreach (SingleActor target in targets)
        {
            switch (target.ID)
            {
                case (int)TargetID.FluxAnomaly1:
                case (int)TargetID.FluxAnomalyCM1:
                    target.OverrideName(target.Character + " " + (1 + 4 * nameCount[0]++));
                    break;
                case (int)TargetID.FluxAnomaly2:
                case (int)TargetID.FluxAnomalyCM2:
                    target.OverrideName(target.Character + " " + (2 + 4 * nameCount[1]++));
                    break;
                case (int)TargetID.FluxAnomaly3:
                case (int)TargetID.FluxAnomalyCM3:
                    target.OverrideName(target.Character + " " + (3 + 4 * nameCount[2]++));
                    break;
                case (int)TargetID.FluxAnomaly4:
                case (int)TargetID.FluxAnomalyCM4:
                    target.OverrideName(target.Character + " " + (4 + 4 * nameCount[3]++));
                    break;
            }
        }
    }

    internal override void EIEvtcParse(ulong gw2Build, EvtcVersionEvent evtcVersion, LogData logData, AgentData agentData, List<CombatItem> combatData, IReadOnlyDictionary<uint, ExtensionHandler> extensions)
    {
        var manualFractalScaleSet = false;
        if (!combatData.Any(x => x.IsStateChange == StateChange.FractalScale))
        {
            manualFractalScaleSet = true;
        }
        DetectUnknownAnomalies(agentData, combatData);
        base.EIEvtcParse(gw2Build, evtcVersion, logData, agentData, combatData, extensions);
        SingleActor skorvald = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.Skorvald)) ?? throw new MissingKeyActorsException("Skorvald not found");
        skorvald.OverrideName("Skorvald");
        if (manualFractalScaleSet && combatData.Any(x => x.IsStateChange == StateChange.MaxHealthUpdate && x.SrcMatchesAgent(skorvald.AgentItem) && MaxHealthUpdateEvent.GetMaxHealth(x) < 5e6 && MaxHealthUpdateEvent.GetMaxHealth(x) > 0))
        {
            // Remove manual scale from T1 to T3 for now
            combatData.FirstOrDefault(x => x.IsStateChange == StateChange.FractalScale)!.OverrideSrcAgent(0);
            // Once we have the hp thresholds, simply apply -75, -50, -25 to the srcAgent of existing event
        }
        RenameAnomalies(Targets);
    }

    internal override long GetLogOffset(EvtcVersionEvent evtcVersion, LogData logData, AgentData agentData, List<CombatItem> combatData)
    {
        CombatItem? logStartNPCUpdate = combatData.FirstOrDefault(x => x.IsStateChange == StateChange.LogNPCUpdate);
        if (logStartNPCUpdate != null)
        {
            AgentItem skorvald = agentData.GetNPCsByID(TargetID.Skorvald).FirstOrDefault() ?? throw new MissingKeyActorsException("Skorvald not found");
            long upperLimit = GetPostLogStartNPCUpdateDamageEventTime(logData, agentData, combatData, logStartNPCUpdate.Time, skorvald);
            // Skorvald may spawns with 0% hp
            CombatItem? firstNonZeroHPUpdate = combatData.FirstOrDefault(x => x.IsStateChange == StateChange.HealthUpdate && x.SrcMatchesAgent(skorvald) && HealthUpdateEvent.GetHealthPercent(x) > 0);
            CombatItem? enterCombat = combatData.FirstOrDefault(x => x.IsStateChange == StateChange.EnterCombat && x.SrcMatchesAgent(skorvald) && x.Time <= upperLimit + ServerDelayConstant);
            return firstNonZeroHPUpdate != null ? Math.Min(firstNonZeroHPUpdate.Time, enterCombat != null ? enterCombat.Time : long.MaxValue) : GetGenericLogOffset(logData);
        }
        return GetGenericLogOffset(logData);
    }

    internal override LogData.LogMode GetLogMode(CombatData combatData, AgentData agentData, LogData logData)
    {
        SingleActor target = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.Skorvald)) ?? throw new MissingKeyActorsException("Skorvald not found");
        if (combatData.GetGW2BuildEvent().Build >= GW2Builds.September2020SunquaPeakRelease)
        {
            // Check some CM skills instead, not perfect but helps, 
            // Solar Bolt is the first thing he tries to cast, that looks very consistent
            // If the phase 1 is super fast to the point skorvald does not cast anything, supernova should be there
            // Otherwise we are looking at a super fast phase 1 (< 7 secondes) where the team ggs just before supernova
            // Joining the encounter mid encounter may also yield a false negative but at that point the log is incomplete already
            // WARNING: Skorvald seems to cast SupernovaCM on T4 regardless of the mode since an unknown amount of time, removing that id check
            // and adding split thrash mob check
            var cmSkills = new HashSet<long>
            {
                SolarBoltCM,
                //SupernovaCM,
            };
            if (combatData.GetSkills().Intersect(cmSkills).Any() ||
                agentData.GetNPCsByID(TargetID.FluxAnomalyCM1).Any(x => x.FirstAware >= target.FirstAware) ||
                agentData.GetNPCsByID(TargetID.FluxAnomalyCM2).Any(x => x.FirstAware >= target.FirstAware) ||
                agentData.GetNPCsByID(TargetID.FluxAnomalyCM3).Any(x => x.FirstAware >= target.FirstAware) ||
                agentData.GetNPCsByID(TargetID.FluxAnomalyCM4).Any(x => x.FirstAware >= target.FirstAware))
            {
                return LogData.LogMode.CM;
            }
            return LogData.LogMode.Normal;
        }
        else
        {
            return (target.GetHealth(combatData) == 5551340) ? LogData.LogMode.CM : LogData.LogMode.Normal;
        }
    }

    internal override IReadOnlyList<TargetID>  GetTargetsIDs()
    {
        return
        [
            TargetID.Skorvald,
            .. FluxAnomalies
        ];
    }

    internal static readonly IReadOnlyList<TargetID> FluxAnomalies = [
            TargetID.FluxAnomaly1,
            TargetID.FluxAnomaly2,
            TargetID.FluxAnomaly3,
            TargetID.FluxAnomaly4,
            TargetID.FluxAnomalyCM1,
            TargetID.FluxAnomalyCM2,
            TargetID.FluxAnomalyCM3,
            TargetID.FluxAnomalyCM4,
        ];

    internal override void CheckSuccess(CombatData combatData, AgentData agentData, LogData logData, IReadOnlyCollection<AgentItem> playerAgents)
    {
        base.CheckSuccess(combatData, agentData, logData, playerAgents);
        // reward or death worked
        if (logData.Success)
        {
            return;
        }
        SingleActor skorvald = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.Skorvald)) ?? throw new MissingKeyActorsException("Skorvald not found");
        HealthDamageEvent? lastDamageTaken = combatData.GetDamageTakenData(skorvald.AgentItem).LastOrDefault(x => (x.HealthDamage > 0) && playerAgents.Any(x.From.IsMasterOrSelf));
        if (lastDamageTaken != null)
        {
            var invul895Apply = combatData.GetBuffApplyDataByIDByDst(Determined895, skorvald.AgentItem).Where(x => x.Time > lastDamageTaken.Time - 500).LastOrDefault();
            if (invul895Apply != null)
            {
                logData.SetSuccess(true, Math.Min(invul895Apply.Time, lastDamageTaken.Time));
            }
        }
    }

    internal override IReadOnlyList<TargetID> GetTrashMobsIDs()
    {
        var trashIDs = new List<TargetID>(1 + base.GetTrashMobsIDs().Count);
        trashIDs.AddRange(base.GetTrashMobsIDs());
        trashIDs.Add(TargetID.SolarBloom);
        return trashIDs;
    }

    internal override void ComputeNPCCombatReplayActors(NPC target, ParsedEvtcLog log, CombatReplay replay)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputeNPCCombatReplayActors(target, log, replay);
        }
        long castDuration;
        long growing;
        (long start, long end) lifespan;

        switch (target.ID)
        {
            case (int)TargetID.Skorvald:
                foreach (CastEvent cast in target.GetAnimatedCastEvents(log))
                {
                    switch (cast.SkillID)
                    {
                        // Horizon Strike
                        case HorizonStrikeSkorvald2:
                        case HorizonStrikeSkorvald4:
                            castDuration = 3900;
                            int shiftingAngle = 45;
                            int sliceSpawnInterval = 750;
                            lifespan = (cast.Time + 100, ComputeEndCastTimeByBuffApplication(log, target, Stun, cast.Time, castDuration));
                            lifespan.end = Math.Min(lifespan.end, ComputeEndCastTimeByBuffApplication(log, target, Determined762, cast.Time, castDuration));

                            if (target.TryGetCurrentFacingDirection(log, cast.Time + 100, out var facingHorizonStrike, castDuration))
                            {
                                float degree = facingHorizonStrike.GetRoundedZRotationDeg();

                                // Horizon Strike starting at Skorvald's facing point
                                if (cast.SkillID == HorizonStrikeSkorvald4)
                                {
                                    for (int i = 0; i < 4; i++)
                                    {
                                        AddHorizonStrikeDecoration(replay, target, lifespan, degree);
                                        lifespan.start += sliceSpawnInterval;
                                        lifespan.end += sliceSpawnInterval;
                                        degree -= shiftingAngle;
                                    }
                                }
                                // Starting at Skorvald's 90° of facing point
                                if (cast.SkillID == HorizonStrikeSkorvald2)
                                {
                                    degree -= 90;
                                    for (int i = 0; i < 4; i++)
                                    {
                                        AddHorizonStrikeDecoration(replay, target, lifespan, degree);
                                        lifespan.start += sliceSpawnInterval;
                                        lifespan.end += sliceSpawnInterval;
                                        degree += shiftingAngle;
                                    }
                                }
                            }
                            break;
                        // Crimson Dawn
                        case CrimsonDawnSkorvaldCM1:
                        case CrimsonDawnSkorvaldCM2:
                        case CrimsonDawnSkorvaldCM3:
                        case CrimsonDawnSkorvaldCM4:
                            castDuration = 3000;
                            uint radius = 1200;
                            int angleCrimsonDawn = 295;
                            lifespan = (cast.Time + 100, ComputeEndCastTimeByBuffApplication(log, target, Stun, cast.Time, castDuration));
                            lifespan.end = Math.Min(lifespan.end, ComputeEndCastTimeByBuffApplication(log, target, Determined762, cast.Time, castDuration));

                            if (target.TryGetCurrentFacingDirection(log, cast.Time + 100, out var facingCrimsonDawn, castDuration))
                            {
                                float degree = facingCrimsonDawn.GetRoundedZRotationDeg();

                                if (cast.SkillID == CrimsonDawnSkorvaldCM2)
                                {
                                    degree += 90;
                                }
                                if (cast.SkillID == CrimsonDawnSkorvaldCM1)
                                {
                                    degree += 270;
                                }
                                var connector = new AgentConnector(target);
                                var rotationConnector = new AngleConnector(degree);
                                replay.Decorations.Add(new PieDecoration(radius, angleCrimsonDawn, lifespan, Colors.Orange, 0.2, connector).UsingRotationConnector(rotationConnector));
                                replay.Decorations.Add(new PieDecoration(radius, angleCrimsonDawn, (lifespan.end, lifespan.end + 500), Colors.Red, 0.2, connector).UsingRotationConnector(rotationConnector));
                            }
                            break;
                        // Punishing Kick
                        case PunishingKickSkorvald:
                            castDuration = 1850;
                            lifespan = (cast.Time + 100, ComputeEndCastTimeByBuffApplication(log, target, Stun, cast.Time, castDuration));
                            lifespan.end = Math.Min(lifespan.end, ComputeEndCastTimeByBuffApplication(log, target, Determined762, cast.Time, castDuration));
                            long expectedEndCast = cast.Time + castDuration;

                            if (target.TryGetCurrentFacingDirection(log, cast.Time + 100, out var frontalPoint, castDuration))
                            {
                                float rotation = frontalPoint.GetRoundedZRotationDeg();
                                // Frontal
                                AddKickIndicatorDecoration(replay, target, lifespan, expectedEndCast, rotation);
                            }
                            break;
                        // Radiant Fury
                        case RadiantFurySkorvald:
                            castDuration = 2700;
                            growing = cast.Time + castDuration;
                            lifespan = (cast.Time, ComputeEndCastTimeByBuffApplication(log, target, Stun, cast.Time, castDuration));
                            lifespan.end = Math.Min(lifespan.end, ComputeEndCastTimeByBuffApplication(log, target, Determined762, cast.Time, castDuration));
                            (long start, long end) lifespanWave = (lifespan.end, lifespan.end + 900);

                            if (growing <= lifespan.end)
                            {
                                GeographicalConnector connector = new AgentConnector(target);
                                replay.Decorations.AddShockwave(connector, lifespanWave, Colors.Red, 0.6, 1200);
                            }
                            break;
                        // Supernova - Phase Oneshot
                        case SupernovaSkorvaldCM:
                            castDuration = 75000;
                            growing = cast.Time + castDuration;
                            lifespan = (cast.Time, ComputeEndCastTimeByBuffApplication(log, target, Stun, cast.Time, castDuration));
                            replay.Decorations.AddWithGrowing(new CircleDecoration(1200, lifespan, Colors.Red, 0.2, new AgentConnector(target)), growing);
                            break;
                        // Cranial Cascade
                        case CranialCascadeSkorvald:
                            castDuration = 1750;
                            growing = cast.Time + castDuration;

                            lifespan = (cast.Time, ComputeEndCastTimeByBuffApplication(log, target, Stun, cast.Time, castDuration));
                            lifespan.end = Math.Min(lifespan.end, ComputeEndCastTimeByBuffApplication(log, target, Determined762, cast.Time, castDuration));

                            if (target.TryGetCurrentFacingDirection(log, cast.Time + 100, out var facingCranialCascade, castDuration))
                            {
                                float rotation = facingCranialCascade.GetRoundedZRotationDeg();

                                // Frontal
                                AddKickIndicatorDecoration(replay, target, lifespan, growing, rotation);
                                // Left
                                AddKickIndicatorDecoration(replay, target, lifespan, growing, rotation - 35);
                                // Right
                                AddKickIndicatorDecoration(replay, target, lifespan, growing, rotation + 35);
                            }
                            break;
                        default:
                            break;
                    }
                }
                break;
            case (int)TargetID.FluxAnomalyCM1:
            case (int)TargetID.FluxAnomalyCM2:
            case (int)TargetID.FluxAnomalyCM3:
            case (int)TargetID.FluxAnomalyCM4:
                foreach (CastEvent cast in target.GetAnimatedCastEvents(log))
                {
                    switch (cast.SkillID)
                    {
                        // Solar Stomp
                        case SolarStomp:
                            castDuration = 2250;
                            lifespan = (cast.Time, cast.Time + castDuration);
                            uint radius = 280;
                            (long start, long end) lifespanShockwave = (lifespan.end, lifespan.end + castDuration);

                            // Stomp
                            GeographicalConnector connector = new AgentConnector(target);
                            replay.Decorations.AddWithGrowing(new CircleDecoration(radius, lifespan, Colors.LightOrange, 0.2, connector), lifespan.end);
                            replay.Decorations.AddShockwave(connector, lifespanShockwave, Colors.Red, 0.6, 1200);
                            break;
                        // Punishing Kick
                        case PunishingKickAnomaly:
                            castDuration = 1850;
                            growing = cast.Time + castDuration;
                            lifespan = (cast.Time, growing);

                            if (target.TryGetCurrentFacingDirection(log, cast.Time + 100, out var frontalPoint, castDuration))
                            {
                                float rotation = frontalPoint.GetRoundedZRotationDeg();
                                // Frontal
                                AddKickIndicatorDecoration(replay, target, lifespan, growing, rotation);
                            }
                            break;
                        // Cranial Cascade
                        case CranialCascadeAnomaly:
                            castDuration = 1750;
                            growing = cast.Time + castDuration;
                            lifespan = (cast.Time, growing);
                            int angleCranialCascade = 35;

                            if (target.TryGetCurrentFacingDirection(log, cast.Time + 100, out var facingCranialCascade, castDuration))
                            {
                                float rotation = facingCranialCascade.GetRoundedZRotationDeg();

                                // Left
                                AddKickIndicatorDecoration(replay, target, lifespan, growing, rotation - angleCranialCascade);
                                // Right
                                AddKickIndicatorDecoration(replay, target, lifespan, growing, rotation + angleCranialCascade);
                            }
                            break;
                        // Mist Smash
                        case MistSmash:
                            castDuration = 1933;
                            lifespan = (cast.Time, cast.Time + castDuration);
                            (long start, long end) lifespanShockwave2 = (lifespan.end, lifespan.end + 2250);
                            replay.Decorations.AddWithGrowing(new CircleDecoration(160, lifespan, Colors.Orange, 0.2, new AgentConnector(target)), lifespan.end);
                            
                            // Nightmare Discharge Shockwave
                            replay.Decorations.AddShockwave(new AgentConnector(target), lifespanShockwave2, Colors.Yellow, 0.3, 1200);
                            break;
                        // Wave of Mutilation
                        case WaveOfMutilation:
                            castDuration = 1850;
                            int angleWaveOfMutilation = 18;
                            long expectedEndCast = cast.Time + castDuration;
                            lifespan = (cast.Time, expectedEndCast);

                            if (target.TryGetCurrentFacingDirection(log, cast.Time + 100, out var facingWaveOfMutilation, castDuration))
                            {
                                float rotation = facingWaveOfMutilation.GetRoundedZRotationDeg();

                                float startingDegree = rotation - angleWaveOfMutilation * 2;
                                for (int i = 0; i < 5; i++)
                                {
                                    AddKickIndicatorDecoration(replay, target, lifespan, expectedEndCast, startingDegree);
                                    startingDegree += angleWaveOfMutilation;
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
                break;
            case (int)TargetID.SolarBloom:
                break;
            default:
                break;
        }
    }

    internal override void ComputeEnvironmentCombatReplayDecorations(ParsedEvtcLog log, CombatReplayDecorationContainer environmentDecorations)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputeEnvironmentCombatReplayDecorations(log, environmentDecorations);
        }

        // Mist Bomb - Both for Skorvald and Flux Anomalies
        if (log.CombatData.TryGetEffectEventsByGUID(EffectGUIDs.MistBomb, out var mistBombs))
        {
            foreach (EffectEvent effect in mistBombs)
            {
                (long start, long end) lifespan = effect.ComputeLifespan(log, 1000);
                environmentDecorations.Add(new CircleDecoration(130, lifespan, Colors.Orange, 0.2, new PositionConnector(effect.Position)));
            }
        }

        // Solar Bolt - Indicator
        AddDistanceCorrectedOrbAoEDecorations(log, environmentDecorations, EffectGUIDs.SolarBoltIndicators, TargetID.Skorvald, 310, 1800, 1300);

        // Solar Bolt - Damage
        if (log.CombatData.TryGetEffectEventsByGUID(EffectGUIDs.SkorvaldSolarBoltDamage, out var solarBolts))
        {
            foreach (EffectEvent effect in solarBolts)
            {
                (long start, long end) lifespan = effect.ComputeLifespan(log, 12000);
                environmentDecorations.Add(new CircleDecoration(100, lifespan, Colors.Red, 0.2, new PositionConnector(effect.Position)));
            }
        }

        // Solar Cyclone
        if (log.CombatData.TryGetEffectEventsByGUID(EffectGUIDs.KickGroundEffect, out var kickEffects))
        {
            foreach (EffectEvent effect in kickEffects)
            {
                (long start, long end) lifespan = (effect.Time, effect.Time + 300);
                environmentDecorations.Add(new RectangleDecoration(300, 180, lifespan, Colors.Red, 0.2, new PositionConnector(effect.Position)).UsingRotationConnector(new AngleConnector(effect.Rotation.Z - 90)));
            }
        }

        // Solar Bolt Orbs
        var solarBolt = log.CombatData.GetMissileEventsBySkillID(SolarBoltCM);
        environmentDecorations.AddNonHomingMissiles(log, solarBolt, Colors.Red, 0.2, 50);
    }

    internal override void ComputePlayerCombatReplayActors(PlayerActor p, ParsedEvtcLog log, CombatReplay replay)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputePlayerCombatReplayActors(p, log, replay);
        }
        // Fixations
        var fixations = p.GetBuffStatus(log, [SkorvaldsIre]).Where(x => x.Value > 0);
        replay.Decorations.AddOverheadIcons(fixations, p, ParserIcons.FixationPurpleOverhead);
    }

    internal override void SetInstanceBuffs(ParsedEvtcLog log, List<InstanceBuff> instanceBuffs)
    {
        if (!log.LogData.IsInstance)
        {
            base.SetInstanceBuffs(log, instanceBuffs);
        }
    }

    /// <summary>
    /// Add Horizon Strike decoration.
    /// </summary>
    /// <param name="replay">Combat Replay.</param>
    /// <param name="target">Actor.</param>
    /// <param name="lifespan">Start and End of cast.</param>
    /// <param name="degree">Degree of the strike.</param>
    private static void AddHorizonStrikeDecoration(CombatReplay replay, SingleActor target, (long start, long end) lifespan, float degree)
    {
        var connector = new AgentConnector(target);
        var frontRotationConnector = new AngleConnector(degree);
        var flipRotationConnector = new AngleConnector(degree + 180);
        // Indicator
        var pieIndicator = new PieDecoration(1200, 70, lifespan, Colors.Orange, 0.2, connector);
        replay.Decorations.Add(pieIndicator.UsingRotationConnector(frontRotationConnector));
        replay.Decorations.Add(pieIndicator.Copy().UsingRotationConnector(flipRotationConnector));
        // Attack hit
        (long start, long end) lifespanHit = (lifespan.end, lifespan.end + 300);
        var pieHit = (PieDecoration)new PieDecoration(1200, 70, lifespanHit, Colors.Red, 0.2, connector).UsingGrowingEnd(lifespanHit.end);
        replay.Decorations.Add(pieHit.UsingRotationConnector(frontRotationConnector));
        replay.Decorations.Add(pieHit.Copy().UsingRotationConnector(flipRotationConnector));
    }

    /// <summary>
    /// Add Kick decoration.
    /// </summary>
    /// <param name="replay">Combat Replay.</param>
    /// <param name="target">Actor.</param>
    /// <param name="lifespan">Start and End of cast.</param>
    /// <param name="growing">Expected end of the cast.</param>
    /// <param name="rotation">Rotation degree.</param>
    private static void AddKickIndicatorDecoration(CombatReplay replay, SingleActor target, (long start, long end) lifespan, long growing, float rotation)
    {
        int translation = 150;
        var rotationConnector = new AngleConnector(rotation);
        var positionConnector = (AgentConnector)new AgentConnector(target).WithOffset(new(translation, 0, 0), true);
        replay.Decorations.AddWithGrowing((RectangleDecoration)new RectangleDecoration(300, target.HitboxWidth, lifespan, Colors.LightOrange, 0.2, positionConnector).UsingRotationConnector(rotationConnector), growing);

        // Cascade count => 4
        for (int i = 0; i < 4; i++)
        {
            replay.Decorations.Add(new RectangleDecoration(300, target.HitboxWidth, (lifespan.end, lifespan.end + 300), Colors.Red, 0.2, positionConnector).UsingRotationConnector(rotationConnector));
            lifespan.end += 300;
            translation += 300;
        }
    }
}

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

internal class DecimaTheStormsinger : MountBalrior
{
    private bool IsCMTriggerID => GenericTriggerID == (int)TargetID.DecimaCM;

    internal readonly MechanicGroup Mechanics = new MechanicGroup([
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic([ChorusOfThunderDamage, ChorusOfThunderCM], new MechanicPlotlySetting(Symbols.Circle, Colors.LightOrange), "ChorThun.H", "Hit by Chorus of Thunder (Spreads AoE / Conduit AoE)", "Chorus of Thunder Hit", 0),
                new PlayerDstEffectMechanic(EffectGUIDs.DecimaChorusOfThunderAoE, new MechanicPlotlySetting(Symbols.Circle, Colors.LightGrey), "ChorThun.T", "Targeted by Chorus of Thunder (Spreads)", "Chorus of Thunder Target", 0),
            ]),
            new PlayerDstHealthDamageHitMechanic([DiscordantThunderCM], new MechanicPlotlySetting(Symbols.Circle, Colors.Orange), "DiscThun.H", "Hit by Discordant Thunder", "Discordant Thunder Hit", 0),
            new PlayerDstHealthDamageHitMechanic(HarmoniousThunder, new MechanicPlotlySetting(Symbols.Circle, Colors.Yellow), "HarmThun.H", "Hit by Harmonious Thunder", "Harmonious Thunder Hit", 0),
            // TODO: Seismic Crash needs to be fully verified again in both modes
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic([SeismicCrashNM, SeismicCrashCM, SeismicCrashCM2, SeismicCrashCM3, SeismicCrashCM4, SeismicCrashCM5, SeismicCrashCM6], new MechanicPlotlySetting(Symbols.Hourglass, Colors.White), "SeisCrash.H", "Hit by Seismic Crash (Concentric Rings)", "Seismic Crash Hit", 0)
                    .WithStabilitySubMechanic(
                        new PlayerDstHealthDamageHitMechanic([SeismicCrashNM, SeismicCrashCM, SeismicCrashCM2, SeismicCrashCM4, SeismicCrashCM5, SeismicCrashCM6], new MechanicPlotlySetting(Symbols.Hourglass, Colors.DarkWhite), "SeisCrash.CC", "CC by Seismic Crash (Concentric Rings)", "Seismic Crash CC", 0),
                        false
                    ),
                new PlayerDstHealthDamageMechanic(SeismicCrashHitboxDamage, new MechanicPlotlySetting(Symbols.CircleCross, Colors.LightRed), "SeisCrash.Dwn", "Downed by Seismic Crash (Hitbox)", "Seismic Crash Downed", 0)
                    .UsingChecker((hde, log) => hde.To.IsDowned(log, hde.Time))
                    .WithBuilds(GW2Builds.December2024MountBalriorNerfs),
                new PlayerDstHealthDamageMechanic(SeismicCrashHitboxDamage, new MechanicPlotlySetting(Symbols.CircleCross, Colors.Red), "SeisCrash.D", "Seismic Crash Death (Hitbox)", "Seismic Crash Death", 0)
                    .UsingChecker((hde, log) => hde.To.IsDead(log, hde.Time)), // If a player is already in downstate they get killed in NM, not logged in CM
            ]),
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic([SeismicReposition_80_50, SeismicReposition_70_40, SeismicReposition_40_TO_10, SeismicReposition_10], new MechanicPlotlySetting(Symbols.HourglassOpen, Colors.White), "SeisRepos.H", "Hit by Seismic Reposition (Concentric Rings Leap)", "Seismic Reposition Hit", 0)
                    .WithStabilitySubMechanic(
                        new PlayerDstHealthDamageHitMechanic([SeismicReposition_80_50, SeismicReposition_70_40, SeismicReposition_40_TO_10, SeismicReposition_10], new MechanicPlotlySetting(Symbols.HourglassOpen, Colors.DarkWhite), "SeisRepos.CC", "CC by Seismic Reposition (Concentric Rings Leap)", "Seismic Reposition CC", 0),
                        false
                    ),
            ]),
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic([EarthrendCastAndOuterRingDamageNM, EarthrendCastAndOuterRingDamageCM], new MechanicPlotlySetting(Symbols.CircleOpen, Colors.Blue), "Earthrend.H", "Hit by Earthrend (Outer Doughnut)", "Earthrend Hit", 0)
                    .WithStabilitySubMechanic(
                        new PlayerDstHealthDamageHitMechanic([EarthrendCastAndOuterRingDamageNM, EarthrendCastAndOuterRingDamageCM], new MechanicPlotlySetting(Symbols.CircleOpen, Colors.DarkBlue), "Earthrend.CC", "CC by Earthrend (Outer Doughnut)", "Earthrend CC", 0),
                        false
                    ),
                new PlayerDstHealthDamageMechanic(EarthrendInnerHitboxDamageNM, new MechanicPlotlySetting(Symbols.CircleCrossOpen, Colors.LightRed), "Earthrend.Dwn", "Downed by Earthrend (Hitbox)", "Earthrend Downed", 0)
                    .UsingChecker((hde, log) => hde.To.IsDowned(log, hde.Time))
                    .WithBuilds(GW2Builds.December2024MountBalriorNerfs),
                new PlayerDstHealthDamageMechanic([EarthrendInnerHitboxDamageNM, EarthrendInnerHitboxDamageCM], new MechanicPlotlySetting(Symbols.CircleCrossOpen, Colors.Red), "Earthrend.D", "Earthrend Death (Hitbox)", "Earthrend Death", 0)
                    .UsingChecker((hde, log) => hde.To.IsDead(log, hde.Time)), // If a player is already in downstate they get killed in NM, not logged in CM
            ]),
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic(Fluxlance, new MechanicPlotlySetting(Symbols.StarSquare, Colors.LightOrange), "Fluxlance.H", "Hit by Fluxlance (Single Orange Arrow)", "Fluxlance Hit", 0),
                new PlayerDstHealthDamageHitMechanic([FluxlanceFusillade, FluxlanceFusilladeCM], new MechanicPlotlySetting(Symbols.StarDiamond, Colors.LightOrange), "FluxFusi.H", "Hit by Fluxlance Fusillade (Sequential Orange Arrows)", "Fluxlance Fusillade Hit", 0),
                new PlayerDstHealthDamageHitMechanic([FluxlanceSalvo1, FluxlanceSalvoCM1, FluxlanceSalvo2, FluxlanceSalvoCM2, FluxlanceSalvo3, FluxlanceSalvoCM3, FluxlanceSalvo4, FluxlanceSalvoCM4, FluxlanceSalvo5, FluxlanceSalvoCM5], new MechanicPlotlySetting(Symbols.StarDiamondOpen, Colors.LightOrange), "FluxSalvo.H", "Hit by Fluxlance Salvo (Simultaneous Orange Arrows)", "Fluxlance Salvo Hit", 0),
                new PlayerDstHealthDamageHitMechanic([Fluxlance, FluxlanceFusillade, FluxlanceFusilladeCM, FluxlanceSalvo1, FluxlanceSalvoCM1, FluxlanceSalvo2, FluxlanceSalvoCM2, FluxlanceSalvo3, FluxlanceSalvoCM3, FluxlanceSalvo4, FluxlanceSalvoCM4, FluxlanceSalvo5, FluxlanceSalvoCM5], new MechanicPlotlySetting(Symbols.DiamondWide, Colors.DarkMagenta), "FluxInc.H", "Hit by Fluxlance with Harmonic Sensitivity", "Fluxlance with Harmonic Sensitivity Hit", 0)
                    .UsingChecker((hde, log) => hde.To.HasBuff(log, HarmonicSensitivity, hde.Time, ServerDelayConstant)),
                new PlayerDstBuffApplyMechanic(FluxlanceTargetBuff1, new MechanicPlotlySetting(Symbols.StarTriangleDown, Colors.Orange), "Fluxlance.T", "Targeted by Fluxlance", "Fluxlance Target", 0),
                new PlayerDstBuffApplyMechanic(FluxlanceRedArrowTargetBuff, new MechanicPlotlySetting(Symbols.StarTriangleDown, Colors.Red), "FluxRed.T", "Targeted by Fluxlance (Red Arrow)", "Fluxlance (Red Arrow)", 0),
                new PlayerDstBuffApplyMechanic([TargetOrder1JW, TargetOrder2JW, TargetOrder3JW, TargetOrder4JW, TargetOrder5JW], new MechanicPlotlySetting(Symbols.StarTriangleDown, Colors.LightOrange), "FluxOrder.T", "Targeted by Fluxlance (Target Order)", "Fluxlance Target (Sequential)", 0),
            ]),
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic([SparkingAuraTier1, SparkingAuraTier1CM], new MechanicPlotlySetting(Symbols.CircleX, Colors.Green), "SparkAura1.H", "Sparking Aura (Absorbed Tier 1 Green Damage)", "Absorbed Tier 1 Green", 0),
                new PlayerDstHealthDamageHitMechanic([SparkingAuraTier2, SparkingAuraTier2CM], new MechanicPlotlySetting(Symbols.CircleX, Colors.LightMilitaryGreen), "SparkAura2.H", "Sparking Aura (Absorbed Tier 2 Green Damage)", "Absorbed Tier 2 Green", 0),
                new PlayerDstHealthDamageHitMechanic([SparkingAuraTier3, SparkingAuraTier3CM], new MechanicPlotlySetting(Symbols.CircleX, Colors.DarkGreen), "SparkAura3.H", "Sparking Aura (Absorbed Tier 3 Green Damage)", "Absorbed Tier 3 Green", 0),
                new PlayerDstHealthDamageHitMechanic([SparkingAuraTier1, SparkingAuraTier1CM, SparkingAuraTier2, SparkingAuraTier2CM, SparkingAuraTier3, SparkingAuraTier3CM], new MechanicPlotlySetting(Symbols.CircleX, Colors.MilitaryGreen), "SparkAuraInc.H", "Hit by Sparking Aura with Galvanic Sensitivity", "Sparking Aura with Galvanic Sensitivity Hit", 0)
                    .UsingChecker((hde, log) => hde.To.HasBuff(log, GalvanicSensitivity, hde.Time, ServerDelayConstant)),
            ]),
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic([FulgentFence, FulgentFenceCM], new MechanicPlotlySetting(Symbols.Octagon, Colors.Purple), "FulFence.H", "Hit by Fulgent Fence (Barriers between Conduits)", "Fulgence Fence Hit", 0),
                new PlayerDstHealthDamageHitMechanic([FulgentAuraTier1, FulgentAuraTier1CM, FulgentAuraTier2, FulgentAuraTier2CM, FulgentAuraTier3, FulgentAuraTier3CM], new MechanicPlotlySetting(Symbols.CircleXOpen, Colors.Purple), "FulAura.H", "Hit by Fulgent Aura (Conduit AoE)", "Fulgent Aura Hit", 0),
                new PlayerDstHealthDamageHitMechanic([ReverberatingImpact, ReverberatingImpactCM], new MechanicPlotlySetting(Symbols.StarOpen, Colors.LightBlue), "RevImpact.H", "Hit by Reverberating Impact (Hit a Conduit)", "Reverberating Impact Hit", 0),
            ]),
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic(Earthfall, new MechanicPlotlySetting(Symbols.YUp, Colors.LightPink), "Earthfall.H", "Hit by Earthfall (Transcendent Boudlers Jump)", "Earthfall Hit", 0),
                new PlayerDstHealthDamageHitMechanic(Sparkwave, new MechanicPlotlySetting(Symbols.TriangleDown, Colors.LightOrange), "Sparkwave.H", "Hit by Sparkwave (Transcendent Boulders Cone)", "Sparkwave Hit", 0),
                new PlayerDstHealthDamageHitMechanic(ChargedGround, new MechanicPlotlySetting(Symbols.CircleOpenDot, Colors.CobaltBlue), "CharGrnd.H", "Hit by Charged Ground (Transcendent Boulders AoEs)", "Charged Ground Hit", 0),
            ]),
            new PlayerDstHealthDamageHitMechanic([FulgentFenceCM, FluxlanceFusilladeCM, FluxlanceSalvoCM1, FluxlanceSalvoCM2, FluxlanceSalvoCM3, FluxlanceSalvoCM4, FluxlanceSalvoCM5, ChorusOfThunderCM, DiscordantThunderCM, HarmoniousThunder], new MechanicPlotlySetting(Symbols.Pentagon, Colors.Lime), "BugDance.Achiv", "Achievement Eligibility: This Bug Can Dance", "Achiv: This Bug Can Dance", 0).UsingChecker((adhe, log) =>
            {
                // If you are dead, lose the achievement
                if (adhe.To.IsDead(log, log.LogData.LogStart, log.LogData.LogEnd))
                {
                    return true;
                }

                // If you get hit by Fulgent Fence, lose the achievement
                if (adhe.SkillID == FulgentFenceCM && adhe.HasHit)
                {
                    return true;
                }

                var damageTaken = log.CombatData.GetDamageTakenData(adhe.To);
                bool hasExposed = log.CombatData.GetBuffData(ExposedPlayer).Any(x => x is BuffApplyEvent && x.To.Is(adhe.To) && Math.Abs(x.Time - adhe.Time) < ServerDelayConstant);

                // If you get hit by your own Fluxlance only during the current sequence, keep the achievement
                // If you get hit by 2 Fluxlance in the current sequence, lose the achievement
                long[] fluxlanceIDs = [FluxlanceFusilladeCM, FluxlanceSalvoCM1, FluxlanceSalvoCM2, FluxlanceSalvoCM3, FluxlanceSalvoCM4, FluxlanceSalvoCM5];
                var fluxlanceTimes = damageTaken.Where(x => (fluxlanceIDs.Contains(x.SkillID)) && x.HasHit).Select(x => x.Time).OrderBy(x => x);
                foreach (long fluxlanceTime in fluxlanceTimes)
                {
                    // Fluxlance sequence lasts about 5 seconds, giving it 7 as margin
                    if (Math.Abs(fluxlanceTime - adhe.Time) < 7000 && fluxlanceIDs.Contains(adhe.SkillID) && hasExposed)
                    {
                        return true;
                    }
                }

                // If you get hit by your own thunder, keep the achievement
                // If you get hit by a thunder on another player or on a conduit, lose the achievement
                long[] thunderIDs = [ChorusOfThunderCM, DiscordantThunderCM, HarmoniousThunder];
                var thunderTimes = damageTaken.Where(x => (thunderIDs.Contains(x.SkillID)) && x.HasHit).Select(x => x.Time).OrderBy(x => x);
                foreach (long thunderTime in thunderTimes)
                {
                    if (Math.Abs(thunderTime - adhe.Time) < ServerDelayConstant && thunderIDs.Contains(adhe.SkillID) && hasExposed)
                    {
                        return true;
                    }
                }

                return false;
            })
                .UsingEnable(log => log.LogData.IsCM)
                .UsingAchievementEligibility(),
            new EnemyDstBuffApplyMechanic(ChargeDecima, new MechanicPlotlySetting(Symbols.BowtieOpen, Colors.DarkMagenta), "Charge", "Charge Stacks", "Charge Stack", 0),
        ]);

    public DecimaTheStormsinger(int triggerID) : base(triggerID)
    {
        MechanicList.Add(Mechanics);
        Extension = "decima";
        Icon = EncounterIconDecima;
        ChestID = ChestID.DecimasChest;
        LogCategoryInformation.InSubCategoryOrder = 1;
        LogID |= 0x000002;
    }
    internal override CombatReplayMap GetCombatMapInternal(ParsedEvtcLog log, CombatReplayDecorationContainer arenaDecorations)
    {
        var crMap = new CombatReplayMap(
                        (1602, 1602),
                        (-13068, 10300, -7141, 16227));
        AddArenaDecorationsPerEncounter(log, arenaDecorations, LogID, CombatReplayDecimaTheStormsinger, crMap);
        return crMap;
    }

    internal override IReadOnlyList<TargetID>  GetTargetsIDs()
    {
        return
        [
            TargetID.Decima,
            TargetID.DecimaCM,
            TargetID.TranscendentBoulder,
        ];
    }

    internal override IReadOnlyList<TargetID> GetTrashMobsIDs()
    {
        return
        [
            TargetID.GreenOrb1Player,
            TargetID.GreenOrb1PlayerCM,
            TargetID.GreenOrb2Players,
            TargetID.GreenOrb2PlayersCM,
            TargetID.GreenOrb3Players,
            TargetID.GreenOrb3PlayersCM,
            TargetID.EnlightenedConduitCM,
            TargetID.EnlightenedConduit,
            TargetID.EnlightenedConduitGadget,
            TargetID.BigEnlightenedConduitGadget,
            TargetID.DecimaBeamStart,
            TargetID.DecimaBeamStartCM,
            TargetID.DecimaBeamEnd,
            TargetID.DecimaBeamEndCM,
        ];
    }

    internal override List<InstantCastFinder> GetInstantCastFinders()
    {
        return
        [
            new DamageCastFinder(ThrummingPresenceBuff, ThrummingPresenceDamage),
            new DamageCastFinder(ThrummingPresenceBuffCM, ThrummingPresenceDamageCM),
        ];
    }

    internal static void FindConduits(AgentData agentData, List<CombatItem> combatData)
    {
        var maxHPEventsAgents = combatData
            .Where(x => x.IsStateChange == StateChange.MaxHealthUpdate && MaxHealthUpdateEvent.GetMaxHealth(x) == 15276)
            .Select(x => agentData.GetAgent(x.SrcAgent, x.Time));
        var conduitsGadgets = maxHPEventsAgents
            .Where(x => x.Type == AgentItem.AgentType.Gadget && x.HitboxWidth == 100 && x.HitboxHeight == 200)
            .Distinct();
        var effects = combatData.Where(x => x.IsEffect && agentData.GetAgent(x.SrcAgent, x.Time).IsSpecies(TargetID.EnlightenedConduitCM));
        foreach (var conduitGadget in conduitsGadgets)
        {
            conduitGadget.OverrideID(TargetID.EnlightenedConduitGadget, agentData);
            conduitGadget.OverrideType(AgentItem.AgentType.NPC, agentData);
            var effectByConduitOnGadget = effects
                .Where(x => x.DstMatchesAgent(conduitGadget)).FirstOrDefault();
            if (effectByConduitOnGadget != null)
            {
                conduitGadget.SetMaster(agentData.GetAgent(effectByConduitOnGadget.SrcAgent, effectByConduitOnGadget.Time));
            }
        }
        var bigConduitsGadgets = maxHPEventsAgents
            .Where(x => x.Type == AgentItem.AgentType.Gadget)
            .Distinct();

        foreach (var conduitGadget in conduitsGadgets)
        {
            conduitGadget.OverrideID(TargetID.BigEnlightenedConduitGadget, agentData);
            conduitGadget.OverrideType(AgentItem.AgentType.NPC, agentData);
        }
    }

    internal override void EIEvtcParse(ulong gw2Build, EvtcVersionEvent evtcVersion, LogData logData, AgentData agentData, List<CombatItem> combatData, IReadOnlyDictionary<uint, ExtensionHandler> extensions)
    {
        FindConduits(agentData, combatData);
        base.EIEvtcParse(gw2Build, evtcVersion, logData, agentData, combatData, extensions);
    }

    private static PhaseData GetBoulderPhase(ParsedEvtcLog log, IEnumerable<SingleActor> boulders, string name, SingleActor decima)
    {
        long start = long.MaxValue;
        long end = long.MinValue;
        foreach (SingleActor boulder in boulders) {
            start = Math.Min(boulder.FirstAware, start);
            var deadEvent = log.CombatData.GetDeadEvents(boulder.AgentItem).FirstOrDefault();
            if (deadEvent != null)
            {
                end = Math.Max(deadEvent.Time, end);
            } 
            else
            {
                end = Math.Max(boulder.LastAware, end);
            }
        }
        var phase = new SubPhasePhaseData(start, end, name);
        phase.AddTargets(boulders, log);
        phase.AddTarget(decima, log, PhaseData.TargetPriority.Blocking);
        return phase;
    }

    internal static List<PhaseData> ComputePhases(ParsedEvtcLog log, SingleActor decima, IReadOnlyList<SingleActor> targets, EncounterPhaseData encounterPhase, bool requirePhases)
    {
        if (!requirePhases)
        {
            return [];
        }
        bool isCM = encounterPhase.IsCM;
        var phases = new List<PhaseData>(isCM ? 9 : 5);
        // Invul check
        phases.AddRange(GetPhasesByInvul(log, isCM ? NovaShieldCM : NovaShield, decima, true, true, encounterPhase.Start, encounterPhase.End));
        List<PhaseData> mainPhases = new List<PhaseData>(3);
        var currentMainPhase = 1;
        for (int i = 0; i < phases.Count; i++)
        {
            var phaseIndex = i + 1;
            PhaseData phase = phases[i];
            phase.AddParentPhase(encounterPhase);
            if (phaseIndex % 2 == 0)
            {
                phase.Name = "Split " + (currentMainPhase++);
                if (isCM && i < phases.Count - 1)
                {
                    var nextMainPhase = phases[i + 1];
                    var fractureArmorStatus = decima.GetBuffStatus(log, FracturedArmorCM, nextMainPhase.Start + ServerDelayConstant);
                    if (fractureArmorStatus.Value > 0)
                    {
                        phase.OverrideEnd(fractureArmorStatus.End);
                        nextMainPhase.OverrideStart(fractureArmorStatus.End);
                    }
                }
                // Decima gets nova shield during enrage, not a phase
                if (decima.GetBuffStatus(log, ChargeDecima, phase.Start + ServerDelayConstant).Value < 10)
                {
                    phase.AddTarget(decima, log);
                }
            }
            else
            {
                mainPhases.Add(phase);
                phase.Name = "Phase " + (currentMainPhase);
                phase.AddTarget(decima, log);
            }
        }
        // Final phases + Boulder phases
        if (isCM)
        {
            var finalSeismicJumpEvent = log.CombatData.GetBuffData(SeismicRepositionInvul).FirstOrDefault(x => x is BuffApplyEvent && x.To.Is(decima.AgentItem) && encounterPhase.InInterval(x.Time));
            if (finalSeismicJumpEvent != null)
            {
                var p3 = mainPhases[^1];
                var preFinalPhase = new SubPhasePhaseData(p3.Start, finalSeismicJumpEvent.Time, "40% - 10%");
                preFinalPhase.AddParentPhase(p3);
                preFinalPhase.AddTarget(decima, log);
                phases.Add(preFinalPhase);
                var finalPhaseStartEvent = log.CombatData.GetBuffRemoveAllDataByIDByDst(SeismicRepositionInvul, decima.AgentItem).FirstOrDefault(x => encounterPhase.InInterval(x.Time));
                if (finalPhaseStartEvent != null)
                {
                    var finalPhase = new SubPhasePhaseData(finalPhaseStartEvent.Time, p3.End, "10% - 0%");
                    finalPhase.AddParentPhase(p3);
                    finalPhase.AddTarget(decima, log);
                    phases.Add(finalPhase);
                }
            }
            var sortedBoulders = targets.Where(x => x.IsSpecies(TargetID.TranscendentBoulder) && encounterPhase.InInterval(x.FirstAware)).OrderBy(x => x.FirstAware);
            var firstBoulders = sortedBoulders.Take(new Range(0, 2));
            if (firstBoulders.Any())
            {
                phases.Add(GetBoulderPhase(log, firstBoulders, "Boulders 1", decima).WithParentPhases(mainPhases));
                var secondBoulders = sortedBoulders.Take(new Range(2, 4));
                if (secondBoulders.Any())
                {
                    phases.Add(GetBoulderPhase(log, secondBoulders, "Boulders 2", decima).WithParentPhases(mainPhases));
                }
            }
        }
        return phases;
    }

    internal override List<PhaseData> GetPhases(ParsedEvtcLog log, bool requirePhases)
    {
        List<PhaseData> phases = GetInitialPhase(log);
        SingleActor decima = Targets.FirstOrDefault(x => IsCMTriggerID ? x.IsSpecies(TargetID.DecimaCM) : x.IsSpecies(TargetID.Decima)) ?? throw new MissingKeyActorsException("Decima not found"); ;
        phases[0].AddTarget(decima, log);
        if (IsCMTriggerID)
        {
            AddTargetsToPhase(phases[0], [TargetID.TranscendentBoulder], log, PhaseData.TargetPriority.Blocking);
        }
        phases.AddRange(ComputePhases(log, decima, Targets, (EncounterPhaseData)phases[0], requirePhases));
        
        return phases;
    }


    internal override void ComputeNPCCombatReplayActors(NPC target, ParsedEvtcLog log, CombatReplay replay)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputeNPCCombatReplayActors(target, log, replay);
        }
        (long start, long end) lifespan = (replay.TimeOffsets.start, replay.TimeOffsets.end);

        switch (target.ID)
        {
            case (int)TargetID.Decima:
            case (int)TargetID.DecimaCM:
                var casts = target.GetAnimatedCastEvents(log).ToList();

                // Thrumming Presence - Red Ring around Decima
                var thrummingSegments = target.GetBuffStatus(log, target.IsSpecies(TargetID.DecimaCM) ? ThrummingPresenceBuffCM : ThrummingPresenceBuff)
                    .Where(x => x.Value > 0);
                foreach (var segment in thrummingSegments)
                {
                    replay.Decorations.Add(new CircleDecoration(700, segment.TimeSpan, Colors.Red, 0.2, new AgentConnector(target)).UsingFilled(false));
                }

                // Add the Charge indicator on top right of the replay
                var chargeSegments = target.GetBuffStatus(log, ChargeDecima).Where(x => x.Value > 0);
                foreach (Segment segment in chargeSegments)
                {
                    replay.Decorations.Add(new TextDecoration(segment.TimeSpan, "Decima Charge(s) " + segment.Value + " out of 10", 15, Colors.Red, 1.0, new ScreenSpaceConnector(new Vector2(600, 40))));
                }

                // Mainshock - Pizza Indicator
                if (log.CombatData.TryGetEffectEventsBySrcWithGUIDs(target.AgentItem, [EffectGUIDs.DecimaMainshockIndicator, EffectGUIDs.DecimaMainshockIndicatorCM], out var mainshockSlices))
                {
                    foreach (EffectEvent effect in mainshockSlices)
                    {
                        long duration = 2300;
                        long growing = effect.Time + duration;
                        lifespan = effect.ComputeLifespan(log, duration);
                        var rotation = new AngleConnector(effect.Rotation.Z + 90);
                        var slice = (PieDecoration)new PieDecoration(1200, 32, lifespan, Colors.LightOrange, 0.4, new PositionConnector(effect.Position)).UsingRotationConnector(rotation);
                        replay.Decorations.AddWithBorder(slice, Colors.LightOrange, 0.6);
                    }
                }

                // For some reason the effects all start at the same time
                // We sequence them using the skill cast
                var foreshock = casts.Where(x => x.SkillID == Foreshock || x.SkillID == ForeshockCM1 || x.SkillID == ForeshockCM2 || x.SkillID == ForeshockCM3 || x.SkillID == ForeshockCM4);
                foreach (var cast in foreshock)
                {
                    (long start, long end) = (cast.Time, cast.Time + cast.ActualDuration + 3000); // 3s padding as safety
                    long nextStartTime = 0;

                    // Decima's Left Side
                    if (log.CombatData.TryGetEffectEventsBySrcWithGUIDs(target.AgentItem, [EffectGUIDs.DecimaForeshockLeft, EffectGUIDs.DecimaForeshockLeftCM], out var foreshockLeft))
                    {
                        foreach (EffectEvent effect in foreshockLeft.Where(x => x.Time >= start && x.Time < end))
                        {
                            lifespan = effect.ComputeLifespan(log, 1967);
                            nextStartTime = lifespan.end;
                            var rotation = new AngleConnector(effect.Rotation.Z + 90);
                            var leftHalf = (PieDecoration)new PieDecoration(1185, 180, lifespan, Colors.LightOrange, 0.4, new PositionConnector(effect.Position)).UsingRotationConnector(rotation);
                            replay.Decorations.AddWithBorder(leftHalf, Colors.LightOrange, 0.6);
                        }
                    }

                    // Decima's Right Side
                    if (log.CombatData.TryGetEffectEventsBySrcWithGUIDs(target.AgentItem, [EffectGUIDs.DecimaForeshockRight, EffectGUIDs.DecimaForeshockRightCM], out var foreshockRight))
                    {
                        foreach (EffectEvent effect in foreshockRight.Where(x => x.Time >= start && x.Time < end))
                        {
                            lifespan = effect.ComputeLifespan(log, 3000);
                            lifespan.start = nextStartTime - 700; // Trying to match in game timings
                            nextStartTime = lifespan.end;
                            var rotation = new AngleConnector(effect.Rotation.Z + 90);
                            var rightHalf = (PieDecoration)new PieDecoration(1185, 180, lifespan, Colors.LightOrange, 0.4, new PositionConnector(effect.Position)).UsingRotationConnector(rotation);
                            replay.Decorations.AddWithBorder(rightHalf, Colors.LightOrange, 0.6);
                        }
                    }

                    // Decima's Frontal
                    if (log.CombatData.TryGetEffectEventsBySrcWithGUIDs(target.AgentItem, [EffectGUIDs.DecimaForeshockFrontal, EffectGUIDs.DecimaForeshockFrontalCM], out var foreshockFrontal))
                    {
                        foreach (EffectEvent effect in foreshockFrontal.Where(x => x.Time >= start && x.Time < end))
                        {
                            lifespan = effect.ComputeLifespan(log, 5100);
                            lifespan.start = nextStartTime;
                            var frontalCircle = new CircleDecoration(600, lifespan, Colors.LightOrange, 0.4, new PositionConnector(effect.Position));
                            replay.Decorations.AddWithBorder(frontalCircle, Colors.LightOrange, 0.6);
                        }
                    }
                }

                // Earthrend - Outer Sliced Doughnut - 8 Slices
                if (log.CombatData.TryGetGroupedEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.DecimaEarthrendDoughnutSlice, out var earthrend))
                {
                    // Since we don't have a decoration shaped like this, we regroup the 8 effects and use Decima position as the center for a doughnut sliced by lines.
                    foreach (List<EffectEvent> group in earthrend)
                    {
                        uint inner = 1200;
                        uint outer = 3000;
                        int lineAngle = 45;
                        var offset = new Vector3(0, inner + (outer - inner) / 2, 0);

                        // Unlike Seismic Crash, the indicator effect end and the damage effect are only ~5ms apart
                        lifespan = group[0].ComputeLifespan(log, 2800);
                        if (target.TryGetCurrentFacingDirection(log, group[0].Time, out Vector3 facing, 100))
                        {
                            for (int i = 0; i < 360; i += lineAngle)
                            {
                                var rotation = facing.GetRoundedZRotationDeg() + i;
                                var line = new RectangleDecoration(10, outer - inner, lifespan, Colors.LightOrange, 0.6, new AgentConnector(target).WithOffset(offset, true)).UsingRotationConnector(new AngleConnector(rotation));
                                replay.Decorations.Add(line);
                            }
                        }

                        var doughnut = new DoughnutDecoration(inner, outer, lifespan, Colors.LightOrange, 0.2, new AgentConnector(target));
                        replay.Decorations.AddWithBorder(doughnut, Colors.LightOrange, 0.6);
                    }
                }

                // Seismic Crash & Seismic Reposition - Jump with rings
                if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.DecimaSeismicCrashRings, out var seismicCrashReposition))
                {
                    foreach (var effect in seismicCrashReposition)
                    {
                        // The effect lasts ~1110ms longer than the hit of damage, so we use the damage as end time.
                        lifespan = effect.ComputeLifespanWithSecondaryEffect(log, EffectGUIDs.DecimaSeismicCrashHit);
                        replay.Decorations.AddContrenticRings(300, 140, lifespan, effect.Position, Colors.LightOrange, 0.30f, 6, false);
                    }
                }

                // Death zones - Earthrend, Seismic Crash and Seismic Reposition
                if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.DecimaJumpAoEUnderneath, out var deathZone))
                {
                    foreach (EffectEvent effect in deathZone)
                    {
                        // Effect duration lasting longer than necessay
                        // Depending on the mechanic the difference between logged duration and the damage effect is ~300ms to ~1120ms.
                        lifespan = effect.ComputeLifespanWithSecondaryEffects(log, [EffectGUIDs.DecimaEarthrendHit1, EffectGUIDs.DecimaSeismicCrashHit]);
                        var zone = new CircleDecoration(300, lifespan, Colors.Red, 0.2, new PositionConnector(effect.Position));
                        replay.Decorations.AddWithGrowing(zone, lifespan.end);
                    }
                }

                // Aftershock - Moving AoEs - 4 Cascades
                if (log.CombatData.TryGetGroupedEffectEventsBySrcWithGUIDs(target.AgentItem, [EffectGUIDs.DecimaAftershockAoECM, EffectGUIDs.DecimaAftershockAoE], out var aftershock, 12000))
                {
                    // All the AoEs take roughly 11-12 seconds to appear
                    // There are 10 AoEs of radius 200, then 10 of 240, 10 of 280 and 10 of 320. When they bounce back to Decima they restart at 200 radius.
                    uint radius = 200;
                    float distance = 0;
                    EffectEvent first = aftershock.First().First();
                    long groupStartTime = first.Time;

                    // Because the x9th and the x0th can happen at the same timestamp, we need to check the distance of the from Decima.
                    // A simple increase every 10 can happen to increase the x9th instead of the following x0th.
                    if (target.TryGetCurrentPosition(log, first.Time, out Vector3 decimaPosition))
                    {
                        foreach (var group in aftershock)
                        {
                            foreach (var effect in group)
                            {
                                distance = (effect.Position - decimaPosition).XY().Length();
                                if (distance > 1074 && distance < 1076 || distance > 1759 && distance < 1761)
                                {
                                    radius = 200;
                                }
                                if (distance > 1324 && distance < 1326 || distance > 1528 && distance < 1530)
                                {
                                    radius = 240;
                                }
                                if (distance > 1574 && distance < 1576 || distance > 1297 && distance < 1299)
                                {
                                    radius = 280;
                                }
                                if (distance > 1824 && distance < 1826 || distance > 1066 && distance < 1068)
                                {
                                    radius = 320;
                                }
                                lifespan = effect.ComputeLifespan(log, 1500);
                                var zone = (CircleDecoration)new CircleDecoration(radius, lifespan, Colors.Red, 0.2, new PositionConnector(effect.Position)).UsingFilled(false);
                                replay.Decorations.Add(zone);
                            }
                        }
                    }
                }

                // Flux Nova - Breakbar
                var breakbarUpdates = target.GetBreakbarPercentUpdates(log);
                var (breakbarNones, breakbarActives, breakbarImmunes, breakbarRecoverings) = target.GetBreakbarStatus(log);
                foreach (var segment in breakbarActives)
                {
                    replay.Decorations.AddActiveBreakbar(segment.TimeSpan, target, breakbarUpdates);
                }
                break;
            case (int)TargetID.GreenOrb1Player:
            case (int)TargetID.GreenOrb1PlayerCM:
                // Green Circle
                replay.Decorations.Add(new CircleDecoration(90, lifespan, Colors.DarkGreen, 0.2, new AgentConnector(target)));

                // Overhead Number
                replay.Decorations.AddOverheadIcon(lifespan, target, ParserIcons.GreenMarkerSize1Overhead);

                // Hp Bar
                var hpUpdatesOrb1 = target.GetHealthUpdates(log);
                replay.Decorations.Add(
                    new OverheadProgressBarDecoration(CombatReplayOverheadProgressBarMinorSizeInPixel, lifespan, Colors.SligthlyDarkGreen, 0.8, Colors.Black, 0.6, hpUpdatesOrb1.Select(x => (x.Start, x.Value)).ToList(), new AgentConnector(target))
                    .UsingInterpolationMethod(Connector.InterpolationMethod.Step)
                    .UsingRotationConnector(new AngleConnector(180))
                );
                break;
            case (int)TargetID.GreenOrb2Players:
            case (int)TargetID.GreenOrb2PlayersCM:
                // Green Circle
                replay.Decorations.Add(new CircleDecoration(185, lifespan, Colors.DarkGreen, 0.2, new AgentConnector(target)));

                // Overhead Number
                replay.Decorations.AddOverheadIcon(lifespan, target, ParserIcons.GreenMarkerSize2Overhead);

                // Hp Bar
                var hpUpdatesOrb2 = target.GetHealthUpdates(log);
                replay.Decorations.Add(
                    new OverheadProgressBarDecoration(CombatReplayOverheadProgressBarMinorSizeInPixel, lifespan, Colors.SligthlyDarkGreen, 0.8, Colors.Black, 0.6, hpUpdatesOrb2.Select(x => (x.Start, x.Value)).ToList(), new AgentConnector(target))
                    .UsingInterpolationMethod(Connector.InterpolationMethod.Step)
                    .UsingRotationConnector(new AngleConnector(180))
                );
                break;
            case (int)TargetID.GreenOrb3Players:
            case (int)TargetID.GreenOrb3PlayersCM:
                // Green Circle
                replay.Decorations.Add(new CircleDecoration(285, lifespan, Colors.DarkGreen, 0.2, new AgentConnector(target)));

                // Overhead Number
                replay.Decorations.AddOverheadIcon(lifespan, target, ParserIcons.GreenMarkerSize3Overhead);

                // Hp Bar
                var hpUpdatesOrb3 = target.GetHealthUpdates(log);
                replay.Decorations.Add(
                    new OverheadProgressBarDecoration(CombatReplayOverheadProgressBarMinorSizeInPixel, lifespan, Colors.SligthlyDarkGreen, 0.8, Colors.Black, 0.6, hpUpdatesOrb3.Select(x => (x.Start, x.Value)).ToList(), new AgentConnector(target))
                    .UsingInterpolationMethod(Connector.InterpolationMethod.Step)
                    .UsingRotationConnector(new AngleConnector(180))
                );
                break;
            case (int)TargetID.EnlightenedConduit:
                AddThunderAoE(target, log, replay, target.AgentItem);
                AddEnlightenedConduitDecorations(log, target, replay, FluxlanceTargetBuff1, DecimaConduitWallWarningBuffCM, DecimaConduitWallBuff);
                break;
            case (int)TargetID.EnlightenedConduitCM:
                AddEnlightenedConduitDecorations(log, target, replay, FluxlanceTargetBuffCM1, DecimaConduitWallWarningBuff, DecimaConduitWallBuffCM);
                break;
            case (int)TargetID.EnlightenedConduitGadget:
                AgentConnector gadgetEffectConnector;
                List<long> chargeTierBuffs = [EnlightenedConduitGadgetChargeTier1Buff, EnlightenedConduitGadgetChargeTier2Buff, EnlightenedConduitGadgetChargeTier3Buff];
                List<uint> chargeRadius = [100, 200, 400];
                List<string> chargeIcons = [ParserIcons.TargetOrder1Overhead, ParserIcons.TargetOrder2Overhead, ParserIcons.TargetOrder3Overhead];
                if (target.AgentItem.Master != null)
                {
                    gadgetEffectConnector = new AgentConnector(target.AgentItem.Master);
                    chargeTierBuffs = [EnlightenedConduitGadgetChargeTier1BuffCM, EnlightenedConduitGadgetChargeTier2BuffCM, EnlightenedConduitGadgetChargeTier3BuffCM];
                    // Chorus of Thunder / Discordant Thunder - Orange AoE
                    AddThunderAoE(target, log, replay, target.AgentItem.Master);
                }
                else
                {
                    gadgetEffectConnector = new AgentConnector(target.AgentItem);
                }
                // Fulgent Aura - Tier Charges
                for (int i = 0; i < chargeTierBuffs.Count; i++)
                {
                    var tier = target.GetBuffStatus(log, chargeTierBuffs[i]);
                    foreach (var segment in tier.Where(x => x.Value > 0))
                    {
                        replay.Decorations.AddWithBorder(new CircleDecoration(chargeRadius[i], segment.TimeSpan, Colors.DarkPurple, 0.4, gadgetEffectConnector), Colors.Red, 0.4);
                        replay.Decorations.AddOverheadIcon(segment.TimeSpan, gadgetEffectConnector.Agent, chargeIcons[i]);
                    }
                }
                break;
            case (int)TargetID.DecimaBeamStart:
                const uint beamLength = 3900;
                const uint orangeBeamWidth = 80;
                const uint redBeamWidth = 160;
                var orangeBeams = GetBuffApplyRemoveSequence(log.CombatData, DecimaBeamTargeting, target.AgentItem, true, true);
                AddBeamWarning(log, target, replay, DecimaBeamLoading, orangeBeamWidth, beamLength, orangeBeams.OfType<BuffApplyEvent>(), Colors.LightOrange);
                AddBeam(log, replay, orangeBeamWidth, orangeBeams, Colors.LightOrange);

                var redBeams = GetBuffApplyRemoveSequence(log.CombatData, DecimaRedBeamTargeting, target.AgentItem, true, true);
                AddBeamWarning(log, target, replay, DecimaRedBeamLoading, redBeamWidth, beamLength, redBeams.OfType<BuffApplyEvent>(), Colors.Red);
                AddBeam(log, replay, redBeamWidth, redBeams, Colors.Red);
                break;
            case (int)TargetID.DecimaBeamStartCM:
                const uint beamLengthCM = 3900;
                const uint orangeBeamWidthCM = 80;
                const uint redBeamWidthCM = 160;
                var orangeBeamsCM = GetBuffApplyRemoveSequence(log.CombatData, DecimaBeamTargetingCM, target.AgentItem, true, true);
                AddBeamWarning(log, target, replay, DecimaBeamLoadingCM1, orangeBeamWidthCM, beamLengthCM, orangeBeamsCM.OfType<BuffApplyEvent>(), Colors.LightOrange);
                AddBeamWarning(log, target, replay, DecimaBeamLoadingCM2, orangeBeamWidthCM, beamLengthCM, orangeBeamsCM.OfType<BuffApplyEvent>(), Colors.LightOrange);
                AddBeam(log, replay, orangeBeamWidthCM, orangeBeamsCM, Colors.LightOrange);

                var redBeamsCM = GetBuffApplyRemoveSequence(log.CombatData, DecimaRedBeamTargetingCM, target.AgentItem, true, true);
                AddBeamWarning(log, target, replay, DecimaRedBeamLoadingCM1, redBeamWidthCM, beamLengthCM, redBeamsCM.OfType<BuffApplyEvent>(), Colors.Red);
                AddBeamWarning(log, target, replay, DecimaRedBeamLoadingCM2, redBeamWidthCM, beamLengthCM, redBeamsCM.OfType<BuffApplyEvent>(), Colors.Red);
                AddBeam(log, replay, redBeamWidthCM, redBeamsCM, Colors.Red);
                break;
            case (int)TargetID.TranscendentBoulder:
                foreach (CastEvent cast in target.GetAnimatedCastEvents(log))
                {
                    switch (cast.SkillID)
                    {
                        // Sparking Reverberation - Breakbar
                        case SparkingReverberation:
                            if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.DecimaSparkingReverberation, out var effects))
                            {
                                foreach (var effect in effects.Where(x => x.Time > cast.Time && x.Time < cast.Time + 2000)) // 2000ms margin
                                {
                                    uint radius = 800;
                                    long warningDuration = effect.Time - cast.Time;
                                    (long start, long end) lifespanWarning = (cast.Time, effect.Time);
                                    (long start, long end) lifespanDamage = effect.ComputeDynamicLifespan(log, 30000);
                                    lifespanWarning.end = ComputeEndCastTimeByBuffApplication(log, target, Stun, cast.Time, warningDuration); // Cast can be interrupted

                                    var warningIndicator = new CircleDecoration(radius, lifespanWarning, Colors.LightOrange, 0.2, new PositionConnector(effect.Position));
                                    replay.Decorations.AddWithGrowing(warningIndicator, effect.Time);

                                    var damageField = new CircleDecoration(radius, lifespanDamage, Colors.LightBlue, 0.1, new PositionConnector(effect.Position));
                                    replay.Decorations.Add(damageField);
                                }
                            }
                            break;
                        // Sparkwave - Cone
                        case Sparkwave:
                            long castDuration = 1800;
                            (long start, long end) lifespanSparkwave = (cast.Time, Math.Min(cast.EndTime, cast.Time + castDuration));
                            if (target.TryGetCurrentFacingDirection(log, cast.Time + castDuration, out var facing))
                            {
                                var cone = (PieDecoration)new PieDecoration(6000, 120, lifespanSparkwave, Colors.LightOrange, 0.2, new AgentConnector(target)).UsingRotationConnector(new AngleConnector(facing));
                                replay.Decorations.AddWithGrowing(cone, cast.Time + castDuration);
                            }
                            break;
                        default:
                            break;
                    }
                }

                // Charged Ground - AoEs from Sparkwave
                if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.DecimaChargedGroundBorder, out var chargedGround))
                {
                    foreach (var effect in chargedGround)
                    {
                        (long start, long end) lifespanChargedGround = (effect.Time, effect.Time + effect.Duration);
                        var circle = new CircleDecoration(400, lifespanChargedGround, Colors.CobaltBlue, 0.2, new PositionConnector(effect.Position));
                        replay.Decorations.AddWithBorder(circle, Colors.Red, 0.2);
                    }
                }

                // Charged Ground - AoEs from Sparkwave - Max Charge
                if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.DecimaChargedGroundMax, out var chargedGroundMax))
                {
                    foreach (var effect in chargedGroundMax)
                    {
                        (long start, long end) lifespanChargedGround = effect.ComputeLifespan(log, 600000);
                        var circle = new CircleDecoration(400, lifespanChargedGround, Colors.Red, 0.4, new PositionConnector(effect.Position));
                        replay.Decorations.Add(circle);
                    }
                }
                break;
            default:
                break;
        }
    }

    private static void AddEnlightenedConduitDecorations(ParsedEvtcLog log, SingleActor target, CombatReplay replay, long fluxLanceTargetBuffID, long wallWarningBuffID, long wallBuffID)
    {

        // Focused Fluxlance - Green Arrow from Decima to the Conduit
        var greenArrow = GetBuffApplyRemoveSequence(log.CombatData, fluxLanceTargetBuffID, target, true, true).Where(x => x is BuffApplyEvent);
        foreach (var apply in greenArrow)
        {
            replay.Decorations.Add(new LineDecoration((apply.Time, apply.Time + 5500), Colors.DarkGreen, 0.2, new AgentConnector(apply.To), new AgentConnector(apply.By)).WithThickess(80, true));
            replay.Decorations.Add(new LineDecoration((apply.Time + 5500, apply.Time + 6500), Colors.DarkGreen, 0.5, new AgentConnector(apply.To), new AgentConnector(apply.By)).WithThickess(80, true));
        }

        // Warning indicator of walls spawning between Conduits.
        var wallsWarnings = GetBuffApplyRemoveSequence(log.CombatData, wallWarningBuffID, target, true, true);
        replay.Decorations.AddTether(wallsWarnings, Colors.Red, 0.2, 30, true);

        // Walls connecting Conduits to each other.
        var walls = GetBuffApplyRemoveSequence(log.CombatData, wallBuffID, target, true, true);
        replay.Decorations.AddTether(walls, Colors.Purple, 0.4, 60, true);
    }

    private static void AddBeam(ParsedEvtcLog log, CombatReplay replay, uint beamWidth, IEnumerable<BuffEvent> beams, Color color)
    {
        int tetherStart = 0;
        AgentItem beamEndAgent = _unknownAgent;
        AgentItem beamStartAgent = _unknownAgent;
        foreach (BuffEvent tether in beams)
        {
            if (tether is BuffApplyEvent)
            {
                tetherStart = (int)tether.Time;
                beamEndAgent = tether.By;
                beamStartAgent = tether.To;
            }
            else if (tether is BuffRemoveAllEvent)
            {
                int tetherEnd = (int)tether.Time;
                if (!beamEndAgent.IsUnknown && !beamStartAgent.IsUnknown)
                {
                    // Get the final position for beam end
                    if (beamEndAgent.TryGetCurrentInterpolatedPosition(log, tetherEnd, out var posSrc))
                    {
                        // Get the position before movement happened for beam start
                        if (beamStartAgent.TryGetCurrentInterpolatedPosition(log, tetherStart - 500, out var posDst))
                        {
                            replay.Decorations.Add(new LineDecoration((tetherStart, tetherEnd), color, 0.5, new PositionConnector(posSrc), new PositionConnector(posDst)).WithThickess(beamWidth, true));
                        }
                        beamEndAgent = _unknownAgent;
                        beamStartAgent = _unknownAgent;
                    }
                }
            }
        }
    }

    private static void AddBeamWarning(ParsedEvtcLog log, SingleActor target, CombatReplay replay, long buffID, uint beamWidth, uint beamLength, IEnumerable<BuffApplyEvent> beamFireds, Color color)
    {
        var beamWarnings = target.GetBuffStatus(log, buffID);
        foreach (var beamWarning in beamWarnings)
        {
            if (beamWarning.Value > 0)
            {
                long start = beamWarning.Start;
                long end = beamFireds.FirstOrDefault(x => x.Time >= start)?.Time ?? beamWarning.End;
                // We ignore the movement of the agent, it moves closer to target before firing
                if (target.TryGetCurrentInterpolatedPosition(log, start, out var posDst))
                {
                    var connector = new PositionConnector(posDst).WithOffset(new(beamLength / 2, 0, 0), true);
                    var rotationConnector = new AgentFacingConnector(target);
                    replay.Decorations.Add(new RectangleDecoration(beamLength, beamWidth, (start, end), color, 0.2, connector).UsingRotationConnector(rotationConnector));
                }
            }
        }
    }


    internal override void ComputePlayerCombatReplayActors(PlayerActor player, ParsedEvtcLog log, CombatReplay replay)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputePlayerCombatReplayActors(player, log, replay);
        }

        // Target Overhead
        // In phase 2 you get the Fluxlance Target Buff but also Target Order, in game only Target Order is displayed overhead, so we filter those out.
        var p2Targets = player.GetBuffStatus(log, [TargetOrder1JW, TargetOrder2JW, TargetOrder3JW, TargetOrder4JW, TargetOrder5JW]).Where(x => x.Value > 0);
        var allTargets = player.GetBuffStatus(log, FluxlanceTargetBuff1).Where(x => x.Value > 0);
        var filtered = allTargets.Where(x => !p2Targets.Any(y => Math.Abs(x.Start - y.Start) < ServerDelayConstant));
        foreach (var segment in filtered)
        {
            replay.Decorations.AddOverheadIcon(segment, player, ParserIcons.TargetOverhead);
        }

        // Target Order Overhead
        replay.Decorations.AddOverheadIcons(player.GetBuffStatus(log, TargetOrder1JW).Where(x => x.Value > 0), player, ParserIcons.TargetOrder1Overhead);
        replay.Decorations.AddOverheadIcons(player.GetBuffStatus(log, TargetOrder2JW).Where(x => x.Value > 0), player, ParserIcons.TargetOrder2Overhead);
        replay.Decorations.AddOverheadIcons(player.GetBuffStatus(log, TargetOrder3JW).Where(x => x.Value > 0), player, ParserIcons.TargetOrder3Overhead);
        replay.Decorations.AddOverheadIcons(player.GetBuffStatus(log, TargetOrder4JW).Where(x => x.Value > 0), player, ParserIcons.TargetOrder4Overhead);
        replay.Decorations.AddOverheadIcons(player.GetBuffStatus(log, TargetOrder5JW).Where(x => x.Value > 0), player, ParserIcons.TargetOrder5Overhead);

        // Chorus of Thunder / Discordant Thunder - Orange AoE
        AddThunderAoE(player, log, replay, player.AgentItem);
    }
    internal override void ComputeEnvironmentCombatReplayDecorations(ParsedEvtcLog log, CombatReplayDecorationContainer environmentDecorations)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputeEnvironmentCombatReplayDecorations(log, environmentDecorations);
        }
    }
    /// <summary>
    /// Chorus of Thunder / Discordant Thunder - Orange spread AoE on players or on Conduits.
    /// </summary>
    private static void AddThunderAoE(SingleActor actor, ParsedEvtcLog log, CombatReplay replay, AgentItem decorationOn)
    {
        if (log.CombatData.TryGetEffectEventsByDstWithGUID(actor.AgentItem, EffectGUIDs.DecimaChorusOfThunderAoE, out var thunders))
        {
            var connector = new AgentConnector(decorationOn);
            foreach (var effect in thunders)
            {
                long duration = 5000;
                long growing = effect.Time + duration;
                (long start, long end) lifespan = effect.ComputeLifespan(log, duration);
                replay.Decorations.AddWithGrowing(new CircleDecoration(285, lifespan, Colors.LightOrange, 0.2, connector), growing);
            }
        }
    }

    internal override LogData.LogMode GetLogMode(CombatData combatData, AgentData agentData, LogData logData)
    {
        return IsCMTriggerID ? LogData.LogMode.CMNoName : LogData.LogMode.Normal;
    }

    internal override void SetInstanceBuffs(ParsedEvtcLog log, List<InstanceBuff> instanceBuffs)
    {
        if (!log.LogData.IsInstance)
        {
            base.SetInstanceBuffs(log, instanceBuffs);
        }
        var encounterPhases = log.LogData.GetPhases(log).OfType<EncounterPhaseData>().Where(x => x.LogID == LogID);
        foreach (var encounterPhase in encounterPhases)
        {
            if (encounterPhase.Success && encounterPhase.IsCM)
            {
                var decima = encounterPhase.Targets.Keys.FirstOrDefault(x => x.IsSpecies(TargetID.DecimaCM));
                if (decima != null && !decima.GetBuffStatus(log, ChargeDecima).Any(x => x.Value > 0))
                {
                    instanceBuffs.Add(new(log.Buffs.BuffsByIDs[AchievementEligibilityCalmBeforeTheStorm], 1, encounterPhase));
                }
            }
        }
    }
}

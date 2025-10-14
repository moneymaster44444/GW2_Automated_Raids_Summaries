﻿using System.Numerics;
using GW2EIEvtcParser.EIData;
using GW2EIEvtcParser.Exceptions;
using GW2EIEvtcParser.Extensions;
using GW2EIEvtcParser.ParsedData;
using GW2EIEvtcParser.ParserHelpers;
using static GW2EIEvtcParser.ArcDPSEnums;
using static GW2EIEvtcParser.LogLogic.LogLogicUtils;
using static GW2EIEvtcParser.LogLogic.LogLogicPhaseUtils;
using static GW2EIEvtcParser.LogLogic.LogLogicTimeUtils;
using static GW2EIEvtcParser.ParserHelper;
using static GW2EIEvtcParser.ParserHelpers.LogImages;
using static GW2EIEvtcParser.SkillIDs;
using static GW2EIEvtcParser.SpeciesIDs;

namespace GW2EIEvtcParser.LogLogic;

internal class GreerTheBlightbringer : MountBalrior
{
    private static readonly long[] ReflectableProjectiles = [BlobOfBlight, BlobOfBlight2, ScatteringSporeblast, RainOfSpores]; // Legacy, no longer reflectable.
    private static readonly long[] Boons =
    [
        Aegis, Alacrity, Fury, Might, Protection, Quickness, Regeneration, Resistance, Resolution, Stability, Swiftness, Vigor
    ];

    internal readonly MechanicGroup Mechanics = new MechanicGroup([
            new MechanicGroup([
                new PlayerSrcHealthDamageHitMechanic(ReflectableProjectiles, new MechanicPlotlySetting(Symbols.YDown, Colors.Pink), "ProjRefl.Greer.H", "Reflected projectiles have hit Greer", "Reflected Projectile Hit (Greer)", 0)
                    .UsingChecker((hde, log) => hde.To.IsSpecies(TargetID.Greer)).WithBuilds(GW2Builds.November2024MountBalriorRelease, GW2Builds.December2024MountBalriorNerfs),
                new PlayerSrcHealthDamageHitMechanic(ReflectableProjectiles, new MechanicPlotlySetting(Symbols.YDown, Colors.Purple), "ProjRefl.Reeg.H", "Reflected projectiles have hit Reeg", "Reflected Projectile Hit (Reeg)", 0)
                    .UsingChecker((hde, log) => hde.To.IsSpecies(TargetID.Reeg)).WithBuilds(GW2Builds.November2024MountBalriorRelease, GW2Builds.December2024MountBalriorNerfs),
                new PlayerSrcHealthDamageHitMechanic(ReflectableProjectiles, new MechanicPlotlySetting(Symbols.YDown, Colors.LightPurple), "ProjRefl.Gree.H", "Reflected projectiles have hit Gree", "Reflected Projectile Hit (Gree)", 0)
                    .UsingChecker((hde, log) => hde.To.IsSpecies(TargetID.Gree)).WithBuilds(GW2Builds.November2024MountBalriorRelease, GW2Builds.December2024MountBalriorNerfs),
            ]),
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic([RotTheWorld, RotTheWorldCM], new MechanicPlotlySetting(Symbols.Star, Colors.Teal), "RotWorld.H", "Hit by Rot the World (Breakbar AoEs)", "Rot the World Hit", 0),
                new PlayerDstHealthDamageHitMechanic([RakeTheRot, RakeTheRot2, RakeTheRot3], new MechanicPlotlySetting(Symbols.PentagonOpen, Colors.LightBlue), "Rake.H", "Hit by Rake the Rot", "Rake the Rot Hit", 0),
                new MechanicGroup([
                    new PlayerDstHealthDamageHitMechanic([EruptionOfRot, EruptionOfRot2, EruptionOfRot3, EruptionOfRot4, EruptionOfRot5, EruptionOfRot6], new MechanicPlotlySetting(Symbols.Hexagram, Colors.GreenishYellow), "ErupRot.H", "Hit by Eruption of Rot", "Eruption of Rot Hit", 0),
                    new PlayerDstHealthDamageHitMechanic([RotEruption, RotEruptionCM], new MechanicPlotlySetting(Symbols.TriangleSEOpen, Colors.DarkBlue), "RotErup", "Hit by Rot Eruption", "Rot Eruption Hit", 0),
                    new PlayerDstBuffApplyMechanic(EruptionOfRotBuff, new MechanicPlotlySetting(Symbols.StarTriangleDown, Colors.LightBrown), "ErupRot.S", "Stood in Eruption of Rot (Green)", "Stood in Eruption of Rot", 0),
                    new PlayerDstBuffApplyMechanic(EruptionOfRotBuff, new MechanicPlotlySetting(Symbols.StarTriangleDownOpen, Colors.LightBrown), "ErupRot.Dwn", "Downed by stacking Eruption of Rot (Green)", "Downed by Eruption of Rot", 0)
                        .UsingChecker((bae, log) => bae.To.IsDowned(log, bae.Time)),
                    new PlayerDstEffectMechanic([EffectGUIDs.GreerEruptionOfRotGreen, EffectGUIDs.GreerEruptionOfRotGreen2, EffectGUIDs.GreerEruptionofRotGreen3], new MechanicPlotlySetting(Symbols.Circle, Colors.Green), "ErupRot.T", "Targeted by Eruption of Rot (Green)", "Eruption of Rot (Green)", 0),
                ]),
                new PlayerDstHealthDamageHitMechanic([RipplesOfRot, RipplesOfRot2, RipplesOfRotCM, RipplesOfRotCM2], new MechanicPlotlySetting(Symbols.StarSquareOpenDot, Colors.Chocolate), "RippRot.H", "Hit by Ripples of Rot", "Ripples of Rot Hit", 0),
                new PlayerDstBuffApplyMechanic(PlagueRot, new MechanicPlotlySetting(Symbols.YDown, Colors.Red), "PlagueRot", "Received Plague Rot", "Plague Rot", 0),
                new PlayerDstBuffApplyMechanic(PlagueRot, new MechanicPlotlySetting(Symbols.YDown, Colors.Yellow), "Unplagued.Achiv", "Achievement Eligibility: Guaranteed Plague Free", "Achiv Unplagued", 0)
                    .UsingEnable(log => log.LogData.IsCM).UsingAchievementEligibility(),
            ]),
            new PlayerDstHealthDamageHitMechanic(WaveOfCorruption, new MechanicPlotlySetting(Symbols.HourglassOpen, Colors.LightRed), "WaveCor.H", "Hit by Wave of Corruption", "Wave of Corruption Hit", 0),
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic([SeedsOfDecay, SeedsOfDecay2, SeedsOfDecay3], new MechanicPlotlySetting(Symbols.TriangleLeftOpen, Colors.Sand), "SeedsDec.H", "Hit by Seeds of Decay (Greer's Armor Falling)", "Seeds of Decay Hit", 0),
                new PlayerDstHealthDamageHitMechanic([CageOfDecay, CageOfDecay2, CageOfDecay3, CageOfDecay4, CageOfDecay5], new MechanicPlotlySetting(Symbols.Hourglass, Colors.LightPurple), "Cage.H", "Hit by Cage of Decay", "Cage of Decay Hit", 0),
            ]),
            new EnemyCastStartMechanic(TheWorldEndsInDecay, new MechanicPlotlySetting(Symbols.X, Colors.DarkRed), "Enrage", "The World Ends in Decay (Enrage)", "The World Ends in Decay (Enrage)", 0),
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic([RainOfSpores, RainOfSpores2], new MechanicPlotlySetting(Symbols.Hexagon, Colors.Green), "RainSpore.H", "Hit by Rain of Spores", "Rain of Spores Hit", 0),
                new PlayerDstHealthDamageHitMechanic([ScatteringSporeblast, ScatteringSporeblast2], new MechanicPlotlySetting(Symbols.SquareOpen, Colors.GreenishYellow), "ScatSpore.H", "Hit by Scattering Sporeblast", "Scattering Sporeblast Hit", 0),
            ]),
            new MechanicGroup([
                new PlayerDstBuffApplyMechanic(InfectiousRotBuff, new MechanicPlotlySetting(Symbols.CircleX, Colors.Red), "InfRot.T", "Targeted by Infectious Rot (Hit by Noxious Blight)", "Infectious Rot Target", 0),
                new PlayerDstHealthDamageHitMechanic([NoxiousBlight, NoxiousBlight2, NoxiousBlightCM, NoxiousBlightCM2], new MechanicPlotlySetting(Symbols.TriangleNEOpen, Colors.DarkPink), "NoxBlight.H", "Hit by Noxious Blight", "Noxious Blight Hit", 0),
            ]),
            new PlayerDstHealthDamageHitMechanic([EnfeeblingMiasma, EnfeeblingMiasma2, EnfeeblingMiasma3, EnfeeblingMiasma4], new MechanicPlotlySetting(Symbols.TriangleDown, Colors.LightPurple), "EnfMiasma.H", "Hit by Enfeebling Miasma", "Enfeebling Miasma Hit", 0),
            new PlayerDstHealthDamageHitMechanic([AuraOfCorruptionDamage_ReegGreeEreg, AuraOfCorruptionDamage_Greer], new MechanicPlotlySetting(Symbols.CircleOpenDot, Colors.Purple), "AuraCorr.H", "Hit by Aura of Corruption (Hitbox)", "Aura of Corruption Hit", 0),
            new PlayerDstHealthDamageHitMechanic([SweepTheMold, SweepTheMold2, SweepTheMold3], new MechanicPlotlySetting(Symbols.PentagonOpen, Colors.Blue), "Sweep.H", "Hit by Sweep the Mold", "Sweep the Mold Hit", 0),
            new MechanicGroup([
                new PlayerDstHealthDamageHitMechanic([BlobOfBlight, BlobOfBlight2, BlobOfBlight3], new MechanicPlotlySetting(Symbols.Star, Colors.CobaltBlue), "BlobBlight.H", "Hit by Blob of Blight", "Blob of Blight Hit", 0),
                new PlayerDstBuffApplyMechanic(TargetBuff, new MechanicPlotlySetting(Symbols.CircleXOpen, Colors.LightBlue), "BlobBlight.T", "Targeted by Blob of Blight", "Blob of Blight Target", 0),
            ]),
            new PlayerDstHealthDamageHitMechanic([StompTheGrowth, StompTheGrowth2, StompTheGrowth3], new MechanicPlotlySetting(Symbols.CircleOpen, Colors.LightOrange), "Stomp.H", "Hit by Stomp the Growth", "Stomp the Growth Hit", 0),
            new PlayerDstBuffRemoveMechanic(Boons, new MechanicPlotlySetting(Symbols.Octagon, Colors.Purple), "BoonCorrupt", "Boons corrupted (any)", "Boons Corrupted", 100)
                .UsingChecker((brae, log) => brae.By.IsAnySpecies([(int)TargetID.Greer, (int)TargetID.Gree, (int)TargetID.Reeg, (int)TargetID.Ereg])),
            new EnemyDstBuffApplyMechanic(EmpoweredGreer, new MechanicPlotlySetting(Symbols.YUp, Colors.Red), "Empowered", "Gained Empowered", "Empowered", 0),
        ]);

    public GreerTheBlightbringer(int triggerID) : base(triggerID)
    {
        MechanicList.Add(Mechanics);
        Extension = "greer";
        Icon = EncounterIconGreer;
        ChestID = ChestID.GreersChest;
        LogCategoryInformation.InSubCategoryOrder = 0;
        LogID |= 0x000001;
    }
    internal override CombatReplayMap GetCombatMapInternal(ParsedEvtcLog log, CombatReplayDecorationContainer arenaDecorations)
    {
        var crMap = new CombatReplayMap(
                        (1912, 1845),
                        (11300, -10621, 18374, -3794));
        AddArenaDecorationsPerEncounter(log, arenaDecorations, LogID, CombatReplayGreerTheBlightbringer, crMap);
        return crMap;
    }

    internal override IReadOnlyList<TargetID>  GetTargetsIDs()
    {
        return
        [
            TargetID.Greer,
            TargetID.Gree,
            TargetID.Reeg,
            TargetID.Ereg,
            TargetID.ProtoGreerling,
        ];
    }

    internal override Dictionary<TargetID, int> GetTargetsSortIDs()
    {
        return new Dictionary<TargetID, int>()
        {
            { TargetID.Greer, 0 },
            { TargetID.Gree, 1 },
            { TargetID.Reeg, 1 },
            { TargetID.Ereg, 1 },
            { TargetID.ProtoGreerling, 2 },
        };
    }

    internal override IReadOnlyList<TargetID> GetTrashMobsIDs()
    {
        return
        [
            TargetID.EmpoweringBeast,
        ];
    }

    internal override long GetLogOffset(EvtcVersionEvent evtcVersion, LogData logData, AgentData agentData, List<CombatItem> combatData)
    {
        long startToUse = GetGenericLogOffset(logData);
        CombatItem? logStartNPCUpdate = combatData.FirstOrDefault(x => x.IsStateChange == StateChange.LogNPCUpdate);
        if (logStartNPCUpdate != null)
        {
            startToUse = GetEnterCombatTime(logData, agentData, combatData, logStartNPCUpdate.Time, [(int)TargetID.Greer, (int)TargetID.Gree, (int)TargetID.Reeg], logStartNPCUpdate.DstAgent);
        }
        return startToUse;
    }

    internal static void RenameProtoGreerlings(IReadOnlyList<SingleActor> targets)
    {
        foreach (SingleActor target in targets)
        {
            if (target.IsSpecies(TargetID.ProtoGreerling))
            {
                target.OverrideName("Champion " + target.Character);
            }
        }
    }

    internal override void EIEvtcParse(ulong gw2Build, EvtcVersionEvent evtcVersion, LogData logData, AgentData agentData, List<CombatItem> combatData, IReadOnlyDictionary<uint, ExtensionHandler> extensions)
    {
        base.EIEvtcParse(gw2Build, evtcVersion, logData, agentData, combatData, extensions);
        RenameProtoGreerlings(Targets);
    }

    internal override LogData.LogMode GetLogMode(CombatData combatData, AgentData agentData, LogData logData)
    {
        SingleActor greer = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.Greer)) ?? throw new MissingKeyActorsException("Greer not found");
        SingleActor? ereg = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.Ereg));
        if (ereg != null)
        {
            greer.OverrideName("Godspoil Greer");
            return LogData.LogMode.CMNoName;
        }
        return LogData.LogMode.Normal;
    }

    private static void SetPhaseNameForHP(PhaseData damageImmunityPhase, double hpPercent)
    {
        if (hpPercent > 81)
        {
            damageImmunityPhase.Name = "100% - 80%";
        }
        else if (hpPercent > 66)
        {
            damageImmunityPhase.Name = "80% - 65%";
        }
        else if (hpPercent > 51)
        {
            damageImmunityPhase.Name = "65% - 50%";
        }
        else if (hpPercent > 36)
        {
            damageImmunityPhase.Name = "50% - 35%";
        }
        else if (hpPercent > 21 )
        {
            damageImmunityPhase.Name = "35% - 20%";
        }
        else
        {
            damageImmunityPhase.Name = "20% - 0%";
        }

    }

    private static void AddMainTitansToPhase(PhaseData phase, SingleActor? greer, IEnumerable<SingleActor> greeAndReg, SingleActor? ereg, ParsedEvtcLog log)
    {
        phase.AddTarget(greer, log);
        phase.AddTargets(greeAndReg, log, PhaseData.TargetPriority.Blocking);
        phase.AddTarget(ereg, log, PhaseData.TargetPriority.NonBlocking);
    }
    internal static List<PhaseData> ComputePhases(ParsedEvtcLog log, SingleActor greer, IEnumerable<SingleActor> greeAndReeg, SingleActor? ereg, IEnumerable<SingleActor> protoGreerlings, EncounterPhaseData encounterPhase, bool requirePhases)
    {
        if (!requirePhases)
        {
            return [];
        }
        var phases = new List<PhaseData>(encounterPhase.IsCM ? 14 : 11);
        // In shield bubble phases
        phases.AddRange(GetPhasesByCast(log, InvulnerableBarrier, greer, true, true, encounterPhase.Start, encounterPhase.End));
        var mainPhases = new List<PhaseData>(3);
        for (int i = 0; i < phases.Count; i++)
        {
            var phaseIndex = i + 1;
            PhaseData phase = phases[i];
            if (phaseIndex % 2 == 0)
            {
                phase.Name = "Split " + (phaseIndex) / 2;
                phase.AddParentPhase(encounterPhase);
                phase.AddTargets(greeAndReeg, log);
                phase.AddTargets([greer, ereg], log, PhaseData.TargetPriority.NonBlocking);
            }
            else
            {
                mainPhases.Add(phase);
                phase.AddParentPhase(encounterPhase);
                phase.Name = "Phase " + (phaseIndex + 1) / 2;
                AddMainTitansToPhase(phase, greer, greeAndReeg, ereg, log);
            }
        }
        // Generic in between damage immunity phases handling
        var damageImmunityPhases = GetPhasesByInvul(log, [DamageImmunity1, DamageImmunity2], greer, false, true, encounterPhase.Start, encounterPhase.End);
        foreach (var damageImmunityPhase in damageImmunityPhases)
        {
            damageImmunityPhase.AddParentPhases(mainPhases);
            var currentMainPhase = phases.LastOrDefault(x => x.Start <= damageImmunityPhase.Start && x.Name.Contains("Phase"));
            var hpAtStart = greer.GetCurrentHealthPercent(log, damageImmunityPhase.Start);
            if (currentMainPhase != null)
            {
                if (currentMainPhase.End > damageImmunityPhase.End)
                {
                    AddMainTitansToPhase(damageImmunityPhase, greer, greeAndReeg, ereg, log);
                    phases.Add(damageImmunityPhase);
                    SetPhaseNameForHP(damageImmunityPhase, hpAtStart);
                }
                else
                {
                    var beforeShieldPhase = new SubPhasePhaseData(damageImmunityPhase.Start, currentMainPhase.End);
                    beforeShieldPhase.AddParentPhases(mainPhases);
                    SetPhaseNameForHP(beforeShieldPhase, hpAtStart);
                    AddMainTitansToPhase(beforeShieldPhase, greer, greeAndReeg, ereg, log);
                    phases.Add(beforeShieldPhase);
                    var nextMainPhase = phases.FirstOrDefault(x => x.Start >= damageImmunityPhase.Start && x.Name.Contains("Phase"));
                    if (nextMainPhase != null)
                    {
                        var afterShieldPhase = new SubPhasePhaseData(nextMainPhase.Start, damageImmunityPhase.End);
                        afterShieldPhase.AddParentPhases(mainPhases);
                        SetPhaseNameForHP(afterShieldPhase, greer.GetCurrentHealthPercent(log, afterShieldPhase.Start));
                        AddMainTitansToPhase(afterShieldPhase, greer, greeAndReeg, ereg, log);
                        phases.Add(afterShieldPhase);
                    }
                }
            }
        }
        // Enrage handling, greer gets damage immunity again, remove that
        var lastPhase = phases.Last();
        if (log.CombatData.GetAnimatedCastData(TheWorldEndsInDecay).Any(x => lastPhase.Start >= x.Time && x.Time >= encounterPhase.Start))
        {
            phases.Remove(lastPhase);
        }
        // Below 20% CM phases handling
        if (encounterPhase.IsCM && greer.GetBuffStatus(log, DamageImmunity3).Any(x => x.Value > 0))
        {
            var finalPhases = GetPhasesByInvul(log, DamageImmunity3, greer, true, true, encounterPhase.Start, encounterPhase.End);
            var finalHPPhase = phases.Last();
            if (finalPhases.Count > 0)
            {
                var p20Percent10PercentPhase = finalPhases[0];
                p20Percent10PercentPhase.AddParentPhase(finalHPPhase);
                p20Percent10PercentPhase.OverrideStart(finalHPPhase.Start);
                p20Percent10PercentPhase.Name = "20% - 10%";
                AddMainTitansToPhase(p20Percent10PercentPhase, greer, greeAndReeg, ereg, log);
                phases.Add(finalPhases[0]);
                var protoPhases = 0;
                var below10Phases = 0;
                for (var i = 1; i < finalPhases.Count; i++)
                {
                    var phase = finalPhases[i];
                    phase.AddParentPhase(finalHPPhase);
                    if (i % 2 == 1)
                    {
                        phase.Name = "Proto Greer " + (++protoPhases);
                        phase.AddTargets(protoGreerlings, log);
                        phases.Add(phase);
                    }
                    else
                    {
                        phase.Name = "Below 10% " + (++below10Phases);
                        AddMainTitansToPhase(phase, greer, greeAndReeg, ereg, log);
                        phases.Add(phase);
                    }
                    phase.OverrideEnd(Math.Min(phase.End, finalHPPhase.End));
                }
            }
        }
        return phases;
    }
    internal override List<PhaseData> GetPhases(ParsedEvtcLog log, bool requirePhases)
    {
        List<PhaseData> phases = GetInitialPhase(log);
        SingleActor greer = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.Greer)) ?? throw new MissingKeyActorsException("Greer not found");
        var greeAndReegIDs = new List<int>
        {
            (int) TargetID.Reeg,
            (int) TargetID.Gree,
        };
        var greeAndReeg = Targets.Where(x => x.IsAnySpecies(greeAndReegIDs));
        // Ereg is present in CM
        var ereg = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.Ereg));
        AddMainTitansToPhase(phases[0], greer, greeAndReeg, ereg, log);
        // The Proto-Greelings can respawn during 10%
        var protoGreerlings = Targets.Where(x => x.IsSpecies(TargetID.ProtoGreerling));
        phases[0].AddTargets(protoGreerlings, log, PhaseData.TargetPriority.Blocking);
        phases.AddRange(ComputePhases(log, greer, greeAndReeg, ereg, protoGreerlings, (EncounterPhaseData)phases[0], requirePhases));
        return phases;
    }

    internal override void ComputeNPCCombatReplayActors(NPC target, ParsedEvtcLog log, CombatReplay replay)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputeNPCCombatReplayActors(target, log, replay);
        }

        (long start, long end) lifespan;

        switch (target.ID)
        {
            case (int)TargetID.Greer:
                AddSweepTheMoldRakeTheRot(target, log, replay);
                AddStompTheGrowth(target, log, replay);
                AddScatteringSporeblast(target, log, replay);
                AddEnfeeblingMiasma(target, log, replay);
                AddRainOfSpores(target, log, replay);
                AddRipplesOfRot(target, log, replay);
                AddBlobOfBlight(target, log, replay);
                AddCageOfDecayOrNoxiousBlight(target, log, replay);

                // Getting breakbar times to filter some effects of different sizes appearing at the end of it.
                var breakbars = target.GetBuffStatus(log, [DamageImmunity1, DamageImmunity2, DamageImmunity3]).Where(x => x.Value > 0);
                foreach (var breakbar in breakbars)
                {
                    // Rot the World - AoEs
                    if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerRotTheWorld, out var rotTheWorld))
                    {
                        foreach (EffectEvent effect in rotTheWorld.Where(x => x.Time >= breakbar.Start && x.Time <= breakbar.End))
                        {
                            lifespan = effect.ComputeLifespan(log, effect.Duration);
                            replay.Decorations.AddWithBorder(new CircleDecoration(240, lifespan, Colors.LightOrange, 0.2, new PositionConnector(effect.Position)), Colors.LightOrange, 0.2);
                        }
                    }
                }

                // Rot the World - Breakbar
                var breakbarUpdates = target.GetBreakbarPercentUpdates(log);
                var (breakbarNones, breakbarActives, breakbarImmunes, breakbarRecoverings) = target.GetBreakbarStatus(log);
                foreach (var segment in breakbarActives)
                {
                    replay.Decorations.AddActiveBreakbar(segment.TimeSpan, target, breakbarUpdates);
                }

                // Invulnerable Barrier
                if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerInvulnerableBarrier, out var invulnerableBarriers))
                {
                    foreach (EffectEvent effect in invulnerableBarriers)
                    {
                        lifespan = effect.ComputeLifespan(log, effect.Duration);
                        replay.Decorations.AddWithBorder(new CircleDecoration(600, lifespan, Colors.GreenishYellow, 0.2, new AgentConnector(target)), Colors.GreenishYellow, 0.4);
                    }
                }
                break;
            case (int)TargetID.Reeg:
                AddEmpoweringBlast(target, log, replay);
                AddScatteringSporeblast(target, log, replay);
                AddRainOfSpores(target, log, replay);
                AddBlobOfBlight(target, log, replay);
                AddCageOfDecayOrNoxiousBlight(target, log, replay);
                break;
            case (int)TargetID.Gree:
                AddEmpoweringBlast(target, log, replay);
                AddSweepTheMoldRakeTheRot(target, log, replay);
                AddStompTheGrowth(target, log, replay);
                AddRipplesOfRot(target, log, replay);
                AddEnfeeblingMiasma(target, log, replay);
                AddCageOfDecayOrNoxiousBlight(target, log, replay);
                break;
            case (int)TargetID.Ereg:
                AddEmpoweringBlast(target, log, replay);
                AddScatteringSporeblast(target, log, replay);
                AddEnfeeblingMiasma(target, log, replay);
                AddRainOfSpores(target, log, replay);
                AddBlobOfBlight(target, log, replay);
                break;
            case (int)TargetID.ProtoGreerling:
                AddSweepTheMoldRakeTheRot(target, log, replay);
                AddStompTheGrowth(target, log, replay);
                AddScatteringSporeblast(target, log, replay);
                break;
            case (int)TargetID.EmpoweringBeast:
                // Blighting Stab - Indicator
                if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerBlightingStabIndicator, out var blightingStabIndicator))
                {
                    foreach (EffectEvent effect in blightingStabIndicator)
                    {
                        // Duration too long by 500ms, use damage effect as end time
                        lifespan = effect.ComputeLifespanWithSecondaryEffectAndPosition(log, EffectGUIDs.GreerBlightingStabDamage);
                        replay.Decorations.Add(new CircleDecoration(300, lifespan, Colors.LightOrange, 0.2, new PositionConnector(effect.Position)));
                    }
                }

                // Blighting Stab - Damage
                if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerBlightingStabDamage, out var blightingStabDamage))
                {
                    foreach (EffectEvent effect in blightingStabDamage)
                    {
                        lifespan = effect.ComputeLifespan(log, 1000);
                        replay.Decorations.Add(new CircleDecoration(300, lifespan, Colors.GreenishYellow, 0.2, new PositionConnector(effect.Position)));
                    }
                }
                break;
            default:
                break;
        }
    }

    internal override void ComputePlayerCombatReplayActors(PlayerActor player, ParsedEvtcLog log, CombatReplay replay)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputePlayerCombatReplayActors(player, log, replay);
        }

        // Eruption of Rot - Green AoE
        if (log.CombatData.TryGetEffectEventsByDstWithGUIDs(player.AgentItem, 
            [EffectGUIDs.GreerEruptionOfRotGreen, EffectGUIDs.GreerEruptionOfRotGreen2, EffectGUIDs.GreerEruptionofRotGreen3], 
            out var noxiousBlight))
        {
            foreach (EffectEvent effect in noxiousBlight)
            {
                long duration = effect.GUIDEvent.ContentGUID == EffectGUIDs.GreerEruptionOfRotGreen ? 10000 : 8000;
                string icon = 
                    effect.GUIDEvent.ContentGUID == EffectGUIDs.GreerEruptionOfRotGreen 
                    || effect.GUIDEvent.ContentGUID == EffectGUIDs.GreerEruptionOfRotGreen2 
                    ? ParserIcons.GreenMarkerSize2Overhead : ParserIcons.GreenMarkerSize3Overhead;
                long growing = effect.Time + duration;
                (long start, long end) lifespan = effect.ComputeLifespan(log, duration);
                var circle = new CircleDecoration(240, lifespan, Colors.DarkGreen, 0.2, new AgentConnector(player));
                replay.Decorations.AddWithGrowing(circle, growing, true);
                replay.Decorations.AddOverheadIcon(lifespan, player, icon);
            }
        }

        // Infectious Rot - Failed Green AoE | Plague Rot - Failed Green AoE CM
        var infectiousRot = player.GetBuffStatus(log, [InfectiousRotBuff, PlagueRot]).Where(x => x.Value > 0);
        foreach (var segment in infectiousRot)
        {
            replay.Decorations.Add(new CircleDecoration(200, segment.TimeSpan, Colors.Red, 0.2, new AgentConnector(player)));
        }

    }

    internal override void ComputeEnvironmentCombatReplayDecorations(ParsedEvtcLog log, CombatReplayDecorationContainer environmentDecorations)
    {
        if (!log.LogData.IsInstance)
        {
            base.ComputeEnvironmentCombatReplayDecorations(log, environmentDecorations);
        }

        // Wave of Corruption
        if (log.CombatData.TryGetEffectEventsByGUID(EffectGUIDs.GreerWaveOfCorruption1, out var shockwaves))
        {
            foreach (EffectEvent effect in shockwaves)
            {
                (long start, long end) lifespan = (effect.Time, effect.Time + 3000);
                environmentDecorations.AddShockwave(new PositionConnector(effect.Position), lifespan, Colors.Purple, 0.2, 1500);
            }
        }
    }

    private static void AddScatteringSporeblast(NPC target, ParsedEvtcLog log, CombatReplay replay)
    {
        // Scattering Sporeblast - Indicator
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerScatteringSporeblastIndicator, out var sporeblasts))
        {
            foreach (EffectEvent effect in sporeblasts)
            {
                (long start, long end) lifespan = effect.ComputeLifespan(log, effect.Duration);
                var indicator = new CircleDecoration(100, lifespan, Colors.LightOrange, 0.2, new PositionConnector(effect.Position));
                replay.Decorations.Add(indicator);
            }
        }
    }

    private static void AddSweepTheMoldRakeTheRot(NPC target, ParsedEvtcLog log, CombatReplay replay)
    {
        // Swepp the Mold / Rake the Rot - Indicator
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerSweepTheMoldRakeTheRotIndicator, out var indicators))
        {
            var casts = target.GetAnimatedCastEvents(log).Where(x =>
            x.SkillID == SweepTheMold || x.SkillID == SweepTheMold2 || x.SkillID == SweepTheMold3 ||
            x.SkillID == RakeTheRot || x.SkillID == RakeTheRot2 || x.SkillID == RakeTheRot3);

            foreach (var cast in casts)
            {
                foreach (EffectEvent effect in indicators.Where(x => x.Time >= cast.Time && x.Time < cast.Time + 1000)) // 1 second padding
                {
                    // Sweep the Mold has a X 20 and Y 40 offset, the X is already covered by the effect rotation.
                    // Adding 40 to the radius matches the in game visual
                    uint radius = 0;
                    switch (cast.SkillID)
                    {
                        case SweepTheMold:
                            radius = 750 + 40;
                            break;
                        case RakeTheRot:
                            radius = 750;
                            break;
                        case SweepTheMold2:
                            radius = 950 + 40;
                            break;
                        case RakeTheRot2:
                            radius = 950;
                            break;
                        case SweepTheMold3:
                            radius = 550 + 40;
                            break;
                        case RakeTheRot3:
                            radius = 550;
                            break;
                        default:
                            break;
                    }

                    (long start, long end) lifespan = effect.ComputeLifespan(log, effect.Duration);
                    var cone = new PieDecoration(radius, 120, lifespan, Colors.LightOrange, 0.2, new PositionConnector(effect.Position)).UsingRotationConnector(new AngleConnector(effect.Rotation.Z + 90));
                    replay.Decorations.Add(cone);
                }
            }
        }
    }

    private static void AddEnfeeblingMiasma(NPC target, ParsedEvtcLog log, CombatReplay replay)
    {
        (long start, long end) lifespan;

        // Enfeebling Miasma - Cone Indicator
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerEnfeeblingMiasma, out var miasmaIndicator))
        {
            foreach (EffectEvent effect in miasmaIndicator)
            {
                lifespan = effect.ComputeLifespan(log, 6000);
                var cone = new PieDecoration(2000, 60, lifespan, Colors.LightOrange, 0.2, new PositionConnector(effect.Position)).UsingRotationConnector(new AngleConnector(effect.Rotation.Z + 90));
                replay.Decorations.Add(cone);
            }
        }

        // Enfeebling Miasma - Gas Circles
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerEnfeeblingMiasmaGasMoving, out var miasmaAnimation) &&
            log.CombatData.TryGetEffectEventsBySrcWithGUIDs(target.AgentItem, [EffectGUIDs.GreerEnfeeblingMiasmaGasClouds, EffectGUIDs.GreerEnfeeblingMiasmaGasCloudsNew], out var miasmaClouds))
        {
            foreach (EffectEvent animation in miasmaAnimation)
            {
                foreach (EffectEvent cloud in miasmaClouds.Where(x => x.Time > animation.Time && x.Time < animation.Time + 6000))
                {
                    long duration = cloud.GUIDEvent.ContentGUID == EffectGUIDs.GreerEnfeeblingMiasmaGasClouds ? 12000 : 13000;
                    lifespan = cloud.ComputeLifespan(log, duration);
                    var circle = new CircleDecoration(150, lifespan, Colors.Purple, 0.2, new PositionConnector(cloud.Position));
                    replay.Decorations.AddWithBorder(circle, Colors.Red, 0.2);
                    if (!log.CombatData.HasMissileData)
                    {
                        replay.Decorations.AddProjectile(animation.Position, cloud.Position, (animation.Time, cloud.Time), Colors.Purple, 0.2, 150);
                    }
                }
            }
        }

        // Enfeebling Miasma - Gas Projectiles
        var enfeeblingMiasmaCloud = log.CombatData.GetMissileEventsBySkillIDs([EnfeeblingMiasma, EnfeeblingMiasma2, EnfeeblingMiasma3, EnfeeblingMiasma4]).Where(x => x.Src.Is(target.AgentItem));
        replay.Decorations.AddNonHomingMissiles(log, enfeeblingMiasmaCloud, Colors.Purple, 0.2, 150);
    }

    private static void AddCageOfDecayOrNoxiousBlight(NPC target, ParsedEvtcLog log, CombatReplay replay)
    {
        (long start, long end) lifespan;

        // Cage of Decay - Arrow Indicator
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerCageOfDecayArrowIndicator, out var cageOfDecayArrows))
        {
            foreach (EffectEvent effect in cageOfDecayArrows)
            {
                lifespan = effect.ComputeLifespan(log, 5000);
                var offset = new Vector3(700, 0, 0);
                var arrow = new RectangleDecoration(1400, 100, lifespan, Colors.LightOrange, 0.2, new PositionConnector(effect.Position).WithOffset(offset, true)).UsingRotationConnector(new AngleConnector(effect.Rotation.Z - 90));
                replay.Decorations.Add(arrow);
            }
        }

        // Cage of Decay - Circle Indicator
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerCageOfDecayCircleIndicator, out var cageOfDecayCirclesIndicators))
        {
            foreach (EffectEvent effect in cageOfDecayCirclesIndicators)
            {
                long duration = 5000;
                long growing = effect.Time + duration;
                lifespan = effect.ComputeLifespan(log, duration);
                var circle = new CircleDecoration(360, lifespan, Colors.LightOrange, 0.2, new PositionConnector(effect.Position));
                replay.Decorations.AddWithGrowing(circle, growing);
            }
        }

        // Cage of Decay + Noxious Blight - Roots
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerCageOfDecayRoots, out var cageOfDecayRoots))
        {
            foreach (EffectEvent effect in cageOfDecayRoots)
            {
                lifespan = effect.ComputeLifespan(log, 2000);
                var roots = (RectangleDecoration)new RectangleDecoration(50, 150, lifespan, Colors.GreenishYellow, 0.3, new PositionConnector(effect.Position)).UsingRotationConnector(new AngleConnector(effect.Rotation.Z));
                replay.Decorations.AddWithBorder(roots, Colors.Purple, 0.2);
            }
        }

        // Cage of Decay + Noxious Blight - Circle Damage
        if (log.CombatData.TryGetEffectEventsBySrcWithGUIDs(target.AgentItem, [EffectGUIDs.GreerCageOfDecayCircleDamage, EffectGUIDs.GreerCageOfDecayCircleDamageNew], out var cageOfDecayCirclesDamage))
        {
            foreach (EffectEvent effect in cageOfDecayCirclesDamage)
            {
                // Durations: Cage of Decay - 23000 | Eruption of Rot - 8000
                lifespan = effect.ComputeLifespan(log, effect.Duration);
                var circle = new CircleDecoration(360, lifespan, Colors.LightPurple, 0.3, new PositionConnector(effect.Position));
                replay.Decorations.AddWithBorder(circle, Colors.GreenishYellow, 0.3);
            }
        }

        // Cage of Decay - Moving roots walls
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerCageOfDecayMovingWalls, out var cageOfDecayWalls))
        {
            foreach (EffectEvent effect in cageOfDecayWalls)
            {
                lifespan = effect.ComputeLifespan(log, 2000);
                var wall = (RectangleDecoration)new RectangleDecoration(200, 50, lifespan, Colors.GreenishYellow, 0.3, new PositionConnector(effect.Position)).UsingRotationConnector(new AngleConnector(effect.Rotation.Z));
                replay.Decorations.AddWithBorder(wall, Colors.Purple, 0.2);
            }
        }

        // Cage of Decay - Roots walls around the circle of damage
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerCageOfDecayCircleWalls, out var cageOfDecayCircleWalls))
        {
            foreach (EffectEvent effect in cageOfDecayCircleWalls)
            {
                lifespan = effect.ComputeLifespan(log, 23000);
                var wall = (RectangleDecoration)new RectangleDecoration(200, 100, lifespan, Colors.GreenishYellow, 0.3, new PositionConnector(effect.Position)).UsingRotationConnector(new AngleConnector(effect.Rotation.Z));
                replay.Decorations.AddWithBorder(wall, Colors.Purple, 0.2);
            }
        }
    }

    private static void AddRainOfSpores(NPC target, ParsedEvtcLog log, CombatReplay replay)
    {
        // Rain of Spores - Indicator
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerRainOfSporesIndicator, out var rainOfSpores))
        {
            foreach (EffectEvent effect in rainOfSpores)
            {
                (long start, long end) lifespan = effect.ComputeLifespan(log, effect.Duration);
                replay.Decorations.Add(new CircleDecoration(200, lifespan, Colors.LightOrange, 0.2, new PositionConnector(effect.Position)));
            }
        }
    }

    private static void AddStompTheGrowth(NPC target, ParsedEvtcLog log, CombatReplay replay)
    {
        // Stomp the Growth - Circle indicator
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerStompTheGrowth, out var stompTheGrowth))
        {
            var casts = target.GetAnimatedCastEvents(log).Where(x => x.SkillID == StompTheGrowth || x.SkillID == StompTheGrowth2 || x.SkillID == StompTheGrowth3);
            
            foreach (var cast in casts)
            {
                foreach (EffectEvent effect in stompTheGrowth.Where(x => x.Time >= cast.Time && x.Time < cast.Time + 3000)) // 3 seconds padding
                {
                    uint radius = 0;
                    switch (cast.SkillID)
                    {
                        case StompTheGrowth:
                            radius = 800;
                            break;
                        case StompTheGrowth2:
                            radius = 600;
                            break;
                        case StompTheGrowth3:
                            radius = 1000;
                            break;
                        default:
                            break;
                    }
                    (long start, long end) lifespan = effect.ComputeLifespan(log, effect.Duration);
                    var circle = new CircleDecoration(radius, lifespan, Colors.LightOrange, 0.2, new PositionConnector(effect.Position));
                    replay.Decorations.AddWithGrowing(circle, effect.Time + effect.Duration);
                }
            }
        }
    }

    private static void AddRipplesOfRot(NPC target, ParsedEvtcLog log, CombatReplay replay)
    {
        (long start, long end) lifespan;

        // Ripples of Rot - Inner Circle
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerRipplesOfRotIndicator1, out var ripplesOfRotIndicator1))
        {
            foreach (EffectEvent effect in ripplesOfRotIndicator1)
            {
                lifespan = effect.ComputeLifespan(log, effect.Duration);
                var innerCircle = new CircleDecoration(240, lifespan, Colors.LightOrange, 0.2, new PositionConnector(effect.Position));
                replay.Decorations.AddWithGrowing(innerCircle, effect.Time + effect.Duration);
            }
        }

        // Ripples of Rot - Outer Circle
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerRipplesOfRotIndicator2, out var ripplesOfRotIndicator2))
        {
            foreach (EffectEvent effect in ripplesOfRotIndicator2)
            {
                lifespan = effect.ComputeLifespan(log, effect.Duration);
                var outerCircle = new CircleDecoration(800, lifespan, Colors.LightOrange, 0.2, new PositionConnector(effect.Position));
                replay.Decorations.AddWithBorder(outerCircle, Colors.LightOrange, 0.2);
            }
        }

        // Ripples of Rot - Moving roots walls
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerRipplesOfRotMovingWalls, out var movingWalls))
        {
            foreach (EffectEvent effect in movingWalls)
            {
                lifespan = effect.ComputeLifespan(log, 2000);
                var wall = (RectangleDecoration)new RectangleDecoration(100, 50, lifespan, Colors.GreenishYellow, 0.3, new PositionConnector(effect.Position)).UsingRotationConnector(new AngleConnector(effect.Rotation.Z));
                replay.Decorations.AddWithBorder(wall, Colors.Purple, 0.2);
            }
        }

        // Ripples of Rot - Roots Walls
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerRipplesOfRotWalls, out var walls))
        {
            foreach (EffectEvent effect in walls)
            {
                lifespan = effect.ComputeLifespan(log, 23000);
                var wall = (RectangleDecoration)new RectangleDecoration(200, 100, lifespan, Colors.GreenishYellow, 0.3, new PositionConnector(effect.Position)).UsingRotationConnector(new AngleConnector(effect.Rotation.Z));
                replay.Decorations.AddWithBorder(wall, Colors.Purple, 0.2);
            }
        }
    }

    private static void AddBlobOfBlight(NPC target, ParsedEvtcLog log, CombatReplay replay)
    {
        (long start, long end) lifespan;

        // Blob of Blight - AoE Indicator
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerBlobOfBlightIndicator, out var blobOfBlightIndicator))
        {
            foreach (EffectEvent effect in blobOfBlightIndicator)
            {
                // The effect has 0 duration logged
                lifespan = effect.ComputeLifespan(log, 2315);
                replay.Decorations.AddWithGrowing(new CircleDecoration(300, lifespan, Colors.LightOrange, 0.2, new PositionConnector(effect.Position)), lifespan.end);
            }
        }

        // Blob of Blight - Stationary Orb
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(target.AgentItem, EffectGUIDs.GreerBlobofBlightStationary, out var blobOfBlight))
        {
            foreach (EffectEvent effect in blobOfBlight)
            {
                lifespan = effect.ComputeLifespan(log, 7000);
                replay.Decorations.Add(new CircleDecoration(60, lifespan, Colors.GreenishYellow, 0.3, new PositionConnector(effect.Position)));
                replay.Decorations.Add(new DoughnutDecoration(60, 80, lifespan, Colors.LightPurple, 0.3, new PositionConnector(effect.Position)));
            }
        }

        // Blob of Blight - Moving Orbs
        // Using the projectile velocity to distinguish between the main orbs and the mini orbs
        // Main orbs have 0.3 and mini orbs have 0.5, once the main orb hits a player, the mini orbs have 0.4
        var blobOfBlightOrbs = log.CombatData.GetMissileEventsBySrcBySkillIDs(target.AgentItem, [BlobOfBlight, BlobOfBlight2, BlobOfBlight3]);
        var blobOfBlightMainOrbs = blobOfBlightOrbs.Where(x => x.LaunchEvents.Any(x => x.Speed == 0.3f));
        var blobOfBlightMiniOrbs = blobOfBlightOrbs.Where(x => x.LaunchEvents.Any(x => x.Speed == 0.4f || x.Speed == 0.5f));

        // Main Orbs
        foreach (MissileEvent missileEvent in blobOfBlightMainOrbs)
        {
            lifespan = (missileEvent.Time, missileEvent.RemoveEvent?.Time ?? log.LogData.LogEnd);
            for (int i = 0; i < missileEvent.LaunchEvents.Count; i++)
            {
                MissileLaunchEvent? launch = missileEvent.LaunchEvents[i];
                lifespan = (launch.Time, i != missileEvent.LaunchEvents.Count - 1 ? missileEvent.LaunchEvents[i + 1].Time : lifespan.end);
                if (!launch.TargetedAgent.IsNonIdentifiedSpecies())
                {
                    replay.Decorations.Add(new CircleDecoration(60, lifespan, Colors.GreenishYellow, 0.3, new PositionToAgentConnector(launch.TargetedAgent, launch.LaunchPosition, launch.Time, launch.Speed)));
                    replay.Decorations.Add(new DoughnutDecoration(60, 80, lifespan, Colors.LightPurple, 0.3, new PositionToAgentConnector(launch.TargetedAgent, launch.LaunchPosition, launch.Time, launch.Speed)));
                }
            }
        }

        // Mini Orbs
        replay.Decorations.AddNonHomingMissiles(log, blobOfBlightMiniOrbs, Colors.GreenishYellow, 0.3, 25);
    }

    private static void AddEmpoweringBlast(NPC target, ParsedEvtcLog log, CombatReplay replay)
    {
        // Empowering Blast - Orbs
        var orbs = log.CombatData.GetMissileEventsBySrcBySkillIDs(target.AgentItem, [EmpoweringBlast1, EmpoweringBlast2]);
        replay.Decorations.AddHomingMissiles(log, orbs, Colors.Purple, 0.5, 25);
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
                // The buff elegibility remains on players even if Ereg is dead
                var ereg = encounterPhase.Targets.Keys.FirstOrDefault(x => x.IsSpecies(TargetID.Ereg));
                if (ereg != null && !log.CombatData.GetDeadEvents(ereg.AgentItem).Any())
                {
                    instanceBuffs.Add(new(log.Buffs.BuffsByIDs[AchievementEligibilitySpareTheEreg], 1, encounterPhase));
                }

            }
        }
    }
}

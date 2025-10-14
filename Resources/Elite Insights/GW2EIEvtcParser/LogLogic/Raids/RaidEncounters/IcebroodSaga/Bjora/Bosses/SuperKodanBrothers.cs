﻿using GW2EIEvtcParser.EIData;
using GW2EIEvtcParser.Exceptions;
using GW2EIEvtcParser.Extensions;
using GW2EIEvtcParser.ParsedData;
using static GW2EIEvtcParser.ArcDPSEnums;
using static GW2EIEvtcParser.LogLogic.LogLogicPhaseUtils;
using static GW2EIEvtcParser.LogLogic.LogLogicUtils;
using static GW2EIEvtcParser.ParserHelper;
using static GW2EIEvtcParser.ParserHelpers.LogImages;
using static GW2EIEvtcParser.SkillIDs;
using static GW2EIEvtcParser.SpeciesIDs;

namespace GW2EIEvtcParser.LogLogic;

internal class SuperKodanBrothers : Bjora
{
    public SuperKodanBrothers(int triggerID) : base(triggerID)
    {
        MechanicList.Add(new MechanicGroup([      
            new PlayerDstHealthDamageHitMechanic(Groundshaker, new MechanicPlotlySetting(Symbols.TriangleDown, Colors.Grey), "Groundshaker.H", "Hit by Groundshaker", "Groundshaker Hit", 150),
            new PlayerDstHealthDamageHitMechanic(Groundpiercer, new MechanicPlotlySetting(Symbols.TriangleDown, Colors.White), "Groundpiercer.H", "Hit by Groundpiercer", "Groundpiercer Knockdown", 150),
            new PlayerDstBuffApplyMechanic(UnrelentingPainBuff, new MechanicPlotlySetting(Symbols.DiamondOpen, Colors.Pink), "UnrelPain.A", "Unreleting Pain Applied", "Unrelenting Pain Applied", 0),
            new PlayerDstBuffApplyMechanic(Immobile, new MechanicPlotlySetting(Symbols.Circle, Colors.Blue), "Trapped", "Trapped", "Trapped", 2500),
            new EnemyDstBuffApplyMechanic(EnragedVC, new MechanicPlotlySetting(Symbols.Circle, Colors.Orange), "Enrage", "Enrage", "Enrage", 1 << 16),
            new EnemyCastStartMechanic(DeadlySynergy, new MechanicPlotlySetting(Symbols.Diamond, Colors.Blue), "Deadly Synergy", "Cast  Deadly Synergy", "Deadly Synergy", 10000),
            new EnemyCastStartMechanic(KodanTeleport, new MechanicPlotlySetting(Symbols.Hexagon, Colors.LightBlue), "Teleport", "Cast Teleport", "Teleport", 150),
        ])
        );
        Extension = "supkodbros";
        Icon = EncounterIconKodanBrothers;
        LogCategoryInformation.InSubCategoryOrder = 1;
        LogID |= 0x000003;
    }

    internal override CombatReplayMap GetCombatMapInternal(ParsedEvtcLog log, CombatReplayDecorationContainer arenaDecorations)
    {
        var crMap = new CombatReplayMap(
                        (905, 789),
                        (-1013, -1600, 2221, 1416));
        AddArenaDecorationsPerEncounter(log, arenaDecorations, LogID, CombatReplayKodanBrothers, crMap);
        return crMap;
    }
    internal override List<InstantCastFinder> GetInstantCastFinders()
    {
        return
        [
            new DamageCastFinder(VengefulAuraClaw, VengefulAuraClaw),
        ];
    }

    internal override LogData.LogStartStatus GetLogStartStatus(CombatData combatData, AgentData agentData, LogData logData)
    {
        if (TargetHPPercentUnderThreshold(TargetID.ClawOfTheFallen, logData.LogStart, combatData, Targets) ||
            TargetHPPercentUnderThreshold(TargetID.VoiceOfTheFallen, logData.LogStart, combatData, Targets))
        {
            return LogData.LogStartStatus.Late;
        }
        return LogData.LogStartStatus.Normal;
    }

    internal override long GetLogOffset(EvtcVersionEvent evtcVersion, LogData logData, AgentData agentData, List<CombatItem> combatData)
    {
        long startToUse = base.GetLogOffset(evtcVersion, logData, agentData, combatData);
        CombatItem? logStartNPCUpdate = combatData.FirstOrDefault(x => x.IsStateChange == StateChange.LogNPCUpdate);
        if (logStartNPCUpdate != null)
        {
            AgentItem mainTarget = (agentData.GetNPCsByID(TargetID.ClawOfTheFallen).FirstOrDefault() ?? agentData.GetNPCsByID(TargetID.VoiceOfTheFallen).FirstOrDefault()) ?? throw new MissingKeyActorsException("Main target not found");
            CombatItem? firstCast = combatData.FirstOrDefault(x => x.SrcMatchesAgent(mainTarget) && x.IsActivation != Activation.None && x.Time <= logStartNPCUpdate.Time && x.SkillID != WeaponStow && x.SkillID != WeaponDraw);
            if (firstCast != null && combatData.Any(x => x.SrcMatchesAgent(mainTarget) && x.Time > logStartNPCUpdate.Time + TimeThresholdConstant))
            {
                startToUse = firstCast.Time;
            }
        }
        return startToUse;
    }

    internal override List<PhaseData> GetPhases(ParsedEvtcLog log, bool requirePhases)
    {
        List<PhaseData> phases = GetInitialPhase(log);
        SingleActor? voice = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.ClawOfTheFallen));
        SingleActor? claw = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.VoiceOfTheFallen));
        if (voice == null || claw == null)
        {
            throw new MissingKeyActorsException("Claw or Voice not found");
        }
        phases[0].AddTarget(voice, log);
        phases[0].AddTarget(claw, log);
        phases[0].AddTargets(Targets.Where(x => x.IsSpecies(TargetID.VoiceAndClaw)), log, PhaseData.TargetPriority.Blocking);
        long logEnd = log.LogData.LogEnd;
        if (!requirePhases)
        {
            return phases;
        }
        //
        List<PhaseData> unmergedPhases = GetPhasesByInvul(log, Determined762, claw, false, true);
        for (int i = 0; i < unmergedPhases.Count; i++)
        {
            unmergedPhases[i].Name = "Phase " + (i + 1);
            unmergedPhases[i].AddParentPhase(phases[0]);
            unmergedPhases[i].AddTarget(claw, log);
            unmergedPhases[i].AddTarget(voice, log);
        }
        phases.AddRange(unmergedPhases);
        //
        int voiceAndClawCount = 0;
        foreach (SingleActor voiceAndClaw in Targets.Where(x => x.IsSpecies(TargetID.VoiceAndClaw)))
        {
            EnterCombatEvent? enterCombat = log.CombatData.GetEnterCombatEvents(voiceAndClaw.AgentItem).FirstOrDefault();
            long phaseStart = 0;
            if (enterCombat != null)
            {
                phaseStart = enterCombat.Time;
            }
            else
            {
                phaseStart = voiceAndClaw.FirstAware;
            }
            PhaseData? nextUnmergedPhase = unmergedPhases.FirstOrDefault(x => x.Start > phaseStart);
            long phaseEnd = Math.Min(logEnd, voiceAndClaw.LastAware);
            if (nextUnmergedPhase != null)
            {
                phaseEnd = nextUnmergedPhase.Start - 1;
            }
            var phase = new SubPhasePhaseData(phaseStart, phaseEnd, "Voice and Claw " + ++voiceAndClawCount);
            phase.AddTarget(voiceAndClaw, log);
            phases.Add(phase);
        }
        //
        var teleports = voice.GetCastEvents(log).Where(x => x.SkillID == KodanTeleport);
        long tpCount = 0;
        long preTPPhaseStart = 0;
        foreach (CastEvent teleport in teleports)
        {
            long preTPPhaseEnd = Math.Min(teleport.Time, log.LogData.LogEnd);
            SingleActor? voiceAndClaw = Targets.FirstOrDefault(x => x.IsSpecies(TargetID.VoiceAndClaw) && x.FirstAware >= preTPPhaseStart);
            if (voiceAndClaw != null)
            {
                long oldEnd = preTPPhaseEnd;
                preTPPhaseEnd = Math.Min(preTPPhaseEnd, voiceAndClaw.FirstAware);
                // To handle position phase after merge phase end
                if (oldEnd != preTPPhaseEnd)
                {
                    PhaseData? nextUnmergedPhase = unmergedPhases.FirstOrDefault(x => x.Start > voiceAndClaw.LastAware);
                    if (nextUnmergedPhase != null)
                    {
                        long postMergedStart = nextUnmergedPhase.Start + 1;
                        long postMergedEnd = oldEnd;
                        var phase = new SubPhasePhaseData(postMergedStart, postMergedEnd, "Position " + (++tpCount));
                        phase.AddParentPhases(unmergedPhases);
                        phase.AddTarget(claw, log);
                        phase.AddTarget(voice, log);
                        phases.Add(phase);
                    }

                }
            }
            if (preTPPhaseEnd - preTPPhaseStart > 2000)
            {
                var phase = new SubPhasePhaseData(preTPPhaseStart, preTPPhaseEnd, "Position " + (++tpCount));
                phase.AddParentPhases(unmergedPhases);
                phase.AddTarget(claw, log);
                phase.AddTarget(voice, log);
                phases.Add(phase);
            }
            preTPPhaseStart = teleport.EndTime;
        }

        //
        BuffEvent? enrage = log.CombatData.GetBuffData(EnragedVC).FirstOrDefault(x => x is BuffApplyEvent);
        if (enrage != null)
        {
            var phase = new SubPhasePhaseData(enrage.Time, log.LogData.LogEnd, "Enrage");
            phase.AddParentPhases(unmergedPhases);
            phase.AddTarget(claw.AgentItem.Is(enrage.To) ? claw : voice, log);
            phases.Add(phase);
        }
        // Missing final position event
        {
            PhaseData? nextUnmergedPhase = unmergedPhases.FirstOrDefault(x => x.Start > preTPPhaseStart);
            long finalStart = preTPPhaseStart;
            long finalPositionEnd = log.LogData.LogEnd;
            if (nextUnmergedPhase != null)
            {
                finalStart = nextUnmergedPhase.Start;
            }
            if (enrage != null)
            {
                finalPositionEnd = enrage.Time;
            }
            if (finalPositionEnd - finalStart > 2000)
            {
                var phase = new SubPhasePhaseData(finalStart, finalPositionEnd, "Position " + (++tpCount));
                phase.AddParentPhases(unmergedPhases);
                phase.AddTarget(claw, log);
                phase.AddTarget(voice, log);
                phases.Add(phase);
            }
        }
        return phases;
    }

    internal override string GetLogicName(CombatData combatData, AgentData agentData)
    {
        return "Super Kodan Brothers";
    }

    protected override IReadOnlyList<TargetID> GetSuccessCheckIDs()
    {
        return
        [
            TargetID.ClawOfTheFallen,
            TargetID.VoiceOfTheFallen,
        ];
    }

    internal override IReadOnlyList<TargetID>  GetTargetsIDs()
    {
        return
        [
            TargetID.VoiceOfTheFallen,
            TargetID.ClawOfTheFallen,
            TargetID.VoiceAndClaw,
        ];
    }
}

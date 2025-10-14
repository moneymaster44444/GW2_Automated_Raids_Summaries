﻿using GW2EIBuilders.JsonModels.JsonActorUtilities;
using GW2EIBuilders.JsonModels.JsonActorUtilities.JsonExtensions.EXTBarrier;
using GW2EIBuilders.JsonModels.JsonActorUtilities.JsonExtensions.EXTHealing;
using GW2EIEvtcParser;
using GW2EIEvtcParser.EIData;
using GW2EIEvtcParser.ParsedData;
using GW2EIJSON;

namespace GW2EIBuilders.JsonModels.JsonActors;

/// <summary>
/// Class corresponding to the regrouping of the same type of minions
/// </summary>
internal static class JsonMinionsBuilder
{

    public static JsonMinions BuildJsonMinions(Minions minions, ParsedEvtcLog log, RawFormatSettings settings, Dictionary<long, SkillItem> skillMap, Dictionary<long, Buff> buffMap)
    {
        var jsonMinions = new JsonMinions
        {
            Id = minions.ID
        };
        IReadOnlyList<PhaseData> phases = log.LogData.GetPhases(log);
        bool isEnemyMinion = !log.FriendlyAgents.Contains(minions.Master.AgentItem);
        //
        jsonMinions.Name = minions.Character;
        //
        var totalDamage = new List<int>(phases.Count);
        var totalShieldDamage = new List<int>(phases.Count);
        var totalBreakbarDamage = new List<double>(phases.Count);
        var totalDamageTaken = new List<int>(phases.Count);
        var totalShieldDamageTaken = new List<int>(phases.Count);
        var totalBreakbarDamageTaken = new List<double>(phases.Count);
        foreach (PhaseData phase in phases)
        {
            int tot = 0;
            int shdTot = 0;
            foreach (HealthDamageEvent de in minions.GetDamageEvents(null, log, phase.Start, phase.End))
            {
                tot += de.HealthDamage;
                shdTot += de.ShieldDamage;
            }
            totalDamage.Add(tot);
            totalShieldDamage.Add(shdTot);
            totalBreakbarDamage.Add(Math.Round(minions.GetBreakbarDamageEvents(null, log, phase.Start, phase.End).Sum(x => x.BreakbarDamage), 1));
            //
            int totTaken = 0;
            int shdTotTaken = 0;
            foreach (HealthDamageEvent de in minions.GetDamageTakenEvents(null, log, phase.Start, phase.End))
            {
                totTaken += de.HealthDamage;
                shdTotTaken += de.ShieldDamage;
            }
            totalDamageTaken.Add(totTaken);
            totalShieldDamageTaken.Add(shdTotTaken);
            totalBreakbarDamageTaken.Add(Math.Round(minions.GetBreakbarDamageTakenEvents(null, log, phase.Start, phase.End).Sum(x => x.BreakbarDamage), 1));
        }
        jsonMinions.TotalDamage = totalDamage;
        jsonMinions.TotalShieldDamage = totalShieldDamage;
        jsonMinions.TotalBreakbarDamage = totalBreakbarDamage;
        jsonMinions.TotalDamageTaken = totalDamageTaken;
        jsonMinions.TotalShieldDamageTaken = totalShieldDamageTaken;
        jsonMinions.TotalBreakbarDamageTaken = totalBreakbarDamageTaken;
        if (!isEnemyMinion)
        {
            var totalTargetDamage = new IReadOnlyList<int>[log.LogData.Logic.Targets.Count];
            var totalTargetShieldDamage = new IReadOnlyList<int>[log.LogData.Logic.Targets.Count];
            var totalTargetBreakbarDamage = new IReadOnlyList<double>[log.LogData.Logic.Targets.Count];
            for (int i = 0; i < log.LogData.Logic.Targets.Count; i++)
            {
                SingleActor tar = log.LogData.Logic.Targets[i];
                var totalTarDamage = new List<int>(phases.Count);
                var totalTarShieldDamage = new List<int>(phases.Count);
                var totalTarBreakbarDamage = new List<double>(phases.Count);
                foreach (PhaseData phase in phases)
                {
                    int tot = 0;
                    int shdTot = 0;
                    foreach (HealthDamageEvent de in minions.GetDamageEvents(tar, log, phase.Start, phase.End))
                    {
                        tot += de.HealthDamage;
                        shdTot += de.ShieldDamage;
                    }
                    totalTarDamage.Add(tot);
                    totalTarShieldDamage.Add(shdTot);
                    totalTarBreakbarDamage.Add(Math.Round(minions.GetBreakbarDamageEvents(tar, log, phase.Start, phase.End).Sum(x => x.BreakbarDamage), 1));
                }
                totalTargetDamage[i] = totalTarDamage;
                totalTargetShieldDamage[i] = totalTarShieldDamage;
                totalTargetBreakbarDamage[i] = totalTarBreakbarDamage;
            }
            jsonMinions.TotalTargetShieldDamage = totalTargetShieldDamage;
            jsonMinions.TotalTargetDamage = totalTargetDamage;
            jsonMinions.TotalTargetBreakbarDamage = totalTargetBreakbarDamage;
        }
        //
        var minionCastEvents = minions.GetIntersectingCastEvents(log);
        //TODO(Rennorb) @perf
        if (minionCastEvents.Any())
        {
            jsonMinions.Rotation = JsonRotationBuilder.BuildJsonRotationList(log, minionCastEvents.GroupBy(x => x.SkillID), skillMap).ToList();
        }
        //
        var totalDamageDist = new IReadOnlyList<JsonDamageDist>[phases.Count];
        var totalDamageTakenDist = new IReadOnlyList<JsonDamageDist>[phases.Count];
        for (int i = 0; i < phases.Count; i++)
        {
            PhaseData phase = phases[i];
            totalDamageDist[i] = JsonDamageDistBuilder.BuildJsonDamageDistList(
                minions.GetDamageEvents(null, log, phase.Start, phase.End).GroupBy(x => x.SkillID).ToDictionary(x => x.Key, x => x.ToList()),
                minions.GetBreakbarDamageEvents(null, log, phase.Start, phase.End).GroupBy(x => x.SkillID).ToDictionary(x => x.Key, x => x.ToList()),
                log,
                skillMap,
                buffMap
            );
            totalDamageTakenDist[i] = JsonDamageDistBuilder.BuildJsonDamageDistList(
                minions.GetDamageTakenEvents(null, log, phase.Start, phase.End).GroupBy(x => x.SkillID).ToDictionary(x => x.Key, x => x.ToList()),
                minions.GetBreakbarDamageTakenEvents(null, log, phase.Start, phase.End).GroupBy(x => x.SkillID).ToDictionary(x => x.Key, x => x.ToList()),
                log,
                skillMap,
                buffMap
            );
        }
        jsonMinions.TotalDamageDist = totalDamageDist;
        jsonMinions.TotalDamageTakenDist = totalDamageTakenDist;
        if (!isEnemyMinion)
        {
            var targetDamageDist = new IReadOnlyList<JsonDamageDist>[log.LogData.Logic.Targets.Count][];
            for (int i = 0; i < log.LogData.Logic.Targets.Count; i++)
            {
                SingleActor target = log.LogData.Logic.Targets[i];
                targetDamageDist[i] = new IReadOnlyList<JsonDamageDist>[phases.Count];
                for (int j = 0; j < phases.Count; j++)
                {
                    PhaseData phase = phases[j];
                    targetDamageDist[i][j] = JsonDamageDistBuilder.BuildJsonDamageDistList(
                        minions.GetDamageEvents(target, log, phase.Start, phase.End).GroupBy(x => x.SkillID).ToDictionary(x => x.Key, x => x.ToList()),
                        minions.GetBreakbarDamageEvents(target, log, phase.Start, phase.End).GroupBy(x => x.SkillID).ToDictionary(x => x.Key, x => x.ToList()),
                        log,
                        skillMap,
                        buffMap
                    );
                }
            }
            jsonMinions.TargetDamageDist = targetDamageDist;
        }
        if (log.CombatData.HasEXTHealing && !isEnemyMinion)
        {
            jsonMinions.EXTHealingStats = EXTJsonMinionsHealingStatsBuilder.BuildMinionsHealingStats(minions, log, skillMap, buffMap);
        }
        if (log.CombatData.HasEXTBarrier && !isEnemyMinion)
        {
            jsonMinions.EXTBarrierStats = EXTJsonMinionsBarrierStatsBuilder.BuildMinionsBarrierStats(minions, log, skillMap, buffMap);
        }
        if (log.CanCombatReplay)
        {
            jsonMinions.CombatReplayData = minions.MinionList.Select(x => JsonActorCombatReplayDataBuilder.BuildJsonActorCombatReplayDataBuilder(x, log, settings)).ToList();
        }
        return jsonMinions;
    }

}

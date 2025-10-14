﻿using GW2EIEvtcParser.EIData;
using GW2EIEvtcParser.Extensions;
using GW2EIEvtcParser.ParsedData;
using GW2EIEvtcParser.ParserHelpers;
using static GW2EIEvtcParser.ArcDPSEnums;
using static GW2EIEvtcParser.ParserHelper;
using static GW2EIEvtcParser.SpeciesIDs;

namespace GW2EIEvtcParser;

public static class AgentManipulationHelper
{

    internal delegate bool ExtraRedirection(CombatItem evt, AgentItem from, AgentItem to);
    internal delegate void StateEventProcessing(CombatItem evt, AgentItem from, AgentItem to);
    /// <summary>
    /// Method used to redirect a subset of events from redirectFrom to to
    /// </summary>
    /// <param name="combatData"></param>
    /// <param name="extensions"></param>
    /// <param name="agentData"></param>
    /// <param name="redirectFrom">AgentItem the events need to be redirected from</param>
    /// <param name="stateCopyFroms">AgentItems from where last known states (hp, position, etc) will be copied from</param>
    /// <param name="to">AgentItem the events need to be redirected to</param>
    /// <param name="copyPositionalDataFromAttackTarget">If true, "to" will get the positional data from attack targets, if possible</param>
    /// <param name="extraRedirections">function to handle special conditions, given event either src or dst matches from</param>
    internal static void RedirectNPCEventsAndCopyPreviousStates(List<CombatItem> combatData, IReadOnlyDictionary<uint, ExtensionHandler> extensions, AgentData agentData, AgentItem redirectFrom, List<AgentItem> stateCopyFroms, AgentItem to, bool copyPositionalDataFromAttackTarget, ExtraRedirection? extraRedirections = null, StateEventProcessing? stateEventProcessing = null)
    {
        if (!(redirectFrom.IsNPC && to.IsNPC))
        {
            throw new InvalidOperationException("Expected NPCs in RedirectNPCEventsAndCopyPreviousStates");
        }
        // Redirect combat events
        foreach (CombatItem evt in combatData)
        {
            if (to.InAwareTimes(evt.Time))
            {
                var srcMatchesAgent = evt.SrcMatchesAgent(redirectFrom, extensions);
                var dstMatchesAgent = evt.DstMatchesAgent(redirectFrom, extensions);
                if (extraRedirections != null && !extraRedirections(evt, redirectFrom, to))
                {
                    continue;
                }
                if (srcMatchesAgent)
                {
                    evt.OverrideSrcAgent(to);
                }
                if (dstMatchesAgent)
                {
                    evt.OverrideDstAgent(to);
                }
            }
        }
        // Copy attack targets
        var attackTargetAgents = new HashSet<AgentItem>();
        var attackTargetsToCopy = combatData.Where(x => x.IsStateChange == StateChange.AttackTarget && x.DstMatchesAgent(redirectFrom)).ToList();
        var targetableOns = combatData.Where(x => x.IsStateChange == StateChange.Targetable && x.DstAgent == 1);
        // Events copied
        var copied = new List<CombatItem>(attackTargetsToCopy.Count + 10);
        foreach (CombatItem c in attackTargetsToCopy)
        {
            var cExtra = new CombatItem(c);
            cExtra.OverrideTime(to.FirstAware - 1); // To make sure they are put before all actual agent events
            cExtra.OverrideDstAgent(to);
            combatData.Add(cExtra);
            copied.Add(cExtra);
            AgentItem at = agentData.GetAgent(c.SrcAgent, c.Time);
            if (targetableOns.Any(x => x.SrcMatchesAgent(at)))
            {
                attackTargetAgents.Add(at);
            }
        }
        // Copy states
        var stateEventsToCopy = new List<CombatItem>();
        Func<CombatItem, bool> canCopyFromAgent = (evt) => stateCopyFroms.Any(x => evt.SrcMatchesAgent(x));
        var stateChangeCopyFromAgentConditions = new List<Func<CombatItem, bool>>()
        {
            (x) => x.IsStateChange == StateChange.BreakbarState,
            (x) => x.IsStateChange == StateChange.MaxHealthUpdate,
            (x) => x.IsStateChange == StateChange.HealthUpdate,
            (x) => x.IsStateChange == StateChange.BreakbarPercent,
            (x) => x.IsStateChange == StateChange.BarrierUpdate,
            (x) => (x.IsStateChange == StateChange.EnterCombat || x.IsStateChange == StateChange.ExitCombat),
            (x) => (x.IsStateChange == StateChange.Spawn || x.IsStateChange == StateChange.Despawn || x.IsStateChange == StateChange.ChangeDead || x.IsStateChange == StateChange.ChangeDown || x.IsStateChange == StateChange.ChangeUp),
        };
        if (!copyPositionalDataFromAttackTarget || attackTargetAgents.Count == 0)
        {
            stateChangeCopyFromAgentConditions.Add((x) => x.IsStateChange == StateChange.Position);
            stateChangeCopyFromAgentConditions.Add((x) => x.IsStateChange == StateChange.Rotation);
            stateChangeCopyFromAgentConditions.Add((x) => x.IsStateChange == StateChange.Velocity);
        }
        foreach (Func<CombatItem, bool> stateChangeCopyCondition in stateChangeCopyFromAgentConditions)
        {
            CombatItem? stateToCopy = combatData.LastOrDefault(x => stateChangeCopyCondition(x) && canCopyFromAgent(x) && x.Time <= to.FirstAware);
            if (stateToCopy != null)
            {
                stateEventsToCopy.Add(stateToCopy);
            }
        }
        // Copy positional data from attack targets
        if (copyPositionalDataFromAttackTarget && attackTargetAgents.Count != 0)
        {
            Func<CombatItem, bool> canCopyFromAttackTarget = (evt) => attackTargetAgents.Any(x => evt.SrcMatchesAgent(x));
            var stateChangeCopyFromAttackTargetConditions = new List<Func<CombatItem, bool>>()
            {
                (x) => x.IsStateChange == StateChange.Position,
                (x) => x.IsStateChange == StateChange.Rotation,
                (x) => x.IsStateChange == StateChange.Velocity,
            };
            foreach (Func<CombatItem, bool> stateChangeCopyCondition in stateChangeCopyFromAttackTargetConditions)
            {
                CombatItem? stateToCopy = combatData.LastOrDefault(x => stateChangeCopyCondition(x) && canCopyFromAttackTarget(x) && x.Time <= to.FirstAware);
                if (stateToCopy != null)
                {
                    stateEventsToCopy.Add(stateToCopy);
                }
            }
        }
        if (stateEventsToCopy.Count > 0)
        {
            foreach (CombatItem c in stateEventsToCopy)
            {
                var cExtra = new CombatItem(c);
                cExtra.OverrideTime(to.FirstAware-1); // To make sure they are put before all actual agent events
                cExtra.OverrideSrcAgent(to);
                combatData.Add(cExtra);
                copied.Add(cExtra);
            }
        }
        if (copied.Count > 0)
        {
            combatData.SortByTime();
            foreach (CombatItem c in copied)
            {
                c.OverrideTime(to.FirstAware);
                if (stateEventProcessing != null)
                {
                    combatData.SortByTime();
                    stateEventProcessing(c, redirectFrom, to);
                }
            }
            if (stateEventProcessing != null)
            {
                combatData.SortByTime();
            }
        }
        // Redirect NPC and Gadget masters
        IReadOnlyList<AgentItem> masterRedirectionCandidates = [
             .. agentData.GetAgentByType(AgentItem.AgentType.NPC),
             .. agentData.GetAgentByType(AgentItem.AgentType.Gadget)
            ];
        foreach (AgentItem ag in masterRedirectionCandidates)
        {
            if (redirectFrom.Is(ag.Master) && to.InAwareTimes(ag.FirstAware))
            {
                ag.SetMaster(to);
            }
        }

        to.AddMergeFrom(redirectFrom, to.FirstAware, to.LastAware);
    }

    internal static void SplitPlayerPerSpecSubgroupAndSwap(IReadOnlyList<EnterCombatEvent> enterCombatEvents, IReadOnlyList<ExitCombatEvent> exitCombatEvents, IReadOnlyDictionary<uint, ExtensionHandler> extensions, AgentData agentData, AgentItem originalPlayer, bool splitByEnterCombat)
    {
        var player = new Player(originalPlayer, false);
        var previousSpec = player.Spec;
        var previousGroup = player.Group;
        var list = new List<(long start, AgentItem from, Spec spec, int group, bool checkExit)>();
        if (splitByEnterCombat)
        {
            bool ignore0Subgroups = originalPlayer.Type == AgentItem.AgentType.Player;
            for (var i = 0; i < enterCombatEvents.Count; i++)
            {
                var enterCombat = enterCombatEvents[i];
                if ((ignore0Subgroups && enterCombat.Subgroup == 0) || enterCombat.Spec == Spec.Unknown)
                {
                    continue;
                }
                if (enterCombat.Spec != previousSpec || enterCombat.Subgroup != previousGroup)
                {
                    previousSpec = enterCombat.Spec;
                    previousGroup = enterCombat.Subgroup;
                    long start = enterCombat.Time;
                    if (originalPlayer.Regrouped.Count > 0)
                    {
                        var copyFrom = originalPlayer.Regrouped.LastOrNull((in AgentItem.MergedAgentItem x) => x.Merged.InAwareTimes(start));
                        if (copyFrom != null)
                        {
                            list.Add((start, copyFrom.Value.Merged, enterCombat.Spec, enterCombat.Subgroup, true));
                        }
                        else
                        {
                            list.Add((start, originalPlayer, enterCombat.Spec, enterCombat.Subgroup, true));
                        }
                    }
                    else
                    {
                        list.Add((start, originalPlayer, enterCombat.Spec, enterCombat.Subgroup, true));
                    }
                }
            }
        }
        foreach (var regrouped in originalPlayer.Regrouped)
        {
            list.Add((regrouped.MergeStart, regrouped.Merged, regrouped.Merged.Spec, new Player(regrouped.Merged, false).Group, false));
        }
        list.Sort((x,y) => x.start.CompareTo(y.start));
        var previousPlayerAgent = originalPlayer;
        var firstSplit = true;
        previousSpec = player.Spec;
        previousGroup = player.Group;
        long previousStart = 0;
        foreach (var couple in list)
        {
            if (couple.spec != previousSpec || couple.group != previousGroup)
            {
                previousSpec = couple.spec;
                previousGroup = couple.group;
                long start = couple.start;
                var previousCombatExit = couple.checkExit ? exitCombatEvents.LastOrDefault(x => x.Time < start && x.Time > previousStart) : null;
                if (previousCombatExit != null)
                {
                    // we don't know when exactly the change happened, it was between previous exit and current start
                    start = (previousCombatExit.Time + 1 + start) / 2;
                }
                long end = previousPlayerAgent.LastAware;
                if (firstSplit)
                {
                    firstSplit = false;
                    if (couple.checkExit && previousCombatExit == null)
                    {
                        // we don't know when exactly the change happened, take half of the aware time
                        start = (previousPlayerAgent.FirstAware + start) / 2;
                    }
                    previousPlayerAgent = agentData.AddCustomAgentFrom(previousPlayerAgent, previousPlayerAgent.FirstAware, start - 1, previousPlayerAgent.Spec);
                    previousPlayerAgent.SetEnglobingAgentItem(originalPlayer, agentData);
                }

                var newPlayerAgent = agentData.AddCustomAgentFrom(couple.from, start, end, couple.spec);
                newPlayerAgent.SetEnglobingAgentItem(originalPlayer, agentData);

                previousPlayerAgent.OverrideAwareTimes(previousPlayerAgent.FirstAware, start - 1);
                previousPlayerAgent = newPlayerAgent;
                previousStart = start;
            }
        }
    }

    private static void RegroupAgents(AgentData agentData, IEnumerable<AgentItem> agentsToRegroup, IReadOnlyDictionary<AgentItem, List<CombatItem>> srcCombatDataDict, IReadOnlyDictionary<AgentItem, List<CombatItem>> dstCombatDataDict, List<AgentItem> toAdd, List<AgentItem> toRemove)
    {

        AgentItem firstItem = agentsToRegroup.First();
        var newAgent = new AgentItem(firstItem);
        newAgent.OverrideAwareTimes(agentsToRegroup.Min(x => x.FirstAware), agentsToRegroup.Max(x => x.LastAware));
        foreach (AgentItem agentItem in agentsToRegroup)
        {
            if (srcCombatDataDict.TryGetValue(agentItem, out var srcCombatItems))
            {
                srcCombatItems.ForEach(x => x.OverrideSrcAgent(newAgent));
            }
            if (dstCombatDataDict.TryGetValue(agentItem, out var dstCombatItems))
            {
                dstCombatItems.ForEach(x => x.OverrideDstAgent(newAgent));
            }
            agentData.SwapMasters(agentItem, newAgent);
            newAgent.AddRegroupedFrom(agentItem);
        }
        toRemove.AddRange(agentsToRegroup);
        toAdd.Add(newAgent);
    }

    internal static void RegroupSameAgentsAndDetermineTeams(AgentData agentData, IReadOnlyList<CombatItem> combatItems, EvtcVersionEvent evtcVersion, IReadOnlyDictionary<uint, ExtensionHandler> extensions)
    {
        var toRemove = new List<AgentItem>(100);
        var toAdd = new List<AgentItem>(30);
        var squadCombatStartCombatEnds = new List<long>(30) { long.MinValue };
        squadCombatStartCombatEnds.AddRange(combatItems
            .Where(x => x.IsStateChange == StateChange.SquadCombatStart || x.IsStateChange == StateChange.SquadCombatEnd)
            .Select(x => x.Time));
        squadCombatStartCombatEnds.Add(long.MaxValue);
        var combatDataDict = combatItems.Where(x => x.SrcIsAgent(extensions) || x.DstIsAgent(extensions));
        var srcCombatDataDict = combatDataDict.Where(x => x.SrcIsAgent(extensions)).GroupBy(x => agentData.GetAgent(x.SrcAgent, x.Time)).ToDictionary(x => x.Key, x => x.ToList());
        var dstCombatDataDict = combatDataDict.Where(x => x.DstIsAgent(extensions)).GroupBy(x => agentData.GetAgent(x.DstAgent, x.Time)).ToDictionary(x => x.Key, x => x.ToList());
        // NPCs
        {
            var npcsByInstIDs = agentData.GetAgentByType(AgentItem.AgentType.NPC).Where(x => !x.IsNonIdentifiedSpecies()).GroupBy(x => x.InstID).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var npcsByInstdID in npcsByInstIDs)
            {
                var agentToRegroup = new List<AgentItem>(5);
                var previousAgent = npcsByInstdID.Value[0];
                var previousStateTime = squadCombatStartCombatEnds[0];
                foreach (var curAgent in npcsByInstdID.Value)
                {
                    var curStateTime = squadCombatStartCombatEnds.Last(x => x <= (curAgent.LastAware + curAgent.FirstAware) / 2);
                    if (previousAgent.ID == curAgent.ID && curAgent.Master == previousAgent.Master && curStateTime == previousStateTime)
                    {
                        agentToRegroup.Add(curAgent);
                    } 
                    else
                    {
                        if (agentToRegroup.Count > 1)
                        {
                            RegroupAgents(agentData, agentToRegroup, srcCombatDataDict, dstCombatDataDict, toAdd, toRemove);
                        }
                        agentToRegroup = new List<AgentItem>(5) { curAgent };
                        previousAgent = curAgent;
                        previousStateTime = curStateTime;
                    }
                }
                if (agentToRegroup.Count > 1)
                {
                    RegroupAgents(agentData, agentToRegroup, srcCombatDataDict, dstCombatDataDict, toAdd, toRemove);
                }
            }
        }
        // Non squad Players
        {
            IReadOnlyList<AgentItem> nonSquadPlayerAgents = agentData.GetAgentByType(AgentItem.AgentType.NonSquadPlayer);
            if (nonSquadPlayerAgents.Any())
            {
                var teamChangeDict = combatItems.Where(x => x.IsStateChange == StateChange.TeamChange).GroupBy(x => x.SrcAgent).ToDictionary(x => x.Key, x => x.ToList());
                IReadOnlyList<AgentItem> squadPlayers = agentData.GetAgentByType(AgentItem.AgentType.Player);
                ulong greenTeam = ulong.MaxValue;
                var greenTeams = new List<ulong>();
                foreach (AgentItem a in squadPlayers)
                {
                    if (teamChangeDict.TryGetValue(a.Agent, out var teamChangeList))
                    {
                        greenTeams.AddRange(teamChangeList.Where(x => x.SrcMatchesAgent(a)).Select(TeamChangeEvent.GetTeamIDInto));
                        if (evtcVersion.Build > ArcDPSBuilds.TeamChangeOnDespawn)
                        {
                            greenTeams.AddRange(teamChangeList.Where(x => x.SrcMatchesAgent(a)).Select(TeamChangeEvent.GetTeamIDComingFrom));
                        }
                    }
                }
                greenTeams.RemoveAll(x => x == 0);
                if (greenTeams.Count != 0)
                {
                    greenTeam = greenTeams.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => x.Key).First();
                }
                foreach (AgentItem nonSquadPlayer in nonSquadPlayerAgents)
                {
                    if (teamChangeDict.TryGetValue(nonSquadPlayer.Agent, out var teamChangeList))
                    {
                        var team = teamChangeList.Where(x => x.SrcMatchesAgent(nonSquadPlayer)).Select(TeamChangeEvent.GetTeamIDInto).ToList();
                        if (evtcVersion.Build > ArcDPSBuilds.TeamChangeOnDespawn)
                        {
                            team.AddRange(teamChangeList.Where(x => x.SrcMatchesAgent(nonSquadPlayer)).Select(TeamChangeEvent.GetTeamIDComingFrom));
                        }
                        team.RemoveAll(x => x == 0);
                        nonSquadPlayer.OverrideIsNotInSquadFriendlyPlayer(team.Any(x => x == greenTeam));
                    }
                }
                var nonSquadPlayersByInstids = nonSquadPlayerAgents.GroupBy(x => x.InstID).ToDictionary(x => x.Key, x => x.ToList());
                foreach (var nonSquadPlayersByInstid in nonSquadPlayersByInstids)
                {
                    var agents = nonSquadPlayersByInstid.Value;
                    if (agents.Count > 1)
                    {
                        RegroupAgents(agentData, agents, srcCombatDataDict, dstCombatDataDict, toAdd, toRemove);
                    }
                }
            }
        }
        // Players
        {
            IReadOnlyList<Player> playerAgents = agentData.GetAgentByType(AgentItem.AgentType.Player).Select(x => new Player(x, false)).ToList();
            var playersByAccounts = playerAgents.GroupBy(x => x.Account).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var playersByAccount in playersByAccounts)
            {
                var players = playersByAccount.Value;
                if (players.Count > 1)
                {
                    RegroupAgents(agentData, players.Select(x => x.AgentItem), srcCombatDataDict, dstCombatDataDict, toAdd, toRemove);
                }
            }
        }
        agentData.ReplaceAgents(toRemove, toAdd);
    }

    /// <summary>
    /// Method used to redirect all events from redirectFrom to to
    /// </summary>
    /// <param name="combatData"></param>
    /// <param name="extensions"></param>
    /// <param name="agentData"></param>
    /// <param name="redirectFrom">AgentItem the events need to be redirected from</param>
    /// <param name="to">AgentItem the events need to be redirected to</param>
    /// <param name="extraRedirections">function to handle special conditions, given event either src or dst matches from</param>
    internal static void RedirectAllEvents(IReadOnlyList<CombatItem> combatData, IReadOnlyDictionary<uint, ExtensionHandler> extensions, AgentData agentData, AgentItem redirectFrom, AgentItem to, ExtraRedirection? extraRedirections = null)
    {
        // Redirect combat events
        foreach (CombatItem evt in combatData)
        {
            var srcMatchesAgent = evt.SrcMatchesAgent(redirectFrom, extensions);
            var dstMatchesAgent = evt.DstMatchesAgent(redirectFrom, extensions);
            if (!dstMatchesAgent && !srcMatchesAgent)
            {
                continue;
            }
            if (extraRedirections != null && !extraRedirections(evt, redirectFrom, to))
            {
                continue;
            }
            if (srcMatchesAgent)
            {
                evt.OverrideSrcAgent(to);
            }
            if (dstMatchesAgent)
            {
                evt.OverrideDstAgent(to);
            }
        }
        agentData.SwapMasters(redirectFrom, to);
        to.AddMergeFrom(redirectFrom, redirectFrom.FirstAware, redirectFrom.LastAware);
    }

}

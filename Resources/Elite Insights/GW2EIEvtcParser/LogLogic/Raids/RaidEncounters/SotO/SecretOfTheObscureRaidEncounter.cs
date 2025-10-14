﻿using GW2EIEvtcParser.ParsedData;
using static GW2EIEvtcParser.ArcDPSEnums;
using static GW2EIEvtcParser.LogLogic.LogCategories;
using static GW2EIEvtcParser.LogLogic.LogLogicTimeUtils;

namespace GW2EIEvtcParser.LogLogic;

internal abstract class SecretOfTheObscureRaidEncounter : RaidEncounterLogic
{
    public SecretOfTheObscureRaidEncounter(int triggerID) : base(triggerID)
    {
        LogCategoryInformation.SubCategory = SubLogCategory.SotO;
        LogID |= LogIDs.RaidEncounterMasks.SotOMask;
    }

    internal override void CheckSuccess(CombatData combatData, AgentData agentData, LogData logData, IReadOnlyCollection<AgentItem> playerAgents)
    {
        if (IsInstance)
        {
            logData.SetSuccess(true, GetFinalMapChangeTime(logData, combatData));
            return;
        }
        IReadOnlyList<RewardEvent> rewards = combatData.GetRewardEvents();
        RewardEvent? reward = rewards.FirstOrDefault(x => x.RewardType == RewardTypes.PostEoDRaidEncounterReward && x.Time > logData.LogStart);
        if (reward != null)
        {
            logData.SetSuccess(true, reward.Time);
        }
        else
        {
            NoBouncyChestGenericCheckSucess(combatData, agentData, logData, playerAgents);
        }
    }
}

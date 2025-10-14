﻿using GW2EIEvtcParser.ParsedData;

namespace GW2EIEvtcParser.EIData;

internal class BreakbarDamageCastFinder : CheckedCastFinder<BreakbarDamageEvent>
{
    private readonly long _damageSkillID;

    public BreakbarDamageCastFinder(long skillID, long damageSkillID) : base(skillID)
    {
        UsingNotAccurate();
        _damageSkillID = damageSkillID;
    }

    public override List<InstantCastEvent> ComputeInstantCast(CombatData combatData, SkillData skillData, AgentData agentData)
    {
        var res = new List<InstantCastEvent>();
        var damages = combatData.GetBreakbarDamageData(_damageSkillID).GroupBy(x => x.From);
        foreach (var group in damages)
        {
            long lastTime = int.MinValue;
            foreach (BreakbarDamageEvent de in group)
            {
                if (CheckCondition(de, combatData, agentData, skillData))
                {
                    if (de.Time - lastTime < ICD)
                    {
                        lastTime = de.Time;
                        continue;
                    }
                    lastTime = de.Time;
                    res.Add(new InstantCastEvent(GetTime(de, de.From, combatData), skillData.Get(SkillID), de.From));
                }
            }
        }
        return res;
    }
}

﻿using GW2EIEvtcParser.ParsedData;
using static GW2EIEvtcParser.EIData.CircleDecoration;

namespace GW2EIEvtcParser.EIData;

public class CircleDecorationRenderingDescription : FormDecorationRenderingDescription
{

    internal CircleDecorationRenderingDescription(ParsedEvtcLog log, CircleDecorationRenderingData decoration, CombatReplayMap map, Dictionary<long, SkillItem> usedSkills, Dictionary<long, Buff> usedBuffs, string metadataSignature) : base(log, decoration, map, usedSkills, usedBuffs, metadataSignature)
    {
        Type = Types.Circle;
    }
}

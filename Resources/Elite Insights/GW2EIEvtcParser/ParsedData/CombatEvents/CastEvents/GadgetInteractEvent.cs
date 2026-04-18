using System.Numerics;
using static GW2EIEvtcParser.ArcDPSEnums;

namespace GW2EIEvtcParser.ParsedData;

public class GadgetInteractEvent : AnimatedCastEvent
{

    public readonly AgentItem Gadget;

    internal GadgetInteractEvent(CombatItem? startItem, AgentData agentData, SkillData skillData, CombatItem? endItem, long maxEnd) : base(startItem, agentData, skillData, endItem, maxEnd)
    {
        var item = (startItem ?? endItem ?? throw new InvalidOperationException("Either start or end item must be non null"));
        Gadget = agentData.GetAgentByInstID((ushort)item.Pad, item.Time);
        // Bandaid, may not be perfect
        if (AnimStop != AnimationStop.AnyViaReset && AnimStop != AnimationStop.Ended && Status != AnimationStatus.Interrupted)
        {
            Status = AnimationStatus.Interrupted;
            SavedDuration = -ActualDuration;
        }
        ExpectedDuration = (int)(ExpectedDuration / AcceleratedToNonAcceleratedRatio);
        if (Status == AnimationStatus.Reduced)
        {
            int scaledExpectedDuration = (int)Math.Round(ExpectedDuration * AcceleratedToNonAcceleratedRatio);
            SavedDuration = Math.Max(scaledExpectedDuration - ActualDuration, 0);
        }
    }

}

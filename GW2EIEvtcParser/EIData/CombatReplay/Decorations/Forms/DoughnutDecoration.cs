﻿using GW2EIEvtcParser.ParsedData;

namespace GW2EIEvtcParser.EIData;

internal class DoughnutDecoration : FormDecoration
{
    public class DoughnutDecorationMetadata : FormDecorationMetadata
    {
        public readonly uint OuterRadius;
        public readonly uint InnerRadius;

        public DoughnutDecorationMetadata(string color, uint innerRadius, uint outerRadius) : base(color)
        {
            OuterRadius = Math.Max(outerRadius, 1);
            InnerRadius = innerRadius;
            if (OuterRadius <= InnerRadius)
            {
                throw new InvalidOperationException("OuterRadius must be > to InnerRadius");
            }
        }

        public override string GetSignature()
        {
            return "Dough" + OuterRadius + Color + InnerRadius;
        }
        public override DecorationMetadataDescription GetCombatReplayMetadataDescription()
        {
            return new DoughnutDecorationMetadataDescription(this);
        }
    }
    public class DoughnutDecorationRenderingData : FormDecorationRenderingData
    {
        public DoughnutDecorationRenderingData((long, long) lifespan, GeographicalConnector connector) : base(lifespan, connector)
        {
        }

        public override DecorationRenderingDescription GetCombatReplayRenderingDescription(CombatReplayMap map, ParsedEvtcLog log, Dictionary<long, SkillItem> usedSkills, Dictionary<long, Buff> usedBuffs, string metadataSignature)
        {
            return new DoughnutDecorationRenderingDescription(log, this, map, usedSkills, usedBuffs, metadataSignature);
        }
    }
    private new DoughnutDecorationMetadata DecorationMetadata => (DoughnutDecorationMetadata)base.DecorationMetadata;
    public uint OuterRadius => DecorationMetadata.OuterRadius;
    public uint InnerRadius => DecorationMetadata.InnerRadius;

    public DoughnutDecoration(uint innerRadius, uint outerRadius, (long start, long end) lifespan, string color, GeographicalConnector connector) : base(new DoughnutDecorationMetadata(color, innerRadius, outerRadius), new DoughnutDecorationRenderingData(lifespan, connector))
    {
    }
    public DoughnutDecoration(uint innerRadius, uint outerRadius, (long start, long end) lifespan, Color color, double opacity, GeographicalConnector connector) : this(innerRadius, outerRadius, lifespan, color.WithAlpha(opacity).ToString(true), connector)
    {
    }
    public override FormDecoration Copy(string? color = null)
    {
        return (FormDecoration)new DoughnutDecoration(InnerRadius, OuterRadius, Lifespan, color ?? Color, ConnectedTo).UsingFilled(Filled).UsingGrowingEnd(GrowingEnd, GrowingReverse).UsingRotationConnector(RotationConnectedTo).UsingSkillMode(SkillMode);
    }

    public override FormDecoration GetBorderDecoration(string? borderColor = null)
    {
        if (!Filled)
        {
            throw new InvalidOperationException("Non filled doughtnuts can't have borders");
        }
        return (DoughnutDecoration)Copy(borderColor).UsingFilled(false);
    }
    //

}

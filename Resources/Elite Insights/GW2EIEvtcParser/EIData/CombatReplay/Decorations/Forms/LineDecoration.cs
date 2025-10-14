﻿using GW2EIEvtcParser.ParsedData;

namespace GW2EIEvtcParser.EIData;

internal class LineDecoration : FormDecoration
{
    public class LineDecorationMetadata : FormDecorationMetadata
    {
        public bool WorldSizeThickness;
        public uint Thickness;
        public LineDecorationMetadata(string color, uint thickness = 2, bool worldSizeThickness = false) : base(color)
        {
            Thickness = thickness;
            WorldSizeThickness = worldSizeThickness;
        }

        public void WithThickess(uint thickness, bool worldSizeThickness = false)
        {
            Thickness = thickness;
            WorldSizeThickness = worldSizeThickness;
        }

        public override string GetSignature()
        {
            return "Line" + Color + "Thickness" + Thickness + "WorldSize" + WorldSizeThickness;
        }
        public override DecorationMetadataDescription GetCombatReplayMetadataDescription()
        {
            return new LineDecorationMetadataDescription(this);
        }
    }
    public class LineDecorationRenderingData : FormDecorationRenderingData
    {
        public readonly GeographicalConnector ConnectedFrom;
        public LineDecorationRenderingData((long, long) lifespan, GeographicalConnector connector, GeographicalConnector targetConnector) : base(lifespan, connector)
        {
            ConnectedFrom = targetConnector;
        }
        public override void UsingFilled(bool filled)
        {
        }
        public override void UsingRotationConnector(RotationConnector? rotationConnectedTo)
        {
        }

        public override DecorationRenderingDescription GetCombatReplayRenderingDescription(CombatReplayMap map, ParsedEvtcLog log, Dictionary<long, SkillItem> usedSkills, Dictionary<long, Buff> usedBuffs, string metadataSignature)
        {
            return new LineDecorationRenderingDescription(log, this, map, usedSkills, usedBuffs, metadataSignature);
        }
    }
    private new LineDecorationRenderingData DecorationRenderingData => (LineDecorationRenderingData)base.DecorationRenderingData;
    public GeographicalConnector ConnectedFrom => DecorationRenderingData.ConnectedFrom;
    private new LineDecorationMetadata DecorationMetadata => (LineDecorationMetadata)base.DecorationMetadata;
    public bool WorldSizeThickness => DecorationMetadata.WorldSizeThickness;
    public uint Thickness => DecorationMetadata.Thickness;

    public LineDecoration((long start, long end) lifespan, string color, GeographicalConnector connector, GeographicalConnector targetConnector) : base(new LineDecorationMetadata(color), new LineDecorationRenderingData(lifespan, connector, targetConnector))
    {
    }

    public LineDecoration((long start, long end) lifespan, Color color, double opacity, GeographicalConnector connector, GeographicalConnector targetConnector) : this(lifespan, color.WithAlpha(opacity).ToString(true), connector, targetConnector)
    {
    }

    public LineDecoration(Segment lifespan, string color, GeographicalConnector connector, GeographicalConnector targetConnector) : this((lifespan.Start, lifespan.End), color, connector, targetConnector)
    {
    }
    public LineDecoration(Segment lifespan, Color color, double opacity, GeographicalConnector connector, GeographicalConnector targetConnector) : this((lifespan.Start, lifespan.End), color.WithAlpha(opacity).ToString(true), connector, targetConnector)
    {
    }
    public override FormDecoration Copy(string? color = null)
    {
        return (FormDecoration)new LineDecoration(Lifespan, color ?? Color, ConnectedTo, ConnectedFrom).UsingFilled(Filled).UsingGrowingEnd(GrowingEnd, GrowingReverse).UsingRotationConnector(RotationConnectedTo).UsingSkillMode(SkillMode);
    }

    public LineDecoration WithThickess(uint thickness, bool worldSizeThickness = false)
    {
        DecorationMetadata.WithThickess(thickness, worldSizeThickness);
        return this;
    }

    public override FormDecoration GetBorderDecoration(string? borderColor = null)
    {
        throw new InvalidOperationException("Lines can't have borders");
    }
    //
}

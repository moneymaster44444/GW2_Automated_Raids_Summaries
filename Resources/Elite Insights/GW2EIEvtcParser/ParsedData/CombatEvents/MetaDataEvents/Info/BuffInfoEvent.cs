﻿using static GW2EIEvtcParser.ArcDPSEnums;

namespace GW2EIEvtcParser.ParsedData;

public class BuffInfoEvent : MetaDataEvent
{
    public readonly long BuffID;

    public bool ProbablyInvul { get; private set; }

    public bool ProbablyInvert { get; private set; }

    //public BuffCategory Category { get; private set; }

    public byte CategoryByte { get; private set; }

    public byte StackingTypeByte { get; private set; } = 6;
    public BuffStackType StackingType { get; private set; } = BuffStackType.Unknown;

    public bool ProbablyResistance { get; private set; }

    public ushort MaxStacks { get; private set; }
    public uint DurationCap { get; private set; }
    public readonly List<BuffFormula> Formulas = [];

    internal BuffInfoEvent(CombatItem evtcItem, EvtcVersionEvent evtcVersion) : base(evtcItem)
    {
        BuffID = evtcItem.SkillID;
        CompleteBuffInfoEvent(evtcItem, evtcVersion);
    }

    internal void CompleteBuffInfoEvent(CombatItem evtcItem, EvtcVersionEvent evtcVersion)
    {
        if (evtcItem.SkillID != BuffID)
        {
            throw new InvalidOperationException("Non matching buff id in BuffDataEvent complete method");
        }
        switch (evtcItem.IsStateChange)
        {
            case StateChange.BuffFormula:
                BuildFromBuffFormula(evtcItem, evtcVersion);
                break;
            case StateChange.BuffInfo:
                BuildFromBuffInfo(evtcItem, evtcVersion);
                break;
            default:
                throw new InvalidDataException("Invalid combat event in BuffDataEvent complete method");
        }
    }

    private void BuildFromBuffInfo(CombatItem evtcItem, EvtcVersionEvent evtcVersion)
    {
        ProbablyInvul = evtcItem.IsFlanking > 0;
        ProbablyInvert = evtcItem.IsShields > 0;
        //Category = GetBuffCategory(evtcItem.IsOffcycle);
        CategoryByte = evtcItem.IsOffcycle;
        MaxStacks = evtcItem.SrcMasterInstid;
        DurationCap = evtcItem.OverstackValue;
        // This was most likely working correctly before that evtc build but I can't remember when the missing Pad1 issue was fixed.
        if (evtcVersion.Build >= ArcDPSBuilds.BuffAttrFlatIncRemoved)
        {
            StackingTypeByte = evtcItem.Pad1;
            StackingType = GetBuffStackType(StackingTypeByte);
        }
        ProbablyResistance = evtcItem.Pad2 > 0;
    }

    internal void AdjustBuffInfo(Dictionary<byte, BuffAttribute> solved)
    {
        Formulas.Sort((x, y) => (x.SortKey).CompareTo(y.SortKey));
        if (solved.Count == 0)
        {
            return;
        }
        foreach (BuffFormula formula in Formulas)
        {
            formula.AdjustUnknownFormulaAttributes(solved);
        }
    }

    private void BuildFromBuffFormula(CombatItem evtcItem, EvtcVersionEvent evtcVersion)
    {
        Formulas.Add(new BuffFormula(evtcItem, evtcVersion));
    }

}

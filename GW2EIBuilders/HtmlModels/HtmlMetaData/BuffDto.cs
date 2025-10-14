﻿using GW2EIEvtcParser;
using GW2EIEvtcParser.EIData;
using GW2EIEvtcParser.ParsedData;

namespace GW2EIBuilders.HtmlModels.HTMLMetaData;

internal class BuffDto : IDItemDto
{
    public string? Description { get; set; } = null;
    public bool Stacking { get; set; }
    public bool Consumable { get; set; }
    public bool EncounterSpecific { get; set; }

    public BuffDto(Buff buff, ParsedEvtcLog log) : base(buff, log)
    {
        Stacking = (buff.Type == Buff.BuffType.Intensity);
        Consumable = (buff.Classification == Buff.BuffClassification.Nourishment || buff.Classification == Buff.BuffClassification.Enhancement || buff.Classification == Buff.BuffClassification.OtherConsumable);
        EncounterSpecific = (buff.Source == ParserHelper.Source.EncounterSpecific || buff.Source == ParserHelper.Source.FractalInstability);
        BuffInfoEvent? buffInfoEvent = log.CombatData.GetBuffInfoEvent(buff.ID);
        if (buffInfoEvent != null)
        {
            var descriptions = new List<string>() {
                "ID: " + buff.ID,
                "Max Stack(s) " + buffInfoEvent.MaxStacks
            };
            if (buffInfoEvent.DurationCap > 0)
            {
                descriptions.Add("Duration Cap: " + Math.Round(buffInfoEvent.DurationCap / 1000.0, 3) + " seconds");
            }
            foreach (BuffFormula formula in buffInfoEvent.Formulas)
            {
                if (formula.IsConditional)
                {
                    continue;
                }
                string desc = formula.GetDescription(false, log.Buffs.BuffsByIDs, buff);
                if (desc.Length > 0)
                {
                    descriptions.Add(desc);
                }
            }
            Description = "";
            foreach (string desc in descriptions)
            {
                Description += desc + "<br>";
            }
        }
    }

    public static void AssembleBuffs(ICollection<Buff> buffs, Dictionary<string, BuffDto> dict, ParsedEvtcLog log)
    {
        foreach (Buff buff in buffs)
        {
            dict["b" + buff.ID] = new BuffDto(buff, log);
        }
    }
}

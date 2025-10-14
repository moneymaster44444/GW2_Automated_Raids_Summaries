﻿using System.Diagnostics.CodeAnalysis;

namespace GW2EIEvtcParser.ParsedData;

public class SkillData
{
    // Fields
    private readonly Dictionary<long, SkillItem> _skills = [];
    private readonly GW2EIGW2API.GW2APIController _apiController;
    public readonly long DodgeID;
    public readonly long GenericBreakbarID;
    // Public Methods

    internal SkillData(GW2EIGW2API.GW2APIController apiController, EvtcVersionEvent evtcVersion)
    {
        _apiController = apiController;
        (DodgeID, GenericBreakbarID) = SkillItem.GetArcDPSCustomIDs(evtcVersion);
    }

    public SkillItem Get(long ID)
    {
        if (_skills.TryGetValue(ID, out var value))
        {
            return value;
        }
        Add(ID, SkillItem.DefaultName);
        return _skills[ID];
    }

    
    internal bool TryGet(long ID, [NotNullWhen(true)] out SkillItem? skillItem)
    {
        return _skills.TryGetValue(ID, out skillItem);
    }

    internal HashSet<long> NotAccurate = [];

    public bool IsNotAccurate(long ID)
    {
        return NotAccurate.Contains(ID);
    }

    internal HashSet<long> GearProc = [];
    public bool IsGearProc(long ID)
    {
        return GearProc.Contains(ID);
    }

    internal HashSet<long> TraitProc = [];
    public bool IsTraitProc(long ID)
    {
        return TraitProc.Contains(ID);
    }

    internal HashSet<long> UnconditionalProc = [];
    public bool IsUnconditionalProc(long ID)
    {
        return UnconditionalProc.Contains(ID);
    }

    internal void Add(long id, string name)
    {
        if (!_skills.ContainsKey(id))
        {
            _skills.Add(id, new SkillItem(id, name, _apiController));
        }
    }

    internal void CombineWithSkillInfo(Dictionary<long, SkillInfoEvent> skillInfoEvents)
    {
        foreach (KeyValuePair<long, SkillItem> pair in _skills)
        {
            if (skillInfoEvents.TryGetValue(pair.Key, out var skillInfoEvent))
            {
                pair.Value.AttachSkillInfoEvent(skillInfoEvent);
            }
        }
    }

}

﻿using GW2EIEvtcParser.ParsedData;
using GW2EIEvtcParser.ParserHelpers;
using static GW2EIEvtcParser.ArcDPSEnums;
using static GW2EIEvtcParser.DamageModifierIDs;
using static GW2EIEvtcParser.EIData.Buff;
using static GW2EIEvtcParser.EIData.DamageModifiersUtils;
using static GW2EIEvtcParser.ParserHelper;
using static GW2EIEvtcParser.SkillIDs;

namespace GW2EIEvtcParser.EIData;

internal static class WillbenderHelper
{

    internal static readonly List<InstantCastFinder> InstantCastFinder = [];

    private static bool LethalTempoChecker(DamageEvent x, ParsedEvtcLog log, long threshold)
    {
        AgentItem src = x.From;
        var effectApply = log.CombatData.GetBuffApplyDataByIDBySrc(LethalTempo, src)
            .LastOrDefault(bae => bae.Time <= x.Time && Math.Abs(bae.AppliedDuration - threshold) < ServerDelayConstant);
        if (effectApply != null)
        {
            return x.Time - effectApply.Time < threshold + ServerDelayConstant;
        }
        return false;
    }

    private static bool LethalTempoEarlyExit(SingleActor a, ParsedEvtcLog log, long threshold)
    {
        AgentItem src = a.AgentItem;
        return !log.CombatData.GetBuffApplyDataByIDBySrc(LethalTempo, src)
            .Any(bae => Math.Abs(bae.AppliedDuration - threshold) < ServerDelayConstant);
    }

    internal static readonly IReadOnlyList<DamageModifierDescriptor> OutgoingDamageModifiers =
    [
        // Lethal Tempo PVE
        new BuffOnActorDamageModifier(Mod_LethalTempo, LethalTempo, "Lethal Tempo", "3% per stack", DamageSource.NoPets, 3.0, DamageType.Strike, DamageType.All, Source.Willbender, ByStack, TraitImages.LethalTempo, DamageModifierMode.All)
            .UsingEarlyExit((a, log) => LethalTempoEarlyExit(a, log, 6000))
            .UsingChecker( (x, log) => LethalTempoChecker(x, log, 6000))
            .UsingApproximate()
            .WithBuilds(GW2Builds.EODBeta1, GW2Builds.EODBeta4),
        new BuffOnActorDamageModifier(Mod_LethalTempo, LethalTempo, "Lethal Tempo", "1% per stack", DamageSource.NoPets, 1.0, DamageType.StrikeAndCondition, DamageType.All, Source.Willbender, ByStack, TraitImages.LethalTempo, DamageModifierMode.PvE)
            .UsingEarlyExit((a, log) => LethalTempoEarlyExit(a, log, 6000))
            .UsingChecker((x, log) => LethalTempoChecker(x, log, 6000))
            .UsingApproximate()
            .WithBuilds(GW2Builds.EODBeta4, GW2Builds.November2022Balance),
        new BuffOnActorDamageModifier(Mod_LethalTempo, LethalTempo, "Lethal Tempo", "2% per stack", DamageSource.NoPets, 2.0, DamageType.StrikeAndCondition, DamageType.All, Source.Willbender, ByStack, TraitImages.LethalTempo, DamageModifierMode.PvE)
            .UsingEarlyExit((a, log) => LethalTempoEarlyExit(a, log, 6000))
            .UsingChecker((x, log) => LethalTempoChecker(x, log, 6000))
            .UsingApproximate()
            .WithBuilds(GW2Builds.November2022Balance),
        // Tyrant's Lethal Tempo PVE
        new BuffOnActorDamageModifier(Mod_TyrantsLethalTempo, LethalTempo, "Tyrant's Lethal Tempo", "5% per stack", DamageSource.NoPets, 5.0, DamageType.Strike, DamageType.All, Source.Willbender, ByStack, TraitImages.TyrantsMomentum, DamageModifierMode.All)
            .UsingEarlyExit((a, log) => LethalTempoEarlyExit(a, log, 4000))
            .UsingChecker((x, log) => LethalTempoChecker(x, log, 4000))
            .UsingApproximate()
            .WithBuilds(GW2Builds.EODBeta1, GW2Builds.EODBeta4),
        new BuffOnActorDamageModifier(Mod_TyrantsLethalTempo, LethalTempo, "Tyrant's Lethal Tempo", "3% per stack", DamageSource.NoPets, 3.0, DamageType.StrikeAndCondition, DamageType.All, Source.Willbender, ByStack, TraitImages.TyrantsMomentum, DamageModifierMode.PvE)
            .UsingEarlyExit((a, log) => LethalTempoEarlyExit(a, log, 4000))
            .UsingChecker((x, log) => LethalTempoChecker(x, log, 4000))
            .UsingApproximate()
            .WithBuilds(GW2Builds.EODBeta4, GW2Builds.November2022Balance),
        new BuffOnActorDamageModifier(Mod_TyrantsLethalTempo, LethalTempo, "Tyrant's Lethal Tempo", "5% per stack", DamageSource.NoPets, 5.0, DamageType.StrikeAndCondition, DamageType.All, Source.Willbender, ByStack, TraitImages.TyrantsMomentum, DamageModifierMode.PvE)
            .UsingEarlyExit((a, log) => LethalTempoEarlyExit(a, log, 4000))
            .UsingChecker((x, log) => LethalTempoChecker(x, log, 4000))
            .UsingApproximate()
            .WithBuilds(GW2Builds.November2022Balance, GW2Builds.April2025Balance),
        new BuffOnActorDamageModifier(Mod_TyrantsLethalTempoStrike, LethalTempo, "Tyrant's Lethal Tempo (Strike)", "5% per stack", DamageSource.NoPets, 5.0, DamageType.Strike, DamageType.All, Source.Willbender, ByStack, TraitImages.TyrantsMomentum, DamageModifierMode.PvE)
            .UsingEarlyExit((a, log) => LethalTempoEarlyExit(a, log, 4000))
            .UsingChecker((x, log) => LethalTempoChecker(x, log, 4000))
            .UsingApproximate()
            .WithBuilds(GW2Builds.April2025Balance),
        new BuffOnActorDamageModifier(Mod_TyrantsLethalTempoCondition, LethalTempo, "Tyrant's Lethal Tempo (Condition)", "4% per stack", DamageSource.NoPets, 4.0, DamageType.Condition, DamageType.All, Source.Willbender, ByStack, TraitImages.TyrantsMomentum, DamageModifierMode.PvE)
            .UsingEarlyExit((a, log) => LethalTempoEarlyExit(a, log, 4000))
            .UsingChecker((x, log) => LethalTempoChecker(x, log, 4000))
            .UsingApproximate()
            .WithBuilds(GW2Builds.April2025Balance),
        // Lethal Tempo Competitive modes
        new BuffOnActorDamageModifier(Mod_LethalTempo, LethalTempo, "Lethal Tempo", "2% per stack", DamageSource.NoPets, 2.0, DamageType.StrikeAndCondition, DamageType.All, Source.Willbender, ByStack, TraitImages.LethalTempo, DamageModifierMode.sPvPWvW)
            .UsingEarlyExit((a, log) => LethalTempoEarlyExit(a, log, 6000))
            .UsingChecker((x, log) => LethalTempoChecker(x, log, 6000))
            .UsingApproximate()
            .WithBuilds(GW2Builds.EODBeta4, GW2Builds.March2022Balance2),
        new BuffOnActorDamageModifier(Mod_LethalTempo, LethalTempo, "Lethal Tempo", "3% per stack", DamageSource.NoPets, 3.0, DamageType.StrikeAndCondition, DamageType.All, Source.Willbender, ByStack, TraitImages.LethalTempo, DamageModifierMode.sPvPWvW)
            .UsingEarlyExit((a, log) => LethalTempoEarlyExit(a, log, 6000))
            .UsingChecker((x, log) => LethalTempoChecker(x, log, 6000))
            .UsingApproximate()
            .WithBuilds(GW2Builds.March2022Balance2),
        // Tyrant's Lethal Tempo Competitive modes
        new BuffOnActorDamageModifier(Mod_TyrantsLethalTempo, LethalTempo, "Tyrant's Lethal Tempo", "4% per stack", DamageSource.NoPets, 4.0, DamageType.StrikeAndCondition, DamageType.All, Source.Willbender, ByStack, TraitImages.TyrantsMomentum, DamageModifierMode.sPvPWvW)
            .UsingEarlyExit((a, log) => LethalTempoEarlyExit(a, log, 4000))
            .UsingChecker((x, log) => LethalTempoChecker(x, log, 4000))
            .UsingApproximate()
            .WithBuilds(GW2Builds.EODBeta4, GW2Builds.March2022Balance2),
        new BuffOnActorDamageModifier(Mod_TyrantsLethalTempo, LethalTempo, "Tyrant's Lethal Tempo", "5% per stack", DamageSource.NoPets, 5.0, DamageType.StrikeAndCondition, DamageType.All, Source.Willbender, ByStack, TraitImages.TyrantsMomentum, DamageModifierMode.sPvPWvW)
            .UsingEarlyExit((a, log) => LethalTempoEarlyExit(a, log, 4000))
            .UsingChecker((x, log) => LethalTempoChecker(x, log, 4000))
            .UsingApproximate()
            .WithBuilds(GW2Builds.March2022Balance2),
        // Rushing Justice
        new CounterOnActorDamageModifier(Mod_RushingJustice, RushingJusticeBuff, "Rushing Justice", "Applies burning on consecutive hits", DamageSource.NoPets, DamageType.Strike, DamageType.Strike, Source.Willbender, SkillImages.RushingJustice, DamageModifierMode.All)
            .WithBuilds(GW2Builds.EODBeta1)
    ];

    internal static readonly IReadOnlyList<DamageModifierDescriptor> IncomingDamageModifiers =
    [
        // Crashing Courage
        new BuffOnActorDamageModifier(Mod_DeathlessCourage, CrashingCourage, "Deathless Courage", "50%", DamageSource.Incoming, 50.0, DamageType.StrikeAndCondition, DamageType.All, Source.Willbender, ByPresence, TraitImages.DeathlessCourage, DamageModifierMode.PvE)
            .WithBuilds(GW2Builds.March2024BalanceAndCerusLegendary),
        new BuffOnActorDamageModifier(Mod_DeathlessCourage, CrashingCourage, "Deathless Courage", "20%", DamageSource.Incoming, 20.0, DamageType.StrikeAndCondition, DamageType.All, Source.Willbender, ByPresence, TraitImages.DeathlessCourage, DamageModifierMode.sPvPWvW)
            .WithBuilds(GW2Builds.March2024BalanceAndCerusLegendary)
    ];

    internal static readonly IReadOnlyList<Buff> Buffs =
    [
        // Virtues
        new Buff("Rushing Justice", RushingJusticeBuff, Source.Willbender, BuffClassification.Other, SkillImages.RushingJustice),
        new Buff("Flowing Resolve", FlowingResolveBuff, Source.Willbender, BuffStackType.Queue, 9, BuffClassification.Other, SkillImages.FlowingResolve),
        new Buff("Crashing Courage", CrashingCourage, Source.Willbender, BuffClassification.Other, SkillImages.CrashingCourage),
        //
        new Buff("Repose", Repose, Source.Willbender, BuffClassification.Other, SkillImages.Repose),
        new Buff("Lethal Tempo", LethalTempo, Source.Willbender, BuffStackType.Stacking, 5, BuffClassification.Other, TraitImages.LethalTempo),
    ];
}

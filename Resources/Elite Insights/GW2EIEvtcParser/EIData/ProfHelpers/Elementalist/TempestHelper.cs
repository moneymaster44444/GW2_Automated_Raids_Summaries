﻿using GW2EIEvtcParser.ParsedData;
using GW2EIEvtcParser.ParserHelpers;
using static GW2EIEvtcParser.ArcDPSEnums;
using static GW2EIEvtcParser.DamageModifierIDs;
using static GW2EIEvtcParser.EIData.Buff;
using static GW2EIEvtcParser.EIData.DamageModifiersUtils;
using static GW2EIEvtcParser.EIData.ProfHelper;
using static GW2EIEvtcParser.ParserHelper;
using static GW2EIEvtcParser.SkillIDs;

namespace GW2EIEvtcParser.EIData;

internal static class TempestHelper
{
    internal static readonly List<InstantCastFinder> InstantCastFinder =
    [
        new EffectCastFinder(FeelTheBurn, EffectGUIDs.TempestFeelTheBurn)
            .UsingSrcSpecChecker(Spec.Tempest),
        new EffectCastFinder(EyeOfTheStormShout, EffectGUIDs.TempestEyeOfTheStorm1)
            .UsingSrcSpecChecker(Spec.Tempest),
    ];

    internal static readonly IReadOnlyList<DamageModifierDescriptor> OutgoingDamageModifiers =
    [
        // Harmonious Conduit
        new BuffOnActorDamageModifier(Mod_HarmoniousConduit, HarmoniousConduit, "Harmonious Conduit", "10% (4s) after overload", DamageSource.NoPets, 10.0, DamageType.Strike, DamageType.All, Source.Tempest, ByPresence, TraitImages.HarmoniousConduit, DamageModifierMode.PvE)
            .WithBuilds(GW2Builds.StartOfLife ,GW2Builds.October2019Balance),
        // Trascendent Tempest
        new BuffOnActorDamageModifier(Mod_TranscendentTempest, TranscendentTempest, "Transcendent Tempest", "7% after overload", DamageSource.NoPets, 7.0, DamageType.StrikeAndCondition, DamageType.All, Source.Tempest, ByPresence, TraitImages.TranscendentTempest, DamageModifierMode.All)
            .WithBuilds(GW2Builds.October2019Balance, GW2Builds.August2022Balance),
        new BuffOnActorDamageModifier(Mod_TranscendentTempest, TranscendentTempest, "Transcendent Tempest", "7% after overload", DamageSource.NoPets, 7.0, DamageType.StrikeAndCondition, DamageType.All, Source.Tempest, ByPresence, TraitImages.TranscendentTempest, DamageModifierMode.sPvPWvW)
            .WithBuilds(GW2Builds.August2022Balance),
        new BuffOnActorDamageModifier(Mod_TranscendentTempest, TranscendentTempest, "Transcendent Tempest", "15% after overload", DamageSource.NoPets, 15.0, DamageType.StrikeAndCondition, DamageType.All, Source.Tempest, ByPresence, TraitImages.TranscendentTempest, DamageModifierMode.PvE)
            .WithBuilds(GW2Builds.August2022Balance, GW2Builds.SOTOReleaseAndBalance),
        new BuffOnActorDamageModifier(Mod_TranscendentTempest, TranscendentTempest, "Transcendent Tempest", "25% after overload", DamageSource.NoPets, 25.0, DamageType.StrikeAndCondition, DamageType.All, Source.Tempest, ByPresence, TraitImages.TranscendentTempest, DamageModifierMode.PvE)
            .WithBuilds(GW2Builds.SOTOReleaseAndBalance),
        // Tempestuous Aria
        new BuffOnActorDamageModifier(Mod_TempestuousAria, TempestuousAria, "Tempestuous Aria", "7% after giving aura", DamageSource.NoPets, 7.0, DamageType.StrikeAndCondition, DamageType.All, Source.Tempest, ByPresence, TraitImages.TempestuousAria, DamageModifierMode.sPvPWvW)
            .WithBuilds(GW2Builds.June2023BalanceAndSOTOBetaAndSilentSurfNM),
        new BuffOnActorDamageModifier(Mod_TempestuousAria, TempestuousAria, "Tempestuous Aria", "10% after giving aura", DamageSource.NoPets, 10.0, DamageType.StrikeAndCondition, DamageType.All, Source.Tempest, ByPresence, TraitImages.TempestuousAria, DamageModifierMode.PvE)
            .WithBuilds(GW2Builds.June2023BalanceAndSOTOBetaAndSilentSurfNM, GW2Builds.March2024BalanceAndCerusLegendary),
        new BuffOnActorDamageModifier(Mod_TempestuousAriaStrike, TempestuousAria, "Tempestuous Aria (Strike)", "10% after giving aura", DamageSource.NoPets, 10.0, DamageType.Strike, DamageType.All, Source.Tempest, ByPresence, TraitImages.TempestuousAria, DamageModifierMode.PvE)
            .WithBuilds(GW2Builds.March2024BalanceAndCerusLegendary),
        new BuffOnActorDamageModifier(Mod_TempestuousAriaCondition, TempestuousAria, "Tempestuous Aria (Condition)", "5% after giving aura", DamageSource.NoPets, 5.0, DamageType.Condition, DamageType.All, Source.Tempest, ByPresence, TraitImages.TempestuousAria, DamageModifierMode.PvE)
            .WithBuilds(GW2Builds.March2024BalanceAndCerusLegendary),
    ];

    internal static readonly IReadOnlyList<DamageModifierDescriptor> IncomingDamageModifiers =
    [
        // Hardy Conduit
        new BuffOnActorDamageModifier(Mod_HardyConduit, Protection, "Hardy Conduit", "20% extra protection effectiveness", DamageSource.Incoming, (0.604/0.67 - 1) * 100, DamageType.Strike, DamageType.All, Source.Tempest, ByPresence, TraitImages.HardyConduit, DamageModifierMode.All), // We only compute the added effectiveness
    ];


    internal static readonly IReadOnlyList<Buff> Buffs =
    [
        new Buff("Rebound", Rebound, Source.Tempest, BuffClassification.Defensive, SkillImages.Rebound),
        new Buff("Harmonious Conduit", HarmoniousConduit, Source.Tempest, BuffClassification.Other, TraitImages.HarmoniousConduit)
            .WithBuilds(GW2Builds.StartOfLife, GW2Builds.October2019Balance),
        new Buff("Transcendent Tempest", TranscendentTempest, Source.Tempest, BuffClassification.Other, TraitImages.TranscendentTempest)
            .WithBuilds(GW2Builds.October2019Balance),
        new Buff("Static Charge", StaticCharge, Source.Tempest, BuffClassification.Offensive, SkillImages.OverloadAir),
        new Buff("Heat Sync", HeatSync, Source.Tempest, BuffClassification.Support, SkillImages.HeatSync),
        new Buff("Tempestuous Aria", TempestuousAria, Source.Tempest, BuffStackType.Queue, 9, BuffClassification.Other, TraitImages.TempestuousAria)
            .WithBuilds(GW2Builds.June2023BalanceAndSOTOBetaAndSilentSurfNM),
    ];


    internal static void ComputeProfessionCombatReplayActors(PlayerActor player, ParsedEvtcLog log, CombatReplay replay)
    {
        Color color = Colors.Elementalist;

        // Overload Fire
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(player.AgentItem, EffectGUIDs.TempestOverloadFire2, out var overloadsFire))
        {
            if (log.CombatData.TryGetEffectEventsBySrcWithGUID(player.AgentItem, EffectGUIDs.TempestOverloadFire1, out var overloadsFireAux))
            {
                var skill = new SkillModeDescriptor(player, Spec.Tempest, OverloadFire);
                foreach (EffectEvent effect in overloadsFire)
                {
                    if (!overloadsFireAux.Any(x => Math.Abs(x.Time - effect.Time) < ServerDelayConstant))
                    {
                        continue;
                    }
                    (long, long) lifespan = effect.ComputeLifespan(log, 5000);
                    AddCircleSkillDecoration(replay, effect, color, skill, lifespan, 180, EffectImages.EffectOverloadFire);
                }
            }
        }

        // Overload Air
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(player.AgentItem, EffectGUIDs.TempestOverloadAir2, out var overloadsAir))
        {
            if (log.CombatData.TryGetEffectEventsBySrcWithGUID(player.AgentItem, EffectGUIDs.TempestOverloadAir1, out var overloadsAirAux))
            {
                var skill = new SkillModeDescriptor(player, Spec.Tempest, OverloadAir);
                foreach (EffectEvent effect in overloadsAir)
                {
                    if (!overloadsAirAux.Any(x => Math.Abs(x.Time - effect.Time) < ServerDelayConstant))
                    {
                        continue;
                    }
                    (long, long) lifespan = effect.ComputeLifespan(log, 4000);
                    AddCircleSkillDecoration(replay, effect, color, skill, lifespan, 360, EffectImages.EffectOverloadAir);
                }
            }
        }
    }
}

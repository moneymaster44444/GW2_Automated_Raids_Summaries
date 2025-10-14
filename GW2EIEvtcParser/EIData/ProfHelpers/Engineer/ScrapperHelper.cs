﻿using GW2EIEvtcParser.ParsedData;
using GW2EIEvtcParser.ParserHelpers;
using static GW2EIEvtcParser.DamageModifierIDs;
using static GW2EIEvtcParser.EIData.Buff;
using static GW2EIEvtcParser.EIData.DamageModifiersUtils;
using static GW2EIEvtcParser.EIData.ProfHelper;
using static GW2EIEvtcParser.EIData.SkillModeDescriptor;
using static GW2EIEvtcParser.ParserHelper;
using static GW2EIEvtcParser.SkillIDs;
using static GW2EIEvtcParser.SpeciesIDs;

namespace GW2EIEvtcParser.EIData;

internal static class ScrapperHelper
{
    internal static readonly List<InstantCastFinder> InstantCastFinder =
    [
        new EffectCastFinder(BulwarkGyro, EffectGUIDs.ScrapperBulwarkGyroTraited)
            .UsingSrcSpecChecker(Spec.Scrapper),
        new EffectCastFinder(BulwarkGyro, EffectGUIDs.ScrapperBulwarkGyro)
            .UsingSrcSpecChecker(Spec.Scrapper),
        new EffectCastFinder(PurgeGyro, EffectGUIDs.ScrapperPurgeGyroTraited)
            .UsingSrcSpecChecker(Spec.Scrapper),
        new EffectCastFinder(PurgeGyro, EffectGUIDs.ScrapperPurgeGyro)
            .UsingSrcSpecChecker(Spec.Scrapper),
        new EffectCastFinder(DefenseField, EffectGUIDs.ScrapperDefenseField)
            .UsingSrcSpecChecker(Spec.Scrapper),
        new EffectCastFinder(BypassCoating, EffectGUIDs.ScrapperBypassCoating)
            .UsingSrcSpecChecker(Spec.Scrapper),
    ];

    internal static readonly IReadOnlyList<DamageModifierDescriptor> OutgoingDamageModifiers =
    [
        // Object in Motion
        new BuffOnActorDamageModifier(Mod_ObjectInMotion, [Swiftness, Superspeed, Stability], "Object in Motion", "5% under swiftness/superspeed/stability, cumulative", DamageSource.NoPets, 5.0, DamageType.Strike, DamageType.All, Source.Scrapper, ByMultiPresence, TraitImages.ObjectInMotion, DamageModifierMode.All)
            .WithBuilds(GW2Builds.July2019Balance)
    ];

    internal static readonly IReadOnlyList<DamageModifierDescriptor> IncomingDamageModifiers =
    [
        // Adaptive Armor
        new DamageLogDamageModifier(Mod_AdaptiveArmor, "Adaptive Armor", "-20%", DamageSource.Incoming, -20.0, DamageType.Condition, DamageType.All, Source.Scrapper, TraitImages.AdaptiveArmor, (x, log) => x.ShieldDamage > 0, DamageModifierMode.All)
            .WithBuilds(GW2Builds.July2019Balance, GW2Builds.January2024Balance),
    ];

    internal static readonly IReadOnlyList<Buff> Buffs =
    [
        new Buff("Watchful Eye", WatchfulEye, Source.Scrapper, BuffClassification.Defensive, SkillImages.BulwarkGyro),
        new Buff("Watchful Eye PvP", WatchfulEyePvP, Source.Scrapper, BuffClassification.Defensive, SkillImages.BulwarkGyro),
    ];

    private static readonly HashSet<int> Minions =
    [
        (int)MinionID.BlastGyro,
        (int)MinionID.BulwarkGyro,
        (int)MinionID.FunctionGyro,
        (int)MinionID.MedicGyro,
        (int)MinionID.ShredderGyro,
        (int)MinionID.SneakGyro,
        (int)MinionID.PurgeGyro,
    ];

    internal static bool IsKnownMinionID(int id)
    {
        return Minions.Contains(id);
    }

    internal static void ComputeProfessionCombatReplayActors(PlayerActor player, ParsedEvtcLog log, CombatReplay replay)
    {
        Color color = Colors.Engineer;

        // Function Gyro
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(player.AgentItem, EffectGUIDs.ScrapperFunctionGyro, out var functionGyros))
        {
            var skill = new SkillModeDescriptor(player, Spec.Scrapper, FunctionGyro);
            foreach (EffectEvent effect in functionGyros)
            {
                (long, long) lifespan = effect.ComputeLifespan(log, 5000);
                AddCircleSkillDecoration(replay, effect, color, skill, lifespan, 240, EffectImages.EffectFunctionGyro);
            }
        }
        // Defense Field
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(player.AgentItem, EffectGUIDs.ScrapperDefenseField, out var defenseFields))
        {
            var skill = new SkillModeDescriptor(player, Spec.Scrapper, DefenseField, SkillModeCategory.ProjectileManagement | SkillModeCategory.ImportantBuffs);
            foreach (EffectEvent effect in defenseFields)
            {
                (long, long) lifespan = effect.ComputeLifespan(log, 5000);
                AddCircleSkillDecoration(replay, effect, color, skill, lifespan, 240, EffectImages.EffectDefenseField);
            }
        }
    }
}

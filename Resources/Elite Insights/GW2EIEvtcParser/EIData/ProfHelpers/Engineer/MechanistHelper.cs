﻿using GW2EIEvtcParser.ParsedData;
using GW2EIEvtcParser.ParserHelpers;
using static GW2EIEvtcParser.DamageModifierIDs;
using static GW2EIEvtcParser.EIData.Buff;
using static GW2EIEvtcParser.EIData.DamageModifiersUtils;
using static GW2EIEvtcParser.ParserHelper;
using static GW2EIEvtcParser.SkillIDs;
using static GW2EIEvtcParser.SpeciesIDs;

namespace GW2EIEvtcParser.EIData;

internal static class MechanistHelper
{

    internal static readonly List<InstantCastFinder> InstantCastFinder =
    [
        new EffectCastFinder(ShiftSignetSkill, EffectGUIDs.MechanistShiftSignet1)
            .UsingSrcSpecChecker(Spec.Mechanist),
        new MinionCastCastFinder(OverclockSignetSkill, JadeBusterCannon)
            .UsingDisableWithEffectData()
            .UsingNotAccurate(),
        new EffectCastFinderByDst(OverclockSignetSkill, EffectGUIDs.MechanistOverclockSignet)
            .UsingDstSpecChecker(Spec.Mechanist),

        // Mech
        new DamageCastFinder(CrashDown, CrashDown)
            .UsingDisableWithEffectData(),
        new MinionSpawnCastFinder(CrashDown, (int)MinionID.JadeMech)
            .UsingChecker((spawn, combatData, agentData, skillData) =>
            {
                // TODO: what if shift signet ports the minion away from effect before arc polls the position? so far unable to produce
                var pos = combatData.GetMovementData(spawn.Src).OfType<PositionEvent>().FirstOrDefault(evt => evt.Time + ServerDelayConstant >= spawn.Time);
                if (pos != null && combatData.TryGetEffectEventsByGUID(EffectGUIDs.MechanistCrashDownImpact, out var effects))
                {
                    return CombatData.FindRelatedEvents(effects, spawn.Time + 800).Any(effect => (pos.GetPointXY() - effect.Position.XY()).Length() < 10.0f);
                }
                return false;
            }) // intersect first position after spawn with delayed effect
            .UsingNotAccurate(),
        // Mech F1
        new MinionCastCastFinder(RoilingSmash, RoilingSmash),
        new MinionCastCastFinder(ExplosiveKnuckle, ExplosiveKnuckle),
        new MinionCastCastFinder(SparkRevolver, SparkRevolver),
        // Mech F2
        new BuffGainCastFinder(DischargeArray, DischargeArrayBuff)
            .WithMinions(),
        new EffectCastFinderByDst(CrisisZone, EffectGUIDs.MechanistCrisisZone)
            .WithMinions()
            .UsingSecondaryEffectChecker(EffectGUIDs.MechanistMechEyeGlow)
            .UsingChecker((effect, combatData, agentData, skillData) => effect.Dst.IsSpecies(MinionID.JadeMech)),
        new MinionCastCastFinder(CoreReactorShot, CoreReactorShot),
        // Mech F3
        new MinionCastCastFinder(JadeMortar, JadeMortar),
        new MinionCastCastFinder(BarrierBurst, BarrierBurst),
        new MinionCastCastFinder(SkyCircus, SkyCircus),
        // Mech Passives
        new MissileCastFinder(RocketPunchMech, RocketPunchMech)
            .WithMinions(),
        new BuffGainCastFinder(ExigencyProtocol, ExigencyProtocol)
            .WithMinions()
            .UsingChecker((buffApply, combatData, agentData, skillData) => buffApply.To.IsSpecies(MinionID.JadeMech)),
    ];

    private static bool WithMechChecker(DamageEvent x, ParsedEvtcLog log)
    {
        return x.From.Is(x.CreditedFrom) || x.From.IsSpecies(MinionID.JadeMech);
    }

    internal static readonly IReadOnlyList<DamageModifierDescriptor> OutgoingDamageModifiers =
    [
        // Force Signet
        new BuffOnActorDamageModifier(Mod_ForceSignet, ForceSignet, "Force Signet", "10%, including Mech", DamageSource.All, 10.0, DamageType.Strike, DamageType.All, Source.Mechanist, ByPresence, SkillImages.ForceSignet, DamageModifierMode.All)
            .WithBuilds(GW2Builds.EODBeta4, GW2Builds.April2025Balance)
            .UsingChecker(WithMechChecker),
        new BuffOnActorDamageModifier(Mod_ForceSignet, ForceSignet, "Force Signet", "10%, including Mech", DamageSource.All, 10.0, DamageType.Strike, DamageType.All, Source.Mechanist, ByPresence, SkillImages.ForceSignet, DamageModifierMode.sPvPWvW)
            .WithBuilds(GW2Builds.April2025Balance)
            .UsingChecker(WithMechChecker),
        new BuffOnActorDamageModifier(Mod_ForceSignet, ForceSignet, "Force Signet", "15%, including Mech", DamageSource.All, 15.0, DamageType.Strike, DamageType.All, Source.Mechanist, ByPresence, SkillImages.ForceSignet, DamageModifierMode.PvE)
            .WithBuilds(GW2Builds.April2025Balance)
            .UsingChecker(WithMechChecker),
        // Superconducting Signet
        new BuffOnActorDamageModifier(Mod_SuperconductingSignet, SuperconductingSignet, "Superconducting Signet", "10%, including Mech", DamageSource.All, 10.0, DamageType.Condition, DamageType.All, Source.Mechanist, ByPresence, SkillImages.SuperconductingSignet, DamageModifierMode.All)
            .WithBuilds(GW2Builds.EODBeta4)
            .UsingChecker(WithMechChecker),
    ];

    internal static readonly IReadOnlyList<DamageModifierDescriptor> IncomingDamageModifiers =
    [
        // Barrier Signet
        new BuffOnActorDamageModifier(Mod_BarrierSignet, BarrierSignet, "Barrier Signet", "-10%", DamageSource.Incoming, -10, DamageType.StrikeAndCondition, DamageType.All, Source.Mechanist, ByPresence, SkillImages.BarrierSignet, DamageModifierMode.All),
    ];


    internal static readonly IReadOnlyList<Buff> Buffs =
    [
        new Buff("Rectifier Signet", RectifierSignet, Source.Mechanist, BuffClassification.Other, SkillImages.RectifierSignet),
        new Buff("Barrier Signet", BarrierSignet, Source.Mechanist, BuffClassification.Other, SkillImages.BarrierSignet),
        new Buff("Force Signet", ForceSignet, Source.Mechanist, BuffClassification.Other, SkillImages.ForceSignet),
        new Buff("Shift Signet", ShiftSignetBuff, Source.Mechanist, BuffClassification.Other, SkillImages.ShiftSignet),
        new Buff("Superconducting Signet", SuperconductingSignet, Source.Mechanist, BuffClassification.Other, SkillImages.SuperconductingSignet),
        new Buff("Overclock Signet", OverclockSignetBuff, Source.Mechanist, BuffClassification.Other, SkillImages.OverclockSignet),
        new Buff("Mechanical Genius", MechanicalGenius, Source.Mechanist, BuffClassification.Other, TraitImages.MechanicalGenius),
        new Buff("Mechanical Genius (Remain)", MechanicalGeniusRemain, Source.Mechanist, BuffClassification.Other, TraitImages.MechanicalGenius),
        new Buff("Exigency Protocols", ExigencyProtocol, Source.PetSpecific, BuffClassification.Other, TraitImages.ExigencyProtocol),
        // J-Drive Signet Buffs - The other signets do not gain a different buff ID.
        new Buff("Barrier Signet (J-Drive)", BarrierSignetJDrive, Source.Mechanist, BuffClassification.Other, SkillImages.BarrierSignet),
        new Buff("Overclock Signet (J-Drive)", OverclockSignetJDrive, Source.Mechanist, BuffClassification.Other, SkillImages.OverclockSignet),
    ];

    private static readonly HashSet<int> Minions =
    [
        (int)MinionID.JadeMech,
    ];
    
    internal static bool IsKnownMinionID(int id)
    {
        return Minions.Contains(id);
    }

}

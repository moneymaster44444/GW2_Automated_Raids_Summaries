﻿using GW2EIEvtcParser.ParsedData;
using GW2EIEvtcParser.ParserHelpers;
using static GW2EIEvtcParser.ArcDPSEnums;
using static GW2EIEvtcParser.DamageModifierIDs;
using static GW2EIEvtcParser.EIData.Buff;
using static GW2EIEvtcParser.EIData.DamageModifiersUtils;
using static GW2EIEvtcParser.EIData.ProfHelper;
using static GW2EIEvtcParser.EIData.SkillModeDescriptor;
using static GW2EIEvtcParser.ParserHelper;
using static GW2EIEvtcParser.SkillIDs;
using static GW2EIEvtcParser.SpeciesIDs;

namespace GW2EIEvtcParser.EIData;

internal static class ThiefHelper
{
    internal static readonly List<InstantCastFinder> InstantCastFinder =
    [
        new BuffGainCastFinder(Shadowstep, Infiltration),
        new BuffLossCastFinder(ShadowReturn, Infiltration)
            .UsingChecker((evt, combatData, agentData, skillData) => evt.RemovedDuration > ServerDelayConstant),
        new DamageCastFinder(Mug, Mug),
        new DamageCastFinder(InfiltratorsStrike, InfiltratorsStrike),
        new BuffGainCastFinder(AssassinsSignet, AssassinsSignetActive),
        new BuffGiveCastFinder(DevourerVenomSkill, DevourerVenomBuff),
        new BuffGiveCastFinder(IceDrakeVenomSkill, IceDrakeVenomBuff),
        new BuffGiveCastFinder(SkaleVenomSkill, SkaleVenomBuff),
        new BuffGiveCastFinder(SoulStoneVenomSkill,SoulStoneVenomBuff),
        new BuffGiveCastFinder(SpiderVenomSkill,SpiderVenomBuff).
            UsingChecker((evt, combatData, agentData, skillData) => !evt.To.Is(evt.By) || Math.Abs(evt.AppliedDuration - 24000) < ServerDelayConstant)
            .UsingNotAccurate(), // same id as leeching venom trait?
        new EffectCastFinder(Pitfall, EffectGUIDs.ThiefPitfallAoE)
            .UsingSrcBaseSpecChecker(Spec.Thief),
        new EffectCastFinder(ThousandNeedles, EffectGUIDs.ThiefThousandNeedlesAoECollision)
            .UsingSecondaryEffectChecker(EffectGUIDs.ThiefThousandNeedlesAoE1, 280)
            .UsingSecondaryEffectChecker(EffectGUIDs.ThiefThousandNeedlesAoE2, 280)
            .UsingSrcBaseSpecChecker(Spec.Thief),
        new EffectCastFinder(SealArea, EffectGUIDs.ThiefSealAreaAoE)
            .UsingSrcBaseSpecChecker(Spec.Thief),
        new BuffGainCastFinder(ShadowPortal, ShadowPortalOpenedBuff),
        new EffectCastFinderByDst(InfiltratorsSignetSkill, EffectGUIDs.ThiefInfiltratorsSignet1)
            .UsingDstBaseSpecChecker(Spec.Thief)
            .UsingSecondaryEffectChecker(EffectGUIDs.ThiefInfiltratorsSignet2),
        new EffectCastFinderByDst(SignetOfAgilitySkill, EffectGUIDs.ThiefSignetOfAgility)
            .UsingDstBaseSpecChecker(Spec.Thief),
        new EffectCastFinderByDst(SignetOfShadowsSkill, EffectGUIDs.ThiefSignetOfShadows)
            .UsingDstBaseSpecChecker(Spec.Thief),
    ];

    internal static readonly IReadOnlyList<DamageModifierDescriptor> OutgoingDamageModifiers =
    [
        // Deadly Arts
        // - Exposed Weakness
        new BuffOnFoeDamageModifier(Mod_ExposedWeakness, NumberOfConditions, "Exposed Weakness", "10% if condition on target", DamageSource.NoPets, 10.0, DamageType.Strike, DamageType.All, Source.Thief, ByPresence, TraitImages.ExposedWeakness, DamageModifierMode.PvE)
            .WithBuilds(GW2Builds.StartOfLife, GW2Builds.July2018Balance),
        new BuffOnFoeDamageModifier(Mod_ExposedWeakness, NumberOfConditions, "Exposed Weakness", "2% per condition on target", DamageSource.NoPets, 2.0, DamageType.Strike, DamageType.All, Source.Thief, ByStack, TraitImages.ExposedWeakness, DamageModifierMode.All)
            .WithBuilds(GW2Builds.July2018Balance, GW2Builds.June2025Balance),
        new BuffOnFoeDamageModifier(Mod_ExposedWeakness, NumberOfConditions, "Exposed Weakness", "2% per condition on target", DamageSource.NoPets, 2.0, DamageType.Strike, DamageType.All, Source.Thief, ByStack, TraitImages.ExposedWeakness, DamageModifierMode.PvE)
            .WithBuilds(GW2Builds.June2025Balance),
        new BuffOnFoeDamageModifier(Mod_ExposedWeakness, NumberOfConditions, "Exposed Weakness", "3% per condition on target", DamageSource.NoPets, 3.0, DamageType.Strike, DamageType.All, Source.Thief, ByStack, TraitImages.ExposedWeakness, DamageModifierMode.sPvPWvW)
            .WithBuilds(GW2Builds.June2025Balance),
        // - Executioner
        new DamageLogDamageModifier(Mod_Executioner, "Executioner", "20% if target <50% HP", DamageSource.NoPets, 20.0, DamageType.Strike, DamageType.All, Source.Thief, TraitImages.Executioner, (x, log) => x.AgainstUnderFifty, DamageModifierMode.All),
        
        // Critical Strikes
        // - Twin Fangs
        new DamageLogDamageModifier(Mod_TwinFangs, "Twin Fangs","7% if hp >=90%", DamageSource.NoPets, 7.0, DamageType.Strike, DamageType.All, Source.Thief, TraitImages.FerociousStrikes, (x, log) => x.IsOverNinety && x.HasCrit, DamageModifierMode.All)
            .WithBuilds(GW2Builds.StartOfLife, GW2Builds.March2024BalanceAndCerusLegendary),
        new DamageLogDamageModifier(Mod_TwinFangs, "Twin Fangs","7% if hp >=50%", DamageSource.NoPets, 7.0, DamageType.Strike, DamageType.All, Source.Thief, TraitImages.FerociousStrikes, (x, log) => x.From.GetCurrentHealthPercent(log, x.Time) >= 50.0 && x.HasCrit, DamageModifierMode.All)
            .WithBuilds(GW2Builds.March2024BalanceAndCerusLegendary)
            .UsingApproximate(),
        // - Ferocious Strikes
        new DamageLogDamageModifier(Mod_FerociousStrikes, "Ferocious Strikes", "10% on critical strikes if target >50%", DamageSource.NoPets, 10.0, DamageType.Strike, DamageType.All, Source.Thief, TraitImages.FerociousStrikes, (x, log) => !x.AgainstUnderFifty && x.HasCrit, DamageModifierMode.All),
        
        // Trickery
        // - Lead Attacks
        new BuffOnActorDamageModifier(Mod_LeadAttacks, LeadAttacks, "Lead Attacks", "1% (10s) per initiative spent", DamageSource.NoPets, 1.0, DamageType.StrikeAndCondition, DamageType.All, Source.Thief, ByStack, TraitImages.LeadAttacks, DamageModifierMode.All), 
        // It's not always possible to detect the presence of pistol and the trait is additive with itself. Staff master is worse as we can't detect endurance at all       
        
        // Acrobatics
        // - Fluid Strikes
        new BuffOnActorDamageModifier(Mod_FluidStrikes, FluidStrikes, "Fluid Strikes", "10%", DamageSource.NoPets, 10.0, DamageType.Strike, DamageType.All, Source.Thief, ByPresence, TraitImages.FluidStrikes, DamageModifierMode.All)
            .WithBuilds(GW2Builds.July2023BalanceAndSilentSurfCM),
        
        // Spear       
        new BuffOnActorDamageModifier(Mod_DistractingThrow, DistractingThrowBuff, "Distracting Throw", "10%", DamageSource.NoPets, 10, DamageType.StrikeAndCondition, DamageType.All, Source.Thief, ByPresence, SkillImages.DistractingThrow, DamageModifierMode.PvE),
        new BuffOnActorDamageModifier(Mod_DistractingThrow, DistractingThrowBuff, "Distracting Throw", "5%", DamageSource.NoPets, 5, DamageType.StrikeAndCondition, DamageType.All, Source.Thief, ByPresence, SkillImages.DistractingThrow, DamageModifierMode.sPvPWvW),
    ];

    internal static readonly IReadOnlyList<DamageModifierDescriptor> IncomingDamageModifiers =
    [
        // Marauder's Resilience
        new DamageLogDamageModifier(Mod_MaraudersResilience, "Marauder's Resilience", "-10% from foes within 360 range", DamageSource.Incoming, -10.0, DamageType.Strike, DamageType.All, Source.Thief, TraitImages.MaraudersResilience, (x, log) => !TargetWithinRangeChecker(x, log, 360, false), DamageModifierMode.All)
            .UsingApproximate()
            .WithBuilds(GW2Builds.April2019Balance)
    ];


    internal static readonly IReadOnlyList<Buff> Buffs =
    [
        // Skills
        new Buff("Shadow Portal (Prepared)", ShadowPortalPreparedBuff, Source.Thief, BuffClassification.Other, SkillImages.PrepareShadowPortal),
        new Buff("Shadow Portal (Open)", ShadowPortalOpenedBuff, Source.Thief, BuffStackType.Stacking, 25, BuffClassification.Other, SkillImages.ShadowPortal),
        new Buff("Kneeling", Kneeling, Source.Thief, BuffClassification.Other, SkillImages.Kneel)
            .WithBuilds(GW2Builds.June2023BalanceAndSOTOBetaAndSilentSurfNM),
        // Signets
        new Buff("Signet of Malice", SignetOfMalice, Source.Thief, BuffClassification.Other, SkillImages.SignetOfMalice),
        new Buff("Assassin's Signet (Passive)", AssassinsSignetPassive, Source.Thief, BuffClassification.Other, SkillImages.AssassinsSignet),
        new Buff("Assassin's Signet (Active)", AssassinsSignetActive, Source.Thief, BuffClassification.Other, SkillImages.AssassinsSignet),
        new Buff("Infiltrator's Signet", InfiltratorsSignetBuff, Source.Thief, BuffClassification.Other, SkillImages.InfiltratorsSignet),
        new Buff("Signet of Agility", SignetOfAgilityBuff, Source.Thief, BuffClassification.Other, SkillImages.SignetOfAgility),
        new Buff("Signet of Shadows", SignetOfShadowsBuff, Source.Thief, BuffClassification.Other, SkillImages.SignetOfShadows),
        // Venoms // src is always the user, makes generation data useless
        new Buff("Skelk Venom", SkelkVenom, Source.Thief, BuffStackType.StackingConditionalLoss, 5, BuffClassification.Defensive, SkillImages.SkelkVenom),
        new Buff("Ice Drake Venom", IceDrakeVenomBuff, Source.Thief, BuffStackType.StackingConditionalLoss, 4, BuffClassification.Support, SkillImages.IceDrakeVenom),
        new Buff("Devourer Venom", DevourerVenomBuff, Source.Thief, BuffStackType.StackingConditionalLoss, 2, BuffClassification.Support, SkillImages.DevourerVenom),
        new Buff("Skale Venom", SkaleVenomBuff, Source.Thief, BuffStackType.StackingConditionalLoss, 4, BuffClassification.Offensive, SkillImages.SkaleVenom),
        new Buff("Spider Venom", SpiderVenomBuff, Source.Thief, BuffStackType.StackingConditionalLoss, 6, BuffClassification.Offensive, SkillImages.SpiderVenom),
        new Buff("Soul Stone Venom", SoulStoneVenomBuff, Source.Thief, BuffStackType.Stacking, 25, BuffClassification.Offensive, SkillImages.SoulStoneVenom),
        new Buff("Basilisk Venom", BasiliskVenomBuff, Source.Thief, BuffStackType.StackingConditionalLoss, 2, BuffClassification.Support, SkillImages.BasiliskVenom),
        new Buff("Petrified 1", Petrified1, Source.Thief, BuffClassification.Other, BuffImages.Stun),
        new Buff("Petrified 2", Petrified2, Source.Thief, BuffClassification.Other, BuffImages.Stun),
        new Buff("Infiltration", Infiltration, Source.Thief, BuffClassification.Other, SkillImages.Shadowstep),
        // Transforms
        new Buff("Dagger Storm", DaggerStorm, Source.Thief, BuffClassification.Other, SkillImages.DaggerStorm),
        // Traits
        new Buff("Hidden Killer", HiddenKiller, Source.Thief, BuffClassification.Other, TraitImages.Hiddenkiller),
        new Buff("Lead Attacks", LeadAttacks, Source.Thief, BuffStackType.Stacking, 15, BuffClassification.Other, TraitImages.LeadAttacks),
        new Buff("Instant Reflexes", InstantReflexes, Source.Thief, BuffClassification.Other, TraitImages.InstantReflexes),
        new Buff("Fluid Strikes", FluidStrikes, Source.Thief, BuffClassification.Other, TraitImages.FluidStrikes)
            .WithBuilds(GW2Builds.July2023BalanceAndSilentSurfCM),
        // Spear
        new Buff("Distracting Throw", DistractingThrowBuff, Source.Thief, BuffStackType.Queue, 9, BuffClassification.Other, SkillImages.DistractingThrow),
        new Buff("Shadow Veil", ShadowVeilBuff, Source.Thief, BuffClassification.Other, SkillImages.ShadowVeil),
        new Buff("Shadow Veil (Stacks)", ShadowVeilBuffStacks, Source.Thief, BuffStackType.StackingConditionalLoss, 25, BuffClassification.Other, SkillImages.ShadowVeil),
    ];

    private static readonly HashSet<int> Minions =
    [
        (int)MinionID.ThiefDaggerHuman,
        (int)MinionID.ThiefPistolHuman,
        (int)MinionID.ThiefUnknown1,
        (int)MinionID.ThiefUnknown2,
        (int)MinionID.ThiefDaggerAsura,
        (int)MinionID.ThiefPistolAsura,
        (int)MinionID.ThiefPistolCharr,
        (int)MinionID.ThiefDaggerCharr,
        (int)MinionID.ThiefPistolNorn,
        (int)MinionID.ThiefDaggerNorn,
        (int)MinionID.ThiefPistolSylvari,
        (int)MinionID.ThiefDaggerSylvari,
        (int)MinionID.ThiefSwordAsura1,
        (int)MinionID.ThiefSwordNorn1,
        (int)MinionID.ThiefSwordAsura2,
        (int)MinionID.ThiefSwordCharr1,
        (int)MinionID.ThiefSwordSylvari1,
        (int)MinionID.ThiefSwordCharr2,
        (int)MinionID.ThiefSwordNorn2,
        (int)MinionID.ThiefSwordHuman1,
        (int)MinionID.ThiefSwordHuman2,
        (int)MinionID.ThiefSwordSylvari2,
    ];

    internal static bool IsKnownMinionID(int id)
    {
        return Minions.Contains(id);
    }

    internal static void ComputeProfessionCombatReplayActors(PlayerActor player, ParsedEvtcLog log, CombatReplay replay)
    {
        Color color = Colors.Thief;

        // Shadow Portal locations
        var entranceDecorations = new List<AttachedDecoration>();
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(player.AgentItem, EffectGUIDs.ThiefShadowPortalActiveEntrance, out var shadowPortalActiveEntrance))
        {
            var skill = new SkillModeDescriptor(player, Spec.Thief, PrepareShadowPortal, SkillModeCategory.Portal);
            foreach (EffectEvent enter in shadowPortalActiveEntrance)
            {
                (long, long) lifespan = enter.ComputeLifespan(log, 8000, player.AgentItem, ShadowPortalOpenedBuff);
                var connector = new PositionConnector(enter.Position);
                replay.Decorations.Add(new CircleDecoration(90, lifespan, color, 0.5, connector).UsingSkillMode(skill));
                AttachedDecoration icon = new IconDecoration(EffectImages.PortalShadowPortalPrepare, CombatReplaySkillDefaultSizeInPixel, CombatReplaySkillDefaultSizeInWorld, 0.7f, lifespan, connector).UsingSkillMode(skill);
                replay.Decorations.Add(icon);
                entranceDecorations.Add(icon);
            }
        }
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(player.AgentItem, EffectGUIDs.ThiefShadowPortalActiveExit, out var shadowPortalActiveExit))
        {
            foreach (EffectEvent exit in shadowPortalActiveExit)
            {
                var skill = new SkillModeDescriptor(player, Spec.Thief, ShadowPortal, SkillModeCategory.Portal);
                (long, long) lifespan = exit.ComputeLifespan(log, 8000, player.AgentItem, ShadowPortalOpenedBuff);
                var connector = new PositionConnector(exit.Position);
                replay.Decorations.Add(new CircleDecoration(90, lifespan, color, 0.5, connector).UsingSkillMode(skill));
                AttachedDecoration icon = new IconDecoration(EffectImages.PortalShadowPortalOpen, CombatReplaySkillDefaultSizeInPixel, CombatReplaySkillDefaultSizeInWorld, 0.7f, lifespan, connector).UsingSkillMode(skill);
                AttachedDecoration? entranceDecoration = entranceDecorations.FirstOrDefault(x => Math.Abs(x.Lifespan.start - exit.Time) < ServerDelayConstant);
                if (entranceDecoration != null)
                {
                    replay.Decorations.Add(entranceDecoration.LineTo(icon, color, 0.5).UsingSkillMode(skill));
                }
                replay.Decorations.Add(icon);
            }
        }

        // Seal Area
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(player.AgentItem, EffectGUIDs.ThiefSealAreaAoE, out var sealAreaAoEs))
        {
            var skill = new SkillModeDescriptor(player, Spec.Thief, SealArea, SkillModeCategory.ProjectileManagement | SkillModeCategory.CC);
            foreach (EffectEvent effect in sealAreaAoEs)
            {
                (long, long) lifespan = effect.ComputeLifespan(log, 8000);
                AddCircleSkillDecoration(replay, effect, color, skill, lifespan, 240, EffectImages.EffectSealArea);
            }
        }
        // Shadow Refuge
        if (log.CombatData.TryGetEffectEventsBySrcWithGUID(player.AgentItem, EffectGUIDs.ThiefShadowRefuge, out var shadowRefuges))
        {
            var skill = new SkillModeDescriptor(player, Spec.Thief, ShadowRefuge, SkillModeCategory.ImportantBuffs | SkillModeCategory.Heal);
            foreach (EffectEvent effect in shadowRefuges)
            {
                (long, long) lifespan = effect.ComputeLifespan(log, 4000);
                AddCircleSkillDecoration(replay, effect, color, skill, lifespan, 240, EffectImages.EffectShadowRefuge);
            }
        }
    }
}

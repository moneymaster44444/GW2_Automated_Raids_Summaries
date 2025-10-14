﻿namespace GW2EIJSON;

/// <summary>
/// Class regrouping player related healing statistics
/// </summary>
public class EXTJsonPlayerHealingStats
{
    /// <summary>
    /// Array of Total Allied Outgoing Healing stats \n
    /// Length == # of players and the length of each sub array is equal to # of phases
    /// </summary>
    /// <seealso cref="EXTJsonHealingStatistics.EXTJsonOutgoingHealingStatistics"/>
    public IReadOnlyList<IReadOnlyList<EXTJsonHealingStatistics.EXTJsonOutgoingHealingStatistics>>? OutgoingHealingAllies;
    /// <summary>
    /// Array of Total Outgoing Healing stats \n
    /// Length == # of phases
    /// </summary>
    /// <seealso cref="EXTJsonHealingStatistics.EXTJsonOutgoingHealingStatistics"/>
    public IReadOnlyList<EXTJsonHealingStatistics.EXTJsonOutgoingHealingStatistics>? OutgoingHealing;
    /// <summary>
    /// Array of Total Incoming Healing stats \n
    /// Length == # of phases
    /// </summary>
    /// <seealso cref="EXTJsonHealingStatistics.EXTJsonIncomingHealingStatistics"/>
    public IReadOnlyList<EXTJsonHealingStatistics.EXTJsonIncomingHealingStatistics>? IncomingHealing;

    /// <summary>
    /// Array of int representing 1S outgoing allied healing points \n
    /// Length == # of players and the length of each sub array is equal to # of phases
    /// </summary>
    /// <remarks>
    /// If the duration of the phase in seconds is non integer, the last point of this array will correspond to the last point  \n
    /// ex: duration === 15250ms, the array will have 17 elements [0, 1000,...,15000,15250]
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>>? AlliedHealing1S;
    /// <summary>
    /// Array of int representing 1S outgoing allied healing power based healing points \n
    /// Length == # of players and the length of each sub array is equal to # of phases
    /// </summary>
    /// <remarks>
    /// If the duration of the phase in seconds is non integer, the last point of this array will correspond to the last point  \n
    /// ex: duration === 15250ms, the array will have 17 elements [0, 1000,...,15000,15250]
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>>? AlliedHealingPowerHealing1S;
    /// <summary>
    /// Array of int representing 1S outgoing allied conversion based healing points \n
    /// Length == # of players and the length of each sub array is equal to # of phases
    /// </summary>
    /// <remarks>
    /// If the duration of the phase in seconds is non integer, the last point of this array will correspond to the last point  \n
    /// ex: duration === 15250ms, the array will have 17 elements [0, 1000,...,15000,15250]
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>>? AlliedConversionHealingHealing1S;

    /// <summary>
    /// Array of int representing 1S outgoing allied hybrid healing points \n
    /// Length == # of players and the length of each sub array is equal to # of phases
    /// </summary>
    /// <remarks>
    /// If the duration of the phase in seconds is non integer, the last point of this array will correspond to the last point  \n
    /// ex: duration === 15250ms, the array will have 17 elements [0, 1000,...,15000,15250]
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>>? AlliedHybridHealing1S;

    /// <summary>
    /// Array of int representing 1S outgoing healing points \n
    /// Length == # of phases
    /// </summary>
    /// <remarks>
    /// If the duration of the phase in seconds is non integer, the last point of this array will correspond to the last point  \n
    /// ex: duration === 15250ms, the array will have 17 elements [0, 1000,...,15000,15250]
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<int>>? Healing1S;
    /// <summary>
    /// Array of int representing 1S outgoing healing power based healing points \n
    /// Length == # of phases
    /// </summary>
    /// <remarks>
    /// If the duration of the phase in seconds is non integer, the last point of this array will correspond to the last point  \n
    /// ex: duration === 15250ms, the array will have 17 elements [0, 1000,...,15000,15250]
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<int>>? HealingPowerHealing1S;
    /// <summary>
    /// Array of int representing 1S outgoing conversion based healing points \n
    /// Length == # of phases
    /// </summary>
    /// <remarks>
    /// If the duration of the phase in seconds is non integer, the last point of this array will correspond to the last point  \n
    /// ex: duration === 15250ms, the array will have 17 elements [0, 1000,...,15000,15250]
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<int>>? ConversionHealingHealing1S;
    /// <summary>
    /// Array of int representing 1S outgoing hybrid healing points \n
    /// Length == # of phases
    /// </summary>
    /// <remarks>
    /// If the duration of the phase in seconds is non integer, the last point of this array will correspond to the last point  \n
    /// ex: duration === 15250ms, the array will have 17 elements [0, 1000,...,15000,15250]
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<int>>? HybridHealing1S;

    /// <summary>
    /// Array of int representing 1S incoming healing points \n
    /// Length == # of phases
    /// </summary>
    /// <remarks>
    /// If the duration of the phase in seconds is non integer, the last point of this array will correspond to the last point  \n
    /// ex: duration === 15250ms, the array will have 17 elements [0, 1000,...,15000,15250]
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<int>>? HealingReceived1S;
    /// <summary>
    /// Array of int representing 1S incoming healing power based healing points \n
    /// Length == # of phases
    /// </summary>
    /// <remarks>
    /// If the duration of the phase in seconds is non integer, the last point of this array will correspond to the last point  \n
    /// ex: duration === 15250ms, the array will have 17 elements [0, 1000,...,15000,15250]
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<int>>? HealingPowerHealingReceived1S;
    /// <summary>
    /// Array of int representing 1S incoming conversion based healing points \n
    /// Length == # of phases
    /// </summary>
    /// <remarks>
    /// If the duration of the phase in seconds is non integer, the last point of this array will correspond to the last point  \n
    /// ex: duration === 15250ms, the array will have 17 elements [0, 1000,...,15000,15250]
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<int>>? ConversionHealingHealingReceived1S;
    /// <summary>
    /// Array of int representing 1S incoming hybrid healing points \n
    /// Length == # of phases
    /// </summary>
    /// <remarks>
    /// If the duration of the phase in seconds is non integer, the last point of this array will correspond to the last point  \n
    /// ex: duration === 15250ms, the array will have 17 elements [0, 1000,...,15000,15250]
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<int>>? HybridHealingReceived1S;

    /// <summary>
    /// Total Outgoing Allied Healing distribution array \n
    /// Length == # of players and the length of each sub array is equal to # of phases
    /// </summary>
    /// <seealso cref="EXTJsonHealingDist"/>
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<EXTJsonHealingDist>>>? AlliedHealingDist;
    /// <summary>
    /// Total Outgoing Healing distribution array \n
    /// Length == # of phases
    /// </summary>
    /// <seealso cref="EXTJsonHealingDist"/>
    public IReadOnlyList<IReadOnlyList<EXTJsonHealingDist>>? TotalHealingDist;
    /// <summary>
    /// Total Incoming Healing distribution array \n
    /// Length == # of phases
    /// </summary>
    /// <seealso cref="EXTJsonHealingDist"/>
    public IReadOnlyList<IReadOnlyList<EXTJsonHealingDist>>? TotalIncomingHealingDist;
}

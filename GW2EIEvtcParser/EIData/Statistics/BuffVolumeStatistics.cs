﻿using GW2EIEvtcParser.ParsedData;
using static GW2EIEvtcParser.EIData.Buff;

namespace GW2EIEvtcParser.EIData;


public class BuffVolumeStatistics
{
    public double Incoming { get; internal set; }
    public double IncomingByExtension { get; internal set; }
    public double IncomingByUnknownExtension { get; internal set; }
    public double Outgoing { get; internal set; }
    public double OutgoingByExtension { get; internal set; }

    internal static (Dictionary<long, BuffVolumeStatistics> Volumes, Dictionary<long, BuffVolumeStatistics> ActiveVolumes) GetBuffVolumesForPlayers(IEnumerable<Player> playerList, ParsedEvtcLog log, SingleActor srcActor, long start, long end)
    {

        long phaseDuration = end - start;

        var buffsToTrack = new HashSet<Buff>();
        var playerCount = 0;
        foreach (Player p in playerList)
        {
            buffsToTrack.UnionWith(p.GetTrackedBuffs(log));
            playerCount++;
        }

        //TODO(Rennorb) @perf
        var buffs = new Dictionary<long, BuffVolumeStatistics>();
        var activeBuffs = new Dictionary<long, BuffVolumeStatistics>();

        foreach (Buff buff in buffsToTrack)
        {
            double totalOutgoing = 0;
            double totalOutgoingByExtension = 0;
            //
            double totalActiveOutgoing = 0;
            double totalActiveOutgoingByExtension = 0;
            int activePlayerCount = 0;
            foreach (Player p in playerList)
            {
                long playerActiveDuration = p.GetActiveDuration(log, start, end);
                if (playerActiveDuration > 0)
                {
                    activePlayerCount++;
                }
                foreach (BuffEvent abae in p.GetBuffApplyEventsOnByID(log, start, end, buff.ID, srcActor))
                {
                    if (abae is BuffApplyEvent bae)
                    {
                        // We ignore infinite duration buffs
                        /*if (bae.AppliedDuration >= int.MaxValue)
                        {
                            continue;
                        }*/
                        totalOutgoing += bae.AppliedDuration;
                        if (playerActiveDuration > 0)
                        {
                            totalActiveOutgoing += bae.AppliedDuration / playerActiveDuration;
                        }
                    }
                    if (abae is BuffExtensionEvent bee)
                    {
                        totalOutgoingByExtension += bee.ExtendedDuration;
                        if (playerActiveDuration > 0)
                        {
                            totalActiveOutgoingByExtension += bee.ExtendedDuration / playerActiveDuration;
                        }
                    }
                }
            }
            totalOutgoing += totalOutgoingByExtension;
            totalActiveOutgoing += totalActiveOutgoingByExtension;

            totalOutgoing /= phaseDuration;
            totalOutgoingByExtension /= phaseDuration;

            //TODO(Rennorb) @perf
            var uptime = new BuffVolumeStatistics();
            var uptimeActive = new BuffVolumeStatistics();
            buffs[buff.ID] = uptime;
            activeBuffs[buff.ID] = uptimeActive;
            if (buff.Type == BuffType.Duration)
            {
                uptime.Outgoing = Math.Round(100.0 * totalOutgoing / playerCount, ParserHelper.BuffDigit);
                uptime.OutgoingByExtension = Math.Round(100.0 * (totalOutgoingByExtension) / playerCount, ParserHelper.BuffDigit);
                //
                if (activePlayerCount > 0)
                {
                    uptimeActive.Outgoing = Math.Round(100.0 * totalActiveOutgoing / activePlayerCount, ParserHelper.BuffDigit);
                    uptimeActive.OutgoingByExtension = Math.Round(100.0 * (totalActiveOutgoingByExtension) / activePlayerCount, ParserHelper.BuffDigit);
                }
            }
            else if (buff.Type == BuffType.Intensity)
            {
                uptime.Outgoing = Math.Round(totalOutgoing / playerCount, ParserHelper.BuffDigit);
                uptime.OutgoingByExtension = Math.Round((totalOutgoingByExtension) / playerCount, ParserHelper.BuffDigit);
                //
                if (activePlayerCount > 0)
                {
                    uptimeActive.Outgoing = Math.Round(totalActiveOutgoing / activePlayerCount, ParserHelper.BuffDigit);
                    uptimeActive.OutgoingByExtension = Math.Round((totalActiveOutgoingByExtension) / activePlayerCount, ParserHelper.BuffDigit);
                }
            }
        }

        return (buffs, activeBuffs);
    }


    internal static (Dictionary<long, BuffVolumeStatistics> Volumes, Dictionary<long, BuffVolumeStatistics> ActiveVolumes) GetBuffVolumesForSelf(ParsedEvtcLog log, SingleActor dstActor, long start, long end)
    {
        var buffs = new Dictionary<long, BuffVolumeStatistics>();
        var activeBuffs = new Dictionary<long, BuffVolumeStatistics>();

        long phaseDuration = end - start;
        long playerActiveDuration = dstActor.GetActiveDuration(log, start, end);
        foreach (Buff buff in dstActor.GetTrackedBuffs(log))
        {

            double totalIncoming = 0;
            double totalIncomingByExtension = 0;
            double totalIncomingByUnknownExtension = 0;
            double totalOutgoing = 0;
            double totalOutgoingByExtension = 0;
            //
            double totalActiveIncoming = 0;
            double totalActiveIncomingByExtension = 0;
            double totalActiveIncomingByUnknownExtension = 0;
            double totalActiveOutgoing = 0;
            double totalActiveOutgoingByExtension = 0;
            foreach (BuffEvent abae in dstActor.GetBuffApplyEventsOnByID(log, start, end, buff.ID, null))
            {
                if (abae is BuffApplyEvent bae)
                {
                    // We ignore infinite duration buffs
                    /*if (bae.AppliedDuration >= int.MaxValue)
                    {
                        continue;
                    }*/
                    if (abae.CreditedBy.Is(dstActor.AgentItem))
                    {
                        totalOutgoing += bae.AppliedDuration;
                    }
                    totalIncoming += bae.AppliedDuration;
                }
                if (abae is BuffExtensionEvent bee)
                {
                    if (abae.CreditedBy.Is(dstActor.AgentItem))
                    {
                        totalOutgoingByExtension += bee.ExtendedDuration;
                    }
                    totalIncomingByExtension += bee.ExtendedDuration;
                    if (abae.CreditedBy.IsUnknown)
                    {
                        totalIncomingByUnknownExtension += bee.ExtendedDuration;
                    }
                }
            }
            totalIncoming += totalIncomingByExtension;
            totalOutgoing += totalOutgoingByExtension;

            if (playerActiveDuration > 0)
            {
                totalActiveIncoming = totalIncoming / playerActiveDuration;
                totalActiveOutgoing = totalOutgoing / playerActiveDuration;
                totalActiveIncomingByExtension = totalIncomingByExtension / playerActiveDuration; 
                totalActiveIncomingByUnknownExtension = totalIncomingByUnknownExtension / playerActiveDuration;
                totalActiveOutgoingByExtension = totalOutgoingByExtension / playerActiveDuration;
            }

            totalIncoming /= phaseDuration;
            totalIncomingByExtension /= phaseDuration;
            totalIncomingByUnknownExtension /= phaseDuration;
            totalOutgoing /= phaseDuration;
            totalOutgoingByExtension /= phaseDuration;

            var uptime = new BuffVolumeStatistics();
            var uptimeActive = new BuffVolumeStatistics();
            buffs[buff.ID] = uptime;
            activeBuffs[buff.ID] = uptimeActive;
            if (buff.Type == BuffType.Duration)
            {
                uptime.Incoming = Math.Round(100.0 * totalIncoming, ParserHelper.BuffDigit);
                uptime.IncomingByExtension = Math.Round(100.0 * totalIncomingByExtension, ParserHelper.BuffDigit);
                uptime.IncomingByUnknownExtension = Math.Round(100.0 * totalIncomingByUnknownExtension, ParserHelper.BuffDigit);
                uptime.Outgoing = Math.Round(100.0 * totalOutgoing, ParserHelper.BuffDigit);
                uptime.OutgoingByExtension = Math.Round(100.0 * (totalOutgoingByExtension), ParserHelper.BuffDigit);
                //
                if (playerActiveDuration > 0)
                {
                    uptimeActive.Incoming = Math.Round(100.0 * totalActiveIncoming, ParserHelper.BuffDigit);
                    uptimeActive.IncomingByExtension = Math.Round(100.0 * totalActiveIncomingByExtension, ParserHelper.BuffDigit);
                    uptimeActive.IncomingByUnknownExtension = Math.Round(100.0 * totalActiveIncomingByUnknownExtension, ParserHelper.BuffDigit);
                    uptimeActive.Outgoing = Math.Round(100.0 * totalActiveOutgoing, ParserHelper.BuffDigit);
                    uptimeActive.OutgoingByExtension = Math.Round(100.0 * (totalActiveOutgoingByExtension), ParserHelper.BuffDigit);
                }
            }
            else if (buff.Type == BuffType.Intensity)
            {
                uptime.Incoming = Math.Round(totalIncoming, ParserHelper.BuffDigit);
                uptime.IncomingByExtension = Math.Round(totalIncomingByExtension, ParserHelper.BuffDigit);
                uptime.IncomingByUnknownExtension = Math.Round(totalIncomingByUnknownExtension, ParserHelper.BuffDigit);
                uptime.Outgoing = Math.Round(totalOutgoing, ParserHelper.BuffDigit);
                uptime.OutgoingByExtension = Math.Round((totalOutgoingByExtension) , ParserHelper.BuffDigit);
                //
                if (playerActiveDuration > 0)
                {
                    uptimeActive.Incoming = Math.Round(totalActiveIncoming, ParserHelper.BuffDigit);
                    uptimeActive.IncomingByExtension = Math.Round(totalActiveIncomingByExtension, ParserHelper.BuffDigit);
                    uptimeActive.IncomingByUnknownExtension = Math.Round(totalActiveIncomingByUnknownExtension , ParserHelper.BuffDigit);
                    uptimeActive.Outgoing = Math.Round(totalActiveOutgoing , ParserHelper.BuffDigit);
                    uptimeActive.OutgoingByExtension = Math.Round((totalActiveOutgoingByExtension) , ParserHelper.BuffDigit);
                }
            }
        }
        return (buffs, activeBuffs);
    }

}

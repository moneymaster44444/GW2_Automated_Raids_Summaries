﻿

namespace GW2EIJSON;

/// <summary>
/// Class representing consumables
/// </summary>
public class JsonConsumable
{
    /// <summary>
    /// Number of stacks
    /// </summary>
    public int Stack;

    /// <summary>
    /// Duration of the consumable
    /// </summary>
    public int Duration;

    /// <summary>
    /// Time of application of the consumable
    /// </summary>
    public long Time;

    /// <summary>
    /// ID of the consumable
    /// </summary>
    /// <seealso cref="JsonLog.BuffMap"/>
    public long Id;
}

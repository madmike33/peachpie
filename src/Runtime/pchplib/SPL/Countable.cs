﻿using Pchp.Core;

/// <summary>
/// Classes implementing Countable can be used with the <c>count()</c> function.
/// </summary>
public interface Countable
{
    /// <summary>
    /// Count elements of an object.
    /// </summary>
    /// <returns>The custom count as an integer.</returns>
    PhpValue count();
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Entry.Enums;

/// <summary>
/// The submodule checkout type for entry.
/// </summary>
public enum SubmoduleCheckoutType
{
    /// <summary>
    /// Do not to fetch submodules.
    /// </summary>
    None,

    /// <summary>
    /// Checkout a single level of submodules.
    /// </summary>
    SingleLevel,

    /// <summary>
    /// Checkout submodules of submodules.
    /// </summary>
    Recursive,
}
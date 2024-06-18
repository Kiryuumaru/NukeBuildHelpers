using NukeBuildHelpers.Common.Models;
using NukeBuildHelpers.Entry.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Entry.Definitions;

internal abstract class DependentEntryDefinition : EntryDefinition, IDependentEntryDefinition
{
    string[] IDependentEntryDefinition.AppIds { get; set; } = [];
}

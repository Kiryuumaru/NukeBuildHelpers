using NukeBuildHelpers.Entry.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Entry.Definitions;

internal abstract class TargetEntryDefinition : EntryDefinition, IPublishEntryDefinition
{
    string? ITargetEntryDefinition.AppId { get; set; }
}

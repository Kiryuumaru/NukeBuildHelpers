using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Entry.Interfaces;

public interface IDependentEntryDefinition : IEntryDefinition
{
    internal string[] AppIds { get; set; }
}

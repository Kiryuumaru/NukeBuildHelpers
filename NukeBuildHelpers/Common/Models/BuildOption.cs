using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Common.Models;

internal class BuildOption
{
    public required string Name { get; set; }

    public required string DisplayText { get; set; }

    public required Func<Task> Execute { get; set; }
}

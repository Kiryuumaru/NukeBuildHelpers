using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Models;

public class AppEntryConfig
{
    public required AppEntry Entry { get; init; }

    public required List<AppTestEntry> Tests { get; init; }
}

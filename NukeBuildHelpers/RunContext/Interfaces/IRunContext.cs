using NukeBuildHelpers.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.RunContext.Interfaces;

public interface IRunContext
{
    RunType RunType { get; }
}

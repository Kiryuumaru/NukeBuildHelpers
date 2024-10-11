using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NukeBuildHelpers.Common.Models;

internal class ObjectHolder<T>
{
    public T? Value { get; set; }
}

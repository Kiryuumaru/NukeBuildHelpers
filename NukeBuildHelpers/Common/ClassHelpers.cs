using Microsoft.Extensions.DependencyModel;
using System.Reflection;

namespace NukeBuildHelpers.Common;

internal static class ClassHelpers
{
    internal static List<T> GetInstances<T>()
    {
        var asmNames = DependencyContext.Default!.GetDefaultAssemblyNames();

        var allTypes = asmNames.Select(Assembly.Load)
            .SelectMany(t => t.GetTypes())
            .Where(p => p.GetTypeInfo().IsSubclassOf(typeof(T)) && !p.IsAbstract);

        List<T> instances = [];
        foreach (Type type in allTypes)
        {
            instances.Add((T)Activator.CreateInstance(type)!);
        }
        return instances;
    }
}

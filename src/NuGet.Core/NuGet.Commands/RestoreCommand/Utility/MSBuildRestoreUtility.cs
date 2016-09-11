using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public static class MSBuildRestoreUtility
    {
        public static DependencyGraphSpec GetDependencySpec(IEnumerable<IMSBuildItem> items)
        {
            var graphSpec = new DependencyGraphSpec();
            var itemsById = new Dictionary<string, List<IMSBuildItem>>(StringComparer.Ordinal);

            // Sort items and add restore specs
            foreach (var item in items)
            {
                var type = item.GetProperty("Type")?.ToLowerInvariant();
                var projectUniqueName = item.GetProperty("ProjectUniqueName");

                if ("restorespec".Equals(type, StringComparison.Ordinal))
                {
                    graphSpec.AddRestore(projectUniqueName);
                }
                else if (!string.IsNullOrEmpty(projectUniqueName))
                {
                    List<IMSBuildItem> idItems;
                    if (!itemsById.TryGetValue(projectUniqueName, out idItems))
                    {
                        idItems = new List<IMSBuildItem>(1);
                        itemsById.Add(projectUniqueName, idItems);
                    }

                    idItems.Add(item);
                }
            }

            // Add projects
            foreach (var spec in itemsById.Values.Select(GetPackageSpec))
            {
                graphSpec.AddProject(spec);
            }

            return graphSpec;
        }

        public static PackageSpec GetPackageSpec(IEnumerable<IMSBuildItem> items)
        {
            PackageSpec result = null;

            var specItem = items.SingleOrDefault(item =>
                "projectSpec".Equals(item.GetProperty("Type"),
                StringComparison.OrdinalIgnoreCase));

            if (specItem != null)
            {
                var typeString = specItem.GetProperty("OutputType");
                var restoreType = RestoreOutputType.Unknown;

                if (!string.IsNullOrEmpty(typeString))
                {
                    Enum.TryParse<RestoreOutputType>(typeString, ignoreCase: true, result: out restoreType);
                }

                // Get base spec
                if (restoreType == RestoreOutputType.UAP)
                {
                    result = GetUAPSpec(specItem);
                }
                else
                {
                    // Read msbuild data for both non-nuget and .NET Core
                    result = GetBaseSpec(specItem);
                }

                // Applies to all types
                result.RestoreMetadata.OutputType = restoreType;
                result.RestoreMetadata.ProjectPath = specItem.GetProperty("ProjectPath");
                result.RestoreMetadata.ProjectUniqueName = specItem.GetProperty("ProjectUniqueName");

                // Read project references for all
                AddProjectReferences(result, items);

                // Read package references for netcore
                if (restoreType == RestoreOutputType.NETCore)
                {
                    AddFrameworkAssemblies(result, items);
                    AddPackageReferences(result, items);
                }
            }

            return result;
        }

        private static void AddProjectReferences(PackageSpec spec, IEnumerable<IMSBuildItem> items)
        {
            var flatReferences = new HashSet<ProjectRestoreReference>();

            foreach (var item in GetItemByType(items, "ProjectReference"))
            {
                var dependency = new LibraryDependency();
                var projectReferenceUniqueName = item.GetProperty("ProjectReferenceUniqueName");
                var projectPath = item.GetProperty("ProjectPath");

                dependency.LibraryRange = new LibraryRange(
                    name: projectReferenceUniqueName,
                    versionRange: VersionRange.All,
                    typeConstraint: (LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject));

                // TODO: include, suppressParent, exclude
                dependency.IncludeType = LibraryIncludeFlags.All;
                dependency.SuppressParent = LibraryIncludeFlagUtils.DefaultSuppressParent;

                var frameworks = GetFrameworks(item);

                if (frameworks.Count == 0)
                {
                    // Add to all
                    AddDependencyIfNotExist(spec, dependency);
                }
                else
                {
                    // Add under each framework
                    foreach (var framework in frameworks)
                    {
                        AddDependencyIfNotExist(spec, framework, dependency);
                    }
                }

                var msbuildDependency = new ProjectRestoreReference()
                {
                    ProjectPath = projectPath,
                    ProjectUniqueName = projectReferenceUniqueName,
                };

                flatReferences.Add(msbuildDependency);
            }

            // Add project paths
            foreach (var msbuildDependency in flatReferences)
            {
                spec.RestoreMetadata.ProjectReferences.Add(msbuildDependency);
            }
        }

        private static bool AddDependencyIfNotExist(PackageSpec spec, LibraryDependency dependency)
        {
            if (!spec.Dependencies
                   .Select(d => d.Name)
                   .Contains(dependency.Name, StringComparer.OrdinalIgnoreCase))
            {
                spec.Dependencies.Add(dependency);

                return true;
            }

            return false;
        }

        private static bool AddDependencyIfNotExist(PackageSpec spec, NuGetFramework framework, LibraryDependency dependency)
        {
            var frameworkInfo = spec.GetTargetFramework(framework);

            if (!spec.Dependencies
                            .Concat(frameworkInfo.Dependencies)
                            .Select(d => d.Name)
                            .Contains(dependency.Name, StringComparer.OrdinalIgnoreCase))
            {
                frameworkInfo.Dependencies.Add(dependency);

                return true;
            }

            return false;
        }

        private static void AddPackageReferences(PackageSpec spec, IEnumerable<IMSBuildItem> items)
        {
            foreach (var item in GetItemByType(items, "Dependency"))
            {
                var dependency = new LibraryDependency();

                dependency.LibraryRange = new LibraryRange(
                    name: item.GetProperty("Id"),
                    versionRange: GetVersionRange(item),
                    typeConstraint: LibraryDependencyTarget.Package);

                // TODO: include, suppressParent, exclude
                dependency.IncludeType = LibraryIncludeFlags.All;
                dependency.SuppressParent = LibraryIncludeFlagUtils.DefaultSuppressParent;

                var frameworks = GetFrameworks(item);

                if (frameworks.Count == 0)
                {
                    AddDependencyIfNotExist(spec, dependency);
                }
                else
                {
                    foreach (var framework in frameworks)
                    {
                        AddDependencyIfNotExist(spec, framework, dependency);
                    }
                }
            }
        }

        private static void AddFrameworkAssemblies(PackageSpec spec, IEnumerable<IMSBuildItem> items)
        {
            foreach (var item in GetItemByType(items, "FrameworkAssembly"))
            {
                var dependency = new LibraryDependency();

                dependency.LibraryRange = new LibraryRange(
                    name: item.GetProperty("Id"),
                    versionRange: GetVersionRange(item),
                    typeConstraint: LibraryDependencyTarget.Reference);

                // TODO: include, suppressParent, exclude
                dependency.IncludeType = LibraryIncludeFlags.All;
                dependency.SuppressParent = LibraryIncludeFlagUtils.DefaultSuppressParent;

                var frameworks = GetFrameworks(item);

                if (frameworks.Count == 0)
                {
                    AddDependencyIfNotExist(spec, dependency);
                }
                else
                {
                    foreach (var framework in frameworks)
                    {
                        AddDependencyIfNotExist(spec, framework, dependency);
                    }
                }
            }
        }

        private static VersionRange GetVersionRange(IMSBuildItem item)
        {
            var rangeString = item.GetProperty("VersionRange");

            if (!string.IsNullOrEmpty(rangeString))
            {
                return VersionRange.Parse(rangeString);
            }

            return VersionRange.All;
        }

        private static PackageSpec GetUAPSpec(IMSBuildItem specItem)
        {
            PackageSpec result;
            var projectPath = specItem.GetProperty("ProjectPath");
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            var projectJsonPath = specItem.GetProperty("ProjectJsonPath");

            // Read project.json
            result = JsonPackageSpecReader.GetPackageSpec(projectName, projectJsonPath);

            result.RestoreMetadata = new ProjectRestoreMetadata();
            result.RestoreMetadata.ProjectJsonPath = projectJsonPath;
            result.RestoreMetadata.ProjectName = projectName;
            return result;
        }

        private static PackageSpec GetBaseSpec(IMSBuildItem specItem)
        {
            var frameworkInfo = GetFrameworks(specItem)
                .Select(framework => new TargetFrameworkInformation()
                {
                    FrameworkName = framework
                })
                .ToList();

            var spec = new PackageSpec(frameworkInfo);
            spec.RestoreMetadata = new ProjectRestoreMetadata();

            spec.FilePath = specItem.GetProperty("ProjectPath");
            spec.Name = specItem.GetProperty("ProjectName");

            if (string.IsNullOrEmpty(spec.Name) && !string.IsNullOrEmpty(spec.FilePath))
            {
                spec.Name = Path.GetFileNameWithoutExtension(spec.FilePath);
            }

            return spec;
        }

        private static HashSet<NuGetFramework> GetFrameworks(IMSBuildItem item)
        {
            var frameworks = new HashSet<NuGetFramework>();

            var frameworksString = item.GetProperty("TargetFrameworks");
            if (!string.IsNullOrEmpty(frameworksString))
            {
                frameworks.UnionWith(frameworksString.Split(';').Select(NuGetFramework.Parse));
            }

            return frameworks;
        }

        private static IEnumerable<IMSBuildItem> GetItemByType(IEnumerable<IMSBuildItem> items, string type)
        {
            return items.Where(e => type.Equals(e.GetProperty("Type"), StringComparison.OrdinalIgnoreCase));
        }

        public static void Dump(IEnumerable<IMSBuildItem> items, ILogger log)
        {
            foreach (var item in items)
            {
                log.LogDebug($"Item: {item.Identity}");

                foreach (var key in item.Properties)
                {
                    var val = item.GetProperty(key);

                    if (!string.IsNullOrEmpty(val))
                    {
                        log.LogDebug($"  {key}={val}");
                    }
                }
            }
        }
    }
}

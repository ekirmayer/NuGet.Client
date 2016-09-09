using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public static class MSBuildRestoreUtility
    {
        public static DependencyGraphSpec GetDependencySpec(IEnumerable<IMSBuildItem> items)
        {
            throw new NotImplementedException();
        }

        public static PackageSpec GetPackageSpec(IEnumerable<IMSBuildItem> items)
        {
            throw new NotImplementedException();
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

        //private static DependencyGraphSpec CreateDGFile(IEnumerable<MSBuildItem> items)
        //{
        //    var dgFile = new DependencyGraphSpec();

        //    // Index items
        //    var restoreSpecs = new List<MSBuildItem>();
        //    var projectSpecs = new List<MSBuildItem>();
        //    var indexBySpecId = new Dictionary<string, List<MSBuildItem>>(StringComparer.OrdinalIgnoreCase);
        //    var projectsByPath = new Dictionary<string, List<MSBuildItem>>();

        //    // All projects
        //    var externalProjects = new Dictionary<string, ExternalProjectReference>(StringComparer.Ordinal);

        //    foreach (var item in items)
        //    {
        //        var type = item.Metadata["Type"].ToLowerInvariant();

        //        if (type == "projectspec")
        //        {
        //            var id = item.Metadata["ProjectSpecId"];

        //            var projectPath = item.Metadata["ProjectPath"];

        //            List<MSBuildItem> projectEntries;
        //            if (!projectsByPath.TryGetValue(projectPath, out projectEntries))
        //            {
        //                projectEntries = new List<MSBuildItem>(1);

        //                projectsByPath.Add(projectPath, projectEntries);
        //            }

        //            projectEntries.Add(item);
        //        }
        //        else if (type == "restorespec")
        //        {
        //            // Restore spec
        //            restoreSpecs.Add(item);
        //        }
        //        else
        //        {
        //            var id = item.Metadata["ProjectSpecId"];

        //            List<MSBuildItem> idItems;
        //            if (!indexBySpecId.TryGetValue(id, out idItems))
        //            {
        //                idItems = new List<MSBuildItem>(1);
        //                indexBySpecId.Add(id, idItems);
        //            }

        //            idItems.Add(item);
        //        }
        //    }

        //    // Create request for UWP
        //    foreach (var projectPath in projectsByPath.Keys)
        //    {
        //        JObject specJson = null;

        //        var specItems = projectsByPath[projectPath];

        //        if (specItems.Any(item => "uap".Equals(GetProperty(item, "OutputType"), StringComparison.OrdinalIgnoreCase)))
        //        {
        //            // This must contain exactly one item for UWP
        //            var specItem = specItems.SingleOrDefault();

        //            if (specItem == null)
        //            {
        //                throw new InvalidDataException($"Invalid restore data for {projectPath}.");
        //            }

        //            var projectSpecId = specItem.Metadata["ProjectSpecId"];
        //            var projectJsonPath = specItem.Metadata["ProjectJsonPath"];
        //            var projectName = Path.GetFileNameWithoutExtension(projectPath);

        //            specJson = GetJson(projectJsonPath);

        //            // Get project references
        //            var projectReferences = new HashSet<string>(StringComparer.Ordinal);

        //            List<MSBuildItem> itemsForSpec;
        //            if (indexBySpecId.TryGetValue(projectSpecId, out itemsForSpec))
        //            {
        //                foreach (var item in itemsForSpec)
        //                {
        //                    var type = item.Metadata["Type"].ToLowerInvariant();

        //                    if (type == "projectreference")
        //                    {
        //                        projectReferences.Add(item.Metadata["ProjectPath"]);
        //                    }
        //                }
        //            }

        //            if (specJson.Property("msbuild") == null)
        //            {
        //                var msbuildObj = new JObject();
        //                specJson.Add("msbuild", msbuildObj);

        //                msbuildObj.Add("projectRestoreGuid", projectSpecId);
        //                msbuildObj.Add("projectPath", projectPath);
        //                msbuildObj.Add("projectJsonPath", projectJsonPath);
        //                msbuildObj.Add("outputType", "uap");

        //                var projRefs = new JObject();
        //                msbuildObj.Add("projectReferences", projRefs);

        //                foreach (var referencePath in projectReferences)
        //                {
        //                    var projRef = new JObject();
        //                    projRefs.Add(referencePath, projRefs);

        //                    projRef.Add("projectPath", referencePath);
        //                }
        //            }
        //        }

        //        // Add project to file
        //        dgFileProjects.Add(projectPath, specJson);
        //    }

        //    foreach (var restoreSpec in restoreSpecs)
        //    {
        //        var restoreSpecJson = new JObject();

        //        var projectPath = restoreSpec.Metadata["ProjectPath"];

        //        restoreSpecJson.Add("projectPath", projectPath);

        //        dgFileRestoreSpecs.Add(restoreSpecJson);
        //    }

        //    return dgFile;
        //}
    }
}

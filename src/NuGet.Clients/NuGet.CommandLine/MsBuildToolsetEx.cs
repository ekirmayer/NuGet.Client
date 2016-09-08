// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.Setup.Configuration;
using NuGet.Commands;
using NuGet.Common;

namespace NuGet.CommandLine
{
    public class MsBuildToolsetEx
    {
        private Toolset _toolset;
        private DateTime _installDateTime;

        public MsBuildToolsetEx(Toolset toolset)
        {
            _toolset = toolset;
        }

        internal MsBuildToolsetEx(ISetupInstance sxsToolset)
        {
            _toolset = new Toolset(toolsVersion: sxsToolset.GetInstallationVersion(),
                        toolsPath: sxsToolset.GetInstallationPath(),
                        projectCollection: null, 
                        msbuildOverrideTasksPath: string.Empty);
            _installDateTime = ConvertFILETIMEToDateTime(sxsToolset.GetInstallDate());
        }

        public string ToolsVersion
        {
            get
            {
                return _toolset?.ToolsVersion;
            }
        }

        public string ToolsPath
        {
            get
            {
                return _toolset?.ToolsPath;
            }
        }

        public static IEnumerable<MsBuildToolsetEx> AsMsToolsetExCollection(IEnumerable<Toolset> toolsets)
        {
            if (toolsets == null)
            {
                yield break;
            }

            foreach(var toolset in toolsets)
            {
                yield return new MsBuildToolsetEx(toolset);
            }
        }

        private static DateTime ConvertFILETIMEToDateTime(FILETIME time)
        {
            long highBits = time.dwHighDateTime;
            highBits = highBits << 32;
            return DateTime.FromFileTimeUtc(highBits | (long)(uint)time.dwLowDateTime);
        }
    }
}
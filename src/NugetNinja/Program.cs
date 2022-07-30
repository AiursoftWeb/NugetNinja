﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using Microsoft.NugetNinja.Core;
using Microsoft.NugetNinja.DeprecatedPackagePlugin;
using Microsoft.NugetNinja.PossiblePackageUpgradePlugin;
using Microsoft.NugetNinja.UselessPackageReferencePlugin;
using Microsoft.NugetNinja.UselessProjectReferencePlugin;

var description = "Nuget Ninja, a tool for detecting dependencies of .NET projects.";

var program = new RootCommand(description)
    .AddGlobalOptions()
    .AddPlugins(
        new DeprecatedPackagePlugin(),
        new PossiblePackageUpgradePlugin(),
        new UselessPackageReferencePlugin(),
        new UselessProjectReferencePlugin()
    );

return await program.InvokeAsync(args);
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Bicep.Cli.Services
{
    public class FeatureProvider : IFeatureProvider
    {
        public bool RegistryEnabled => bool.TryParse(Environment.GetEnvironmentVariable("BICEP_REGISTRY_ENABLED_EXPERIMENTAL"), out var enabled) ? enabled : false;
    }
}

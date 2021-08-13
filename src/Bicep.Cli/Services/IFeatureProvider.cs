// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Bicep.Cli.Services
{
    public interface IFeatureProvider
    {
        public bool RegistryEnabled { get; }
    }
}

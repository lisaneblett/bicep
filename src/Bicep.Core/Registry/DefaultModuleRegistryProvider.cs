// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Core.FileSystem;
using System.Collections.Immutable;

namespace Bicep.Core.Registry
{
    public class DefaultModuleRegistryProvider : IModuleRegistryProvider
    {
        private readonly IFileResolver fileResolver;
        private readonly IContainerRegistryClientFactory clientFactory;

        public DefaultModuleRegistryProvider(IFileResolver fileResolver, IContainerRegistryClientFactory clientFactory)
        {
            this.fileResolver = fileResolver;
            this.clientFactory = clientFactory;
        }

        public ImmutableArray<IModuleRegistry> Registries => new IModuleRegistry[]
{
            new LocalModuleRegistry(this.fileResolver),
            new OciModuleRegistry(fileResolver, this.clientFactory)
        }.ToImmutableArray();
    }
}

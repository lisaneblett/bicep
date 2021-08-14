// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Cli.Services;
using Bicep.Core.Registry;
using Bicep.Core.TypeSystem;
using System.IO;

namespace Bicep.Cli
{
    public class InvocationContext
    {
        public InvocationContext(
            IResourceTypeProvider resourceTypeProvider,
            TextWriter outputWriter,
            TextWriter errorWriter,
            string assemblyFileVersion,
            IFeatureProvider? features = null,
            IContainerRegistryClientFactory? clientFactory = null)
        {
            ResourceTypeProvider = resourceTypeProvider;
            OutputWriter = outputWriter;
            ErrorWriter = errorWriter;
            AssemblyFileVersion = assemblyFileVersion;
            Features = features ?? new FeatureProvider();
            ClientFactory = clientFactory ?? new ContainerRegistryClientFactory();
        }

        public IResourceTypeProvider ResourceTypeProvider { get; }

        public TextWriter OutputWriter { get; } 

        public TextWriter ErrorWriter { get; }

        public string AssemblyFileVersion { get; }

        public IFeatureProvider Features { get; }

        public IContainerRegistryClientFactory ClientFactory { get; }
    }
}

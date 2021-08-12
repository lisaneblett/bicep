// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Core.Modules;
using Bicep.Core.Registry.Oci;
using System.Threading.Tasks;

namespace Bicep.Core.Registry
{
    public interface IOciArtifactClient
    {
        Task<OciClientResult> PullArtifactsync(OciArtifactModuleReference reference);

        Task PushArtifactAsync(OciArtifactModuleReference reference, StreamDescriptor config, params StreamDescriptor[] layers);

        string GetLocalPackageDirectory(OciArtifactModuleReference reference);

        string GetLocalPackageEntryPointPath(OciArtifactModuleReference reference);
    }
}

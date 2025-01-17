// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

namespace Bicep.Core.RegistryClient.Models
{
    /// <summary> Sort options for ordering manifests in a collection. </summary>
    public enum ArtifactManifestOrderBy
    {
        /// <summary> Do not provide an orderby value in the request. </summary>
        None,
        /// <summary> Order manifests by LastUpdatedOn field, from most recently updated to least recently updated. </summary>
        LastUpdatedOnDescending,
        /// <summary> Order manifest by LastUpdatedOn field, from least recently updated to most recently updated. </summary>
        LastUpdatedOnAscending
    }
}

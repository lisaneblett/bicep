// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure;
using Azure.Core;
using Bicep.Core.Modules;
using Bicep.Core.Registry.Oci;
using Bicep.Core.RegistryClient;
using Bicep.Core.RegistryClient.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using UploadManifestOptions = Bicep.Core.RegistryClient.UploadManifestOptions;

namespace Bicep.Core.Registry
{
    public class AzureContainerRegistryManager
    {
        private readonly string artifactCachePath;
        private readonly TokenCredential tokenCredential;
        private readonly IContainerRegistryClientFactory clientFactory;

        public AzureContainerRegistryManager(string artifactCachePath, TokenCredential tokenCredential, IContainerRegistryClientFactory clientFactory)
        {
            this.artifactCachePath = artifactCachePath;
            this.tokenCredential = tokenCredential;
            this.clientFactory = clientFactory;
        }

        public async Task<OciClientResult> PullArtifactsync(OciArtifactModuleReference moduleReference)
        {
            try
            {
                await PullArtifactInternalAsync(moduleReference);

                return new(true, null);
            }
            catch(RequestFailedException exception) when (exception.Status == 404)
            {
                return new(false, "Module not found.");
            }
            catch(AcrManagerException exception)
            {
                // we can trust the message in our own exception
                return new(false, exception.Message);
            }
            catch(Exception exception)
            {
                return new(false, $"Unhandled exception: {exception}");
            }
        }

        public async Task PushArtifactAsync(OciArtifactModuleReference moduleReference, StreamDescriptor config, params StreamDescriptor[] layers)
        {
            // TODO: Add similar exception handling as in the pull* method

            // TODO: How do we choose this? Does it ever change?
            var algorithmIdentifier = DescriptorFactory.AlgorithmIdentifierSha256;

            var blobClient = this.CreateBlobClient(moduleReference);

            config.ResetStream();
            var configDescriptor = DescriptorFactory.CreateDescriptor(algorithmIdentifier, config);

            config.ResetStream();
            var configUploadResult = await blobClient.UploadBlobAsync(config.Stream);

            var layerDescriptors = new List<OciDescriptor>(layers.Length);
            foreach (var layer in layers)
            {
                layer.ResetStream();
                var layerDescriptor = DescriptorFactory.CreateDescriptor(algorithmIdentifier, layer);
                layerDescriptors.Add(layerDescriptor);

                layer.ResetStream();
                var layerUploadResult = await blobClient.UploadBlobAsync(layer.Stream);
            }

            var manifest = new OciManifest(2, configDescriptor, layerDescriptors);
            using var manifestStream = new MemoryStream();
            OciManifestSerialization.SerializeManifest(manifestStream, manifest);

            manifestStream.Position = 0;
            // BUG: the client closes the stream :(
            var manifestUploadResult = await blobClient.UploadManifestAsync(manifestStream, new UploadManifestOptions(ContentType.ApplicationVndOciImageManifestV1Json, moduleReference.Tag));
        }

        public string GetLocalPackageDirectory(OciArtifactModuleReference reference)
        {
            var baseDirectories = new[]
            {
                this.artifactCachePath,
                reference.Registry
            };

            // TODO: Directory convention problematic. /foo/bar:baz and /foo:bar will share directories
            var directories = baseDirectories
                .Concat(reference.Repository.Split('/', StringSplitOptions.RemoveEmptyEntries))
                .Append(reference.Tag)
                .ToArray();

            return Path.Combine(directories);
        }

        public string GetLocalPackageEntryPointPath(OciArtifactModuleReference reference) => Path.Combine(this.GetLocalPackageDirectory(reference), "main.json");

        private static Uri GetRegistryUri(OciArtifactModuleReference moduleReference) => new Uri($"https://{moduleReference.Registry}");

        private BicepRegistryBlobClient CreateBlobClient(OciArtifactModuleReference moduleReference) => this.clientFactory.CreateBlobClient(GetRegistryUri(moduleReference), moduleReference.Repository, this.tokenCredential);

        private static string TrimSha(string digest)
        {
            int index = digest.IndexOf(':');
            if (index > -1)
            {
                return digest.Substring(index + 1);
            }

            return digest;
        }

        private static void CreateModuleDirectory(string modulePath)
        {
            try
            {
                // ensure that the directory exists
                Directory.CreateDirectory(modulePath);
            }
            catch (Exception exception)
            {
                throw new AcrManagerException("Unable to create the local module directory.", exception);
            }
        }

        private async Task PullArtifactInternalAsync(OciArtifactModuleReference moduleReference)
        {
            var client = this.CreateBlobClient(moduleReference);

            var manifestResponse = await client.DownloadManifestAsync(moduleReference.Tag, new DownloadManifestOptions(ContentType.ApplicationVndOciImageManifestV1Json));
            ValidateManifestResponse(manifestResponse);

            string modulePath = GetLocalPackageDirectory(moduleReference);
            CreateModuleDirectory(modulePath);

            // the SDK doesn't expose all the manifest properties we need
            // so we need to deserialize the manifest ourselves to get everything
            var manifest = DeserializeManifest(manifestResponse.Value.Content);

            ValidateConfig(manifest.Config);
            

            foreach (var layer in manifest.Layers)
            {
                var fileName = layer.Annotations.TryGetValue("org.opencontainers.image.title", out var title) ? title : TrimSha(layer.Digest);

                var layerPath = Path.Combine(modulePath, fileName) ?? throw new InvalidOperationException("Combined artifact path is null.");

                var blobResult = await client.DownloadBlobAsync(layer.Digest);

                using var fileStream = new FileStream(layerPath, FileMode.Create);
                await blobResult.Value.Content.CopyToAsync(fileStream);
            }
        }

        private static void ValidateManifestResponse(Response<DownloadManifestResult> manifestResponse)
        {
            var expectedDigest = GetDigest(manifestResponse.GetRawResponse());

            var stream = manifestResponse.Value.Content;
            stream.Position = 0;

            // TODO: The registry may use a different digest algorithm - we need to handle that
            string actualDigest = DigestHelper.ComputeDigest(DigestHelper.AlgorithmIdentifierSha256, stream);

            if (!string.Equals(expectedDigest, actualDigest, StringComparison.Ordinal))
            {
                throw new AcrManagerException($"The digest of manifest contents (\"{actualDigest}\") does not match the digest in the registry response header (\"{expectedDigest}\").");
            }
        }

        private static void ValidateConfig(OciDescriptor config)
        {
            // media types are case insensitive
            if(!string.Equals(config.MediaType, BicepMediaTypes.BicepModuleConfigV1, StringComparison.OrdinalIgnoreCase))
            {
                throw new AcrManagerException($"The OCI artifact is not a valid Bicep module. The config media type \"{config.MediaType}\" is not supported.");
            }

            if(config.Size != 0)
            {
                throw new AcrManagerException($"The OCI artifact is not a valid Bicep module. Unexpected non-empty config blob.");
            }
        }

        private static string GetDigest(Response response)
        {
            const string HeaderName = "Docker-Content-Digest";
            if (response.Headers.TryGetValue(HeaderName, out var digest))
            {
                return digest;
            }

            throw new AcrManagerException($"The registry response did not include the {HeaderName} header with the digest.");
        }

        private static OciManifest DeserializeManifest(Stream stream)
        {
            try
            {
                return OciManifestSerialization.DeserializeManifest(stream);
            }
            catch(Exception exception)
            {
                throw new AcrManagerException("Unable to deserialize the module manifest.", exception);
            }
        }

        private class AcrManagerException : Exception
        {
            public AcrManagerException(string message) : base(message)
            {
            }

            public AcrManagerException(string message, Exception innerException) : base(message, innerException)
            {
            }
        }
    }
}

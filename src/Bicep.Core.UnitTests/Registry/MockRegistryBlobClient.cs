// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure;
using Bicep.Core.Registry.Oci;
using Bicep.Core.RegistryClient;
using Moq;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bicep.Core.UnitTests.Registry
{
    public class MockRegistryBlobClient: BicepRegistryBlobClient
    {
        private static MockRepository Repository = new(MockBehavior.Strict);

        // maps digest to blob bytes
        private readonly ConcurrentDictionary<string, ImmutableArray<byte>> blobs = new();

        // maps digest to manifest bytes
        private readonly ConcurrentDictionary<string, ImmutableArray<byte>> manifests = new();

        // maps tag to manifest digest
        private readonly ConcurrentDictionary<string, string> manifestTags = new();

        public MockRegistryBlobClient() : base()
        {
            // ensure we call the base parameterless constructor to prevent outgoing calls
        }

        public override async Task<Response<DownloadBlobResult>> DownloadBlobAsync(string digest, DownloadBlobOptions? options = default, CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            if(!this.blobs.TryGetValue(digest, out var bytes))
            {
                throw new RequestFailedException(404, "Mock blob does not exist.");
            }

            return CreateResult(new DownloadBlobResult(digest, WriteStream(bytes)));
        }

        public override async Task<Response<DownloadManifestResult>> DownloadManifestAsync(string reference, DownloadManifestOptions? options = default, CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            string? digest;
            if (reference.StartsWith(DigestHelper.AlgorithmIdentifierSha256 + ':'))
            {
                // digest ref
                digest = reference;
            }
            else
            {
                // tag ref
                if (!this.manifestTags.TryGetValue(reference, out digest))
                {
                    throw new RequestFailedException(404, "Mock manifest tag does not exist.");
                }
            }

            if(!this.manifests.TryGetValue(digest, out var bytes))
            {
                throw new RequestFailedException(404, "Mock manifest does not exist.");
            }

            return CreateResult(new DownloadManifestResult(WriteStream(bytes)));
        }

        public override async Task<Response<UploadBlobResult>> UploadBlobAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            var (copy, digest) = ReadStream(stream);
            blobs.TryAdd(digest, copy);

            return CreateResult(new UploadBlobResult());
        }

        public override async Task<Response<UploadManifestResult>> UploadManifestAsync(Stream stream, UploadManifestOptions? options = default, CancellationToken cancellationToken = default)
        {
            options ??= new UploadManifestOptions();
            if(options.MediaType != RegistryClient.Models.ContentType.ApplicationVndOciImageManifestV1Json)
            {
                throw new RequestFailedException(500, "This mock client supports only OCI artifacts.");
            }

            await Task.Yield();

            var (copy, digest) = ReadStream(stream);
            blobs.TryAdd(digest, copy);

            if(options.Tag is not null)
            {
                // map tag to the digest
                this.manifestTags[options.Tag] = digest;
            }

            return CreateResult(new UploadManifestResult());
        }

        private static Response<T> CreateResult<T>(T value)
        {
            var response = Repository.Create<Response>();

            var result = Repository.Create<Response<T>>();
            result.SetupGet(m => m.Value).Returns(value);
            result.Setup(m => m.GetRawResponse()).Returns(response.Object);

            return result.Object;
        }

        private static (ImmutableArray<byte>, string) ReadStream(Stream stream)
        {
            stream.Position = 0;
            string digest = DigestHelper.ComputeDigest(DigestHelper.AlgorithmIdentifierSha256, stream);

            stream.Position = 0;
            using var reader = new BinaryReader(stream, new UTF8Encoding(false), true);

            var builder = ImmutableArray.CreateBuilder<byte>();

            stream.Position = 0;
            var bytes = reader.ReadBytes((int)stream.Length).ToImmutableArray();

            return (bytes, digest);
        }

        private static Stream WriteStream(ImmutableArray<byte> bytes)
        {
            var stream = new MemoryStream(bytes.Length);
            var writer = new BinaryWriter(stream, new UTF8Encoding(false), true);

            writer.Write(bytes.AsSpan());
            stream.Position = 0;

            return stream;
        }
    }
}

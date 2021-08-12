// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Identity;
using Bicep.Core.Modules;
using Bicep.Core.Registry;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Bicep.Core.IntegrationTests.Registry
{
    [TestClass]
    public class AcrClientTests
    {
        [TestMethod]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "Not needed")]
        public async Task Foo()
        {
            var creds = new DefaultAzureCredential();
            var client = new AcrClient(@"D:\OCISDK", creds);

            var reference = OciArtifactModuleReference.TryParse("majastrzoci.azurecr.io/examples/000/01-hello-world:v0.1", out _)!;
            reference.Should().NotBeNull();

            var result = await client.PullArtifactsync(reference);
        }
    }
}

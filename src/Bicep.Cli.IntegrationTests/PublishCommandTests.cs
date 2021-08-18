// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Core;
using Bicep.Core.Registry;
using Bicep.Core.Samples;
using Bicep.Core.UnitTests.Assertions;
using Bicep.Core.UnitTests.Registry;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DataSet = Bicep.Core.Samples.DataSet;

namespace Bicep.Cli.IntegrationTests
{
    [TestClass]
    public class PublishCommandTests : TestBase
    {
        [NotNull]
        public TestContext? TestContext { get; set; }

        [TestMethod]
        public async Task Publish_ZeroFiles_ShouldFail_WithExpectedErrorMessage()
        {
            var (output, error, result) = await Bicep("publish");

            using (new AssertionScope())
            {
                result.Should().Be(1);
                output.Should().BeEmpty();

                error.Should().NotBeEmpty();
                error.Should().Contain($"The input file path was not specified");
            }
        }

        [TestMethod]
        public async Task Publish_MissingTarget_ShouldProduceExpectedError()
        {
            var (output, error, result) = await Bicep("publish", "WrongFile.bicep");

            result.Should().Be(1);
            output.Should().BeEmpty();
            error.Should().Contain("The target module was not specified.");
        }

        [TestMethod]
        public async Task Publish_InvalidTarget_ShouldProduceExpectedError()
        {
            var (output, error, result) = await Bicep("publish", "WrongFile.bicep", "--target", "fake:");

            result.Should().Be(1);
            output.Should().BeEmpty();
            error.Should().Contain("The specified module target \"fake:\" is not valid.");
        }

        [TestMethod]
        public async Task Publish_InvalidInputFile_ShouldProduceExpectedError()
        {
            var (output, error, result) = await Bicep("publish", "WrongFile.bicep", "--target", "oci:example.azurecr.io/hello/there:v1");

            result.Should().Be(1);
            output.Should().BeEmpty();
            error.Should().MatchRegex(@"An error occurred reading file\. Could not find file '.+WrongFile.bicep'\.");
        }

        [TestMethod]
        public async Task Publish_LocalTarget_ShouldProduceExpectedError()
        {
            var (output, error, result) = await Bicep("publish", "WrongFile.bicep", "--target", "./test.bicep");

            result.Should().Be(1);
            output.Should().BeEmpty();
            error.Should().Contain("The specified module target \"./test.bicep\" is not supported.");
        }

        [DataTestMethod]
        [DynamicData(nameof(GetValidDataSets), DynamicDataSourceType.Method, DynamicDataDisplayNameDeclaringType = typeof(DataSet), DynamicDataDisplayName = nameof(DataSet.GetDisplayName))]
        public async Task Publish_ValidFile_ShouldSucceed(DataSet dataSet)
{
            var outputDirectory = dataSet.SaveFilesToTestDirectory(TestContext);
            var bicepFilePath = Path.Combine(outputDirectory, DataSet.TestFileMain);
            var compiledFilePath = Path.Combine(outputDirectory, DataSet.TestFileMainCompiled);

            var testClient = new MockRegistryBlobClient();

            // files are valid, so we need to setup a mock client
            var clientFactory = Repository.Create<IContainerRegistryClientFactory>();
            clientFactory
                .Setup(m => m.CreateBlobClient(new Uri("https://example.com"), $"test/{dataSet.Name}", It.IsAny<TokenCredential>()))
                .Returns(testClient);

            var settings = CreateDefaultSettings() with { ClientFactory = clientFactory.Object };

            var (output, error, result) = await Bicep(settings, "publish", bicepFilePath, "--target", $"oci:example.com/test/{dataSet.Name}:v1");
            result.Should().Be(0);
            output.Should().BeEmpty();
            AssertNoErrors(error);

            using var expectedCompiledStream = new FileStream(compiledFilePath, FileMode.Open, FileAccess.Read);

            // verify the module was published
            testClient.Should().OnlyHaveModule("v1", expectedCompiledStream);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetInvalidDataSets), DynamicDataSourceType.Method, DynamicDataDisplayNameDeclaringType = typeof(DataSet), DynamicDataDisplayName = nameof(DataSet.GetDisplayName))]
        public async Task Publish_InvalidFile_ShouldFail_WithExpectedErrorMessage(DataSet dataSet)
        {
            var outputDirectory = dataSet.SaveFilesToTestDirectory(TestContext);
            var bicepFilePath = Path.Combine(outputDirectory, DataSet.TestFileMain);

            // default settings are fine, publish won't actually happen
            var settings = CreateDefaultSettings();

            var diagnostics = GetAllDiagnostics(bicepFilePath, settings.ClientFactory);

            var (output, error, result) = await Bicep(settings, "publish", bicepFilePath, "--target", $"oci:example.com/fail/{dataSet.Name}:v1");

            using (new AssertionScope())
            {
                result.Should().Be(1);
                output.Should().BeEmpty();
                error.Should().ContainAll(diagnostics);
            }
        }

        private static IEnumerable<object[]> GetValidDataSets() => DataSets
            .AllDataSets
            .Where(ds => ds.IsValid)
            .ToDynamicTestData();

        private static IEnumerable<object[]> GetInvalidDataSets() => DataSets
            .AllDataSets
            .Where(ds => ds.IsValid == false)
            .ToDynamicTestData();
    }
}

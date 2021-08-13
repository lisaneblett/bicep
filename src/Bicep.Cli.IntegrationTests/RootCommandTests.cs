// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Cli.Services;
using Bicep.Core.UnitTests.Assertions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;

namespace Bicep.Cli.IntegrationTests
{
    [TestClass]
    public class RootCommandTests : TestBase
    {
        private static readonly MockRepository Repository = new MockRepository(MockBehavior.Strict);

        [TestMethod]
        public async Task Build_WithWrongArgs_ShouldFail_WithExpectedErrorMessage()
        {
            var (output, error, result) = await Bicep("wrong", "fake", "broken");

            using (new AssertionScope())
            {
                result.Should().Be(1);
                output.Should().BeEmpty();

                error.Should().NotBeEmpty();
                error.Should().Contain($"Unrecognized arguments \"wrong fake broken\" specified. Use \"bicep --help\" to view available options.");
            }
        }

        [TestMethod]
        public async Task BicepVersionShouldPrintVersionInformation()
        {
            var (output, error, result) = await Bicep("--version");

            using (new AssertionScope())
            {
                result.Should().Be(0);
                error.Should().BeEmpty();

                output.Should().NotBeEmpty();
                output.Should().StartWith("Bicep CLI version");
            }
        }

        [TestMethod]
        public async Task BicepHelpShouldPrintHelp()
        {
            var (output, error, result) = await Bicep("--help");

            using (new AssertionScope())
            {
                result.Should().Be(0);
                error.Should().BeEmpty();

                output.Should().NotBeEmpty();
                output.Should().ContainAll(
                    "build",
                    "[options]",
                    "<file>",
                    ".bicep",
                    "Arguments:",
                    "Options:",
                    "--outdir",
                    "--outfile",
                    "--stdout",
                    "--version",
                    "--help",
                    "information",
                    "version",
                    "bicep",
                    "usage");
            }
        }

        [TestMethod]
        public async Task BicepHelpShouldIncludePublishWhenRegistryEnabled()
        {
            var featuresMock = Repository.Create<IFeatureProvider>();
            featuresMock.Setup(m => m.RegistryEnabled).Returns(true);

            var (output, error, result) = await Bicep(featuresMock.Object, "--help");

            result.Should().Be(0);
            error.Should().BeEmpty();

            output.Should().NotBeEmpty();
            output.Should().ContainAll(
                "publish",
                "Publishes",
                "registry",
                "reference",
                "azurecr.io",
                "oci",
                "--target");
        }

        [TestMethod]
        public async Task BicepHelpShouldNotIncludePublishWhenRegistryDisabled()
        {
            var featuresMock = Repository.Create<IFeatureProvider>();
            featuresMock.Setup(m => m.RegistryEnabled).Returns(false);

            var (output, error, result) = await Bicep(featuresMock.Object, "--help");

            result.Should().Be(0);
            error.Should().BeEmpty();

            output.Should().NotBeEmpty();
            output.Should().NotContainAny(
                "publish",
                "Publishes",
                "registry",
                "reference",
                "azurecr.io",
                "oci",
                "--target");
        }
    }
}


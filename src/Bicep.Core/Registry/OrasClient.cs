// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Core.Modules;
using Bicep.Core.Registry.Oci;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bicep.Core.Registry
{
    /// <summary>
    /// Wraps the ORAS CLI tool to interact with OCI artifacts.
    /// </summary>
    public class OrasClient : IOciArtifactClient
    {
        private readonly string artifactCachePath;

        public OrasClient(string artifactCachePath)
        {
            this.artifactCachePath = artifactCachePath;
        }

        public Task<OciClientResult> PullArtifactsync(OciArtifactModuleReference reference)
        {
            string localArtifactPath = GetLocalPackageDirectory(reference);

            // ensure that the directory exists
            Directory.CreateDirectory(localArtifactPath);

            using var process = new Process
            {
                // TODO: What about spaces? Do we need escaping?
                StartInfo = new("oras", $"pull {reference.ArtifactId} -a")
                {
                    CreateNoWindow = true,
                    ErrorDialog = false,
                    LoadUserProfile = true,

                    // LSP communicates over StdIn/StdOut, so we must not let the streams leak back to the parent process
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,

                    // ORAS uses the CWD to download artifacts
                    WorkingDirectory = localArtifactPath
                }
            };

            // drop StdOut from ORAS
            process.OutputDataReceived += (sender, e) => { };

            var error = new StringBuilder();
            process.ErrorDataReceived += (sender, e) => error.AppendLine(e.Data);

            try
            {
                process.Start();
            }
            catch (Exception exception)
            {
                return Task.FromResult(new OciClientResult(false, $"Unable to invoke \"oras pull\". {exception.Message}"));
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();

            if(process.ExitCode == 0)
            {
                return Task.FromResult(new OciClientResult(true, null));
}

            return Task.FromResult(new OciClientResult(false, error.ToString().Trim()));
        }

        public Task PushArtifactAsync(OciArtifactModuleReference reference, StreamDescriptor config, params StreamDescriptor[] layers)
        {
            throw new NotImplementedException();
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

        public string GetLocalPackageEntryPointPath(OciArtifactModuleReference reference) => Path.Combine(this.GetLocalPackageDirectory(reference), "main.bicep");
    }
}

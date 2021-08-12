// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Cli.Logging;
using Bicep.Core.Extensions;
using Bicep.Core.FileSystem;
using Bicep.Core.Registry;
using Bicep.Core.Semantics;
using Bicep.Core.Workspaces;
using Bicep.Decompiler;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Bicep.Cli.Services
{
    public class CompilationService
    {
        private readonly IDiagnosticLogger diagnosticLogger;
        private readonly IFileResolver fileResolver;
        private readonly IModuleDispatcher moduleDispatcher;
        private readonly InvocationContext invocationContext;
        private readonly Workspace workspace;

        public CompilationService(IDiagnosticLogger diagnosticLogger, IFileResolver fileResolver, InvocationContext invocationContext, IModuleDispatcher moduleDispatcher) 
        {
            this.diagnosticLogger = diagnosticLogger;
            this.fileResolver = fileResolver;
            this.moduleDispatcher = moduleDispatcher;
            this.invocationContext = invocationContext;
            this.workspace = new Workspace();
        }

        public Compilation Compile(string inputPath)
        {
            var inputUri = PathHelper.FilePathToFileUrl(inputPath);

            var sourceFileGrouping = SourceFileGroupingBuilder.Build(this.fileResolver, this.moduleDispatcher, this.workspace, inputUri);

            // module references in the file may be malformed
            // however we still want to surface as many errors as we can for the module refs that are valid
            // so we will try to restore modules with valid refs and skip everything else
            // (the diagnostics will be collected during compilation)
            if (moduleDispatcher.RestoreModules(moduleDispatcher.GetValidModuleReferences(sourceFileGrouping.ModulesToRestore)).Result)
            {
                // modules had to be restored - recompile
                sourceFileGrouping = SourceFileGroupingBuilder.Rebuild(moduleDispatcher, new Workspace(), sourceFileGrouping);
            }

            var compilation = new Compilation(this.invocationContext.ResourceTypeProvider, sourceFileGrouping);

            LogDiagnostics(compilation);

            return compilation;
        }

        public (Uri, ImmutableDictionary<Uri, string>) Decompile(string inputPath, string outputPath)
        {
            inputPath = PathHelper.ResolvePath(inputPath);
            Uri inputUri = PathHelper.FilePathToFileUrl(inputPath);

            Uri outputUri = PathHelper.FilePathToFileUrl(outputPath);

            var decompilation = TemplateDecompiler.DecompileFileWithModules(invocationContext.ResourceTypeProvider, new FileResolver(), inputUri, outputUri);

            foreach (var (fileUri, bicepOutput) in decompilation.filesToSave)
            {
                workspace.UpsertSourceFile(SourceFileFactory.CreateBicepFile(fileUri, bicepOutput));
            }

            _ = Compile(decompilation.entrypointUri.AbsolutePath); // to verify success we recompile and check for syntax errors.

            return decompilation;
        }

        private void LogDiagnostics(Compilation compilation)
        {
            if (compilation is null)
            {
                throw new Exception("Compilation is null. A compilation must exist before logging the diagnostics.");
            }

            foreach (var (bicepFile, diagnostics) in compilation.GetAllDiagnosticsByBicepFile())
            {
                foreach (var diagnostic in diagnostics)
                {
                    diagnosticLogger.LogDiagnostic(bicepFile.FileUri, diagnostic, bicepFile.LineStarts);
                }
            }
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NPlant.Core;

namespace NPlant.Generation
{
    public class NPlantRunner
    {
        private readonly Func<IRunnerRecorder> _recorder;
        private readonly INPlantRunnerOptions _options;

        public NPlantRunner(INPlantRunnerOptions options) : this(options, () => NullRecorder.Instance) { }

        public NPlantRunner(INPlantRunnerOptions options, Func<IRunnerRecorder> recorder)
        {
            _recorder = recorder;
            _options = options;
        }

        public void Run()
        {
            using (var recorder = _recorder())
            {
                recorder.Log("NPlantRunner Started...");
                recorder.Log(SummarizeConfiguration());

                var loader = new NPlantAssemblyLoader(recorder);
                Assembly assembly = loader.Load(_options.AssemblyToScan);

                var diagramLoader = new NPlantDiagramLoader(recorder);
                IEnumerable<IDiagram> diagrams = diagramLoader.Load(assembly);

                DirectoryInfo outputDirectory = RunInitializeOutputDirectoryStage();

                RunGenerateDiagramImagesStage(outputDirectory, diagrams, recorder);

                recorder.Log("NPlantRunner Finished...");
            }
        }

        private string SummarizeConfiguration()
        {
            var summary = new StringBuilder();

            summary.AppendLine("Task Attributes:");

            IEnumerable<PropertyInfo> properties = _options.GetType().GetProperties();

            foreach (var property in properties)
            {
                summary.AppendLine("    [{0}]: {1}".FormatWith(property.Name, property.GetValue(_options, null)));
            }

            summary.AppendLine();

            return summary.ToString();
        }

        private void RunGenerateDiagramImagesStage(FileSystemInfo outputDirectory, IEnumerable<IDiagram> diagrams, IRunnerRecorder recorder)
        {
            recorder.Log("Starting Stage: Diagram Rendering (output={0})...".FormatWith(outputDirectory.FullName));

            foreach (var diagram in diagrams)
            {
                var text = diagram.CreateGenerator().Generate();
                var javaPath = _options.JavaPath ?? "java.exe";
                var plantUml = _options.PlantUml ?? Assembly.GetExecutingAssembly().Location;

                var npImage = new NPlantImage(javaPath, new PlantUmlInvocation(plantUml))
                    {
                        Logger = recorder.Log
                    };

                var image = npImage.Create(text);

                if (image != null)
                {
                    var filePath = Path.Combine(outputDirectory.FullName, diagram.Name.ReplaceIllegalPathCharacters('_'));

                    image.SaveNPlantImage(filePath);
                }
            }

            recorder.Log("Finished Stage: Diagram Rendering...");
        }

        private DirectoryInfo RunInitializeOutputDirectoryStage()
        {
            var outputDirectory = new DirectoryInfo(_options.OutputDirectory.IfIsNullOrEmpty("."));

            outputDirectory.EnsureExists();

            if (this.ShouldClean())
            {
                var files = outputDirectory.GetFiles("*.*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    file.Delete();
                }
            }

            return outputDirectory;
        }

        private bool ShouldClean()
        {
            if (_options.Clean.IsNullOrEmpty())
                return false;

            return _options.Clean.ToBool(false);
        }
    }
}

using System;
using Palmmedia.ReportGenerator.Parser;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Palmmedia.ReportGenerator.Parser.Analysis;
using System.Linq;
using System.Xml.Linq;
using System.Security.Policy;

namespace Palmmedia.ReportGenerator
{
    /// <summary>
    /// Parser for updated SharpCover results
    /// </summary>
    internal class SharpCoverParser : ParserBase
    {

        struct CoverageLine
        {
          
            public string Assembly { get; set;}
            public string TypeName { get; set;}
            public string MethodName { get; set;}
            public int LineNumStart { get; set;}
            public int LineNumEnd { get; set;}
            public string Instruction { get; set; }
            public string SourceFile { get; set;}
            public bool Missed { get; set;}
        }

        List<CoverageLine> coverageData = new List<CoverageLine> ();

        public SharpCoverParser (string reportFile)
        {

            using (var sr = new StreamReader (reportFile)) {
                while (sr.Peek() >= 0)
                {
                    var line = sr.ReadLine ();
                    ProcessLine (line);
                }
            }

            var assemblies = ProcessAssemblies ();
            foreach (var assemblyData in assemblies)
                AddAssembly (assemblyData);
        }

        private void ProcessLine (string line)
        {
            bool missed = false;
            if (line.StartsWith ("!")) {
                missed = true;
                line = line.TrimStart ('!');
            }

            var data = line.Split ('\t');
           
            var assembly = data [0];
            var typeName = data [1];
            var methodName = data [2];
            var lineNumStart = Int32.Parse (data [3]);
            var lineNumEnd = Int32.Parse (data [4]);
            var instruction = data [6];
            var sourceFile = string.Empty;
            if (data.Length == 8)
                sourceFile = data [7];

            var coverageLine = new CoverageLine {
                Assembly = assembly,
                TypeName = typeName,
                MethodName = methodName,
                LineNumStart = lineNumStart,
                LineNumEnd = lineNumEnd,
                Instruction = instruction,
                SourceFile = sourceFile,
                Missed = missed
            };   

            coverageData.Add (coverageLine);

          
        }


        private IEnumerable<Assembly> ProcessAssemblies()
        {
            var assemblies = this.coverageData.GroupBy (x => x.Assembly);
            foreach (var assemblyData in assemblies)
            {
               
                Assembly assembly = new Assembly (assemblyData.Key);
                foreach (var cd in ProcessClasses (assembly, assemblyData.ToList()))
                    assembly.AddClass (cd);
                yield return assembly;
            }
        }

        private IEnumerable<Class> ProcessClasses(Assembly rootAssembly, IEnumerable<CoverageLine> lines)
        {
            var classes = lines.GroupBy (x => x.TypeName);
            foreach (var classData in classes)
            {
                if (classData.Key.StartsWith ("<>__AnonType"))
                    continue;
                
                Class classEntry = new Class (classData.Key, rootAssembly);
                var totalNum = classData.Count ();
                var hit = classData.Count (x => !x.Missed);

                classEntry.CoverageQuota = hit / totalNum;
                foreach (var codeFile in ProcessFiles (classEntry, classData.ToList()))
                    classEntry.AddFile (codeFile);

                yield return classEntry;
            }
        }

        private IEnumerable<CodeFile> ProcessFiles(Class rootClass, IEnumerable<CoverageLine> lines)
        {
            var files = lines.Where (x => !string.IsNullOrEmpty (x.SourceFile)).GroupBy(x=>x.SourceFile);

            foreach (var sourceFile in files) {
                var maxLine = sourceFile.Max ((arg) => arg.LineNumStart);

                if (maxLine == -1)
                    continue;
                
                int [] coverage = new int [maxLine+1];
                for (int i = 0; i < coverage.Length; i++)
                    coverage [i] = -1;

                foreach (var sourceLine in sourceFile)
                {
                    if (sourceLine.LineNumStart == -1)
                        continue;

                    for (int i = sourceLine.LineNumStart; i <= sourceLine.LineNumEnd; i++)
                        coverage [i] = sourceLine.Missed ? 0 : 1;
                   
                }

                CodeFile codeFile = new CodeFile (sourceFile.Key, coverage);
                ProcessTestMethods (codeFile, sourceFile.ToList());
                yield return codeFile;

            }
        }

        private void ProcessTestMethods(CodeFile codeFile, IEnumerable<CoverageLine> lines)
        {
            var methods = lines.GroupBy (x => x.MethodName);
          
                
            foreach (var method in methods)
            {
               
                
                string shortName = method.Key.Substring(method.Key.Substring(0, method.Key.IndexOf('/') + 1).LastIndexOf('.') + 1);
                TestMethod testMethod = new TestMethod (method.Key, shortName);

                var maxLine = method.Max (x => x.LineNumStart);


                if (maxLine == -1)
                    continue;

                int [] coverage = new int [maxLine+1];
                for (int i = 0; i < coverage.Length; i++)
                    coverage [i] = -1;
                
                foreach (var sourceLine in method)
                {
                    if (sourceLine.LineNumStart != -1)
                        for (int i = sourceLine.LineNumStart; i <= sourceLine.LineNumEnd; i++)
                            coverage [i] = sourceLine.Missed ? 0 : 1;
                }
                codeFile.AddCoverageByTestMethod (testMethod, coverage);
            }
        }

    }
}


using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Zajda.RoslynCodeGenCLI;

public static class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: CodeGenRunner <Generator.dll> <OutDir> [-T:AdditionalTextPath] [-P:Key=Value]...");
            return;
        }

        try
        {
            var assemblyPath = Path.GetFullPath(args[0]);
            var outDir = Path.GetFullPath(args[1]);
            var additionalTextPath = args.Skip(2).FirstOrDefault(a => a.StartsWith("-T:"))?[3..];

            var additionalTexts = new List<AdditionalText>();
            if (additionalTextPath != null)
            {
                additionalTexts = [new FileAdditionalText(Path.GetFullPath(additionalTextPath))];
            }
            
            var props = ParseProps(args.Skip(2).Where(a => a.StartsWith("-P:")));

            ValidateFiles(assemblyPath, outDir);
            var asm = Assembly.LoadFrom(assemblyPath);
            var generatorType  = GetGeneratorType(asm);
            
            var syntaxTrees =  RunGenerator(generatorType, additionalTexts, props);
            WriteResults(outDir, syntaxTrees, generatorType);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error executing generator: {ex.Message}");
            Environment.Exit(1);
        }
        
    }

    public static IList<SyntaxTree> RunGenerator(Type generatorType, List<AdditionalText> additionalTexts, IDictionary<string, string> props)
    {
        // 1) load the generator
        var generator = ((IIncrementalGenerator)Activator.CreateInstance(generatorType)!).AsSourceGenerator();

        // 2) feed it an empty compilation (or add references/texts if it needs them)
        var tpa = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(Path.PathSeparator);
        var refs = tpa.Select(p => MetadataReference.CreateFromFile(p));
        var emptyTree   = CSharpSyntaxTree.ParseText("");
        var compilation = CSharpCompilation.Create(Guid.NewGuid().ToString(), 
            [emptyTree],
            refs);

        var options = new InMemoryOptionsProvider(props);

        var driver = CSharpGeneratorDriver.Create([generator], additionalTexts: additionalTexts, optionsProvider: options);
            
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var result, out var diagnostics);
        
        // Report any errors coming from source generators
        var allDiagnostics = result.GetDiagnostics().Concat(diagnostics);
        foreach (var diagnostic in allDiagnostics)
        {
            Console.Error.WriteLine(diagnostic.ToString());
        }

        return result.SyntaxTrees.Except(compilation.SyntaxTrees).ToList();
    }

    private static void WriteResults(string outDir, IList<SyntaxTree> trees, Type generatorType)
    {
        // 3) write out every file the generator produced
        Directory.CreateDirectory(outDir);
        foreach (var tree in trees)
        {
            var name = string.IsNullOrEmpty(tree.FilePath) ? $"{generatorType.Name}_{Guid.NewGuid()}.g.cs" : Path.GetFileName(tree.FilePath);
            File.WriteAllText(Path.Combine(outDir, name), tree.ToString());
        }
        
        var generatedFiles = trees.Count;
        Console.WriteLine($"Successfully generated {generatedFiles} file(s) to {outDir}");
    }

    private static IDictionary<string, string> ParseProps(IEnumerable<string> args)
    {
        var props = new Dictionary<string, string>();
        foreach (var arg in args)
        {
            var pair = arg.Replace("-P:", "").Split('=');
            if (pair.Length != 2)
            {
                Console.WriteLine($"Warning: Ignoring invalid property format: {arg}");
                continue;
            }
            props.Add($"build_property.{pair[0]}", pair[1]);
        }

        return props;
    }

    private static Type GetGeneratorType(Assembly asm)
    {
        var generatorTypes = asm.GetTypes()
            .Where(t => typeof(IIncrementalGenerator).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
            .ToList();

        if (generatorTypes.Count == 0)
            throw new InvalidOperationException($"No IIncrementalGenerator implementations found in {asm.FullName}");

        if (generatorTypes.Count > 1)
            Console.WriteLine($"Warning: Multiple generators found in {asm.FullName}, using the first one: {generatorTypes[0].FullName}");

        return generatorTypes[0];
    }

    private static void ValidateFiles(string assemblyPath, string outDir)
    {
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Generator assembly not found: {assemblyPath}");

        if (!Directory.Exists(outDir))
        {
            Console.WriteLine($"Output directory does not exist, creating: {outDir}");
            Directory.CreateDirectory(outDir);
        }
    }

    sealed class FileAdditionalText : AdditionalText
    {
        public FileAdditionalText(string path) => Path = path;
        public override string Path { get; }
        
        public override SourceText GetText(CancellationToken _ = default)
        {
            return SourceText.From(File.ReadAllText(Path), Encoding.UTF8);
        }
    }

    sealed class DictOptions : AnalyzerConfigOptions
    {
        private readonly ImmutableDictionary<string,string> map;
        public DictOptions(IDictionary<string,string> map)
        {
            this.map = map.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
        }

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        {
            return map.TryGetValue(key, out value);
        }

        public static DictOptions Empty { get; } = new(new Dictionary<string, string>());
    }
    
    sealed class InMemoryOptionsProvider : AnalyzerConfigOptionsProvider
    {
        public InMemoryOptionsProvider(IDictionary<string,string> globals)
        {
            this.GlobalOptions = new DictOptions(globals);
        }

        public override AnalyzerConfigOptions GlobalOptions { get; }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree _)      => DictOptions.Empty;
        public override AnalyzerConfigOptions GetOptions(AdditionalText _)  => DictOptions.Empty;
    }
}
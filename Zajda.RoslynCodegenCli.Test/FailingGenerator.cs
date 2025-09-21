using Microsoft.CodeAnalysis;

namespace Zajda.RoslynCodegenCli.Test;

[Generator]
public class FailingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        throw new System.NotImplementedException("This generator always fails");
    }
}
using Microsoft.CodeAnalysis;
using Xunit;

using Zajda.RoslynCodeGenCLI;

namespace Zajda.RoslynCodegenCli.Test;

public class BasicFixture
{
    [Fact]
    public void FailingGeneratorReportsError()
    {
        Program.RunGenerator(typeof(FailingGenerator), new List<AdditionalText>(), new Dictionary<string, string>());
    }
}
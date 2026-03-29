using System.Threading.Tasks.Dataflow;

namespace TestsGenerator.Core;

public interface IGeneratorOutputHandler
{
    void Link(ISourceBlock<GeneratorOutput> sourceBlock, DataflowLinkOptions linkOptions);
    void WaitForCompletion();
}

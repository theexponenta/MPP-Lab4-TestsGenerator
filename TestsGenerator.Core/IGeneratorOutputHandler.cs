using System.Threading.Tasks.Dataflow;

namespace TestsGenerator.Core;

public interface IGeneratorOutputHandler
{
    void Link(ISourceBlock<GeneratorResult<GeneratorOutput, Exception>> sourceBlock, DataflowLinkOptions linkOptions);
    void WaitForCompletion();
    void SetParentGenerator(PipelineTestsGenerator generator);
}

using System.Threading.Tasks.Dataflow;

namespace TestsGenerator.Core;

using Exceptions;

public class PipelineTestsGenerator
{
    TransformManyBlock<GeneratorResult<SourceCode, Exception>, GeneratorResult<GeneratorOutput, Exception>> _generatorBlock;
    ActionBlock<GeneratorResult<SourceCode, Exception>> _inputErrorBlock;
    ActionBlock<GeneratorResult<GeneratorOutput, Exception>> _generatorErrorBlock;

    IGeneratorOutputHandler _outputHandler;
    
    object _listLock = new();
    List<PipelineGeneratorError> _errors = new();

    public IReadOnlyList<PipelineGeneratorError> Errors {get {return _errors.AsReadOnly();}}

    public PipelineTestsGenerator(int maxTasks, IGeneratorOutputHandler outputHandler)
    {
        _outputHandler = outputHandler;
        _outputHandler.SetParentGenerator(this);

        var executionOptions = new ExecutionDataflowBlockOptions{MaxDegreeOfParallelism = maxTasks};
        var linkOptions = new DataflowLinkOptions{PropagateCompletion = true};

        _inputErrorBlock = new(msg => AddError(msg.Error!, PipelineGeneratorStage.INPUT), executionOptions);
        _generatorErrorBlock = new(msg => AddError(msg.Error!, PipelineGeneratorStage.GENERATE), executionOptions);

        _generatorBlock = new(GenerateTests, executionOptions);
        _generatorBlock.LinkTo(_generatorErrorBlock, linkOptions, msg => msg.Error != null);
        _outputHandler.Link(_generatorBlock, linkOptions);
    }

    public void Link(ISourceBlock<GeneratorResult<SourceCode, Exception>> sourceBlock, DataflowLinkOptions linkOptions)
    {
        sourceBlock.LinkTo(_inputErrorBlock, linkOptions, msg => msg.Error != null);
        sourceBlock.LinkTo(_generatorBlock, linkOptions);
    }   

    public void WaitForCompletion()
    {
        _outputHandler.WaitForCompletion();
        _inputErrorBlock.Completion.Wait();
        _generatorErrorBlock.Completion.Wait();
    }

    public void AddError(Exception exception, PipelineGeneratorStage stage)
    {
        lock (_listLock)
        {
            _errors.Add(new PipelineGeneratorError(exception, stage));
        }
    }

    private IEnumerable<GeneratorResult<GeneratorOutput, Exception>> GenerateTests(GeneratorResult<SourceCode, Exception> result)
    {
        SourceCode sourceCode = result.Result!;

        var enumerator = TestsGenerator.GenerateTests(sourceCode.Source).GetEnumerator();
        int testsCount = 0;
        GeneratorOutput? generatorOutput = null;
        Exception? exception = null;
        while (true)
        {
            try
            {
                if (!enumerator.MoveNext())
                {
                    break;
                }

                generatorOutput = enumerator.Current;
                testsCount++;
            }
            catch (Exception e)
            {
                exception = e;
            }

            if (exception != null)
            {
                yield return new GeneratorResult<GeneratorOutput, Exception>(null, exception);
                yield break;
            }

            if (generatorOutput != null)
            {
                yield return new GeneratorResult<GeneratorOutput, Exception>(generatorOutput, null);
            }
        }

        if (testsCount == 0)
        {
            yield return new GeneratorResult<GeneratorOutput, Exception>(null, new SourceParsingError(sourceCode.Id));
        }
    }
}

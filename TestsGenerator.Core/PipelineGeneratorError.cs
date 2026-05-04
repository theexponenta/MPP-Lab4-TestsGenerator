namespace TestsGenerator.Core;


public enum PipelineGeneratorStage
{
    INPUT,
    GENERATE,
    OUTPUT,
}


public class PipelineGeneratorError
{
    public PipelineGeneratorStage Stage {get;}
    public Exception Error {get;}

    public PipelineGeneratorError(Exception error, PipelineGeneratorStage stage)
    {
        Stage = stage;
        Error = error;
    }
}

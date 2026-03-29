namespace TestsGenerator.Core;

public class GeneratorOutput
{
    public string ClassName {get;}
    public string Source {get;}

    public GeneratorOutput(string className, string source)
    {
        ClassName = className;
        Source = source;
    }
}


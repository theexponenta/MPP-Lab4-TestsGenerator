namespace TestsGenerator.Core.Exceptions;

public class SourceParsingError : Exception
{
    public string Id {get;}
    public SourceParsingError(string id)
    {
        Id = id;
    }
}

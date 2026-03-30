namespace TestsGenerator.Tests.TestData;

public interface IDependency
{
    void Method(string val);
}

public class ClassWithDependency
{
    public ClassWithDependency(IDependency dep)
    {
    }

    public void Publish(string key)
    {
    }
}

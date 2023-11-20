namespace epub2.Stories;

public class SiteAttribute : Attribute
{
    public string[] Name { get; }
    // public string TruyenfullVn { get; }

    public SiteAttribute(params string[] name)
    {
        Name = name.Select(o=>o.ToLower()).ToArray();
        // TruyenfullVn = truyenfullVn;
        // throw new NotImplementedException();
    }
}
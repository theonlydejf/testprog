using Newtonsoft.Json.Linq;

public static class GoldenStandard
{
    public static object Solve(JObject input)
    {
        int a = input.Value<int>("a");
        int b = input.Value<int>("b");
        return a + b;
    }
}

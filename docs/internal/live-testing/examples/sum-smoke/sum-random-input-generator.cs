using System;

public static class RandomInputGenerator
{
    public static object Generate(Random random, int testcaseIndex)
    {
        int a = random.Next(-50, 51);
        int b = random.Next(-50, 51);
        return new { a, b };
    }
}

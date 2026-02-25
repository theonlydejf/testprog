using System;

public static class RandomInputGenerator
{
    public static object Generate(Random random, int testcaseIndex)
    {
        int a = random.Next(-100, 101);
        int b = random.Next(-100, 101);
        return new { a, b };
    }
}

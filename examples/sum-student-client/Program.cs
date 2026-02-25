using testprog.client;

namespace testprog.examples.sum_student_client;

internal static class Program
{
    public static int Main(string[] args)
    {
        StudentClientOptions options = CreateOptions(args);

        return StudentConsoleTestRunner.RunWithExitCode(options, input =>
        {
            int a = input.GetInt("a");
            int b = input.GetInt("b");
            return a + b;
        });
    }

    private static StudentClientOptions CreateOptions(string[] args)
    {
        string studentId = args.Length > 0 ? args[0] : "sum-smoke-student";
        string displayName = args.Length > 1 ? args[1] : "Sum Smoke Student";
        string host = args.Length > 2 ? args[2] : "127.0.0.1";

        int port = 15000;
        if (args.Length > 3 && int.TryParse(args[3], out int parsedPort))
        {
            port = parsedPort;
        }

        return new StudentClientOptions
        {
            StudentId = studentId,
            DisplayName = displayName,
            ServerHost = host,
            ServerPort = port,
            DiscoveryPort = 15001
        };
    }
}

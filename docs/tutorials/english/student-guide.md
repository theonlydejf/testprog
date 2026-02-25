# Student Guide: How to use the testing client step by step

This guide assumes the client library is already added to your project.
If not, open [Student - Adding the Library (coming soon)](student-add-library.md) first.

## What you need

- your own C# project (console app)
- access to a running test server
- your identity values: `StudentId` and `DisplayName`

## Step 1: Prepare `StudentClientOptions` (UDP discovery)

Identity is mandatory. The server uses it for authorization (whitelist) and logs.

- `StudentId`: stable technical ID, for example `novakj`
- `DisplayName`: human-readable name, for example `Jan Novak`

Copy-paste ready client configuration using **UDP discovery**:

```csharp
using testprog.client;

StudentClientOptions options = new()
{
    StudentId = "novakj",       // TODO: your ID
    DisplayName = "Jan Novak",  // TODO: your name

    // UDP discovery (recommended):
    DiscoveryMulticastAddress = "239.0.0.222",
    DiscoveryPort = 15001,
    DiscoveryTimeout = TimeSpan.FromSeconds(3),

    ConnectTimeout = TimeSpan.FromSeconds(8),
    HeartbeatTimeout = TimeSpan.FromSeconds(10)
};
```

Important:

- for discovery mode, do **not** set `ServerHost`
- `DiscoveryPort` and `DiscoveryMulticastAddress` must match server config

## Step 2: Create the method that solves the task

The simplest approach is to keep one `Solve` method and adjust it as your assignment evolves.

```csharp
using testprog.messenger;

internal static class StudentSolution
{
    public static object Solve(TestInput input)
    {
        // TODO: adapt this to your assignment
        int a = input.GetInt("a");
        int b = input.GetInt("b");
        return a + b;
    }
}
```

Notes:

- `Solve` returns `object`, so you can return a number, string, or object
- if server expects JSON object output, return for example `new { result = a + b }`

## Step 3: Run tests using `StudentConsoleTestRunner`

Below is a fully copy-paste ready `Program.cs`.

```csharp
using testprog.client;
using testprog.messenger;

namespace MyStudentApp;

internal static class Program
{
    public static int Main(string[] args)
    {
        StudentClientOptions options = new()
        {
            StudentId = "novakj",       // TODO: your ID
            DisplayName = "Jan Novak",  // TODO: your name

            DiscoveryMulticastAddress = "239.0.0.222",
            DiscoveryPort = 15001,
            DiscoveryTimeout = TimeSpan.FromSeconds(3),

            ConnectTimeout = TimeSpan.FromSeconds(8),
            HeartbeatTimeout = TimeSpan.FromSeconds(10)
        };

        return StudentConsoleTestRunner.RunWithExitCode(options, StudentSolution.Solve);
    }
}

internal static class StudentSolution
{
    public static object Solve(TestInput input)
    {
        int a = input.GetInt("a");
        int b = input.GetInt("b");
        return a + b;
    }
}
```

## Step 4: How to read the output

During execution you will see:

- connection established
- test group start
- `PASS`/`FAIL` for each testcase
- final summary

Process exit code:

- `0`: run completed and all testcases passed
- `1`: run did not complete or at least one testcase failed

## Step 5: Most common issues

## `Run was stopped (unauthorized)`

- your `StudentId` is not allowed on the server
- contact your instructor and share the exact `StudentId` value

## `Unable to connect within configured timeout`

- server is not running
- `DiscoveryPort` or `DiscoveryMulticastAddress` does not match
- network blocks UDP multicast

## `invalid-answer`

- your output type/shape does not match expected output
- compare assignment requirements with the structure returned by `Solve`

## Recommended workflow during the semester

- keep your solution in `Solve`, not in `Main`
- use small helper methods, but keep final output format consistent
- when something fails, fix the first failing testcase first, then continue

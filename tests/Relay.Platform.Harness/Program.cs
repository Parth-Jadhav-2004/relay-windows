using Relay.Platform.Harness;

if (AppProcessRegressionTests.TryRunChild(args) is { } appExit)
    return appExit;
if (await OpenCodeRegressionTests.TryRunChildAsync(args) is { } serverExit)
    return serverExit;

var failures = 0;
SettingsStoreRegressionTests.Run(Check);
WindowHookRegressionTests.Run(Check);
failures += await AppProcessRegressionTests.RunAsync();
failures += await OpenCodeRegressionTests.RunAsync();
Console.WriteLine(failures == 0 ? "All platform regressions passed." : $"{failures} platform regressions failed.");
return failures == 0 ? 0 : 1;

void Check(string name, bool passed, string? detail)
{
    if (!passed)
        failures++;
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")}: {name}{(detail is null ? "" : ": " + detail)}");
}

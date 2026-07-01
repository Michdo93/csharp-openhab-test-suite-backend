using System.Reflection;
using System.Text;
using System.Text.Json;
using OpenHABRestClient;
using OpenHABTestSuite;
using OpenHABTestSuiteBackend.Models;

namespace OpenHABTestSuiteBackend.Services;

/// <summary>
/// Builds <see cref="OpenHABClient"/> instances, instantiates tester classes,
/// dispatches method calls via reflection, and captures console output.
/// </summary>
public class TesterDispatcher
{
    // ── Client construction ───────────────────────────────────────────────────

    public OpenHABClient BuildClient(string url, string? username,
                                     string? password, string? token)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("url is required");

        // Ensure an explicit protocol prefix is present.
        // Without it, a bare host like "192.168.0.5:8080" is not a valid
        // absolute URI and HttpClient will throw a UriFormatException.
        var base_ = url.Trim().TrimEnd('/');
        if (!base_.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !base_.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            base_ = "http://" + base_;
        }

        return !string.IsNullOrWhiteSpace(token)
            ? new OpenHABClient(base_, token: token)
            : new OpenHABClient(base_, username, password);
    }

    public OpenHABClient BuildClient(ConnectRequest r) =>
        BuildClient(r.Url, r.Username, r.Password, r.Token);

    public OpenHABClient BuildClient(TestRequest r) =>
        BuildClient(r.Url, r.Username, r.Password, r.Token);

    // ── Tester instantiation ──────────────────────────────────────────────────

    public object BuildTester(string name, OpenHABClient client) => name switch
    {
        "ItemTester"        => new ItemTester(client),
        "ThingTester"       => new ThingTester(client),
        "RuleTester"        => new RuleTester(client),
        "ChannelTester"     => new ChannelTester(client),
        "PersistenceTester" => new PersistenceTester(client),
        "SitemapTester"     => new SitemapTester(client),
        _ => throw new ArgumentException(
            $"Unknown tester '{name}'. Valid: ItemTester, ThingTester, RuleTester, " +
            "ChannelTester, PersistenceTester, SitemapTester")
    };

    // ── Method dispatch ───────────────────────────────────────────────────────

    /// <summary>
    /// Invokes <c>tester.Method(params…)</c> via reflection while
    /// capturing <see cref="Console.Out"/> and <see cref="Console.Error"/>.
    /// </summary>
    public (object? result, string output) Invoke(
        object tester, string methodName, List<object?> rawParams)
    {
        var method = FindMethod(tester.GetType(), methodName, rawParams.Count)
                     ?? throw new MissingMethodException(
                         tester.GetType().Name, methodName);

        var args = Coerce(rawParams, method.GetParameters());

        // ── Capture Console output ────────────────────────────────────────────
        var buf     = new StringBuilder();
        var writer  = new StringWriter(buf);
        var prevOut = Console.Out;
        var prevErr = Console.Error;
        Console.SetOut(writer);
        Console.SetError(writer);

        object? result;
        try   { result = method.Invoke(tester, args); }
        finally
        {
            Console.SetOut(prevOut);
            Console.SetError(prevErr);
            writer.Flush();
        }

        return (result, buf.ToString().Trim());
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static MethodInfo? FindMethod(Type type, string name, int arity)
    {
        // The frontend sends camelCase names (doesItemExist, testSwitch) to
        // match the Python/Java/Node naming convention, but C# methods are
        // PascalCase (DoesItemExist, TestSwitch). Convert automatically.
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];

        // Try PascalCase first (exact arity), then camelCase as fallback
        foreach (var candidate in new[] { pascal, name })
        {
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                if (m.Name == candidate && m.GetParameters().Length == arity) return m;
        }
        // Then methods with optional parameters covering the arity
        foreach (var candidate in new[] { pascal, name })
        {
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                if (m.Name == candidate && m.GetParameters().Length >= arity) return m;
        }
        return null;
    }

    private static object?[] Coerce(List<object?> raw, ParameterInfo[] paramInfos)
    {
        var result = new object?[paramInfos.Length];
        for (int i = 0; i < paramInfos.Length; i++)
        {
            var val    = i < raw.Count ? raw[i] : null;
            var target = paramInfos[i].ParameterType;
            result[i]  = CoerceOne(val, target);
        }
        return result;
    }

    private static object? CoerceOne(object? val, Type target)
    {
        if (val is null)
            return target.IsValueType ? Activator.CreateInstance(target) : null;

        // JsonElement from System.Text.Json deserialization
        if (val is JsonElement je)
        {
            if (target == typeof(string))
                return je.ValueKind == JsonValueKind.Null ? null : je.ToString();
            if (target == typeof(bool)   || target == typeof(Nullable<bool>))
                return je.ValueKind == JsonValueKind.True;
            if (target == typeof(int)    || target == typeof(Nullable<int>))
                return je.TryGetInt32(out var i32) ? i32 : 0;
            if (target == typeof(long)   || target == typeof(Nullable<long>))
                return je.TryGetInt64(out var i64) ? i64 : 0L;
            if (target == typeof(double) || target == typeof(Nullable<double>))
                return je.TryGetDouble(out var d) ? d : 0.0;
            return je.ToString();
        }

        // Primitive conversions
        if (target == typeof(string))
            return val.ToString();
        if (target == typeof(bool)    || target == typeof(Nullable<bool>))
            return val is bool b ? b : bool.Parse(val.ToString()!);
        if (target == typeof(int)     || target == typeof(Nullable<int>))
            return val is int i ? i : Convert.ToInt32(val);
        if (target == typeof(long)    || target == typeof(Nullable<long>))
            return val is long l ? l : Convert.ToInt64(val);
        if (target == typeof(double)  || target == typeof(Nullable<double>))
            return val is double d ? d : Convert.ToDouble(val);

        return val;
    }
}
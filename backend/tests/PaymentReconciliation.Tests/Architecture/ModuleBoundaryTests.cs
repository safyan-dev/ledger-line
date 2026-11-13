using Xunit;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PaymentReconciliation.Tests.Architecture;

public sealed class ModuleBoundaryTests
{
    private static readonly Dictionary<string, string[]> AllowedReferences = new(StringComparer.Ordinal)
    {
        ["Identity"] = ["SharedKernel", "Persistence.Abstractions"],
        ["Tenancy"] = ["SharedKernel", "Persistence.Abstractions", "Identity"],
        ["Orders"] = ["SharedKernel", "Persistence.Abstractions"],
        ["Payments"] = ["SharedKernel", "Persistence.Abstractions", "Orders"],
        ["Webhooks"] = ["SharedKernel", "Persistence.Abstractions", "Payments"],
        ["Reconciliation"] = ["SharedKernel", "Persistence.Abstractions", "Orders", "Payments"],
        ["Auditing"] = ["SharedKernel", "Persistence.Abstractions", "Tenancy", "Orders", "Payments", "Webhooks", "Reconciliation"],
        ["Reporting"] = ["SharedKernel", "Persistence.Abstractions", "Orders", "Payments", "Webhooks", "Reconciliation", "Auditing"]
    };

    private static readonly Dictionary<string, string> EntityOwners = new(StringComparer.Ordinal)
    {
        ["Order"] = "Orders",
        ["Payment"] = "Payments",
        ["Refund"] = "Payments",
        ["WebhookEvent"] = "Webhooks",
        ["ReconciliationRecord"] = "Reconciliation",
        ["AuditLog"] = "Auditing",
        ["Tenant"] = "Tenancy",
        ["TenantUser"] = "Tenancy"
    };

    [Fact]
    public void Modules_reference_only_allowed_projects()
    {
        var root = BackendRoot();
        var violations = new List<string>();

        foreach (var (module, allowed) in AllowedReferences)
        {
            var projectFile = Path.Combine(root, "src", "Modules", module, $"{module}.csproj");
            Assert.True(File.Exists(projectFile), $"Missing project file for {module}.");

            var references = XDocument.Load(projectFile)
                .Descendants("ProjectReference")
                .Select(element => Path.GetFileNameWithoutExtension((string)element.Attribute("Include")!))
                .ToList();

            var unexpected = references.Except(allowed, StringComparer.Ordinal).ToList();
            var forbidden = references
                .Where(reference => reference is "Persistence" or "Api")
                .ToList();

            if (unexpected.Count > 0)
            {
                violations.Add($"{module} -> {string.Join(", ", unexpected)}");
            }

            if (forbidden.Count > 0)
            {
                violations.Add($"{module} must not reference {string.Join(", ", forbidden)}");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Unexpected module references:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void Identity_never_references_tenancy()
    {
        var projectFile = Path.Combine(BackendRoot(), "src", "Modules", "Identity", "Identity.csproj");
        var references = XDocument.Load(projectFile)
            .Descendants("ProjectReference")
            .Select(element => (string)element.Attribute("Include")!)
            .ToList();

        Assert.DoesNotContain(references, reference => reference.Contains("/Tenancy/", StringComparison.Ordinal));
    }

    [Fact]
    public void Modules_use_only_other_modules_contracts_namespaces()
    {
        var root = BackendRoot();
        var usingPattern = new Regex(
            @"using\s+PaymentReconciliation\.Modules\.(?<module>[A-Za-z]+)\.(?<namespace>[A-Za-z.]+)\s*;",
            RegexOptions.Compiled);
        var violations = new List<string>();

        foreach (var module in AllowedReferences.Keys)
        {
            var moduleDirectory = Path.Combine(root, "src", "Modules", module);

            foreach (var file in SourceFiles(moduleDirectory))
            {
                foreach (Match match in usingPattern.Matches(File.ReadAllText(file)))
                {
                    var referencedModule = match.Groups["module"].Value;
                    var referencedNamespace = match.Groups["namespace"].Value;

                    if (string.Equals(referencedModule, module, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!referencedNamespace.StartsWith("Contracts", StringComparison.Ordinal))
                    {
                        violations.Add($"{module}: {Path.GetFileName(file)} uses {referencedModule}.{referencedNamespace}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Cross-module namespace violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void Modules_do_not_read_other_modules_tables()
    {
        var root = BackendRoot();
        var setPattern = new Regex(@"Set<(?<entity>[A-Za-z]+)>\(", RegexOptions.Compiled);
        var violations = new List<string>();

        foreach (var module in AllowedReferences.Keys)
        {
            var moduleDirectory = Path.Combine(root, "src", "Modules", module);

            foreach (var file in SourceFiles(moduleDirectory))
            {
                foreach (Match match in setPattern.Matches(File.ReadAllText(file)))
                {
                    var entity = match.Groups["entity"].Value;
                    if (!EntityOwners.TryGetValue(entity, out var owner) || owner == module)
                    {
                        continue;
                    }

                    violations.Add($"{module}: {Path.GetFileName(file)} reads Set<{entity}> owned by {owner}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Cross-module table access:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    private static IEnumerable<string> SourceFiles(string directory)
        => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string BackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Modules")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the backend root from the test output directory.");
    }
}

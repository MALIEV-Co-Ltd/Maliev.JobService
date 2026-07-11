using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Maliev.JobService.Tests.Deployment;

/// <summary>
/// Guards the deterministic shared-library and credential-free pull-request build boundaries.
/// </summary>
public sealed class DeploymentDependencyPinTests
{
    /// <summary>
    /// Verifies every package-mode boundary uses the reviewed ServiceDefaults and MessagingContracts releases.
    /// </summary>
    [Fact]
    public void DeploymentBoundaries_PinVerifiedSharedPackageVersions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var buildProperties = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"));
        var propertyGroup = Assert.Single(buildProperties.Root!.Elements("PropertyGroup"));

        Assert.Equal("1.0.81-alpha", propertyGroup.Element("ServiceDefaultsVersion")?.Value);
        Assert.Equal("1.0.91-alpha", propertyGroup.Element("MessagingContractsVersion")?.Value);
        Assert.Null(propertyGroup.Element("SharedLibraryVersion"));

        foreach (var projectFile in Directory.EnumerateFiles(
                     repositoryRoot,
                     "*.csproj",
                     SearchOption.AllDirectories)
                 .Where(path => !path.Contains(
                     $"{Path.DirectorySeparatorChar}.ci-sources{Path.DirectorySeparatorChar}",
                     StringComparison.OrdinalIgnoreCase)))
        {
            var project = XDocument.Load(projectFile);
            foreach (var packageReference in project.Descendants("PackageReference"))
            {
                var packageName = packageReference.Attribute("Include")?.Value;
                var expectedProperty = packageName switch
                {
                    "Maliev.Aspire.ServiceDefaults" => "$(ServiceDefaultsVersion)",
                    "Maliev.MessagingContracts" => "$(MessagingContractsVersion)",
                    _ => null
                };

                if (expectedProperty is not null)
                {
                    Assert.Equal(expectedProperty, packageReference.Attribute("Version")?.Value);
                }
            }
        }

        var dockerfile = File.ReadAllText(
            Path.Combine(repositoryRoot, "Maliev.JobService.Api", "Dockerfile"));
        Assert.Contains("ARG SERVICE_DEFAULTS_VERSION=1.0.81-alpha", dockerfile, StringComparison.Ordinal);
        Assert.Contains("ARG MESSAGING_CONTRACTS_VERSION=1.0.91-alpha", dockerfile, StringComparison.Ordinal);
        Assert.Contains("ENV GITHUB_ACTIONS=true", dockerfile, StringComparison.Ordinal);
        Assert.Contains("ARG dependency_restore_stage=restore-private", dockerfile, StringComparison.Ordinal);
        Assert.Contains("FROM build-base AS restore-local", dockerfile, StringComparison.Ordinal);

        foreach (var workflowName in new[]
                 {
                     "_build-and-test.yml",
                     "ci-develop.yml",
                     "ci-main.yml",
                     "ci-staging.yml",
                     "pr-validation.yml"
                 })
        {
            var workflow = File.ReadAllText(
                Path.Combine(repositoryRoot, ".github", "workflows", workflowName));
            Assert.Contains("ServiceDefaultsVersion: 1.0.81-alpha", workflow, StringComparison.Ordinal);
            Assert.Contains("MessagingContractsVersion: 1.0.91-alpha", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("SharedLibraryVersion", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("1.0.*", workflow, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Verifies untrusted pull requests rebuild exact public dependencies without receiving credentials.
    /// </summary>
    [Fact]
    public void PullRequestValidation_ReconstructsExactPackagesWithoutSecrets()
    {
        var repositoryRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "pr-validation.yml");
        var ciNuGetConfigPath = Path.Combine(repositoryRoot, "NuGet.PRValidation.Config");
        var packageScriptPath = Path.Combine(repositoryRoot, "scripts", "prepare-job-ci-packages.sh");

        Assert.True(File.Exists(ciNuGetConfigPath), "Expected a credential-free PR validation NuGet configuration.");
        Assert.True(File.Exists(packageScriptPath), "Expected an exact dependency package reconstruction script.");

        var workflow = File.ReadAllText(workflowPath);
        var ciNuGetConfig = File.ReadAllText(ciNuGetConfigPath);
        var packageScript = File.ReadAllText(packageScriptPath);
        var productionNuGetConfig = File.ReadAllText(Path.Combine(repositoryRoot, "nuget.config"));

        Assert.Contains("pull_request:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("pull_request_target", workflow, StringComparison.Ordinal);
        Assert.Contains("permissions:", workflow, StringComparison.Ordinal);
        Assert.Contains("contents: read", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("packages: read", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("GITOPS_PAT", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NUGET_USERNAME", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NUGET_PASSWORD", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("_build-and-test.yml", workflow, StringComparison.Ordinal);
        Assert.Contains("concurrency:", workflow, StringComparison.Ordinal);
        Assert.Contains("cancel-in-progress: true", workflow, StringComparison.Ordinal);

        Assert.Contains("repository: MALIEV-Co-Ltd/Maliev.MessagingContracts", workflow, StringComparison.Ordinal);
        Assert.Contains("ref: 0bcd4c704d842211c5ff9bd6b9c4b3aacfcbd8e7", workflow, StringComparison.Ordinal);
        Assert.Contains("repository: MALIEV-Co-Ltd/Maliev.Aspire", workflow, StringComparison.Ordinal);
        Assert.Contains("ref: 7121d57705fc1eff6c7ebb6a69e33e9c26ebfccc", workflow, StringComparison.Ordinal);
        Assert.Contains("persist-credentials: false", workflow, StringComparison.Ordinal);
        Assert.Contains("prepare-job-ci-packages.sh", workflow, StringComparison.Ordinal);
        Assert.Contains("github.run_id", workflow, StringComparison.Ordinal);
        Assert.Contains("github.run_attempt", workflow, StringComparison.Ordinal);
        Assert.Contains("artifact-digest", workflow, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS", workflow, StringComparison.Ordinal);
        Assert.Contains("sha256sum --check", workflow, StringComparison.Ordinal);
        Assert.Contains("overwrite: true", workflow, StringComparison.Ordinal);
        Assert.Contains("retention-days: 1", workflow, StringComparison.Ordinal);

        Assert.Contains("--configfile NuGet.PRValidation.Config", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet build Maliev.JobService.slnx --configuration Release --no-restore", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test Maliev.JobService.slnx --configuration Release --no-build --no-restore", workflow, StringComparison.Ordinal);
        Assert.Contains("dependency_restore_stage=restore-local", workflow, StringComparison.Ordinal);
        Assert.Contains("push: false", workflow, StringComparison.Ordinal);
        Assert.Contains("load: true", workflow, StringComparison.Ordinal);
        Assert.Contains("Smoke test production image liveness", workflow, StringComparison.Ordinal);
        Assert.Contains("postgres:18-alpine", workflow, StringComparison.Ordinal);
        Assert.Contains("trap cleanup EXIT", workflow, StringComparison.Ordinal);
        Assert.Contains("for attempt in {1..60}", workflow, StringComparison.Ordinal);
        Assert.Contains("curl --fail --silent \"http://127.0.0.1:18080/job/liveness\"", workflow, StringComparison.Ordinal);
        Assert.Contains("format: cyclonedx", workflow, StringComparison.Ordinal);
        Assert.Contains("severity: HIGH,CRITICAL", workflow, StringComparison.Ordinal);
        Assert.Contains("exit-code: \"1\"", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("docker/login-action", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gcloud", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("argocd", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kubectl", workflow, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("nuget.pkg.github.com", ciNuGetConfig, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("packageSourceCredentials", ciNuGetConfig, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<packageSourceMapping>", ciNuGetConfig, StringComparison.Ordinal);
        Assert.Contains("<add key=\"maliev-ci\" value=\".ci-packages\" />", ciNuGetConfig, StringComparison.Ordinal);
        Assert.Contains("<package pattern=\"Maliev.*\" />", ciNuGetConfig, StringComparison.Ordinal);
        Assert.Contains("<packageSourceMapping>", productionNuGetConfig, StringComparison.Ordinal);
        Assert.Contains("<packageSource key=\"github\">", productionNuGetConfig, StringComparison.Ordinal);
        Assert.Contains("<package pattern=\"Maliev.*\" />", productionNuGetConfig, StringComparison.Ordinal);

        Assert.Contains("messaging_commit=\"0bcd4c704d842211c5ff9bd6b9c4b3aacfcbd8e7\"", packageScript, StringComparison.Ordinal);
        Assert.Contains("aspire_commit=\"7121d57705fc1eff6c7ebb6a69e33e9c26ebfccc\"", packageScript, StringComparison.Ordinal);
        Assert.Contains("messaging_version=\"1.0.91-alpha\"", packageScript, StringComparison.Ordinal);
        Assert.Contains("service_defaults_version=\"1.0.81-alpha\"", packageScript, StringComparison.Ordinal);
        Assert.Contains("dotnet restore \"$generator_project\" --configfile \"$ci_nuget_config\"", packageScript, StringComparison.Ordinal);
        Assert.Contains("dotnet run --project tools/Generator/Generator.csproj --configuration Release --no-restore", packageScript, StringComparison.Ordinal);
        Assert.Contains("sha256sum", packageScript, StringComparison.Ordinal);
        Assert.DoesNotContain("--source", packageScript, StringComparison.Ordinal);

        Assert.DoesNotContain(
            "HEALTHCHECK",
            File.ReadAllText(Path.Combine(repositoryRoot, "Maliev.JobService.Api", "Dockerfile")),
            StringComparison.OrdinalIgnoreCase);

        var unpinnedActions = Regex.Matches(
            workflow,
            @"uses:\s+[^\s@]+@(?![0-9a-f]{40}(?:\s|$))[^\s]+",
            RegexOptions.CultureInvariant);
        Assert.Empty(unpinnedActions.Select(match => match.Value));
    }

    private static string FindRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "Maliev.JobService.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the JobService repository root.");
    }
}

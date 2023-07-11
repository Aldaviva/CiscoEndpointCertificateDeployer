using System.Reflection;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using McMaster.Extensions.CommandLineUtils;
using Octokit;

namespace CiscoEndpointDocumentationReleaseIssueCreator;

public class GithubIssueCreator {

    private const string REPOSITORY_OWNER = "Aldaviva";
    private const string REPOSITORY_NAME  = "CiscoEndpointCertificateDeployer";
    private const string ISSUE_LABEL      = "upstream update";

    private static readonly AssemblyName ASSEMBLY = Assembly.GetExecutingAssembly().GetName();
    private static readonly Url DOCUMENTATION_LIST_PAGE_LOCATION = new("https://www.cisco.com/c/en/us/support/collaboration-endpoints/spark-room-kit-series/products-command-reference-list.html");

    private readonly IBrowsingContext anglesharp = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
    private readonly IGitHubClient    gitHubClient;

    public static async Task<int> Main(string[] args) {
        CommandLineApplication argumentParser = new();
        CommandOption<string> gitHubAccessTokenOption =
            argumentParser.Option<string>("--github-access-token", $"Token with repo scope access to {REPOSITORY_OWNER}/{REPOSITORY_NAME}", CommandOptionType.SingleValue);
        argumentParser.Parse(args);
        if (gitHubAccessTokenOption.Value() is not { } gitHubAccessToken) {
            Console.WriteLine($"Usage: {Path.GetFileName(Environment.ProcessPath)} --{gitHubAccessTokenOption.LongName} XXXXXXXXX");
            return 1;
        }

        await new GithubIssueCreator(gitHubAccessToken).createIssueIfMissing();
        return 0;
    }

    public GithubIssueCreator(string gitHubAccessToken) {
        gitHubClient = new GitHubClient(new ProductHeaderValue(ASSEMBLY.Name!, ASSEMBLY.Version!.ToString(3))) { Credentials = new Credentials(gitHubAccessToken) };
    }

    public async Task createIssueIfMissing() {
        Console.WriteLine("Checking version of latest documented release...");
        PublishedDocumentation latestDocumentation = await findLatestDocumentation();
        Console.WriteLine($"Latest documentation version: {latestDocumentation.name}");

        if (await findIssueForLatestDocumentation(latestDocumentation) is not { } existingIssue) {
            Console.WriteLine("No existing issues found, so creating a new issue...");
            Issue newIssue = await createIssue(latestDocumentation);
            Console.WriteLine($"Created issue #{newIssue.Number}: {newIssue.Title}");
        } else {
            Console.WriteLine($"GitHub issue #{existingIssue.Number} already exists, so not creating a new issue");
        }
    }

    private async Task<PublishedDocumentation> findLatestDocumentation() {
        using IDocument listPage = await anglesharp.OpenAsync(DOCUMENTATION_LIST_PAGE_LOCATION);

        IHtmlAnchorElement latestDocumentationLink = listPage.QuerySelectorAll(".heading")
            .First(element => element.Text().StartsWith("Cisco "))
            .NextElementSibling!
            .QuerySelector<IHtmlAnchorElement>("a")!;

        string releaseName = Regex.Match(latestDocumentationLink.Text, @"\((?<name>.*?)\)").Groups["name"].Value;

        return new PublishedDocumentation(releaseName, new Uri(latestDocumentationLink.Href));
    }

    private async Task<Issue?> findIssueForLatestDocumentation(PublishedDocumentation documentation) {
        IReadOnlyList<Issue> upstreamUpdateIssues = await gitHubClient.Issue.GetAllForRepository(REPOSITORY_OWNER, REPOSITORY_NAME, new RepositoryIssueRequest { Labels = { ISSUE_LABEL } });
        string               expectedIssueTitle   = getIssueTitle(documentation.name);
        return upstreamUpdateIssues.FirstOrDefault(issue => issue.Title == expectedIssueTitle);
    }

    private async Task<Issue> createIssue(PublishedDocumentation documentation) {
        return await gitHubClient.Issue.Create(REPOSITORY_OWNER, REPOSITORY_NAME, new NewIssue(getIssueTitle(documentation.name)) {
            Assignees = { REPOSITORY_OWNER },
            Labels    = { ISSUE_LABEL },
            Body = $"""
            Cisco has released documentation for a new on-premises endpoint software release.

            [**{documentation.name}** (PDF)]({documentation.location})
            [All command reference guides]({DOCUMENTATION_LIST_PAGE_LOCATION})
            """
        });
    }

    private static string getIssueTitle(string releaseName) => $"Update for {releaseName}";

}
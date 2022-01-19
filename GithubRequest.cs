namespace GithubApiProxy;

public record GitHubRequest(string url, string method, string payload);
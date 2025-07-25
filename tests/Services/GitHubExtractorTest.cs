// Project Name: DataModelerTests
// File Name: GitHubExtractorTest.cs
// Author:  Kyle Crowder
// Github:  OldSkoolzRoolz
// Distributed under Open Source License
// Do not remove file headers




using System.Reflection;

using CopilotModeler.Services;

using JetBrains.Annotations;

using Microsoft.Extensions.Logging;

using Moq;

using Octokit;



namespace DataModelerTests.Services;


[TestClass]
[TestSubject(typeof(GitHubExtractor))]
public class GitHubExtractorTest
{

    private GitHubExtractor? _gitHubExtractor;
    private Mock<GitHubClient>? _mockGitHubClient;
    private Mock<ILogger<GitHubExtractor>>? _mockLogger;






    [TestMethod]
    [DataRow(null)]
    public async Task ExtractDefaultBranchProjectAsync_NullRepository_ThrowsArgumentNullException(Repository repo)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _gitHubExtractor!.ExtractDefaultBranchProjectAsync(repo));
    }






    [TestMethod]
    public void GetTimeDifference_ValidTimes_ReturnsCorrectDifference()
    {
        // Arrange
        var startTime = DateTime.Now;
        var endTime = startTime.AddMinutes(5);

        // Act
        var method = typeof(GitHubExtractor).GetMethod("GetTimeDifference", BindingFlags.NonPublic | BindingFlags.Instance);
        var result = (TimeSpan)method!.Invoke(_gitHubExtractor!, new object[] { startTime, endTime });

        // Assert
        Assert.AreEqual(TimeSpan.FromMinutes(5), result);
    }






    [TestMethod]
    public async Task SearchCsharpRepositoriesAsync_ValidSearch_ReturnsRepositories()
    {
        // Arrange
        var repoMock = new Mock<Repository>();
        repoMock.SetupGet(r => r.FullName).Returns("owner/repo");
        var repo = repoMock.Object;

        var repoList = new List<Repository> { repo };
        var searchResultMock = new Mock<SearchRepositoryResult>(1, true, repoList);
        searchResultMock.SetupGet(s => s.Items).Returns(repoList);
        var searchResult = searchResultMock.Object;

        _mockGitHubClient!.Setup(client => client.Search.SearchRepo(It.IsAny<SearchRepositoriesRequest>())).ReturnsAsync(searchResult);

        // Act
        var result = await _gitHubExtractor!.SearchCsharpRepositoriesAsync();

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("owner/repo", result[0].FullName);
    }






    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<GitHubExtractor>>();
        _mockGitHubClient = new Mock<GitHubClient>(new ProductHeaderValue("AICodingHelper"));
        _gitHubExtractor = new GitHubExtractor("fake-token", _mockLogger.Object);
    }

}

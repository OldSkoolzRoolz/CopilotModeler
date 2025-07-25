// Project Name: DataModelerTests
// File Name: DataMiningTest.cs
// Author:  Kyle Crowder
// Github:  OldSkoolzRoolz
// Distributed under Open Source License
// Do not remove file headers




using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using CopilotModeler.DataExtraction;

using JetBrains.Annotations;



namespace DataModelerTests.DataExtraction;


[TestClass]
[TestSubject(typeof(DataMining))]
public class DataMiningTest
{



    [TestMethod]
    public void CalculateSha256Hash_ValidInput_ReturnsHash()
    {
        // Arrange
        var input = "TestString";
        var expectedHash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
        var expectedHashString = string.Concat(expectedHash.Select(b => b.ToString("x2")));

        // Act
        var result = typeof(DataMining).GetMethod("CalculateSha256Hash", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, new object[] { input }) as string;

        // Assert
        Assert.AreEqual(expectedHashString, result);
    }






    [TestMethod]
    public void CreateDocumentFromCode_ValidCode_ReturnsDocument()
    {
        // Arrange
        var code = "public class TestClass { }";
        var fileName = "TestClass.cs";

        // Act
        var document = DataMining.CreateDocumentFromCode(code, fileName);

        // Assert
        Assert.IsNotNull(document);
        Assert.AreEqual(fileName, document.Name);
    }






    [TestMethod]
    public void LoadSolutionFile_ValidSolutionPath_ReturnsSolution()
    {
        // Arrange
        var solutionPath = "path/to/solution.sln";

        // Act
        var solution = DataMining.LoadSolutionFile(solutionPath);

        // Assert
        Assert.IsNotNull(solution);
    }

}

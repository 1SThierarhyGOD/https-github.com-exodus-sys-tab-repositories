using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using SharpYaml.Tokens;

public partial class OpenApiDocumentServiceTests
{
    [Fact]
    public async Task GetOpenApiOperation_CapturesSummary()
    {
        // Arrange
        var builder = CreateBuilder();
        var summary = "Get all todos";

        // Act
        builder.MapGet("/api/todos", () => { }).WithSummary(summary);

        // Assert
        await VerifyOpenApiDocument(builder, document =>
        {
            var operation = document.Paths["/api/todos"].Operations[OperationType.Get];
            Assert.Equal(summary, operation.Summary);
        });
    }

    [Fact]
    public async Task GetOpenApiOperation_CapturesDescription()
    {
        // Arrange
        var builder = CreateBuilder();
        var description = "Returns all the todos provided in an array.";

        // Act
        builder.MapGet("/api/todos", () => { }).WithDescription(description);

        // Assert
        await VerifyOpenApiDocument(builder, document =>
        {
            var operation = document.Paths["/api/todos"].Operations[OperationType.Get];
            Assert.Equal(description, operation.Description);
        });
    }

    [Fact]
    public async Task GetOpenApiOperation_CapturesTags()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        builder.MapGet("/api/todos", () => { }).WithTags(["todos", "v1"]);

        // Assert
        await VerifyOpenApiDocument(builder, document =>
        {
            var operation = document.Paths["/api/todos"].Operations[OperationType.Get];
            Assert.Collection(operation.Tags, tag =>
            {
                Assert.Equal("todos", tag.Name);
            },
            tag =>
            {
                Assert.Equal("v1", tag.Name);
            });
        });
    }

    [Fact]
    public async Task GetOpenApiOperation_CapturesTagsInDocument()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        builder.MapGet("/api/todos", () => { }).WithTags(["todos", "v1"]);
        builder.MapGet("/api/users", () => { }).WithTags(["users", "v1"]);

        // Assert
        await VerifyOpenApiDocument(builder, document =>
        {
            Assert.Collection(document.Tags, tag =>
            {
                Assert.Equal("todos", tag.Name);
            },
            tag =>
            {
                Assert.Equal("v1", tag.Name);
            },
            tag =>
            {
                Assert.Equal("users", tag.Name);
            });
        });
    }
}

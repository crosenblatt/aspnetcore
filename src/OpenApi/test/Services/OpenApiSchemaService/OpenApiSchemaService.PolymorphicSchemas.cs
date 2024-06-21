// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

public partial class OpenApiSchemaServiceTests : OpenApiDocumentServiceTestBase
{
    [Fact]
    public async Task HandlesPolymorphicTypeWithMappingsAndStringDiscriminator()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        builder.MapPost("/api", (Shape shape) => { });

        // Assert
        await VerifyOpenApiDocument(builder, document =>
        {
            var operation = document.Paths["/api"].Operations[OperationType.Post];
            Assert.NotNull(operation.RequestBody);
            var requestBody = operation.RequestBody.Content;
            Assert.True(requestBody.TryGetValue("application/json", out var mediaType));
            var schema = mediaType.Schema;
            // Assert discriminator mappings have been configured correctly
            Assert.Equal("$type", schema.Discriminator.PropertyName);
            Assert.Contains(schema.Discriminator.PropertyName, schema.Required);
            Assert.Collection(schema.Discriminator.Mapping,
                item => Assert.Equal("triangle", item.Key),
                item => Assert.Equal("square", item.Key)
            );
            Assert.Collection(schema.Discriminator.Mapping,
                item => Assert.Equal("#/components/schemas/ShapeTriangle", item.Value),
                item => Assert.Equal("#/components/schemas/ShapeSquare", item.Value)
            );
            // Assert the schemas with the discriminator have been inserted into the components
            Assert.True(document.Components.Schemas.TryGetValue("ShapeTriangle", out var triangleSchema));
            Assert.Contains(schema.Discriminator.PropertyName, triangleSchema.Properties.Keys);
            Assert.Equal("triangle", ((OpenApiString)triangleSchema.Properties[schema.Discriminator.PropertyName].Enum.First()).Value);
            Assert.True(document.Components.Schemas.TryGetValue("ShapeSquare", out var squareSchema));
            Assert.Equal("square", ((OpenApiString)squareSchema.Properties[schema.Discriminator.PropertyName].Enum.First()).Value);
        });
    }

    [Fact]
    public async Task HandlesPolymorphicTypeWithMappingsAndIntegerDiscriminator()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        builder.MapPost("/api", (WeatherForecastBase forecast) => { });

        // Assert
        await VerifyOpenApiDocument(builder, document =>
        {
            var operation = document.Paths["/api"].Operations[OperationType.Post];
            Assert.NotNull(operation.RequestBody);
            var requestBody = operation.RequestBody.Content;
            Assert.True(requestBody.TryGetValue("application/json", out var mediaType));
            var schema = mediaType.Schema;
            // Assert discriminator mappings have been configured correctly
            Assert.Equal("$type", schema.Discriminator.PropertyName);
            Assert.Contains(schema.Discriminator.PropertyName, schema.Required);
            Assert.Collection(schema.Discriminator.Mapping,
                item => Assert.Equal("0", item.Key),
                item => Assert.Equal("1", item.Key),
                item => Assert.Equal("2", item.Key)
            );
            Assert.Collection(schema.Discriminator.Mapping,
                item => Assert.Equal("#/components/schemas/WeatherForecastBaseWeatherForecastWithCity", item.Value),
                item => Assert.Equal("#/components/schemas/WeatherForecastBaseWeatherForecastWithTimeSeries", item.Value),
                item => Assert.Equal("#/components/schemas/WeatherForecastBaseWeatherForecastWithLocalNews", item.Value)
            );
            // Assert schema with discriminator = 0 has been inserted into the components
            Assert.True(document.Components.Schemas.TryGetValue("WeatherForecastBaseWeatherForecastWithCity", out var citySchema));
            Assert.Contains(schema.Discriminator.PropertyName, citySchema.Properties.Keys);
            Assert.Equal(0, ((OpenApiInteger)citySchema.Properties[schema.Discriminator.PropertyName].Enum.First()).Value);
            // Assert schema with discriminator = 1 has been inserted into the components
            Assert.True(document.Components.Schemas.TryGetValue("WeatherForecastBaseWeatherForecastWithTimeSeries", out var timeSeriesSchema));
            Assert.Contains(schema.Discriminator.PropertyName, timeSeriesSchema.Properties.Keys);
            Assert.Equal(1, ((OpenApiInteger)timeSeriesSchema.Properties[schema.Discriminator.PropertyName].Enum.First()).Value);
            // Assert schema with discriminator = 2 has been inserted into the components
            Assert.True(document.Components.Schemas.TryGetValue("WeatherForecastBaseWeatherForecastWithLocalNews", out var newsSchema));
            Assert.Contains(schema.Discriminator.PropertyName, newsSchema.Properties.Keys);
            Assert.Equal(2, ((OpenApiInteger)newsSchema.Properties[schema.Discriminator.PropertyName].Enum.First()).Value);
        });
    }

    [Fact]
    public async Task HandlesPolymorphicTypesWithCustomPropertyName()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        builder.MapPost("/api", (Person person) => { });

        // Assert
        await VerifyOpenApiDocument(builder, document =>
        {
            var operation = document.Paths["/api"].Operations[OperationType.Post];
            Assert.NotNull(operation.RequestBody);
            var requestBody = operation.RequestBody.Content;
            Assert.True(requestBody.TryGetValue("application/json", out var mediaType));
            var schema = mediaType.Schema;
            // Assert discriminator mappings have been configured correctly
            Assert.Equal("discriminator", schema.Discriminator.PropertyName);
            Assert.Contains(schema.Discriminator.PropertyName, schema.Required);
            Assert.Collection(schema.Discriminator.Mapping,
                item => Assert.Equal("student", item.Key),
                item => Assert.Equal("teacher", item.Key)
            );
            Assert.Collection(schema.Discriminator.Mapping,
                item => Assert.Equal("#/components/schemas/PersonStudent", item.Value),
                item => Assert.Equal("#/components/schemas/PersonTeacher", item.Value)
            );
            // Assert schema with discriminator = 0 has been inserted into the components
            Assert.True(document.Components.Schemas.TryGetValue("PersonStudent", out var citySchema));
            Assert.Contains(schema.Discriminator.PropertyName, citySchema.Properties.Keys);
            Assert.Equal("student", ((OpenApiString)citySchema.Properties[schema.Discriminator.PropertyName].Enum.First()).Value);
            // Assert schema with discriminator = 1 has been inserted into the components
            Assert.True(document.Components.Schemas.TryGetValue("PersonTeacher", out var timeSeriesSchema));
            Assert.Contains(schema.Discriminator.PropertyName, timeSeriesSchema.Properties.Keys);
            Assert.Equal("teacher", ((OpenApiString)timeSeriesSchema.Properties[schema.Discriminator.PropertyName].Enum.First()).Value);
        });
    }
}

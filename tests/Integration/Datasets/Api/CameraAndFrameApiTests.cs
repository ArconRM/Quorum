using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Datasets.IntegrationTests.Infrastructure;

namespace Datasets.IntegrationTests.Api;

[Trait("Category", "Integration")]
public sealed class CameraAndFrameApiTests(DatasetsApiFixture fixture) : IClassFixture<DatasetsApiFixture>
{
    private HttpClient Client(string? token = null)
    {
        var client = fixture.Factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> CreateCamera(HttpClient operatorClient, int minFrames = 2)
    {
        var response = await operatorClient.PostAsJsonAsync("/cameras", new
        {
            name = "Въезд, камера 1", task = "Detection", prompt = "license plate", minFramesPerCycle = minFrames, cycleIntervalHours = 24,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static MultipartFormDataContent Image(byte[] bytes, string contentType = "image/jpeg")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", "frame.jpg" } };
    }

    [Fact]
    public async Task Anonymous_request_is_unauthorized() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/cameras")).StatusCode);

    [Fact]
    public async Task Annotator_cannot_create_camera()
    {
        var response = await Client(TestTokens.For("Annotator")).PostAsJsonAsync("/cameras", new
        {
            name = "X", task = "Ocr", prompt = "read", minFramesPerCycle = 1, cycleIntervalHours = 1,
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_camera_returns_validation_problem()
    {
        var response = await Client(TestTokens.For("Operator")).PostAsJsonAsync("/cameras", new
        {
            name = "X", task = "Segmentation", prompt = "", minFramesPerCycle = 0, cycleIntervalHours = 1,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Uploaded_frames_are_counted_as_new_and_downloadable()
    {
        var op = Client(TestTokens.For("Operator"));
        var cameraId = await CreateCamera(op);
        var bytes = Enumerable.Range(0, 2048).Select(i => (byte)(i % 251)).ToArray();

        var upload = await op.PostAsync($"/cameras/{cameraId}/frames", Image(bytes));
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var frameId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await op.PostAsync($"/cameras/{cameraId}/frames", Image(bytes));

        var camera = await op.GetFromJsonAsync<JsonElement>($"/cameras/{cameraId}");
        Assert.Equal(2, camera.GetProperty("newFrames").GetInt32());

        var content = await Client(TestTokens.For("Annotator")).GetByteArrayAsync($"/frames/{frameId}/content");
        Assert.Equal(bytes, content);
    }

    [Fact]
    public async Task Non_image_upload_is_rejected()
    {
        var op = Client(TestTokens.For("Operator"));
        var cameraId = await CreateCamera(op);
        var response = await op.PostAsync($"/cameras/{cameraId}/frames", Image([1, 2, 3], "application/pdf"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_camera_does_not_accept_frames()
    {
        var op = Client(TestTokens.For("Operator"));
        var cameraId = await CreateCamera(op);
        await op.PatchAsJsonAsync($"/cameras/{cameraId}", new { isActive = false });

        var response = await op.PostAsync($"/cameras/{cameraId}/frames", Image([1, 2, 3]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_is_available() =>
        Assert.Equal(HttpStatusCode.OK, (await Client().GetAsync("/openapi/v1.json")).StatusCode);
}

using System.Net;
using System.Net.Http;
using System.Text;

namespace ColumnPadStudio.SmokeTests;

internal sealed class StaticJsonResponseHandler : HttpMessageHandler
{
    private readonly string _json;
    private readonly HttpStatusCode _statusCode;

    public StaticJsonResponseHandler(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _json = json;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_json, Encoding.UTF8, "application/json"),
            RequestMessage = request
        };

        return Task.FromResult(response);
    }
}

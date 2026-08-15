using System.Net;
using System.Text.Json;

namespace OrbitBackend.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Failed to send email"))
            {
                _logger.LogError(ex, "Email sending failed.");
                await WriteResponseAsync(context, HttpStatusCode.BadGateway, new
                {
                    message = "Could not send the email. Please try again later.",
                    detail = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                await WriteResponseAsync(context, HttpStatusCode.BadRequest, new
                {
                    message = ex.Message
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                await WriteResponseAsync(context, HttpStatusCode.Unauthorized, new
                {
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred.");
                await WriteResponseAsync(context, HttpStatusCode.InternalServerError, new
                {
                    message = "An unexpected error occurred."
                });
            }
        }

        private static async Task WriteResponseAsync(HttpContext context, HttpStatusCode statusCode, object body)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }
}

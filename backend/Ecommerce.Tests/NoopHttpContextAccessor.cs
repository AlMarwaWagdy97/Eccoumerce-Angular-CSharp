using Microsoft.AspNetCore.Http;

namespace Ecommerce.Tests;

public class NoopHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}

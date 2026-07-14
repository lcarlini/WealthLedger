using System.Collections;
using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Models;

public class Response<T> : JsonResult
{
    private const string DataCountHeader = "X-Total-Count";
    private const string DataTypeHeader = "X-Entity-Type";

    public Response(IEnumerable<Error> errors)
        : this(default, errors, HttpStatusCode.InternalServerError) { }

    public Response(IEnumerable<Error> errors, HttpStatusCode statusCode)
        : this(default, errors, statusCode) { }

    public Response(T? data)
        : this(data, null, HttpStatusCode.OK) { }

    private Response(T? data, IEnumerable<Error>? errors, HttpStatusCode statusCode)
        : base(null)
    {
        Data = data;
        Errors = errors?.ToList() ?? [];
        Value = new { Data, Errors };
        StatusCode = (int)statusCode;
    }

    public T? Data { get; }
    public List<Error> Errors { get; }

    public static implicit operator Response<T>(T data) => new(data);
    public static implicit operator Response<T>(Error error) => new([error]);
    public static implicit operator Response<T>(string errorMessage) => new([new Error(errorMessage)]);
    public static implicit operator Response<T>(Exception exception) => new([new Error(exception.Message)]);

    public override async Task ExecuteResultAsync(ActionContext context)
    {
        context.HttpContext.Response.Headers[DataTypeHeader] = typeof(T).FullName;

        if (Data is IEnumerable data)
        {
            var count = data.OfType<object>().Count();
            context.HttpContext.Response.Headers[DataCountHeader] = count.ToString();
        }

        await base.ExecuteResultAsync(context);
    }
}

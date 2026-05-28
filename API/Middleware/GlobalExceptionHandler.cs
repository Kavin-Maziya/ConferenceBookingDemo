using API.Exceptions; 
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace API.Middleware; 
//IExceptionHandler is the interface for typed exception handling 
//AddEceptionHandler<T>() registers it; UseExceptionHandler() activates it. 
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger): IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        //1. Record the error before doing anything else (Log it)
        logger.LogError(exception, "An expection occured: {Message}",exception.Message); 
       //2. Translate(Map) the domain exception to an HTTO status code

       var statusCode = exception switch
       {
           BookingNotFoundException => StatusCodes.Status404NotFound,
           DuplicateBookingException => StatusCodes.Status409Conflict,
           _ => StatusCodes.Status500InternalServerError       
       }; 
       //3. Construct Problem Details shape 
       var problemDetails = new ProblemDetails
       {
           Status = statusCode,
           Title = GetTitle(statusCode),
           Detail = exception.Message,
           Instance = httpContext.Request.Path
       };
     
      //4 Write the status code and JSON body
      httpContext.Response.StatusCode = statusCode;
      httpContext.Response.ContentType = "application/problem+json";
      await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken); 

      return true; 



    }
    //Helper method to get Problem details title
    private static string GetTitle(int statusCode) => statusCode switch
    {
        StatusCodes.Status404NotFound => "Resource Not Found",
        StatusCodes.Status409Conflict => "Resource Conflict", 
        _                             => "Internal server error"
    };

}
using Microsoft.AspNetCore.Mvc;
using NetCoreEventBus.Web.Extensions;
using NetCoreEventBus.Web.Resources;

namespace NetCoreEventBus.Web.Controllers.Configurations
{
    public static class InvalidModelStateResponseFactory
    {
        public static IActionResult ProduceErrorResponse(ActionContext context)
        {
            var error = context.ModelState.GetErrorMessage();
            var response = new ErrorResource(message: error);

            return new BadRequestObjectResult(response);
        }
    }
}

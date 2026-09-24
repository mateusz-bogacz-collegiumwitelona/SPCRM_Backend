using Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.OutputCaching;
using System.Reflection;

namespace Api.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class InvalidateCacheAttribute : ActionFilterAttribute
    {
        private readonly string[] _tags;

        public InvalidateCacheAttribute(params string[] tags)
        {
            _tags = tags ?? Array.Empty<string>();
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executedContext = await next();

            if (executedContext.Exception == null &&
                executedContext.Result is ObjectResult objectResult &&
                objectResult.StatusCode is >= 200 and < 300)
            {
                var cacheStore = context.HttpContext.RequestServices.GetRequiredService<IOutputCacheStore>();
                var tagsToEvict = ResolveTags(_tags);

                foreach (var tag in tagsToEvict)
                {
                    await cacheStore.EvictByTagAsync(tag, context.HttpContext.RequestAborted);
                }
            }
        }

        private static IEnumerable<string> ResolveTags(string[] items)
        {
            foreach (var item in items)
            {
                var field = typeof(CacheTags).GetField(item, BindingFlags.Public | BindingFlags.Static);

                if (field != null && field.GetValue(null) is string[] array)
                {
                    foreach (var tag in array)
                    {
                        yield return tag;
                    }
                }
                else
                {
                    yield return item;
                }
            }
        }
    }
}

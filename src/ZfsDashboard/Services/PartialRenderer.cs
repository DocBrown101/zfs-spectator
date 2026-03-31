using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ZfsDashboard.Services;

public interface IPartialRenderer
{
    Task<string> RenderAsync<TModel>(ActionContext actionContext, string partialName, TModel model);
}

public class PartialRenderer(IRazorViewEngine viewEngine, ITempDataProvider tempDataProvider) : IPartialRenderer
{
    public async Task<string> RenderAsync<TModel>(ActionContext actionContext, string partialName, TModel model)
    {
        var viewResult = viewEngine.FindView(actionContext, partialName, false);
        if (!viewResult.Success)
            viewResult = viewEngine.GetView(null, partialName, false);
        if (!viewResult.Success)
            throw new InvalidOperationException($"Partial view '{partialName}' not found.");

        await using var writer = new StringWriter();
        var viewData = new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model,
        };
        var viewContext = new ViewContext(
            actionContext,
            viewResult.View,
            viewData,
            new TempDataDictionary(actionContext.HttpContext, tempDataProvider),
            writer,
            new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}

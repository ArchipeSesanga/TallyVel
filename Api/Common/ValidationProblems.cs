using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api.Common;

/// <summary>
/// The one way a validation failure becomes a 400: a ValidationProblemDetails
/// whose "errors" keys are the camelCase field names the client sent
/// ("amount", not "Amount"), always built by ProblemDetailsFactory so it
/// carries type, status and correlationId like every other problem.
/// </summary>
public static class ValidationProblems
{
    /// <summary>Controllers: <c>return ValidationProblem(result.ToModelState());</c></summary>
    public static ModelStateDictionary ToModelState(this ValidationResult result)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in result.Errors)
            modelState.AddModelError(FieldName(error.PropertyName), error.ErrorMessage);
        return modelState;
    }

    /// <summary>
    /// The automatic [ApiController] 400 (malformed JSON, a string where a
    /// number belongs, a bad query value) in the same shape: "$.amount"
    /// becomes "amount", and the body parameter's own "field is required"
    /// entry is dropped when the real cause is a field inside the body.
    /// Built through ProblemDetailsFactory, so correlationId is added.
    /// </summary>
    public static IActionResult FromModelState(ActionContext context)
    {
        var bodyParameters = context.ActionDescriptor.Parameters
            .Where(p => p.BindingInfo?.BindingSource == BindingSource.Body)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasJsonErrors = context.ModelState.Keys.Any(k => k.StartsWith('$'));

        var normalised = new ModelStateDictionary();
        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0 || (hasJsonErrors && bodyParameters.Contains(key)))
                continue;

            var field = key switch
            {
                "$" => "body",
                _ when key.StartsWith("$.") => key[2..],
                _ => FieldName(key),
            };
            foreach (var error in entry.Errors)
                normalised.AddModelError(field, error.ErrorMessage);
        }

        var problem = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(context.HttpContext, normalised, StatusCodes.Status400BadRequest);
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    // "Members[0].UserId" -> "members[0].userId": each segment camelCased.
    public static string FieldName(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}

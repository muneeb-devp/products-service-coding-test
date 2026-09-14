using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Products.Api.Contracts;
using Products.Api.Infrastructure;
using Products.Application.Common.Models;
using Products.Application.Products.Commands.CreateProduct;
using Products.Application.Products.Commands.DeleteProduct;
using Products.Application.Products.Commands.UpdateProduct;
using Products.Application.Products.Dtos;
using Products.Application.Products.Queries.GetProductById;
using Products.Application.Products.Queries.GetProducts;
using Products.Domain.Products;

namespace Products.Api.Controllers;

/// <summary>
/// The product catalogue.
/// </summary>
/// <remarks>
/// Every action on this controller requires a bearer token. The controller is
/// intentionally thin — it translates HTTP to a MediatR message and back, and
/// contains no business logic. Errors are not caught here: the global exception
/// handler owns the mapping from exception to status code, so that mapping
/// exists once rather than in every action.
/// <para>
/// Two routes are registered. <c>/api/products</c> matches the contract in the
/// brief and resolves to the default version; <c>/api/v1/products</c> is the
/// explicit form. Both reach the same code, so clients can pin a version
/// without the unversioned URL breaking.
/// </para>
/// </remarks>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/products")]
[Route("api/v{version:apiVersion}/products")]
[Produces("application/json")]
public sealed class ProductsController(ISender sender) : ControllerBase
{
    /// <summary>Returns a page of products, optionally filtered by colour.</summary>
    /// <param name="colour">
    /// Optional colour filter, e.g. <c>Red</c>. Omit to return every colour.
    /// Accepts <c>color</c> as an alias for callers using American spelling.
    /// </param>
    /// <param name="color">Alias for <paramref name="colour"/>.</param>
    /// <param name="page">1-based page number. Defaults to 1.</param>
    /// <param name="pageSize">Items per page. Defaults to 20, maximum 100.</param>
    /// <param name="sortBy">
    /// Field to sort by: <c>name</c>, <c>price</c>, <c>colour</c>, <c>sku</c>,
    /// <c>createdAt</c> or <c>updatedAt</c>. Defaults to <c>createdAt</c>.
    /// </param>
    /// <param name="sortDescending">Sort descending. Defaults to false.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">A page of products. An empty page is a valid result.</response>
    /// <response code="400">A paging, sorting or filter parameter was invalid.</response>
    /// <response code="401">No valid bearer token was supplied.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResponse<ProductDto>>> GetProducts(
        [FromQuery] ProductColour? colour,
        [FromQuery] ProductColour? color,
        [FromQuery] int page = PagingDefaults.Page,
        [FromQuery] int pageSize = PagingDefaults.PageSize,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        // The brief spells it "colour"; JSON APIs more often use "color".
        // Accepting both costs one line and avoids a confusing empty result for
        // a caller who guessed the other spelling.
        var result = await sender.Send(
            new GetProductsQuery(page, pageSize, colour ?? color, sortBy, sortDescending),
            cancellationToken);

        return Ok(result.ToResponse());
    }

    /// <summary>Returns products of one colour.</summary>
    /// <remarks>
    /// A convenience route for the requirement "retrieve all products of a
    /// specific colour". It delegates to the same query as
    /// <see cref="GetProducts"/> with the filter set, so there is one
    /// implementation of the filtering, paging and sorting rather than two that
    /// can drift apart.
    /// </remarks>
    /// <param name="colour">The colour to filter by, e.g. <c>Red</c>.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="sortBy">Field to sort by.</param>
    /// <param name="sortDescending">Sort descending.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">A page of products in that colour, possibly empty.</response>
    /// <response code="400">The colour or a paging parameter was invalid.</response>
    /// <response code="401">No valid bearer token was supplied.</response>
    [HttpGet("colour/{colour}")]
    [HttpGet("color/{colour}")]
    [ProducesResponseType(typeof(PagedResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResponse<ProductDto>>> GetProductsByColour(
        ProductColour colour,
        [FromQuery] int page = PagingDefaults.Page,
        [FromQuery] int pageSize = PagingDefaults.PageSize,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetProductsQuery(page, pageSize, colour, sortBy, sortDescending),
            cancellationToken);

        return Ok(result.ToResponse());
    }

    /// <summary>Returns a single product.</summary>
    /// <param name="id">The product identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The product.</response>
    /// <response code="401">No valid bearer token was supplied.</response>
    /// <response code="404">No product exists with that id.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetProductById(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetProductByIdQuery(id), cancellationToken));

    /// <summary>Creates a product.</summary>
    /// <param name="request">The product to create.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="201">Created. The <c>Location</c> header points at the new product.</response>
    /// <response code="400">The payload failed validation; the body lists every failure.</response>
    /// <response code="401">No valid bearer token was supplied.</response>
    /// <response code="409">A product with that SKU already exists.</response>
    /// <response code="429">Too many write requests; retry after a short delay.</response>
    [HttpPost]
    [EnableRateLimiting(RateLimitingPolicies.Writes)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ProductDto>> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await sender.Send(request.ToCommand(), cancellationToken);

        // 201 with a Location header pointing at the canonical URL of the new
        // resource, as RFC 9110 requires — not a bare 200 with a body.
        //
        // CreatedAtAction rather than CreatedAtRoute: the controller exposes both
        // the unversioned and versioned route templates, and a *named* route must
        // map to exactly one template. Resolving by action name lets routing pick
        // the template matching how the caller reached us.
        return CreatedAtAction(
            actionName: nameof(GetProductById),
            routeValues: new { id = product.Id },
            value: product);
    }

    /// <summary>Replaces a product's mutable attributes.</summary>
    /// <param name="id">The product to update.</param>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The updated product.</response>
    /// <response code="400">The payload failed validation.</response>
    /// <response code="401">No valid bearer token was supplied.</response>
    /// <response code="404">No product exists with that id.</response>
    /// <response code="429">Too many write requests.</response>
    [HttpPut("{id:guid}")]
    [EnableRateLimiting(RateLimitingPolicies.Writes)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ProductDto>> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(request.ToCommand(id), cancellationToken));

    /// <summary>Deletes a product.</summary>
    /// <param name="id">The product to delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="401">No valid bearer token was supplied.</response>
    /// <response code="404">No product exists with that id.</response>
    /// <response code="429">Too many write requests.</response>
    [HttpDelete("{id:guid}")]
    [EnableRateLimiting(RateLimitingPolicies.Writes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteProductCommand(id), cancellationToken);
        return NoContent();
    }
}

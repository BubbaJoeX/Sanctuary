using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Sanctuary.Game;
using Sanctuary.Game.Authoring;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Admin;

/// <summary>
/// The coin store as the game has it loaded, for the admin panel's Store page: its categories, what each sells and at
/// what price, and its bundles. The panel changes the files through the zone editor (CoinStoreItems.json, prices in
/// ClientItemDefinitions.json, CoinStoreBundles.json) and then reloads "items" and "coinstore". Custom items (Item
/// Gen's, 9000000 and up) are made and changed here, in the database.
/// </summary>
public static partial class AdminApiEndpoints
{
    private const int DefaultStoreTake = 120;
    private const int MaxStoreTake = 500;
    private const int MaxCustomItems = 500;

    private static void MapStoreEndpoints(RouteGroupBuilder referee)
    {
        referee.MapGet("/store/categories", StoreCategoriesHandler);
        referee.MapGet("/store/items", StoreItemsHandler);
        referee.MapGet("/store/bundles", StoreBundlesHandler);

        referee.MapGet("/custom-items", CustomItemsHandler);
        referee.MapPost("/custom-items", CreateCustomItemHandler);
        referee.MapGet("/custom-items/{id:int}", CustomItemHandler);
        referee.MapPut("/custom-items/{id:int}", UpdateCustomItemHandler);
        referee.MapDelete("/custom-items/{id:int}", DeleteCustomItemHandler);
    }

    #region Store

    /// <summary>
    /// The store's categories (ItemCategories.txt), the groups that hold them, and how many store items each has.
    /// Names are client string ids: the panel reads them through the zone editor.
    /// </summary>
    private static StoreCategoriesModel StoreCategoriesHandler(IResourceManager resourceManager)
    {
        var counts = resourceManager.CoinStoreItems.Values
            .GroupBy(item => item.CategoryId)
            .ToDictionary(group => group.Key, group => group.Count());

        var groups = resourceManager.ItemCategoryGroups
            .Select(group => new StoreCategoryGroupModel(group.Key, [.. group.Value.Select(x => x.CategoryId)]))
            .OrderBy(group => group.Id)
            .ToArray();

        var categories = resourceManager.ItemCategories.Values
            .Select(category => new StoreCategoryModel(category.Id, category.NameId, category.IconId, category.Display != 0,
                counts.GetValueOrDefault(category.Id)))
            .OrderBy(category => category.Id)
            .ToArray();

        // Store items in a category the file doesn't list still show, under their id.
        var unlisted = counts.Keys.Where(id => !resourceManager.ItemCategories.ContainsKey(id))
            .Select(id => new StoreCategoryModel(id, 0, 0, false, counts[id]));

        return new StoreCategoriesModel([.. categories, .. unlisted], groups, resourceManager.CoinStoreItems.Count,
            resourceManager.CoinStoreBundles.Count);
    }

    /// <summary>
    /// What the store sells, in a category or matching a search (name or id), cheapest first within a category. With
    /// all, every item matching the search, sold or not, so one can be added.
    /// </summary>
    private static StoreItemsModel StoreItemsHandler(int? category, string? search, bool? all, int? skip, int? take,
        IResourceManager resourceManager)
    {
        var store = resourceManager.CoinStoreItems;
        var text = search?.Trim() ?? string.Empty;
        var searchId = int.TryParse(text, out var parsed) ? parsed : (int?)null;
        IEnumerable<ClientItemDefinition> definitions;

        if (all == true)
        {
            if (string.IsNullOrWhiteSpace(search))
                return new StoreItemsModel(0, []);

            definitions = resourceManager.ClientItemDefinitions.Values;
        }
        else
        {
            definitions = store.Values
                .Where(item => category is null || item.CategoryId == category)
                .Select(item => resourceManager.ClientItemDefinitions.TryGetValue(item.Id, out var definition) ? definition : null)
                .OfType<ClientItemDefinition>();
        }

        if (text.Length > 0)
        {
            definitions = definitions.Where(definition => definition.Id == searchId
                || ItemName(definition).Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        // The item with the id searched for first, then names that start with the search, then by price.
        var matched = definitions
            .OrderBy(definition => definition.Id != searchId)
            .ThenBy(definition => category is null && text.Length > 0 && !ItemName(definition).StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .ThenBy(definition => definition.GetMemberPurchasePrice())
            .ThenBy(definition => definition.Id)
            .ToList();

        var page = matched
            .Skip(Math.Max(0, skip ?? 0))
            .Take(Math.Clamp(take ?? DefaultStoreTake, 1, MaxStoreTake))
            .Select(definition => ToStoreItem(definition, resourceManager))
            .ToArray();

        return new StoreItemsModel(matched.Count, page);
    }

    private static StoreBundleModel[] StoreBundlesHandler(IResourceManager resourceManager) =>
        [.. resourceManager.CoinStoreBundles.Values
            .OrderBy(bundle => bundle.Id)
            .Select(bundle =>
            {
                resourceManager.ClientItemDefinitions.TryGetValue(bundle.Id, out var face);

                return new StoreBundleModel(
                    bundle.Id,
                    face is null ? $"item {bundle.Id}" : ItemName(face),
                    bundle.Comment,
                    face?.GetMemberPurchasePrice() ?? 0,
                    face?.Icon.Id ?? 0,
                    face?.Icon.TintId ?? 0,
                    resourceManager.CoinStoreItems.TryGetValue(bundle.Id, out var entry) ? entry.CategoryId : null,
                    [.. bundle.Items.Select(item =>
                    {
                        resourceManager.ClientItemDefinitions.TryGetValue(item.Id, out var definition);

                        return new StoreBundleItemModel(item.Id, definition is null ? $"item {item.Id}" : ItemName(definition), item.Quantity,
                            item.Tint, definition?.Icon.Id ?? 0, definition?.Icon.TintId ?? 0);
                    })],
                    bundle.Coins);
            })];

    private static StoreItemModel ToStoreItem(ClientItemDefinition definition, IResourceManager resourceManager)
    {
        resourceManager.CoinStoreItems.TryGetValue(definition.Id, out var entry);

        return new StoreItemModel(
            definition.Id,
            ItemName(definition),
            entry is not null,
            entry?.CategoryId ?? definition.CategoryId,
            entry?.TintGroupId ?? 0,
            entry?.BuyDisabled ?? false,
            entry?.CoinStoreOnly ?? false,
            entry?.Hidden ?? false,
            definition.Cost,
            definition.MemberDiscount,
            definition.GetMemberPurchasePrice(),
            definition.ResellValue,
            definition.Icon.Id,
            definition.Icon.TintId,
            definition.Type,
            definition.MaxStackSize,
            CustomItemManager.IsCustomItemId(definition.Id),
            resourceManager.CoinStoreBundles.ContainsKey(definition.Id));
    }

    #endregion

    #region Custom items

    private static IResult CustomItemsAdminsOnly() =>
        Results.Problem("Only admins can make and change custom items.", statusCode: StatusCodes.Status403Forbidden);

    /// <summary>
    /// Item Gen's items: copies of a game item with fields overridden, as !itemgen makes them.
    /// </summary>
    private static CustomItemModel[] CustomItemsHandler(string? search, ICustomItemManager customItemManager, IResourceManager resourceManager) =>
        [.. customItemManager.GetAll(string.IsNullOrWhiteSpace(search) ? null : search.Trim())
            .OrderByDescending(item => item.Updated)
            .Take(MaxCustomItems)
            .Select(item => ToCustomItem(item, customItemManager, resourceManager))];

    private static IResult CustomItemHandler(int id, ICustomItemManager customItemManager, IResourceManager resourceManager) =>
        customItemManager.TryGet(ToCustomRow(id), out var item)
            ? Results.Ok(ToCustomItem(item, customItemManager, resourceManager))
            : Results.NotFound($"There's no custom item {id}.");

    private static IResult CreateCustomItemHandler(HttpContext context, CustomItemCreateRequest request, ICustomItemManager customItemManager,
        IResourceManager resourceManager, RefereeDirectory directory)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return CustomItemsAdminsOnly();

        var item = customItemManager.Create(request.Base, request.Label?.Trim() ?? string.Empty, directory.FindOnlineCharacter(referee)?.CharacterId,
            out var error);

        if (item is null)
            return Results.BadRequest(error);

        foreach (var (field, value) in request.Fields ?? [])
        {
            if (customItemManager.Set(item.Id, field, value, null) is { } fieldError)
                return Results.BadRequest($"Made item {CustomItemManager.ItemIdBase + item.Id}, but {field}: {fieldError}");
        }

        Audit(referee, "CreateCustomItem", item.Label, $"Item: {CustomItemManager.ItemIdBase + item.Id}, Base: {request.Base}");

        customItemManager.TryGet(item.Id, out item);

        return Results.Ok(ToCustomItem(item, customItemManager, resourceManager));
    }

    /// <summary>
    /// Sets fields (null clears an override, as "clear" does in game) and the label.
    /// </summary>
    private static IResult UpdateCustomItemHandler(HttpContext context, int id, Dictionary<string, string?> fields, ICustomItemManager customItemManager,
        IResourceManager resourceManager)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return CustomItemsAdminsOnly();

        var row = ToCustomRow(id);

        if (!customItemManager.TryGet(row, out _))
            return Results.NotFound($"There's no custom item {id}.");

        foreach (var (field, value) in fields)
        {
            if (customItemManager.Set(row, field.ToLowerInvariant(), value ?? "clear", null) is { } error)
                return Results.BadRequest($"{field}: {error}");
        }

        Audit(referee, "EditCustomItem", null, $"Item: {CustomItemManager.ItemIdBase + row}, {string.Join(", ", fields.Select(x => $"{x.Key}={x.Value ?? "clear"}"))}");

        customItemManager.TryGet(row, out var item);

        return Results.Ok(ToCustomItem(item, customItemManager, resourceManager));
    }

    private static IResult DeleteCustomItemHandler(HttpContext context, int id, ICustomItemManager customItemManager, IResourceManager resourceManager)
    {
        var referee = GetReferee(context);

        if (!referee.IsAdmin)
            return CustomItemsAdminsOnly();

        var itemId = CustomItemManager.ItemIdBase + ToCustomRow(id);

        if (resourceManager.CoinStoreItems.ContainsKey(itemId) || resourceManager.CoinStoreBundles.ContainsKey(itemId))
            return Results.Problem($"Item {itemId} is in the coin store; take it out first.", statusCode: StatusCodes.Status409Conflict);

        if (!customItemManager.TryDelete(ToCustomRow(id), out var error))
            return Results.Problem(error, statusCode: StatusCodes.Status409Conflict);

        Audit(referee, "DeleteCustomItem", null, $"Item: {itemId}");

        return Results.NoContent();
    }

    /// <summary>
    /// A custom item's row, from its item id (9000001 and up) or the row itself.
    /// </summary>
    private static int ToCustomRow(int id) => CustomItemManager.IsCustomItemId(id) ? id - CustomItemManager.ItemIdBase : id;

    private static CustomItemModel ToCustomItem(Sanctuary.Database.Entities.DbCustomItem item, ICustomItemManager customItemManager,
        IResourceManager resourceManager)
    {
        var itemId = CustomItemManager.ItemIdBase + item.Id;

        resourceManager.ClientItemDefinitions.TryGetValue(itemId, out var definition);
        resourceManager.ClientItemDefinitions.TryGetValue(item.BaseItemId, out var baseDefinition);

        return new CustomItemModel(
            itemId,
            item.Label,
            item.BaseItemId,
            baseDefinition is null ? $"item {item.BaseItemId}" : ItemName(baseDefinition),
            definition is null ? item.Label : ItemName(definition),
            definition?.Icon.Id ?? 0,
            definition?.Icon.TintId ?? 0,
            definition?.GetMemberPurchasePrice() ?? 0,
            [.. customItemManager.DescribeFields(item).Select(x => new CustomItemFieldModel(x.Field, x.Value, x.Overridden))],
            item.Updated);
    }

    #endregion
}

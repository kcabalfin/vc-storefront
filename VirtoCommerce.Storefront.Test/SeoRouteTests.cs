﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest;
using Moq;
using VirtoCommerce.Storefront.AutoRestClients.CatalogModuleApi;
using VirtoCommerce.Storefront.AutoRestClients.CoreModuleApi;
using VirtoCommerce.Storefront.Common;
using VirtoCommerce.Storefront.Model;
using VirtoCommerce.Storefront.Model.Common;
using VirtoCommerce.Storefront.Model.StaticContent;
using VirtoCommerce.Storefront.Model.Stores;
using VirtoCommerce.Storefront.Routing;
using Xunit;
using catalogDto = VirtoCommerce.Storefront.AutoRestClients.CatalogModuleApi.Models;
using coreDto = VirtoCommerce.Storefront.AutoRestClients.CoreModuleApi.Models;

namespace VirtoCommerce.Storefront.Test
{
    [Trait("Category", "CI")]
    public class SeoRouteTests
    {
        private readonly List<catalogDto.Category> _categories;
        private readonly List<catalogDto.Product> _products;
        private readonly List<catalogDto.SeoInfo> _catalogSeoRecords;
        private readonly List<ContentPage> _pages;
        private readonly coreDto.SeoInfo[] _coreSeoRecords;

        // Catalog structure
        //
        // Slug letters:
        //     a = active
        //     i = inactive
        //     c = category
        //     p = product
        //     v = vendor
        //     g = page
        //     d = duplicate
        //
        // c
        // L- c1            ac1, ic1
        //     |- c2        ac2
        //     L- c3        acd, ic3
        //         |- c4    acd, ic4
        //         |- p1    ap1, ipd, ip1
        //         L- p2    ap2, ipd

        public SeoRouteTests()
        {
            _categories = new List<catalogDto.Category>();
            _products = new List<catalogDto.Product>();
            _pages = new List<ContentPage>();

            _catalogSeoRecords = new List<catalogDto.SeoInfo>
            {
                new catalogDto.SeoInfo { ObjectType = "Vendor", ObjectId = "v1", SemanticUrl = "av1", IsActive = true },
                new catalogDto.SeoInfo { ObjectType = "Vendor", ObjectId = "v1", SemanticUrl = "iv1", IsActive = false },
            };

            var c = new catalogDto.Catalog { Id = "c" };

            var c1 = AddCategory(c, "c1", "ac1", "ic1");
            var c2 = AddCategory(c, "c2", "ac2");
            var c3 = AddCategory(c, "c3", "acd", "ic3");
            var c4 = AddCategory(c, "c4", "acd", "ic4");
            var p1 = AddProduct(c, "p1", "ap1", "ipd", "ip1");
            var p2 = AddProduct(c, "p2", "ap2", "ipd");

            c1.Outlines.Add(CreateOutline(c, new[] { c1 }));
            c2.Outlines.Add(CreateOutline(c, new[] { c1, c2 }));
            c3.Outlines.Add(CreateOutline(c, new[] { c1, c3 }));
            c4.Outlines.Add(CreateOutline(c, new[] { c1, c3, c4 }));
            p1.Outlines.Add(CreateOutline(c, new[] { c1, c3 }, p1));
            p2.Outlines.Add(CreateOutline(c, new[] { c1, c3 }, p2));

            AddPage("en-US", "ag1", "ig1");

            _coreSeoRecords = _catalogSeoRecords.Select(s => s.JsonConvert<coreDto.SeoInfo>()).ToArray();
        }

        [Theory]
        [InlineData(SeoLinksType.Short, "ac1", null, "CatalogSearch", "CategoryBrowsing", "categoryId", "c1")]
        [InlineData(SeoLinksType.Short, "ic1", "ac1", null, null, null, null)]
        [InlineData(SeoLinksType.Short, "ac1/ac2", "ac2", null, null, null, null)]
        [InlineData(SeoLinksType.Short, "ic1/ac2", "ac2", null, null, null, null)]
        [InlineData(SeoLinksType.Short, "ac2", null, "CatalogSearch", "CategoryBrowsing", "categoryId", "c2")]
        [InlineData(SeoLinksType.Short, "ac1/acd", null, "Asset", "HandleStaticFiles", null, null)]
        [InlineData(SeoLinksType.Short, "ac1/ic3", "acd", null, null, null, null)]
        [InlineData(SeoLinksType.Short, "acd", null, "CatalogSearch", "CategoryBrowsing", "categoryId", "c3")]
        [InlineData(SeoLinksType.Short, "ic3", "acd", null, null, null, null)]
        [InlineData(SeoLinksType.Short, "ac1/acd/acd", null, "Asset", "HandleStaticFiles", null, null)]
        [InlineData(SeoLinksType.Short, "ac1/ic3/ic4", "acd", null, null, null, null)]
        [InlineData(SeoLinksType.Short, "ac1/ic3/acd", null, "Asset", "HandleStaticFiles", null, null)]
        [InlineData(SeoLinksType.Short, "ac1/acd/ap1", "ap1", null, null, null, null)]
        [InlineData(SeoLinksType.Short, "ac1/acd/ip1", "ap1", null, null, null, null)]
        [InlineData(SeoLinksType.Short, "ap1", null, "Product", "ProductDetails", "productId", "p1")]
        [InlineData(SeoLinksType.Short, "ip1", "ap1", null, null, null, null)]
        [InlineData(SeoLinksType.Short, "ipd", null, "Asset", "HandleStaticFiles", null, null)]
        [InlineData(SeoLinksType.Short, "ac1/acd/ipd", null, "Asset", "HandleStaticFiles", null, null)]
        [InlineData(SeoLinksType.Short, "av1", null, "Vendor", "VendorDetails", "vendorId", "v1")]
        [InlineData(SeoLinksType.Short, "iv1", "av1", null, null, null, null)]
        [InlineData(SeoLinksType.Short, "ag1", null, "Page", "GetContentPage", null, null)]
        [InlineData(SeoLinksType.Short, "ig1", "ag1", null, null, null, null)]
        [InlineData(SeoLinksType.Collapsed, "ac1", null, "CatalogSearch", "CategoryBrowsing", "categoryId", "c1")]
        [InlineData(SeoLinksType.Collapsed, "ic1", "ac1", null, null, null, null)]
        [InlineData(SeoLinksType.Collapsed, "ac1/ac2", null, "CatalogSearch", "CategoryBrowsing", "categoryId", "c2")]
        [InlineData(SeoLinksType.Collapsed, "ic1/ac2", "ac1/ac2", null, null, null, null)]
        [InlineData(SeoLinksType.Collapsed, "ac2", "ac1/ac2", null, null, null, null)]
        [InlineData(SeoLinksType.Collapsed, "ac1/acd", null, "CatalogSearch", "CategoryBrowsing", "categoryId", "c3")]
        [InlineData(SeoLinksType.Collapsed, "ac1/ic3", "ac1/acd", null, null, null, null)]
        [InlineData(SeoLinksType.Collapsed, "acd", null, "Asset", "HandleStaticFiles", null, null)]
        [InlineData(SeoLinksType.Collapsed, "ic3", "ac1/acd", null, null, null, null)]
        [InlineData(SeoLinksType.Collapsed, "ac1/acd/acd", null, "CatalogSearch", "CategoryBrowsing", "categoryId", "c4")]
        [InlineData(SeoLinksType.Collapsed, "ac1/ic3/ic4", "ac1/acd/acd", null, null, null, null)]
        [InlineData(SeoLinksType.Collapsed, "ac1/ic3/acd", null, "Asset", "HandleStaticFiles", null, null)]
        [InlineData(SeoLinksType.Collapsed, "ac1/acd/ap1", null, "Product", "ProductDetails", "productId", "p1")]
        [InlineData(SeoLinksType.Collapsed, "ac1/acd/ip1", "ac1/acd/ap1", null, null, null, null)]
        [InlineData(SeoLinksType.Collapsed, "ap1", "ac1/acd/ap1", null, null, null, null)]
        [InlineData(SeoLinksType.Collapsed, "ip1", "ac1/acd/ap1", null, null, null, null)]
        [InlineData(SeoLinksType.Collapsed, "ipd", null, "Asset", "HandleStaticFiles", null, null)]
        [InlineData(SeoLinksType.Collapsed, "ac1/acd/ipd", null, "Asset", "HandleStaticFiles", null, null)]
        [InlineData(SeoLinksType.Collapsed, "av1", null, "Vendor", "VendorDetails", "vendorId", "v1")]
        [InlineData(SeoLinksType.Collapsed, "iv1", "av1", null, null, null, null)]
        [InlineData(SeoLinksType.Collapsed, "ag1", null, "Page", "GetContentPage", null, null)]
        [InlineData(SeoLinksType.Collapsed, "ig1", "ag1", null, null, null, null)]
        public void ValidateSeoRouteResponse(SeoLinksType linksType, string seoPath, string expectedRedirectLocation, string expectedController, string expectedAction, string expectedObjectIdName, string expectedObjectId)
        {
            // Don't use catalog API client for short SEO links
            var catalogApi = linksType == SeoLinksType.Collapsed ? CreateCatalogApiClient() : null;
            var service = CreateSeoRouteService(catalogApi);

            var workContext = CreateWorkContext(linksType);
            var response = service.HandleSeoRequest(seoPath, workContext);

            Assert.NotNull(response);
            Assert.Equal(expectedRedirectLocation, response.RedirectLocation);

            if (expectedRedirectLocation != null)
            {
                Assert.True(response.Redirect);
            }
            else
            {
                Assert.False(response.Redirect);
                Assert.NotNull(response.RouteData);
                Assert.Equal(expectedController, response.RouteData["controller"]);
                Assert.Equal(expectedAction, response.RouteData["action"]);

                if (expectedObjectIdName != null)
                {
                    Assert.Equal(expectedObjectId, response.RouteData[expectedObjectIdName]);
                }

                if (expectedController == "Page")
                {
                    Assert.NotNull(response.RouteData["page"]);
                }
            }
        }


        private WorkContext CreateWorkContext(SeoLinksType linksType)
        {
            var store = CreateStore("s", "c", linksType, "en-US", "ru-RU");

            return new WorkContext
            {
                CurrentStore = store,
                CurrentLanguage = store.DefaultLanguage,
                Pages = new MutablePagedList<ContentItem>(_pages),
            };
        }

        private static Store CreateStore(string storeId, string catalogId, SeoLinksType linksType, params string[] cultureNames)
        {
            var result = new Store
            {
                Id = storeId,
                Catalog = catalogId,
                SeoLinksType = linksType,
                Languages = cultureNames.Select(n => new Language(n)).ToList(),
            };

            result.DefaultLanguage = result.Languages.FirstOrDefault();

            return result;
        }

        private catalogDto.Category AddCategory(catalogDto.Catalog catalog, string categoryId, params string[] slugs)
        {
            var result = new catalogDto.Category
            {
                Id = categoryId,
                Catalog = catalog,
                CatalogId = catalog.Id,
                Outlines = new List<catalogDto.Outline>()
            };

            _categories.Add(result);
            _catalogSeoRecords.AddRange(slugs.Select(s => CreateSeoInfo("Category", categoryId, s)));

            return result;
        }

        private catalogDto.Product AddProduct(catalogDto.Catalog catalog, string productId, params string[] slugs)
        {
            var result = new catalogDto.Product
            {
                Id = productId,
                Catalog = catalog,
                CatalogId = catalog.Id,
                Outlines = new List<catalogDto.Outline>()
            };

            _products.Add(result);
            _catalogSeoRecords.AddRange(slugs.Select(s => CreateSeoInfo("CatalogProduct", productId, s)));

            return result;
        }

        private void AddPage(string cultureName, params string[] urls)
        {
            _pages.Add(new ContentPage
            {
                Language = new Language(cultureName),
                Url = urls[0],
                AliasesUrls = urls.Skip(1).ToList(),
            });
        }

        private catalogDto.Outline CreateOutline(catalogDto.Catalog catalog, catalogDto.Category[] categories, catalogDto.Product product = null)
        {
            var result = new catalogDto.Outline
            {
                Items = new List<catalogDto.OutlineItem> { CreateOutlineItem("Catalog", catalog.Id) }
            };

            result.Items.AddRange(categories.Select(c => CreateOutlineItem("Category", c.Id)));

            if (product != null)
            {
                result.Items.Add(CreateOutlineItem("CatalogProduct", product.Id));
            }

            return result;
        }

        private catalogDto.OutlineItem CreateOutlineItem(string objectType, string objectId)
        {
            return new catalogDto.OutlineItem
            {
                SeoObjectType = objectType,
                Id = objectId,
                SeoInfos = _catalogSeoRecords.Where(s => s.ObjectType == objectType && s.ObjectId == objectId).ToList(),
            };
        }

        private static catalogDto.SeoInfo CreateSeoInfo(string objectType, string objectId, string slug)
        {
            return new catalogDto.SeoInfo
            {
                ObjectType = objectType,
                ObjectId = objectId,
                SemanticUrl = slug,
                IsActive = slug.StartsWith("a"),
            };
        }

        #region Mocks

        private ICatalogModuleApiClient CreateCatalogApiClient()
        {
            var catalogApi = new Mock<ICatalogModuleApiClient>();
            catalogApi
                .Setup(x => x.CatalogModuleCategories.GetCategoriesByIdsWithHttpMessagesAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<Dictionary<string, List<string>>>(), It.IsAny<CancellationToken>()))
                .Returns<IList<string>, string, Dictionary<string, List<string>>, CancellationToken>(GetCategoriesByIdsWithHttpMessagesAsync);
            catalogApi
                .Setup(x => x.CatalogModuleProducts.GetProductByIdsWithHttpMessagesAsync(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<Dictionary<string, List<string>>>(), It.IsAny<CancellationToken>()))
                .Returns<IList<string>, string, Dictionary<string, List<string>>, CancellationToken>(GetProductByIdsWithHttpMessagesAsync);

            return catalogApi.Object;
        }

        private ISeoRouteService CreateSeoRouteService(ICatalogModuleApiClient catalogApiClient)
        {
            var cacheManager = new Mock<ILocalCacheManager>();
            //cacheManager.Setup(cache => cache.Get<List<catalogDto.SeoInfo>>(It.IsAny<string>(), It.IsAny<string>())).Returns<List<catalogDto.SeoInfo>>(null);

            var coreApi = new Mock<ICoreModuleApiClient>();
            coreApi
                .Setup(x => x.Commerce.GetSeoInfoBySlugWithHttpMessagesAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, List<string>>>(), It.IsAny<CancellationToken>()))
                .Returns<string, Dictionary<string, List<string>>, CancellationToken>(GetSeoInfoBySlugWithHttpMessagesAsync);


            var result = new SeoRouteService(() => coreApi.Object, () => catalogApiClient, cacheManager.Object);
            return result;
        }

        private Task<HttpOperationResponse<IList<coreDto.SeoInfo>>> GetSeoInfoBySlugWithHttpMessagesAsync(string slug, Dictionary<string, List<string>> customHeaders, CancellationToken cancellationToken)
        {
            var result = new HttpOperationResponse<IList<coreDto.SeoInfo>>
            {
                Body = _coreSeoRecords
                    .Where(s => s.SemanticUrl.EqualsInvariant(slug))
                    .Join(_coreSeoRecords, x => x.ObjectId, y => y.ObjectId, (x, y) => y)
                    .ToList()
            };
            return Task.FromResult(result);
        }

        private Task<HttpOperationResponse<IList<catalogDto.Category>>> GetCategoriesByIdsWithHttpMessagesAsync(IList<string> ids, string respGroup, Dictionary<string, List<string>> customHeaders, CancellationToken cancellationToken)
        {
            var result = new HttpOperationResponse<IList<catalogDto.Category>>
            {
                Body = _categories.Where(c => ids.Contains(c.Id, StringComparer.OrdinalIgnoreCase)).ToList()
            };
            return Task.FromResult(result);
        }

        private Task<HttpOperationResponse<IList<catalogDto.Product>>> GetProductByIdsWithHttpMessagesAsync(IList<string> ids, string respGroup, Dictionary<string, List<string>> customHeaders, CancellationToken cancellationToken)
        {
            var result = new HttpOperationResponse<IList<catalogDto.Product>>
            {
                Body = _products.Where(p => ids.Contains(p.Id, StringComparer.OrdinalIgnoreCase)).ToList()
            };
            return Task.FromResult(result);
        }

        #endregion
    }
}